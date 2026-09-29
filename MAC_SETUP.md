# MacBook Setup Checklist

Do these once before handing the project to the agent.

## 1. Tools
- [ ] Install Unity Hub, then Unity 6000.3.x (same version as the Windows machine) with the **WebGL Build Support** module.
- [ ] Install Git (`xcode-select --install` gives you Git and command line tools).
- [ ] Install Claude Code (or your MCP-capable agent) and sign in.
- [ ] Install NAV MCP from https://github.com/Biltoa/nav-mcp-unity/releases/latest (.dmg). Drag to Applications. First launch: right-click > Open.
  - This is the first run on a physical Mac. Write down anything odd in NAV_MCP_FIELD_NOTES.md > Setup on macOS.

## 2. Project
- [ ] Create a new Unity project named `Tarneeb` (Universal 2D template).
- [ ] Switch the build target to WebGL (File > Build Profiles).
- [ ] `git init` in the project folder. Copy in the `.gitignore` from this folder.
- [ ] Copy these files into the project root (next to `Assets/`):
  - `CLAUDE.md`
  - `TARNEEB_PROTOTYPE_PLAN.md`
  - `NAV_MCP_FIELD_NOTES.md`
  - `DEVLOG.md`
- [ ] First commit.

## 3. Connect NAV MCP
- [ ] Open NAV MCP > Projects > Link a project > choose the `Tarneeb` folder.
- [ ] Connections > connect Claude Code. Restart Claude Code.
- [ ] Keep permission mode on `standard`. Switch to `full` only when the agent asks for `unity_script`.
- [ ] In Claude Code, from the project folder, ask: "Run unity_projects." It should show the project as healthy with the Editor connected.

## 4. Accounts for later days
- [ ] itch.io account (Day 3 deploy).
- [ ] Photon account and a PUN 2 App ID (Day 4).
- [ ] New public GitHub repo for the project.

## 5. Kick off
Tell the agent: "Read CLAUDE.md and the plan, then start Day 1."
