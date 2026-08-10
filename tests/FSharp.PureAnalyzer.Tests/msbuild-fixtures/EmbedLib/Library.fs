namespace Fspure.Phase3.EmbedLib

module Api =

    /// Pure arithmetic — should appear in collected pure.json for public surface.
    let embedPureAdd (x: int) (y: int) : int = x + y

    /// Impure side effect — must not be classified pure by the collector.
    let embedImpureLog (msg: string) : unit =
        System.Console.WriteLine msg
