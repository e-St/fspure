// @ts-check
"use strict";
/**
 * Thin JS surface for the VS Code host.
 * SOURCE OF TRUTH: src/Fspure.DecorationLogic/Logic.fs (tested under
 * src/Fspure.DecorationLogic.Tests). Keep this file in sync with that F#.
 * Full Fable emission is available under editor/vscode-extension/fable/.
 */

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

/** @param {string} code */
function badgeForDefinitionCode(code) {
  if (code === "PURE002") return BADGE_IMPURE;
  if (code === "PURE003") return BADGE_PURE;
  return null;
}

module.exports = {
  IMPURE_CODES,
  PURE_CODES,
  BADGE_IMPURE,
  BADGE_PURE,
  diagnosticCode,
  isPureAnalyzerDiagnostic,
  badgesByLine,
  badgeForDefinitionCode,
};
