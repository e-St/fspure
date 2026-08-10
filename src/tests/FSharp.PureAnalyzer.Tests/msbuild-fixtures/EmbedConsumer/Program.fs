module EmbedConsumer

open Fspure.Phase3.EmbedLib

/// Pure only when EmbedLib's pure.json (from MSBuild embed) is loaded.
let useEmbedPure (x: int) = Api.embedPureAdd x 1

let useEmbedImpure () = Api.embedImpureLog "x"

let useFoundational (xs: int list) = List.map (fun n -> n + 1) xs

[<EntryPoint>]
let main _ =
    ignore (useEmbedPure 2)
    useEmbedImpure ()
    ignore (useFoundational [ 1 ])
    0
