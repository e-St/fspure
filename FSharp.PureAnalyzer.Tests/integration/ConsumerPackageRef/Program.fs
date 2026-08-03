module ConsumerPackageRef

open Fspure.Phase2.PureLib

/// Pure only if libraryPureAdd is known pure (library-embedded pure.json from nupkg).
let useLibraryPure (x: int) = Api.libraryPureAdd x 1

let useLibraryImpure () = Api.libraryImpureLog "hi"

let useFoundational (xs: int list) = List.map (fun x -> x + 1) xs

[<EntryPoint>]
let main _ =
    ignore (useLibraryPure 1)
    useLibraryImpure ()
    ignore (useFoundational [ 1; 2 ])
    0
