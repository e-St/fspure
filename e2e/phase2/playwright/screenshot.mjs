// @ts-check
/**
 * Phase 2 visual capture:
 *   open customer-fixture/Program.fs in code-server (VS Code Web),
 *   wait for pure/impure decoration labels (and Ionide readiness), take screenshots.
 *
 * Env:
 *   CODE_SERVER_URL   default http://127.0.0.1:8080
 *   ARTIFACTS_DIR     output directory for PNGs
 *   WAIT_MS           max wait for labels (default 180000)
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

fs.mkdirSync(ARTIFACTS, { recursive: true });

function log(...args) {
  console.log("[phase2-screenshot]", ...args);
}

/**
 * @param {import('playwright').Page} page
 */
async function dismissNoise(page) {
  for (let i = 0; i < 5; i++) {
    await page.keyboard.press("Escape").catch(() => {});
    await page.waitForTimeout(300);
  }
  // Notifications "x" if present
  const notifClose = page.locator(
    ".notification-toast .codicon-notifications-clear, .notification-list-item-toolbar-container .codicon-close, .monaco-dialog-box .dialog-buttons .monaco-button"
  );
  const count = await notifClose.count().catch(() => 0);
  for (let i = 0; i < Math.min(count, 5); i++) {
    await notifClose
      .nth(i)
      .click({ timeout: 1000 })
      .catch(() => {});
  }
}

/**
 * @param {import('playwright').Page} page
 */
async function openProgramFs(page) {
  await page.goto(BASE, { waitUntil: "domcontentloaded", timeout: 60_000 });
  await page.waitForTimeout(2000);
  await dismissNoise(page);

  // Quick Open → Program.fs
  await page.keyboard.press("Control+P");
  await page.waitForTimeout(700);
  await page.keyboard.type(FILE_PATH, { delay: 40 });
  await page.waitForTimeout(1000);
  await page.keyboard.press("Enter");
  await page.waitForTimeout(2500);
  await dismissNoise(page);

  const editor = page.locator(".monaco-editor").first();
  await editor.waitFor({ state: "visible", timeout: 90_000 });
  await editor.click({ timeout: 15_000 }).catch(() => {});
  log("editor visible for", FILE_PATH);

  // Give Ionide a moment to attach language features after open
  await page.waitForTimeout(5000);
}

/**
 * Probe the Monaco editor for decoration labels and inlay-hint presence.
 * Prefer editor view text over full-page body so chrome / settings do not
 * create false positives. Fixture source intentionally avoids bare
 * pure/impure words outside identifiers like pureAdd.
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

    // Whole-word pure/impure — decorations inject these as end-of-line text.
    const word = (w, text) =>
      new RegExp(`(^|[^a-zA-Z])${w}([^a-zA-Z]|$)`, "m").test(text);

    const sawImpure =
      word("impure", editorText) || word("impure", editorHtml);
    // "pure" must not match inside "impure" (already excluded by word) or pureAdd
    // (letter after "pure" fails the trailing boundary).
    const sawPure = word("pure", editorText) || word("pure", editorHtml);

    // Inlay hints: Monaco ghost text / inlay widgets
    const inlayCount = document.querySelectorAll(
      [
        ".monaco-editor .codicon-symbol-parameter",
        ".monaco-editor .ghost-text-decoration",
        ".monaco-editor .ghost-text",
        ".monaco-editor [class*='inlayHint']",
        ".monaco-editor [class*='inlay-hint']",
        ".monaco-editor [class*='InlayHint']",
      ].join(",")
    ).length;
    const sawInlayish = inlayCount > 0;

    const sawAnalyzerHint =
      /PURE00[123]|Pure analyzer|FSharp\.PureAnalyzer/.test(bodyText) ||
      /PURE00[123]|Pure analyzer|FSharp\.PureAnalyzer/.test(editorText);

    return {
      sawImpure,
      sawPure,
      sawInlayish,
      sawAnalyzerHint,
      inlayCount,
      editorSnippet: editorText.slice(0, 500),
    };
  });
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
        remainingMs: deadline - Date.now(),
      });
      lastLog = Date.now();
    }

    if (sawImpure && sawPure) {
      log("found both pure and impure labels", { sawInlayish });
      // Extra settle so inlay hints can paint before capture
      await page.waitForTimeout(2000);
      return { sawImpure, sawPure, sawInlayish, timedOut: false };
    }

    // Nudge language service / analyzers / decorations
    nudge += 1;
    if (nudge % 4 === 1) {
      // Touch file to re-trigger diagnostics
      await page.keyboard.press("Control+S").catch(() => {});
    } else if (nudge % 4 === 2) {
      // Jump around the buffer so decorations re-layout
      await page.keyboard.press("Control+Home").catch(() => {});
      await page.waitForTimeout(200);
      await page.keyboard.press("Control+End").catch(() => {});
      await page.waitForTimeout(200);
      await page.keyboard.press("Control+Home").catch(() => {});
    } else if (nudge % 4 === 3) {
      // Focus editor again (extension updates on visible editors)
      const editor = page.locator(".monaco-editor").first();
      await editor.click({ timeout: 3000 }).catch(() => {});
    }

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

  // Scroll toward truly-pure helpers near the bottom of the fixture
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

  const meta = {
    capturedAt: new Date().toISOString(),
    codeServerUrl: BASE,
    file: FILE_PATH,
    badges: badgeState,
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
      inlayHints:
        "Parameter-name inlay hints should be on (FSharp.inlayHints.parameterNames).",
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
