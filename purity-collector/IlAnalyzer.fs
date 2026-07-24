namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.Reflection.PortableExecutable
open System.Runtime.InteropServices

/// IL-level static analysis used to produce List A from FSharp.Core / BCL assemblies.
module IlAnalyzer =

    type private AssemblyModel =
        {
            Name: string
            Path: string
            PEReader: PEReader
            Metadata: MetadataReader
        }

    let private safeDispose (d: #IDisposable) =
        try
            d.Dispose()
        with _ ->
            ()

    let private readAssembly (path: string) : AssemblyModel option =
        try
            if not (File.Exists path) then
                None
            else
                let stream = File.OpenRead path
                let pe = new PEReader(stream, PEStreamOptions.PrefetchEntireImage)

                if not pe.HasMetadata then
                    safeDispose pe
                    None
                else
                    let md = pe.GetMetadataReader()
                    let def = md.GetAssemblyDefinition()
                    let name = md.GetString def.Name

                    Some
                        {
                            Name = name
                            Path = path
                            PEReader = pe
                            Metadata = md
                        }
        with _ ->
            None

    let private getString (md: MetadataReader) (h: StringHandle) = if h.IsNil then "" else md.GetString h

    let private typeFullName (md: MetadataReader) (td: TypeDefinition) : string =
        let ns = getString md td.Namespace
        let name = getString md td.Name

        if String.IsNullOrEmpty ns then name else ns + "." + name

    let private resolveTypeRefName (md: MetadataReader) (tr: TypeReferenceHandle) : string =
        let t = md.GetTypeReference tr
        let ns = getString md t.Namespace
        let name = getString md t.Name

        if String.IsNullOrEmpty ns then name else ns + "." + name

    let private resolveTypeDefName (md: MetadataReader) (td: TypeDefinitionHandle) : string =
        typeFullName md (md.GetTypeDefinition td)

    let private methodFullName (typeName: string) (methodName: string) = $"{typeName}.{methodName}"

    let private resolveEntityName (md: MetadataReader) (handle: EntityHandle) : string option =
        try
            match handle.Kind with
            | HandleKind.TypeDefinition ->
                let h = MetadataTokens.TypeDefinitionHandle(MetadataTokens.GetRowNumber handle)
                Some(resolveTypeDefName md h)
            | HandleKind.TypeReference ->
                let h = MetadataTokens.TypeReferenceHandle(MetadataTokens.GetRowNumber handle)
                Some(resolveTypeRefName md h)
            | HandleKind.TypeSpecification -> Some "System.Object"
            | _ -> None
        with _ ->
            None

    let private resolveMemberRefName (md: MetadataReader) (mr: MemberReferenceHandle) : string option =
        try
            let m = md.GetMemberReference mr
            let name = getString md m.Name

            match resolveEntityName md m.Parent with
            | Some parent -> Some(methodFullName parent name)
            | None -> None
        with _ ->
            None

    let private resolveMethodDefName (md: MetadataReader) (mh: MethodDefinitionHandle) (fallbackType: string) : string =
        let m = md.GetMethodDefinition mh
        let name = getString md m.Name

        let declaring =
            try
                let td = m.GetDeclaringType()

                if td.IsNil then fallbackType else resolveTypeDefName md td
            with _ ->
                fallbackType

        methodFullName declaring name

    let private resolveMethodSpecName (md: MetadataReader) (ms: MethodSpecificationHandle) : string option =
        try
            let spec = md.GetMethodSpecification ms

            match spec.Method.Kind with
            | HandleKind.MethodDefinition ->
                let mh =
                    MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber spec.Method)

                Some(resolveMethodDefName md mh "Unknown")
            | HandleKind.MemberReference ->
                let mr =
                    MetadataTokens.MemberReferenceHandle(MetadataTokens.GetRowNumber spec.Method)

                resolveMemberRefName md mr
            | _ -> None
        with _ ->
            None

    let private handleMemberToken (md: MetadataReader) (declaringType: string) (token: int) (callees: HashSet<string>) =
        try
            let handle = MetadataTokens.EntityHandle token

            match handle.Kind with
            | HandleKind.MethodDefinition ->
                let mh = MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber handle)
                callees.Add(resolveMethodDefName md mh declaringType) |> ignore
            | HandleKind.MemberReference ->
                let mr = MetadataTokens.MemberReferenceHandle(MetadataTokens.GetRowNumber handle)

                match resolveMemberRefName md mr with
                | Some name -> callees.Add name |> ignore
                | None -> ()
            | HandleKind.MethodSpecification ->
                let ms =
                    MetadataTokens.MethodSpecificationHandle(MetadataTokens.GetRowNumber handle)

                match resolveMethodSpecName md ms with
                | Some name -> callees.Add name |> ignore
                | None -> ()
            | _ -> ()
        with _ ->
            ()

    let private handleFieldToken (md: MetadataReader) (token: int) (isStore: bool) (mark: string -> unit) =
        try
            let handle = MetadataTokens.EntityHandle token

            match handle.Kind with
            | HandleKind.FieldDefinition ->
                let fh = MetadataTokens.FieldDefinitionHandle(MetadataTokens.GetRowNumber handle)
                let fd = md.GetFieldDefinition fh
                let fname = getString md fd.Name
                let attrs = fd.Attributes

                if isStore then
                    if attrs.HasFlag FieldAttributes.Static then
                        mark $"Stores static field '{fname}'"
                    elif not (attrs.HasFlag FieldAttributes.InitOnly) then
                        mark $"Mutates instance field '{fname}'"
            | HandleKind.MemberReference ->
                let mr = MetadataTokens.MemberReferenceHandle(MetadataTokens.GetRowNumber handle)
                let m = md.GetMemberReference mr
                let fname = getString md m.Name

                if isStore then
                    mark $"Stores external field '{fname}'"
            | _ -> ()
        with _ ->
            ()

    /// Decode a method body and collect callees + local impurity signals.
    let private analyzeBody
        (asm: AssemblyModel)
        (methodDef: MethodDefinition)
        (declaringType: string)
        : string list * bool * string list =
        let md = asm.Metadata

        if methodDef.RelativeVirtualAddress = 0 then
            [], false, []
        else
            try
                let body = asm.PEReader.GetMethodBody(methodDef.RelativeVirtualAddress)

                match body.GetILBytes() with
                | null -> [], false, []
                | il ->
                    let callees = HashSet<string>(StringComparer.Ordinal)
                    let reasons = ResizeArray<string>()
                    let mutable hasImpurity = false
                    let mutable i = 0

                    let mark (reason: string) =
                        hasImpurity <- true
                        reasons.Add reason

                    let readByte () =
                        let b = il[i]
                        i <- i + 1
                        b

                    let readInt32 () =
                        let v = BitConverter.ToInt32(il, i)
                        i <- i + 4
                        v

                    let readToken () = readInt32 ()

                    while i < il.Length do
                        let op = readByte ()

                        match op with
                        | 0x28uy ->
                            let token = readToken ()
                            handleMemberToken md declaringType token callees
                        | 0x6Fuy ->
                            let token = readToken ()
                            handleMemberToken md declaringType token callees
                        | 0x29uy ->
                            let _ = readToken ()
                            mark "Indirect call (calli)"
                        | 0x73uy ->
                            let token = readToken ()
                            handleMemberToken md declaringType token callees
                        | 0x80uy ->
                            let token = readToken ()
                            handleFieldToken md token true mark
                        | 0x7Duy ->
                            let token = readToken ()
                            handleFieldToken md token true mark
                        | 0x7Euy
                        | 0x7Buy ->
                            let _ = readToken ()
                            ()
                        | 0x9Cuy
                        | 0x9Duy
                        | 0x9Euy
                        | 0x9Fuy
                        | 0xA0uy
                        | 0xA1uy
                        | 0xA2uy -> mark "Array element store (stelem)"
                        | 0xFEuy when i < il.Length ->
                            let sub = readByte ()

                            match sub with
                            | 0x06uy
                            | 0x07uy ->
                                let token = readToken ()
                                handleMemberToken md declaringType token callees
                            | 0x15uy ->
                                let _ = readToken ()
                                mark "initobj"
                            | 0x17uy -> mark "cpblk"
                            | 0x18uy -> mark "initblk"
                            | 0x0Euy -> mark "localloc"
                            | 0x09uy -> mark "arglist"
                            | 0x12uy ->
                                let _ = readByte ()
                                ()
                            | 0x13uy
                            | 0x14uy
                            | 0x1Fuy -> ()
                            | 0x16uy ->
                                let _ = readToken ()
                                ()
                            | 0x1Cuy
                            | 0x1Duy ->
                                let _ = readToken ()
                                ()
                            | _ -> ()
                        | 0x27uy ->
                            let _ = readToken ()
                            mark "jmp (non-local control transfer)"
                        | 0x0Euy
                        | 0x0Fuy
                        | 0x10uy
                        | 0x11uy
                        | 0x12uy
                        | 0x13uy
                        | 0x1Fuy
                        | 0x2Buy
                        | 0x2Cuy
                        | 0x2Duy
                        | 0x2Euy
                        | 0x2Fuy
                        | 0x30uy
                        | 0x31uy
                        | 0x32uy
                        | 0x33uy
                        | 0x34uy
                        | 0x35uy
                        | 0x36uy
                        | 0x37uy ->
                            let _ = readByte ()
                            ()
                        | 0x20uy
                        | 0x38uy
                        | 0x39uy
                        | 0x3Auy
                        | 0x3Buy
                        | 0x3Cuy
                        | 0x3Duy
                        | 0x3Euy
                        | 0x3Fuy
                        | 0x40uy
                        | 0x41uy
                        | 0x42uy
                        | 0x43uy
                        | 0x44uy ->
                            let _ = readInt32 ()
                            ()
                        | 0x21uy -> i <- i + 8
                        | 0x22uy -> i <- i + 4
                        | 0x23uy -> i <- i + 8
                        | 0x45uy ->
                            let n = readInt32 ()
                            i <- i + (n * 4)
                        | 0x70uy
                        | 0x71uy
                        | 0x72uy
                        | 0x74uy
                        | 0x79uy
                        | 0x81uy
                        | 0x7Cuy
                        | 0x8Cuy
                        | 0x8Duy
                        | 0xA3uy
                        | 0xA4uy
                        | 0xA5uy
                        | 0xD0uy
                        | 0x8Fuy
                        | 0x7Fuy ->
                            let token = readToken ()

                            match op with
                            | 0xA4uy -> mark "stelem (typed)"
                            | 0x8Fuy -> mark "ldelema (managed pointer to array element)"
                            | 0x81uy
                            | 0x7Fuy -> mark "ldsflda (address of static field)"
                            | 0x7Cuy -> mark "ldflda (address of instance field)"
                            | _ -> ignore token
                        | _ -> ()

                    List.ofSeq callees, hasImpurity, List.ofSeq reasons
            with ex ->
                [], true, [ $"Failed to decode IL: {ex.Message}" ]

    let private isCompilerGeneratedName (name: string) =
        String.IsNullOrEmpty name
        || name.StartsWith("<", StringComparison.Ordinal)
        || name.StartsWith("-", StringComparison.Ordinal)
        || name.Contains("<>", StringComparison.Ordinal)
        || name.Contains("@", StringComparison.Ordinal)
        || name.Contains("|", StringComparison.Ordinal)

    let private shouldSkipType (typeName: string) =
        isCompilerGeneratedName typeName
        || typeName.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal)
        || typeName.StartsWith("System.Diagnostics.CodeAnalysis", StringComparison.Ordinal)

    /// Analyze a single assembly path into AnalyzedMethod records.
    let analyzeAssembly (path: string) : AnalyzedMethod list =
        match readAssembly path with
        | None -> []
        | Some asm ->
            try
                let md = asm.Metadata
                let results = ResizeArray<AnalyzedMethod>()

                for th in md.TypeDefinitions do
                    let td = md.GetTypeDefinition th
                    let typeName = typeFullName md td

                    if not (shouldSkipType typeName) then
                        let typeImpure = ImpurityRules.typeImpurityReason typeName

                        for mh in td.GetMethods() do
                            let m = md.GetMethodDefinition mh
                            let methodName = getString md m.Name

                            if methodName <> ".cctor" && not (isCompilerGeneratedName methodName) then
                                let fullName = methodFullName typeName methodName
                                let attrs = m.Attributes

                                let isPublic =
                                    attrs.HasFlag MethodAttributes.Public
                                    || attrs.HasFlag MethodAttributes.Family
                                    || attrs.HasFlag MethodAttributes.FamORAssem

                                let isStatic = attrs.HasFlag MethodAttributes.Static
                                let isAbstract = attrs.HasFlag MethodAttributes.Abstract
                                let isPinvoke = attrs.HasFlag MethodAttributes.PinvokeImpl
                                let hasRva = m.RelativeVirtualAddress <> 0

                                let callees, bodyImpure, bodyReasons =
                                    if hasRva && not isAbstract then
                                        analyzeBody asm m typeName
                                    else
                                        [], false, []

                                let reasons = ResizeArray<string>()
                                let mutable localImpure = bodyImpure

                                match typeImpure with
                                | Some r ->
                                    localImpure <- true
                                    reasons.Add r
                                | None -> ()

                                if isPinvoke then
                                    localImpure <- true
                                    reasons.Add "P/Invoke method"

                                if methodName.StartsWith("set_", StringComparison.Ordinal) then
                                    localImpure <- true
                                    reasons.Add "Property setter"

                                if
                                    methodName.StartsWith("add_", StringComparison.Ordinal)
                                    || methodName.StartsWith("remove_", StringComparison.Ordinal)
                                then
                                    localImpure <- true
                                    reasons.Add "Event accessor"

                                for r in bodyReasons do
                                    reasons.Add r

                                let isLeaf = ImpurityRules.isKnownPureLeaf fullName

                                let finalImpure =
                                    if isLeaf then false
                                    elif not hasRva && not isAbstract && not isPinvoke then true
                                    else localImpure

                                let finalReasons =
                                    if isLeaf then
                                        []
                                    elif not hasRva && finalImpure && reasons.Count = 0 then
                                        [ "No IL body and not a known pure leaf/intrinsic" ]
                                    else
                                        List.ofSeq reasons

                                results.Add
                                    {
                                        FullName = fullName
                                        AssemblyName = asm.Name
                                        IsPublic = isPublic
                                        IsStatic = isStatic
                                        HasBody = hasRva
                                        Callees = callees |> List.distinct |> List.sort
                                        HasLocalImpurity = finalImpure
                                        ImpurityReasons = finalReasons
                                    }

                List.ofSeq results
            finally
                safeDispose asm.PEReader

    /// Default assemblies that form the foundational List A surface.
/// Default assemblies that form the foundational List A surface.
/// Extended with high-value pure-leaning libraries commonly used in F# codebases.
let defaultAssemblyPaths () : string list =
    let tpa =
        match AppContext.GetData "TRUSTED_PLATFORM_ASSEMBLIES" with
        | :? string as s when not (String.IsNullOrWhiteSpace s) ->
            s.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList
        | _ -> []

    // -----------------------------------------------------------------
    // Expanded wanted set
    // -----------------------------------------------------------------
    let wanted =
        set
            [
                "System.Private.CoreLib"
                "System.Runtime"
                "System.Console"
                "System.Linq"
                "System.Collections"
                "System.Collections.Concurrent"
                "System.Collections.Immutable"
                "System.Memory"
                "System.Threading"
                "System.Threading.Tasks"
                "System.Text.RegularExpressions"
                "System.ObjectModel"
                "System.Numerics"
                "FSharp.Core"
                "System.Text.Json"                    // JsonSerializer, JsonNode, Utf8JsonReader (pure parts)
                "System.Text.Encodings.Web"           // HtmlEncoder, UrlEncoder
                "System.Xml.Linq"                     // XElement / XDocument construction
                "System.Xml.XDocument"                // (some runtimes ship this separately)
                "System.Xml"                          // core XML
                "System.Globalization"               // CultureInfo pure helpers, formatting
                "System.Buffers"                      // ArrayPool is impure, many helpers pure
                "System.IO.Pipelines"                 // PipeReader/Writer pure helpers (careful)
                "System.Runtime.CompilerServices.Unsafe"
                "System.Runtime.InteropServices"      // limited pure surface
                "Microsoft.Extensions.Primitives"     // StringValues, StringSegment
                "Microsoft.Extensions.DependencyInjection.Abstractions" // rarely pure, but small
                "System.ComponentModel"               // TypeConverter pure bits
                "System.ComponentModel.Primitives"
                "System.ComponentModel.TypeConverter"
                "System.Diagnostics.DiagnosticSource" // Activity is impure, some helpers pure
                "System.Net.Http.Json"                // JsonContent pure helpers
                "System.Text.Json.Serialization"      // (if present as separate assembly)
            ]

    // -----------------------------------------------------------------
    // Discovery (unchanged structure, slightly more locations)
    // -----------------------------------------------------------------
    let fromTpa =
        tpa
        |> List.filter (fun p ->
            match Path.GetFileNameWithoutExtension p with
            | null -> false
            | name -> wanted.Contains name)

    let runtimeDirs =
        let candidates = ResizeArray<string>()

        let addIfExists (dir: string) =
            if not (String.IsNullOrWhiteSpace dir) && Directory.Exists dir then
                candidates.Add(dir)

        addIfExists AppContext.BaseDirectory
        addIfExists Environment.CurrentDirectory
        addIfExists (RuntimeEnvironment.GetRuntimeDirectory())

        // DOTNET_ROOT / shared frameworks
        let dotnetRoot =
            match Environment.GetEnvironmentVariable "DOTNET_ROOT" with
            | null | "" -> None
            | v -> Some v

        match dotnetRoot with
        | Some r ->
            let sharedNetCore = Path.Combine(r, "shared", "Microsoft.NETCore.App")
            addIfExists sharedNetCore
            if Directory.Exists sharedNetCore then
                for versionDir in Directory.GetDirectories sharedNetCore do
                    addIfExists versionDir

            // Also look in ASP.NET shared framework (System.Text.Json often lives here)
            let sharedAspNet = Path.Combine(r, "shared", "Microsoft.AspNetCore.App")
            addIfExists sharedAspNet
            if Directory.Exists sharedAspNet then
                for versionDir in Directory.GetDirectories sharedAspNet do
                    addIfExists versionDir
        | None -> ()

        // NuGet package cache (user + machine)
        let nugetRoots =
            [
                Path.Combine(
                    Environment.GetFolderPath Environment.SpecialFolder.UserProfile,
                    ".nuget",
                    "packages"
                )
                match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
                | null | "" -> None
                | p -> Some p
                |> Option.toList
            ]
            |> List.distinct

        for root in nugetRoots do
            if Directory.Exists root then
                // We only add the root; the later EnumerateFiles will not recurse deeply
                // for performance. Callers can still pass explicit -a paths for deep NuGet hits.
                addIfExists root

        List.ofSeq candidates |> List.distinctBy (fun p -> Path.GetFullPath p)

    let runtimeCandidates =
        runtimeDirs
        |> List.collect (fun dir ->
            try
                Directory.EnumerateFiles(dir, "*.dll", SearchOption.TopDirectoryOnly)
                |> Seq.filter (fun p ->
                    match Path.GetFileNameWithoutExtension p with
                    | null -> false
                    | name -> wanted.Contains name)
                |> Seq.toList
            with _ ->
                [])
        |> List.distinctBy (fun p -> Path.GetFullPath p)

    // Explicit FSharp.Core location
    let fscoreLoc =
        let loc = typeof<unit>.Assembly.Location
        if String.IsNullOrWhiteSpace loc then None
        elif File.Exists loc then Some loc
        else None

    let localFs = Path.Combine(AppContext.BaseDirectory, "FSharp.Core.dll")

    let extras =
        [
            match fscoreLoc with
            | Some p -> yield p
            | None -> ()
            if File.Exists localFs then
                yield localFs
        ]

    (fromTpa @ runtimeCandidates @ extras)
    |> List.distinctBy (fun p -> Path.GetFullPath p)
    |> List.sort

    let analyzeAssemblies (paths: string list) : AnalyzedMethod list * string list =
        let analyzed = ResizeArray<string>()
        let methods = ResizeArray<AnalyzedMethod>()

        for path in paths do
            let ms = analyzeAssembly path

            if not ms.IsEmpty then
                analyzed.Add(Path.GetFullPath path)
                methods.AddRange ms

        List.ofSeq methods, List.ofSeq analyzed
