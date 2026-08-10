# fspure Nushell helpers — interactive orchestration, no product logic.
# Logic lives in F# tools (DocsGenerator, DevcontainerGen, …).
#
# Usage (inside flake / direnv env):
#   nu
#   use src/scripts/fspure.nu *
#   fspure docs preview
#   fspure devcontainer
#   fspure info
#
# Or without importing:
#   nu src/scripts/fspure.nu docs preview

def --env "fspure root" [] {
  mut d = $env.PWD
  loop {
    if ($d | path join "fspure.slnx" | path exists) {
      return $d
    }
    let parent = $d | path dirname
    if $parent == $d { error make {msg: "fspure.slnx not found (not in monorepo?)"} }
    $d = $parent
  }
}

def "fspure docs" [mode: string = "preview", arg?: string] {
  let root = (fspure root)
  cd $root
  let args = if $arg == null { [$mode] } else { [$mode, $arg] }
  ^dotnet run --project src/DocsGenerator/DocsGenerator.fsproj -c ($env.CONFIGURATION? | default "Release") -- ...$args
}

def "fspure devcontainer" [...rest: string] {
  let root = (fspure root)
  cd $root
  ^dotnet run --project src/DevcontainerGen/DevcontainerGen.fsproj -c ($env.CONFIGURATION? | default "Release") -- ...$rest
}

def "fspure info" [] {
  print $"fspure monorepo: (fspure root)"
  print $"  dotnet: (^dotnet --version | str trim)"
  if (which node | is-not-empty) {
    print $"  node:   (^node --version | str trim)"
  }
  print "  Prefer: nix run .#docs -- preview"
  print "          nix run .#devcontainer"
  print "          fspure-docs / fspure-devcontainer (on PATH in flake shell)"
}

# Allow: nu src/scripts/fspure.nu docs preview
def main [cmd?: string, mode?: string, arg?: string] {
  match $cmd {
    null | "info" => { fspure info }
    "docs" => { fspure docs ($mode | default "preview") $arg }
    "devcontainer" => {
      if $mode == null {
        fspure devcontainer
      } else if $arg == null {
        fspure devcontainer $mode
      } else {
        fspure devcontainer $mode $arg
      }
    }
    _ => {
      print "Usage: fspure [docs|devcontainer|info] …"
      print "  docs preview|stable [arg]"
      print "  devcontainer [--check]"
    }
  }
}
