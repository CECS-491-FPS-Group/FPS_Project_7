using UnityEngine;

/// <summary>Drains finished generation jobs on the main thread each frame.</summary>

public class GenerationSchedulerPump : MonoBehaviour
{
    static GenerationSchedulerPump instance;

    public static void Create()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("GenerationSchedulerPump");
        host.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(host);
        instance = host.AddComponent<GenerationSchedulerPump>();
    }

    void Update()
    {
        GenerationScheduler.Pump();
    }
}
