using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

// Pure policy and disk operations. Paths are protocol-defined, never supplied over the network.
public static class BuildIntegrity
{
    public const int Schema = 1;
    public const int MaxManifestBytes = 8192;
    public const string ClientManifest = "integrity-client.json";
    public static readonly string[] RequiredPaths = {
        "Managed/Assembly-CSharp.dll", "globalgamemanagers", "resources.assets"
    };

    [Serializable] public sealed class Manifest
    {
        public int schema;
        public string buildId, platform, backend, createdUtc;
        public Entry[] files;
    }
    [Serializable] public sealed class Entry { public string path, sha256; }

    public static string DataRootForBuild(string selectedBuild, string platform)
    {
        if (platform == "Windows" && Path.GetExtension(selectedBuild).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(selectedBuild)), Path.GetFileNameWithoutExtension(selectedBuild) + "_Data");
        // Layout only: the macOS branch has not been validated on a Mac build.
        if (platform == "macOS" && selectedBuild.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Path.GetFullPath(selectedBuild), "Contents", "Resources", "Data");
        throw new InvalidDataException("Unsupported build layout.");
    }

    // Fixed wire representation: no variable-length strings, arrays, or client paths.
    public struct Digest
    {
        public byte slot, status; // 0=read, 1=missing, 2=unreadable
        public ulong a, b, c, d;
    }

    public static void Validate(Manifest m)
    {
        if (m == null || m.schema != Schema || !IsHex(m.buildId, 32) ||
            (m.platform != "Windows" && m.platform != "macOS") || m.backend != "Mono" ||
            m.files == null || m.files.Length != RequiredPaths.Length)
            throw new InvalidDataException("Unsupported or malformed manifest identity/coverage.");
        for (int i = 0; i < RequiredPaths.Length; i++)
            if (m.files[i] == null || m.files[i].path != RequiredPaths[i] || !IsHex(m.files[i].sha256, 64))
                throw new InvalidDataException("Required paths must occur exactly once, in protocol order, with SHA-256 hashes.");
    }

    public static bool IsHex(string value, int length)
    {
        if (value == null || value.Length != length) return false;
        foreach (char c in value)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        return true;
    }

    public static string ReadManifest(string path)
    {
        using (var stream = File.OpenRead(path))
        {
            if (stream.Length > MaxManifestBytes) throw new InvalidDataException("Manifest too large.");
            using (var reader = new StreamReader(stream))
            {
                char[] chars = new char[MaxManifestBytes + 1];
                int count = reader.ReadBlock(chars, 0, chars.Length);
                if (count > MaxManifestBytes) throw new InvalidDataException("Manifest too large.");
                return new string(chars, 0, count);
            }
        }
    }

    public static string ConfinedPath(string dataRoot, string relative)
    {
        if (Array.IndexOf(RequiredPaths, relative) < 0 && relative != ClientManifest)
            throw new InvalidDataException("Path outside integrity allowlist.");
        string root = Path.GetFullPath(dataRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Path escapes build data root.");
        // Reject symlinks/junctions, including ancestors of the selected build folder.
        for (string p = full; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked build paths are unsupported.");
        return full;
    }

    public static string Hash(string path, CancellationToken cancel)
    {
        using (var sha = SHA256.Create())
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
        {
            byte[] buffer = new byte[65536];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancel.ThrowIfCancellationRequested();
                sha.TransformBlock(buffer, 0, read, buffer, 0);
            }
            sha.TransformFinalBlock(buffer, 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
        }
    }

    public static Digest FromHash(int slot, string hex)
    {
        if (!IsHex(hex, 64)) throw new InvalidDataException("Invalid SHA-256.");
        return new Digest { slot = (byte)slot, a = Convert.ToUInt64(hex.Substring(0, 16), 16),
            b = Convert.ToUInt64(hex.Substring(16, 16), 16), c = Convert.ToUInt64(hex.Substring(32, 16), 16),
            d = Convert.ToUInt64(hex.Substring(48, 16), 16) };
    }

    public static Digest Measure(string root, int slot, CancellationToken cancel)
    {
        try { return FromHash(slot, Hash(ConfinedPath(root, RequiredPaths[slot]), cancel)); }
        catch (FileNotFoundException) { return new Digest { slot = (byte)slot, status = 1 }; }
        catch (DirectoryNotFoundException) { return new Digest { slot = (byte)slot, status = 1 }; }
        catch (IOException) { return new Digest { slot = (byte)slot, status = 2 }; }
        catch (UnauthorizedAccessException) { return new Digest { slot = (byte)slot, status = 2 }; }
    }

    public static string Compare(Manifest expected, Digest x, Digest y, Digest z)
    {
        Digest[] received = { x, y, z };
        bool mismatch = false, unreadable = false, missing = false;
        for (int i = 0; i < received.Length; i++)
        {
            Digest got = received[i], want = FromHash(i, expected.files[i].sha256);
            if (got.slot != i || got.status > 2 || (got.status != 0 && (got.a | got.b | got.c | got.d) != 0))
                return "InvalidReport";
            missing |= got.status == 1;
            unreadable |= got.status == 2;
            mismatch |= got.status == 0 && (got.a != want.a || got.b != want.b || got.c != want.c || got.d != want.d);
        }
        return unreadable ? "UnreadableFile" : missing ? "MissingRequiredFile" : mismatch ? "HashMismatch" : "Verified";
    }

    public static bool CanEnforce(string result, bool late)
        => !late && (result == "HashMismatch" || result == "MissingRequiredFile");
}
