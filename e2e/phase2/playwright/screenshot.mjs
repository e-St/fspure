// @ts-check
/**
 * Phase 2 visual capture — mirrors the manual consumer codespace flow:
 *   1) Load customer-fixture.slnx into Ionide (workspace / solution)
 *   2) Open Program.fs
 *   3) Wait for Ionide type/parameter inlay hints (project ready)
 *   4) Wait for pure/impure badges (inlay hints after type annotations)
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
 * @param {import('playwright').Page} page
 * @param {string} query
 */
async function runCommand(page, query) {
  await page.keyboard.press("Control+Shift+P");
  await page.waitForTimeout(600);
  await page.keyboard.press("Control+A").catch(() => {});
  await page.keyboard.type(query, { delay: 25 });
  await page.waitForTimeout(900);
  await page.keyboard.press("Enter");
  await page.waitForTimeout(1200);
}

/**
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
 * @param {import('playwright').Page} page
 */
async function loadSolution(page) {
  log("loading Ionide workspace/solution:", SOLUTION_NAME);

  const commands = [
    "F#: Change Workspace or Solution",
    "F# Change Workspace or Solution",
    "Ionide: Change Workspace or Solution",
  ];

  for (const cmd of commands) {
    await dismissNoise(page);
    await runCommand(page, cmd);
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

  log("command palette workspace change unavailable; quick-opening", SOLUTION_NAME);
  await page.keyboard.press("Control+P");
  await page.waitForTimeout(600);
  await pickQuickOpen(page, SOLUTION_NAME);
  await page.waitForTimeout(3000);
}

/**
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
  await page.waitForTimeout(8000);
}

/**
 * Detect pure/impure badges and Ionide readiness.
 * Badges are InlayHints (same layer as type annotations), so they should appear
 * in editor text / inlay DOM. Also check CSS pseudo-elements for decoration fallback.
 * @param {import('playwright').Page} page
 */
async function probeUi(page) {
  const dom = await page.evaluate(() => {
    const editors = [...document.querySelectorAll(".monaco-editor")];
    const viewRoots = editors.flatMap((ed) => [
      ed.querySelector(".view-lines"),
      ed,
    ]).filter(Boolean);

    let editorText = "";
    let editorHtml = "";
    for (const root of viewRoots) {
      editorText += `\n${root.innerText || root.textContent || ""}`;
      editorHtml += `\n${root.innerHTML || ""}`;
    }

    const bodyText = document.body?.innerText || "";

    const word = (w, text) =>
      new RegExp(`(^|[^a-zA-Z])${w}([^a-zA-Z]|$)`, "m").test(text || "");

    // Pseudo-element content (decoration fallback path)
    let pseudoHitImpure = false;
    let pseudoHitPure = false;
    for (const root of editors) {
      const nodes = root.querySelectorAll("*");
      for (const n of nodes) {
        for (const pseudo of [":before", ":after"]) {
          const c = getComputedStyle(n, pseudo).content || "";
          // content is quoted, e.g. "impure" or "'impure'"
          if (/impure/i.test(c)) pseudoHitImpure = true;
          if (/(^|[^a-z])pure([^a-z]|$)/i.test(c.replace(/^["']|["']$/g, ""))) {
            if (!/impure/i.test(c)) pseudoHitPure = true;
          }
        }
      }
    }

    const sawImpure =
      word("impure", editorText) ||
      word("impure", editorHtml) ||
      word("impure", bodyText) ||
      pseudoHitImpure;

    const sawPure =
      word("pure", editorText) ||
      word("pure", editorHtml) ||
      pseudoHitPure;

    // Inlay widgets (Ionide types + our pure/impure badges)
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

    const typeAnnoHits = (
      editorText.match(/:\s*(int|string|bool|unit|list|float|decimal|obj|DateTime)\b/gi) ||
      []
    ).length;

    const statusBar =
      document.querySelector("#workbench\\.parts\\.statusbar")?.textContent ||
      document.querySelector(".statusbar")?.textContent ||
      "";

    return {
      sawImpure,
      sawPure,
      sawInlayish: inlayNodes.length > 0 || typeAnnoHits > 0,
      sawAnalyzerHint: /PURE00[123]|Pure analyzer/i.test(bodyText),
      solutionLoaded: /customer-fixture|Ionide|FSAC|F#/i.test(statusBar),
      inlayCount: inlayNodes.length,
      typeAnnoHits,
      statusBar: statusBar.replace(/\s+/g, " ").slice(0, 160),
      editorSnippet: editorText.replace(/\s+/g, " ").slice(0, 500),
    };
  });

  // Playwright text engine (sometimes sees accessible text evaluate misses)
  const impureLoc = page.locator(".monaco-editor").getByText("impure", {
    exact: true,
  });
  const pureLoc = page.locator(".monaco-editor").getByText("pure", {
    exact: true,
  });
  const impureCount = await impureLoc.count().catch(() => 0);
  const pureCount = await pureLoc.count().catch(() => 0);

  // Also non-exact contains (inlay may be in a larger string with spaces)
  const impureSoft = await page
    .locator(".monaco-editor")
    .getByText(/\bimpure\b/)
    .count()
    .catch(() => 0);
  const pureSoft = await page
    .locator(".monaco-editor")
    .getByText(/(?<![a-zA-Z])pure(?![a-zA-Z])/)
    .count()
    .catch(() => 0);

  return {
    ...dom,
    sawImpure: dom.sawImpure || impureCount > 0 || impureSoft > 0,
    sawPure: dom.sawPure || pureCount > 0 || pureSoft > 0,
    locatorCounts: { impureCount, pureCount, impureSoft, pureSoft },
  };
}

/**
 * @param {import('playwright').Page} page
 * @param {number} nudge
 */
async function nudgeIonide(page, nudge) {
  // Only re-assert solution once if still cold; avoid resetting a healthy session
  if (nudge === 8) {
    const probe = await probeUi(page);
    if (!probe.sawInlayish && !probe.sawImpure) {
      log("nudge: re-assert Ionide workspace →", SOLUTION_NAME);
      await loadSolution(page);
      await openProgramFs(page);
    }
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
    await page.keyboard.press("Control+Shift+M").catch(() => {});
    await page.waitForTimeout(400);
  } else if (nudge % 5 === 4) {
    const editor = page.locator(".monaco-editor").first();
    await editor.click({ timeout: 3000 }).catch(() => {});
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
        locatorCounts: probe.locatorCounts,
        statusBar: probe.statusBar,
        editorSnippet: probe.editorSnippet,
        remainingMs: deadline - Date.now(),
      });
      lastLog = Date.now();
    }

    if (sawImpure && sawPure) {
      log("found both pure and impure labels", { sawInlayish });
      await page.waitForTimeout(2000);
      return { sawImpure, sawPure, sawInlayish, timedOut: false };
    }

    // Once impure is visible, jump to pure helpers so PURE003 badges mount
    if (sawImpure && !sawPure && nudge > 2 && nudge % 3 === 0) {
      await findInEditor(page, "let add a b");
    }

    nudge += 1;
    await nudgeIonide(page, nudge);
    await page.waitForTimeout(3000);
  }

  log("timeout waiting for badges", { sawImpure, sawPure, sawInlayish, WAIT_MS });
  return { sawImpure, sawPure, sawInlayish, timedOut: true };
}

/**
 * Find text in the editor via the find widget (Ctrl+F).
 * @param {import('playwright').Page} page
 * @param {string} query
 */
async function findInEditor(page, query) {
  await page.keyboard.press("Escape").catch(() => {});
  await page.waitForTimeout(200);
  await page.keyboard.press("Control+F").catch(() => {});
  await page.waitForTimeout(500);
  await page.keyboard.press("Control+A").catch(() => {});
  await page.keyboard.type(query, { delay: 25 });
  await page.waitForTimeout(700);
  await page.keyboard.press("Enter").catch(() => {});
  await page.waitForTimeout(600);
  await page.keyboard.press("Escape").catch(() => {});
  await page.waitForTimeout(400);
}

/**
 * Reveal the referentially-transparent helpers so screenshots include:
 *   let add / let isEmpty / let myEmpty
 * @param {import('playwright').Page} page
 */
async function revealPureHelpersSection(page) {
  log("revealing pure helpers section (add / isEmpty / myEmpty)");

  // Anchor on the first pure binding — keeps isEmpty + myEmpty in the same viewport
  await findInEditor(page, "let add a b");

  // Nudge up slightly so the section header / full `add` body is visible
  for (let i = 0; i < 3; i++) {
    await page.keyboard.press("ArrowUp").catch(() => {});
  }
  await page.waitForTimeout(400);

  const visible = await page.evaluate(() => {
    const text =
      document.querySelector(".monaco-editor .view-lines")?.innerText ||
      document.querySelector(".monaco-editor")?.innerText ||
      "";
    return {
      hasAdd: /let\s+add\s+a\s+b/.test(text),
      hasIsEmpty: /let\s+isEmpty\b/.test(text),
      hasMyEmpty: /let\s+myEmpty\b/.test(text),
      snippet: text.replace(/\s+/g, " ").slice(0, 400),
    };
  });

  // If myEmpty is still below the fold, page down once from add
  if (visible.hasAdd && !visible.hasMyEmpty) {
    await page.keyboard.press("PageDown").catch(() => {});
    await page.waitForTimeout(300);
    // Re-center on add so all three stay together in a tall viewport
    await findInEditor(page, "let add a b");
    for (let i = 0; i < 2; i++) {
      await page.keyboard.press("ArrowUp").catch(() => {});
    }
    await page.waitForTimeout(400);
  }

  const again = await page.evaluate(() => {
    const text =
      document.querySelector(".monaco-editor .view-lines")?.innerText ||
      document.querySelector(".monaco-editor")?.innerText ||
      "";
    return {
      hasAdd: /let\s+add\s+a\s+b/.test(text),
      hasIsEmpty: /let\s+isEmpty\b/.test(text),
      hasMyEmpty: /let\s+myEmpty\b/.test(text),
      snippet: text.replace(/\s+/g, " ").slice(0, 500),
    };
  });

  log("pure helpers visibility", again);
  if (!again.hasAdd || !again.hasIsEmpty || !again.hasMyEmpty) {
    log(
      "warning: pure helpers not fully visible in editor viewport; screenshot may be incomplete"
    );
  }
  return again;
}

/**
 * @param {import('playwright').Page} page
 * @param {{ sawImpure: boolean, sawPure: boolean, sawInlayish?: boolean, timedOut: boolean }} badgeState
 */
async function capture(page, badgeState) {
  const editor = page.locator(".monaco-editor").first();
  await editor.waitFor({ state: "visible" });

  // --- Impure / top-of-file section ---
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

  // --- Pure helpers: add / isEmpty / myEmpty (must appear in pure-section PNG) ---
  const pureVisibility = await revealPureHelpersSection(page);
  await page.waitForTimeout(800);

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
    pureHelpersVisible: pureVisibility,
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
      pureSectionMustInclude: [
        "let add a b",
        "let isEmpty",
        "let myEmpty",
      ],
      pureShouldInclude: ["add", "isEmpty", "myEmpty", "double", "purePipeline"],
      flow: "slnx → Program.fs → LineLens (// HM type) → pure/impure badge after signature",
      badgeOrder:
        "Expected: let add a b = // int -> int -> list<int> pure  (no a : int on args; pure after LineLens).",
      consumerIonideSettings: {
        "FSharp.inlayHints.typeAnnotations": false,
        "FSharp.inlayHints.parameterNames": true,
        "FSharp.lineLens.enabled": "replaceCodeLens",
        "FSharp.lineLens.prefix": "// ",
      },
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
          "note: pure/impure labels found, but Ionide inlay widgets were not clearly detected"
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
