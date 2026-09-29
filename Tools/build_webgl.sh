#!/bin/bash
# Usage: Tools/build_webgl.sh   (closes the Editor, builds WebGL headless into Build/WebGL)
cd "$(dirname "$0")/.."
pkill -f "[6]000.3.25f1/Unity.app/Contents/MacOS/Unity"; sleep 6; rm -f Temp/UnityLockfile
rm -rf Build/WebGL
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -buildTarget WebGL -executeMethod BuildTools.BuildWebGL -logFile Logs/build-webgl.log
echo "exit=$?"
grep -a "\[Build\]" Logs/build-webgl.log | tail -2
du -sh Build/WebGL 2>/dev/null; ls -la Build/WebGL/Build 2>/dev/null
