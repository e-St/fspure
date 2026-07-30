// @ts-check
/**
 * Phase 2 visual capture — mirrors the manual consumer codespace flow:
 *   1) Load customer-fixture.slnx into Ionide (workspace / solution)
 *   2) Open Program.fs
 *   3) Wait for Ionide type/parameter inlay hints (project ready)
 *   4) Wait for pure/impure end-of-line decorations
 *   5) Screenshot
 *
 * Env:
 *   CODE_SERVER_URL   default http://127.0.0.1:8080
 *   ARTIFACTS_DIR     output directory for PNGs
 *   WAIT_MS           max wait for labels (default 180000)
 *   SOLUTION_NAME     default customer-fixture.slnx
 */
import { chromium } from "playwright";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const BASE = process.env.CODE_SERVER_URL || "http://127.0.0.1:8080";
const ARTIFACTS =
  process.env.ARTIFACTS_DIR ||
  path.resolve(__dirname, "../../../.artifacts/phase2");
const WAIT_MS = Number(process.env.WAIT_MS || 180_000);
const FILE_PATH = "Program.fs";
const SOLUTION_NAME = process.env.SOLUTION_NAME || "customer-fixture.slnx";

fs.mkdirSync(ARTIFACTS, { recursive: true });

function log(...args) {
  console.log("[phase2-screenshot]", ...args);
}

/**
 * @param {import('playwright').Page} page
 */
async function dismissNoise(page) {
  for (let i = 0; i < 6; i++) {
    await page.keyboard.press("Escape").catch(() => {});
    await page.waitForTimeout(250);
  }
  // Notifications / modal buttons
  const closers = page.locator(
    [
      ".notification-toast .codicon-notifications-clear",
      ".notification-list-item-toolbar-container .codicon-close",
      ".monaco-dialog-box .dialog-buttons .monaco-button",
      ".welcome-view .button-container a",
    ].join(", ")
  );
  const count = await closers.count().catch(() => 0);
  for (let i = 0; i < Math.min(count, 8); i++) {
    await closers
      .nth(i)
      .click({ timeout: 800 })
      .catch(() => {});
  }
}

/**
 * Run a command-palette entry by typing its label and pressing Enter.
 * @param {import('playwright').Page} page
 * @param {string} query
 */
async function runCommand(page, query) {
  await page.keyboard.press("Control+Shift+P");
  await page.waitForTimeout(600);
  // Clear any previous filter
  await page.keyboard.press("Control+A").catch(() => {});
  await page.keyboard.type(query, { delay: 25 });
  await page.waitForTimeout(900);
  await page.keyboard.press("Enter");
  await page.waitForTimeout(1200);
}

/**
 * Select a quick-pick row by typing a filter string.
 * @param {import('playwright').Page} page
 * @param {string} filter
 */
async function pickQuickOpen(page, filter) {
  await page.waitForTimeout(500);
  await page.keyboard.press("Control+A").catch(() => {});
  await page.keyboard.type(filter, { delay: 30 });
  await page.waitForTimeout(900);
  await page.keyboard.press("Enter");
  await page.waitForTimeout(1500);
}

/**
 * Manual-flow step 1: force Ionide onto customer-fixture.slnx.
 * FSharp.workspacePath is set in settings; this command re-asserts it when
 * Ionide still shows a solution picker (common on first launch).
 * @param {import('playwright').Page} page
 */
async function loadSolution(page) {
  log("loading Ionide workspace/solution:", SOLUTION_NAME);

  // Prefer Ionide's explicit workspace picker (same action as manual testing).
  const commands = [
    "F#: Change Workspace or Solution",
    "F# Change Workspace or Solution",
    "Ionide: Change Workspace or Solution",
  ];

  for (const cmd of commands) {
    await dismissNoise(page);
    await runCommand(page, cmd);
    // If a quick-pick opened, filter to our slnx
    const picker = page.locator(
      ".quick-input-widget:visible, .monaco-list:visible .monaco-list-row"
    );
    const visible = await picker
      .first()
      .isVisible()
      .catch(() => false);
    if (visible) {
      await pickQuickOpen(page, SOLUTION_NAME.replace(/\.slnx$/, ""));
      log("selected solution via command:", cmd);
      await page.waitForTimeout(4000);
      return;
    }
    await page.keyboard.press("Escape").catch(() => {});
  }

  // Fallback: open the slnx file itself (C# / Ionide often treat this as load).
  log("command palette workspace change unavailable; quick-opening", SOLUTION_NAME);
  await page.keyboard.press("Control+P");
  await page.waitForTimeout(600);
  await pickQuickOpen(page, SOLUTION_NAME);
  await page.waitForTimeout(3000);

  // Also try "Open Solution" style commands used by C# extension
  await runCommand(page, "Open Solution");
  const openSolPicker = await page
    .locator(".quick-input-widget:visible")
    .first()
    .isVisible()
    .catch(() => false);
  if (openSolPicker) {
    await pickQuickOpen(page, SOLUTION_NAME.replace(/\.slnx$/, ""));
    log("selected solution via Open Solution");
    await page.waitForTimeout(3000);
  } else {
    await page.keyboard.press("Escape").catch(() => {});
  }
}

/**
 * Manual-flow step 2: open Program.fs in the editor.
 * @param {import('playwright').Page} page
 */
async function openProgramFs(page) {
  await dismissNoise(page);
  await page.keyboard.press("Control+P");
  await page.waitForTimeout(700);
  await pickQuickOpen(page, FILE_PATH);

  const editor = page.locator(".monaco-editor").first();
  await editor.waitFor({ state: "visible", timeout: 90_000 });
  await editor.click({ timeout: 15_000 }).catch(() => {});
  log("editor visible for", FILE_PATH);

  // Ensure F# language mode is active (Ionide + decorations activate on fsharp)
  await runCommand(page, "Change Language Mode");
  const langPicker = await page
    .locator(".quick-input-widget:visible")
    .first()
    .isVisible()
    .catch(() => false);
  if (langPicker) {
    await pickQuickOpen(page, "F#");
    log("set language mode to F#");
  } else {
    await page.keyboard.press("Escape").catch(() => {});
  }

  await editor.click({ timeout: 5000 }).catch(() => {});
  // Give FSAC time to attach after solution + file open
  await page.waitForTimeout(8000);
}

/**
 * Snapshot UI signals used for readiness logging.
 * @param {import('playwright').Page} page
 */
async function probeUi(page) {
  return page.evaluate(() => {
    const viewLines =
      document.querySelector(".monaco-editor .view-lines") ||
      document.querySelector(".monaco-editor");
    const editorText = viewLines?.innerText || viewLines?.textContent || "";
    const editorHtml = viewLines?.innerHTML || "";
    const bodyText = document.body?.innerText || "";
    const statusBar =
      document.querySelector("#workbench\\.parts\\.statusbar")?.textContent ||
      document.querySelector(".statusbar")?.textContent ||
      "";

    const word = (w, text) =>
      new RegExp(`(^|[^a-zA-Z])${w}([^a-zA-Z]|$)`, "m").test(text || "");

    const sawImpure =
      word("impure", editorText) || word("impure", editorHtml);
    const sawPure = word("pure", editorText) || word("pure", editorHtml);

    // Inlay hints (type / parameter) — signal that Ionide project is loaded
    const inlayNodes = document.querySelectorAll(
      [
        ".monaco-editor .codicon-symbol-parameter",
        ".monaco-editor .ghost-text-decoration",
        ".monaco-editor .ghost-text",
        ".monaco-editor [class*='inlayHint']",
        ".monaco-editor [class*='inlay-hint']",
        ".monaco-editor [class*='InlayHint']",
        ".monaco-editor span[class*='inline-injected']",
      ].join(",")
    );
    let inlayCount = inlayNodes.length;
    // Type-ish annotations often show as ": int" / ": string" injected next to bindings
    const typeAnnoHits = (editorText.match(/:\s*(int|string|bool|unit|list|float|decimal|obj)\b/gi) || [])
      .length;
    if (typeAnnoHits > 0) inlayCount += typeAnnoHits;

    const sawInlayish = inlayCount > 0;

    // Real analyzer diagnostics (not the DLL filename in the explorer)
    const sawAnalyzerHint =
      /PURE00[123]/.test(bodyText) ||
      /Pure analyzer/i.test(bodyText) ||
      /PURE00[123]/.test(editorText);

    const solutionLoaded =
      /customer-fixture/i.test(statusBar) ||
      /Ionide/i.test(statusBar) ||
      /FSAC|F#/i.test(statusBar);

    return {
      sawImpure,
      sawPure,
      sawInlayish,
      sawAnalyzerHint,
      solutionLoaded,
      inlayCount,
      typeAnnoHits,
      statusBar: statusBar.slice(0, 200),
      editorSnippet: editorText.slice(0, 400),
    };
  });
}

/**
 * Re-run workspace change if Ionide still looks idle after a while.
 * @param {import('playwright').Page} page
 * @param {number} nudge
 */
async function nudgeIonide(page, nudge) {
  if (nudge === 3) {
    log("nudge: re-assert Ionide workspace →", SOLUTION_NAME);
    await loadSolution(page);
    await openProgramFs(page);
    return;
  }
  if (nudge % 5 === 1) {
    await page.keyboard.press("Control+S").catch(() => {});
  } else if (nudge % 5 === 2) {
    await page.keyboard.press("Control+Home").catch(() => {});
    await page.waitForTimeout(150);
    await page.keyboard.press("Control+End").catch(() => {});
    await page.waitForTimeout(150);
    await page.keyboard.press("Control+Home").catch(() => {});
  } else if (nudge % 5 === 3) {
    // Toggle Problems panel — sometimes forces diagnostic refresh
    await page.keyboard.press("Control+Shift+M").catch(() => {});
    await page.waitForTimeout(500);
  } else if (nudge % 5 === 4) {
    const editor = page.locator(".monaco-editor").first();
    await editor.click({ timeout: 3000 }).catch(() => {});
    // Tiny edit + undo to poke FSAC
    await page.keyboard.type(" ", { delay: 20 }).catch(() => {});
    await page.keyboard.press("Control+Z").catch(() => {});
  }
}

/**
 * @param {import('playwright').Page} page
 */
async function waitForBadges(page) {
  const deadline = Date.now() + WAIT_MS;
  let sawImpure = false;
  let sawPure = false;
  let sawInlayish = false;
  let lastLog = 0;
  let nudge = 0;

  while (Date.now() < deadline) {
    const probe = await probeUi(page);
    sawImpure = sawImpure || probe.sawImpure;
    sawPure = sawPure || probe.sawPure;
    sawInlayish = sawInlayish || probe.sawInlayish;

    if (Date.now() - lastLog > 15_000) {
      log("still waiting…", {
        sawImpure,
        sawPure,
        sawInlayish,
        sawAnalyzerHint: probe.sawAnalyzerHint,
        solutionLoaded: probe.solutionLoaded,
        inlayCount: probe.inlayCount,
        typeAnnoHits: probe.typeAnnoHits,
        statusBar: probe.statusBar,
        remainingMs: deadline - Date.now(),
      });
      lastLog = Date.now();
    }

    if (sawImpure && sawPure) {
      log("found both pure and impure labels", { sawInlayish });
      await page.waitForTimeout(2000);
      return { sawImpure, sawPure, sawInlayish, timedOut: false };
    }

    nudge += 1;
    await nudgeIonide(page, nudge);
    await page.waitForTimeout(3000);
  }

  log("timeout waiting for badges", { sawImpure, sawPure, sawInlayish, WAIT_MS });
  return { sawImpure, sawPure, sawInlayish, timedOut: true };
}

/**
 * @param {import('playwright').Page} page
 * @param {{ sawImpure: boolean, sawPure: boolean, sawInlayish?: boolean, timedOut: boolean }} badgeState
 */
async function capture(page, badgeState) {
  const editor = page.locator(".monaco-editor").first();
  await editor.waitFor({ state: "visible" });

  await page.keyboard.press("Control+Home").catch(() => {});
  await page.waitForTimeout(800);

  const shots = {
    full: path.join(ARTIFACTS, "program-fs-full.png"),
    editor: path.join(ARTIFACTS, "program-fs-editor.png"),
    impureSection: path.join(ARTIFACTS, "program-fs-impure-section.png"),
    pureSection: path.join(ARTIFACTS, "program-fs-pure-section.png"),
  };

  await page.screenshot({ path: shots.full, fullPage: true });
  log("wrote", shots.full);

  await editor.screenshot({ path: shots.impureSection }).catch(async () => {
    await page.screenshot({ path: shots.impureSection });
  });
  log("wrote", shots.impureSection);

  await editor.screenshot({ path: shots.editor }).catch(async () => {
    await page.screenshot({ path: shots.editor });
  });
  log("wrote", shots.editor);

  await page.keyboard.press("Control+End").catch(() => {});
  await page.waitForTimeout(400);
  for (let i = 0; i < 10; i++) {
    await page.keyboard.press("PageUp").catch(() => {});
  }
  await page.waitForTimeout(600);
  await editor.screenshot({ path: shots.pureSection }).catch(async () => {
    await page.screenshot({ path: shots.pureSection });
  });
  log("wrote", shots.pureSection);

  const finalProbe = await probeUi(page);
  const meta = {
    capturedAt: new Date().toISOString(),
    codeServerUrl: BASE,
    file: FILE_PATH,
    solution: SOLUTION_NAME,
    badges: badgeState,
    probe: finalProbe,
    screenshots: Object.keys(shots).map((k) => path.basename(shots[k])),
    reviewGuide: {
      impureShouldInclude: [
        "logSideEffect",
        "mutateGlobal",
        "pureAdd",
        "pureMultiply",
        "pureProcessBatch",
        "main",
      ],
      pureShouldInclude: ["add", "isEmpty", "myEmpty", "double", "purePipeline"],
      flow: "slnx loaded → Program.fs open → Ionide inlays → pure/impure badges",
    },
  };
  fs.writeFileSync(
    path.join(ARTIFACTS, "screenshot-meta.json"),
    JSON.stringify(meta, null, 2) + "\n"
  );
  return meta;
}

async function main() {
  log("connecting to", BASE);
  log("artifacts →", ARTIFACTS);
  log("solution →", SOLUTION_NAME);

  const browser = await chromium.launch({
    headless: true,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1100 },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  page.setDefaultTimeout(60_000);

  try {
    await page.goto(BASE, { waitUntil: "domcontentloaded", timeout: 60_000 });
    await page.waitForTimeout(3000);
    await dismissNoise(page);

    // Match manual testing: select solution first, then open Program.fs
    await loadSolution(page);
    await openProgramFs(page);

    log("waiting for Ionide / analyzer / decorations (up to", WAIT_MS, "ms)");
    const badgeState = await waitForBadges(page);
    const meta = await capture(page, badgeState);

    if (badgeState.timedOut || !badgeState.sawImpure || !badgeState.sawPure) {
      console.error(
        "Phase 2: did not observe both pure and impure labels in the editor UI.",
        badgeState
      );
      console.error(
        "Screenshots were still saved for visual inspection under",
        ARTIFACTS
      );
      process.exitCode = 1;
    } else {
      log("Phase 2 visual capture OK", meta.screenshots);
      if (!badgeState.sawInlayish) {
        log(
          "note: pure/impure labels found, but inlay-hint widgets were not clearly detected (may still be visible in PNGs)"
        );
      }
    }
  } finally {
    await page
      .screenshot({
        path: path.join(ARTIFACTS, "program-fs-final-state.png"),
        fullPage: true,
      })
      .catch(() => {});
    await browser.close();
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
