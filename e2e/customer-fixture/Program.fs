// Customer-style fixture for pure/impure end-to-end checks.
// Mirrors the manual codespace validation done against skinow/inaction/Program.fs:
// open this file with FSharp.PureAnalyzer + fsharp-pure-decorations and confirm
// end-of-line "pure" / "impure" badges on the definitions below.

open System
open System.IO
open System.Collections.Generic

// ---------------------------------------------------------------------------
// Impure helpers (side effects / mutation / randomness)
// ---------------------------------------------------------------------------
let mutable globalAccumulator = 0
let globalLog = List<string>()
let randomGen = Random(42)

let logSideEffect (msg: string) =
    globalLog.Add(msg)
    printfn "[SIDE-EFFECT] %s" msg
    try
        File.AppendAllText("impure_side_effects.log", msg + Environment.NewLine)
    with _ ->
        ()

let mutateGlobal (delta: int) =
    globalAccumulator <- globalAccumulator + delta

let getRandomImpure () =
    let r = randomGen.Next(0, 1000)
    logSideEffect (sprintf "Generated random %d" r)
    r

// ---------------------------------------------------------------------------
// Misnamed "pure*" functions that are actually impure (should be PURE002)
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
// Truly pure functions (should be PURE003 — green "pure" badge)
// ---------------------------------------------------------------------------
let add a b =
    List.map (fun x -> x * a + b) [ 1; 2; 3 ]

let isEmpty = List.isEmpty

let myEmpty l =
    add 1 2 |> isEmpty

let double x = x * 2

let purePipeline (x: int) =
    x |> double |> fun n -> add n 0 |> List.sum

// ---------------------------------------------------------------------------
// Entry
// ---------------------------------------------------------------------------
let main () =
    logSideEffect "========== E2E FIXTURE START =========="
    let impureResult = pureProcessBatch [ 3; 1; 4 ]
    let pureResult = purePipeline 7
    printfn "impureResult=%d pureResult=%d empty=%b" impureResult pureResult (myEmpty [])
    logSideEffect "========== E2E FIXTURE END =========="

main ()
