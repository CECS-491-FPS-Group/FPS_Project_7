using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class BuildIntegrityManifestTool
{
    private static bool _busy;

    [MenuItem("Tools/Anti-Cheat/Generate Windows Mono Integrity Manifest")]
    private static async void Generate()
    {
        if (_busy) return;
        string exe = EditorUtility.OpenFilePanel("Select completed known-good Windows game executable", "", "exe");
        if (string.IsNullOrEmpty(exe)) return;
        string root = BuildIntegrity.DataRootForBuild(exe, "Windows");
        _busy = true;
        try
        {
            if (File.Exists(Path.Combine(Path.GetDirectoryName(exe), "GameAssembly.dll")))
                throw new InvalidDataException("IL2CPP is not supported by schema 1. Select a Windows Mono build.");
            var manifest = new BuildIntegrity.Manifest {
                schema = BuildIntegrity.Schema, buildId = Guid.NewGuid().ToString("N"), platform = "Windows",
                backend = "Mono", createdUtc = DateTime.UtcNow.ToString("O"), files = new BuildIntegrity.Entry[3]
            };
            // Hash only finished files; no rebuild, importer, or build callback mutates the selected build.
            await Task.Run(() => {
                for (int i = 0; i < manifest.files.Length; i++)
                    manifest.files[i] = new BuildIntegrity.Entry { path = BuildIntegrity.RequiredPaths[i],
                        sha256 = BuildIntegrity.Hash(BuildIntegrity.ConfinedPath(root, BuildIntegrity.RequiredPaths[i]), CancellationToken.None) };
            });
            BuildIntegrity.Validate(manifest);
            string json = JsonUtility.ToJson(manifest, true);
            // Developer explicitly installs the trusted copy separately; never auto-approve client files.
            string approved = EditorUtility.SaveFilePanel("Save trusted manifest OUTSIDE client test build (install as approved.json)", "", "approved", "json");
            if (string.IsNullOrEmpty(approved)) return;
            string buildRoot = Path.GetFullPath(Path.GetDirectoryName(exe)) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(approved).StartsWith(buildRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Trusted manifest must be saved outside the selected client build directory.");
            File.WriteAllText(approved, json);
            File.WriteAllText(BuildIntegrity.ConfinedPath(root, BuildIntegrity.ClientManifest), json);
            Debug.Log($"[File AC] Generated build={manifest.buildId} Windows/Mono schema=1. Client descriptor: {root}/{BuildIntegrity.ClientManifest}; trusted copy: {approved}. Restart server to load its configured copy. No rebuild required.");
        }
        catch (Exception e) { Debug.LogError($"[File AC] Manifest generation failed: {e.Message}"); }
        finally { _busy = false; }
    }
}
