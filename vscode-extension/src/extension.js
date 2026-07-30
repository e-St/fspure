// @ts-check
"use strict";

const vscode = require("vscode");
const {
  diagnosticCode,
  isPureAnalyzerDiagnostic,
  badgesByLine,
} = require("./logic");

/** @type {vscode.TextEditorDecorationType | undefined} */
let impureBadge;
/** @type {vscode.TextEditorDecorationType | undefined} */
let pureBadge;

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

  // Preserve hover messages from the winning diagnostic per line.
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
    // impure (PURE002) wins over pure (PURE003)
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
