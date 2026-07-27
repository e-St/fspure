    // ========== REPLACE the existing `isKnownPureLeaf` function with this ==========

    /// Names that must NEVER be treated as pure leaves via structural Invoke rules.
    let private invokeDenylistPrefixes =
        [|
            "System.Reflection."
            "System.Reflection.Emit."
            "System.Delegate"
            "System.MulticastDelegate"
        |]

    let private invokeDenylistExact =
        set
            [
                "System.Delegate.DynamicInvoke"
                "System.Reflection.MethodBase.Invoke"
                "System.Reflection.MethodInfo.Invoke"
                "System.Reflection.ConstructorInfo.Invoke"
                "System.Type.InvokeMember"
            ]

    let private isDeniedInvoke (normalized: string) =
        invokeDenylistExact.Contains normalized
        || invokeDenylistPrefixes
           |> Array.exists (fun p -> normalized.StartsWith(p, StringComparison.Ordinal))

    /// Only Microsoft.FSharp.Core FSharpFunc / OptimizedClosures Invoke* —
    /// never reflection or arbitrary *.Invoke.
    let private isSafeFunctionInvokeLeaf (normalized: string) =
        if isDeniedInvoke normalized then
            false
        else
            let isFsharpFunc =
                normalized.Contains("Microsoft.FSharp.Core.FSharpFunc", StringComparison.Ordinal)
                || normalized.Contains("Microsoft.FSharp.Core.OptimizedClosures", StringComparison.Ordinal)

            let isInvoke =
                normalized.EndsWith(".Invoke", StringComparison.Ordinal)
                || normalized.Contains(".InvokeFast", StringComparison.Ordinal)

            isFsharpFunc && isInvoke

    let isKnownPureLeaf (fullName: string) : bool =
        let n = normalizeName fullName

        if isDeniedInvoke n || isDeniedInvoke fullName then
            false
        elif knownPureLeaves.Contains n
             || knownPureLeaves.Contains fullName
             || CollectionLeaves.all.Contains n
             || CollectionLeaves.all.Contains fullName then
            true
        elif isSafeFunctionInvokeLeaf n then
            true
        else
            false
