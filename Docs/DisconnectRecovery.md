# Disconnect reason and local menu recovery

## Observed before this change

User-supplied runtime evidence:

- Remote clean startup: owner 1 Verified; Editor host: owner 0 EditorSkipped.
- ReportOnly after-load tamper and recovery passed.
- Static lobby kick: HashMismatch, policy Disconnect, remote Id 1 stopped. The
  disconnected standalone still appeared in the lobby.
- Dynamic gameplay kick: sustained suspicion 8.07 against threshold 8, policy
  Disconnect, remote Id 2 stopped. Standalone stayed on a frozen gameplay view.
- Host continued and later returned to the lobby without the removed player.

This establishes server disconnects, not successful client recovery, correct messages,
or immediate roster updates. No second-clean-client survival result was supplied.

## Trace and changes

`LobbyConnection`'s scene-local stop callback previously handled only `_joiningClient`
failures. It did not recover established connections; gameplay has no such menu
object. `MainMenuButtons.OpenMainMenu` previously loaded a scene without disconnecting.
These explain the stale disconnected-client UI. The supplied lobby observation does
not establish a stale host roster.

Installed FishNet source:

- `ServerManager.Transport_OnRemoteConnectionState` invokes remote-stop callbacks,
  removes the connection, calls `ServerObjects.ClientDisconnected`, then resets it.
- `ClientDisconnected` despawns owned objects unless PreventDespawnOnDisconnect;
  the active player inherits this flag set to false. Existing GameplayPlayerSpawner,
  scoreboard and currency callbacks remove per-connection state. No global match
  ReturnToLobby is invoked for a single kick.
- `NetworkLobbyUI` already removes name/id/ready/ping entries on remote stop. The
  final UI refresh is now explicit; ping/roster/start checks reject inactive senders.
- `ClientManager` clears/despawns client objects before its public stop event. It
  does not unload the local scene or reset application-specific menu session state.
- `Disconnect(false)` marks Disconnecting immediately (`IsActive` becomes false),
  sends queued data in the outgoing iteration and schedules transport close after
  at least 100ms or two ticks in TransportManager. No client acknowledgment wait.
- DefaultSceneProcessor defers activation of scene loads. Recovery therefore drains
  FishNet's current queue before disposing its manager, rather than abandoning a
  disabled-activation operation. Remaining queued client loads skip once disconnected.

`FileAntiCheat` now sends a reliable, targeted one-byte Notice before the existing
Disconnect(false). No server handler accepts this notice from clients. Only codes
1=integrity and 2=movement are recognized; arbitrary text is never received. The
installed bounded graceful-close interval is reused; no extra timer/ACK is added.
Existing gameplay RPCs check sender IsActive, so a disconnecting sender cannot fire,
reload, switch weapons, acknowledge respawn, ready or report ping. Terrain/boost/
snapshot requests also validate active connections. No detector or threshold changes.

`GameplayPlayerSpawner` installs one separate persistent ClientDisconnectRecovery
runner. It owns balanced manager subscriptions, remembers a server reason, and reacts
once to an established client's Stopped event even without a reason. It waits for
any FishNet scene queue to finish, unsubscribes, destroys the old **client-only**
manager, waits for destruction, clears LocalLobbySession, restores time scale/cursor,
and locally loads MainMenu in Single mode. MainMenu's serialized manager can then
initialize without an old persistent manager destroying it. The runner binds to that
fresh manager once. No server/global scene-change API is used for unexpected loss.
No host manager is torn down by unexpected local-client loss while its server runs.

The notice is a small runtime uGUI panel using TMP and MainMenu's existing InputSystem
EventSystem, with a Dismiss button and keyboard selection. No manual UI/scene/prefab
wiring. Exact messages:

- Static: "Disconnected: game file integrity check failed. Restore the original game files and try again."
- Dynamic: "Disconnected: movement exceeded the server's allowed limits."
- No/unknown reason: "Connection to the server was lost."

Reason state survives local scene changes, then clears on dismissal or a new connection
attempt (including failed attempts). Repeated stops cannot start duplicate loads;
new attempts are blocked while recovery runs. Notices arriving after recovery begins
cannot overwrite it. Delivery is best-effort: lost reason packets use the generic
message. The reason is not an authentication or enforcement input.

`MainMenuButtons.OpenMainMenu` now uses an intentional local leave path, suppressing
loss messages and stopping the local connection before loading. Explicit host Return
may stop its own server; an anti-cheat kick never does. Application quit suppresses
recovery. No automatic reconnect. ReportOnly defaults and trusted local-host exemption
are unchanged.

## Build and manual tests (pending)

Rebuild **host and every test client**: the targeted Notice broadcast and recovery
code are new. No new NetworkBehaviours or GUID/prefab changes. Generate a fresh
manifest from the completed clean rebuild, install its separate trusted copy, then
copy that exact clean build to test clients as described in StaticIntegrity.md.
Preserve earlier clean builds/manifests; do not regenerate from a tampered copy.

1. With ReportOnly defaults, connect two standalone clients to the Editor host.
   Confirm both startup Verified. Verify normal lobby ready/start and gameplay.
2. Static kick **in the lobby**: after a clean on-time verification, enable only
   Static Disconnect; append one byte to the test client's backed-up globalgamemanagers
   after load, wait the 15s cooldown and press F9. Expect one targeted reason and one
   policy disconnect. Within transport-close latency, host/clean client's roster
   loses the kicked row and its ready state; ready/start eligibility reflects remaining
   players. Kicked client must show MainMenu and the exact static message.
3. Dismiss with mouse or selected-button keyboard input. Confirm cursor/menu usable,
   only one live NetworkManager and one recovery runner, and no stale lobby HUD.
   Restore the exact original file before restarting the client. Manually rejoin;
   expect Verified and no old message. Repeat a second connect/kick cycle to detect
   duplicate callbacks/managers or subscription accumulation.
4. Dynamic kick **during gameplay**: keep static ReportOnly, enable Dynamic Disconnect
   threshold 8, focus only the remote test client and F7 sprint unobstructed. Expect
   the existing sustained-speed enforcement and the exact movement notice on a usable
   local MainMenu. Its player disappears on host/clean client; they can still move,
   shoot, respawn and finish the same match. No global reset or early return.
5. Transport loss with no reason: close/stop the test server or simulate loss in a
   separate session. After normal transport timeout, expect generic lost-connection
   notice and usable MainMenu. Test loss during lobby-to-game scene loading and during
   match return: one local recovery, no stale late-loaded scene or duplicate manager.
6. Intentional leave: use existing lobby Back/Return-to-MainMenu, then manually join
   again. No kick/loss notice; host roster updates. Explicit host Return stops its
   own hosted session sensibly. Application Quit must not trigger a new menu load.
7. Confirm local-host anti-cheat violations still produce HostReportOnly without
   sending a kick notice, stopping its server, or removing any remote client. Reset
   both modes to ReportOnly after isolated enforcement tests.

Executed: Editor/development/release-symbol and Editor-tool compiler checks; 18 pure
notice-state checks (exact messages, missing/unknown/unauthenticated reasons, repeated
callbacks, scene persistence, dismissal, attempts and intentional leaves); existing
31 integrity and 33 movement checks passed. Release metadata still excludes F7/F8/F9.
These do **not** execute Unity scene loading, FishNet-generated serialization, UI
interaction or real disconnects. All manual tests above remain pending for this change.
