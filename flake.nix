{
  description = "fspure — F# purity tooling monorepo (flakes + direnv + Nushell; logic in F#)";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs { inherit system; };

        # Prefer .NET 10 when present in nixpkgs; fall back gracefully.
        dotnet =
          pkgs.dotnetCorePackages.sdk_10_0 or pkgs.dotnetCorePackages.sdk_9_0 or pkgs.dotnet-sdk;

        # ------------------------------------------------------------------
        # Thin writeShellApplication wrappers only.
        # All real logic lives in F# tools under src/ (DocsGenerator, DevcontainerGen, …).
        # These wrappers exist so `nix run` / apps work without a hand-written bash script
        # tree. Keep the `text` blocks tiny — no branching product logic here.
        # ------------------------------------------------------------------

        # Locate monorepo root (git or walk) then exec a F# project with `dotnet run`.
        # $1… are forwarded to the tool. First argument after derivation build is unused.
        mkDotnetTool =
          {
            name,
            project, # path relative to monorepo root
            description ? "",
          }:
          pkgs.writeShellApplication {
            inherit name;
            runtimeInputs = [
              dotnet
              pkgs.git
            ];
            text = ''
              # shellcheck disable=SC2164
              root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
              if [ -z "$root" ]; then
                d="$PWD"
                while [ "$d" != "/" ]; do
                  if [ -f "$d/fspure.slnx" ]; then root="$d"; break; fi
                  d="$(dirname "$d")"
                done
              fi
              if [ -z "''${root:-}" ] || [ ! -f "$root/fspure.slnx" ]; then
                echo "fspure: monorepo root (fspure.slnx) not found" >&2
                exit 1
              fi
              cd "$root"
              export DOTNET_ROOT="${dotnet}"
              export DOTNET_CLI_TELEMETRY_OPTOUT=1
              export DOTNET_NOLOGO=1
              exec ${dotnet}/bin/dotnet run --project "${project}" -c "''${CONFIGURATION:-Release}" -- "$@"
            '';
            meta = {
              inherit description;
              mainProgram = name;
            };
          };

        fspure-docs = mkDotnetTool {
          name = "fspure-docs";
          project = "src/DocsGenerator/DocsGenerator.fsproj";
          description = "Generate docs site / Markdown into .generated/ (F# + Scriban)";
        };

        fspure-devcontainer = mkDotnetTool {
          name = "fspure-devcontainer";
          project = "src/DevcontainerGen/DevcontainerGen.fsproj";
          description = "Merge src/devcontainer/fragments → .generated/devcontainer/";
        };

        # One-shot environment banner (opt-in). Not run from shellHook so direnv stays quiet.
        fspure-info = pkgs.writeShellApplication {
          name = "fspure-info";
          runtimeInputs = [
            dotnet
            pkgs.nodejs_22
          ];
          text = ''
            echo "fspure nix env"
            echo "  dotnet: $(${dotnet}/bin/dotnet --version 2>/dev/null || echo '?')"
            echo "  node:   $(${pkgs.nodejs_22}/bin/node --version 2>/dev/null || echo '?')"
            echo "  apps:   nix run .#docs -- preview"
            echo "          nix run .#devcontainer"
            echo "          nix run .#info"
            echo "  nu:     nu  # then: use src/scripts/fspure.nu"
          '';
        };

      in
      {
        formatter = pkgs.nixfmt-rfc-style;

        packages = {
          default = fspure-docs;
          docs = fspure-docs;
          devcontainer = fspure-devcontainer;
          info = fspure-info;
        };

        apps = {
          default = {
            type = "app";
            program = "${fspure-docs}/bin/fspure-docs";
          };
          docs = {
            type = "app";
            program = "${fspure-docs}/bin/fspure-docs";
          };
          devcontainer = {
            type = "app";
            program = "${fspure-devcontainer}/bin/fspure-devcontainer";
          };
          info = {
            type = "app";
            program = "${fspure-info}/bin/fspure-info";
          };
        };

        # direnv + nix-direnv load this automatically via `use flake` in .envrc.
        # Interactive shell preference: Nushell (available on PATH; not forced as $SHELL
        # so CI / non-interactive callers keep working).
        devShells.default = pkgs.mkShell {
          name = "fspure";
          packages = with pkgs; [
            dotnet
            nodejs_22
            git
            jq
            curl
            cacert
            nushell
            direnv
            nix-direnv
            nixfmt-rfc-style
            # Exposed as commands inside the shell (same thin wrappers as apps).
            fspure-docs
            fspure-devcontainer
            fspure-info
          ];

          # Keep shellHook to pure env exports — no echo spam (direnv reloads often).
          shellHook = ''
            export DOTNET_ROOT="${dotnet}"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            export PATH="$HOME/.dotnet/tools:$PATH"
            # Hint for interactive shells only (skip under direnv reload noise when possible).
            if [ -n "''${PS1-}" ] && [ -z "''${FSPURE_QUIET-}" ]; then
              echo "fspure: fspure-docs | fspure-devcontainer | fspure-info | nu"
            fi
          '';
        };
      }
    );
}
