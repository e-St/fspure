module Fspure.DecorationLogic.Tests.LogicTests

open Fspure.DecorationLogic
open Xunit

let private d code line source =
    {
        Logic.Code = code
        Logic.Source = source
        Logic.Line = Some line
    }

[<Fact>]
let ``diagnosticCode normalizes string`` () =
    Assert.Equal("PURE002", Logic.diagnosticCode (box "PURE002"))

[<Fact>]
let ``recognition of pure analyzer diagnostics`` () =
    Assert.True(Logic.isPureAnalyzerDiagnostic (d "PURE001" 0 "Pure analyzer"))
    Assert.True(Logic.isPureAnalyzerDiagnostic (d "PURE002" 0 "Pure analyzer"))
    Assert.True(Logic.isPureAnalyzerDiagnostic (d "PURE003" 0 "Pure analyzer"))
    Assert.True(Logic.isPureAnalyzerDiagnostic (d "OTHER" 0 "FSharp.PureAnalyzer"))
    Assert.False(Logic.isPureAnalyzerDiagnostic (d "FS0039" 0 "F# Compiler"))

[<Fact>]
let ``definition badge contract`` () =
    Assert.Equal(Some Logic.badgeImpure, Logic.badgeForDefinitionCode "PURE002")
    Assert.Equal(Some Logic.badgePure, Logic.badgeForDefinitionCode "PURE003")
    Assert.Equal(None, Logic.badgeForDefinitionCode "PURE001")

[<Fact>]
let ``PURE001 alone produces no badge`` () =
    let map = Logic.badgesByLine [ d "PURE001" 5 "Pure analyzer" ]
    Assert.Equal(0, map.Count)

[<Fact>]
let ``pure and impure definition badges`` () =
    let pureMap = Logic.badgesByLine [ d "PURE003" 10 "Pure analyzer" ]
    Assert.Equal("pure", pureMap[10].Badge)
    let impureMap = Logic.badgesByLine [ d "PURE002" 11 "Pure analyzer" ]
    Assert.Equal("impure", impureMap[11].Badge)

[<Fact>]
let ``impure wins over pure on the same line`` () =
    let map =
        Logic.badgesByLine
            [
                d "PURE003" 12 "Pure analyzer"
                d "PURE002" 12 "Pure analyzer"
            ]
    Assert.Equal("impure", map[12].Badge)

[<Fact>]
let ``customer e2e multi-line contract`` () =
    let map =
        Logic.badgesByLine
            [
                d "PURE002" 55 "Pure analyzer"
                d "PURE003" 449 "Pure analyzer"
                d "PURE003" 452 "Pure analyzer"
                d "PURE003" 454 "Pure analyzer"
                d "PURE002" 505 "Pure analyzer"
                d "PURE001" 560 "Pure analyzer"
            ]
    Assert.Equal("impure", map[55].Badge)
    Assert.Equal("pure", map[449].Badge)
    Assert.Equal("pure", map[452].Badge)
    Assert.Equal("pure", map[454].Badge)
    Assert.Equal("impure", map[505].Badge)
    Assert.False(Map.containsKey 560 map)
