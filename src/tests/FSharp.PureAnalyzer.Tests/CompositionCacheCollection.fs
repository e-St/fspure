namespace FSharp.PureAnalyzer.Tests

open Xunit

/// PureSet.compositionCache is process-wide. Tests that clear it and then
/// assert reference equality must not run beside each other, or a clear
/// between the two lookups yields a new Index and the assertion fails.
[<CollectionDefinition("CompositionCache")>]
type CompositionCacheCollection() = class end
