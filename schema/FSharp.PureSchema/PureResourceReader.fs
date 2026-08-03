namespace FSharp.PureSchema

open System
open System.Collections.Generic
open System.IO
open System.Reflection.Metadata
open System.Reflection.PortableExecutable
open System.Security.Cryptography
open System.Text

/// One embedded pure.json resource extracted from a managed assembly.
type ExtractedPureResource =
    {
        /// Manifest resource name as stored in the PE metadata.
        ResourceName: string
        /// UTF-8 text of the resource.
        Content: string
        /// SHA-256 (hex, lowercase) of the UTF-8 content bytes.
        ContentHash: string
        /// Parsed PureFile, or a schema/parse error.
        PureFile: Result<PureFile, PureFileError>
    }

/// Pure manifests discovered in a managed assembly PE.
type AssemblyPureManifests =
    {
        Path: string
        AssemblyName: string
        /// Module version id (MVID) of the primary module.
        Mvid: Guid
        Resources: ExtractedPureResource list
    }

/// Reads embedded `.pure.json` resources from managed assemblies using
/// System.Reflection.Metadata + PEReader (same stack as purity-collector).
module PureResourceReader =

    /// SHA-256 hex digest of UTF-8 bytes (stable cache key component).
    let contentHash (content: string) : string =
        let bytes = Encoding.UTF8.GetBytes(content)
        Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()

    /// True when a manifest resource name is a pure.json candidate.
    /// Matches names that end with `.pure.json`, or the conventional
    /// `{assemblyName}.pure.json` (with optional namespace prefix).
    let isPureJsonResourceName (assemblyName: string) (resourceName: string) : bool =
        if String.IsNullOrWhiteSpace resourceName then
            false
        else
            let rn = resourceName.Trim()

            if rn.EndsWith(".pure.json", StringComparison.OrdinalIgnoreCase) then
                true
            else
                let conventional = assemblyName + ".pure.json"

                rn.Equals(conventional, StringComparison.OrdinalIgnoreCase)
                || rn.EndsWith("." + conventional, StringComparison.OrdinalIgnoreCase)

    /// Read an embedded manifest resource blob (length-prefixed at Offset in the resources section).
    let private tryReadEmbeddedResource
        (pe: PEReader)
        (md: MetadataReader)
        (handle: ManifestResourceHandle)
        : (string * byte[]) option =
        try
            let resource = md.GetManifestResource handle
            let name = md.GetString resource.Name

            // Linked/external resources are out of scope for v1.
            if not resource.Implementation.IsNil then
                None
            else
                match pe.PEHeaders.CorHeader with
                | null -> None
                | corHeader ->
                    let rva = corHeader.ResourcesDirectory.RelativeVirtualAddress

                    if rva = 0 then
                        None
                    else
                        let block = pe.GetSectionData rva
                        // ManifestResource.Offset is int64; PEMemoryBlock uses int.
                        let offset = int resource.Offset

                        if offset < 0 || offset >= block.Length then
                            None
                        else
                            let remaining = block.Length - offset

                            if remaining < 4 then
                                None
                            else
                                let br = block.GetReader(offset, remaining)
                                let len = br.ReadInt32()

                                if len < 0 || len > br.RemainingBytes then
                                    None
                                else
                                    let bytes = br.ReadBytes len
                                    Some(name, bytes)
        with _ ->
            None

    let private extractFromPe (path: string) (pe: PEReader) : Result<AssemblyPureManifests, string> =
        try
            if not pe.HasMetadata then
                Error $"assembly has no metadata: {path}"
            else
                let md = pe.GetMetadataReader()
                let asmDef = md.GetAssemblyDefinition()
                let assemblyName = md.GetString asmDef.Name

                let mvid =
                    let moduleDef = md.GetModuleDefinition()
                    md.GetGuid moduleDef.Mvid

                let results = ResizeArray<ExtractedPureResource>()

                for handle in md.ManifestResources do
                    match tryReadEmbeddedResource pe md handle with
                    | None -> ()
                    | Some(resourceName, bytes) ->
                        if isPureJsonResourceName assemblyName resourceName then
                            let content =
                                try
                                    Encoding.UTF8.GetString bytes
                                with _ ->
                                    ""

                            let hash = contentHash content
                            let parsed = PureFileIO.parse content

                            results.Add(
                                {
                                    ResourceName = resourceName
                                    Content = content
                                    ContentHash = hash
                                    PureFile = parsed
                                }
                            )

                Ok
                    {
                        Path = path
                        AssemblyName = assemblyName
                        Mvid = mvid
                        Resources = results |> Seq.sortBy _.ResourceName |> List.ofSeq
                    }
        with ex ->
            Error $"failed to read pure resources from '{path}': {ex.Message}"

    /// Open a managed assembly path and return every matching embedded pure.json resource.
    let tryReadFromPath (path: string) : Result<AssemblyPureManifests, string> =
        if String.IsNullOrWhiteSpace path then
            Error "path is empty"
        elif not (File.Exists path) then
            Error $"file not found: {path}"
        else
            try
                use stream = File.OpenRead path
                use pe = new PEReader(stream, PEStreamOptions.PrefetchEntireImage)
                extractFromPe path pe
            with ex ->
                Error $"failed to open PE '{path}': {ex.Message}"

    /// Read pure manifests from a readable stream (stream is left open; caller owns lifetime).
    let tryReadFromStream (pathLabel: string) (stream: Stream) : Result<AssemblyPureManifests, string> =
        if isNull (box stream) then
            Error "stream is null"
        else
            try
                use pe = new PEReader(stream, PEStreamOptions.PrefetchEntireImage ||| PEStreamOptions.LeaveOpen)
                extractFromPe pathLabel pe
            with ex ->
                Error $"failed to read PE stream '{pathLabel}': {ex.Message}"

    /// Successfully parsed PureFile values from matching resources (skips parse failures).
    let parsedFiles (manifests: AssemblyPureManifests) : PureFile list =
        manifests.Resources
        |> List.choose (fun r ->
            match r.PureFile with
            | Ok f -> Some f
            | Error _ -> None)

    /// Build a stable cache-key fragment: `{mvid}:{hash1},{hash2},...` (hashes sorted).
    let cacheKeyFragment (mvid: Guid) (contentHashes: string seq) : string =
        let hashes =
            contentHashes
            |> Seq.filter (fun h -> not (String.IsNullOrWhiteSpace h))
            |> Seq.sort
            |> String.concat ","

        $"{mvid:N}:{hashes}"

    /// Join fragments for a multi-assembly composition cache key.
    let compositionCacheKey (fragments: string seq) : string =
        fragments
        |> Seq.filter (fun s -> not (String.IsNullOrWhiteSpace s))
        |> Seq.sort
        |> String.concat "|"
