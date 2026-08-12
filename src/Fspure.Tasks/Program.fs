module Fspure.Tasks.Program

open System
open System.IO
open Fspure.Tasks

let private usage () =
    """
fspure — monorepo task runner (F#, statically typed).

Usage:
  dotnet run --project src/Fspure.Tasks -- <command> [args…]

Commands:
  info
  build [args…]
  test [args…]
  docs [args…]              DocsGenerator (preview | stable | …)
  devcontainer [args…]
  security
  ready-lib-gate
  phase1                    Customer-fixture analyzer baseline e2e
  phase5                    Full phase-5 regression net
  analyze [args…]           Agent CLI (`src/fspure`, JSON/SARIF)
  assert-golden <path>      ReadyLib pure-methods golden check
  assert-nupkg <nupkg>      ReadyLib nupkg embed check
  help
"""

[<EntryPoint>]
let main argv =
    try
        let root = Repo.findRoot ()
        Directory.SetCurrentDirectory root

        match Array.tryHead argv with
        | None
        | Some ("help" | "--help" | "-h") ->
            printf "%s" (usage ())
            0

        | Some "info" ->
            printfn "fspure monorepo: %s" root
            printfn "  configuration: %s" (Repo.configuration ())
            let dv = (Repo.dotnetCapture root "--version").Stdout.Trim()
            printfn "  dotnet: %s" (if dv = "" then "?" else dv)
            0

        | Some "build" ->
            let rest = argv |> Array.skip 1 |> String.concat " "
            let cfg = Repo.configuration ()
            let args =
                if rest = "" then $"build fspure.slnx -c {cfg} --nologo"
                else $"build fspure.slnx -c {cfg} --nologo {rest}"
            Repo.dotnet root args

        | Some "test" ->
            let rest = argv |> Array.skip 1 |> String.concat " "
            let cfg = Repo.configuration ()
            let args =
                if rest = "" then $"test fspure.slnx -c {cfg} --nologo"
                else $"test fspure.slnx -c {cfg} --nologo {rest}"
            Repo.dotnet root args

        | Some "docs" ->
            Repo.runProject root "src/DocsGenerator/DocsGenerator.fsproj" (argv |> Array.skip 1 |> Array.toList)

        | Some "devcontainer" ->
            Repo.runProject root "src/DevcontainerGen/DevcontainerGen.fsproj" (argv |> Array.skip 1 |> Array.toList)

        | Some "security" -> Security.run root

        | Some ("ready-lib-gate" | "ready-lib" | "gate") -> ReadyLibGate.run root

        | Some "phase1" -> Phase1.run root

        | Some ("phase5" | "phase5-regression") -> Phase5.run root

        | Some "analyze" ->
            Repo.runProject root "src/fspure/fspure.fsproj" ("analyze" :: (argv |> Array.skip 1 |> Array.toList))

        | Some "assert-golden" ->
            match Array.tryItem 1 argv with
            | None ->
                eprintfn "usage: fspure assert-golden <pure.json|dll>"
                2
            | Some p -> Asserts.assertGoldenPureMethods root p

        | Some "assert-nupkg" ->
            match Array.tryItem 1 argv with
            | None ->
                eprintfn "usage: fspure assert-nupkg <nupkg>"
                2
            | Some p -> Asserts.assertNupkgEmbed root p

        | Some other ->
            eprintfn "Unknown command: %s" other
            eprintf "%s" (usage ())
            2
    with ex ->
        eprintfn "ERROR: %s" ex.Message

        if not (isNull ex.StackTrace) && Repo.envOr "FSPURE_DEBUG" "" = "1" then
            eprintfn "%s" ex.StackTrace

        1
