const workflowSteps = {
  preview: {
    tool: "preview_axaml",
    title: "Render the real project view",
    description: "Build in isolation, load application resources and design data, then render the requested AXAML through Avalonia and Skia.",
    points: [
      "Theme, size, DPI, culture, and state variants",
      "Project build diagnostics with bounded artifacts",
      "Persistent sessions with explicit reload"
    ],
    json: `{
  "projectPath": "Sample.csproj",
  "viewPath": "Views/MainView.axaml",
  "themeVariant": "Dark",
  "width": 720,
  "height": 420
}`,
    visual: "preview"
  },
  inspect: {
    tool: "inspect_node",
    title: "Understand the live control tree",
    description: "Select controls by stable semantic identity and retrieve the state an agent needs to explain what the application rendered.",
    points: [
      "Visual and logical tree snapshots",
      "Bindings, DataContext, resources, and source provenance",
      "Bounds, clipping, accessibility, and validation state"
    ],
    json: `{
  "target": { "automationId": "trade-confirm" },
  "nodeType": "Button",
  "isVisible": true,
  "availableActions": ["invoke"],
  "sourcePath": "Views/TradeView.axaml:84"
}`,
    visual: "inspect"
  },
  act: {
    tool: "run_workflow",
    title: "Perform a bounded semantic action",
    description: "Resolve the target immediately before use, execute through Avalonia automation where possible, and avoid persisting fragile runtime ids or coordinates.",
    points: [
      "Invoke, select, toggle, type, scroll, drag, and swipe",
      "Custom app actions with explicit allowlists",
      "Dry-run validation and idempotency protection"
    ],
    json: `{
  "action": "invoke",
  "selector": { "automationId": "trade-confirm" },
  "verify": {
    "condition": "top_level_opened",
    "title": "Confirm trade"
  }
}`,
    visual: "act"
  },
  verify: {
    tool: "semantic_diff",
    title: "Verify the result and keep the evidence",
    description: "Wait for a typed postcondition, capture the resulting UI, and export compact summaries plus complete local artifacts for review or CI.",
    points: [
      "Before/after screenshots and semantic visual findings",
      "JSON, Markdown, HTML, JUnit, and SARIF-style reports",
      "Privacy policy, masking, redaction, and bounded retention"
    ],
    json: `{
  "status": "passed",
  "assertion": "dialog opened",
  "changedPixels": 18442,
  "artifacts": [
    "after.png", "workflow-report.html"
  ]
}`,
    visual: "verify"
  }
};

const tabs = [...document.querySelectorAll("[data-step]")];
const panel = document.querySelector("#workflow-panel");
const title = document.querySelector("#workflow-title");
const tool = document.querySelector("#workflow-tool");
const description = document.querySelector("#workflow-description");
const points = document.querySelector("#workflow-points");
const json = document.querySelector("#workflow-json");
const visual = document.querySelector("#workflow-visual");

function selectStep(stepName, focusPanel = false) {
  const step = workflowSteps[stepName];
  if (!step) return;

  tabs.forEach((tab) => tab.setAttribute("aria-selected", String(tab.dataset.step === stepName)));
  tool.textContent = step.tool;
  title.textContent = step.title;
  description.textContent = step.description;
  points.replaceChildren(...step.points.map((point) => {
    const item = document.createElement("li");
    item.textContent = point;
    return item;
  }));
  json.textContent = step.json;

  if (step.visual === "preview") {
    visual.innerHTML = '<img src="assets/getting-started-preview.png" width="720" height="420" alt="AvaScope rendering of the Getting Started Avalonia sample"><span class="visual-badge">real PreviewHost output</span>';
  } else {
    const labels = {
      inspect: ["Window", "Grid", "Button #trade-confirm", "Bounds 812, 644 · 124×40"],
      act: ["Resolve selector", "Invoke provider", "Wait for dialog", "Capture post-state"],
      verify: ["Pre-state", "Action", "Postcondition passed", "Evidence retained"]
    };
    visual.innerHTML = `<div class="visual-sequence ${step.visual}">${labels[step.visual].map((label, index) => `<div><span>${String(index + 1).padStart(2, "0")}</span><strong>${label}</strong>${index < 3 ? "<i></i>" : ""}</div>`).join("")}</div><span class="visual-badge">structured ${step.visual} result</span>`;
  }

  if (focusPanel) panel.focus({ preventScroll: true });
}

tabs.forEach((tab, index) => {
  tab.addEventListener("click", () => selectStep(tab.dataset.step));
  tab.addEventListener("keydown", (event) => {
    if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") return;
    event.preventDefault();
    const direction = event.key === "ArrowRight" ? 1 : -1;
    const next = tabs[(index + direction + tabs.length) % tabs.length];
    next.focus();
    selectStep(next.dataset.step);
  });
});

const style = document.createElement("style");
style.textContent = `.visual-sequence{display:grid;align-content:center;gap:0;width:100%;height:100%;padding:clamp(24px,5vw,56px);background:radial-gradient(circle at 80% 15%,rgba(25,198,174,.12),transparent 45%),#071113}.visual-sequence>div{display:grid;grid-template-columns:34px 1fr;align-items:center;position:relative;min-height:62px;color:#eef8f6}.visual-sequence span{color:#718986;font:700 .7rem monospace}.visual-sequence strong{padding:12px 14px;border:1px solid rgba(184,228,219,.14);border-radius:9px;background:rgba(255,255,255,.025);font-size:.85rem}.visual-sequence i{position:absolute;left:16px;top:48px;width:1px;height:28px;background:rgba(25,198,174,.4)}.visual-sequence>div:last-child strong{border-color:rgba(25,198,174,.5);color:#64ead6;background:rgba(25,198,174,.08)}.visual-sequence.act>div:nth-child(2) strong{border-color:rgba(123,181,255,.45);color:#b9d8ff}.visual-sequence.verify>div:nth-child(3) strong{border-color:rgba(25,198,174,.5);color:#64ead6}`;
document.head.appendChild(style);

const copyButton = document.querySelector("#copy-command");
copyButton.addEventListener("click", async () => {
  const command = document.querySelector("#quick-command").textContent;
  try {
    await navigator.clipboard.writeText(command);
    copyButton.textContent = "Copied";
    window.setTimeout(() => { copyButton.textContent = "Copy"; }, 1800);
  } catch {
    copyButton.textContent = "Select text";
  }
});

fetch("release.json", { cache: "no-store" })
  .then((response) => response.ok ? response.json() : null)
  .then((release) => {
    if (!release?.tagName) return;
    const published = release.publishedAt ? new Date(release.publishedAt).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" }) : null;
    document.querySelector("#release-label").textContent = `${release.tagName} stable${published ? ` · ${published}` : ""}`;
  })
  .catch(() => {});
