{
  description = "fspure — F# pure/impure analyzer monorepo (dev shell via Nix)";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs { inherit system; };
        # .NET 10 SDK when available in nixpkgs; fall back to latest 9/10 channel name.
        dotnet = pkgs.dotnetCorePackages.sdk_9_0 or pkgs.dotnet-sdk;
      in {
        devShells.default = pkgs.mkShell {
          name = "fspure";
          packages = with pkgs; [
            dotnet
            nodejs_22
            git
            jq
            curl
            cacert
            # Optional local helpers (not required for CI)
            # nixfmt-rfc-style
          ];

          shellHook = ''
            export DOTNET_ROOT="${dotnet}"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            export PATH="$HOME/.dotnet/tools:$PATH"
            echo "fspure nix shell: dotnet=$(dotnet --version 2>/dev/null || echo '?') node=$(node --version 2>/dev/null || echo '?')"
            echo "  Restore tools:  dotnet tool restore   (repo root)"
            echo "  Devcontainers:  dotnet run --project src/DevcontainerGen"
            echo "  Docs:           bash scripts/docs-generate.sh preview"
          '';
        };
      });
}
