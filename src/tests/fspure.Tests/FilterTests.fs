module fspure.Tests.FilterTests

open Fspure.Cli
open Xunit

let private d file code name =
    {
        Code = code
        File = file
        StartLine = 1
        StartColumn = 1
        EndLine = 1
        EndColumn = 8
        Message = $"Function '{name}' is not transitively pure."
        FullName = name
        Caller = ""
        Callee = ""
    }

[<Fact>]
let ``directory focus keeps children and not siblings`` () =
    let items =
        [
            d "src/Core/Logic.fs" "PURE002" "A"
            d "src/Core/More.fs" "PURE003" "B"
            d "src/Host/Program.fs" "PURE002" "C"
        ]

    let got = Filter.apply [ "src/Core" ] [] items |> List.map _.File
    Assert.Equal<string list>([ "src/Core/Logic.fs"; "src/Core/More.fs" ], got)

[<Fact>]
let ``ignore subtracts after focus`` () =
    let items =
        [
            d "src/Core/Logic.fs" "PURE002" "A"
            d "src/Core/Generated.fs" "PURE002" "B"
        ]

    let got = Filter.apply [ "src/Core" ] [ "src/Core/Generated.fs" ] items |> List.map _.File
    Assert.Equal<string list>([ "src/Core/Logic.fs" ], got)

[<Fact>]
let ``glob focus matches nested files`` () =
    let items =
        [
            d "src/Core/A.fs" "PURE002" "A"
            d "src/Web/B.fs" "PURE002" "B"
        ]

    let got = Filter.apply [ "src/Core/**/*.fs" ] [] items |> List.map _.File
    Assert.Equal<string list>([ "src/Core/A.fs" ], got)

[<Fact>]
let ``empty focus keeps everything until ignore`` () =
    let items = [ d "a.fs" "PURE002" "A"; d "b.fs" "PURE003" "B" ]
    let got = Filter.apply [] [ "b.fs" ] items |> List.map _.File
    Assert.Equal<string list>([ "a.fs" ], got)

[<Fact>]
let ``directory prefix does not match a similarly named file`` () =
    Assert.False(Filter.matches "src/Core" "src/Core.fs")
    Assert.True(Filter.matches "src/Core" "src/Core/X.fs")
    Assert.True(Filter.matches "src/Core/X.fs" "src/Core/X.fs")
