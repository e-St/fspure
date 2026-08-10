/// Phase 2 visual capture (F# + Playwright.NET). Replaces screenshot.mjs.
module Fspure.E2E.Phase2.Program

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.Playwright

let private env name defaultValue =
    match Environment.GetEnvironmentVariable name with
    | null
    | "" -> defaultValue
    | v -> v

let private log (msg: string) = printfn "[phase2-screenshot] %s" msg

let private baseUrl = env "CODE_SERVER_URL" "http://127.0.0.1:8080"

let private artifactsDir =
    env "ARTIFACTS_DIR" (Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "..", ".artifacts", "phase2")))

let private waitMs =
    match Int32.TryParse(env "WAIT_MS" "180000") with
    | true, n -> n
    | _ -> 180_000

let private solutionName = env "SOLUTION_NAME" "customer-fixture.slnx"
let private filePath = "Program.fs"

let private delay (ms: int) = Task.Delay(ms)

let private dismissNoise (page: IPage) =
    task {
        for _ in 1..6 do
            try
                do! page.Keyboard.PressAsync "Escape"
            with _ ->
                ()

            do! delay 250

        let closers =
            page.Locator(
                String.Join(
                    ", ",
                    [
                        ".notification-toast .codicon-notifications-clear"
                        ".notification-list-item-toolbar-container .codicon-close"
                        ".monaco-dialog-box .dialog-buttons .monaco-button"
                        ".welcome-view .button-container a"
                    ]
                )
            )

        let! count = closers.CountAsync()

        for i in 0 .. (min (count - 1) 7) do
            try
                do! closers.Nth(i).ClickAsync(LocatorClickOptions(Timeout = System.Nullable(800.0f)))
            with _ ->
                ()
    }

let private runCommand (page: IPage) (query: string) =
    task {
        do! page.Keyboard.PressAsync "Control+Shift+P"
        do! delay 600

        try
            do! page.Keyboard.PressAsync "Control+A"
        with _ ->
            ()

        do! page.Keyboard.TypeAsync(query, KeyboardTypeOptions(Delay = System.Nullable(25.0f)))
        do! delay 900
        do! page.Keyboard.PressAsync "Enter"
        do! delay 1200
    }

let private pickQuickOpen (page: IPage) (filter: string) =
    task {
        do! delay 500

        try
            do! page.Keyboard.PressAsync "Control+A"
        with _ ->
            ()

        do! page.Keyboard.TypeAsync(filter, KeyboardTypeOptions(Delay = System.Nullable(30.0f)))
        do! delay 900
        do! page.Keyboard.PressAsync "Enter"
        do! delay 1500
    }

let private loadSolution (page: IPage) =
    task {
        log $"loading Ionide workspace/solution: {solutionName}"

        let commands =
            [
                "F#: Change Workspace or Solution"
                "F# Change Workspace or Solution"
                "Ionide: Change Workspace or Solution"
            ]

        let mutable done' = false

        for cmd in commands do
            if not done' then
                do! dismissNoise page
                do! runCommand page cmd

                let picker =
                    page.Locator(".quick-input-widget:visible, .monaco-list:visible .monaco-list-row")

                let! visible =
                    task {
                        try
                            return! picker.First.IsVisibleAsync()
                        with _ ->
                            return false
                    }

                if visible then
                    let filter = solutionName.Replace(".slnx", "")
                    do! pickQuickOpen page filter
                    log $"selected solution via command: {cmd}"
                    do! delay 4000
                    done' <- true
                else
                    try
                        do! page.Keyboard.PressAsync "Escape"
                    with _ ->
                        ()

        if not done' then
            log $"command palette workspace change unavailable; quick-opening {solutionName}"
            do! page.Keyboard.PressAsync "Control+P"
            do! delay 600
            do! pickQuickOpen page solutionName
            do! delay 3000
    }

let private openProgramFs (page: IPage) =
    task {
        do! dismissNoise page
        do! page.Keyboard.PressAsync "Control+P"
        do! delay 700
        do! pickQuickOpen page filePath

        let editor = page.Locator(".monaco-editor").First
        do! editor.WaitForAsync(LocatorWaitForOptions(State = WaitForSelectorState.Visible, Timeout = System.Nullable(90_000.0f)))

        try
            do! editor.ClickAsync(LocatorClickOptions(Timeout = System.Nullable(15_000.0f)))
        with _ ->
            ()

        log $"editor visible for {filePath}"

        do! runCommand page "Change Language Mode"

        let! langPicker =
            task {
                try
                    return! page.Locator(".quick-input-widget:visible").First.IsVisibleAsync()
                with _ ->
                    return false
            }

        if langPicker then
            do! pickQuickOpen page "F#"
            log "set language mode to F#"
        else
            try
                do! page.Keyboard.PressAsync "Escape"
            with _ ->
                ()

        try
            do! editor.ClickAsync(LocatorClickOptions(Timeout = System.Nullable(5000.0f)))
        with _ ->
            ()

        do! delay 8000
    }

/// Browser-side probe (same logic as former screenshot.mjs evaluate block).
let private probeScript =
    """() => {
    const editors = [...document.querySelectorAll(".monaco-editor")];
    const viewRoots = editors.flatMap((ed) => [
      ed.querySelector(".view-lines"),
      ed,
    ]).filter(Boolean);
    let editorText = "";
    let editorHtml = "";
    for (const root of viewRoots) {
      editorText += `\n${root.innerText || root.textContent || ""}`;
      editorHtml += `\n${root.innerHTML || ""}`;
    }
    const bodyText = document.body?.innerText || "";
    const word = (w, text) =>
      new RegExp(`(^|[^a-zA-Z])${w}([^a-zA-Z]|$)`, "m").test(text || "");
    let pseudoHitImpure = false;
    let pseudoHitPure = false;
    for (const root of editors) {
      const nodes = root.querySelectorAll("*");
      for (const n of nodes) {
        for (const pseudo of [":before", ":after"]) {
          const c = getComputedStyle(n, pseudo).content || "";
          if (/impure/i.test(c)) pseudoHitImpure = true;
          if (/(^|[^a-z])pure([^a-z]|$)/i.test(c.replace(/^["']|["']$/g, ""))) {
            if (!/impure/i.test(c)) pseudoHitPure = true;
          }
        }
      }
    }
    const sawImpure =
      word("impure", editorText) || word("impure", editorHtml) || word("impure", bodyText) || pseudoHitImpure;
    const sawPure =
      word("pure", editorText) || word("pure", editorHtml) || pseudoHitPure;
    const inlayNodes = document.querySelectorAll(
      [
        ".monaco-editor .codicon-symbol-parameter",
        ".monaco-editor .ghost-text-decoration",
        ".monaco-editor .ghost-text",
        ".monaco-editor [class*='inlayHint']",
        ".monaco-editor [class*='inlay-hint']",
        ".monaco-editor [class*='InlayHint']",
        ".monaco-editor span[class*='inline-injected']",
      ].join(",")
    );
    const typeAnnoHits = (
      editorText.match(/:\\s*(int|string|bool|unit|list|float|decimal|obj|DateTime)\\b/gi) || []
    ).length;
    const statusBar =
      document.querySelector("#workbench\\\\.parts\\\\.statusbar")?.textContent ||
      document.querySelector(".statusbar")?.textContent ||
      "";
    return {
      sawImpure,
      sawPure,
      sawInlayish: inlayNodes.length > 0 || typeAnnoHits > 0,
      sawAnalyzerHint: /PURE00[123]|Pure analyzer/i.test(bodyText),
      solutionLoaded: /customer-fixture|Ionide|FSAC|F#/i.test(statusBar),
      inlayCount: inlayNodes.length,
      typeAnnoHits,
      statusBar: statusBar.replace(/\\s+/g, " ").slice(0, 160),
      editorSnippet: editorText.replace(/\\s+/g, " ").slice(0, 500),
    };
  }"""

type Probe =
    {
        SawImpure: bool
        SawPure: bool
        SawInlayish: bool
        SawAnalyzerHint: bool
        SolutionLoaded: bool
        InlayCount: int
        TypeAnnoHits: int
        StatusBar: string
        EditorSnippet: string
        ImpureCount: int
        PureCount: int
    }

let private probeUi (page: IPage) =
    task {
        let! json = page.EvaluateAsync(probeScript)
        let el = json.Value
        // Playwright returns JsonElement via GetProperty for object results
        let getBool (name: string) =
            try
                el.GetProperty(name).GetBoolean()
            with _ ->
                false

        let getInt (name: string) =
            try
                el.GetProperty(name).GetInt32()
            with _ ->
                0

        let getStr (name: string) =
            try
                el.GetProperty(name).GetString() |> Option.ofObj |> Option.defaultValue ""
            with _ ->
                ""

        let impureLoc = page.Locator(".monaco-editor").GetByText("impure", LocatorGetByTextOptions(Exact = true))
        let pureLoc = page.Locator(".monaco-editor").GetByText("pure", LocatorGetByTextOptions(Exact = true))
        let! impureCount = impureLoc.CountAsync()
        let! pureCount = pureLoc.CountAsync()

        let sawImpure = getBool "sawImpure" || impureCount > 0
        let sawPure = getBool "sawPure" || pureCount > 0

        return
            {
                SawImpure = sawImpure
                SawPure = sawPure
                SawInlayish = getBool "sawInlayish"
                SawAnalyzerHint = getBool "sawAnalyzerHint"
                SolutionLoaded = getBool "solutionLoaded"
                InlayCount = getInt "inlayCount"
                TypeAnnoHits = getInt "typeAnnoHits"
                StatusBar = getStr "statusBar"
                EditorSnippet = getStr "editorSnippet"
                ImpureCount = impureCount
                PureCount = pureCount
            }
    }

let private findInEditor (page: IPage) (query: string) =
    task {
        try
            do! page.Keyboard.PressAsync "Escape"
        with _ ->
            ()

        do! delay 200

        try
            do! page.Keyboard.PressAsync "Control+F"
        with _ ->
            ()

        do! delay 500

        try
            do! page.Keyboard.PressAsync "Control+A"
        with _ ->
            ()

        do! page.Keyboard.TypeAsync(query, KeyboardTypeOptions(Delay = System.Nullable(25.0f)))
        do! delay 700

        try
            do! page.Keyboard.PressAsync "Enter"
        with _ ->
            ()

        do! delay 600

        try
            do! page.Keyboard.PressAsync "Escape"
        with _ ->
            ()

        do! delay 400
    }

let private nudgeIonide (page: IPage) (nudge: int) =
    task {
        if nudge = 8 then
            let! probe = probeUi page

            if not probe.SawInlayish && not probe.SawImpure then
                log $"nudge: re-assert Ionide workspace → {solutionName}"
                do! loadSolution page
                do! openProgramFs page
        elif nudge % 5 = 1 then
            try
                do! page.Keyboard.PressAsync "Control+S"
            with _ ->
                ()
        elif nudge % 5 = 2 then
            for key in [ "Control+Home"; "Control+End"; "Control+Home" ] do
                try
                    do! page.Keyboard.PressAsync key
                with _ ->
                    ()

                do! delay 150
        elif nudge % 5 = 3 then
            try
                do! page.Keyboard.PressAsync "Control+Shift+M"
            with _ ->
                ()

            do! delay 400
        elif nudge % 5 = 4 then
            let editor = page.Locator(".monaco-editor").First

            try
                do! editor.ClickAsync(LocatorClickOptions(Timeout = System.Nullable(3000.0f)))
                do! page.Keyboard.TypeAsync(" ", KeyboardTypeOptions(Delay = System.Nullable(20.0f)))
                do! page.Keyboard.PressAsync "Control+Z"
            with _ ->
                ()
    }

type BadgeState =
    {
        SawImpure: bool
        SawPure: bool
        SawInlayish: bool
        TimedOut: bool
    }

let private waitForBadges (page: IPage) =
    task {
        let deadline = DateTime.UtcNow.AddMilliseconds(float waitMs)
        let mutable sawImpure = false
        let mutable sawPure = false
        let mutable sawInlayish = false
        let mutable lastLog = DateTime.MinValue
        let mutable nudge = 0
        let mutable finished = false
        let mutable result =
            {
                SawImpure = false
                SawPure = false
                SawInlayish = false
                TimedOut = true
            }

        while DateTime.UtcNow < deadline && not finished do
            let! probe = probeUi page
            sawImpure <- sawImpure || probe.SawImpure
            sawPure <- sawPure || probe.SawPure
            sawInlayish <- sawInlayish || probe.SawInlayish

            if (DateTime.UtcNow - lastLog).TotalMilliseconds > 15_000.0 then
                log
                    $"still waiting… impure={sawImpure} pure={sawPure} inlay={sawInlayish} remainingMs={(deadline - DateTime.UtcNow).TotalMilliseconds}"

                lastLog <- DateTime.UtcNow

            if sawImpure && sawPure then
                log "found both pure and impure labels"
                do! delay 2000

                result <-
                    {
                        SawImpure = true
                        SawPure = true
                        SawInlayish = sawInlayish
                        TimedOut = false
                    }

                finished <- true
            else
                if sawImpure && not sawPure && nudge > 2 && nudge % 3 = 0 then
                    do! findInEditor page "let add a b"

                nudge <- nudge + 1
                do! nudgeIonide page nudge
                do! delay 3000

        if not finished then
            log $"timeout waiting for badges impure={sawImpure} pure={sawPure}"

            result <-
                {
                    SawImpure = sawImpure
                    SawPure = sawPure
                    SawInlayish = sawInlayish
                    TimedOut = true
                }

        return result
    }

let private revealPureHelpersSection (page: IPage) =
    task {
        log "revealing pure helpers section (add / isEmpty / myEmpty)"
        do! findInEditor page "let add a b"

        for _ in 1..3 do
            try
                do! page.Keyboard.PressAsync "ArrowUp"
            with _ ->
                ()

        do! delay 400
        return ()
    }

let private capture (page: IPage) (badgeState: BadgeState) =
    task {
        let editor = page.Locator(".monaco-editor").First
        do! editor.WaitForAsync(LocatorWaitForOptions(State = WaitForSelectorState.Visible))

        try
            do! page.Keyboard.PressAsync "Control+Home"
        with _ ->
            ()

        do! delay 800

        let shots =
            {|
                Full = Path.Combine(artifactsDir, "program-fs-full.png")
                Editor = Path.Combine(artifactsDir, "program-fs-editor.png")
                ImpureSection = Path.Combine(artifactsDir, "program-fs-impure-section.png")
                PureSection = Path.Combine(artifactsDir, "program-fs-pure-section.png")
            |}

        let shotEditor (path: string) =
            task {
                try
                    let! _ = editor.ScreenshotAsync(LocatorScreenshotOptions(Path = path))
                    return ()
                with _ ->
                    let! _ = page.ScreenshotAsync(PageScreenshotOptions(Path = path))
                    return ()
            }

        let! _ = page.ScreenshotAsync(PageScreenshotOptions(Path = shots.Full, FullPage = true))
        log $"wrote {shots.Full}"

        do! shotEditor shots.ImpureSection
        log $"wrote {shots.ImpureSection}"

        do! shotEditor shots.Editor
        log $"wrote {shots.Editor}"

        do! revealPureHelpersSection page
        do! delay 800

        do! shotEditor shots.PureSection
        log $"wrote {shots.PureSection}"

        let! finalProbe = probeUi page

        let meta =
            {|
                capturedAt = DateTime.UtcNow.ToString("o")
                codeServerUrl = baseUrl
                file = filePath
                solution = solutionName
                badges = badgeState
                probe = finalProbe
                screenshots =
                    [
                        Path.GetFileName shots.Full
                        Path.GetFileName shots.Editor
                        Path.GetFileName shots.ImpureSection
                        Path.GetFileName shots.PureSection
                    ]
            |}

        let opts = JsonSerializerOptions(WriteIndented = true)
        let json = JsonSerializer.Serialize(meta, opts)
        File.WriteAllText(Path.Combine(artifactsDir, "screenshot-meta.json"), json + "\n")
        return meta
    }

let private run () =
    task {
        Directory.CreateDirectory artifactsDir |> ignore
        log $"connecting to {baseUrl}"
        log $"artifacts → {artifactsDir}"
        log $"solution → {solutionName}"

        use! playwright = Playwright.CreateAsync()

        let! browser =
            playwright.Chromium.LaunchAsync(
                BrowserTypeLaunchOptions(Headless = true, Args = [| "--no-sandbox"; "--disable-dev-shm-usage" |])
            )

        let! context =
            browser.NewContextAsync(
                BrowserNewContextOptions(ViewportSize = ViewportSize(Width = 1440, Height = 1100), DeviceScaleFactor = 1.0f)
            )

        let! page = context.NewPageAsync()
        page.SetDefaultTimeout(60_000f)

        try
            let! __ = page.GotoAsync(baseUrl, PageGotoOptions(WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = System.Nullable(60_000.0f)))
            do! delay 3000
            do! dismissNoise page
            do! loadSolution page
            do! openProgramFs page
            log $"waiting for Ionide / analyzer / decorations (up to {waitMs} ms)"
            let! badgeState = waitForBadges page
            let! _meta = capture page badgeState

            if badgeState.TimedOut || not badgeState.SawImpure || not badgeState.SawPure then
                eprintfn "Phase 2: did not observe both pure and impure labels in the editor UI. %A" badgeState
                eprintfn "Screenshots were still saved under %s" artifactsDir
                return 1
            else
                log "Phase 2 visual capture OK"
                return 0
        finally
            try
                page
                    .ScreenshotAsync(
                        PageScreenshotOptions(
                            Path = Path.Combine(artifactsDir, "program-fs-final-state.png"),
                            FullPage = true
                        )
                    )
                    .GetAwaiter()
                    .GetResult()
                |> ignore
            with _ ->
                ()

            browser.CloseAsync().GetAwaiter().GetResult()
    }

[<EntryPoint>]
let main _argv =
    try
        run().GetAwaiter().GetResult()
    with ex ->
        eprintfn "%O" ex
        1
