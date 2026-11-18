"""Regression replay for a local Playworks 7.1 develop package.

Requires Python Playwright + Chrome. Exercises all five levels, including a
14-arrow chain, cache invalidation, expired effects and endless continuation.
"""
from pathlib import Path
from http.server import SimpleHTTPRequestHandler,ThreadingHTTPServer
from functools import partial
import threading,json
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[2]
out=root/'Logs/Performance';out.mkdir(exist_ok=True)
class Handler(SimpleHTTPRequestHandler):
 def log_message(self,*args):pass
server=ThreadingHTTPServer(('127.0.0.1',0),partial(Handler,directory=str(root/'LunaTemp/stage4/develop')))
threading.Thread(target=server.serve_forever,daemon=True).start()
with sync_playwright() as p:
 browser=p.chromium.launch(channel='chrome',headless=True,args=['--enable-webgl','--ignore-gpu-blocklist','--use-angle=swiftshader'])
 page=browser.new_page(viewport={'width':390,'height':844})
 errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 page.route('**/*',lambda r:r.continue_() if r.request.url.startswith(f'http://127.0.0.1:{server.server_port}/') and not r.request.url.endswith('/tracing') else r.fulfill(status=200,content_type='application/json',body='{}',headers={'Access-Control-Allow-Origin':'*'}))
 page.goto(f'http://127.0.0.1:{server.server_port}/iframe.html')
 page.wait_for_function('window.UIManager?.Instance && UnityEngine.Object.FindObjectOfType(HueRelay.HueGame)?.P',timeout=60000)
 page.evaluate('''()=>{
 window.g=UnityEngine.Object.FindObjectOfType(HueRelay.HueGame);
 window.counts={};
 for(const name of ['Render','RenderGrid','RenderEffects']){const f=g[name];g[name]=function(...a){counts[name]=(counts[name]||0)+1;return f.apply(this,a)}}
 g.StartLevel(3);
 }''')
 page.wait_for_timeout(1700)
 page.evaluate('counts={}')
 page.wait_for_timeout(500)
 idle=page.evaluate('counts')
 assert idle.get('Render',0)>5 and idle.get('RenderGrid',0)==0 and idle.get('RenderEffects',0)==0,idle
 page.screenshot(path=str(out/'optimized-idle.png'))
 page.evaluate('g.Hint();counts={}')
 page.wait_for_timeout(400)
 assert page.evaluate('counts.RenderGrid>3'),'Hint not redrawn'
 page.evaluate('g.Retry();counts={}')
 page.wait_for_timeout(1500)
 assert page.evaluate('g.Mask===g.P.Full && counts.RenderGrid>3'),'Retry not redrawn'
 page.set_viewport_size({'width':1920,'height':900})
 page.wait_for_timeout(350)
 assert page.evaluate('g.renderedOrigin.x===g.ox && g.renderedOrigin.y===g.oy && g.renderedCellSize===g.cs'),'Resize cache stale'
 page.screenshot(path=str(out/'optimized-landscape.png'))
 page.set_viewport_size({'width':390,'height':844})
 levels=[]
 for level in range(5):
  page.evaluate('(i)=>g.StartLevel(i)',level)
  page.wait_for_timeout(1500)
  taps=[]
  for turn in range(20):
   state=page.evaluate('''()=>({mask:g.Mask,over:g.Over,won:g.Won,busy:g.busy,flies:g.flies.Count})''')
   if state['over'] or state['mask']==0:break
   assert not state['busy'],state
   selected=page.evaluate('''()=>{const b=HueRelay.HueEngine.Best(g.P,g.Mask,new HueRelay.HueRandom(7),300);g.TapArrow(b.first);return {id:b.first,taps:g.Taps,mask:g.Mask}}''')
   taps.append(selected)
   page.wait_for_function('!g.busy && g.flies.Count===0',timeout=15000)
  page.wait_for_function('g.Over',timeout=8000)
  state=page.evaluate('''()=>({mask:g.Mask,won:g.Won,score:g.Score,taps:g.Taps,chain:g.chainN})''')
  assert state['won'] and state['mask']==0,state
  levels.append(state)
  if level<4:
   page.wait_for_function('g.ModalVisible',timeout=15000)
   page.wait_for_function('g.conf.Count===0 && g.rings.Count===0 && g.particles.Count===0 && g.tokens.Count===0 && g.floats.Count===0 && g.beads.Count===0',timeout=15000)
   page.wait_for_timeout(100)
   assert page.evaluate('g.fx.geometry.vertices.Count===0 && g.floats.Count===0'),'Expired effect mesh retained'
 page.wait_for_function('g.ModalVisible',timeout=15000)
 page.evaluate('g.ContinueResult()')
 page.wait_for_function('g.Endless && !g.generating && !g.Over',timeout=15000)
 page.evaluate('counts={}')
 page.wait_for_timeout(450)
 assert page.evaluate('counts.Render>0'),'Endless gameplay stopped rendering'
 assert not errors,errors
 data={'idle':idle,'levels':levels,'errors':errors,'endlessContinuation':True,'resizeHintRetryCache':True}
 (out/'regression.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
 print(json.dumps(data,indent=2))
 browser.close()
server.shutdown()
