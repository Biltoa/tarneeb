# NAV MCP Field Notes (Tarneeb Prototype, macOS)

Findings from using NAV MCP to build a complete game. See section 3A of the plan for when and how to log.

## Tally

- NAV MCP calls (approx): 0
- Batches run: 0
- `unity_script` uses: 0
- Issues by type: Bug 0 | Missing operation 0 | Guidance 0 | Performance 0 | macOS 0 | UX 0 | Works well 0
- WebGL validation over time (errors / warnings): 

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

## Prioritized improvements (fill in on Day 5)

1.
