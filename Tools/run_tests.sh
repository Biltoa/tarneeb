#!/bin/bash
# Usage: Tools/run_tests.sh EditMode|PlayMode [test filter]
# Closes the Editor (batch mode needs the project unlocked), runs the tests headless, prints the summary.
set -u
cd "$(dirname "$0")/.."
U=/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity
pkill -f "6000.3.25f1/Unity.app/Contents/MacOS/Unity" ; sleep 6; rm -f Temp/UnityLockfile
PLATFORM=${1:-EditMode}; FILTER=${2:-}
ARGS=(-batchmode -projectPath "$PWD" -runTests -testPlatform "$PLATFORM" -testResults "Logs/${PLATFORM}-results.xml" -logFile "Logs/${PLATFORM}-tests.log")
[ "$PLATFORM" = "EditMode" ] && ARGS+=(-nographics)
[ -n "$FILTER" ] && ARGS+=(-testFilter "$FILTER")
"$U" "${ARGS[@]}" >/dev/null 2>&1
python3 - "$PLATFORM" <<'PY'
import sys,re
x=open('Logs/%s-results.xml'%sys.argv[1]).read()
m=re.search(r'<test-run [^>]*result="([^"]*)"[^>]*total="(\d+)" passed="(\d+)" failed="(\d+)"',x)
print('result=%s total=%s passed=%s failed=%s'%m.groups())
for f in re.finditer(r'<test-case name="([^"]+)"[^>]*result="Failed"[^>]*>.*?<message><!\[CDATA\[(.*?)\]\]>',x,re.S):
    print('FAILED',f.group(1),'\n   ',f.group(2)[:600])
PY
