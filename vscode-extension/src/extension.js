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

function disposeDecorations() {
  impureBadge?.dispose();
  pureBadge?.dispose();
  impureBadge = undefined;
  pureBadge = undefined;
}

function createDecorations() {
  disposeDecorations();

  const cfg = vscode.workspace.getConfiguration("fsharpPureDecorations");
  const impureColor = /** @type {string} */ (cfg.get("impureColor", "#E2A66A")); // orange
  const pureColor = /** @type {string} */ (cfg.get("pureColor", "#6A9955")); // green

  // No leading/trailing spaces inside the badge text.
  impureBadge = vscode.window.createTextEditorDecorationType({
    after: {
      contentText: "impure",
      color: impureColor,
      backgroundColor: "rgba(226, 166, 106, 0.15)",
      border: `1px solid ${impureColor}`,
      margin: "0 0 0 1.2em",
      fontWeight: "600",
    },
  });

  pureBadge = vscode.window.createTextEditorDecorationType({
    after: {
      contentText: "pure",
      color: pureColor,
      backgroundColor: "rgba(106, 153, 85, 0.15)",
      border: `1px solid ${pureColor}`,
      margin: "0 0 0 1.2em",
      fontWeight: "600",
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
    // Badge only at end of the signature line — never decorate the function name.
    const line = editor.document.lineAt(d.range.start.line);
    const endOfLine = line.range.end;
    const endRange = new vscode.Range(endOfLine, endOfLine);
    const opt = { range: endRange, hoverMessage: d.message };

    if (PURE_CODES.has(code)) {
      pureOpts.push(opt);
    } else if (code === "PURE002" || code === "PURE001") {
      // Definitions (and optionally call sites) marked impure
      if (code === "PURE002") {
        impureOpts.push(opt);
      }
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
