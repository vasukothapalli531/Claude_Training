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
