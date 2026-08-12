namespace Fspure.Cli

open System
open System.IO

/// Path normalization for deterministic reports and --focus / --ignore.
module Paths =

    /// Forward slashes, no leading `./`, no trailing slash (except empty).
    let normalize (p: string) : string =
        if String.IsNullOrWhiteSpace p then
            ""
        else
            let n = p.Replace('\\', '/')
            let n = n.Trim()
            let n =
                if n.StartsWith("./", StringComparison.Ordinal) then
                    n.Substring(2)
                else
                    n

            n.TrimStart('/').TrimEnd('/')

    let isAbsolute (p: string) =
        if String.IsNullOrWhiteSpace p then false
        elif p.StartsWith("/", StringComparison.Ordinal) then true
        elif p.Length >= 3 && Char.IsLetter p[0] && p[1] = ':' then true
        else Path.IsPathRooted p

    /// Relativize `path` against `root`. Always returns a normalized relative path
    /// when possible; otherwise the normalized absolute path.
    let relativeTo (root: string) (path: string) : string =
        if String.IsNullOrWhiteSpace path then
            ""
        else
            try
                let fullPath =
                    if isAbsolute path then
                        Path.GetFullPath path
                    elif String.IsNullOrWhiteSpace root then
                        Path.GetFullPath path
                    else
                        Path.GetFullPath(Path.Combine(root, path))

                if String.IsNullOrWhiteSpace root then
                    normalize fullPath
                else
                    let fullRoot = Path.GetFullPath root
                    let rel = Path.GetRelativePath(fullRoot, fullPath)

                    if rel.StartsWith("..", StringComparison.Ordinal) then
                        normalize fullPath
                    else
                        normalize rel
            with _ ->
                normalize path

    let fromUri (projectDir: string) (uri: string) : string =
        if String.IsNullOrWhiteSpace uri then
            ""
        else
            let raw =
                if uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase) then
                    try
                        Uri(uri).LocalPath
                    with _ ->
                        uri
                else
                    uri

            relativeTo projectDir raw

    let projectDirectory (projectPath: string) : string =
        match Path.GetDirectoryName(Path.GetFullPath projectPath) with
        | null
        | "" -> Directory.GetCurrentDirectory()
        | d -> d
