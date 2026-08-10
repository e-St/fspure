namespace Fspure.Phase2.PureLib

/// Library surface used by Phase 2 analyser integration tests.
module Api =

    /// Deliberately pure arithmetic; claimed pure only via embedded pure.json
    /// (not in foundational set under this name).
    let libraryPureAdd (x: int) (y: int) : int = x + y

    /// Deliberately impure (side effect); must NOT appear in pure.json.
    let libraryImpureLog (msg: string) : unit =
        System.Console.WriteLine msg
