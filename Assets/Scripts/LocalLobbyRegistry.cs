using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

public static class LocalLobbyRegistry
{
    public const string StateStarting = "Starting";
    public const string StateOpen = "Open";

    private const int SchemaVersion = 1;
    private const int FirstPort = 7770;
    private const int LastPort = 7799;
    private const int CodeGenerationAttempts = 64;
    private const int MutexWaitMilliseconds = 1500;
    private const double StaleRoomSeconds = 15d;
    private const string MutexName = "FPS_Project_7_LocalLobbyRegistry_v1";
    private const string RegistryFileName = "local-lobbies.json";

    private static readonly object InProcessLock = new object();

    public static string RegistryPath =>
        Path.Combine(Application.persistentDataPath, RegistryFileName);

    public static bool TryReserveRoom(
        string requestedRoomName,
        bool isListed,
        int maxPlayers,
        out LocalLobbyRecord reservedRoom,
        out string error)
    {
        LocalLobbyRecord result = null;

        bool accessed = TryAccessRegistry(database =>
        {
            HashSet<int> usedPorts = new HashSet<int>();

            for (int i = 0; i < database.rooms.Count; i++)
                usedPorts.Add(database.rooms[i].port);

            int availablePort = -1;

            for (int port = FirstPort; port <= LastPort; port++)
            {
                if (!usedPorts.Contains(port))
                {
                    availablePort = port;
                    break;
                }
            }

            if (availablePort < 0)
                return;

            string code = string.Empty;

            for (int attempt = 0; attempt < CodeGenerationAttempts; attempt++)
            {
                string candidate = LobbyCodeUtility.Generate();
                bool collision = false;

                for (int i = 0; i < database.rooms.Count; i++)
                {
                    if (string.Equals(database.rooms[i].code, candidate, StringComparison.Ordinal))
                    {
                        collision = true;
                        break;
                    }
                }

                if (!collision)
                {
                    code = candidate;
                    break;
                }
            }

            if (string.IsNullOrEmpty(code))
                return;

            string roomName = string.IsNullOrWhiteSpace(requestedRoomName)
                ? $"Room {code}"
                : requestedRoomName.Trim();

            result = new LocalLobbyRecord
            {
                code = code,
                roomName = roomName,
                address = "127.0.0.1",
                port = availablePort,
                playerCount = 1,
                maxPlayers = Mathf.Max(1, maxPlayers),
                isListed = isListed,
                state = StateStarting,
                ownerToken = Guid.NewGuid().ToString("N"),
                lastHeartbeatUtcTicks = DateTime.UtcNow.Ticks,
                gameVersion = Application.version
            };

            database.rooms.Add(result);
        }, out error);

        reservedRoom = result?.Copy();

        if (!accessed)
            return false;

        if (reservedRoom == null)
        {
            error = $"No local lobby slot was available. Ports {FirstPort}-{LastPort} may already be reserved.";
            return false;
        }

        return true;
    }

    public static bool TryFindOpenRoom(
        string enteredCode,
        out LocalLobbyRecord room,
        out string error)
    {
        string normalizedCode = LobbyCodeUtility.Normalize(enteredCode);
        LocalLobbyRecord result = null;
        string lookupError = string.Empty;

        if (!LobbyCodeUtility.IsValid(normalizedCode))
        {
            room = null;
            error = "Enter the complete 6-character code using only 0-9 and A-F.";
            return false;
        }

        bool accessed = TryAccessRegistry(database =>
        {
            for (int i = 0; i < database.rooms.Count; i++)
            {
                LocalLobbyRecord candidate = database.rooms[i];

                if (!string.Equals(candidate.code, normalizedCode, StringComparison.Ordinal))
                    continue;

                if (!string.Equals(candidate.gameVersion, Application.version, StringComparison.Ordinal))
                    lookupError = "That room was created by a different game version.";
                else if (!string.Equals(candidate.state, StateOpen, StringComparison.Ordinal))
                    lookupError = "That room is still starting. Try again in a moment.";
                else if (candidate.playerCount >= candidate.maxPlayers)
                    lookupError = "That room is full.";
                else
                    result = candidate.Copy();

                return;
            }

            lookupError = "No active local room matches that code.";
        }, out error);

        room = result;

        if (!accessed)
            return false;

        if (room == null)
        {
            error = lookupError;
            return false;
        }

        return true;
    }

    public static bool TryGetListedRooms(
        out List<LocalLobbyRecord> rooms,
        out string error)
    {
        List<LocalLobbyRecord> result = new List<LocalLobbyRecord>();

        bool accessed = TryAccessRegistry(database =>
        {
            for (int i = 0; i < database.rooms.Count; i++)
            {
                LocalLobbyRecord room = database.rooms[i];

                if (!room.isListed ||
                    !string.Equals(room.state, StateOpen, StringComparison.Ordinal) ||
                    !string.Equals(room.gameVersion, Application.version, StringComparison.Ordinal) ||
                    room.playerCount >= room.maxPlayers)
                {
                    continue;
                }

                result.Add(room.Copy());
            }

            result.Sort((left, right) =>
                string.Compare(left.code, right.code, StringComparison.Ordinal));
        }, out error);

        rooms = result;
        return accessed;
    }

    public static bool TryUpdateRoom(
        string code,
        string ownerToken,
        string state,
        int playerCount,
        out string error)
    {
        bool found = false;

        bool accessed = TryAccessRegistry(database =>
        {
            for (int i = 0; i < database.rooms.Count; i++)
            {
                LocalLobbyRecord room = database.rooms[i];

                if (!MatchesOwner(room, code, ownerToken))
                    continue;

                room.state = state;
                room.playerCount = Mathf.Clamp(playerCount, 0, room.maxPlayers);
                room.lastHeartbeatUtcTicks = DateTime.UtcNow.Ticks;
                found = true;
                return;
            }
        }, out error);

        return accessed && found;
    }

    public static bool TryRemoveRoom(
        string code,
        string ownerToken,
        out string error)
    {
        bool removed = false;

        bool accessed = TryAccessRegistry(database =>
        {
            for (int i = database.rooms.Count - 1; i >= 0; i--)
            {
                if (!MatchesOwner(database.rooms[i], code, ownerToken))
                    continue;

                database.rooms.RemoveAt(i);
                removed = true;
            }
        }, out error);

        return accessed && removed;
    }

    private static bool MatchesOwner(
        LocalLobbyRecord room,
        string code,
        string ownerToken)
    {
        return room != null &&
               string.Equals(room.code, code, StringComparison.Ordinal) &&
               string.Equals(room.ownerToken, ownerToken, StringComparison.Ordinal);
    }

    private static bool TryAccessRegistry(Action<RegistryData> action, out string error)
    {
        error = string.Empty;

        lock (InProcessLock)
        {
            bool mutexAcquired = false;
            Mutex mutex = null;

            try
            {
                mutex = new Mutex(false, MutexName);

                try
                {
                    mutexAcquired = mutex.WaitOne(MutexWaitMilliseconds);
                }
                catch (AbandonedMutexException)
                {
                    mutexAcquired = true;
                }

                if (!mutexAcquired)
                {
                    error = "The local room list is busy. Try again.";
                    return false;
                }

                RegistryData database = LoadRegistry();
                RemoveExpiredRooms(database);
                action(database);
                SaveRegistry(database);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not access the local room list: {exception.Message}";
                return false;
            }
            finally
            {
                if (mutexAcquired)
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch
                    {
                        // Another registry attempt can recover an abandoned mutex.
                    }
                }

                mutex?.Dispose();
            }
        }
    }

    private static RegistryData LoadRegistry()
    {
        RegistryData result = TryReadRegistryFile(RegistryPath);

        if (result == null)
            result = TryReadRegistryFile(GetBackupPath());

        if (result == null)
            result = new RegistryData();

        if (result.rooms == null)
            result.rooms = new List<LocalLobbyRecord>();

        result.schemaVersion = SchemaVersion;
        return result;
    }

    private static RegistryData TryReadRegistryFile(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<RegistryData>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveRegistry(RegistryData database)
    {
        string directory = Path.GetDirectoryName(RegistryPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = RegistryPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(database, true));

            if (File.Exists(RegistryPath))
            {
                try
                {
                    File.Replace(temporaryPath, RegistryPath, GetBackupPath());
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(temporaryPath, RegistryPath, true);
                }
                catch (IOException)
                {
                    File.Copy(temporaryPath, RegistryPath, true);
                }
            }
            else
            {
                File.Move(temporaryPath, RegistryPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string GetBackupPath()
    {
        return RegistryPath + ".backup";
    }

    private static void RemoveExpiredRooms(RegistryData database)
    {
        long cutoffTicks = DateTime.UtcNow.Subtract(
            TimeSpan.FromSeconds(StaleRoomSeconds)).Ticks;

        for (int i = database.rooms.Count - 1; i >= 0; i--)
        {
            LocalLobbyRecord room = database.rooms[i];

            if (room == null || room.lastHeartbeatUtcTicks < cutoffTicks)
                database.rooms.RemoveAt(i);
        }
    }

    [Serializable]
    private sealed class RegistryData
    {
        public int schemaVersion = SchemaVersion;
        public List<LocalLobbyRecord> rooms = new List<LocalLobbyRecord>();
    }
}
