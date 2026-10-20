using System;
using UnityEngine;
using UnityEngine.UI;

// Captures the local microphone and exposes gain-adjusted samples for later networking.
public class VoiceChat : MonoBehaviour
{
    public const int SampleRate = 16000;

    [SerializeField] private Slider gainSlider;
    [SerializeField, Range(0f, 5f)] private float gain = 1f;
    [SerializeField] private string deviceName = "";
    [SerializeField] private bool loopback = false;

    // Fires with gain-applied mono samples (-1..1). Hook networking here.
    public event Action<float[]> OnSamplesCaptured;

    private AudioClip micClip;
    private string device;
    private int lastPosition;
    private AudioSource loopbackSource;

    public float Gain
    {
        get => gain;
        set => gain = Mathf.Clamp(value, 0f, 5f);
    }

    private void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("VoiceChat: no microphone found.");
            enabled = false;
            return;
        }

        device = string.IsNullOrEmpty(deviceName) ? Microphone.devices[0] : deviceName;
        micClip = Microphone.Start(device, true, 1, SampleRate);

        if (gainSlider != null)
        {
            gainSlider.minValue = 0f;
            gainSlider.maxValue = 5f;
            gainSlider.value = gain;
            gainSlider.onValueChanged.AddListener(v => Gain = v);
        }

        if (loopback)
        {
            loopbackSource = gameObject.AddComponent<AudioSource>();
            loopbackSource.loop = true;
        }
    }

    private void Update()
    {
        if (micClip == null) return;

        int pos = Microphone.GetPosition(device);
        if (pos == lastPosition) return;

        int length = micClip.samples;
        int count = pos > lastPosition ? pos - lastPosition : length - lastPosition + pos;
        float[] data = new float[count];

        if (pos > lastPosition)
        {
            micClip.GetData(data, lastPosition);
        }
        else
        {
            float[] tail = new float[length - lastPosition];
            micClip.GetData(tail, lastPosition);
            Array.Copy(tail, data, tail.Length);
            if (pos > 0)
            {
                float[] head = new float[pos];
                micClip.GetData(head, 0);
                Array.Copy(head, 0, data, tail.Length, pos);
            }
        }
        lastPosition = pos;

        for (int i = 0; i < data.Length; i++)
            data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);

        OnSamplesCaptured?.Invoke(data);

        if (loopbackSource != null)
        {
            var clip = AudioClip.Create("loop", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            loopbackSource.PlayOneShot(clip);
        }
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(device) && Microphone.IsRecording(device))
            Microphone.End(device);
    }
}
