# NAV MCP Field Notes (Tarneeb Prototype, macOS)

Findings from using NAV MCP to build a complete game. See section 3A of the plan for when and how to log.

## Tally

- NAV MCP calls (approx): **~410** (259 in the daemon audit log, which records writes and scripts; plus ~150 reads such as `compile.errors`, `console.read`, `scene.query`, `unity_projects`, `unity_skill`, `unity_find`)
- Batches run: **136** `unity_batch` calls (up to 30 ops each; roughly 3,000 ops in total, most of them repeated rebuilds of the same scenes by `Tools/build_scenes.py`)
- `unity_script` uses: **78** (Player Settings, sprite import and references, TMP import, URP asset versions, scene clearing, one-off inspections; ~40 of them are the generator's own sprite/scale passes)
- Issues by type: Bug 5 | Missing operation 1 (7 items) | Guidance 4 | Performance 0 | macOS 0 | UX 5 | Works well 5
- Errors returned by NAV MCP: 8 of 259 audited calls (3 `E_ASSET_NOT_FOUND` from my sub-asset syntax guesses, 1 each `E_ENUM_INVALID`, `E_NOT_A_RECT`, `E_SCENE_DIRTY`, `E_TYPE_NOT_COMPONENT`, `E_TARGET_NOT_FOUND`); every one had an actionable message
- WebGL validation over time (errors / warnings): first run 0 / 6 (audio resampling on the SFX) -> after `audio.setImportSettings` 0 / 0 (stayed 0 / 0 for the final build)

## Setup on macOS

- NAV MCP version: 1.0.2 (arm64 dmg, sha256 verified)
- macOS version / chip: Darwin 25.6.0, Apple Silicon (arm64)
- Unity version: project was created on 6000.6.3f1 (fails, see entries); switching to 6000.3.25f1 per plan
- Install notes (Gatekeeper, first launch, linking, client connection): downloaded with `gh release download` (no quarantine flag), copied to /Applications, `open` launched fine, daemon healthy on :8730 with no Gatekeeper prompt. Linked by hand (manifest.json edit) because no GUI control was available.

## Entries

<!--
### [date] Short title
- Type: Bug | Missing operation | Guidance | Performance | macOS | UX | Works well
- What I tried:
- What happened:
- Expected:
- Workaround used:
- Suggested fix for NAV MCP:
- Severity: Blocker | Annoying | Minor
-->

### 2026-09-29 Package does not compile on Unity 6000.6.3f1
- Type: Bug
- What I tried: linked com.umcp.agent (from the 1.0.2 app bundle) into a fresh Unity 6000.6.3f1 project
- What happened: ~50x error CS0619, e.g. `'Object.GetInstanceID()' is obsolete: 'Use GetEntityId instead.'` (also InstanceIDToObject, objectReferenceInstanceIDValue, *EventArgs.instanceId in UmcpMirror.cs). The agent never connects; `unity_projects` shows no editors. Unity stayed idle with no visible error to the app.
- Expected: README says Unity 6000.0 or newer works
- Workaround used: install Unity 6000.3.25f1 (plan's version) and reopen the project there
- Suggested fix for NAV MCP: use `#if UNITY_6000_2_OR_NEWER` / EntityId APIs (or a compat shim) for instance IDs; add a Unity-version CI matrix including latest 6000.x; the app should show a compile-error state for the linked project instead of silently "Click the Unity window"
- Severity: Blocker (on 6000.6)

### 2026-09-29 Hand-linking a project does not trigger Unity re-resolve
- Type: UX
- What I tried: edited manifest.json while Unity was open, brought Unity to front with `open`
- What happened: Unity re-resolved the package list only after focus, then had to compile; with no GUI access I could not tell it was stuck
- Suggested fix: expose a CLI (`umcpd link <path>`) and a status endpoint that reports package-resolve/compile state per project
- Severity: Minor

### 2026-09-29 No GUI/computer-use available: linking and client connection done by hand
- Type: UX
- What I tried: link the project and connect the MCP client through the NAV MCP app (Link a project..., Connect)
- What happened: the agent session had no desktop-control tools, so the app could not be clicked. I edited `Packages/manifest.json` by hand (the README documents this) and had to bring Unity to the front with `open <Unity.app>` to make it re-resolve packages. AppleScript (`osascript ... System Events`) hung on the Automation permission prompt.
- Expected: a headless way to link a project and to register the MCP client
- Workaround used: hand-edited manifest; talked to the daemon over HTTP (`POST /mcp`, JSON-RPC, `Authorization: Bearer <token file>`, `Accept: application/json, text/event-stream`, then `initialize` + `notifications/initialized`) using a 60-line Python client (`Tools/nav_client.py`)
- Suggested fix for NAV MCP: `umcpd link <path>` / `umcpd connect claude-code` CLI verbs; a `unity_projects` field that says whether the package resolved, compiled or failed to compile (I lost ~30 minutes to an Editor that silently sat on 40+ compile errors)
- Severity: Annoying

### 2026-09-29 Works well: daemon over HTTP, bearer token, audit log
- Type: Works well
- The MCP endpoint is plain streamable HTTP, the token file is easy to find, and `~/Library/Application Support/UnityMCP/logs/audit.jsonl` records every tool call (tool, ok, ms, args). That made the tally in this file cheap to produce.

### 2026-09-29 Profile can only be changed in the GUI
- Type: UX
- What I tried: switch `standard` -> `full` (needed for `unity_script`, `scene.save`)
- What happened: no settings file or CLI flag I could find for the app; I stopped the app and started `umcpd --profile full` from `/Applications/NAV MCP.app/Contents/Resources`
- Suggested fix: persist the profile in `~/Library/Application Support/UnityMCP/config.json` and document it; `unity_projects` could accept `profile` (with the same confirmation gate as the app)
- Severity: Minor

### 2026-09-29 Works well: agent survives a daemon restart
- Type: Works well
- After killing the app and starting `umcpd` by hand, the Unity agent reconnected within seconds, no Editor action needed.

### 2026-09-29 component.set with a texture path leaves a Sprite field null but reports success
- Type: Bug
- What I tried: `component.set { target, type: "CardArt", props: { heart: "Assets/.../suit_heart.png" } }` and the same for `Image.sprite`
- What happened: result says `"set": 1`, `component.get` shows the field still null. The path resolves to the Texture2D main asset, which cannot be assigned to a Sprite field. `path#name`, `path/name` and `path:name` all give `E_ASSET_NOT_FOUND`.
- Expected: either resolve to the Sprite sub-asset when the field type is Sprite, or fail loudly with E_TYPE_MISMATCH
- Workaround used: one `unity_script` that loads the Sprite and assigns it (52 Images per call)
- Suggested fix: resolve by field type; report `warnings` when the assigned value is null after the write
- Severity: Annoying (silent)

### 2026-09-29 gameobject.create under a UI parent makes a plain Transform
- Type: Bug
- What I tried: `gameobject.create { name, parent: "GameCanvas" }` then `ui.rect`
- What happened: `E_NOT_A_RECT`; the error hint says "gameobject.create with a parent under one [a Canvas]" works.
- Workaround used: `component.add RectTransform` before `ui.rect` for empty containers (Graphic components convert the transform automatically)
- Suggested fix: create a RectTransform when the parent has one
- Severity: Annoying

### 2026-09-29 gameobject.create keeps the world transform when parenting (compensating scale)
- Type: Bug
- What I tried: built a UI hierarchy under a Content object that a `[ExecuteAlways]` script had scaled to 0.18
- What happened: children created afterwards got a localScale of ~5.4 (world-space preserved), so the hand cards rendered 3x too large at runtime. Took a Play mode dump to find it.
- Expected: for UI parents, `SetParent(parent, false)`
- Workaround used: reset all local scales with a script after each build (`normalize_scales` in `Tools/build_scenes.py`) and removed ExecuteAlways
- Suggested fix: `worldPositionStays` arg on `gameobject.create` and `gameobject.setParent`, default false under a RectTransform parent
- Severity: Annoying

### 2026-09-29 Type names resolve to the wrong namespace
- Type: Guidance
- What I tried: `component.add { type: "EventSystem" }`
- What happened: `E_TYPE_NOT_COMPONENT: 'EventSystem' resolves to UnityEngine.U2D.Interface.EventSystem` (an internal type of the 2D package)
- Workaround used: fully qualified `UnityEngine.EventSystems.EventSystem`
- Suggested fix: prefer UnityEngine.Component-derived types when several types share a name; list candidates in the error
- Severity: Minor

### 2026-09-29 Enum names must match the display name
- Type: Guidance
- What I tried: `component.set Canvas { renderMode: "ScreenSpaceOverlay" }`
- What happened: `E_ENUM_INVALID` with `didYouMean: ["Screen Space - Overlay", ...]`. The error and the suggestion made it a one-step fix, which is good, but the docs say "enum names".
- Suggested fix: accept the C# identifier too
- Severity: Minor

### 2026-09-29 Non-atomic batch stops at the first failure and leaves the earlier ops applied
- Type: UX
- What I tried: 30-op batches, `atomic` left at default
- What happened: a failure at op 12 leaves ops 1-11 applied and 13-30 skipped, so a scripted rebuild has to be idempotent. I made the generator clear the scene first.
- Suggested fix: make `atomic: true` the default for `unity_batch` (the description promises "one undo group"), or return the remaining ops so the caller can resume
- Severity: Minor

### 2026-09-29 build.scenes changes are not persisted until the assets are saved
- Type: Bug
- What I tried: `build.scenes add/remove`, then closed the Editor (`kill`) for a batch-mode test run
- What happened: `EditorBuildSettings.asset` still listed only `SampleScene` and the PlayMode tests said "Scene 'Game' couldn't be loaded because it has not been added to the active build profile". After `AssetDatabase.SaveAssets()` (via `unity_script`) it was written.
- Suggested fix: call `AssetDatabase.SaveAssets()` (or `EditorBuildSettings.scenes =` + `SaveAssets`) at the end of `build.scenes`
- Severity: Annoying

### 2026-09-29 Missing operations (all done with unity_script)
- Type: Missing operation
- Player Settings (WebGL compression format, decompression fallback, template, managed stripping, orientation, company/product name)
- Texture/Sprite import settings (texture type = Sprite, 9-slice border, mipmaps)
- Importing TMP Essential Resources (`TMP_PackageResourceImporter.ImportResources(true,false,false)`)
- Assigning Sprite / AudioClip sub-assets (see above)
- Running EditMode / PlayMode tests, and reading the results. The plan says to ask the developer to use the Test Runner if there is no operation; I ran tests headless from the CLI instead (`Tools/run_tests.sh`), which needs the Editor closed.
- Building a player. `unity_script` cannot run `BuildPipeline.BuildPlayer` (blocks the main thread for minutes and would look like a modal dialog), so builds run from the CLI with `-executeMethod`.
- Capturing the Game view / a screenshot (I rendered a camera to PNG from a PlayMode test)
- Severity: Annoying (each is a candidate operation)

### 2026-09-29 ui.layoutReport findings depend on the Game view size
- Type: Guidance
- What I tried: `ui.layoutReport` on a 1080x1920 canvas in edit mode
- What happened: it evaluated against a 691x343 Game view and reported the top seat as "entirely outside the canvas". Real layout was correct.
- Suggested fix: let the caller pass a reference resolution / target resolution, or evaluate against CanvasScaler.referenceResolution
- Severity: Minor

### 2026-09-29 Calls block for 180 s when no Editor is connected
- Type: UX
- What I tried: sent operations while I had intentionally closed the Editor for a batch-mode test run
- What happened: each call held for the full 180 s hold window before `E_NO_EDITOR`; a chained script lost 6 minutes. Correct for domain reloads, wasteful when the Editor is simply closed.
- Suggested fix: fail fast when `unity_projects` says the process is gone (`alive: false`), keep the hold window only for `reloading`
- Severity: Annoying

### 2026-09-29 Works well: unity_batch, scene.query, compile.errors, build.validateTarget, unity_script
- Type: Works well
- 30-op batches complete in ~100-500 ms; building the whole game scene (about 130 GameObjects, 500+ ops) took ~4 s of NAV time.
- `compile.errors` and `console.read` were the reliable "is it compiling" checks after every script change; `unity_script` compiles in ~100 ms and made bulk edits trivial.
- `build.validateTarget WebGL` found 6 real warnings (audio resampling) before any 25-minute build, and `audio.setImportSettings` fixed all six in one call. Errors/warnings: 0 / 6 -> 0 / 0.

### 2026-09-29 NAV MCP agent is included in the batch-mode build
- Type: Works well
- The agent stays quiet in `-batchmode -executeMethod` runs and in CI-style test runs (a single "agent up" log line).

## Prioritized improvements

1. **Compile on the newest Unity 6 and say so when it does not.** 6000.6 removed `Object.GetInstanceID()` and friends (`CS0619`); use `EntityId` APIs behind `#if UNITY_6000_x`, add a Unity-version CI matrix, and make `unity_projects` report "package failed to compile: N errors" instead of a silent non-connection. (Blocker; cost about 30 minutes plus a 6.3 install.)
2. **`component.set` must not silently succeed.** Resolve Sprite/AudioClip sub-assets from a file path (or fail with a type-mismatch), and report `warnings` when the value is still null after the write.
3. **Parenting under a UI parent.** `gameobject.create` should make a RectTransform and use `worldPositionStays = false` (it compensated a scaled parent and inflated a whole hierarchy 5x). Expose `worldPositionStays` on `gameobject.setParent` too.
4. **Persist what you change.** `build.scenes` (and other settings writers) should call `AssetDatabase.SaveAssets()`; and fail fast (not after the 180 s hold) when the Editor process is gone.
5. **Missing operations that every project needs:** Player Settings (WebGL compression/template/stripping/orientation), texture/sprite import, run tests + read results, build a player (as a detached process), screenshot / Game view capture, import TMP essentials.
6. **Friendlier surface:** `atomic` batches by default; type-name disambiguation (prefer `Component` types) and C# enum identifiers in `component.set`; a target resolution for `ui.layoutReport`; `umcpd link/connect/profile` verbs and a persisted profile so the app is optional for headless use.
