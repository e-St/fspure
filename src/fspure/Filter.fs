namespace Fspure.Cli

open System
open System.Text
open System.Text.RegularExpressions

/// --focus / --ignore matching: directory prefix, exact file, or a simple glob.
module Filter =

    let private hasGlob (pattern: string) =
        pattern.IndexOf '*' >= 0 || pattern.IndexOf '?' >= 0

    let private globToRegex (pattern: string) : Regex =
        let p = Paths.normalize pattern
        let sb = StringBuilder("^")
        let mutable i = 0

        while i < p.Length do
            match p[i] with
            | '*' when i + 1 < p.Length && p[i + 1] = '*' ->
                if i + 2 < p.Length && p[i + 2] = '/' then
                    sb.Append("(?:.*/)?") |> ignore
                    i <- i + 3
                else
                    sb.Append(".*") |> ignore
                    i <- i + 2
            | '*' ->
                sb.Append("[^/]*") |> ignore
                i <- i + 1
            | '?' ->
                sb.Append("[^/]") |> ignore
                i <- i + 1
            | c when Char.IsAsciiLetterOrDigit c || c = '_' || c = '-' || c = '/' || c = '.' ->
                sb.Append c |> ignore
                i <- i + 1
            | c ->
                sb.Append('\\').Append c |> ignore
                i <- i + 1

        sb.Append('$') |> ignore
        Regex(sb.ToString(), RegexOptions.CultureInvariant ||| RegexOptions.IgnoreCase)

    /// A pattern matches a project-relative file path.
    let matches (pattern: string) (file: string) : bool =
        let pat = Paths.normalize pattern
        let path = Paths.normalize file

        if String.IsNullOrWhiteSpace pat || String.IsNullOrWhiteSpace path then
            false
        elif hasGlob pat then
            (globToRegex pat).IsMatch path
        elif path.Equals(pat, StringComparison.OrdinalIgnoreCase) then
            true
        else
            path.StartsWith(pat + "/", StringComparison.OrdinalIgnoreCase)

    let private anyMatch (patterns: string list) (file: string) =
        patterns
        |> List.exists (fun p -> not (String.IsNullOrWhiteSpace p) && matches p file)

    /// Apply focus (if any) then ignore. Empty focus = all files.
    let apply (focus: string list) (ignore: string list) (items: Diagnostic list) : Diagnostic list =
        let focused =
            let focus = focus |> List.filter (fun p -> not (String.IsNullOrWhiteSpace p))

            if focus.IsEmpty then
                items
            else
                items |> List.filter (fun d -> anyMatch focus d.File)

        let ignore = ignore |> List.filter (fun p -> not (String.IsNullOrWhiteSpace p))

        if ignore.IsEmpty then
            focused
        else
            focused |> List.filter (fun d -> not (anyMatch ignore d.File))
