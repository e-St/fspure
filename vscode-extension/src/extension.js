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
 * @param {vscode.Diagnostic} d
 */
function endOfLineAnchor(editor, d) {
  const line = editor.document.lineAt(d.range.start.line);
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

  // No border/box — bold-italic colored label only
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

  /** @type {vscode.DecorationOptions[]} */
  const impureOpts = [];
  /** @type {vscode.DecorationOptions[]} */
  const pureOpts = [];

  for (const d of diagnostics) {
    const code = diagnosticCode(d);
    const range = endOfLineAnchor(editor, d);
    const opt = { range, hoverMessage: d.message };

    if (PURE_CODES.has(code)) {
      pureOpts.push(opt);
    } else if (code === "PURE002") {
      impureOpts.push(opt);
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
