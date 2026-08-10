/// Merge .devcontainer/fragments into generated devcontainer.json files (F#; replaces generate.py).
module Fspure.DevcontainerGen.Program

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

let private banner =
    "// GENERATED FILE — do not edit by hand.\n"
    + "// Source: .devcontainer/fragments/  |  Regenerate: dotnet run --project src/DevcontainerGen\n"

let rec private deepMerge (baseNode: JsonNode) (overlay: JsonNode) : JsonNode =
    match baseNode, overlay with
    | (:? JsonObject as baseObj), (:? JsonObject as overObj) ->
        let out = JsonObject()

        for prop in baseObj do
            out[prop.Key] <-
                match prop.Value with
                | null -> null
                | v -> v.DeepClone()

        for prop in overObj do
            match prop.Value with
            | null -> out.Remove prop.Key |> ignore
            | overVal ->
                match out[prop.Key], overVal with
                | (:? JsonObject as existing), (:? JsonObject as overObj2) ->
                    out[prop.Key] <- deepMerge existing overObj2
                | _ -> out[prop.Key] <- overVal.DeepClone()

        out
    | _, over -> over.DeepClone()

let private loadJson (path: string) : JsonNode =
    JsonNode.Parse(File.ReadAllText path)

let private stripBanner (text: string) : string =
    let lines: string list = text.Split([| '\n' |], StringSplitOptions.None) |> Array.toList

    let rec skip (xs: string list) =
        match xs with
        | h :: t when h.TrimStart().StartsWith("//") || h.Trim() = "" -> skip t
        | rest -> rest

    String.Join("\n", skip lines)

let private render (doc: JsonObject) : string =
    let opts = JsonSerializerOptions(WriteIndented = true)
    banner + doc.ToJsonString(opts) + "\n"

let private loadFlavourTable (flavoursPath: string) : JsonObject =
    let root = loadJson flavoursPath :?> JsonObject

    match root["flavours"], root["flavors"] with
    | (:? JsonObject as f), _ -> f
    | _, (:? JsonObject as f) -> f
    | _ -> failwith "flavours.json must define 'flavours'"

let private buildFlavor (fragmentsDir: string) (fragmentNames: JsonArray) : JsonObject =
    let mutable doc: JsonNode = JsonObject()

    for nameNode in fragmentNames do
        let name = nameNode.GetValue<string>()
        let path = Path.Combine(fragmentsDir, name)

        if not (File.Exists path) then
            failwith $"missing fragment: {path}"

        doc <- deepMerge doc (loadJson path)

    match doc with
    | :? JsonObject as o ->
        // Drop top-level nulls
        let keys =
            o
            |> Seq.filter (fun p -> isNull p.Value)
            |> Seq.map (fun p -> p.Key)
            |> Seq.toList

        for k in keys do
            o.Remove k |> ignore

        o
    | _ -> failwith "merged document must be an object"

let private generateAll (devcontainerDir: string) : unit =
    let fragmentsDir = Path.Combine(devcontainerDir, "fragments")
    let flavoursPath = Path.Combine(fragmentsDir, "flavours.json")
    let table = loadFlavourTable flavoursPath

    for prop in table do
        let flavour = prop.Key
        let cfg = prop.Value :?> JsonObject
        let fragments = cfg["fragments"] :?> JsonArray
        let outputRel = cfg["output"].GetValue<string>()
        let doc = buildFlavor fragmentsDir fragments
        let outPath = Path.GetFullPath(Path.Combine(devcontainerDir, outputRel))
        Directory.CreateDirectory(Path.GetDirectoryName outPath) |> ignore
        File.WriteAllText(outPath, render doc)
        printfn "  wrote %-5s → %s" flavour outputRel

    printfn "Done."

let private checkAll (devcontainerDir: string) : int =
    let fragmentsDir = Path.Combine(devcontainerDir, "fragments")
    let flavoursPath = Path.Combine(fragmentsDir, "flavours.json")
    let table = loadFlavourTable flavoursPath
    let stale = ResizeArray<string>()

    for prop in table do
        let flavour = prop.Key
        let cfg = prop.Value :?> JsonObject
        let fragments = cfg["fragments"] :?> JsonArray
        let outputRel = cfg["output"].GetValue<string>()
        let expected = buildFlavor fragmentsDir fragments
        let outPath = Path.GetFullPath(Path.Combine(devcontainerDir, outputRel))

        if not (File.Exists outPath) then
            stale.Add $"{flavour} (missing {outPath})"
        else
            let actualText = File.ReadAllText outPath

            try
                let actual = JsonNode.Parse(stripBanner actualText)

                if
                    actual.ToJsonString() <> (expected :> JsonNode).ToJsonString()
                    || not (actualText.StartsWith("// GENERATED"))
                then
                    stale.Add flavour
            with _ ->
                stale.Add $"{flavour} (invalid JSON)"

    if stale.Count > 0 then
        eprintfn "Generated devcontainer.json files are out of date:"

        for s in stale do
            eprintfn "  - %s" s

        eprintfn "\nRun: dotnet run --project src/DevcontainerGen"
        1
    else
        printfn "OK: all generated devcontainer.json files are up to date."
        0

[<EntryPoint>]
let main argv =
    let repoRoot =
        // Prefer cwd; walk up for fspure.slnx
        let start = Directory.GetCurrentDirectory()

        let rec walk d n =
            if n > 8 then start
            elif File.Exists(Path.Combine(d, "fspure.slnx")) then d
            else
                match Directory.GetParent d with
                | null -> start
                | p -> walk p.FullName (n + 1)

        walk start 0

    let devcontainerDir = Path.Combine(repoRoot, ".devcontainer")
    let check = argv |> Array.exists (fun a -> a = "--check")

    try
        if check then
            checkAll devcontainerDir
        else
            generateAll devcontainerDir
            0
    with ex ->
        eprintfn "ERROR: %s" ex.Message
        1
