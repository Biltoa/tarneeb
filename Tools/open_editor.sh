#!/bin/bash
# Opens the Unity 6000.3 Editor on this project and waits until NAV MCP reports it connected and idle.
cd "$(dirname "$0")/.."
rm -f Temp/UnityLockfile Tools/.navsession
pgrep -f "6000.3.25f1/Unity.app/Contents/MacOS/Unity" >/dev/null || nohup /Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -projectPath "$PWD" -logFile "$PWD/Logs/Editor-63.log" >/dev/null 2>&1 &
for i in $(seq 1 90); do
  curl -s -m 5 http://127.0.0.1:8730/health | grep -q '"health":"ok"' && break
  sleep 4
done
sleep 12
python3 Tools/nav_client.py unity_run '{"tool":"assets.refresh","args":{}}' >/dev/null
sleep 15
python3 Tools/nav_client.py unity_run '{"tool":"compile.errors","args":{}}' | head -c 400; echo
