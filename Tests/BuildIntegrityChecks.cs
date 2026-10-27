using System;
using System.IO;
using System.Threading;

public static class BuildIntegrityChecks
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _checks++;
    }
    private static void Reject(Action action, string name)
    {
        try { action(); } catch (InvalidDataException) { Check(true, name); return; }
        throw new Exception("Accepted " + name);
    }
    public static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "fps-integrity-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Managed"));
        try
        {
            var m = new BuildIntegrity.Manifest { schema = 1, buildId = new string('a', 32),
                platform = "Windows", backend = "Mono", files = new BuildIntegrity.Entry[3] };
            for (int i = 0; i < 3; i++)
            {
                string path = BuildIntegrity.ConfinedPath(root, BuildIntegrity.RequiredPaths[i]);
                File.WriteAllText(path, "known-good-" + i);
                m.files[i] = new BuildIntegrity.Entry { path = BuildIntegrity.RequiredPaths[i], sha256 = BuildIntegrity.Hash(path, CancellationToken.None) };
            }
            BuildIntegrity.Validate(m);
            var a = BuildIntegrity.Measure(root, 0, CancellationToken.None);
            var b = BuildIntegrity.Measure(root, 1, CancellationToken.None);
            var c = BuildIntegrity.Measure(root, 2, CancellationToken.None);
            Check(BuildIntegrity.Compare(m, a, b, c) == "Verified", "clean");
            File.AppendAllText(Path.Combine(root, "resources.assets"), "tamper");
            var tamper = BuildIntegrity.Measure(root, 2, CancellationToken.None);
            Check(BuildIntegrity.Compare(m, a, b, tamper) == "HashMismatch", "tamper");
            Check(BuildIntegrity.Compare(m, a, a, c) == "InvalidReport", "duplicate slots");
            var invalid = c; invalid.status = 3;
            Check(BuildIntegrity.Compare(m, a, b, invalid) == "InvalidReport", "invalid status");
            invalid.status = 1;
            Check(BuildIntegrity.Compare(m, a, b, invalid) == "InvalidReport", "missing with digest");
            File.Delete(Path.Combine(root, "resources.assets"));
            Check(BuildIntegrity.Compare(m, a, b, BuildIntegrity.Measure(root, 2, CancellationToken.None)) == "MissingRequiredFile", "missing");
            Check(BuildIntegrity.CanEnforce("MissingRequiredFile", false), "missing eligible");
            Check(BuildIntegrity.CanEnforce("HashMismatch", false), "mismatch eligible");
            foreach (string result in new[] { "Verified", "UnreadableFile", "UnsupportedBuild", "InvalidReport", "MissingReport", "ServerConfigurationError", "ClientConfigurationError" })
                Check(!BuildIntegrity.CanEnforce(result, false), "ineligible " + result);
            Check(!BuildIntegrity.CanEnforce("HashMismatch", true) && !BuildIntegrity.CanEnforce("MissingRequiredFile", true), "late failures ineligible");
            var unreadable = new BuildIntegrity.Digest { slot = 2, status = 2 };
            Check(BuildIntegrity.Compare(m, a, b, unreadable) == "UnreadableFile", "unreadable distinct");
            Check(BuildIntegrity.Compare(m, new BuildIntegrity.Digest { slot = 0, status = 1 }, b, unreadable) == "UnreadableFile", "unreadable vetoes mixed missing enforcement");
            using (var locked = new FileStream(Path.Combine(root, "globalgamemanagers"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(BuildIntegrity.Measure(root, 1, CancellationToken.None).status == 2, "unreadable");
            Reject(() => BuildIntegrity.ConfinedPath(root, "../outside"), "traversal");
            Reject(() => BuildIntegrity.ConfinedPath(root, Path.Combine(root, "resources.assets")), "absolute");
            Reject(() => BuildIntegrity.ConfinedPath(root, "weapon_stats.json"), "nonallowlisted");
            string original = m.files[1].path;
            m.files[1].path = m.files[0].path;
            Reject(() => BuildIntegrity.Validate(m), "duplicate manifest paths");
            m.files[1].path = original;
            m.schema = 2; Reject(() => BuildIntegrity.Validate(m), "schema"); m.schema = 1;
            m.backend = "IL2CPP"; Reject(() => BuildIntegrity.Validate(m), "backend"); m.backend = "Mono";
            m.platform = "Linux"; Reject(() => BuildIntegrity.Validate(m), "platform"); m.platform = "Windows";
            m.buildId = "bad"; Reject(() => BuildIntegrity.Validate(m), "identity"); m.buildId = new string('a', 32);
            string hash = m.files[0].sha256;
            m.files[0].sha256 = new string('z', 64); Reject(() => BuildIntegrity.Validate(m), "hash format"); m.files[0].sha256 = hash;
            m.files = new[] { m.files[0] }; Reject(() => BuildIntegrity.Validate(m), "coverage");
            string oversized = Path.Combine(root, BuildIntegrity.ClientManifest);
            File.WriteAllText(oversized, new string('x', BuildIntegrity.MaxManifestBytes + 1));
            Reject(() => BuildIntegrity.ReadManifest(oversized), "oversized manifest");
            Check(BuildIntegrity.Hash(Path.Combine(root, "globalgamemanagers"), CancellationToken.None) == b.a.ToString("x16") + b.b.ToString("x16") + b.c.ToString("x16") + b.d.ToString("x16"), "wire digest roundtrip");
            Console.WriteLine("Passed " + _checks + " build integrity checks.");
        }
        finally { Directory.Delete(root, true); }
    }
}
