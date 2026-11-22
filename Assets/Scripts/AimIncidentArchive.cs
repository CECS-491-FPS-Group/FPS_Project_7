using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static AimIncidentData;

// Host-local archive. Immutable snapshots, one worker, bounded pending queue, no RPC.
public static class AimIncidentArchive
{
    public const int MaximumBytes = 256 * 1024, RetainedFiles = 32, RetentionDays = 7;
    public const int MaximumPending = 16;
    private static readonly object Gate = new object();
    private static readonly List<Incident> Pending = new List<Incident>(MaximumPending);
    private static bool _working;
    private static string _failure;
    private static string _saved;
    private static string _folder;
    public static string Folder => _folder ?? (_folder = Path.Combine(Application.persistentDataPath, "AimIncidents"));
    public static bool Busy { get { lock (Gate) return _working; } }
    public static string TakeFailure() { lock (Gate) { string value = _failure; _failure = null; return value; } }
    public static string TakeSaved() { lock (Gate) { string value = _saved; _saved = null; return value; } }
    internal static void ReportFailure(string reason) { lock (Gate) _failure = reason; }
    public static bool Enqueue(Incident data)
    {
        Validate(data); string folder = Folder; // Resolve Unity path on caller/main thread.
        lock (Gate)
        {
            int existing = Pending.FindIndex(d => d.incidentId == data.incidentId);
            if (existing >= 0) Pending[existing] = data;
            else if (Pending.Count < MaximumPending) Pending.Add(data);
            else { _failure = "save queue full; incident snapshot not persisted"; return false; }
            if (!_working) { _working = true; Task.Run(() => Drain(folder)); }
        }
        return true;
    }
    private static void Drain(string folder)
    {
        while (true)
        {
            Incident next;
            lock (Gate)
            {
                if (Pending.Count == 0) { _working = false; return; }
                next = Pending[0]; Pending.RemoveAt(0);
            }
            try { Write(folder, next); lock (Gate) _saved = $"{Leaf(next)} revision={next.revision} final={next.final}"; }
            catch (Exception error) { lock (Gate) _failure = "incident save failed: " + error.GetType().Name; }
        }
    }
    public static string Leaf(Incident data) => data.matchId + "_" + data.incidentId + ".json";
    private static bool ValidLeaf(string leaf) => leaf != null && leaf.Length == 70 && leaf[32] == '_' &&
        leaf.EndsWith(".json", StringComparison.Ordinal) && Id(leaf.Substring(0, 32)) && Id(leaf.Substring(33, 32));
    private static void Root(string folder, bool create)
    {
        if (create) Directory.CreateDirectory(folder);
        if (!Directory.Exists(folder) || (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Incident folder unavailable or linked.");
    }
    private static void Unlinked(string path)
    {
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked incident file unsupported.");
    }
    private static void Atomic(string path, string json, int maximum)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length > maximum) throw new InvalidDataException("Incident exceeds byte limit.");
        Unlinked(path);
        string temporary = Path.Combine(Path.GetDirectoryName(path), Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes, 0, bytes.Length);
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static FileInfo[] Entries(string folder)
    {
        Root(folder, false);
        var files = Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .Where(p => ValidLeaf(Path.GetFileName(p))).Take(129).Select(p => new FileInfo(p)).ToArray();
        if (files.Length > 128) throw new IOException("Incident archive has excessive external files; inspect locally.");
        return files;
    }
    private static void Prune(string folder, string incoming)
    {
        var files = Entries(folder).OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
        int keep = 0;
        foreach (var file in files)
        {
            Unlinked(file.FullName);
            bool expire = DateTime.UtcNow - file.LastWriteTimeUtc > TimeSpan.FromDays(RetentionDays) || file.Length > MaximumBytes;
            if (!expire && file.Name != incoming && keep++ < RetainedFiles - 1) continue;
            if (!expire && file.Name == incoming) continue;
            File.Delete(file.FullName);
            string review = file.FullName + ".review.json"; Unlinked(review); if (File.Exists(review)) File.Delete(review);
        }
        // Only our bounded temporary pattern, abandoned by an earlier process crash.
        foreach (string path in Directory.EnumerateFiles(folder, "*.tmp").Take(16))
            if (Id(Path.GetFileNameWithoutExtension(path)) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromDays(1))
            { Unlinked(path); File.Delete(path); }
        foreach (string path in Directory.EnumerateFiles(folder, "*.review.json").Take(129))
        {
            string parent = path.Substring(0, path.Length - ".review.json".Length);
            if (ValidLeaf(Path.GetFileName(parent)) && !File.Exists(parent)) { Unlinked(path); File.Delete(path); }
        }
    }
    private static void Write(string folder, Incident data)
    {
        Validate(data); Root(folder, true); string leaf = Leaf(data); Prune(folder, leaf);
        // JsonUtility supports plain serializable data on the worker; snapshots never mutate.
        Atomic(Path.Combine(folder, leaf), JsonUtility.ToJson(data), MaximumBytes);
    }
    public static string[] Files()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<string>();
        return Entries(Folder).Where(f => f.Length <= MaximumBytes && (f.Attributes & FileAttributes.ReparsePoint) == 0 &&
                DateTime.UtcNow - f.LastWriteTimeUtc <= TimeSpan.FromDays(RetentionDays))
            .OrderByDescending(f => f.LastWriteTimeUtc).Take(RetainedFiles).Select(f => f.Name).ToArray();
    }
    private static string Read(string path, int maximum)
    {
        Unlinked(path);
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            if (stream.Length > maximum) throw new InvalidDataException("Incident too large.");
            using (var reader = new StreamReader(stream))
            {
                var text = new StringBuilder(); char[] buffer = new char[4096]; int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                { if (text.Length + read > maximum) throw new InvalidDataException("Incident grew past limit."); text.Append(buffer, 0, read); }
                return text.ToString();
            }
        }
    }
    public static Incident Load(string leaf)
    {
        Root(Folder, false); if (!ValidLeaf(leaf)) throw new InvalidDataException("Invalid incident name.");
        var data = JsonUtility.FromJson<Incident>(Read(Path.Combine(Folder, leaf), MaximumBytes)); Validate(data);
        if (Leaf(data) != leaf) throw new InvalidDataException("Incident filename/identity mismatch.");
        return data;
    }
    public static Review LoadReview(Incident data)
    {
        Validate(data);
        Root(Folder, false); string path = Path.Combine(Folder, Leaf(data)) + ".review.json";
        if (!File.Exists(path)) return new Review { incidentId = data.incidentId };
        var review = JsonUtility.FromJson<Review>(Read(path, 4096));
        if (review == null || review.incidentId != data.incidentId || review.note == null || review.note.Length > 280 || review.revision < 0)
            throw new InvalidDataException("Invalid review note.");
        return review;
    }
    public static void SaveReview(Incident data, bool reviewed, string note)
    {
        Validate(data);
        if (Load(Leaf(data)).revision != data.revision) throw new IOException("Evidence updated; reload before reviewing.");
        note = note ?? ""; if (note.Length > 280) note = note.Substring(0, 280);
        var review = new Review { incidentId = data.incidentId, revision = data.revision, reviewed = reviewed,
            note = note, reviewedUtc = DateTime.UtcNow.ToString("O") };
        Atomic(Path.Combine(Folder, Leaf(data)) + ".review.json", JsonUtility.ToJson(review), 4096);
        // A final worker retention pass can race an offline reviewer. Never leave an orphan note.
        if (!File.Exists(Path.Combine(Folder, Leaf(data))))
        { File.Delete(Path.Combine(Folder, Leaf(data)) + ".review.json"); throw new IOException("Incident expired during review."); }
    }
}
