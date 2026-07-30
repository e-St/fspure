// @ts-check
/**
 * Phase 2 visual capture:
 *   open customer-fixture/Program.fs in code-server (VS Code Web),
 *   wait for pure/impure decoration labels, take screenshots.
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
async function openProgramFs(page) {
  await page.goto(BASE, { waitUntil: "domcontentloaded", timeout: 60_000 });

  // Dismiss residual modals / trust / welcome
  for (let i = 0; i < 3; i++) {
    await page.keyboard.press("Escape").catch(() => {});
    await page.waitForTimeout(400);
  }

  // Quick Open → Program.fs
  await page.keyboard.press("Control+P");
  await page.waitForTimeout(600);
  await page.keyboard.type(FILE_PATH, { delay: 35 });
  await page.waitForTimeout(900);
  await page.keyboard.press("Enter");
  await page.waitForTimeout(2000);

  const editor = page.locator(".monaco-editor").first();
  await editor.waitFor({ state: "visible", timeout: 90_000 });
  await editor.click({ timeout: 15_000 }).catch(() => {});
  log("editor visible for", FILE_PATH);
}

/**
 * Probe page for decoration label text.
 * @param {import('playwright').Page} page
 */
async function probeBadges(page) {
  const bodyText = await page.locator("body").innerText().catch(() => "");
  const html = await page.content().catch(() => "");
  const combined = `${bodyText}\n${html}`;

  // Word-boundary style checks; decorations usually inject literal "pure"/"impure"
  const sawImpure = /(^|[^a-zA-Z])impure([^a-zA-Z]|$)/m.test(combined);
  // Avoid matching the English word inside "impure"
  const sawPure =
    /(^|[^a-zA-Z])pure([^a-zA-Z]|$)/m.test(bodyText) ||
    /(?<!im)pure(?![a-zA-Z])/i.test(html);

  return { sawImpure, sawPure };
}

/**
 * @param {import('playwright').Page} page
 */
async function waitForBadges(page) {
  const deadline = Date.now() + WAIT_MS;
  let sawImpure = false;
  let sawPure = false;
  let lastLog = 0;

  while (Date.now() < deadline) {
    const probe = await probeBadges(page);
    sawImpure = sawImpure || probe.sawImpure;
    sawPure = sawPure || probe.sawPure;

    if (Date.now() - lastLog > 15_000) {
      log("still waiting…", { sawImpure, sawPure, remainingMs: deadline - Date.now() });
      lastLog = Date.now();
    }

    if (sawImpure && sawPure) {
      log("found both pure and impure labels");
      return { sawImpure, sawPure, timedOut: false };
    }

    // Nudge language service / analyzers
    await page.keyboard.press("Control+S").catch(() => {});
    await page.waitForTimeout(3000);
  }

  log("timeout waiting for badges", { sawImpure, sawPure, WAIT_MS });
  return { sawImpure, sawPure, timedOut: true };
}

/**
 * @param {import('playwright').Page} page
 * @param {{ sawImpure: boolean, sawPure: boolean, timedOut: boolean }} badgeState
 */
async function capture(page, badgeState) {
  const editor = page.locator(".monaco-editor").first();
  await editor.waitFor({ state: "visible" });

  await page.keyboard.press("Control+Home").catch(() => {});
  await page.waitForTimeout(500);

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
  await page.waitForTimeout(300);
  for (let i = 0; i < 10; i++) {
    await page.keyboard.press("PageUp").catch(() => {});
  }
  await page.waitForTimeout(400);
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
