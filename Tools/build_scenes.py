#!/usr/bin/env python3
"""Builds the Game and Menu scenes through NAV MCP (unity_script / unity_run). Editor work is driven by NAV MCP rather than
hand-edited YAML (see TARNEEB_PROTOTYPE_PLAN.md section 3A).

Game scene: the Figma design (Tools/design/gameplay.figma.json) is imported with the Krykftn Figma To Unity importer
(Tarneeb.EditorTools.FigmaBundleImport) and then reshaped into the game's hierarchy by GameSceneAssembler.
Menu scene: built by MenuSceneAssembler from the same design pieces.
Usage: python3 Tools/build_scenes.py [game|menu|all]"""
import json, sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nav_client as nav

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
FIGMA = os.path.abspath(os.path.join(ROOT, 'Tools', 'design', 'gameplay.figma.json'))

def run_script(code):
    r = nav.call('unity_script', {"code": code})
    txt = r['result']['content'][0]['text']
    d = json.loads(txt)
    if not d.get('ok'):
        print('SCRIPT FAILED', txt[:2500]); sys.exit(1)
    return d['data'].get('value')

def active_scene_path():
    return run_script('return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;')

def open_scene(path, create=False):
    if active_scene_path() == path:
        print('scene already active', path); return
    tool, args = ('scene.create', {"path": path, "setup": "defaultGameObjects", "mode": "single"}) if create else ('scene.open', {"path": path, "mode": "single"})
    r = nav.call('unity_run', {"tool": tool, "args": args})
    print(r['result']['content'][0]['text'][:200])

def clear_scene():
    run_script('foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()){ if(go.name!="Main Camera") Undo.DestroyObjectImmediate(go);} return 1;')

def import_figma():
    print('figma import:', run_script('return Tarneeb.EditorTools.FigmaBundleImport.Import(@"%s");' % FIGMA))
    for _ in range(120):
        time.sleep(2)
        res = run_script('return Tarneeb.EditorTools.FigmaBundleImport.LastResult;')
        if res != 'running':
            print('figma import:', res)
            if res.startswith('FAILED'): sys.exit(1)
            return
    print('figma import timed out'); sys.exit(1)

def save():
    r = nav.call('unity_run', {"tool": "scene.save", "args": {"confirm": True}})
    print('save', r['result']['content'][0]['text'][:200])

def build_game():
    open_scene('Assets/_Project/Scenes/Game.unity')
    clear_scene()
    import_figma()
    print(run_script('return Tarneeb.EditorTools.GameSceneAssembler.Assemble();'))
    save()

def build_menu():
    open_scene('Assets/_Project/Scenes/Menu.unity')
    clear_scene()
    import_figma()
    print(run_script('return Tarneeb.EditorTools.MenuSceneAssembler.Assemble();'))
    save()

if __name__ == '__main__':
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    if what in ('game', 'all'): build_game()
    if what in ('menu', 'all'): build_menu()
