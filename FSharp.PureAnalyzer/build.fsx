#r "nuget: Fun.Build"

open Fun.Build

pipeline "Build" {
    stage "Clean" { run "dotnet clean" }
    stage "Restore" { run "dotnet restore" }
    stage "Build" { run "dotnet build" }
    runIfOnlySpecified false
}

pipeline "Run" {
    stage "Run" { run "dotnet run" }
    runIfOnlySpecified true
}

pipeline "Watch" {
    stage "Watch" { run "dotnet watch run" }
    runIfOnlySpecified true
}

pipeline "Pack" {
    stage "Pack" { run "dotnet pack -c Release -o ./nupkgs" }
    runIfOnlySpecified true
}

tryPrintPipelineCommandHelp ()
