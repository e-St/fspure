module Fspure.Tasks.Program

open System
open System.IO
open Fspure.Tasks

let private usage () =
    """
fspure — monorepo task runner (F#, statically typed).

Replaces bash under src/scripts/ for build, test, docs, security, and gates.

Usage:
  dotnet run --project src/Fspure.Tasks -- <command> [args…]

Commands:
  info                 Environment banner
  build [args…]        dotnet build fspure.slnx
  test [args…]         dotnet test fspure.slnx
  docs [args…]         DocsGenerator (preview | stable | flags)
  devcontainer [args…] DevcontainerGen (optional --check)
  security             NuGet vulnerable scan + npm audit
  ready-lib-gate       Pack analyzer + ReadyLib local-feed e2e gate
  phase5               Phase 5 regression net
  help                 This text

Examples:
  dotnet run --project src/Fspure.Tasks -- docs preview
  dotnet run --project src/Fspure.Tasks -- security
  dotnet run --project src/Fspure.Tasks -- ready-lib-gate
  dotnet run --project src/Fspure.Tasks -- test --filter FullyQualifiedName~SchemaTests
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

            let dv =
                let r = Repo.dotnetCapture root "--version"
                r.Stdout.Trim()

            printfn "  dotnet: %s" (if dv = "" then "?" else dv)
            printfn "  apps: build test docs devcontainer security ready-lib-gate phase5 info"
            0

        | Some "build" ->
            let rest = argv |> Array.skip 1 |> String.concat " "
            let cfg = Repo.configuration ()
            let args =
                if rest = "" then
                    $"build fspure.slnx -c {cfg} --nologo"
                else
                    $"build fspure.slnx -c {cfg} --nologo {rest}"

            Repo.dotnet root args

        | Some "test" ->
            let rest = argv |> Array.skip 1 |> String.concat " "
            let cfg = Repo.configuration ()
            let args =
                if rest = "" then
                    $"test fspure.slnx -c {cfg} --nologo"
                else
                    $"test fspure.slnx -c {cfg} --nologo {rest}"

            Repo.dotnet root args

        | Some "docs" ->
            let rest = argv |> Array.skip 1 |> Array.toList
            Repo.runProject root "src/DocsGenerator/DocsGenerator.fsproj" rest

        | Some "devcontainer" ->
            let rest = argv |> Array.skip 1 |> Array.toList
            Repo.runProject root "src/DevcontainerGen/DevcontainerGen.fsproj" rest

        | Some "security" -> Security.run root

        | Some ("ready-lib-gate" | "ready-lib" | "gate") -> ReadyLibGate.run root

        | Some ("phase5" | "phase5-regression") -> Phase5.run root

        | Some other ->
            eprintfn "Unknown command: %s" other
            eprintf "%s" (usage ())
            2
    with ex ->
        eprintfn "ERROR: %s" ex.Message

        if not (isNull ex.StackTrace) && Repo.envOr "FSPURE_DEBUG" "" = "1" then
            eprintfn "%s" ex.StackTrace

        1
