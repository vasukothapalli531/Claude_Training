# HTML Report Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `--html <file>` flag to the existing Code Scanner CLI that produces a single self-contained HTML report — KPI dashboard, two language charts, severity bar, file breakdown, and expandable findings list — per `docs/superpowers/specs/2026-05-07-html-report-design.md`.

**Architecture:** Three new files under `src/CodeScanner/Html/`. `Template.cs` is a const HTML string with `{{placeholder}}` markers. `TemplateRenderer.cs` is a pure helper for placeholder substitution and HTML/JSON-tag escaping. `HtmlReport.cs` orchestrates: takes the scan data, computes the placeholder map, runs substitution, returns the final HTML string. `Cli.cs` gains a single new option that writes the result to a file. No new NuGet dependencies.

**Tech Stack:** .NET 9, C#, xUnit. Browser-side: Chart.js v4 from `cdn.jsdelivr.net` (no other scripts). Hand-rolled CSS, inline. Bootstrap JS reads `<script id="scan-data">` and renders all charts/tables via DOM APIs (no `innerHTML` for user data).

**Repo additions produced by this plan:**

```
hackathon/src/CodeScanner/Html/
  ├── HtmlReport.cs
  ├── Template.cs
  └── TemplateRenderer.cs

hackathon/tests/CodeScanner.Tests/Html/
  ├── HtmlReportTests.cs
  └── TemplateRendererTests.cs
```

Working directory throughout: `C:\Cmm-testing\Claude_Training\hackathon`. Branch this work onto `feat/html-report` before Task 1.

---

## Task 0: Branch off main

- [ ] **Step 0.1: Create feature branch from main**

```powershell
git -C C:/Cmm-testing/Claude_Training switch -c feat/html-report
git -C C:/Cmm-testing/Claude_Training status -sb
```

Expected: `Switched to a new branch 'feat/html-report'`. Branch shows clean.

---

## Task 1: TemplateRenderer + escape helpers

**Files:**
- Create: `hackathon/src/CodeScanner/Html/TemplateRenderer.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Html/TemplateRendererTests.cs`

- [ ] **Step 1.1: Write failing tests**

Create `tests/CodeScanner.Tests/Html/TemplateRendererTests.cs`:

```csharp
namespace CodeScanner.Tests.Html;

public class TemplateRendererTests
{
    [Fact]
    public void Render_SubstitutesAllPlaceholders()
    {
        var template = "<h1>{{title}}</h1><p>{{root}}</p>";
        var result = TemplateRenderer.Render(template, new Dictionary<string, string>
        {
            ["title"] = "Hello",
            ["root"] = "/x",
        });

        Assert.Equal("<h1>Hello</h1><p>/x</p>", result);
    }

    [Fact]
    public void Render_LeavesUnknownPlaceholdersUntouched()
    {
        var template = "{{a}} {{b}}";
        var result = TemplateRenderer.Render(template, new Dictionary<string, string> { ["a"] = "1" });
        Assert.Equal("1 {{b}}", result);
    }

    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("a < b", "a &lt; b")]
    [InlineData("Tom & Jerry", "Tom &amp; Jerry")]
    [InlineData("\"quoted\"", "&quot;quoted&quot;")]
    [InlineData("'single'", "&#39;single&#39;")]
    public void HtmlEscape_HandlesAllCharacters(string input, string expected)
    {
        Assert.Equal(expected, TemplateRenderer.HtmlEscape(input));
    }

    [Fact]
    public void EscapeForScriptTag_ReplacesClosingScriptTag()
    {
        var json = "{\"snippet\":\"</script><script>alert(1)</script>\"}";
        var escaped = TemplateRenderer.EscapeForScriptTag(json);
        Assert.DoesNotContain("</script>", escaped);
        Assert.Contains("<\\/script>", escaped);
    }

    [Fact]
    public void EscapeForScriptTag_LeavesNormalContentAlone()
    {
        var json = "{\"x\": 42, \"y\": \"hello\"}";
        Assert.Equal(json, TemplateRenderer.EscapeForScriptTag(json));
    }
}
```

- [ ] **Step 1.2: Run tests — verify they fail**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet test CodeScanner.sln --nologo
```

Expected: build error "TemplateRenderer does not exist".

- [ ] **Step 1.3: Implement `TemplateRenderer.cs`**

Create `src/CodeScanner/Html/TemplateRenderer.cs`:

```csharp
namespace CodeScanner;

internal static class TemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        var sb = new System.Text.StringBuilder(template);
        foreach (var (key, val) in values)
        {
            sb.Replace("{{" + key + "}}", val);
        }
        return sb.ToString();
    }

    public static string HtmlEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) { return s; }
        return s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Escapes JSON for safe embedding inside an HTML &lt;script&gt; tag by
    /// neutralizing any literal &lt;/script&gt; sequences. Other characters are
    /// already legal inside a JSON string literal.
    /// </summary>
    public static string EscapeForScriptTag(string json)
    {
        if (string.IsNullOrEmpty(json)) { return json; }
        return json.Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 1.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all tests green (prior + 9 new).

- [ ] **Step 1.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Html/TemplateRenderer.cs hackathon/tests/CodeScanner.Tests/Html/TemplateRendererTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(html): add TemplateRenderer with HTML and script-tag escaping"
```

---

## Task 2: Template (const HTML string)

**Files:**
- Create: `hackathon/src/CodeScanner/Html/Template.cs`

- [ ] **Step 2.1: Implement `Template.cs`**

Create `src/CodeScanner/Html/Template.cs`. Note the deliberate `{{...}}` placeholders inside the C# raw string and the JS using `'{{...}}'` style is avoided because all data flows through the embedded JSON.

```csharp
namespace CodeScanner;

internal static class Template
{
    public const string Html = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Code Scanner Report — {{rootPath}}</title>
  <style>
    :root { color-scheme: light; }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: -apple-system, "Segoe UI", system-ui, sans-serif;
      background: #f6f7f9;
      color: #1a2230;
      line-height: 1.5;
    }
    .container { max-width: 1100px; margin: 0 auto; padding: 28px 24px; }
    header h1 { margin: 0 0 4px 0; font-size: 24px; color: #0a1124; letter-spacing: -0.01em; }
    header .meta { color: #5d6b86; font-size: 13px; margin-bottom: 24px; }
    header .meta code { background: #eef2f7; padding: 1px 6px; border-radius: 3px; font-size: 12px; }

    .panel {
      background: #fff; border: 1px solid #e3e7ee; border-radius: 8px;
      padding: 16px 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    .panel h3 { margin: 0 0 12px 0; font-size: 14px; font-weight: 600; color: #1a2230; }

    .kpis { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 18px; }
    .kpi { background: #fff; border: 1px solid #e3e7ee; border-radius: 8px; padding: 14px 16px; box-shadow: 0 1px 3px rgba(0,0,0,0.04); }
    .kpi .num { font-size: 28px; font-weight: 700; line-height: 1.05; letter-spacing: -0.02em; }
    .kpi .lbl { font-size: 10px; letter-spacing: 0.08em; text-transform: uppercase; color: #6b7790; margin-top: 6px; }
    .kpi.blue .num { color: #2563eb; }
    .kpi.violet .num { color: #7c3aed; }
    .kpi.green .num { color: #059669; }
    .kpi.amber .num { color: #d97706; }

    .charts-row { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 18px; }

    table { width: 100%; border-collapse: separate; border-spacing: 0; font-size: 12px; }
    thead th {
      text-align: left; font-size: 10px; letter-spacing: 0.06em; text-transform: uppercase;
      color: #6b7790; font-weight: 600; border-bottom: 1px solid #e3e7ee; padding: 8px 10px;
      background: #fafbfc;
    }
    tbody td { padding: 8px 10px; border-bottom: 1px solid #f1f3f7; color: #1a2230; }
    tbody tr:nth-child(odd) td { background: #fafbfc; }
    td.num { text-align: right; font-variant-numeric: tabular-nums; }

    .badge {
      display: inline-block; font-size: 9px; padding: 1px 6px; border-radius: 10px; font-weight: 600;
      letter-spacing: 0.04em; text-transform: uppercase; margin-right: 2px;
    }
    .badge.high   { background: #fee2e2; color: #991b1b; }
    .badge.medium { background: #fef3c7; color: #92400e; }
    .badge.low    { background: #dcfce7; color: #166534; }
    .badge.zero   { background: #f1f5f9; color: #94a3b8; }

    #findings-detail summary { cursor: pointer; font-weight: 600; padding: 8px 0; color: #1a2230; }
    #findings-list { list-style: none; padding: 0; max-height: 50vh; overflow-y: auto; }
    #findings-list li { padding: 10px 12px; border-bottom: 1px solid #f1f3f7; font-size: 12px; }
    #findings-list .finding-meta { color: #5d6b86; font-size: 11px; margin: 2px 0 6px 0; }
    #findings-list pre {
      background: #f6f7f9; padding: 6px 8px; border-radius: 4px; font-size: 11px;
      margin: 4px 0 0 0; overflow-x: auto; white-space: pre-wrap; word-break: break-all;
    }

    section { margin-bottom: 18px; }
    canvas { max-height: 240px; }
  </style>
</head>
<body>
  <div class="container">
    <header>
      <h1>Code Scanner Report</h1>
      <p class="meta">root: <code>{{rootPath}}</code> · scanned {{timestamp}} · {{flagsLine}}</p>
    </header>

    <section class="kpis">
      <div class="kpi blue"><div class="num" id="kpi-files">0</div><div class="lbl">Files</div></div>
      <div class="kpi violet"><div class="num" id="kpi-lines">0</div><div class="lbl">Lines</div></div>
      <div class="kpi green"><div class="num" id="kpi-languages">0</div><div class="lbl">Languages</div></div>
      <div class="kpi amber"><div class="num" id="kpi-issues">0</div><div class="lbl">Issues</div></div>
    </section>

    <section class="charts-row">
      <div class="panel"><h3>Lines by language</h3><canvas id="chart-lines-donut"></canvas></div>
      <div class="panel"><h3>Files by language</h3><canvas id="chart-files-bar"></canvas></div>
    </section>

    <section id="severity-section" class="panel">
      <h3>Findings by severity</h3>
      <canvas id="chart-severity-bar"></canvas>
    </section>

    <section class="panel">
      <h3>File breakdown</h3>
      <table>
        <thead>
          <tr><th>Language</th><th class="num">Files</th><th class="num">Lines</th><th>Extensions</th><th>Smells</th><th>Security</th></tr>
        </thead>
        <tbody id="file-breakdown-tbody"></tbody>
      </table>
    </section>

    <section id="findings-detail" class="panel" hidden>
      <details>
        <summary>Findings detail (<span id="findings-count">0</span>)</summary>
        <ul id="findings-list"></ul>
      </details>
    </section>
  </div>

  <script type="application/json" id="scan-data">{{dataJson}}</script>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.5.0/dist/chart.umd.min.js"></script>
  <script>
  (function() {
    'use strict';
    var data = JSON.parse(document.getElementById('scan-data').textContent);
    var LANG_COLORS = ['#2563eb', '#7c3aed', '#059669', '#0891b2'];
    var OTHER_COLOR = '#94a3b8';
    var SEV_COLORS = { low: '#10b981', medium: '#f59e0b', high: '#dc2626' };

    var smellsArr = data.smells || [];
    var secArr = data.securityIssues || [];
    var langs = data.languages || {};

    document.getElementById('kpi-files').textContent = (data.totalFiles || 0).toLocaleString();
    document.getElementById('kpi-lines').textContent = (data.totalLines || 0).toLocaleString();
    document.getElementById('kpi-languages').textContent = Object.keys(langs).length;
    document.getElementById('kpi-issues').textContent = smellsArr.length + secArr.length;

    var langEntries = Object.keys(langs).map(function (name) {
      var v = langs[name];
      return { name: name, files: v.files || 0, lines: v.lines || 0, extensions: v.extensions || [], raw: v };
    }).sort(function (a, b) { return b.lines - a.lines; });

    var displayLangs;
    if (langEntries.length <= 4) {
      displayLangs = langEntries.map(function (e, i) {
        e.color = LANG_COLORS[i] || OTHER_COLOR;
        return e;
      });
    } else {
      var top = langEntries.slice(0, 4).map(function (e, i) { e.color = LANG_COLORS[i]; return e; });
      var rest = langEntries.slice(4);
      var other = {
        name: 'Other',
        files: rest.reduce(function (a, e) { return a + e.files; }, 0),
        lines: rest.reduce(function (a, e) { return a + e.lines; }, 0),
        extensions: rest.reduce(function (a, e) { return a.concat(e.extensions); }, []),
        color: OTHER_COLOR,
        raw: null,
      };
      displayLangs = top.concat([other]);
    }

    if (typeof Chart !== 'undefined') {
      new Chart(document.getElementById('chart-lines-donut'), {
        type: 'doughnut',
        data: {
          labels: displayLangs.map(function (l) { return l.name; }),
          datasets: [{
            data: displayLangs.map(function (l) { return l.lines; }),
            backgroundColor: displayLangs.map(function (l) { return l.color; }),
            borderWidth: 0,
          }],
        },
        options: { responsive: true, plugins: { legend: { position: 'right' } }, cutout: '60%' },
      });

      new Chart(document.getElementById('chart-files-bar'), {
        type: 'bar',
        data: {
          labels: displayLangs.map(function (l) { return l.name; }),
          datasets: [{
            data: displayLangs.map(function (l) { return l.files; }),
            backgroundColor: displayLangs.map(function (l) { return l.color; }),
            borderWidth: 0,
          }],
        },
        options: {
          indexAxis: 'y', responsive: true,
          plugins: { legend: { display: false } },
          scales: { x: { beginAtZero: true } },
        },
      });
    }

    var sevCounts = { low: 0, medium: 0, high: 0 };
    smellsArr.concat(secArr).forEach(function (f) {
      if (f.severity === 'high') sevCounts.high++;
      else if (f.severity === 'medium') sevCounts.medium++;
      else sevCounts.low++;
    });
    var totalSev = sevCounts.low + sevCounts.medium + sevCounts.high;

    if (totalSev > 0 && typeof Chart !== 'undefined') {
      new Chart(document.getElementById('chart-severity-bar'), {
        type: 'bar',
        data: {
          labels: ['Findings'],
          datasets: [
            { label: 'low', data: [sevCounts.low], backgroundColor: SEV_COLORS.low },
            { label: 'medium', data: [sevCounts.medium], backgroundColor: SEV_COLORS.medium },
            { label: 'high', data: [sevCounts.high], backgroundColor: SEV_COLORS.high },
          ],
        },
        options: {
          indexAxis: 'y', responsive: true,
          plugins: { legend: { position: 'bottom' } },
          scales: { x: { stacked: true, beginAtZero: true }, y: { stacked: true } },
        },
      });
    } else {
      document.getElementById('severity-section').hidden = true;
    }

    var tbody = document.getElementById('file-breakdown-tbody');
    if (displayLangs.length === 0) {
      var emptyRow = document.createElement('tr');
      var emptyTd = document.createElement('td');
      emptyTd.colSpan = 6;
      emptyTd.style.textAlign = 'center';
      emptyTd.style.color = '#6b7790';
      emptyTd.textContent = 'No files scanned';
      emptyRow.appendChild(emptyTd);
      tbody.appendChild(emptyRow);
    } else {
      displayLangs.forEach(function (lang) {
        var row = document.createElement('tr');
        function appendText(txt) {
          var td = document.createElement('td');
          td.textContent = txt;
          row.appendChild(td);
        }
        function appendNum(txt) {
          var td = document.createElement('td');
          td.className = 'num';
          td.textContent = txt;
          row.appendChild(td);
        }
        function makeBadge(sev, count) {
          var span = document.createElement('span');
          span.className = 'badge ' + (count === 0 ? 'zero' : sev);
          span.textContent = count.toString();
          return span;
        }
        function appendSeverityCell(summary) {
          var td = document.createElement('td');
          if (!summary) { td.textContent = '—'; row.appendChild(td); return; }
          td.appendChild(makeBadge('high', summary.high || 0));
          td.appendChild(document.createTextNode(' '));
          td.appendChild(makeBadge('medium', summary.medium || 0));
          td.appendChild(document.createTextNode(' '));
          td.appendChild(makeBadge('low', summary.low || 0));
          row.appendChild(td);
        }

        appendText(lang.name);
        appendNum(lang.files.toLocaleString());
        appendNum(lang.lines.toLocaleString());
        appendText(lang.extensions.join(', '));
        appendSeverityCell(lang.raw && lang.raw.smells);
        appendSeverityCell(lang.raw && lang.raw.security);
        tbody.appendChild(row);
      });
    }

    var allFindings = smellsArr.concat(secArr);
    if (allFindings.length === 0) {
      document.getElementById('findings-detail').hidden = true;
    } else {
      document.getElementById('findings-count').textContent = allFindings.length.toString();
      var list = document.getElementById('findings-list');
      allFindings.forEach(function (f) {
        var li = document.createElement('li');
        var title = document.createElement('strong');
        title.textContent = (f.subtype || f.type) + ' (' + f.severity + ')';
        li.appendChild(title);

        var meta = document.createElement('div');
        meta.className = 'finding-meta';
        var lineNum = (f.line !== undefined ? f.line : f.startLine);
        meta.textContent = f.file + (lineNum ? (':' + lineNum) : '');
        li.appendChild(meta);

        var msg = document.createElement('div');
        msg.textContent = f.message;
        li.appendChild(msg);

        if (f.snippet) {
          var pre = document.createElement('pre');
          pre.textContent = f.snippet;
          li.appendChild(pre);
        }
        list.appendChild(li);
      });
    }
  })();
  </script>
</body>
</html>
""";
}
```

- [ ] **Step 2.2: Build to verify the raw string compiles**

```powershell
dotnet build src/CodeScanner/CodeScanner.csproj --nologo
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 2.3: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Html/Template.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(html): add HTML template with Chart.js bootstrap"
```

---

## Task 3: HtmlReport.Render (orchestrator)

**Files:**
- Create: `hackathon/src/CodeScanner/Html/HtmlReport.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Html/HtmlReportTests.cs`

- [ ] **Step 3.1: Write failing tests**

Create `tests/CodeScanner.Tests/Html/HtmlReportTests.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeScanner.Tests.Html;

public class HtmlReportTests
{
    private static (ScanResult, AnalysisResult, ScanOptions) Empty() => (
        new ScanResult("/x", Array.Empty<FileEntry>(), Array.Empty<string>(), Array.Empty<ScanError>()),
        new AnalysisResult(Array.Empty<SmellFinding>(), Array.Empty<SecurityFinding>(), Array.Empty<ScanError>()),
        new ScanOptions());

    [Fact]
    public void Render_EmptyResult_ProducesValidHtmlSkeleton()
    {
        var (result, analysis, options) = Empty();
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html lang=\"en\">", html);
        Assert.Contains("id=\"scan-data\"", html);
        Assert.Contains("chart.umd.min.js", html);
    }

    [Fact]
    public void Render_EmbedsJsonAsScriptTag()
    {
        var (result, analysis, options) = Empty();
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        var match = Regex.Match(html,
            "<script type=\"application/json\" id=\"scan-data\">(?<j>.*?)</script>",
            RegexOptions.Singleline);
        Assert.True(match.Success);

        var doc = JsonDocument.Parse(match.Groups["j"].Value);
        Assert.Equal(0, doc.RootElement.GetProperty("totalFiles").GetInt32());
    }

    [Fact]
    public void Render_HtmlEscapesRootPath()
    {
        var result = new ScanResult("path<&>\"'", Array.Empty<FileEntry>(),
            Array.Empty<string>(), Array.Empty<ScanError>());
        var analysis = new AnalysisResult(Array.Empty<SmellFinding>(),
            Array.Empty<SecurityFinding>(), Array.Empty<ScanError>());

        var html = HtmlReport.Render(result, analysis, new ScanOptions(), DateTimeOffset.UtcNow);

        Assert.DoesNotContain("path<&>\"'", html);
        Assert.Contains("path&lt;&amp;&gt;&quot;&#39;", html);
    }

    [Fact]
    public void Render_EscapesScriptTagsInJsonPayload()
    {
        var result = new ScanResult("/x",
            new[] { new FileEntry("a.cs", ".cs", "C#", 1, false) },
            Array.Empty<string>(), Array.Empty<ScanError>());

        var analysis = new AnalysisResult(
            Smells: Array.Empty<SmellFinding>(),
            SecurityFindings: new[]
            {
                new SecurityFinding("dangerous_function", "eval", "high", "x.js",
                    1, 1, "</script><script>alert(1)</script>", "msg"),
            },
            Errors: Array.Empty<ScanError>());

        var html = HtmlReport.Render(result, analysis, new ScanOptions { Security = true }, DateTimeOffset.UtcNow);

        // The matched </script> in the snippet must be neutralized so it
        // does not terminate the surrounding <script id="scan-data"> tag.
        var dataMatch = Regex.Match(html,
            "<script type=\"application/json\" id=\"scan-data\">(?<j>.*?)</script>",
            RegexOptions.Singleline);
        Assert.True(dataMatch.Success);
        Assert.Contains("<\\/script>", dataMatch.Groups["j"].Value);
    }

    [Fact]
    public void Render_FlagsLine_ListsActiveFlags()
    {
        var (result, analysis, _) = Empty();
        var options = new ScanOptions { Smells = true, Security = true };
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        Assert.Contains("--analyze", html);
    }

    [Fact]
    public void Render_FlagsLine_NoFlagsShowsBaseScan()
    {
        var (result, analysis, options) = Empty();
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        Assert.Contains("scan only", html);
    }

    [Fact]
    public void Render_TimestampIsIso8601()
    {
        var (result, analysis, options) = Empty();
        var when = new DateTimeOffset(2026, 5, 7, 11, 48, 0, TimeSpan.Zero);
        var html = HtmlReport.Render(result, analysis, options, when);

        Assert.Contains("2026-05-07T11:48:00", html);
    }
}
```

- [ ] **Step 3.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build error referring to missing `HtmlReport`.

- [ ] **Step 3.3: Implement `HtmlReport.cs`**

Create `src/CodeScanner/Html/HtmlReport.cs`:

```csharp
namespace CodeScanner;

public static class HtmlReport
{
    public static string Render(
        ScanResult result,
        AnalysisResult analysis,
        ScanOptions options,
        DateTimeOffset timestamp)
    {
        var json = Report.Serialize(result, analysis, options, pretty: false);
        var safeJson = TemplateRenderer.EscapeForScriptTag(json);
        var rootPath = TemplateRenderer.HtmlEscape(NormalizePath(result.Root));
        var ts = TemplateRenderer.HtmlEscape(timestamp.ToString("yyyy-MM-ddTHH:mm:ssK"));
        var flagsLine = TemplateRenderer.HtmlEscape(BuildFlagsLine(options));

        var values = new Dictionary<string, string>
        {
            ["rootPath"]  = rootPath,
            ["timestamp"] = ts,
            ["flagsLine"] = flagsLine,
            ["dataJson"]  = safeJson,
        };

        return TemplateRenderer.Render(Template.Html, values);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string BuildFlagsLine(ScanOptions options)
    {
        if (options.Smells && options.Security) { return "--analyze"; }
        if (options.Smells)   { return "--smells"; }
        if (options.Security) { return "--security"; }
        return "scan only";
    }
}
```

- [ ] **Step 3.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green (prior + 7 new).

- [ ] **Step 3.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Html/HtmlReport.cs hackathon/tests/CodeScanner.Tests/Html/HtmlReportTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(html): add HtmlReport.Render with template binding and escaping"
```

---

## Task 4: Wire CLI `--html` flag

**Files:**
- Modify: `hackathon/src/CodeScanner/Cli.cs`
- Modify: `hackathon/tests/CodeScanner.Tests/CliTests.cs`

- [ ] **Step 4.1: Append failing CLI tests**

Open `tests/CodeScanner.Tests/CliTests.cs`. Insert these methods inside the `CliTests` class, immediately before the closing `}` of the class (i.e., after the existing `Cli_Analyze_OnFixtures_ProducesExpectedFindings` test):

```csharp
    [Fact]
    public void Cli_HtmlFlag_WritesFile()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        var outFile = Path.Combine(tree.Root, "report.html");

        var (exit, stdout, _) = RunCli(tree.Root, "--html", outFile);

        Assert.Equal(0, exit);
        Assert.Empty(stdout.Trim());
        Assert.True(File.Exists(outFile));
        var html = File.ReadAllText(outFile);
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("id=\"scan-data\"", html);
    }

    [Fact]
    public void Cli_HtmlFlag_WithAnalyze_EmbedsFindings()
    {
        using var tree = new TempTree();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("class C {");
        sb.AppendLine("    void Foo(int a, int b, int c, int d, int e, int f) {");
        for (var i = 0; i < 60; i++) { sb.AppendLine("        var x = 1;"); }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        tree.WriteFile("a.cs", sb.ToString());
        var outFile = Path.Combine(tree.Root, "r.html");

        var (exit, _, _) = RunCli(tree.Root, "--html", outFile, "--analyze");

        Assert.Equal(0, exit);
        var html = File.ReadAllText(outFile);
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            "<script type=\"application/json\" id=\"scan-data\">(?<j>.*?)</script>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(match.Success);
        var doc = System.Text.Json.JsonDocument.Parse(match.Groups["j"].Value);
        Assert.True(doc.RootElement.GetProperty("smells").GetArrayLength() >= 1);
    }

    [Fact]
    public void Cli_HtmlFlag_AndJsonOutput_BothProduced()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        var jsonFile = Path.Combine(tree.Root, "out.json");
        var htmlFile = Path.Combine(tree.Root, "out.html");

        var (exit, stdout, _) = RunCli(tree.Root, "--output", jsonFile, "--html", htmlFile);

        Assert.Equal(0, exit);
        Assert.Empty(stdout.Trim());
        Assert.True(File.Exists(jsonFile));
        Assert.True(File.Exists(htmlFile));

        var jsonText = File.ReadAllText(jsonFile);
        var html = File.ReadAllText(htmlFile);
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            "<script type=\"application/json\" id=\"scan-data\">(?<j>.*?)</script>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(match.Success);

        // The script-tag escape (</script> -> <\/script>) means the embedded JSON
        // can differ from the file JSON only by that one substitution. Reverse it
        // before comparing.
        var embeddedJson = match.Groups["j"].Value.Replace("<\\/script>", "</script>");
        Assert.Equal(jsonText, embeddedJson);
    }

    [Fact]
    public void Cli_HtmlFlag_BadDirectory_ExitsTwo()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        var bogus = Path.Combine(tree.Root, "no", "such", "dir", "r.html");

        var (exit, _, stderr) = RunCli(tree.Root, "--html", bogus);

        Assert.Equal(2, exit);
        Assert.Contains("error:", stderr);
    }
```

- [ ] **Step 4.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: failures because `--html` is not a recognized option.

- [ ] **Step 4.3: Update `Cli.cs`**

Replace the entire contents of `src/CodeScanner/Cli.cs`:

```csharp
using System.CommandLine;

namespace CodeScanner;

public static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var pathArg = new Argument<string>("path") { Description = "Directory to scan" };
        var outputOpt = new Option<string?>("--output", "-o") { Description = "Write JSON to this file instead of stdout" };
        var excludeOpt = new Option<string[]>("--exclude", "-e")
        {
            Description = "Extra directory names to skip (additive to defaults)",
            AllowMultipleArgumentsPerToken = true,
        };
        var followOpt = new Option<bool>("--follow-symlinks") { Description = "Follow symlinked directories (default: skip)" };
        var prettyOpt = new Option<bool>("--pretty") { Description = "Pretty-print JSON output" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Log scan progress to stderr" };
        var smellsOpt   = new Option<bool>("--smells")    { Description = "Run Roslyn-based smell analyzer on .cs files" };
        var securityOpt = new Option<bool>("--security")  { Description = "Run regex-based security scanners (secrets + dangerous functions)" };
        var analyzeOpt  = new Option<bool>("--analyze")   { Description = "Shorthand for --smells --security" };
        var securitySkipOpt = new Option<string[]>("--security-skip")
        {
            Description = "Glob patterns to skip during security scan only (additive)",
            AllowMultipleArgumentsPerToken = true,
        };
        var htmlOpt = new Option<string?>("--html") { Description = "Write a self-contained HTML report to this file" };

        var root = new RootCommand("Recursively scan a directory and emit JSON file/line statistics.")
        {
            pathArg, outputOpt, excludeOpt, followOpt, prettyOpt, verboseOpt,
            smellsOpt, securityOpt, analyzeOpt, securitySkipOpt, htmlOpt,
        };

        root.SetAction(parseResult =>
        {
            var path     = parseResult.GetValue(pathArg)!;
            var output   = parseResult.GetValue(outputOpt);
            var excludes = parseResult.GetValue(excludeOpt) ?? Array.Empty<string>();
            var follow   = parseResult.GetValue(followOpt);
            var pretty   = parseResult.GetValue(prettyOpt);
            var verbose  = parseResult.GetValue(verboseOpt);
            var smells   = parseResult.GetValue(smellsOpt);
            var security = parseResult.GetValue(securityOpt);
            var analyze  = parseResult.GetValue(analyzeOpt);
            var skip     = parseResult.GetValue(securitySkipOpt) ?? Array.Empty<string>();
            var html     = parseResult.GetValue(htmlOpt);

            if (analyze) { smells = true; security = true; }

            return Execute(path, output, excludes, follow, pretty, verbose, smells, security, skip, html);
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static int Execute(
        string path,
        string? output,
        string[] excludes,
        bool followSymlinks,
        bool pretty,
        bool verbose,
        bool smells,
        bool security,
        string[] securitySkipGlobs,
        string? htmlPath)
    {
        if (!Directory.Exists(path))
        {
            if (File.Exists(path))
            {
                Console.Error.WriteLine($"error: path is a file, not a directory: {path}");
            }
            else
            {
                Console.Error.WriteLine($"error: path does not exist: {path}");
            }
            return 1;
        }

        try
        {
            if (verbose) { Console.Error.WriteLine($"info: scanning {path}"); }

            var options = new ScanOptions
            {
                FollowSymlinks = followSymlinks,
                ExtraExcludes = excludes,
                Smells = smells,
                Security = security,
                SecuritySkipGlobs = securitySkipGlobs,
            };

            var result = Scanner.Scan(path, options);

            AnalysisResult analysis;
            if (smells || security)
            {
                if (verbose) { Console.Error.WriteLine("info: running analysis pass"); }
                var host = new AnalyzerHost();
                analysis = host.Analyze(result, options);
            }
            else
            {
                analysis = new AnalysisResult(
                    Array.Empty<SmellFinding>(),
                    Array.Empty<SecurityFinding>(),
                    Array.Empty<ScanError>());
            }

            // JSON output (stdout or --output file).
            var json = Report.Serialize(result, analysis, options, pretty);
            if (htmlPath is not null)
            {
                if (output is not null)
                {
                    File.WriteAllText(output, json);
                    if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
                }
                // else: skip stdout when --html is set without --output, to keep it quiet.
            }
            else if (output is null)
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                File.WriteAllText(output, json);
                if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
            }

            // HTML output.
            if (htmlPath is not null)
            {
                try
                {
                    var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);
                    File.WriteAllText(htmlPath, html);
                    if (verbose) { Console.Error.WriteLine($"info: wrote {htmlPath}"); }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: cannot write HTML report: {ex.GetType().Name}: {ex.Message}");
                    return 2;
                }
            }

            if (verbose)
            {
                Console.Error.WriteLine(
                    $"info: {result.FileEntries.Count} files, {result.Errors.Count + analysis.Errors.Count} errors, " +
                    $"{analysis.Smells.Count} smells, {analysis.SecurityFindings.Count} security");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.GetType().Name}: {ex.Message}");
            return 2;
        }
    }
}
```

- [ ] **Step 4.4: Build before subprocess CLI tests**

```powershell
dotnet build CodeScanner.sln --nologo
```

Expected: clean build.

- [ ] **Step 4.5: Run all tests**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green (prior + 4 new CLI tests).

- [ ] **Step 4.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Cli.cs hackathon/tests/CodeScanner.Tests/CliTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(html): wire --html flag to HtmlReport with file IO and exit codes"
```

---

## Task 5: README + final verification

**Files:**
- Modify: `hackathon/README.md`

- [ ] **Step 5.1: Update README**

Open `hackathon/README.md`. Replace the entire `## Run (development)` section (the one with `--smells/--security/--analyze` usage) with:

```markdown
## Run (development)

```powershell
dotnet run --project src/CodeScanner -- <path> `
  [--output report.json] [--html report.html] [--pretty] `
  [--exclude name ...] [--follow-symlinks] [--verbose] `
  [--smells] [--security] [--analyze] [--security-skip glob ...]
```

`--analyze` is shorthand for `--smells --security`. `--html` writes a self-contained HTML dashboard (KPI tiles, language charts, severity bar, file breakdown table, expandable findings). `--output` and `--html` may be combined.
```

After the `## Output shape` section, append a new section:

```markdown
## HTML report

```powershell
dotnet run --project src/CodeScanner -- . --analyze --html report.html
start report.html  # opens in default browser
```

The generated `report.html` is a single self-contained file. It loads Chart.js v4 from `cdn.jsdelivr.net` at view time (~80 KB). Embeds a complete copy of the scan JSON in `<script id="scan-data">` so the page renders the same dashboard whenever it's opened.
```

- [ ] **Step 5.2: Final verification — `/warnaserror` build**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet build CodeScanner.sln /warnaserror --nologo
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 5.3: Final verification — full test run**

```powershell
dotnet test CodeScanner.sln --nologo --logger "console;verbosity=normal"
```

Expected: all tests pass.

- [ ] **Step 5.4: Final verification — manual smoke**

```powershell
dotnet run --no-build --project src/CodeScanner -- ../ --analyze --html report.html
Test-Path report.html
(Get-Content report.html -Raw).Length
```

Expected: `True`, file size > 5,000 bytes (template is ~7 KB plus the embedded JSON).

- [ ] **Step 5.5: Manual visual verification (instruction only — agent cannot perform)**

Open `hackathon/report.html` in a browser. Confirm:

1. KPI tiles show non-zero numbers for Files, Lines, Languages.
2. The donut chart renders with at least one slice.
3. The horizontal bar chart renders with one bar per top language.
4. If the scan included findings, the severity bar shows segments and the "Findings detail" section is expandable.
5. The file breakdown table has one row per language sorted by lines descending.
6. No console errors in DevTools (F12 → Console).

- [ ] **Step 5.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/README.md
git -C C:/Cmm-testing/Claude_Training commit -m "docs(html): document --html flag and report contents"
```

---

## Self-Review Checklist (executed before handoff)

**1. Spec coverage:**

- `--html <file>` CLI flag → Task 4 `htmlOpt`
- Compatible with all other flags (including `--output`) → Task 4 `Cli_HtmlFlag_AndJsonOutput_BothProduced`
- Bad output dir → exit 2 → Task 4 `Cli_HtmlFlag_BadDirectory_ExitsTwo` + try/catch in `Execute`
- HTML structure (header, KPI strip, charts row, severity, table, findings detail) → Task 2 `Template.Html`
- Embedded JSON via `<script type="application/json" id="scan-data">` → Task 2 `Template.Html`, Task 3 `Render`
- HTML escape of root path / timestamp / flags line → Task 3 `Render` calls `HtmlEscape`
- `</script>` neutralization in JSON → Task 1 `EscapeForScriptTag`, exercised in Task 3 test
- Chart.js v4 from `cdn.jsdelivr.net` → Task 2 `Template.Html` `<script src>`
- Three charts (donut, horizontal bar, stacked severity) → Task 2 bootstrap JS
- 4-color palette + grey "Other" bucket → Task 2 bootstrap JS `LANG_COLORS` / `OTHER_COLOR`
- Severity bar hidden when no findings → Task 2 bootstrap JS `if (totalSev > 0) ... else hidden`
- Findings detail hidden when no findings → Task 2 bootstrap JS `if (allFindings.length === 0)`
- Light theme, system fonts → Task 2 `<style>` block
- Empty scan → "No files scanned" row → Task 2 bootstrap JS `if (displayLangs.length === 0)`
- Timestamp ISO-8601 → Task 3 `timestamp.ToString("yyyy-MM-ddTHH:mm:ssK")`
- Path normalization (forward slashes) → Task 3 `NormalizePath`

**2. Placeholder scan:** No "TBD"/"TODO"/"add tests for above" patterns. Every code-mutating step shows the full code; every command shows expected output.

**3. Type consistency:**

- `TemplateRenderer.Render(string, IReadOnlyDictionary<string,string>) → string` — defined Task 1, used Task 3.
- `TemplateRenderer.HtmlEscape(string) → string` — defined Task 1, used Task 3.
- `TemplateRenderer.EscapeForScriptTag(string) → string` — defined Task 1, used Task 3.
- `Template.Html` (`const string`) — defined Task 2, used Task 3.
- `HtmlReport.Render(ScanResult, AnalysisResult, ScanOptions, DateTimeOffset) → string` — defined Task 3, used Task 4 in `Cli.Execute`.
- `ScanOptions.Smells / Security` — already exist (from `2026-05-05-code-analysis-design.md`); Task 3 reads them in `BuildFlagsLine`.
- `Report.Serialize(ScanResult, AnalysisResult, ScanOptions, bool) → string` — already exists; Task 3 calls it.

No type drift detected.
