using UnityEngine;
using FishNet.Object;
using System.IO;

public class ReplayRecorder : NetworkBehaviour
{
    public float recordRate = 0.1f;

    private ReplayData replay;
    private float recordTimer;
    private float matchTimer;
    private bool recording = false;

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Only record the player controlled by this client
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        StartRecording();
    }

    void StartRecording()
    {
        replay = new ReplayData();

        replay.matchName = "Match";
        replay.mapName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        matchTimer = 0f;
        recordTimer = 0f;

        recording = true;

        Debug.Log("Replay recording started for local player.");
    }

    void Update()
    {
        if (!recording)
            return;

        matchTimer += Time.deltaTime;
        recordTimer += Time.deltaTime;

        if (recordTimer >= recordRate)
        {
            RecordFrame();
            recordTimer = 0f;
        }

        // TEMPORARY TEST:
        // Press F6 to save the replay.
        if (Input.GetKeyDown(KeyCode.F6))
        {
            SaveReplay();
        }
    }

    void RecordFrame()
    {
        ReplayFrame frame = new ReplayFrame();

        frame.time = matchTimer;

        frame.posX = transform.position.x;
        frame.posY = transform.position.y;
        frame.posZ = transform.position.z;

        frame.rotX = transform.rotation.x;
        frame.rotY = transform.rotation.y;
        frame.rotZ = transform.rotation.z;
        frame.rotW = transform.rotation.w;

        replay.frames.Add(frame);

        Debug.Log("Replay frames: " + replay.frames.Count);
    }

    public void SaveReplay()
    {
    if (replay == null || replay.frames.Count == 0)
    {
        Debug.Log("No replay data to save.");
        return;
    }

    replay.matchLength = matchTimer;

    string json = JsonUtility.ToJson(replay, true);

    string folder = Path.Combine(
        Application.persistentDataPath,
        "Replays"
    );

    Directory.CreateDirectory(folder);

    string fileName =
        "Replay_" +
        System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +
        ".json";

    string path = Path.Combine(folder, fileName);

    File.WriteAllText(path, json);

    Debug.Log("Replay saved!");
    Debug.Log("Replay frames saved: " + replay.frames.Count);
    Debug.Log("Replay path: " + path);
    }

    private void OnApplicationQuit()
    {
        if (recording)
            SaveReplay();
    }
}