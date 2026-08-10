// @ts-check
"use strict";

/**
 * VS Code host for pure/impure decorations.
 * Badge rules must stay aligned with src/Fspure.DecorationLogic/Logic.fs
 * (tested in Fspure.DecorationLogic.Tests). This file is the only JS host surface.
 */

const vscode = require("vscode");

const IMPURE_CODES = new Set(["PURE001", "PURE002"]);
const PURE_CODES = new Set(["PURE003"]);
const BADGE_IMPURE = "impure";
const BADGE_PURE = "pure";

/** @param {unknown} code */
function diagnosticCode(code) {
  if (typeof code === "object" && code !== null) {
    const value = /** @type {{ value?: unknown }} */ (code).value;
    return String(value ?? "");
  }
  return String(code ?? "");
}

/** @param {{ code?: unknown, source?: unknown }} d */
function isPureAnalyzerDiagnostic(d) {
  const code = diagnosticCode(d.code);
  const source = String(d.source ?? "");
  return (
    IMPURE_CODES.has(code) ||
    PURE_CODES.has(code) ||
    source.includes("Pure analyzer") ||
    source.includes("FSharp.PureAnalyzer")
  );
}

/**
 * @param {Array<{ code?: unknown, source?: unknown, range?: { start?: { line?: number } } }>} diagnostics
 * @returns {Map<number, { badge: "impure" | "pure", code: string }>}
 */
function badgesByLine(diagnostics) {
  /** @type {Map<number, { impure?: string, pure?: string }>} */
  const byLine = new Map();
  for (const d of diagnostics) {
    if (!isPureAnalyzerDiagnostic(d)) continue;
    const code = diagnosticCode(d.code);
    const line = d.range?.start?.line;
    if (typeof line !== "number") continue;
    let entry = byLine.get(line);
    if (!entry) {
      entry = {};
      byLine.set(line, entry);
    }
    if (code === "PURE002") entry.impure = code;
    else if (code === "PURE003") entry.pure = code;
  }
  /** @type {Map<number, { badge: "impure" | "pure", code: string }>} */
  const result = new Map();
  for (const [line, entry] of byLine) {
    if (entry.impure) result.set(line, { badge: BADGE_IMPURE, code: entry.impure });
    else if (entry.pure) result.set(line, { badge: BADGE_PURE, code: entry.pure });
  }
  return result;
}

/**
 * Pure/impure badges as end-of-line decorations (`after` content).
 * Stacks to the right of Ionide LineLens when registered after Ionide boots.
 */

/** @type {vscode.TextEditorDecorationType | undefined} */
let impureBadge;
/** @type {vscode.TextEditorDecorationType | undefined} */
let pureBadge;

/**
 * @param {vscode.TextEditor} editor
 * @param {number} lineNumber
 */
function endOfLineAnchor(editor, lineNumber) {
  const end = editor.document.lineAt(lineNumber).range.end;
  return new vscode.Range(end, end);
}

/**
 * @param {string} contentText
 * @param {string} color
 */
function badgeDecorationOptions(contentText, color) {
  return {
    after: {
      contentText: `  ${contentText}`,
      color,
      fontWeight: "bold",
      fontStyle: "italic",
    },
    rangeBehavior: vscode.DecorationRangeBehavior.ClosedClosed,
  };
}

function disposeDecorations() {
  impureBadge?.dispose();
  pureBadge?.dispose();
  impureBadge = undefined;
  pureBadge = undefined;
}

function createDecorations() {
  disposeDecorations();

  const cfg = vscode.workspace.getConfiguration("fsharpPureDecorations");
  const impureColor = /** @type {string} */ (cfg.get("impureColor", "#E2A66A"));
  const pureColor = /** @type {string} */ (cfg.get("pureColor", "#6A9955"));

  impureBadge = vscode.window.createTextEditorDecorationType(
    badgeDecorationOptions("impure", impureColor)
  );
  pureBadge = vscode.window.createTextEditorDecorationType(
    badgeDecorationOptions("pure", pureColor)
  );
}

/**
 * @param {vscode.TextEditor} editor
 */
function updateEditor(editor) {
  if (!editor || editor.document.languageId !== "fsharp") {
    return;
  }

  const cfg = vscode.workspace.getConfiguration("fsharpPureDecorations");
  if (!cfg.get("enabled", true)) {
    if (impureBadge) editor.setDecorations(impureBadge, []);
    if (pureBadge) editor.setDecorations(pureBadge, []);
    return;
  }

  const diagnostics = vscode.languages
    .getDiagnostics(editor.document.uri)
    .filter(isPureAnalyzerDiagnostic);

  /** @type {Map<number, vscode.Diagnostic>} */
  const diagByLine = new Map();
  for (const d of diagnostics) {
    const code = diagnosticCode(d.code);
    const line = d.range.start.line;
    const existing = diagByLine.get(line);
    if (!existing) {
      diagByLine.set(line, d);
      continue;
    }
    if (code === "PURE002") {
      diagByLine.set(line, d);
    }
  }

  const badges = badgesByLine(diagnostics);

  /** @type {vscode.DecorationOptions[]} */
  const impureOpts = [];
  /** @type {vscode.DecorationOptions[]} */
  const pureOpts = [];

  for (const [line, entry] of badges) {
    const range = endOfLineAnchor(editor, line);
    const hoverMessage = diagByLine.get(line)?.message;
    const opt = hoverMessage ? { range, hoverMessage } : { range };
    if (entry.badge === "impure") {
      impureOpts.push(opt);
    } else {
      pureOpts.push(opt);
    }
  }

  if (impureBadge) editor.setDecorations(impureBadge, impureOpts);
  if (pureBadge) editor.setDecorations(pureBadge, pureOpts);
}

function updateAllEditors() {
  for (const editor of vscode.window.visibleTextEditors) {
    updateEditor(editor);
  }
}

/**
 * @param {vscode.ExtensionContext} context
 */
function activate(context) {
  const boot = () => {
    createDecorations();
    updateAllEditors();
  };
  boot();
  const t1 = setTimeout(boot, 2000);
  const t2 = setTimeout(boot, 5000);

  context.subscriptions.push(
    vscode.languages.onDidChangeDiagnostics(() => updateAllEditors()),
    vscode.window.onDidChangeActiveTextEditor((e) => {
      if (e) updateEditor(e);
    }),
    vscode.window.onDidChangeVisibleTextEditors(() => updateAllEditors()),
    vscode.workspace.onDidChangeConfiguration((e) => {
      if (e.affectsConfiguration("fsharpPureDecorations")) {
        createDecorations();
        updateAllEditors();
      }
    }),
    {
      dispose: () => {
        clearTimeout(t1);
        clearTimeout(t2);
        disposeDecorations();
      },
    }
  );
}

function deactivate() {
  disposeDecorations();
}

module.exports = { activate, deactivate };
