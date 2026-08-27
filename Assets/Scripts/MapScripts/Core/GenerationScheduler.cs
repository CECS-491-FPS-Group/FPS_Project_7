using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Bounded worker pool for terrain generation work. Replaces the one-OS-thread-per-request
/// model, which spawned ~100 threads when a whole bounded world was requested in one frame.
/// </summary>
public static class GenerationScheduler
{
    struct WorkItem
    {
        public Func<object> Generate;
        public Action<object> Callback;
    }

    struct CompletedItem
    {
        public Action<object> Callback;
        public object Result;
    }

    static readonly Queue<WorkItem> pending = new Queue<WorkItem>();
    static readonly Queue<CompletedItem> completed = new Queue<CompletedItem>();
    static readonly object pendingLock = new object();
    static readonly object completedLock = new object();

    static Thread[] workers;
    static volatile bool running;
    static int outstanding;
    static int workerCount = 1;

    /// <summary>Milliseconds of callbacks to run per frame. Zero drains the queue completely.</summary>
    public static float MaxPumpMillisecondsPerFrame = 0f;

    public static int Outstanding
    {
        get { return Volatile.Read(ref outstanding); }
    }

    public static void Request(Func<object> generate, Action<object> callback)
    {
        EnsureStarted();
        Interlocked.Increment(ref outstanding);

        lock (pendingLock)
        {
            pending.Enqueue(new WorkItem { Generate = generate, Callback = callback });
            Monitor.Pulse(pendingLock);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        workerCount = Mathf.Max(1, SystemInfo.processorCount - 1);
        Application.quitting += Shutdown;
        GenerationSchedulerPump.Create();
    }

    static void EnsureStarted()
    {
        if (running)
        {
            return;
        }

        lock (pendingLock)
        {
            if (running)
            {
                return;
            }

            running = true;
            workers = new Thread[workerCount];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(WorkerLoop);
                workers[i].IsBackground = true;
                workers[i].Name = "TerrainGeneration" + i;
                workers[i].Start();
            }
        }
    }

    static void WorkerLoop()
    {
        while (true)
        {
            WorkItem item;

            lock (pendingLock)
            {
                while (running && pending.Count == 0)
                {
                    Monitor.Wait(pendingLock);
                }

                if (!running)
                {
                    return;
                }

                item = pending.Dequeue();
            }

            object result = null;
            try
            {
                result = item.Generate();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            lock (completedLock)
            {
                completed.Enqueue(new CompletedItem { Callback = item.Callback, Result = result });
            }
        }
    }

    internal static void Pump()
    {
        Stopwatch stopwatch = MaxPumpMillisecondsPerFrame > 0f ? Stopwatch.StartNew() : null;

        while (true)
        {
            CompletedItem item;

            lock (completedLock)
            {
                if (completed.Count == 0)
                {
                    return;
                }
                item = completed.Dequeue();
            }

            Interlocked.Decrement(ref outstanding);

            if (item.Result != null)
            {
                try
                {
                    item.Callback(item.Result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (stopwatch != null && stopwatch.Elapsed.TotalMilliseconds >= MaxPumpMillisecondsPerFrame)
            {
                return;
            }
        }
    }

    static void Shutdown()
    {
        lock (pendingLock)
        {
            running = false;
            pending.Clear();
            Monitor.PulseAll(pendingLock);
        }

        workers = null;
        Volatile.Write(ref outstanding, 0);

        lock (completedLock)
        {
            completed.Clear();
        }
    }
}
