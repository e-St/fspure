// @ts-check
"use strict";

/**
 * Node unit tests for pure/impure badge mapping.
 * Run: node vscode-extension/test/decorations.logic.test.js
 */

const assert = require("assert");
const path = require("path");
const {
  diagnosticCode,
  isPureAnalyzerDiagnostic,
  badgesByLine,
  badgeForDefinitionCode,
  BADGE_IMPURE,
  BADGE_PURE,
} = require(path.join(__dirname, "..", "src", "logic.js"));

function d(code, line, source = "Pure analyzer") {
  return {
    code,
    source,
    range: { start: { line }, end: { line } },
    message: `test ${code}`,
  };
}

function run() {
  // diagnosticCode
  assert.strictEqual(diagnosticCode("PURE002"), "PURE002");
  assert.strictEqual(diagnosticCode({ value: "PURE003" }), "PURE003");
  assert.strictEqual(diagnosticCode(null), "");

  // recognition
  assert.ok(isPureAnalyzerDiagnostic(d("PURE001", 0)));
  assert.ok(isPureAnalyzerDiagnostic(d("PURE002", 0)));
  assert.ok(isPureAnalyzerDiagnostic(d("PURE003", 0)));
  assert.ok(
    isPureAnalyzerDiagnostic({
      code: "OTHER",
      source: "FSharp.PureAnalyzer",
      range: { start: { line: 0 } },
    })
  );
  assert.ok(
    !isPureAnalyzerDiagnostic({
      code: "FS0039",
      source: "F# Compiler",
      range: { start: { line: 0 } },
    })
  );

  // definition badge contract used by customer e2e
  assert.strictEqual(badgeForDefinitionCode("PURE002"), BADGE_IMPURE);
  assert.strictEqual(badgeForDefinitionCode("PURE003"), BADGE_PURE);
  assert.strictEqual(badgeForDefinitionCode("PURE001"), null);

  // line aggregation: PURE001 alone → no badge (call site)
  {
    const map = badgesByLine([d("PURE001", 5)]);
    assert.strictEqual(map.size, 0, "PURE001 must not paint pure/impure badges");
  }

  // pure definition
  {
    const map = badgesByLine([d("PURE003", 10)]);
    assert.strictEqual(map.get(10)?.badge, "pure");
  }

  // impure definition
  {
    const map = badgesByLine([d("PURE002", 11)]);
    assert.strictEqual(map.get(11)?.badge, "impure");
  }

  // impure wins over pure on the same line
  {
    const map = badgesByLine([d("PURE003", 12), d("PURE002", 12)]);
    assert.strictEqual(map.get(12)?.badge, "impure");
  }

  // multiple lines like inaction Program.fs
  {
    const map = badgesByLine([
      d("PURE002", 55), // pureAdd misnamed
      d("PURE003", 449), // add
      d("PURE003", 452), // isEmpty
      d("PURE003", 454), // myEmpty
      d("PURE002", 505), // main
      d("PURE001", 560), // call site — ignored for badges
    ]);
    assert.strictEqual(map.get(55)?.badge, "impure");
    assert.strictEqual(map.get(449)?.badge, "pure");
    assert.strictEqual(map.get(452)?.badge, "pure");
    assert.strictEqual(map.get(454)?.badge, "pure");
    assert.strictEqual(map.get(505)?.badge, "impure");
    assert.ok(!map.has(560));
  }

  console.log("✅ decorations.logic.test.js passed");
}

run();
