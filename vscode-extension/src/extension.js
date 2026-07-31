// @ts-check
"use strict";

const vscode = require("vscode");
const {
  diagnosticCode,
  isPureAnalyzerDiagnostic,
  badgesByLine,
} = require("./logic");

/**
 * Pure/impure badges as end-of-line decorations (`after` content).
 *
 * Why decorations (not InlayHints)?
 * - Ionide's Hindley–Milner signature is **LineLens** (`// int -> int -> list<int>`),
 *   also an `after` decoration on the definition line.
 * - InlayHints paint *before* LineLens at EOL, which produced:
 *     `let add a b = pure // int -> int -> list<int>`
 * - A second decoration type registered after Ionide stacks to the *right* of LineLens:
 *     `let add a b = // int -> int -> list<int> pure`
 *
 * Pair with recommended consumer Ionide settings:
 *   FSharp.inlayHints.typeAnnotations = false  (no `a : int` on args)
 *   FSharp.lineLens.enabled = replaceCodeLens     (HM signature via // …)
 */

/** @type {vscode.TextEditorDecorationType | undefined} */
let impureBadge;
/** @type {vscode.TextEditorDecorationType | undefined} */
let pureBadge;

/**
 * Zero-width range at absolute end of line (after source text / LineLens anchor).
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
      // Two ASCII spaces after LineLens: `// unit -> 'a -> unit  impure`
      // (nbsp-only was easy to miss; margin alone does not put spaces in the text run).
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
  // Create decoration types after a short delay so Ionide's LineLens
  // decoration types are registered first. Later decoration types stack to the
  // *right* of earlier ones at the same EOL column:
  //   let add a b = // int -> int -> list<int> pure
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
