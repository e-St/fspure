module FSharp.PureSchema.Tests.OverridesTests

open FSharp.PureSchema
open Xunit

[<Fact>]
let ``parse add and remove methods`` () =
    let json =
        """
        {
          "schemaVersion": "1.0",
          "useFoundational": true,
          "add": [ "Lib.A.Pure", "Lib.B.Pure" ],
          "remove": [ "Microsoft.FSharp.Core.Operators.op_PipeRight" ]
        }
        """

    match PureOverridesIO.parse json with
    | Error e -> Assert.Fail(string e)
    | Ok ov ->
        Assert.True(ov.UseFoundational)
        Assert.Equal<string list>([ "Lib.A.Pure"; "Lib.B.Pure" ], ov.Add)
        Assert.Equal<string list>(
            [ "Microsoft.FSharp.Core.Operators.op_PipeRight" ],
            ov.Remove
        )
        Assert.False(System.String.IsNullOrWhiteSpace ov.ContentHash)

[<Fact>]
let ``parse useFoundational false`` () =
    let json =
        """
        { "schemaVersion": "1.0", "useFoundational": false, "add": [], "remove": [] }
        """

    match PureOverridesIO.parse json with
    | Error e -> Assert.Fail(string e)
    | Ok ov -> Assert.False(ov.UseFoundational)

[<Fact>]
let ``default useFoundational is true when omitted`` () =
    let json = """{ "schemaVersion": "1.0", "add": [ "X.Y" ] }"""

    match PureOverridesIO.parse json with
    | Error e -> Assert.Fail(string e)
    | Ok ov ->
        Assert.True(ov.UseFoundational)
        Assert.Equal<string list>([ "X.Y" ], ov.Add)

[<Fact>]
let ``reject unknown schema version`` () =
    let json = """{ "schemaVersion": "9.9", "add": [] }"""

    match PureOverridesIO.parse json with
    | Ok _ -> Assert.Fail("expected error")
    | Error(PureOverridesError.UnsupportedSchemaVersion v) -> Assert.Equal("9.9", v)
    | Error e -> Assert.Fail($"unexpected {e}")

[<Fact>]
let ``reject missing schemaVersion`` () =
    match PureOverridesIO.parse """{ "add": [] }""" with
    | Ok _ -> Assert.Fail("expected error")
    | Error(PureOverridesError.MissingRequiredField name) -> Assert.Equal("schemaVersion", name)
    | Error e -> Assert.Fail($"unexpected {e}")

[<Fact>]
let ``isOverridesFileName matches conventional name`` () =
    Assert.True(PureOverridesIO.isOverridesFileName "fspure.overrides.json")
    Assert.True(PureOverridesIO.isOverridesFileName "/proj/fspure.overrides.json")
    Assert.False(PureOverridesIO.isOverridesFileName "pure-extra.json")
