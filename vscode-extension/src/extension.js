// @ts-check
"use strict";

const vscode = require("vscode");

/** @type {vscode.TextEditorDecorationType | undefined} */
let impureBadge;
/** @type {vscode.TextEditorDecorationType | undefined} */
let pureBadge;

const IMPURE_CODES = new Set(["PURE001", "PURE002"]);
const PURE_CODES = new Set(["PURE003"]);

/**
 * @param {vscode.Diagnostic} d
 */
function diagnosticCode(d) {
  return typeof d.code === "object" && d.code !== null
    ? String(/** @type {{ value?: unknown }} */ (d.code).value ?? "")
    : String(d.code ?? "");
}

/**
 * @param {vscode.Diagnostic} d
 */
function isPureAnalyzerDiagnostic(d) {
  const code = diagnosticCode(d);
  const source = String(d.source ?? "");
  return (
    IMPURE_CODES.has(code) ||
    PURE_CODES.has(code) ||
    source.includes("Pure analyzer") ||
    source.includes("FSharp.PureAnalyzer")
  );
}

/**
 * Anchor for `after` text: last non-empty character of the line.
 * Zero-width end-of-line ranges often do not render `after` content in VS Code.
 * @param {vscode.TextEditor} editor
 * @param {number} lineNumber
 */
function endOfLineAnchor(editor, lineNumber) {
  const line = editor.document.lineAt(lineNumber);
  const end = line.range.end;
  if (end.character === 0) {
    return new vscode.Range(end, end);
  }
  const start = end.translate(0, -1);
  return new vscode.Range(start, end);
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

  impureBadge = vscode.window.createTextEditorDecorationType({
    after: {
      contentText: "impure",
      color: impureColor,
      margin: "0 0 0 1.5em",
      fontWeight: "bold",
      fontStyle: "italic",
    },
  });

  pureBadge = vscode.window.createTextEditorDecorationType({
    after: {
      contentText: "pure",
      color: pureColor,
      margin: "0 0 0 1.5em",
      fontWeight: "bold",
      fontStyle: "italic",
    },
  });
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

  // Per line: impure wins over pure (never show "impure pure").
  /** @type {Map<number, { impure?: vscode.Diagnostic, pure?: vscode.Diagnostic }>} */
  const byLine = new Map();

  for (const d of diagnostics) {
    const code = diagnosticCode(d);
    const line = d.range.start.line;
    let entry = byLine.get(line);
    if (!entry) {
      entry = {};
      byLine.set(line, entry);
    }
    if (code === "PURE002") {
      entry.impure = d;
    } else if (code === "PURE003") {
      entry.pure = d;
    }
  }

  /** @type {vscode.DecorationOptions[]} */
  const impureOpts = [];
  /** @type {vscode.DecorationOptions[]} */
  const pureOpts = [];

  for (const [line, entry] of byLine) {
    const range = endOfLineAnchor(editor, line);
    if (entry.impure) {
      impureOpts.push({
        range,
        hoverMessage: entry.impure.message,
      });
    } else if (entry.pure) {
      pureOpts.push({
        range,
        hoverMessage: entry.pure.message,
      });
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
  createDecorations();

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
    { dispose: () => disposeDecorations() }
  );

  updateAllEditors();
}

function deactivate() {
  disposeDecorations();
}

module.exports = { activate, deactivate };
