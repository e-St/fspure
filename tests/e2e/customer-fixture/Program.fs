// Customer-style fixture for purity end-to-end checks.
// Open this file with FSharp.PureAnalyzer + the decorations extension and confirm
// end-of-line PURE002 / PURE003 badges on the definitions below.
//
// Note: avoid bare badge tokens in comments so Phase 2 probes do not false-positive.

open System
open System.IO
open System.Collections.Generic

// ---------------------------------------------------------------------------
// Side-effecting helpers (I/O / mutation / randomness) — expect PURE002
// ---------------------------------------------------------------------------
let mutable globalAccumulator = 0
let globalLog = List<string>()
let randomGen = Random(42)

let logSideEffect (msg: string) =
    globalLog.Add(msg)
    printfn "[SIDE-EFFECT] %s" msg
    try
        File.AppendAllText("side_effects.log", msg + Environment.NewLine)
    with _ ->
        ()

let mutateGlobal (delta: int) =
    globalAccumulator <- globalAccumulator + delta

let getRandomImpure () =
    let r = randomGen.Next(0, 1000)
    logSideEffect (sprintf "Generated random %d" r)
    r

// <docs-snippet id="customer-pure-impure">
// ---------------------------------------------------------------------------
// Misnamed helpers that look clean but call side effects — expect PURE002
// ---------------------------------------------------------------------------
let pureAdd (a: int) (b: int) =
    logSideEffect (sprintf "pureAdd %d %d" a b)
    mutateGlobal (a + b)
    a + b

let pureMultiply (a: int) (b: int) =
    logSideEffect (sprintf "pureMultiply %d %d" a b)
    let r = a * b
    mutateGlobal r
    r

let pureSquare (n: int) = pureMultiply n n

let pureProcessBatch (values: int list) =
    logSideEffect (sprintf "pureProcessBatch %d" values.Length)
    let mutable sum = 0
    for x in values do
        sum <- pureAdd sum x
    pureMultiply sum (getRandomImpure () % 3 + 1)

// ---------------------------------------------------------------------------
// Referentially transparent helpers — expect PURE003
// (Phase 2 pure-section screenshots must include add / isEmpty / myEmpty.)
// ---------------------------------------------------------------------------
let add a b =
    List.map (fun x -> x * a + b) [1; 2; 3]

let isEmpty = List.isEmpty

let myEmpty l =
    add 1 2 |> isEmpty

let double x = x * 2

let purePipeline (x: int) =
    x |> double |> fun n -> add n 0 |> List.sum
// </docs-snippet>

// ---------------------------------------------------------------------------
// Entry
// ---------------------------------------------------------------------------
let main () =
    logSideEffect "========== E2E FIXTURE START =========="
    let sideEffectingResult = pureProcessBatch [ 3; 1; 4 ]
    let transparentResult = purePipeline 7
    printfn "sideEffectingResult=%d transparentResult=%d empty=%b" sideEffectingResult transparentResult (myEmpty [])
    logSideEffect "========== E2E FIXTURE END =========="

main ()
