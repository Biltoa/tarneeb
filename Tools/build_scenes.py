#!/usr/bin/env python3
"""Builds the Game/Menu scenes and the CardView prefab through NAV MCP (unity_batch / unity_run / unity_script).
Editor work is driven by NAV MCP rather than hand-edited YAML (see TARNEEB_PROTOTYPE_PLAN.md section 3A).
Usage: python3 Tools/build_scenes.py [game|menu|card|all]"""
import json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nav_client as nav

SPR = 'Assets/_Project/Art/Sprites/'
SFX = 'Assets/_Project/Audio/Sfx/'
WHITE = [1, 1, 1, 1]
GOLD = [0.96, 0.80, 0.36, 1]
DARK = [0.06, 0.09, 0.08, 1]
TEAL = [0.30, 0.80, 0.88, 1]
ORANGE = [0.98, 0.60, 0.28, 1]
FELT = [0.08, 0.30, 0.20, 1]

class Builder:
    def __init__(self):
        self.ops = []          # pending batch ops
        self.sprites = []      # (path, key) resolved later by one unity_script
        self.batches = 0

    def flush(self, name):
        if not self.ops: return
        for i in range(0, len(self.ops), 30):
            chunk = self.ops[i:i + 30]
            r = nav.call('unity_batch', {"ops": chunk, "undoName": name, "returns": "none"})
            res = r.get('result', r)
            txt = res['content'][0]['text']
            d = json.loads(txt)
            if not d.get('ok'):
                print('BATCH FAILED', name, txt[:1500])
                sys.exit(1)
            self.batches += 1
        self.ops = []

    def op(self, op, **args): self.ops.append({"op": op, "args": args})

    def node(self, name, parent, comps=(), size=None, pos=(0, 0), preset='center', stretch=None,
             color=None, sprite=None, raycast=None, text=None, fontSize=None, tcolor=None, align='center',
             bold=False, props=None, imgtype=None, wrap=None, rect=True):
        path = parent + '/' + name if parent else name
        self.op("gameobject.create", name=name, parent=parent) if parent else self.op("gameobject.create", name=name)
        is_graphic = any(c in ('Image', 'TextMeshProUGUI', 'RawImage') for c in comps)
        if rect and not is_graphic:
            self.op("component.add", target=path, type="RectTransform")
        for c in comps:
            self.op("component.add", target=path, type=c)
        if rect:
            if stretch is not None:
                self.op("ui.rect", action="stretch", target=path, margin=list(stretch))
            else:
                a = dict(action="anchor", target=path, preset=preset, position=list(pos))
                if size: a['size'] = list(size)
                self.op("ui.rect", **a)
        ip = {}
        if color is not None: ip['color'] = color
        if raycast is not None: ip['raycastTarget'] = raycast
        if imgtype: ip['type'] = imgtype
        if ip and 'Image' in comps:
            self.op("component.set", target=path, type="Image", props=ip)
        if sprite: self.sprites.append((path, sprite))
        if text is not None or 'TextMeshProUGUI' in comps:
            t = dict(action="set", target=path, text=text or "", fontSize=fontSize or 36, color=tcolor or WHITE, alignment=align)
            if wrap is not None: t['wrap'] = wrap
            self.op("ui.text", **t)
            tp = {"raycastTarget": False}
            if bold: tp['fontStyle'] = 'Bold'
            self.op("component.set", target=path, type="TextMeshProUGUI", props=tp)
        if props:
            for typ, p in props.items():
                self.op("component.set", target=path, type=typ, props=p)
        return path

    # helpers
    def text(self, name, parent, text, size, pos, fs, color=WHITE, align='center', bold=False, preset='center'):
        return self.node(name, parent, comps=("TextMeshProUGUI",), size=size, pos=pos, text=text, fontSize=fs, tcolor=color, align=align, bold=bold, preset=preset)

    def button(self, name, parent, size, pos, label, fs=44, color=GOLD, tcolor=DARK, sprite='panel'):
        p = self.node(name, parent, comps=("Image", "Button"), size=size, pos=pos, color=color, sprite=sprite, imgtype="Sliced")
        self.text("Label", p, label, size, (0, 0), fs, tcolor, bold=True)
        # stretch label to button
        self.op("ui.rect", action="stretch", target=p + "/Label", margin=[8, 4, 8, 4])
        return p

def run_script(code):
    r = nav.call('unity_script', {"code": code})
    txt = r['result']['content'][0]['text']
    d = json.loads(txt)
    if not d.get('ok'):
        print('SCRIPT FAILED', txt[:1500]); sys.exit(1)
    return d['data'].get('value')

def assign_sprites(b):
    """NAV component.set cannot assign Sprite sub-assets from a texture path (see field notes), so one script does it."""
    items = ",".join('("%s","%s")' % (p, k) for p, k in b.sprites)
    code = '''
var map=new Dictionary<string,string>{{"panel","panel"},{"circle","circle"},{"ring","ring"},{"face","card_face"},{"back","card_back"},
 {"heart","suit_heart"},{"diamond","suit_diamond"},{"spade","suit_spade"},{"club","suit_club"}};
var items=new List<(string,string)>{%s};
int ok=0; var miss=new List<string>();
foreach(var (path,key) in items){
  var go=GameObject.Find(path); if(go==null){miss.Add(path);continue;}
  var sp=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/"+map[key]+".png");
  var img=go.GetComponent<UnityEngine.UI.Image>(); if(img==null){miss.Add(path+"(noImage)");continue;}
  Undo.RecordObject(img,"sprite"); img.sprite=sp; EditorUtility.SetDirty(img); ok++;
}
return new{ok,miss};''' % items
    print('sprites', run_script(code))
    b.sprites = []

def normalize_scales(canvas_name):
    """gameobject.create keeps world transforms when parenting (worldPositionStays=true), so children created under a
    scaled parent inherit a compensating scale. Reset every UI child to unit scale."""
    print('normalized', run_script('''var root=GameObject.Find("%s"); int n=0; foreach(var t in root.GetComponentsInChildren<Transform>(true)){ if(t==root.transform) continue; if(t.localScale!=Vector3.one){ t.localScale=Vector3.one; n++; } } return n;''' % canvas_name))

def clear_scene():
    run_script('''foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()){ if(go.name!="Main Camera") Undo.DestroyObjectImmediate(go);} return 1;''')

def canvas_root(b, name="GameCanvas"):
    b.node(name, None, comps=("Canvas", "CanvasScaler", "GraphicRaycaster"), rect=False)
    b.op("component.set", target=name, type="Canvas", props={"renderMode": "Screen Space - Overlay"})
    b.op("component.set", target=name, type="CanvasScaler", props={"uiScaleMode": "ScaleWithScreenSize", "referenceResolution": [1080, 1920], "screenMatchMode": "MatchWidthOrHeight", "matchWidthOrHeight": 0.5})
    b.node("Background", name, comps=("Image",), stretch=[0, 0, 0, 0], color=FELT, raycast=True)
    c = b.node("Content", name, comps=("Tarneeb.Presentation.FitScale",), size=(1080, 1920), pos=(0, 0))
    return c

def event_system(b):
    b.node("EventSystem", None, comps=("UnityEngine.EventSystems.EventSystem", "UnityEngine.InputSystem.UI.InputSystemUIInputModule"), rect=False)

def camera_setup(b):
    b.op("component.set", target="Main Camera", type="Camera", props={"clearFlags": "Solid Color", "backgroundColor": FELT})

# ------------------------------------------------------------------ card prefab
def build_card():
    b = Builder()
    clear_scene()
    b.node("PrefabCanvas", None, comps=("Canvas", "CanvasScaler", "GraphicRaycaster"), rect=False)
    b.op("component.set", target="PrefabCanvas", type="Canvas", props={"renderMode": "Screen Space - Overlay"})
    root = b.node("CardView", "PrefabCanvas", comps=("Image", "CanvasGroup", "Tarneeb.Presentation.CardView"), size=(170, 246), color=WHITE, sprite='face')
    face = b.node("Face", root, size=(170, 246))
    b.op("ui.rect", action="stretch", target=face, margin=[0, 0, 0, 0])
    b.node("Rank", face, comps=("TextMeshProUGUI",), size=(90, 60), pos=(-36, 92), text="A", fontSize=56, tcolor=[0.1, 0.1, 0.13, 1], align="left", bold=True)
    b.node("SuitSmall", face, comps=("Image",), size=(40, 40), pos=(-52, 42), sprite='spade', color=[0.1, 0.1, 0.13, 1], raycast=False)
    b.node("SuitBig", face, comps=("Image",), size=(104, 104), pos=(14, -40), sprite='spade', color=[0.1, 0.1, 0.13, 1], raycast=False)
    back = b.node("Back", root, comps=("Image",), stretch=[0, 0, 0, 0], sprite='back', color=WHITE, raycast=False)
    dim = b.node("Dim", root, comps=("Image",), stretch=[0, 0, 0, 0], sprite='face', color=[0, 0, 0, 0.5], raycast=False)
    b.flush("Build CardView")
    normalize_scales("PrefabCanvas")
    assign_sprites(b)
    r = nav.call('unity_run', {"tool": "prefab.create", "args": {"target": "PrefabCanvas/CardView", "path": "Assets/_Project/Resources/Prefabs/CardView.prefab", "connect": False}})
    print('prefab', r['result']['content'][0]['text'][:300])

# ------------------------------------------------------------------ game scene
def seat(b, parent, name, pos, bubble_pos, count=True):
    s = b.node(name, parent, comps=("Image", "Tarneeb.Presentation.SeatView"), size=(260, 120), pos=pos, color=[0, 0, 0, 0.42], sprite='panel', imgtype="Sliced", raycast=False)
    b.node("TurnRing", s, comps=("Image",), stretch=[-6, -6, -6, -6], color=[0.98, 0.84, 0.35, 0.42], sprite='panel', imgtype="Sliced", raycast=False)
    b.node("TeamStripe", s, comps=("Image",), size=(260, 12), preset='top', pos=(0, -6), color=TEAL, sprite='panel', imgtype="Sliced", raycast=False)
    b.node("NameLabel", s, comps=("TextMeshProUGUI",), size=(240, 60), pos=(0, -8), text=name, fontSize=44, bold=True)
    b.node("HandAnchor", s, size=(10, 10), pos=(0, 0))
    bub = b.node("Bubble", s, comps=("Image",), size=(220, 74), pos=bubble_pos, color=[1, 1, 1, 0.96], sprite='panel', imgtype="Sliced", raycast=False)
    b.node("BubbleText", bub, comps=("TextMeshProUGUI",), stretch=[8, 4, 8, 4], text="Bid 8", fontSize=42, tcolor=DARK, bold=True)
    dc = b.node("DealerChip", s, comps=("Image",), size=(50, 50), pos=(-118, 52), color=GOLD, sprite='circle', raycast=False)
    b.node("Text", dc, comps=("TextMeshProUGUI",), stretch=[0, 0, 0, 0], text="D", fontSize=32, tcolor=DARK, bold=True)
    if count:
        cb = b.node("CountBadge", s, comps=("Image",), size=(70, 70), pos=(118, 48), color=[0.96, 0.96, 0.96, 1], sprite='circle', raycast=False)
        b.node("CountText", cb, comps=("TextMeshProUGUI",), stretch=[0, 0, 0, 0], text="13", fontSize=38, tcolor=DARK, bold=True)
    return s

def build_game():
    b = Builder()
    run_script('''var sc=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); return sc.path;''')
    clear_scene()
    camera_setup(b)
    event_system(b)
    content = canvas_root(b)
    C = content
    b.op("component.add", target=C, type="Tarneeb.Presentation.TableView")

    # table decor
    table = b.node("Table", C, size=(1080, 1920)); b.op("ui.rect", action="stretch", target=table, margin=[0, 0, 0, 0])
    b.node("TableMat", table, comps=("Image",), size=(960, 900), pos=(0, 90), color=[0.11, 0.42, 0.28, 0.85], sprite='panel', imgtype="Sliced", raycast=False)
    b.node("DeckAnchor", table, size=(10, 10), pos=(0, 60))
    area = b.node("TrickArea", table, size=(400, 400), pos=(0, 60))
    for n, p in (("Bottom", (0, -100)), ("Right", (140, 0)), ("Top", (0, 100)), ("Left", (-140, 0))):
        b.node("Slot_" + n, area, size=(10, 10), pos=p)
    b.flush("Build table")

    # seats
    seats = b.node("Seats", C, size=(1080, 1920)); b.op("ui.rect", action="stretch", target=seats, margin=[0, 0, 0, 0])
    seat(b, seats, "Seat_Top", (0, 545), (0, -95))
    seat(b, seats, "Seat_Right", (355, 110), (0, 95))
    seat(b, seats, "Seat_Left", (-355, 110), (0, 95))
    seat(b, seats, "Seat_Bottom", (0, -395), (0, 95), count=False)
    b.flush("Build seats")

    # hand
    b.node("Hand", C, comps=("Tarneeb.Presentation.HandView",), size=(1000, 300), pos=(0, -690))
    b.flush("Build hand")

    # HUD
    hud = b.node("HUD", C, comps=("Tarneeb.Presentation.HudView",), size=(1080, 1920)); b.op("ui.rect", action="stretch", target=hud, margin=[0, 0, 0, 0])
    top = b.node("TopBar", hud, comps=("Image",), size=(760, 250), pos=(0, 815), color=[0, 0, 0, 0.32], sprite='panel', imgtype="Sliced", raycast=False)
    b.text("ScoreUsCaption", top, "US", (200, 40), (-170, 88), 32, TEAL, bold=True)
    b.text("ScoreThemCaption", top, "THEM", (200, 40), (170, 88), 32, ORANGE, bold=True)
    b.text("ScoreUs", top, "0", (240, 110), (-170, 24), 100, TEAL, bold=True)
    b.text("ScoreThem", top, "0", (240, 110), (170, 24), 100, ORANGE, bold=True)
    b.text("ContractText", top, "Dealing...", (720, 50), (0, -50), 36, WHITE)
    b.text("TricksText", top, "Tricks  0 - 0", (700, 44), (0, -98), 32, GOLD, bold=True)
    tb = b.node("TrumpBadge", hud, comps=("Image",), size=(170, 190), pos=(415, 815), color=[0, 0, 0, 0.42], sprite='panel', imgtype="Sliced", raycast=False)
    b.node("Icon", tb, comps=("Image",), size=(100, 100), pos=(0, 24), sprite='spade', color=WHITE, raycast=False)
    b.text("Label", tb, "Trump", (160, 44), (0, -62), 32, GOLD, bold=True)
    b.button("MenuButton", hud, (170, 84), (-415, 815), "Menu", 38, [0, 0, 0, 0.42], WHITE)
    b.text("StatusText", hud, "", (900, 56), (0, -262), 38, [1, 0.95, 0.75, 1], bold=True)
    toast = b.node("Toast", hud, comps=("CanvasGroup",), size=(800, 110), pos=(0, 300))
    tim = b.node("ToastBg", toast, comps=("Image",), stretch=[0, 0, 0, 0], color=[0, 0, 0, 0.7], sprite='panel', imgtype="Sliced", raycast=False)
    b.text("ToastText", toast, "", (780, 90), (0, 0), 46, WHITE, bold=True)
    b.flush("Build HUD")

    # card flight layer
    layer = b.node("CardLayer", C, size=(1080, 1920)); b.op("ui.rect", action="stretch", target=layer, margin=[0, 0, 0, 0])

    # bid panel
    bp = b.node("BidPanel", C, comps=("Image", "Tarneeb.Presentation.BidPanel"), size=(940, 470), pos=(0, 40), color=[0.03, 0.08, 0.06, 0.92], sprite='panel', imgtype="Sliced")
    b.text("Title", bp, "Your bid", (800, 70), (0, 175), 56, GOLD, bold=True)
    xs = [-330, -110, 110, 330]
    for i, n in enumerate(range(7, 14)):
        row, col = divmod(i, 4)
        b.button("Bid%d" % n, bp, (190, 120), (xs[col], 52 - row * 150), str(n), 64)
    b.button("Pass", bp, (190, 120), (xs[3], 52 - 150), "Pass", 48, [0.85, 0.35, 0.32, 1], WHITE)
    b.flush("Build BidPanel")

    tp = b.node("TrumpPicker", C, comps=("Image", "Tarneeb.Presentation.TrumpPicker"), size=(940, 400), pos=(0, 40), color=[0.03, 0.08, 0.06, 0.92], sprite='panel', imgtype="Sliced")
    b.text("Title", tp, "Choose trump", (800, 70), (0, 140), 56, GOLD, bold=True)
    for i, s in enumerate(("Clubs", "Diamonds", "Hearts", "Spades")):
        p = b.node("Pick_" + s, tp, comps=("Image", "Button"), size=(190, 200), pos=(xs[i], -30), color=[0.97, 0.97, 0.95, 1], sprite='panel', imgtype="Sliced")
        b.node("Icon", p, comps=("Image",), size=(120, 120), pos=(0, 10), sprite=s.lower()[:-1] if s != 'Diamonds' else 'diamond', color=WHITE, raycast=False)
        b.text("Name", p, s, (180, 40), (0, -75), 30, DARK, bold=True)
    b.flush("Build TrumpPicker")

    # popups
    rs = b.node("RoundSummary", C, comps=("CanvasGroup", "Tarneeb.Presentation.RoundSummaryView"), size=(1080, 1920)); b.op("ui.rect", action="stretch", target=rs, margin=[0, 0, 0, 0])
    b.node("Dim", rs, comps=("Image",), stretch=[0, 0, 0, 0], color=[0, 0, 0, 0.65])
    pn = b.node("Panel", rs, comps=("Image",), size=(880, 780), pos=(0, 20), color=[0.05, 0.14, 0.10, 1], sprite='panel', imgtype="Sliced")
    b.text("Title", pn, "Contract made", (800, 100), (0, 300), 74, WHITE, bold=True)
    b.text("Body", pn, "", (800, 420), (0, 30), 42, WHITE)
    b.button("Continue", pn, (480, 130), (0, -300), "Next round", 54)
    go = b.node("GameOver", C, comps=("CanvasGroup", "Tarneeb.Presentation.GameOverView"), size=(1080, 1920)); b.op("ui.rect", action="stretch", target=go, margin=[0, 0, 0, 0])
    b.node("Dim", go, comps=("Image",), stretch=[0, 0, 0, 0], color=[0, 0, 0, 0.75])
    gp = b.node("Panel", go, comps=("Image",), size=(880, 720), pos=(0, 20), color=[0.05, 0.14, 0.10, 1], sprite='panel', imgtype="Sliced")
    b.text("Title", gp, "You win!", (800, 130), (0, 250), 96, GOLD, bold=True)
    b.text("Body", gp, "", (800, 300), (0, 30), 48, WHITE)
    b.button("PlayAgain", gp, (390, 130), (-215, -270), "Play again", 48)
    b.button("MenuBtn", gp, (390, 130), (215, -270), "Menu", 48, [0.85, 0.85, 0.82, 1], DARK)
    b.flush("Build popups")

    # singletons
    art = b.node("CardArt", None, comps=("Tarneeb.Presentation.CardArt",), rect=False)
    aud = b.node("Audio", None, comps=("AudioSource", "Tarneeb.Presentation.AudioManager"), rect=False)
    gc = b.node("GameController", None, comps=("Tarneeb.Game.GameController",), rect=False)
    b.flush("Build singletons")
    normalize_scales("GameCanvas")
    assign_sprites(b)
    wire_art_and_audio()
    print('game scene built')

def wire_art_and_audio():
    code = '''
Sprite S(string n)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/"+n+".png");
AudioClip A(string n)=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Sfx/"+n+".wav");
var art=GameObject.Find("CardArt").GetComponent<Tarneeb.Presentation.CardArt>();
art.heart=S("suit_heart"); art.diamond=S("suit_diamond"); art.spade=S("suit_spade"); art.club=S("suit_club"); art.cardFace=S("card_face"); art.cardBack=S("card_back"); EditorUtility.SetDirty(art);
var au=GameObject.Find("Audio").GetComponent<Tarneeb.Presentation.AudioManager>();
au.cardFlip=A("card_flip"); au.cardPlace=A("card_place"); au.trickWin=A("trick_win"); au.roundWin=A("round_win"); au.roundLose=A("round_lose"); au.click=A("click"); EditorUtility.SetDirty(au);
return new{artOk=art.heart!=null, audioOk=au.click!=null};'''
    print('wire', run_script(code))

def build_menu():
    b = Builder()
    clear_scene()
    camera_setup(b)
    event_system(b)
    C = canvas_root(b, "MenuCanvas")
    m = b.node("Menu", C, comps=("Tarneeb.Game.MenuController",), size=(1080, 1920)); b.op("ui.rect", action="stretch", target=m, margin=[0, 0, 0, 0])
    b.text("Title", m, "TARNEEB", (1000, 220), (0, 560), 170, GOLD, bold=True)
    b.text("Subtitle", m, "The classic card game", (900, 70), (0, 440), 46, WHITE)
    xs = [-330, -110, 110, 330]
    for i, (k, col) in enumerate((("spade", WHITE), ("heart", [1, 0.42, 0.42, 1]), ("diamond", [1, 0.42, 0.42, 1]), ("club", WHITE))):
        b.node("Suit%d" % i, m, comps=("Image",), size=(120, 120), pos=(xs[i], 290), sprite=k, color=col, raycast=False)
    b.button("PlayButton", m, (700, 160), (0, 80), "Play vs Bots", 62)
    b.button("OnlineButton", m, (700, 130), (0, -110), "Play Online", 46, [0.55, 0.60, 0.58, 1], DARK)
    b.button("DifficultyButton", m, (700, 110), (0, -270), "Bots: Normal", 42, [0.13, 0.42, 0.30, 1], WHITE)
    b.button("SpeedButton", m, (700, 110), (0, -400), "Speed: Normal", 42, [0.13, 0.42, 0.30, 1], WHITE)
    b.button("SoundButton", m, (700, 110), (0, -530), "Sound: On", 42, [0.13, 0.42, 0.30, 1], WHITE)
    b.text("Footer", m, "Portfolio prototype", (900, 50), (0, -880), 32, [1, 1, 1, 0.6])
    b.node("CardArt", None, comps=("Tarneeb.Presentation.CardArt",), rect=False)
    b.node("Audio", None, comps=("AudioSource", "Tarneeb.Presentation.AudioManager"), rect=False)
    b.flush("Build menu")
    normalize_scales("MenuCanvas")
    assign_sprites(b)
    wire_art_and_audio()
    print('menu scene built')

def active_scene_path():
    return run_script('return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;')

def open_scene(path, create):
    if active_scene_path() == path:
        print('scene already active', path); return
    if create:
        r = nav.call('unity_run', {"tool": "scene.create", "args": {"path": path, "setup": "defaultGameObjects", "mode": "single"}})
    else:
        r = nav.call('unity_run', {"tool": "scene.open", "args": {"path": path, "mode": "single"}})
    print(r['result']['content'][0]['text'][:200])

def save():
    r = nav.call('unity_run', {"tool": "scene.save", "args": {"confirm": True}})
    print('save', r['result']['content'][0]['text'][:200])

if __name__ == '__main__':
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    exists = lambda p: os.path.exists(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', p))
    if what in ('card', 'all'):
        open_scene('Assets/_Project/Scenes/Game.unity', not exists('Assets/_Project/Scenes/Game.unity'))
        build_card()
    if what in ('game', 'all'):
        open_scene('Assets/_Project/Scenes/Game.unity', not exists('Assets/_Project/Scenes/Game.unity'))
        build_game(); save()
    if what in ('menu', 'all'):
        open_scene('Assets/_Project/Scenes/Menu.unity', not exists('Assets/_Project/Scenes/Menu.unity'))
        build_menu(); save()
