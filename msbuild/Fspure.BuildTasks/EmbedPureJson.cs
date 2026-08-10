using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Fspure.BuildTasks;

/// <summary>
/// Injects (or replaces) an embedded pure.json resource into an already-built managed assembly.
/// Used after fspure-collector runs against $(TargetPath).
/// </summary>
public sealed class EmbedPureJson : Microsoft.Build.Utilities.Task
{
    /// <summary>Path to the built assembly (DLL/EXE) to mutate in place.</summary>
    [Required]
    public string AssemblyPath { get; set; } = "";

    /// <summary>Path to the .pure.json file to embed.</summary>
    [Required]
    public string PureJsonPath { get; set; } = "";

    /// <summary>
    /// Manifest resource name. Convention: <c>{AssemblyName}.pure.json</c>.
    /// </summary>
    [Required]
    public string ResourceName { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AssemblyPath) || !File.Exists(AssemblyPath))
            {
                Log.LogError("Fspure EmbedPureJson: assembly not found: {0}", AssemblyPath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(PureJsonPath) || !File.Exists(PureJsonPath))
            {
                Log.LogError("Fspure EmbedPureJson: pure.json not found: {0}", PureJsonPath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(ResourceName))
            {
                Log.LogError("Fspure EmbedPureJson: ResourceName is required.");
                return false;
            }

            var jsonBytes = File.ReadAllBytes(PureJsonPath);
            var assemblyFull = Path.GetFullPath(AssemblyPath);
            var directory = Path.GetDirectoryName(assemblyFull) ?? ".";

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(directory);
            AddCommonSearchPaths(resolver);

            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadWrite = false,
                InMemory = true,
                ReadingMode = ReadingMode.Immediate,
            };

            using var module = ModuleDefinition.ReadModule(assemblyFull, readerParameters);

            // Remove any prior pure.json resource with the same logical name.
            for (var i = module.Resources.Count - 1; i >= 0; i--)
            {
                if (string.Equals(module.Resources[i].Name, ResourceName, StringComparison.Ordinal))
                {
                    module.Resources.RemoveAt(i);
                }
            }

            module.Resources.Add(
                new EmbeddedResource(ResourceName, ManifestResourceAttributes.Public, jsonBytes));

            var tempPath = assemblyFull + ".fspure-tmp";
            try
            {
                module.Write(tempPath);
                File.Copy(tempPath, assemblyFull, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                }
            }

            Log.LogMessage(
                MessageImportance.Low,
                "Fspure: embedded resource '{0}' ({1} bytes) into {2}",
                ResourceName,
                jsonBytes.Length,
                assemblyFull);

            return true;
        }
        catch (Exception ex)
        {
            Log.LogError("Fspure EmbedPureJson failed: {0}", ex.Message);
            Log.LogMessage(MessageImportance.Low, ex.ToString());
            return false;
        }
    }

    private static void AddCommonSearchPaths(DefaultAssemblyResolver resolver)
    {
        // NuGet global packages (FSharp.Core, etc.)
        var nuget =
            Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages");

        TryAddPackageLibs(resolver, nuget, "fsharp.core");

        // .NET shared framework (System.* facades)
        var dotnetRoot =
            Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? Path.GetDirectoryName(Environment.ProcessPath)
            ?? "/usr/share/dotnet";

        var shared = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
        if (Directory.Exists(shared))
        {
            foreach (var ver in Directory.GetDirectories(shared).OrderByDescending(d => d))
            {
                resolver.AddSearchDirectory(ver);
                break;
            }
        }
    }

    private static void TryAddPackageLibs(DefaultAssemblyResolver resolver, string nugetRoot, string packageId)
    {
        try
        {
            var pkg = Path.Combine(nugetRoot, packageId);
            if (!Directory.Exists(pkg))
            {
                return;
            }

            foreach (var verDir in Directory.GetDirectories(pkg).OrderByDescending(d => d))
            {
                var lib = Path.Combine(verDir, "lib");
                if (!Directory.Exists(lib))
                {
                    continue;
                }

                foreach (var tfm in Directory.GetDirectories(lib))
                {
                    resolver.AddSearchDirectory(tfm);
                }
            }
        }
        catch
        {
            // Best-effort only.
        }
    }
}
