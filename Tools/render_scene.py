#!/usr/bin/env python3
"""Renders the open scene's canvas (edit mode, no Play) to Logs/<name>.png at 850x1850 so layouts can be checked quickly.
Hides the runtime-only popups the way their Awake() would. Usage: python3 Tools/render_scene.py [name]"""
import json, subprocess, sys, os
name = sys.argv[1] if len(sys.argv) > 1 else 'scene'
code = r'''
foreach (var n in new[]{"BidPanel","TrumpPicker","RoundSummary","GameOver","PauseMenu","SettingsPanel","EmojiPanel","ChatPanel","OnlinePanel","Hint","Toast","Timer","DealerChip","Bubble","ChatBubble","EmojiBadge","Turn","Fader","Unread"})
  foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.name==n) t.gameObject.SetActive(false);
var canvas = Object.FindFirstObjectByType<Canvas>(); var cam = Camera.main;
int W = 850, H = 1850;
var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
cam.targetTexture = rt; cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.magenta;
var oldMode = canvas.renderMode;
canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10f;
Canvas.ForceUpdateCanvases(); cam.orthographicSize = H / 2f;
foreach (var fs in canvas.GetComponentsInChildren<Tarneeb.Presentation.FitScale>()) fs.transform.localScale = Vector3.one;
cam.Render();
RenderTexture.active = rt; var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
System.IO.File.WriteAllBytes("%s/Logs/%s.png", tex.EncodeToPNG());
cam.targetTexture = null; canvas.renderMode = oldMode;
return "ok";''' % (os.path.abspath(os.path.join(os.path.dirname(__file__), '..')), name)
r = subprocess.run(['python3', os.path.join(os.path.dirname(__file__), 'nav_client.py'), 'unity_script', json.dumps({"code": code})], capture_output=True, text=True)
print(r.stdout[:200])
