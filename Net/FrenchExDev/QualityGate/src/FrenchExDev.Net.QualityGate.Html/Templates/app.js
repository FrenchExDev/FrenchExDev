(function () {
  "use strict";

  // ── State ──────────────────────────────────────────────────────────
  let runs = [];
  let selectedRunIds = []; // max 2 for diff

  // ── Bootstrap ──────────────────────────────────────────────────────
  document.addEventListener("DOMContentLoaded", () => {
    init();
    initWebSocket();
  });

  async function init() {
    try {
      const resp = await fetch("runs.json");
      if (!resp.ok) throw new Error("Failed to load runs.json");
      runs = await resp.json();
      renderRunList();
      if (runs.length >= 2) {
        show("trend-charts");
        renderTrends();
      }
    } catch (err) {
      document.getElementById("runs-table-container").innerHTML =
        '<p class="error">Could not load runs: ' + escapeHtml(err.message) + "</p>";
    }
  }

  // ── WebSocket Live Reload ────────────────────────────────────────
  function initWebSocket() {
    const wsPort = parseInt(location.port || "3000", 10) + 1;
    const wsUrl = "ws://" + location.hostname + ":" + wsPort + "/ws/";
    let ws = null;
    let reconnectTimer = null;
    let fallbackTimer = null;

    function connect() {
      try {
        ws = new WebSocket(wsUrl);
      } catch (_) {
        startFallbackPolling();
        return;
      }

      ws.onopen = function () {
        setLiveIndicator(true);
        if (reconnectTimer) { clearTimeout(reconnectTimer); reconnectTimer = null; }
        if (fallbackTimer) { clearInterval(fallbackTimer); fallbackTimer = null; }
      };

      ws.onmessage = function (event) {
        try {
          const msg = JSON.parse(event.data);
          if (msg.type === "reload") {
            init(); // re-fetch runs.json and re-render
          }
        } catch (_) {}
      };

      ws.onclose = function () {
        setLiveIndicator(false);
        ws = null;
        reconnectTimer = setTimeout(connect, 3000);
      };

      ws.onerror = function () {
        setLiveIndicator(false);
        if (ws) { try { ws.close(); } catch (_) {} }
      };
    }

    function startFallbackPolling() {
      if (fallbackTimer) return;
      let lastCount = runs.length;
      fallbackTimer = setInterval(async function () {
        try {
          const resp = await fetch("runs.json");
          if (resp.ok) {
            const data = await resp.json();
            if (data.length !== lastCount) {
              lastCount = data.length;
              init();
            }
          }
        } catch (_) {}
      }, 5000);
    }

    function setLiveIndicator(connected) {
      let el = document.getElementById("live-indicator");
      if (!el) {
        el = document.createElement("div");
        el.id = "live-indicator";
        el.style.cssText = "position:fixed;top:8px;right:8px;padding:4px 10px;border-radius:12px;font-size:12px;font-weight:bold;z-index:9999;";
        document.body.appendChild(el);
      }
      el.textContent = connected ? "Live" : "Offline";
      el.style.background = connected ? "#22c55e" : "#94a3b8";
      el.style.color = "#fff";
    }

    connect();
  }

  // ── Run List ───────────────────────────────────────────────────────
  function renderRunList() {
    const container = document.getElementById("runs-table-container");
    if (runs.length === 0) {
      container.innerHTML = "<p>No runs found.</p>";
      return;
    }

    let html = '<table><thead><tr>';
    html += "<th></th><th>Timestamp</th><th>Gates</th><th>Types</th><th>Methods</th>";
    html += "<th>Coverage</th><th>Mutation</th><th>Status</th><th></th>";
    html += "</tr></thead><tbody>";

    for (const run of runs) {
      const passed = run.gatesPassed || 0;
      const failed = run.gatesFailed || 0;
      const allPass = failed === 0;
      html += "<tr>";
      html += '<td><input type="checkbox" class="diff-check" data-id="' + escapeHtml(run.id) + '"></td>';
      html += "<td>" + escapeHtml(run.timestamp) + "</td>";
      html += "<td>" + passed + " / " + (passed + failed) + "</td>";
      html += "<td>" + (run.totalTypes || 0) + "</td>";
      html += "<td>" + (run.totalMethods || 0) + "</td>";
      html += "<td>" + formatPct(run.coveragePct) + "</td>";
      html += "<td>" + formatPct(run.mutationPct) + "</td>";
      html += '<td><span class="badge ' + (allPass ? "badge-pass" : "badge-fail") + '">' + (allPass ? "PASS" : "FAIL") + "</span></td>";
      html += '<td><button class="btn btn-small" onclick="window.__viewRun(\'' + escapeHtml(run.id) + "')\">" + "View</button></td>";
      html += "</tr>";
    }

    html += "</tbody></table>";
    container.innerHTML = html;

    // Wire diff checkboxes
    container.querySelectorAll(".diff-check").forEach(function (cb) {
      cb.addEventListener("change", onDiffCheckChanged);
    });

    show("diff-viewer");
  }

  function onDiffCheckChanged() {
    const checked = document.querySelectorAll(".diff-check:checked");
    selectedRunIds = Array.from(checked).map(function (cb) { return cb.dataset.id; });

    // Cap at 2
    if (selectedRunIds.length > 2) {
      this.checked = false;
      selectedRunIds = selectedRunIds.slice(0, 2);
    }

    document.getElementById("btn-diff").disabled = selectedRunIds.length !== 2;
  }

  document.addEventListener("click", function (e) {
    if (e.target && e.target.id === "btn-diff") { doDiff(); }
    if (e.target && e.target.id === "btn-clear-diff") { clearDiff(); }
  });

  // ── View Single Run ────────────────────────────────────────────────
  window.__viewRun = async function (runId) {
    const panel = document.getElementById("detail-panel");
    const content = document.getElementById("detail-content");
    show("detail-panel");
    content.innerHTML = "<p class=\"loading\">Loading&hellip;</p>";

    try {
      const resp = await fetch(runId + "/report.json");
      if (!resp.ok) throw new Error("Failed to load report");
      const report = await resp.json();
      content.innerHTML = renderReportDetail(report);
    } catch (err) {
      content.innerHTML = '<p class="error">' + escapeHtml(err.message) + "</p>";
    }
  };

  function renderReportDetail(r) {
    let html = "<h3>" + escapeHtml(r.solutionPath || "") + "</h3>";
    html += "<p>" + escapeHtml(r.timestamp || "") + "</p>";

    // Gates
    if (r.gateResults && r.gateResults.length) {
      html += '<table><thead><tr><th>Gate</th><th>Threshold</th><th>Actual</th><th>Status</th></tr></thead><tbody>';
      for (const g of r.gateResults) {
        html += "<tr><td>" + escapeHtml(g.gateName) + "</td>";
        html += '<td class="mono">' + g.threshold + "</td>";
        html += '<td class="mono">' + g.actualValue + "</td>";
        html += "<td>" + (g.passed ? '<span class="pass">PASS</span>' : '<span class="fail">FAIL</span>') + "</td></tr>";
      }
      html += "</tbody></table>";
    }

    // Coverage / Mutation summary
    if (r.coverage) {
      html += '<div class="cards"><div class="card"><div class="label">Line Coverage</div><div class="value">' + (r.coverage.lineRate * 100).toFixed(1) + "%</div></div>";
      html += '<div class="card"><div class="label">Branch Coverage</div><div class="value">' + (r.coverage.branchRate * 100).toFixed(1) + "%</div></div></div>";
    }
    if (r.mutation) {
      html += '<div class="cards"><div class="card"><div class="label">Mutation Score</div><div class="value">' + (r.mutation.mutationScore * 100).toFixed(1) + "%</div></div>";
      html += '<div class="card"><div class="label">Killed / Total</div><div class="value">' + r.mutation.killed + " / " + r.mutation.totalMutants + "</div></div></div>";
    }

    return html;
  }

  // ── Diff Engine ────────────────────────────────────────────────────
  async function doDiff() {
    const content = document.getElementById("diff-content");
    const clearBtn = document.getElementById("btn-clear-diff");
    content.innerHTML = "<p class=\"loading\">Computing diff&hellip;</p>";
    clearBtn.classList.remove("hidden");

    try {
      const [respA, respB] = await Promise.all([
        fetch(selectedRunIds[0] + "/report.json"),
        fetch(selectedRunIds[1] + "/report.json")
      ]);
      if (!respA.ok || !respB.ok) throw new Error("Failed to load reports");
      const a = await respA.json();
      const b = await respB.json();
      content.innerHTML = renderDiff(a, b);
    } catch (err) {
      content.innerHTML = '<p class="error">' + escapeHtml(err.message) + "</p>";
    }
  }

  function clearDiff() {
    document.getElementById("diff-content").innerHTML = "";
    document.getElementById("btn-clear-diff").classList.add("hidden");
  }

  function renderDiff(a, b) {
    let html = '<div class="diff-grid">';

    // Metric comparison
    const metrics = extractMetrics(a);
    const metricsB = extractMetrics(b);
    html += '<div class="diff-col"><h3>Run A: ' + escapeHtml(a.timestamp || "") + "</h3></div>";
    html += '<div class="diff-col"><h3>Run B: ' + escapeHtml(b.timestamp || "") + "</h3></div>";
    html += "</div>";

    html += "<table><thead><tr><th>Metric</th><th>Run A</th><th>Run B</th><th>Delta</th></tr></thead><tbody>";
    const keys = Object.keys(metrics);
    for (const key of keys) {
      const va = metrics[key];
      const vb = metricsB[key] !== undefined ? metricsB[key] : 0;
      const delta = vb - va;
      const cls = delta > 0 ? "delta-pos" : delta < 0 ? "delta-neg" : "";
      const sign = delta > 0 ? "+" : "";
      html += "<tr><td>" + escapeHtml(key) + "</td>";
      html += '<td class="mono">' + formatNum(va) + "</td>";
      html += '<td class="mono">' + formatNum(vb) + "</td>";
      html += '<td class="mono ' + cls + '">' + sign + formatNum(delta) + "</td></tr>";
    }
    html += "</tbody></table>";

    // Gate status changes
    if (a.gateResults && b.gateResults) {
      const gatesA = Object.fromEntries(a.gateResults.map(function (g) { return [g.gateName, g]; }));
      const gatesB = Object.fromEntries(b.gateResults.map(function (g) { return [g.gateName, g]; }));
      const allGates = new Set([...Object.keys(gatesA), ...Object.keys(gatesB)]);
      let changed = [];

      for (const name of allGates) {
        const ga = gatesA[name];
        const gb = gatesB[name];
        if (!ga || !gb) {
          changed.push({ name: name, statusA: ga ? ga.passed : "N/A", statusB: gb ? gb.passed : "N/A" });
        } else if (ga.passed !== gb.passed) {
          changed.push({ name: name, statusA: ga.passed, statusB: gb.passed });
        }
      }

      if (changed.length) {
        html += "<h3 style=\"margin-top:1rem;\">Gate Status Changes</h3>";
        html += "<table><thead><tr><th>Gate</th><th>Run A</th><th>Run B</th></tr></thead><tbody>";
        for (const c of changed) {
          html += "<tr><td>" + escapeHtml(c.name) + "</td>";
          html += "<td>" + formatStatus(c.statusA) + "</td>";
          html += "<td>" + formatStatus(c.statusB) + "</td></tr>";
        }
        html += "</tbody></table>";
      }
    }

    return html;
  }

  function extractMetrics(report) {
    const m = {};
    let totalTypes = 0, totalMethods = 0, totalCC = 0;
    if (report.projects) {
      for (const p of report.projects) {
        if (p.namespaces) {
          for (const ns of p.namespaces) {
            totalTypes += (ns.types || []).length;
            for (const t of (ns.types || [])) {
              totalCC += t.cyclomaticComplexity || 0;
              totalMethods += (t.methods || []).length;
            }
          }
        }
      }
    }
    m["Total Types"] = totalTypes;
    m["Total Methods"] = totalMethods;
    m["Total Cyclomatic Complexity"] = totalCC;
    if (report.coverage) {
      m["Line Coverage %"] = +(report.coverage.lineRate * 100).toFixed(1);
      m["Branch Coverage %"] = +(report.coverage.branchRate * 100).toFixed(1);
    }
    if (report.mutation) {
      m["Mutation Score %"] = +(report.mutation.mutationScore * 100).toFixed(1);
      m["Killed Mutants"] = report.mutation.killed;
      m["Survived Mutants"] = report.mutation.survived;
    }
    if (report.duplication) {
      m["Duplication %"] = +report.duplication.duplicationPercent.toFixed(1);
    }
    return m;
  }

  function formatStatus(val) {
    if (val === "N/A") return "<em>N/A</em>";
    return val ? '<span class="pass">PASS</span>' : '<span class="fail">FAIL</span>';
  }

  // ── Trend Charts (SVG) ────────────────────────────────────────────
  function renderTrends() {
    const container = document.getElementById("trend-container");
    if (runs.length < 2) { container.innerHTML = "<p>Need at least 2 runs for trends.</p>"; return; }

    const series = [
      { label: "Coverage %", key: "coveragePct", color: "#2563eb" },
      { label: "Mutation %", key: "mutationPct", color: "#7c3aed" },
      { label: "Total Types", key: "totalTypes", color: "#059669" },
      { label: "Total Methods", key: "totalMethods", color: "#d97706" }
    ];

    let html = '<div class="trend-grid">';
    for (const s of series) {
      const values = runs.map(function (r) { return r[s.key] || 0; });
      html += '<div class="trend-card">';
      html += "<div class=\"trend-label\">" + escapeHtml(s.label) + "</div>";
      html += renderSvgChart(values, s.color, runs.map(function (r) { return r.timestamp || ""; }));
      html += "</div>";
    }
    html += "</div>";
    container.innerHTML = html;
  }

  function renderSvgChart(values, color, labels) {
    const w = 320, h = 140, pad = 30;
    const n = values.length;
    if (n === 0) return "";

    const min = Math.min.apply(null, values);
    const max = Math.max.apply(null, values);
    const range = max - min || 1;

    let points = [];
    for (let i = 0; i < n; i++) {
      const x = pad + (i / (n - 1 || 1)) * (w - 2 * pad);
      const y = h - pad - ((values[i] - min) / range) * (h - 2 * pad);
      points.push(x + "," + y);
    }

    let svg = '<svg viewBox="0 0 ' + w + " " + h + '" class="trend-svg">';
    // Grid lines
    svg += '<line x1="' + pad + '" y1="' + (h - pad) + '" x2="' + (w - pad) + '" y2="' + (h - pad) + '" stroke="#e2e8f0" />';
    svg += '<line x1="' + pad + '" y1="' + pad + '" x2="' + (w - pad) + '" y2="' + pad + '" stroke="#e2e8f0" />';
    // Axis labels
    svg += '<text x="' + pad + '" y="' + (h - 8) + '" font-size="9" fill="#64748b">' + escapeHtml(String(min)) + "</text>";
    svg += '<text x="' + pad + '" y="' + (pad - 5) + '" font-size="9" fill="#64748b">' + escapeHtml(String(max)) + "</text>";
    // Line
    svg += '<polyline fill="none" stroke="' + color + '" stroke-width="2" points="' + points.join(" ") + '" />';
    // Dots
    for (let i = 0; i < n; i++) {
      const parts = points[i].split(",");
      svg += '<circle cx="' + parts[0] + '" cy="' + parts[1] + '" r="3" fill="' + color + '"><title>' + escapeHtml(labels[i]) + ": " + values[i] + "</title></circle>";
    }
    svg += "</svg>";
    return svg;
  }

  // ── Helpers ────────────────────────────────────────────────────────
  function show(id) { document.getElementById(id).classList.remove("hidden"); }
  function escapeHtml(str) { var d = document.createElement("div"); d.textContent = str; return d.innerHTML; }
  function formatPct(v) { return v != null ? v.toFixed(1) + "%" : "N/A"; }
  function formatNum(v) { return typeof v === "number" ? (Number.isInteger(v) ? String(v) : v.toFixed(1)) : String(v); }

})();
