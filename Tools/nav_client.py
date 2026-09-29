#!/usr/bin/env python3
"""Tiny MCP-over-HTTP client for NAV MCP. usage: nav.py <tool> '<json args>'"""
import sys, json, urllib.request, os
TOKEN=open(os.path.expanduser('~/Library/Application Support/UnityMCP/token')).read().strip()
URL='http://127.0.0.1:8730/mcp'
SF=os.path.join(os.path.dirname(os.path.abspath(__file__)),'.navsession')
def post(body, sid=None):
    h={'Content-Type':'application/json','Accept':'application/json, text/event-stream','Authorization':'Bearer '+TOKEN}
    if sid: h['Mcp-Session-Id']=sid
    r=urllib.request.urlopen(urllib.request.Request(URL,json.dumps(body).encode(),h),timeout=600)
    txt=r.read().decode()
    out=None
    for line in txt.splitlines():
        if line.startswith('data:'): out=json.loads(line[5:].strip())
    if out is None and txt.strip().startswith('{'): out=json.loads(txt)
    return r.headers.get('Mcp-Session-Id'), out
def session():
    if os.path.exists(SF):
        return open(SF).read().strip()
    sid,_=post({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"claude-code-curl","version":"1"}}})
    post({"jsonrpc":"2.0","method":"notifications/initialized"},sid)
    open(SF,'w').write(sid or '')
    return sid
def call(tool,args):
    sid=session()
    try:
        _,o=post({"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":tool,"arguments":args}},sid)
    except Exception as e:
        if os.path.exists(SF): os.remove(SF)
        sid=session()
        _,o=post({"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":tool,"arguments":args}},sid)
    return o
if __name__=='__main__':
    tool=sys.argv[1]; args=json.loads(sys.argv[2]) if len(sys.argv)>2 else {}
    if tool=='--list':
        sid=session(); _,o=post({"jsonrpc":"2.0","id":3,"method":"tools/list"},sid)
        for t in o['result']['tools']: print(t['name'],'-',t.get('description','')[:150].replace('\n',' '))
    else:
        o=call(tool,args)
        res=o.get('result',o)
        for c in res.get('content',[]): print(c.get('text',c))
        if res.get('isError'): print('[isError]')
