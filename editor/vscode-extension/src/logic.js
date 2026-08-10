// @ts-check
"use strict";

/**
 * Pure decoration rules shared by the extension and unit tests.
 * Keep this free of the `vscode` module so CI can run it with plain Node.
 */

const IMPURE_CODES = new Set(["PURE001", "PURE002"]);
const PURE_CODES = new Set(["PURE003"]);

/** Badge text shown in the editor (end-of-line `after` content). */
const BADGE_IMPURE = "impure";
const BADGE_PURE = "pure";

/**
 * @param {unknown} code
 * @returns {string}
 */
function diagnosticCode(code) {
  if (typeof code === "object" && code !== null) {
    const value = /** @type {{ value?: unknown }} */ (code).value;
    return String(value ?? "");
  }
  return String(code ?? "");
}

/**
 * @param {{ code?: unknown, source?: unknown }} d
 */
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
 * Map definition diagnostics to per-line badges.
 * Only PURE002 / PURE003 produce badges (PURE001 is call-site only).
 * Impure wins over pure on the same line.
 *
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
    if (code === "PURE002") {
      entry.impure = code;
    } else if (code === "PURE003") {
      entry.pure = code;
    }
  }

  /** @type {Map<number, { badge: "impure" | "pure", code: string }>} */
  const result = new Map();
  for (const [line, entry] of byLine) {
    if (entry.impure) {
      result.set(line, { badge: BADGE_IMPURE, code: entry.impure });
    } else if (entry.pure) {
      result.set(line, { badge: BADGE_PURE, code: entry.pure });
    }
  }
  return result;
}

/**
 * @param {string} code  e.g. PURE002
 * @returns {"impure" | "pure" | null}
 */
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
