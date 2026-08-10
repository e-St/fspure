{
  description = "fspure — F# purity tooling monorepo (logic in F#; flake only for .NET SDK + thin wrappers)";

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

        dotnet =
          pkgs.dotnetCorePackages.sdk_10_0 or pkgs.dotnetCorePackages.sdk_9_0 or pkgs.dotnet-sdk;

        # Absolute minimum shell: find monorepo root, exec F# via dotnet run.
        mkDotnetTool =
          {
            name,
            project,
            description ? "",
            extraArgs ? "",
          }:
          pkgs.writeShellApplication {
            inherit name;
            runtimeInputs = [
              dotnet
              pkgs.git
            ];
            text = ''
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
              exec ${dotnet}/bin/dotnet run --project "${project}" -c "''${CONFIGURATION:-Release}" -- ${extraArgs} "$@"
            '';
            meta = {
              inherit description;
              mainProgram = name;
            };
          };

        tasks = "src/Fspure.Tasks/Fspure.Tasks.fsproj";

        fspure = mkDotnetTool {
          name = "fspure";
          project = tasks;
          description = "F# monorepo task runner (build/test/docs/security/gates)";
        };

        fspure-docs = mkDotnetTool {
          name = "fspure-docs";
          project = "src/DocsGenerator/DocsGenerator.fsproj";
          description = "Docs site / Markdown → .generated/ (F# + Scriban)";
        };

        fspure-devcontainer = mkDotnetTool {
          name = "fspure-devcontainer";
          project = "src/DevcontainerGen/DevcontainerGen.fsproj";
          description = "Merge devcontainer fragments (F#)";
        };

        fspure-security = mkDotnetTool {
          name = "fspure-security";
          project = tasks;
          extraArgs = "security";
          description = "NuGet vulnerable + npm audit (F#)";
        };

        fspure-ready-lib-gate = mkDotnetTool {
          name = "fspure-ready-lib-gate";
          project = tasks;
          extraArgs = "ready-lib-gate";
          description = "ReadyLib local-feed e2e gate (F#)";
        };

        fspure-phase5 = mkDotnetTool {
          name = "fspure-phase5";
          project = tasks;
          extraArgs = "phase5";
          description = "Phase 5 regression net (F#)";
        };

        app =
          pkg: bin: {
            type = "app";
            program = "${pkg}/bin/${bin}";
          };

      in
      {
        formatter = pkgs.nixfmt-rfc-style;

        packages = {
          default = fspure;
          fspure = fspure;
          docs = fspure-docs;
          devcontainer = fspure-devcontainer;
          security = fspure-security;
          ready-lib-gate = fspure-ready-lib-gate;
          phase5 = fspure-phase5;
        };

        apps = {
          default = app fspure "fspure";
          fspure = app fspure "fspure";
          docs = app fspure-docs "fspure-docs";
          devcontainer = app fspure-devcontainer "fspure-devcontainer";
          security = app fspure-security "fspure-security";
          ready-lib-gate = app fspure-ready-lib-gate "fspure-ready-lib-gate";
          phase5 = app fspure-phase5 "fspure-phase5";
        };

        devShells.default = pkgs.mkShell {
          name = "fspure";
          packages = with pkgs; [
            dotnet
            nodejs_22
            git
            jq
            curl
            cacert
            unzip
            direnv
            nix-direnv
            nixfmt-rfc-style
            fspure
            fspure-docs
            fspure-devcontainer
            fspure-security
            fspure-ready-lib-gate
            fspure-phase5
          ];

          shellHook = ''
            export DOTNET_ROOT="${dotnet}"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            export PATH="$HOME/.dotnet/tools:$PATH"
            if [ -n "''${PS1-}" ] && [ -z "''${FSPURE_QUIET-}" ]; then
              echo "fspure: fspure | fspure-docs | fspure-security | fspure-ready-lib-gate | fspure-phase5"
            fi
          '';
        };
      }
    );
}
