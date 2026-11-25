using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.UTP;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Drives the shipped scene and listeners. Only the service response and the second
// host's loopback endpoint are fixtures; this check never requests an allocation.
[InitializeOnLoad]
public static class MultiplayerMenuChecks
{
    private const string Running = "FPS.MultiplayerMenuChecks";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string FixtureCode = "BCDFGHJKLMNP";

    static MultiplayerMenuChecks() => EditorApplication.playModeStateChanged += ModeChanged;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this check in batch mode.");
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void ModeChanged(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Exercise;
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseBool(Running);
            EditorApplication.Exit(SessionState.GetBool(Running + ".passed", false) ? 0 : 1);
        }
    }

    private static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>();
    private static void Click(string name)
    {
        var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(b => b.name == name && b.isActiveAndEnabled && b.interactable);
        button.onClick.Invoke();
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, Hidden).GetValue(target);

    private static async Task Until(Func<bool> test, string label, int seconds = 45)
    {
        double deadline = EditorApplication.timeSinceStartup + seconds;
        while (!test())
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(label);
            await Task.Delay(50);
        }
        Debug.Log("[Multiplayer check] Passed: " + label);
    }

    private static async void Exercise()
    {
        bool passed = false;
        try
        {
            await Until(() => Find<LobbyConnection>() != null, "entry menu");
            Click("PlayButton");
            var menu = Find<LobbyConnection>();
            var status = Field<TMP_Text>(menu, "statusText");
            var direct = Field<TMP_InputField>(menu, "lanHostInputField");
            var mp = InstanceFinder.NetworkManager.GetComponent<Multipass>();
            await Until(() => !mp.GlobalServerActions && mp.Transports.Count == 2 && direct.gameObject.activeInHierarchy, "LAN default and Multipass children");
            Click("INTERNET");
            menu.ipInputField.text = FixtureCode;
            await Until(() => menu.ipInputField.text == FixtureCode && !direct.gameObject.activeInHierarchy, "12-character invite entry");
            direct.text = "127.0.0.1:29989";
            menu.ipInputField.text = "X";
            Click("JoinButton");
            await Until(() => status.text.Contains("6–12"), "invalid invite rejects before networking despite stored direct IP");

            // Hold the real menu's awaited sign-in boundary, then leave and deliver a late result.
            var serviceTask = typeof(LobbyConnection).Assembly.GetType("RelayLobby")
                .GetField("_initializing", BindingFlags.NonPublic | BindingFlags.Static);
            var pending = new TaskCompletionSource<bool>();
            serviceTask.SetValue(null, pending.Task);
            menu.ipInputField.text = FixtureCode;
            Click("JoinButton");
            Click("JoinButton");
            Click("LAN");
            pending.SetResult(true);
            await Task.Delay(100);
            await Until(() => !LocalLobbySession.IsInternet && !InstanceFinder.IsClientStarted && direct.gameObject.activeInHierarchy, "late service completion after mode change");
            Click("INTERNET");
            pending = new TaskCompletionSource<bool>();
            serviceTask.SetValue(null, pending.Task);
            Click("HostButton");
            Click("CreateRoomButton");
            Field<GameObject>(menu, "hostOptions").transform.Find("HostOptionsWindow/CancelHostButton")
                .GetComponent<Button>().onClick.Invoke();
            pending.SetResult(true);
            await Until(() => status.text.Contains("cancelled"), "cancel CREATE before service completion");
            menu.ipInputField.text = FixtureCode;
            pending = new TaskCompletionSource<bool>();
            serviceTask.SetValue(null, pending.Task);
            Click("JoinButton");
            pending.SetException(new RelayServiceException(RelayExceptionReason.JoinCodeNotFound, "fixture"));
            await Until(() => status.text.Contains("JoinCodeNotFound"), "recoverable provider error");
            serviceTask.SetValue(null, null);

            Click("LAN");
            menu.ipInputField.text = "ZZZZZZ";
            direct.text = "";
            Click("JoinButton");
            await Until(() => status.text.Contains("0-9"), "LAN six-hex validator");
            direct.text = "127.0.0.1:29989";
            Click("JoinButton");
            await Until(() => status.text.Contains("stopped") || status.text.Contains("timed out"), "unanswered direct LAN connection");
            Click("HostButton");
            Click("CreateRoomButton");
            Click("CreateRoomButton");
            await Until(() => Find<NetworkLobbyUI>()?.IsClientInitialized == true, "LAN host authenticated and in lobby after retry");
            string lanCode = LocalLobbySession.LobbyCode;
            await Until(() => LocalLobbyRegistry.TryFindOpenRoom(lanCode, out var room, out _) && room.port == mp.Transports[0].GetPort(), "same-PC typed lookup and selected gameplay port");
            using (var discovery = new LanLobbyDiscovery(false))
            {
                discovery.Request();
                await Until(() => { discovery.Poll(); return discovery.GetRooms().Any(r => r.code == lanCode && r.port == LocalLobbySession.Port); }, "UDP server-list advertisement");
            }
            await Until(() => UnityServices.State == ServicesInitializationState.Uninitialized && mp.Transports[1].GetConnectionState(true) == LocalConnectionState.Stopped, "offline LAN with idle Relay and UGS");
            ClientDisconnectRecovery.ReturnToMenu();
            await Until(() => Find<LobbyConnection>() != null && !ClientDisconnectRecovery.InProgress, "host leave returns to reusable menu");

            Click("PlayButton");
            Click("INTERNET");
            Click("HostButton");
            menu = Find<LobbyConnection>();
            mp = InstanceFinder.NetworkManager.GetComponent<Multipass>();
            // Use the adapter's direct loopback protocol for this isolated FishNet test.
            // The shipping code always obtains DTLS credentials from Relay.
            var fishy = (FishyUnityTransport)mp.Transports[1];
            if (!fishy.UseEncryption || fishy.UseWebSockets) throw new Exception("DTLS configuration");
            var begin = typeof(LobbyConnection).GetMethod("Begin", Hidden);
            if (!(bool)begin.Invoke(menu, new object[] { true })) throw new Exception("begin offline bind");
            fishy.SetRelayServerData(new Unity.Networking.Transport.Relay.RelayServerData(
                "127.0.0.1", 29991, Guid.NewGuid().ToByteArray(), new byte[255], new byte[255], new byte[64], true));
            LocalLobbySession.BeginInternet(FixtureCode, true);
            typeof(LobbyConnection).GetMethod("StartRelayServer", Hidden).Invoke(menu, new object[] { fishy });
            status = Field<TMP_Text>(menu, "statusText");
            await Until(() => status.text.Contains("Waiting for Relay") && !InstanceFinder.IsClientStarted, "DTLS host waits for BIND from an offline endpoint");
            await Until(() => fishy.GetConnectionState(true) == LocalConnectionState.Stopped &&
                !LocalLobbySession.IsHosting && Find<LobbyConnection>() != null, "unreachable DTLS endpoint releases host for retry", 50);
            fishy.UseEncryption = false;
            fishy.SetConnectionData("127.0.0.1", 29990);
            if (!(bool)begin.Invoke(menu, new object[] { true })) throw new Exception("begin adapter host");
            LocalLobbySession.BeginInternet(FixtureCode, true);
            typeof(LobbyConnection).GetMethod("StartServer", Hidden).Invoke(menu, null);
            await Until(() => Find<NetworkLobbyUI>()?.IsClientInitialized == true && Find<LobbyCodeUI>()?.CurrentLobbyCode == FixtureCode, "Fishy host on index 1, authenticated and provider-length code replicated");
            await Until(() => mp.Transports[0].GetConnectionState(true) == LocalConnectionState.Stopped, "Tugboat idle during adapter session");
            var codeUi = Find<LobbyCodeUI>();
            var codeLabel = Field<TMP_Text>(codeUi, "lobbyCodeText");
            if (codeLabel.text == FixtureCode || codeLabel.text.Length != FixtureCode.Length) throw new Exception("invite mask");
            Field<Button>(codeUi, "visibilityButton").onClick.Invoke();
            await Until(() => codeLabel.text == FixtureCode, "eye reveals complete invite");
            var lobby = Find<NetworkLobbyUI>();
            lobby.ReadyUpClicked();
            await Until(() => lobby.startGameButton.interactable, "host ready");
            lobby.StartGameClicked();
            await Until(() => Find<Health>()?.IsSpawned == true, "authoritative gameplay player spawned", 120);
            Find<GameplayPlayerSpawner>().ReturnToLobby();
            await Until(() => Find<LobbyCodeUI>()?.CurrentLobbyCode == FixtureCode, "same invite after return from match");
            ClientDisconnectRecovery.ReturnToMenu();
            await Until(() => Find<LobbyConnection>() != null && !ClientDisconnectRecovery.InProgress, "adapter host stopped and menu restored");
            passed = true;
        }
        catch (Exception error) { Debug.LogException(error); }
        SessionState.SetBool(Running + ".passed", passed);
        EditorApplication.ExitPlaymode();
    }
}
