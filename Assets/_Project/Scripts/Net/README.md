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

## Chat and emoji

`ChatService` (Game) is the only thing the UI talks to. Offline it echoes to the local seat and `BotChatter` lets a bot answer
sometimes. Online, `NetChat` takes over: every player sends `NetCodes.Chat` to the master, which infers the seat from the sender
(nobody can speak as another seat), rate-limits (0.8 s per seat), re-cleans the text and relays `NetCodes.ChatOut` to everyone
including itself. Text is 60 characters, Latin only (the font has no Arabic glyphs yet), rich-text brackets are stripped, links and
a small word list are masked (`ChatText`). Emoji are 12 sprites cut from `Resources/Emoji/emoji_atlas.png` (`Tools/gen_emoji.py`).

## Lobby

The Menu talks to `ILobby` (Game/Lobby.cs), never to Photon. `PhotonLobby` (compiled only with PUN) implements it: Quick Match
(random public room, max 4), Private Room (5-letter code), waiting room, master presses Start (bots fill empty seats; a full table
auto-starts after 3 s). Starting sets a room property that every client sees, then each loads the Game scene. `PhotonTransport`
is created when the room is joined and buffers events until the Game scene subscribes. `LoopbackLobby` is the same interface
in-process; `OnlineFlowTests` drives Menu window, host match, chat and leaving with it.

## Turning it on

1. Import **PUN 2 - FREE** from the Asset Store (Package Manager, My Assets). It adds the `PHOTON_UNITY_NETWORKING` define, which
   compiles `Net/Photon/`.
2. In the PUN Setup Wizard paste the App ID. It is stored in `Assets/Photon/.../PhotonServerSettings.asset`, which is git-ignored so
   the ID is not committed (it is still visible in any WebGL build, like every Photon client ID).
3. Nothing else: `GameSettings.OnlineEnabled` is true when a lobby backend is registered (`GameSettings.OnlineAllowed` is the kill
   switch), and the "Play Online" button opens the lobby window.
4. Test with two browser tabs (or the Editor plus one tab).

Free-plan note: the free 20 CCU plan is labelled "for development only" by Photon; a public site should move to a paid plan.
