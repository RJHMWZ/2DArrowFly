"""Profile a locally exported Playworks 7.1 package; requires Python Playwright + Chrome.

Usage: python Tools/HueRelay/BenchmarkPlayable.py <package-directory> <report-name>
All remote requests are stubbed. No package or report is uploaded.
"""
from pathlib import Path
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from functools import partial
import json, threading, sys
from playwright.sync_api import sync_playwright

root=Path(__file__).resolve().parents[2]
package=(Path(sys.argv[1]) if len(sys.argv)>1 else root/'LunaTemp/stage4/develop').resolve()
label=sys.argv[2] if len(sys.argv)>2 else 'current'
output=root/'Logs/Performance';output.mkdir(parents=True,exist_ok=True)
class Handler(SimpleHTTPRequestHandler):
    def log_message(self,*args):pass
server=ThreadingHTTPServer(('127.0.0.1',0),partial(Handler,directory=str(package)))
threading.Thread(target=server.serve_forever,daemon=True).start()
with sync_playwright() as p:
    browser=p.chromium.launch(channel='chrome',headless=True,args=['--enable-webgl','--ignore-gpu-blocklist','--use-angle=swiftshader'])
    page=browser.new_page(viewport={'width':390,'height':844})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('**/*',lambda r:r.continue_() if r.request.url.startswith(f'http://127.0.0.1:{server.server_port}/') and not r.request.url.endswith('/tracing') else r.fulfill(status=200,content_type='application/json',body='{}',headers={'Access-Control-Allow-Origin':'*'}))
    entry='iframe.html' if (package/'iframe.html').exists() else 'index.html'
    page.goto(f'http://127.0.0.1:{server.server_port}/{entry}')
    if entry=='index.html':page.evaluate("window.postMessage(JSON.stringify({name:'setPlaygroundAssetOverrides',data:'{}'}),'*')")
    page.wait_for_function('window.UIManager?.Instance && UnityEngine.Object.FindObjectOfType(HueRelay.HueGame)?.P',timeout=60000)
    page.evaluate('''()=>{window.g=UnityEngine.Object.FindObjectOfType(HueRelay.HueGame);g.StartLevel(3)}''')
    page.wait_for_timeout(1600)
    data=page.evaluate('''async()=>{
      g.enabled=false;
      const clock=g.now;
      const stats={};
      for(const name of ['Render','RenderGrid','RenderArrows','RenderEffects','RenderChrome','RenderCombo','RenderTutorial','RenderSheet','Layout','UpdateEffects']){
        const f=g[name];if(!f)continue;
        g[name]=function(...args){const t=performance.now();const v=f.apply(this,args);(stats[name]??=[]).push(performance.now()-t);return v};
      }
      const geo=HueRelay.HueGeometry.prototype, original=geo.Populate;
      geo.Populate=function(...args){const t=performance.now(),v=original.apply(this,args);(stats.Populate??=[]).push(performance.now()-t);return v};
      const summarize=a=>{a=[...a].sort((a,b)=>a-b);return {mean:a.reduce((s,v)=>s+v,0)/a.length,p95:a[Math.floor(a.length*.95)],max:a.at(-1),n:a.length}};
      const result={};
      for(const scene of ['idle','combo','victory']){
        let seed=1741;Math.random=()=>((seed=(Math.imul(seed,1664525)+1013904223)>>>0)/4294967296);
        g.ClearEffects();g.now=clock;g.dt=1/60;
        if(scene!=='idle'){
          // Three overlapping high-chain exits, using the game's own emitters.
          for(let i=0;i<3;i++){
            const p=g.G(g.P.arrs[i].Head);
            const c=i%2?HueRelay.HueGame.Orange:HueRelay.HueGame.Blue;
            g.ExitRing(p,c,9+i,g.P.arrs[i].d);g.Spawn(p,c,5,g.P.arrs[i].d);g.Burst(p,c,22,g.cs*6);
          }
          if(scene==='victory'){g.ConfettiFire(1,60);g.ConfettiFire(-1,60)}
          for(let i=0;i<12;i++){g.now+=1/60;g.UpdateEffects()}
        }
        // Freeze the same state so every measured frame has identical effect counts.
        for(let i=0;i<20;i++){g.Render();await new Promise(requestAnimationFrame)}
        for(const k of Object.keys(stats))stats[k]=[];
        const frame=[];
        for(let i=0;i<150;i++){
          const t=performance.now();g.Render();await new Promise(requestAnimationFrame);frame.push(performance.now()-t);
        }
        const size=x=>x?.Count??0;
        result[scene]={methods:Object.fromEntries(Object.entries(stats).filter(([k,v])=>v.length).map(([k,v])=>[k,summarize(v)])),frame:summarize(frame),
          boardVertices:size(g.board.geometry.vertices),effectVertices:size(g.fx.geometry.vertices),rings:size(g.rings),particles:size(g.particles),confetti:size(g.conf),ripples:size(g.ripples)};
      }
      return result;
    }''')
    page.screenshot(path=str(output/(label+'.png')))
    data['errors']=errors
    (output/(label+'.json')).write_text(json.dumps(data,indent=2),encoding='utf-8')
    print(json.dumps(data,indent=2));assert not errors,errors
    browser.close()
server.shutdown()
