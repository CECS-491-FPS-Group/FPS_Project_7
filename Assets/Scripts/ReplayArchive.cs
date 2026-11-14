using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Local movement captures only; no network payload or trusted anti-cheat evidence.
public static class ReplayArchive
{
    public const int MaximumFrames = 36000;
    public const int MaximumBytes = 16 * 1024 * 1024;
    public static string Folder => Path.Combine(Application.persistentDataPath, "Replays");
    public static string[] Files()
    {
        if (!Directory.Exists(Folder) || (File.GetAttributes(Folder) & FileAttributes.ReparsePoint) != 0) return Array.Empty<string>();
        return Directory.EnumerateFiles(Folder, "*.json", SearchOption.TopDirectoryOnly).Take(128)
            .Where(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0)
            .OrderByDescending(File.GetLastWriteTimeUtc).Take(12).Select(Path.GetFileName).ToArray();
    }
    public static LocalReplayCapture.Recording Load(string leaf)
    {
        if (string.IsNullOrEmpty(leaf) || Path.GetFileName(leaf) != leaf || leaf.Contains("/") || leaf.Contains("\\") ||
            !leaf.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid replay name.");
        if ((File.GetAttributes(Folder) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked replay folder is unsupported.");
        string path = Path.Combine(Folder, leaf);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > MaximumBytes)
            throw new InvalidDataException("Replay unavailable or too large.");
        using (var reader = new StreamReader(path))
        {
            var text = new System.Text.StringBuilder();
            char[] buffer = new char[4096];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (text.Length + read > MaximumBytes) throw new InvalidDataException("Replay grew beyond the read limit.");
                text.Append(buffer, 0, read);
            }
            return Parse(text.ToString());
        }
    }
    [Serializable] private class Legacy
    { public string mapName; public List<LegacyFrame> frames; }
    [Serializable] private class LegacyFrame
    { public float time, posX, posY, posZ, rotX, rotY, rotZ, rotW; }
    public static LocalReplayCapture.Recording Parse(string json)
    {
        if (json == null || json.Length > MaximumBytes) throw new InvalidDataException("Replay too large.");
        var recording = JsonUtility.FromJson<LocalReplayCapture.Recording>(json);
        if (recording != null && string.IsNullOrEmpty(recording.scene))
        {
            var legacy = JsonUtility.FromJson<Legacy>(json);
            if (legacy?.frames != null && legacy.frames.Count <= MaximumFrames)
            {
                recording.scene = legacy.mapName;
                recording.frames = legacy.frames.Select(f => new LocalReplayCapture.Frame { time = f.time,
                    position = new Vector3(f.posX, f.posY, f.posZ), rotation = new Quaternion(f.rotX, f.rotY, f.rotZ, f.rotW) }).ToList();
            }
        }
        Validate(recording);
        return recording;
    }
    public static void Validate(LocalReplayCapture.Recording data)
    {
        if (data == null || data.version < 0 || data.version > 1 || string.IsNullOrEmpty(data.scene) || data.scene.Length > 64 ||
            data.frames == null || data.frames.Count == 0 || data.frames.Count > MaximumFrames)
            throw new InvalidDataException("Unsupported or empty movement recording.");
        float previous = -1;
        foreach (var frame in data.frames)
        {
            var p = frame.position; var q = frame.rotation;
            if (!Finite(frame.time) || frame.time < previous || frame.time < 0 || frame.time > 86400 ||
                !Finite(p.x) || !Finite(p.y) || !Finite(p.z) || p.sqrMagnitude > 1e12f ||
                !Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w) ||
                q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w < .000001f ||
                !Finite(q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w)) throw new InvalidDataException("Invalid movement sample.");
            previous = frame.time;
        }
    }
    public static Vector3 PositionAt(LocalReplayCapture.Recording data, float time)
    {
        int low = 0, high = data.frames.Count - 1;
        while (low < high) { int mid = (low + high + 1) / 2; if (data.frames[mid].time <= time) low = mid; else high = mid - 1; }
        var a = data.frames[low]; var b = data.frames[Math.Min(low + 1, data.frames.Count - 1)];
        return Vector3.Lerp(a.position, b.position, b.time > a.time ? Mathf.Clamp01((time - a.time) / (b.time - a.time)) : 0);
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
