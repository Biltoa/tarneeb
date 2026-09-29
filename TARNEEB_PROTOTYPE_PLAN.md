# Tarneeb Prototype: Technical Plan

## 1. Goal

Build a small but polished Tarneeb (classic Levantine trick-taking card game) prototype in Unity, playable in the browser as a WebGL build, in 3 to 5 working days.

The prototype is a portfolio piece for a job application to a mobile card game studio. It must show four things:

1. Clean, testable, production-minded C# architecture (not a throwaway hack).
2. A complete, fun, mobile-friendly game loop that feels finished.
3. A disciplined AI-assisted workflow, where AI output is validated by tests and review.
4. A real-world field test of NAV MCP on a complete game project, on macOS. Every friction point, bug, and missing capability found in NAV MCP is a valuable output of this project, not a distraction.

Priority order if time runs short: **single player vs bots (must) > WebGL hosting (must) > polish (should) > online multiplayer (should) > Arabic localization (could)**.

The single-player version must be complete, deployed, and playable by the end of Day 3, before any multiplayer work starts. Multiplayer is added on top and must never break single player.

## 2. Tech Stack

- **Engine:** Unity 6 (6000.3.x, matching the developer's other machine), URP 2D, portrait orientation, WebGL build support module installed.
- **Development machine:** macOS (MacBook). All paths, shortcuts, and instructions must work on macOS.
- **Editor automation:** NAV MCP (the developer's own open-source MCP server for Unity, github.com/Biltoa/nav-mcp-unity). See section 3A.
- **Language:** C#.
- **UI:** uGUI + TextMeshPro. Reference resolution 1080x1920, Canvas Scaler "Scale With Screen Size", match 0.5. Must also look acceptable in a landscape desktop browser (letterbox or adaptive layout).
- **Tweening:** DOTween (free version) for dealing, playing, and collecting cards.
- **Networking (phase 2):** Photon PUN 2 (free). PUN 2 supports WebGL through secure WebSockets. If PUN 2 fails to import or run on Unity 6, stop and report; the fallback is Photon Fusion 2 in Shared Mode.
- **Testing:** Unity Test Framework (NUnit), Edit Mode tests for the rules engine.
- **Card art:** Kenney "Playing Cards Pack" (CC0) or procedurally drawn cards. Original visual style only. Do not copy any branding, art, or UI from existing commercial Tarneeb apps.
- **AI agent:** Claude Code (or another MCP-capable agent) with NAV MCP connected.
- **Version control:** Git, small focused commits with clear messages.

## 3. Architecture

Strict separation between pure game logic and Unity presentation.

```
Assets/
  _Project/
    Scripts/
      Core/            # Pure C#, NO UnityEngine references (own .asmdef, "No Engine References" ON)
        Cards/         # Card, Suit, Rank, Deck, Hand
        Rules/         # TarneebRules, RulesConfig, validation
        State/         # GameState, RoundState, TrickState, phases
        Bots/          # IBot, HeuristicBot, HandEvaluator, CardTracker
        Events/        # Game events emitted by the engine
      Game/            # Unity layer: GameController, seat mapping, turn flow, timers
      Presentation/    # CardView, HandView, TableView, BidPanel, TrumpPicker, ScoreBoard, animations
      Net/             # Photon layer (phase 2), own .asmdef
      Config/          # ScriptableObjects: RulesConfigAsset, BotConfigAsset
    Tests/
      EditMode/        # NUnit tests for Core
    Scenes/            # Boot, Menu, Game
    Art/ Audio/ Prefabs/
```

### Key principles

- **The engine is the single source of truth.** `TarneebEngine` takes commands (`PlaceBid`, `Pass`, `ChooseTrump`, `PlayCard`), validates them against the rules, updates `GameState`, and emits events (`CardsDealt`, `BidPlaced`, `TrumpChosen`, `CardPlayed`, `TrickWon`, `RoundEnded`, `GameEnded`). It returns a result type with an error reason for illegal commands, never throws for normal illegal moves.
- **Presentation only reacts to events.** Views never mutate game state directly. Animations are queued so events play out in order even if the engine resolves instantly.
- **Deterministic and seedable.** All shuffling uses an injected `System.Random` seed so any game can be replayed exactly for debugging and tests.
- **Seats, not players.** The engine works with seat indices 0 to 3. Seat 0 and 2 are Team A, seats 1 and 3 are Team B. The Game layer maps seats to controllers: `LocalHumanController`, `BotController`, `RemotePlayerController`. This is what makes multiplayer and bot-fill easy later.
- **Rules are data.** Every variant choice below lives in a `RulesConfig` (plain C# class, wrapped by a ScriptableObject in the Unity layer).

## 3A. NAV MCP: How the Agent Works With the Unity Editor

This project doubles as the first full game built with NAV MCP, and the first time NAV MCP runs on a physical Mac (the macOS build is CI-tested but has never been used on real hardware). Treat NAV MCP as both a tool and a test subject.

### Setup (developer does this once, see MAC_SETUP.md)

1. Install NAV MCP from the latest GitHub release (.dmg). First launch: right-click the app, choose Open (unsigned build).
2. Create the Unity project, then in NAV MCP: Projects > Link a project.
3. Connections > connect Claude Code, restart Claude Code.
4. The agent's first action in every session: call `unity_projects` and confirm the project is linked, healthy, and the Editor is connected.

### Division of labor

- **C# source files:** The agent writes and edits `.cs`, `.asmdef`, and other text files directly on disk, as usual.
- **Everything inside the Editor goes through NAV MCP:** creating scenes, GameObjects, prefabs, UI Canvas hierarchies, components and their values, materials, sprite import settings, sprite atlases, audio import settings, Player Settings, build settings, reading console errors after compilation, and validating the WebGL target.
- **Do not hand-edit** `.unity`, `.prefab`, `.asset`, or `.meta` files as text. If NAV MCP cannot do something, log it (see below) and ask the developer to do it manually in the Editor.

### How to use the six tools

- **`unity_find` first.** Never guess operation ids or argument shapes. Search for the operation, then load its area with `unity_skill` to get the schema.
- **`unity_run`** for single operations.
- **`unity_batch`** for any multi-step Editor change (e.g. building the whole table layout, creating the bid panel with its 8 buttons). Batches run in one Editor update and create one named undo step. Give each batch a clear name, e.g. "Build BidPanel UI".
- **`unity_script`** (C# code mode) only when no operation covers the task. It needs the `full` permission mode; the default is `standard`. When it is needed, stop and ask the developer to switch modes, and log why it was needed.
- **`unity_projects`** to check health, compile state, and Editor connection, especially after script changes and domain reloads.

### Required NAV MCP checkpoints

- After every batch of script changes: wait for compilation, then read the console through NAV MCP and fix all errors before continuing.
- Before every WebGL build: run NAV MCP's WebGL validation and fix every error it reports. Record the error/warning counts over time in the field notes.
- If NAV MCP exposes a test runner operation, use it to run EditMode tests. If it does not, log that as a gap and have the developer run tests from the Test Runner window.

### NAV_MCP_FIELD_NOTES.md (required, living document)

Log an entry whenever any of these happen:

- An operation fails, errors, times out, or returns something wrong or confusing.
- Guidance from `unity_skill` is unclear, wrong, or missing a needed argument.
- `unity_find` does not surface the right operation on the first try.
- A task needed `unity_script` because no operation covered it (these are candidate new operations).
- Behavior during compilation, domain reload, Play mode, or modal dialogs is surprising.
- Anything macOS-specific: install, Gatekeeper, permissions, paths, performance, the desktop app UI.
- Something works notably well (these are useful for the README too).

Entry format:

```
### [date] Short title
- Type: Bug | Missing operation | Guidance | Performance | macOS | UX | Works well
- What I tried: (tool, operation id, key arguments)
- What happened: (exact error text or result)
- Expected:
- Workaround used:
- Suggested fix for NAV MCP:
- Severity: Blocker | Annoying | Minor
```

Also keep a running tally at the top of the file: total NAV MCP calls (approximate), batches run, `unity_script` uses, issues found by type.

### Rules

- **Do not modify NAV MCP's source from this project.** NAV MCP fixes happen separately in its own repository. This project only records findings.
- If NAV MCP is blocking progress for more than ~20 minutes, log it, fall back to asking the developer to do the Editor step manually, and move on. The game deadline comes first.

## 4. Game Rules (Classic Tarneeb)

Implement exactly this; each item marked (config) must be a `RulesConfig` field with the default shown.

- 4 players, 2 fixed partnerships, partners sit opposite.
- Standard 52-card deck, rank order A high down to 2. 13 cards dealt to each player.
- Play direction: counterclockwise (config: `Counterclockwise`).
- Dealer rotates each round in play direction. First dealer is random.
- **Bidding:**
  - Starts with the player after the dealer and goes around in play direction.
  - Each bid is a number of tricks, minimum 7 (config), maximum 13.
  - Each bid must be higher than the current highest bid.
  - A player may pass. A player who passes cannot bid again that round.
  - Bidding ends when three players have passed after a bid. The highest bidder becomes the declarer.
  - If all four pass, the hand is thrown in and redealt by the next dealer (config: `RedealOnAllPass = true`).
- **Trump:** The declarer chooses the trump suit (the "tarneeb") after winning the bid. Trump is announced to everyone.
- **Play:**
  - The declarer leads the first trick (config: `DeclarerLeadsFirstTrick = true`).
  - Players must follow the suit led if they can.
  - If they cannot follow suit, they may play any card, including a trump.
  - The trick is won by the highest trump played, or if no trump, the highest card of the led suit.
  - The trick winner leads the next trick.
- **Scoring per round (defaults):**
  - If the declaring team takes at least as many tricks as bid: declaring team scores the number of tricks it took. Defending team scores 0 (config: `DefendersScoreWhenContractMade = false`).
  - If the declaring team fails: declaring team loses the bid amount (scores can go negative). Defending team scores the number of tricks it took.
  - Bid of 13 made (all tricks, "kaboot"): declaring team scores 16 instead (config: `KabootBonusScore = 16`).
- **Game end:** First team to reach 41 points wins (config: `TargetScore = 41`). If both teams cross it in the same round, the declaring team wins.

### Required rule tests (EditMode)

Write these before or alongside the engine:

- Deck has 52 unique cards; dealing gives 13 unique cards per seat.
- Bid validation: below minimum, not higher than current bid, bidding after passing, bidding out of turn.
- Bidding end conditions, including the all-pass redeal.
- Follow-suit enforcement (illegal card rejected when holding the led suit).
- Trick winner: led suit only, single trump, multiple trumps, off-suit discards ignored.
- Scoring: contract made, contract failed, negative scores, kaboot, game-end tie-breaker.
- Seeded determinism: same seed produces the same deal.
- **Headless simulation:** run 10,000 full bot-vs-bot games with random seeds. Assert no exceptions, no illegal moves, every round has exactly 13 tricks, total tricks per round = 13, and every game terminates. Report timing. (This number goes in the README.)

## 5. Bot AI

Heuristic bots, no ML. Implement behind `IBot` so difficulty levels can be swapped.

- **CardTracker:** Tracks every card played this round, which suits each seat has shown void in, and which high cards remain.
- **Hand evaluation for bidding:**
  - Count likely tricks: Aces ~1, protected Kings ~0.7, protected Queens ~0.3, plus extra value for long suits (each card beyond 4 in the best suit ~0.5 if it becomes trump).
  - Pick the best candidate trump as the longest/strongest suit.
  - Bid only if the estimate plus a partner allowance (config, e.g. +2) reaches the next legal bid. Otherwise pass. Never bid above estimate + allowance.
- **Trump selection:** The suit used in the evaluation.
- **Card play heuristics:**
  - Leading: lead winners (top remaining card of a suit) first; as declarer, draw trumps if holding many; avoid leading into a known void of an opponent.
  - Following, partner currently winning the trick: play the lowest legal card.
  - Following, opponent winning: win with the cheapest card that beats it if possible; otherwise play lowest.
  - Void in led suit: trump with the lowest trump that wins, unless partner is already winning; otherwise discard the lowest card from the weakest side suit.
- Add a small think delay (0.4 to 0.9s, random) in the Unity layer only, never in the engine.
- Two difficulty presets via `BotConfig`: Easy (random legal move 30% of the time) and Normal.

## 6. Presentation and UX

Mobile-first portrait layout. The local player is always shown at the bottom, partner at the top, opponents left and right, regardless of absolute seat index.

- **Menu scene:** Play vs Bots, Play Online (phase 2), bot difficulty toggle, sound toggle.
- **Table:** Local hand fanned at the bottom, face up. Other hands shown as card backs with counts. Center area for the current trick. Each seat shows name, team color, and a turn indicator.
- **Card interaction:** Tap to select (card lifts), tap again or drag up to play. Illegal cards are dimmed during the local player's turn so the player can't make an illegal move.
- **Bidding panel:** Buttons 7 to 13 (only legal ones enabled) plus Pass. Show every player's bid as a speech bubble near their seat.
- **Trump picker:** Four suit buttons for the declarer; the chosen trump is shown permanently in a corner badge.
- **HUD:** Current contract (e.g. "Team A bid 9, trump ♠"), tricks won by each team this round, total scores.
- **Round summary:** Popup with contract, tricks taken, points gained/lost, running totals. Game-over screen with winner and a Play Again button.
- **Animations:** Deal (cards fly from center to seats), play (card moves from hand to center), trick collection (cards slide to winner's side). Keep each under 0.4s; add a speed setting if time allows.
- **Audio:** Card flip, card place, trick win, round win. Free/CC0 sounds only. Remember browsers block audio until the first user interaction.
- **Turn timer:** 15 seconds per human turn in online games (config). On timeout, the bot logic plays for that seat.

## 7. Online Multiplayer (Phase 2, Photon PUN 2)

Only start after the single-player WebGL build is deployed and working.

- **Model:** Master client is authoritative. It owns the `TarneebEngine` instance, runs the bots, validates every command, and broadcasts results. Other clients never run the engine's authoritative state; they render from events.
- **Rooms:** Quick Match (random room, max 4 players) and Private Room (short 4-6 character code to share with friends). Start button for the room creator; empty seats are filled with bots at start.
- **Messaging:** Use `PhotonNetwork.RaiseEvent` with custom event codes rather than PhotonViews for game logic.
  - Client to master: `RequestBid`, `RequestPass`, `RequestTrump`, `RequestPlayCard` (seat + payload).
  - Master to all: public events (`BidPlaced`, `TrumpChosen`, `CardPlayed`, `TrickWon`, `RoundEnded`, turn changes).
  - Master to one player: `YourHand`, sent with `RaiseEventOptions.TargetActors` so hidden cards are never sent to other clients.
- **Disconnects:** If a non-master player leaves, a bot takes over their seat immediately. If the master leaves, end the match gracefully with a message (host migration is out of scope; note it in the README as a known limitation).
- **Serialization:** Send compact data (card as a single byte: suit * 13 + rank). Register no custom types unless needed.
- **WebGL:** PUN 2 uses WSS automatically on WebGL. Test with at least two browser tabs plus one editor instance. Use the free Photon plan (20 CCU) and a fixed region to keep latency predictable.

## 8. WebGL Build and Hosting

- Compression: Brotli, with **Decompression Fallback ON** so it works on hosts that don't set the right headers.
- Target a download size under ~20 MB. Compress textures, use sprite atlases, strip unused code (managed stripping level Medium/High, verify nothing breaks).
- Use a WebGL template that fills the browser window and scales the canvas correctly on mobile browsers (test Chrome Android and Safari iOS).
- Host on itch.io (HTML5 game, "mobile friendly" and "fullscreen button" enabled). GitHub Pages is an acceptable backup.
- No threading, no `System.Threading.Tasks` background work, no synchronous file IO in WebGL.

## 9. Schedule (3 to 5 days)

**Day 1: Foundation and rules engine**
- Confirm NAV MCP connection (`unity_projects`). Project setup, folder structure, assembly definitions, packages.
- Start NAV_MCP_FIELD_NOTES.md and DEVLOG.md.
- Complete `Core`: cards, deck, bidding, trump, trick resolution, scoring, game flow.
- All rule tests passing. Basic `HeuristicBot`. Headless 10,000-game simulation passing.

**Day 2: Playable single player**
- Game scene built through NAV MCP batches: table layout, hand view, card interaction, bidding panel, trump picker, HUD.
- Wire engine events to presentation with an animation queue.
- Full game vs 3 bots playable from start to game over in the editor.

**Day 3: Polish and deploy (single player is DONE after this day)**
- Animations, sound, round summary, game-over flow, menu.
- NAV MCP WebGL validation, WebGL build, test on real phones, deploy to itch.io.
- README first draft.

**Day 4: Online multiplayer**
- Photon integration, rooms, bot-fill, authoritative master flow, private hands.
- Disconnect handling and turn timer.

**Day 5: Buffer and presentation**
- Bug fixing from playtests, performance check, optional Arabic localization (see stretch goals).
- Record a 60 to 90 second gameplay video. Finalize README.
- Clean up NAV_MCP_FIELD_NOTES.md into a prioritized list of NAV MCP improvements.

## 10. Stretch Goals (only if ahead of schedule)

- Arabic/English toggle with correct RTL text (Unity Localization + an RTL TextMeshPro solution). Tarneeb's audience is largely Arabic-speaking, so this is the highest-value stretch.
- Simple analytics: log events like `game_start`, `bid_placed`, `round_end`, `game_end` with a debug overlay showing session stats (to show a live ops mindset without a real backend).
- Hint button that asks the bot for the best legal card.
- Game speed setting and card back themes.

## 11. Out of Scope

Accounts, persistence beyond PlayerPrefs, monetization, chat, matchmaking by skill, host migration, native mobile builds, anti-cheat beyond keeping hidden hands off other clients.

## 12. Deliverables

1. Public GitHub repository with clean history.
2. Playable WebGL link (itch.io).
3. Gameplay video (60 to 90 seconds).
4. NAV_MCP_FIELD_NOTES.md with a prioritized improvement list for NAV MCP.
5. `README.md` containing:
   - What it is, link to play, screenshots or GIF.
   - Architecture overview (engine vs presentation vs networking) with a small diagram.
   - Rules implemented and which are configurable.
   - Test summary: number of unit tests and the headless simulation result (e.g. "10,000 bot games, 0 errors, X seconds").
   - **AI-assisted workflow section:** which AI tools were used, for what parts, how output was validated (tests, review, profiling), and concrete examples of AI suggestions that were rejected or corrected and why. The developer will write the final version of this section; the agent should keep notes in `DEVLOG.md` to support it.
   - **Built with NAV MCP section:** how the Editor work was driven through NAV MCP, the tally from the field notes, and the most important issues found and fixed in NAV MCP as a result. Link to the NAV MCP repo.
   - Known limitations and what would come next in production.
   - Time spent.

## 13. Working Rules for the AI Agent

- Work in the order of the schedule. Do not start multiplayer before single player is deployed.
- Start every session with `unity_projects`. Do all Editor work through NAV MCP (section 3A) and log every issue in NAV_MCP_FIELD_NOTES.md as it happens, not at the end of the day.
- Keep `Core` free of any `UnityEngine` reference. If a change needs one, it belongs in another layer.
- Write or update tests with every rules change. Run the full test suite before marking any task done.
- Never add a package, asset, or third-party code without stating why and confirming its license is free for commercial/portfolio use.
- Prefer small, reviewable commits. One feature or fix per commit.
- Maintain `DEVLOG.md`: after each work session, append what was built, decisions made and why, bugs found, and anything uncertain that the developer should review.
- If a rule is ambiguous or a requirement conflicts with another, stop and ask instead of guessing.
- Do not optimize prematurely, but profile once on Day 3 and fix anything that drops below 60 FPS in a mobile browser.
