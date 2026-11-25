using System;
using System.Threading.Tasks;
using FishNet.Transporting.UTP;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

internal static class RelayLobby
{
    public const int MaxCodeLength = 12;
    private const string Alphabet = "6789BCDFGHJKLMNPQRTW";
    private static Task _initializing;

    public static bool ValidCode(string code)
    {
        if (code.Length < 6 || code.Length > MaxCodeLength) return false;
        foreach (char c in code)
            if (Alphabet.IndexOf(c) < 0) return false;
        return true;
    }

    // Multiple menu attempts can overlap a service call that the SDK cannot cancel.
    public static Task Initialize()
    {
        if (_initializing == null || _initializing.IsCompleted)
            _initializing = InitializeAndSignIn();
        return _initializing;
    }

    private static async Task InitializeAndSignIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public static async Task<string> ConfigureHost(FishyUnityTransport transport, int remoteSlots, Action checkAttempt)
    {
        var allocation = await RelayService.Instance.CreateAllocationAsync(remoteSlots);
        checkAttempt();
        string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        checkAttempt();
        transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
        return code;
    }

    public static async Task ConfigureClient(FishyUnityTransport transport, string code, Action checkAttempt)
    {
        var allocation = await RelayService.Instance.JoinAllocationAsync(code);
        checkAttempt();
        transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
    }
}

// The adapter's Started event confirms a local socket. UTP receives the Relay BIND
// acknowledgement later. Borrow its driver through the adapter's public factory hook.
internal sealed class RelayHostBinding : INetworkStreamDriverConstructor, IDisposable
{
    private readonly FishyUnityTransport _transport;
    private readonly INetworkStreamDriverConstructor _factory;
    private NetworkDriver _driver;

    public RelayHostBinding(FishyUnityTransport transport)
    {
        _transport = transport;
        _factory = transport.DriverConstructor;
        transport.DriverConstructor = this;
    }

    public void CreateDriver(FishyUnityTransport transport, out NetworkDriver driver,
        out NetworkPipeline unreliable, out NetworkPipeline sequenced, out NetworkPipeline reliable)
    {
        _factory.CreateDriver(transport, out driver, out unreliable, out sequenced, out reliable);
        _driver = driver;
    }

    public bool Established => _driver.IsCreated && _driver.GetRelayConnectionStatus() == RelayConnectionStatus.Established;

    public void Dispose()
    {
        if (_transport != null) _transport.DriverConstructor = _factory;
        _driver = default; // Only the adapter owns and disposes the native driver.
    }
}
