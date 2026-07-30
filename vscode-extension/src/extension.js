// @ts-check
"use strict";

const vscode = require("vscode");
const {
  diagnosticCode,
  isPureAnalyzerDiagnostic,
  badgesByLine,
} = require("./logic");

/**
 * Preferred rendering: InlayHints at end-of-line.
 * They share Ionide's inlay layer, so badges appear *after* type/parameter hints.
 *
 * Fallback: TextEditorDecorationType `after` when editor inlay hints are off
 * (decorations always paint before inlays at the same column, so they are only
 * used when there are no type inlays to order against).
 */

/** @type {vscode.EventEmitter<void>} */
const onDidChangeInlayHintsEmitter = new vscode.EventEmitter();

/** @type {vscode.TextEditorDecorationType | undefined} */
let impureBadgeDecoration;
/** @type {vscode.TextEditorDecorationType | undefined} */
let pureBadgeDecoration;

/**
 * @returns {boolean}
 */
function inlayHintsEnabledInEditor() {
  const v = vscode.workspace
    .getConfiguration("editor")
    .get("inlayHints.enabled");
  // "on" | "off" | "onUnlessPressed" | "offUnlessPressed" | boolean (legacy)
  if (v === false || v === "off") {
    return false;
  }
  return true;
}

/**
 * @param {vscode.TextDocument} document
 * @returns {Map<number, { badge: "impure" | "pure", code: string, message?: string }>}
 */
function badgeEntriesForDocument(document) {
  /** @type {Map<number, { badge: "impure" | "pure", code: string, message?: string }>} */
  const result = new Map();
  if (document.languageId !== "fsharp") {
    return result;
  }

  const cfg = vscode.workspace.getConfiguration("fsharpPureDecorations");
  if (!cfg.get("enabled", true)) {
    return result;
  }

  const diagnostics = vscode.languages
    .getDiagnostics(document.uri)
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

  for (const [line, entry] of badgesByLine(diagnostics)) {
    const diag = diagByLine.get(line);
    result.set(line, {
      badge: entry.badge,
      code: entry.code,
      message: diag?.message,
    });
  }
  return result;
}

/**
 * @param {string} contentText
 * @param {string} color
 */
function decorationTypeOptions(contentText, color) {
  return {
    after: {
      contentText: `\u00A0\u00A0${contentText}`,
      color,
      margin: "0 0 0 1.5em",
      fontWeight: "bold",
      fontStyle: "italic",
    },
    rangeBehavior: vscode.DecorationRangeBehavior.ClosedClosed,
  };
}

function disposeDecorationTypes() {
  impureBadgeDecoration?.dispose();
  pureBadgeDecoration?.dispose();
  impureBadgeDecoration = undefined;
  pureBadgeDecoration = undefined;
}

function createDecorationTypes() {
  disposeDecorationTypes();
  const cfg = vscode.workspace.getConfiguration("fsharpPureDecorations");
  const impureColor = /** @type {string} */ (cfg.get("impureColor", "#E2A66A"));
  const pureColor = /** @type {string} */ (cfg.get("pureColor", "#6A9955"));
  impureBadgeDecoration = vscode.window.createTextEditorDecorationType(
    decorationTypeOptions("impure", impureColor)
  );
  pureBadgeDecoration = vscode.window.createTextEditorDecorationType(
    decorationTypeOptions("pure", pureColor)
  );
}

/**
 * Clear decoration-based badges (used when inlay path is active).
 * @param {vscode.TextEditor} editor
 */
function clearDecorationBadges(editor) {
  if (impureBadgeDecoration) {
    editor.setDecorations(impureBadgeDecoration, []);
  }
  if (pureBadgeDecoration) {
    editor.setDecorations(pureBadgeDecoration, []);
  }
}

/**
 * Fallback path when editor.inlayHints is off.
 * @param {vscode.TextEditor} editor
 */
function updateDecorationBadges(editor) {
  if (!editor || editor.document.languageId !== "fsharp") {
    return;
  }
  if (!impureBadgeDecoration || !pureBadgeDecoration) {
    createDecorationTypes();
  }

  const entries = badgeEntriesForDocument(editor.document);
  /** @type {vscode.DecorationOptions[]} */
  const impureOpts = [];
  /** @type {vscode.DecorationOptions[]} */
  const pureOpts = [];

  for (const [line, entry] of entries) {
    const end = editor.document.lineAt(line).range.end;
    const range = new vscode.Range(end, end);
    const opt = entry.message
      ? { range, hoverMessage: entry.message }
      : { range };
    if (entry.badge === "impure") {
      impureOpts.push(opt);
    } else {
      pureOpts.push(opt);
    }
  }

  if (impureBadgeDecoration) {
    editor.setDecorations(impureBadgeDecoration, impureOpts);
  }
  if (pureBadgeDecoration) {
    editor.setDecorations(pureBadgeDecoration, pureOpts);
  }
}

/**
 * @param {vscode.TextDocument} document
 * @param {vscode.Range} visibleRange
 * @returns {vscode.InlayHint[]}
 */
function buildInlayHints(document, visibleRange) {
  // When inlays are disabled, the decoration path owns the UI.
  if (!inlayHintsEnabledInEditor()) {
    return [];
  }

  const entries = badgeEntriesForDocument(document);
  if (entries.size === 0) {
    return [];
  }

  /** @type {vscode.InlayHint[]} */
  const hints = [];
  const startLine = visibleRange.start.line;
  const endLine = Math.min(visibleRange.end.line, document.lineCount - 1);

  for (const [line, entry] of entries) {
    if (line < startLine || line > endLine) {
      continue;
    }
    if (line < 0 || line >= document.lineCount) {
      continue;
    }

    const textLine = document.lineAt(line);
    // End of line: after Ionide type/parameter inlays on this binding line.
    const position = textLine.range.end;
    // Leading spaces separate the purity badge from the preceding type hint.
    const label = `  ${entry.badge}`;
    const hint = new vscode.InlayHint(position, label, vscode.InlayHintKind.Type);
    hint.paddingLeft = true;
    hint.tooltip = entry.message || `${entry.badge} (${entry.code})`;
    hints.push(hint);
  }

  hints.sort((a, b) =>
    a.position.line !== b.position.line
      ? a.position.line - b.position.line
      : a.position.character - b.position.character
  );

  return hints;
}

const inlayHintsProvider = {
  onDidChangeInlayHints: onDidChangeInlayHintsEmitter.event,
  /**
   * @param {vscode.TextDocument} document
   * @param {vscode.Range} range
   * @param {vscode.CancellationToken} _token
   */
  provideInlayHints(document, range, _token) {
    return buildInlayHints(document, range);
  },
};

function refreshAll() {
  onDidChangeInlayHintsEmitter.fire();

  const useInlays = inlayHintsEnabledInEditor();
  for (const editor of vscode.window.visibleTextEditors) {
    if (editor.document.languageId !== "fsharp") {
      continue;
    }
    if (useInlays) {
      clearDecorationBadges(editor);
    } else {
      updateDecorationBadges(editor);
    }
  }
}

/**
 * @param {vscode.ExtensionContext} context
 */
function activate(context) {
  createDecorationTypes();

  context.subscriptions.push(
    vscode.languages.registerInlayHintsProvider(
      { language: "fsharp" },
      inlayHintsProvider
    ),
    vscode.languages.onDidChangeDiagnostics(() => refreshAll()),
    vscode.window.onDidChangeActiveTextEditor(() => refreshAll()),
    vscode.window.onDidChangeVisibleTextEditors(() => refreshAll()),
    vscode.workspace.onDidChangeConfiguration((e) => {
      if (
        e.affectsConfiguration("fsharpPureDecorations") ||
        e.affectsConfiguration("editor.inlayHints")
      ) {
        createDecorationTypes();
        refreshAll();
      }
    }),
    onDidChangeInlayHintsEmitter,
    { dispose: () => disposeDecorationTypes() }
  );

  refreshAll();
}

function deactivate() {
  disposeDecorationTypes();
}

module.exports = { activate, deactivate };
