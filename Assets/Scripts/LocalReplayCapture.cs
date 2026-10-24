using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

// Adapted from theater-mode recording: local diagnostics only, no playback or network authority.
public sealed class LocalReplayCapture : MonoBehaviour
{
    [Serializable] public struct Frame { public float time; public Vector3 position; public Quaternion rotation; }
    [Serializable] public class Recording { public string scene; public int owner; public List<Frame> frames = new(); }
    private Health _health;
    private Recording _recording;
    private float _start, _next;
    private int _savedCount;
    private void Awake() { _health = GetComponent<Health>(); }
    private void Update()
    {
        if (_health == null || !_health.IsClientInitialized || !_health.IsOwner) return;
        if (_recording == null)
        {
            _recording = new Recording { scene = gameObject.scene.name, owner = _health.OwnerId };
            _start = Time.unscaledTime;
        }
        if (Time.unscaledTime >= _next && _recording.frames.Count < 36000)
        {
            _next = Time.unscaledTime + 0.1f;
            _recording.frames.Add(new Frame { time = Time.unscaledTime - _start, position = transform.position, rotation = transform.rotation });
        }
        if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame) Save();
    }
    private void Save()
    {
        if (_recording == null || _recording.frames.Count == _savedCount) return;
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "Replays"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"Player{_recording.owner}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json");
            File.WriteAllText(path, JsonUtility.ToJson(_recording)); _savedCount = _recording.frames.Count;
            Debug.Log($"[Replay] Saved local movement capture: {path}");
        }
        catch (Exception ex) { Debug.LogWarning($"[Replay] Save failed: {ex.Message}"); }
    }
    private void OnDestroy() => Save();
}
