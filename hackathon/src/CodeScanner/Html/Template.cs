namespace CodeScanner;

internal static class Template
{
    public const string Html = """
<!DOCTYPE html>
<html lang="en" data-theme="dark">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Code Scanner Report — {{rootPath}}</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap" rel="stylesheet">
  <style>
    :root {
      --bg: #0f1117; --panel: #181b25; --panel-hover: #1c2030;
      --border: #1d2030; --text: #e7e9ee; --text-dim: #cbd0dc; --muted: #6b7393;
      --high: #ef4444; --medium: #f59e0b; --low: #10b981;
      --blue: #3b82f6; --violet: #a855f7; --amber: #f59e0b;
      color-scheme: dark;
    }
    * { box-sizing: border-box; }
    html, body { background: var(--bg); color: var(--text); margin: 0; padding: 0;
      font-family: "Inter", -apple-system, "Segoe UI", system-ui, sans-serif; line-height: 1.5; }
    body { opacity: 0; transition: opacity 300ms ease; }
    body[data-loaded="true"] { opacity: 1; }

    .container { max-width: 1200px; margin: 0 auto; padding: 24px; }

    .hdr { position: sticky; top: 0; z-index: 10; background: var(--bg); padding-bottom: 16px;
      border-bottom: 1px solid var(--border); margin-bottom: 18px;
      display: grid; grid-template-columns: 1fr auto; gap: 16px; align-items: center; }
    .hdr-text h1 { margin: 0; font-size: 20px; font-weight: 600; color: var(--text); letter-spacing: -0.01em; }
    .hdr-text .meta { font-size: 12px; color: var(--muted); margin-top: 4px; }
    .hdr-text .meta code { background: var(--border); padding: 1px 6px; border-radius: 3px; font-size: 11px; }

    .grade-tile { width: 80px; height: 80px; border-radius: 14px;
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      font-size: 38px; font-weight: 800; letter-spacing: -0.04em; color: #fff;
      transition: transform 250ms cubic-bezier(0.4,0,0.2,1); cursor: default; }
    .grade-tile:hover { transform: scale(1.04); }
    .grade-tile .score { font-size: 10px; font-weight: 600; opacity: 0.85; letter-spacing: 0.06em; margin-top: -4px; }
    .grade-a { background: linear-gradient(135deg, #10b981, #059669); box-shadow: 0 6px 18px rgba(16,185,129,0.32); }
    .grade-b { background: linear-gradient(135deg, #84cc16, #65a30d); box-shadow: 0 6px 18px rgba(132,204,22,0.32); }
    .grade-c { background: linear-gradient(135deg, #f59e0b, #d97706); box-shadow: 0 6px 18px rgba(245,158,11,0.32); }
    .grade-d { background: linear-gradient(135deg, #f97316, #ea580c); box-shadow: 0 6px 18px rgba(249,115,22,0.32); }
    .grade-f { background: linear-gradient(135deg, #ef4444, #dc2626); box-shadow: 0 6px 18px rgba(239,68,68,0.32); }

    .kpis { display: grid; grid-template-columns: repeat(4, 1fr); gap: 14px; margin-bottom: 18px; }
    .kpi { background: var(--panel); border: 1px solid var(--border); border-radius: 10px;
      padding: 14px 16px; position: relative; overflow: hidden; }
    .kpi::after { content:""; position:absolute; left:0; top:0; bottom:0; width:3px; }
    .kpi.blue::after   { background: var(--blue); }
    .kpi.violet::after { background: var(--violet); }
    .kpi.red::after    { background: var(--high); }
    .kpi.amber::after  { background: var(--amber); }
    .kpi .num { font-size: 26px; font-weight: 700; line-height: 1.1;
      letter-spacing: -0.02em; color: var(--text); font-variant-numeric: tabular-nums; }
    .kpi .lbl { font-size: 10px; letter-spacing: 0.1em; text-transform: uppercase;
      color: var(--muted); margin-top: 6px; font-weight: 600; }

    .charts-row { display: grid; grid-template-columns: 1fr 1.2fr 1fr; gap: 14px; margin-bottom: 18px; }
    .panel { background: var(--panel); border: 1px solid var(--border); border-radius: 10px; padding: 16px 18px; }
    .panel h3 { margin: 0 0 12px 0; font-size: 12px; font-weight: 600; letter-spacing: 0.04em;
      text-transform: uppercase; color: var(--muted); }
    canvas { max-height: 220px; }

    .table-panel { background: var(--panel); border: 1px solid var(--border); border-radius: 10px; padding: 16px 18px; }
    .table-hdr { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .table-hdr h3 { margin: 0; font-size: 12px; font-weight: 600; letter-spacing: 0.04em;
      text-transform: uppercase; color: var(--muted); }
    .pills { display: flex; gap: 6px; }
    .pill { padding: 4px 10px; border-radius: 14px; background: var(--bg); border: 1px solid var(--border);
      color: var(--muted); font-size: 10px; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase;
      cursor: pointer; transition: background 120ms, color 120ms, border-color 120ms; user-select: none; }
    .pill:hover { background: var(--panel-hover); color: var(--text-dim); }
    .pill.active { background: #1f2330; color: var(--text); border-color: #2a2f42; }
    .pill .ct { color: var(--muted); margin-left: 6px; font-weight: 500; }
    .pill.active .ct { color: var(--text-dim); }

    table.tbl { width: 100%; border-collapse: collapse; font-size: 12px; }
    table.tbl th { padding: 9px 10px; text-align: left; font-size: 9px; letter-spacing: 0.06em;
      text-transform: uppercase; color: var(--muted); font-weight: 600; border-bottom: 1px solid var(--border);
      user-select: none; cursor: pointer; }
    table.tbl th:hover { color: var(--text-dim); }
    table.tbl th .arrow { display: inline-block; width: 8px; opacity: 0.5; margin-left: 4px; }
    table.tbl th.sorted .arrow { opacity: 1; color: var(--text); }
    table.tbl td { padding: 10px 10px; color: var(--text-dim); border-bottom: 1px solid var(--border); }
    table.tbl tbody tr.row-main { cursor: pointer; transition: background 120ms; }
    table.tbl tbody tr.row-main:hover td { background: var(--panel-hover); }
    table.tbl tbody tr.row-detail td { background: #14161f; padding: 0; }
    table.tbl tbody tr.row-detail.hidden { display: none; }
    table.tbl tbody tr.row-detail ul { margin: 0; padding: 12px 18px; list-style: none; }
    table.tbl tbody tr.row-detail li { padding: 8px 0; border-bottom: 1px solid var(--border); font-size: 11px; }
    table.tbl tbody tr.row-detail li:last-child { border-bottom: 0; }
    table.tbl tbody tr.row-detail .meta { color: var(--muted); font-size: 10px; margin-top: 2px; }
    table.tbl tbody tr.row-detail pre { margin: 4px 0 0 0; padding: 6px 8px; background: var(--bg);
      border-radius: 4px; font-size: 11px; overflow-x: auto; white-space: pre-wrap; word-break: break-all; }
    table.tbl td.path { color: var(--text); font-family: "JetBrains Mono", "Cascadia Mono", monospace; font-size: 11px; }
    table.tbl td.num { text-align: right; font-variant-numeric: tabular-nums; }

    .severity-stack { display: inline-flex; gap: 2px; vertical-align: middle; }
    .severity-stack span { display: inline-block; height: 6px; border-radius: 1px; }
    .badge { display: inline-block; font-size: 9px; padding: 1px 7px; border-radius: 8px; font-weight: 700;
      letter-spacing: 0.04em; text-transform: uppercase; margin-right: 2px; }
    .badge.h { background: rgba(239,68,68,0.15);  color: #fca5a5; }
    .badge.m { background: rgba(245,158,11,0.15); color: #fbbf24; }
    .badge.l { background: rgba(16,185,129,0.15); color: #6ee7b7; }

    .skeleton { background: linear-gradient(90deg, #1a1d29 0%, #20242f 50%, #1a1d29 100%);
      background-size: 200% 100%; animation: shimmer 1.4s infinite linear; border-radius: 6px; }
    @keyframes shimmer { 0% { background-position: 200% 0; } 100% { background-position: -200% 0; } }
    body[data-loaded="true"] .skeleton { display: none; }
    body:not([data-loaded="true"]) .real { opacity: 0; }

    .empty-note { color: var(--muted); font-size: 12px; text-align: center; padding: 24px 0; }

    @media (max-width: 900px) {
      .charts-row { grid-template-columns: 1fr; }
    }
    @media (max-width: 700px) {
      .kpis { grid-template-columns: repeat(2, 1fr); }
    }
  </style>
</head>
<body>
  <div class="container">
    <header class="hdr">
      <div class="hdr-text">
        <h1>Code Scanner Report</h1>
        <p class="meta">root: <code>{{rootPath}}</code> · scanned {{timestamp}} · {{flagsLine}}</p>
      </div>
      <div class="grade-tile" id="grade-tile">
        <span id="grade-letter">·</span>
        <span class="score"><span id="grade-score">0</span>/100</span>
      </div>
    </header>

    <section class="kpis">
      <div class="kpi blue"><div class="num" id="kpi-files">0</div><div class="lbl">Total Files</div></div>
      <div class="kpi violet"><div class="num" id="kpi-quality">0</div><div class="lbl">Quality Score</div></div>
      <div class="kpi red"><div class="num" id="kpi-critical">0</div><div class="lbl">Critical Issues</div></div>
      <div class="kpi amber"><div class="num" id="kpi-fixtime">—</div><div class="lbl">Est. Fix Time</div></div>
    </section>

    <section class="charts-row">
      <div class="panel"><h3>Issues by severity</h3><canvas id="chart-severity-donut"></canvas></div>
      <div class="panel"><h3>Top files by risk</h3><canvas id="chart-top-files-bar"></canvas></div>
      <div class="panel"><h3>Quality dimensions</h3><canvas id="chart-quality-radar"></canvas><div id="radar-empty" class="empty-note" hidden>Run with <code>--analyze</code> for quality dimensions.</div></div>
    </section>

    <section class="table-panel">
      <div class="table-hdr">
        <h3 id="table-title">Files</h3>
        <div class="pills" id="severity-pills">
          <span class="pill active" data-sev="all">All <span class="ct" id="pill-all-ct">0</span></span>
          <span class="pill" data-sev="high">High <span class="ct" id="pill-high-ct">0</span></span>
          <span class="pill" data-sev="medium">Medium <span class="ct" id="pill-medium-ct">0</span></span>
          <span class="pill" data-sev="low">Low <span class="ct" id="pill-low-ct">0</span></span>
        </div>
      </div>
      <table class="tbl" id="files-table">
        <thead>
          <tr>
            <th data-sort="file">File <span class="arrow">↕</span></th>
            <th class="num" data-sort="riskScore">Risk <span class="arrow">↕</span></th>
            <th class="num" data-sort="lines">Lines <span class="arrow">↕</span></th>
            <th data-sort="severity">Severity <span class="arrow">↕</span></th>
            <th>Stack</th>
          </tr>
        </thead>
        <tbody id="files-tbody"></tbody>
      </table>
      <div id="table-empty" class="empty-note" hidden>No files have findings.</div>
    </section>
  </div>

  <script type="application/json" id="scan-data">{{dataJson}}</script>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.5.0/dist/chart.umd.min.js"></script>
  <script>
  (function () {
    'use strict';
    var data = JSON.parse(document.getElementById('scan-data').textContent);
    var smells = data.smells || [];
    var sec    = data.securityIssues || [];
    var risks  = data.fileRiskScores || [];

    var grade = data.grade || 'A';
    document.getElementById('grade-letter').textContent = grade;
    document.getElementById('grade-tile').classList.add('grade-' + grade.toLowerCase());

    animateCount('kpi-files',    data.totalFiles    || 0);
    animateCount('kpi-quality',  data.qualityScore  || 0);
    animateCount('grade-score',  data.qualityScore  || 0);
    var critical = countSeverity(smells, 'high') + countSeverity(sec, 'high');
    animateCount('kpi-critical', critical);
    document.getElementById('kpi-fixtime').textContent = formatMinutes(data.estimatedFixMinutes || 0);

    var sevCounts = { high: 0, medium: 0, low: 0 };
    smells.concat(sec).forEach(function (f) {
      if (f.severity === 'high')   sevCounts.high++;
      else if (f.severity === 'medium') sevCounts.medium++;
      else sevCounts.low++;
    });
    var totalIssues = sevCounts.high + sevCounts.medium + sevCounts.low;

    document.getElementById('pill-all-ct').textContent    = risks.length;
    document.getElementById('pill-high-ct').textContent   = risks.filter(function(r){return r.high>0;}).length;
    document.getElementById('pill-medium-ct').textContent = risks.filter(function(r){return r.medium>0;}).length;
    document.getElementById('pill-low-ct').textContent    = risks.filter(function(r){return r.low>0;}).length;

    if (typeof Chart !== 'undefined') {
      Chart.defaults.color = '#cbd0dc';
      Chart.defaults.borderColor = '#1d2030';
      Chart.defaults.font.family = 'Inter, sans-serif';

      if (totalIssues === 0) {
        document.getElementById('chart-severity-donut').replaceWith(
          Object.assign(document.createElement('div'), { className: 'empty-note', textContent: 'No findings' }));
      } else {
        new Chart(document.getElementById('chart-severity-donut'), {
          type: 'doughnut',
          data: {
            labels: ['High', 'Medium', 'Low'],
            datasets: [{
              data: [sevCounts.high, sevCounts.medium, sevCounts.low],
              backgroundColor: ['#ef4444', '#f59e0b', '#10b981'],
              borderColor: '#181b25', borderWidth: 2,
            }],
          },
          options: {
            responsive: true, cutout: '62%',
            plugins: { legend: { position: 'bottom', labels: { boxWidth: 10 } } },
            onClick: function (_, elements) {
              if (elements && elements[0]) {
                var labels = ['high', 'medium', 'low'];
                activatePill(labels[elements[0].index]);
                scrollToTable();
              }
            },
          },
        });
      }

      var top10 = risks.slice(0, 10);
      if (top10.length === 0) {
        document.getElementById('chart-top-files-bar').replaceWith(
          Object.assign(document.createElement('div'), { className: 'empty-note', textContent: 'No risk data' }));
      } else {
        var maxRisk = Math.max.apply(null, top10.map(function (r) { return r.riskScore; }));
        new Chart(document.getElementById('chart-top-files-bar'), {
          type: 'bar',
          data: {
            labels: top10.map(function (r) { return shortenPath(r.file); }),
            datasets: [{
              label: 'Risk',
              data: top10.map(function (r) { return r.riskScore; }),
              backgroundColor: top10.map(function (r) { return riskColor(r.riskScore, maxRisk); }),
              borderWidth: 0,
            }],
          },
          options: {
            indexAxis: 'y', responsive: true,
            plugins: { legend: { display: false }, tooltip: { callbacks: {
              title: function (items) { return top10[items[0].dataIndex].file; },
            } } },
            scales: { x: { beginAtZero: true } },
            onClick: function (_, elements) {
              if (elements && elements[0]) {
                expandFileRow(top10[elements[0].index].file);
              }
            },
          },
        });
      }

      var radarData = computeRadar(data, smells, sec);
      if (radarData) {
        new Chart(document.getElementById('chart-quality-radar'), {
          type: 'radar',
          data: {
            labels: ['Cleanliness', 'Security', 'Function Length', 'Nesting', 'Parameters'],
            datasets: [{
              label: 'Score',
              data: radarData,
              backgroundColor: 'rgba(168,85,247,0.25)',
              borderColor: '#a855f7',
              borderWidth: 2,
              pointBackgroundColor: '#a855f7',
              pointRadius: 3,
            }],
          },
          options: {
            responsive: true,
            scales: {
              r: { beginAtZero: true, max: 100,
                grid: { color: '#1d2030' }, angleLines: { color: '#1d2030' },
                pointLabels: { color: '#cbd0dc', font: { size: 10 } },
                ticks: { display: false } },
            },
            plugins: { legend: { display: false } },
          },
        });
      } else {
        document.getElementById('chart-quality-radar').hidden = true;
        document.getElementById('radar-empty').hidden = false;
      }
    }

    var sortState = { key: 'riskScore', dir: 'desc' };
    var activeSeverities = new Set(['all']);
    renderTable();

    document.querySelectorAll('#severity-pills .pill').forEach(function (p) {
      p.addEventListener('click', function () { activatePill(p.dataset.sev); });
    });
    document.querySelectorAll('#files-table th[data-sort]').forEach(function (h) {
      h.addEventListener('click', function () {
        var key = h.dataset.sort;
        if (sortState.key === key) {
          sortState.dir = (sortState.dir === 'desc' ? 'asc' : 'desc');
        } else {
          sortState.key = key; sortState.dir = 'desc';
        }
        renderTable();
      });
    });

    requestAnimationFrame(function () { document.body.dataset.loaded = 'true'; });

    function animateCount(id, target) {
      var el = document.getElementById(id);
      if (!el) return;
      var start = performance.now(), duration = 600;
      function step(now) {
        var t = Math.min(1, (now - start) / duration);
        var eased = 1 - Math.pow(1 - t, 3);
        var v = Math.round(target * eased);
        el.textContent = v.toLocaleString();
        if (t < 1) requestAnimationFrame(step);
      }
      requestAnimationFrame(step);
    }
    function countSeverity(arr, sev) {
      var n = 0; for (var i = 0; i < arr.length; i++) if (arr[i].severity === sev) n++; return n;
    }
    function formatMinutes(m) {
      if (!m) return '0m';
      var h = Math.floor(m / 60), mm = m % 60;
      if (h === 0) return mm + 'm';
      if (mm === 0) return h + 'h';
      return h + 'h ' + mm + 'm';
    }
    function shortenPath(p) {
      var parts = p.replace(/\\\\/g, '/').split('/');
      return parts.length <= 2 ? p : '…/' + parts.slice(-2).join('/');
    }
    function riskColor(v, max) {
      if (max <= 0) return '#10b981';
      var t = v / max;
      if (t > 0.8) return '#ef4444';
      if (t > 0.6) return '#f97316';
      if (t > 0.4) return '#f59e0b';
      if (t > 0.2) return '#84cc16';
      return '#10b981';
    }
    function computeRadar(data, smells, sec) {
      var totalFiles = data.totalFiles || 0;
      var totalFunctions = data.totalFunctions || 0;
      if (totalFiles === 0 && totalFunctions === 0) return null;

      function norm(numerator, denominator, scale) {
        if (denominator === 0) return 100;
        var raw = scale * numerator / denominator;
        return Math.max(0, 100 - Math.min(100, raw * 100));
      }

      var smellsByType = function (t) { return smells.filter(function (s) { return s.type === t; }).length; };
      var cleanliness  = norm(smells.length, Math.max(totalFunctions, 1), 0.5);
      var security     = norm(sec.length,    Math.max(totalFiles, 1),     0.5);
      var funcLength   = norm(smellsByType('long_function'),       Math.max(totalFunctions, 1), 1.0);
      var nesting      = norm(smellsByType('deep_nesting'),        Math.max(totalFunctions, 1), 1.0);
      var params       = norm(smellsByType('long_parameter_list'), Math.max(totalFunctions, 1), 1.0);

      return [cleanliness, security, funcLength, nesting, params];
    }
    function activatePill(sev) {
      if (sev === 'all') {
        activeSeverities = new Set(['all']);
      } else {
        activeSeverities.delete('all');
        if (activeSeverities.has(sev)) { activeSeverities.delete(sev); }
        else { activeSeverities.add(sev); }
        if (activeSeverities.size === 0) { activeSeverities.add('all'); }
      }
      document.querySelectorAll('#severity-pills .pill').forEach(function (p) {
        p.classList.toggle('active', activeSeverities.has(p.dataset.sev));
      });
      renderTable();
    }
    function rowMatchesFilter(r) {
      if (activeSeverities.has('all')) return true;
      if (activeSeverities.has('high')   && r.high   > 0) return true;
      if (activeSeverities.has('medium') && r.medium > 0) return true;
      if (activeSeverities.has('low')    && r.low    > 0) return true;
      return false;
    }
    function renderTable() {
      var rows = risks.filter(rowMatchesFilter).slice();
      rows.sort(function (a, b) {
        var av, bv;
        if (sortState.key === 'file')     { av = a.file; bv = b.file; }
        else if (sortState.key === 'severity') { av = a.high * 100 + a.medium * 10 + a.low; bv = b.high * 100 + b.medium * 10 + b.low; }
        else { av = a[sortState.key]; bv = b[sortState.key]; }
        var cmp = (av < bv) ? -1 : (av > bv ? 1 : 0);
        return sortState.dir === 'desc' ? -cmp : cmp;
      });
      var tbody = document.getElementById('files-tbody');
      tbody.innerHTML = '';
      if (rows.length === 0) {
        document.getElementById('table-empty').hidden = false;
        return;
      }
      document.getElementById('table-empty').hidden = true;
      rows.forEach(function (r) {
        var main = document.createElement('tr');
        main.className = 'row-main';
        main.dataset.file = r.file;
        main.appendChild(td('path', r.file));
        main.appendChild(td('num',  r.riskScore.toLocaleString()));
        main.appendChild(td('num',  r.lines.toLocaleString()));
        main.appendChild(severityTd(r));
        main.appendChild(stackTd(r));
        tbody.appendChild(main);

        var detail = document.createElement('tr');
        detail.className = 'row-detail hidden';
        var dtd = document.createElement('td'); dtd.colSpan = 5;
        var ul = document.createElement('ul');
        findingsForFile(r.file).forEach(function (f) { ul.appendChild(findingLi(f)); });
        if (ul.children.length === 0) {
          var empty = document.createElement('li'); empty.className = 'meta'; empty.textContent = 'No findings.'; ul.appendChild(empty);
        }
        dtd.appendChild(ul);
        detail.appendChild(dtd);
        tbody.appendChild(detail);

        main.addEventListener('click', function () {
          detail.classList.toggle('hidden');
        });
      });
      document.querySelectorAll('#files-table th[data-sort]').forEach(function (h) {
        h.classList.toggle('sorted', h.dataset.sort === sortState.key);
        var arrow = h.querySelector('.arrow');
        if (h.classList.contains('sorted')) arrow.textContent = (sortState.dir === 'desc' ? '↓' : '↑');
        else arrow.textContent = '↕';
      });
    }
    function td(cls, text) { var e = document.createElement('td'); e.className = cls; e.textContent = text; return e; }
    function severityTd(r) {
      var e = document.createElement('td');
      if (r.high > 0)   e.appendChild(badge('h', r.high));
      if (r.medium > 0) e.appendChild(badge('m', r.medium));
      if (r.low > 0)    e.appendChild(badge('l', r.low));
      return e;
    }
    function badge(cls, n) { var e = document.createElement('span'); e.className = 'badge ' + cls; e.textContent = n.toString(); return e; }
    function stackTd(r) {
      var e = document.createElement('td');
      var holder = document.createElement('span');
      holder.className = 'severity-stack';
      var total = r.high + r.medium + r.low;
      if (total === 0) { e.textContent = '—'; return e; }
      var widthFor = function (n) { return Math.max(4, Math.round(60 * n / total)) + 'px'; };
      if (r.high > 0)   { var s = document.createElement('span'); s.style.width = widthFor(r.high);   s.style.background = '#ef4444'; holder.appendChild(s); }
      if (r.medium > 0) { var s2 = document.createElement('span'); s2.style.width = widthFor(r.medium); s2.style.background = '#f59e0b'; holder.appendChild(s2); }
      if (r.low > 0)    { var s3 = document.createElement('span'); s3.style.width = widthFor(r.low);    s3.style.background = '#10b981'; holder.appendChild(s3); }
      e.appendChild(holder);
      return e;
    }
    function findingsForFile(file) {
      return smells.filter(function (s) { return s.file === file; })
        .concat(sec.filter(function (s) { return s.file === file; }));
    }
    function findingLi(f) {
      var li = document.createElement('li');
      var title = document.createElement('strong');
      title.textContent = (f.subtype || f.type) + ' (' + f.severity + ')';
      li.appendChild(title);
      var meta = document.createElement('div');
      meta.className = 'meta';
      var lineNum = (f.line !== undefined ? f.line : f.startLine);
      meta.textContent = (lineNum ? ('line ' + lineNum + ' · ') : '') + f.message;
      li.appendChild(meta);
      if (f.snippet) { var pre = document.createElement('pre'); pre.textContent = f.snippet; li.appendChild(pre); }
      return li;
    }
    function expandFileRow(file) {
      var rows = document.querySelectorAll('#files-tbody tr.row-main');
      for (var i = 0; i < rows.length; i++) {
        if (rows[i].dataset.file === file) {
          rows[i].scrollIntoView({ behavior: 'smooth', block: 'center' });
          var detail = rows[i].nextElementSibling;
          if (detail && detail.classList.contains('row-detail')) {
            detail.classList.remove('hidden');
          }
          return;
        }
      }
    }
    function scrollToTable() {
      document.querySelector('.table-panel').scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  })();
  </script>
</body>
</html>
""";
}
