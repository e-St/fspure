// @ts-check
"use strict";

const vscode = require("vscode");

/** @type {vscode.TextEditorDecorationType | undefined} */
let badgeDecoration;
/** @type {vscode.TextEditorDecorationType | undefined} */
let boxDecoration;

const PURE_CODES = new Set(["PURE001", "PURE002"]);

/**
 * @param {vscode.Diagnostic} d
 */
function isPureDiagnostic(d) {
  const code =
    typeof d.code === "object" && d.code !== null
      ? String(/** @type {{ value?: unknown }} */ (d.code).value ?? "")
      : String(d.code ?? "");
  const source = String(d.source ?? "");
  return (
    PURE_CODES.has(code) ||
    source.includes("Pure analyzer") ||
    source.includes("FSharp.PureAnalyzer")
  );
}

/**
 * @param {vscode.Diagnostic} d
 */
function diagnosticCode(d) {
  return typeof d.code === "object" && d.code !== null
    ? String(/** @type {{ value?: unknown }} */ (d.code).value ?? "")
    : String(d.code ?? "");
}

function disposeDecorations() {
  badgeDecoration?.dispose();
  boxDecoration?.dispose();
  badgeDecoration = undefined;
  boxDecoration = undefined;
}

function createDecorations() {
  disposeDecorations();

  const cfg = vscode.workspace.getConfiguration("fsharpPureDecorations");
  const color = /** @type {string} */ (cfg.get("color", "#4FC1FF"));

  badgeDecoration = vscode.window.createTextEditorDecorationType({
    after: {
      contentText: " impure ",
      color: color,
      backgroundColor: "rgba(79, 193, 255, 0.12)",
      border: `1px solid ${color}`,
      margin: "0 0 0 1.2em",
      fontWeight: "600",
    },
  });

  boxDecoration = vscode.window.createTextEditorDecorationType({
    border: `1px solid ${color}`,
    borderRadius: "3px",
    backgroundColor: "rgba(79, 193, 255, 0.08)",
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
    if (badgeDecoration) editor.setDecorations(badgeDecoration, []);
    if (boxDecoration) editor.setDecorations(boxDecoration, []);
    return;
  }

  const style = /** @type {string} */ (cfg.get("style", "badge-end-of-line"));
  const diagnostics = vscode.languages
    .getDiagnostics(editor.document.uri)
    .filter(isPureDiagnostic);

  /** @type {vscode.DecorationOptions[]} */
  const badgeOpts = [];
  /** @type {vscode.DecorationOptions[]} */
  const boxOpts = [];

  for (const d of diagnostics) {
    const code = diagnosticCode(d);

    if (style === "box-name-only") {
      boxOpts.push({ range: d.range, hoverMessage: d.message });
      continue;
    }

    if (style === "badge-after-name") {
      badgeOpts.push({ range: d.range, hoverMessage: d.message });
      continue;
    }

    // badge-end-of-line (default): badge at end of signature line + box on name
    const line = editor.document.lineAt(d.range.start.line);
    const endOfLine = line.range.end;
    const endRange = new vscode.Range(endOfLine, endOfLine);

    if (code === "PURE002" || code === "") {
      badgeOpts.push({ range: endRange, hoverMessage: d.message });
    }

    boxOpts.push({ range: d.range, hoverMessage: d.message });
  }

  if (badgeDecoration) editor.setDecorations(badgeDecoration, badgeOpts);
  if (boxDecoration) editor.setDecorations(boxDecoration, boxOpts);
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
