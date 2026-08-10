/// Merge src/devcontainer/fragments into generated configs under .generated/devcontainer/.
/// Optionally materializes platform copies (root .devcontainer, nested CI configs).
module Fspure.DevcontainerGen.Program

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

let private banner =
    "// GENERATED FILE — do not edit by hand.\n"
    + "// Source: src/devcontainer/fragments/  |  Regenerate: dotnet run --project src/DevcontainerGen\n"

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
        let keys =
            o
            |> Seq.filter (fun p -> isNull p.Value)
            |> Seq.map (fun p -> p.Key)
            |> Seq.toList

        for k in keys do
            o.Remove k |> ignore

        o
    | _ -> failwith "merged document must be an object"

let private writeText (path: string) (text: string) =
    Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
    File.WriteAllText(path, text)

let private resolveOut (repoRoot: string) (rel: string) =
    Path.GetFullPath(Path.Combine(repoRoot, rel.Replace('/', Path.DirectorySeparatorChar)))

let private generateAll (repoRoot: string) (fragmentsDir: string) : unit =
    let flavoursPath = Path.Combine(fragmentsDir, "flavours.json")
    let table = loadFlavourTable flavoursPath

    for prop in table do
        let flavour = prop.Key
        let cfg = prop.Value :?> JsonObject
        let fragments = cfg["fragments"] :?> JsonArray
        let outputRel = cfg["output"].GetValue<string>()
        let doc = buildFlavor fragmentsDir fragments
        let text = render doc
        let outPath = resolveOut repoRoot outputRel
        writeText outPath text
        printfn "  wrote %-5s → %s" flavour outputRel

        // Optional platform materialization (Codespaces / nested CI configFile paths).
        match cfg["platform"] with
        | null -> ()
        | node ->
            let platformRel = node.GetValue<string>()
            let platformPath = resolveOut repoRoot platformRel
            writeText platformPath text
            printfn "           + platform → %s" platformRel

    printfn "Done."

let private checkAll (repoRoot: string) (fragmentsDir: string) : int =
    let flavoursPath = Path.Combine(fragmentsDir, "flavours.json")
    let table = loadFlavourTable flavoursPath
    let stale = ResizeArray<string>()

    for prop in table do
        let flavour = prop.Key
        let cfg = prop.Value :?> JsonObject
        let fragments = cfg["fragments"] :?> JsonArray
        let outputRel = cfg["output"].GetValue<string>()
        let expected = buildFlavor fragmentsDir fragments
        let outPath = resolveOut repoRoot outputRel

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

        // Platform copies must match when present on disk.
        match cfg["platform"] with
        | null -> ()
        | node ->
            let platformRel = node.GetValue<string>()
            let platformPath = resolveOut repoRoot platformRel

            if File.Exists platformPath then
                let platformText = File.ReadAllText platformPath

                try
                    let actual = JsonNode.Parse(stripBanner platformText)

                    if
                        actual.ToJsonString() <> (expected :> JsonNode).ToJsonString()
                        || not (platformText.StartsWith("// GENERATED"))
                    then
                        stale.Add $"{flavour} platform ({platformRel})"
                with _ ->
                    stale.Add $"{flavour} platform invalid ({platformRel})"

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
        let start = Directory.GetCurrentDirectory()

        let rec walk d n =
            if n > 8 then start
            elif File.Exists(Path.Combine(d, "fspure.slnx")) then d
            else
                match Directory.GetParent d with
                | null -> start
                | p -> walk p.FullName (n + 1)

        walk start 0

    let fragmentsDir = Path.Combine(repoRoot, "src", "devcontainer", "fragments")
    let check = argv |> Array.exists (fun a -> a = "--check")

    try
        if not (Directory.Exists fragmentsDir) then
            eprintfn "ERROR: fragments dir missing: %s" fragmentsDir
            1
        elif check then
            checkAll repoRoot fragmentsDir
        else
            generateAll repoRoot fragmentsDir
            0
    with ex ->
        eprintfn "ERROR: %s" ex.Message
        1
