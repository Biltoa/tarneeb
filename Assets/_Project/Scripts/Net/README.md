# Online multiplayer (implemented, disabled)

Status: **complete and covered by headless tests, but switched off** (`GameSettings.OnlineEnabled = false`, the menu shows
"Play Online (coming soon)") until a Photon account and PUN 2 App ID are connected.

## Model

- **Master client is authoritative.** It owns the only real `TarneebEngine`, runs the bots, validates every request and
  broadcasts the *accepted command* (not the resulting state).
- **Clients keep a replica engine** (`TarneebEngine.StartReplicaGame`). It applies only the master's accepted commands,
  learns other players' cards as they are played, and emits the same events as the master, so the same `TableView`,
  animation queue and `LocalHumanController` render an online game with no online-specific UI code.
- **Hidden hands** are sent as `Hand` messages targeted at one actor (`TargetActors`), never broadcast. A test asserts
  that no hand packet is ever addressed to `All`.
- **Messages** (`NetCodes`): `Request` (client to master, `[kind, arg]`), `Command` (master to all, `[kind, seat, arg]`),
  `Hand` (master to one, 13 bytes), `Init` (master to one, `[seat]`). A card is one byte (`suit * 13 + rank - 2`).
  They go through `PhotonNetwork.RaiseEvent`, not PhotonViews.
- **Seats, not players**: `SeatController` is the only thing that differs. `LocalHumanController`, `BotController`,
  `RemotePlayerController` (master side, with the turn timer: on timeout the bot plays), `NetSeatController` and
  `NetLocalController` (client side).
- **Disconnects**: a non-master player leaving is replaced by a bot immediately (the bot is caught up on the round's
  events). If the master leaves the match ends with a message; host migration is out of scope (known limitation).
- **Turn timer**: `RulesConfig.TurnTimerSeconds` (15) is applied to remote humans on the master.

## Files

| File | Purpose |
|---|---|
| `INetTransport.cs` | Reliable ordered channel abstraction |
| `NetProtocol.cs` | Event codes and compact codec |
| `NetHost.cs` / `NetClient.cs` | Master and replica logic |
| `NetSeatControllers.cs` | Client-side seat controllers |
| `LoopbackTransport.cs` | In-process hub used by `Tests/EditMode/NetTests.cs` |
| `OnlineSession.cs` | Builds the host/client `MatchSetup` for the Game scene |
| `Photon/` | `PhotonTransport` and `PhotonLobby`, compiled only when PUN 2 defines `PHOTON_UNITY_NETWORKING` |

## Turning it on

1. Create a Photon account, make a PUN application and copy the App ID.
2. Import **PUN 2 - FREE** (Asset Store). If it fails on Unity 6, use Photon Fusion 2 (Shared Mode) instead and
   reimplement only `INetTransport`.
3. Paste the App ID in `PhotonServerSettings`, set the fixed region (the lobby uses `eu`).
4. Set `GameSettings.OnlineEnabled = true`, add a lobby screen that calls `PhotonLobby.QuickMatch()` /
   `CreatePrivate(code)` / `JoinPrivate(code)` and, when the room is ready, sets `OnlineSession.Transport` (a
   `PhotonTransport` instance) and loads the `Game` scene.
5. Test with two browser tabs plus one Editor.
