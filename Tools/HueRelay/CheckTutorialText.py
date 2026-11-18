"""Replay the tutorial with live frames and check rendered glyphs in Playworks 7.1.

Requires Python Playwright + Chrome. Optional argument: extracted package folder.
Only local HTTP is used; telemetry requests are stubbed.
"""
from pathlib import Path
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from functools import partial
import json, sys, threading
from playwright.sync_api import sync_playwright

root=Path(__file__).resolve().parents[2]
package=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else root/'LunaTemp/stage4/develop'
out=root/'Logs/TutorialFix';out.mkdir(parents=True,exist_ok=True)
class Handler(SimpleHTTPRequestHandler):
    def log_message(self,*args):pass
server=ThreadingHTTPServer(('127.0.0.1',0),partial(Handler,directory=str(package)))
threading.Thread(target=server.serve_forever,daemon=True).start()
with sync_playwright() as p:
    browser=p.chromium.launch(channel='chrome',headless=True,args=['--enable-webgl','--ignore-gpu-blocklist','--use-angle=swiftshader'])
    page=browser.new_page(viewport={'width':340,'height':605})
    warnings=[];errors=[]
    page.on('console',lambda m:warnings.append(m.text) if m.type=='warning' and 'Text will be hidden' in m.text else None)
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('**/*',lambda r:r.continue_() if r.request.url.startswith(f'http://127.0.0.1:{server.server_port}/') and not r.request.url.endswith('/tracing') else r.fulfill(status=200,content_type='application/json',body='{}',headers={'Access-Control-Allow-Origin':'*'}))
    entry='iframe.html' if (package/'iframe.html').exists() else 'index.html'
    page.goto(f'http://127.0.0.1:{server.server_port}/{entry}')
    if entry=='index.html':page.evaluate("window.postMessage(JSON.stringify({name:'setPlaygroundAssetOverrides',data:'{}'}),'*')")
    page.wait_for_function('window.UIManager?.Instance && UnityEngine.Object.FindObjectOfType(HueRelay.HueGame)?.P',timeout=60000)
    page.evaluate(r'''()=>{
      window.g=UnityEngine.Object.FindObjectOfType(HueRelay.HueGame);
      g.StartLevel(0);g.StartTutorial();
      window.tutorialGlyphs=()=>{
        const t=g.sceneView.tutorialText,gen=t.textGenerator;
        return {step:g.TutorialStepIndex,text:t.text,characters:gen._characters.length,lines:gen._lines.length,
          expectedCharacters:t.text.replace(/\s/g,'').length,expectedLines:t.text.split('\n').length,
          width:t.rectTransform.rect.width,height:t.rectTransform.rect.height,
          renderedHeight:gen._lines.reduce((sum,line)=>sum+line.height,0),alpha:g.sceneView.tutorialGroup.alpha,
          active:t.gameObject.activeInHierarchy,enabled:g.enabled};
      };
      window.tutorialNextPoint=()=>{
        const c=Array.from({length:4},()=>new UnityEngine.Vector3());
        g.sceneView.tutorialNext.transform.GetWorldCorners(c);
        const a=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,c[0]),b=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,c[2]);
        return {x:(a.x+b.x)/2,y:innerHeight-(a.y+b.y)/2};
      };
    }''')
    rows=[]
    def check():
        row=page.evaluate('tutorialGlyphs()')
        assert row['enabled'] and row['active'] and row['alpha']>.99,row
        assert row['characters']>=row['expectedCharacters'] and row['lines']>=row['expectedLines'],row
        assert row['renderedHeight']<=row['height']+1,row
        rows.append(row)
    # Advance every tutorial step using real pointer input and the game's timers.
    for step in range(6):
        page.wait_for_function('(s)=>g.TutorialStepIndex===s',arg=step,timeout=15000)
        page.wait_for_timeout(700);check()
        page.screenshot(path=str(out/f'live-step-{step}.png'))
        if step<3:
            point=page.evaluate('''id=>{const h=g.P.arrs[id].Head;return {x:(g.ox+(h.x+.5)*g.cs)*g.pixelScale,y:(g.oy+(h.y+.5)*g.cs)*g.pixelScale}}''',step)
        else:point=page.evaluate('tutorialNextPoint()')
        page.mouse.click(point['x'],point['y'],delay=120)
    page.wait_for_function('!g.IsTutorial',timeout=10000)
    for w,h in [(320,480),(340,605),(390,844),(568,320),(844,390),(1920,900)]:
        page.set_viewport_size({'width':w,'height':h})
        page.evaluate('g.StartLevel(0);g.StartTutorial()')
        for step in range(6):
            page.evaluate('s=>g.TutorialStep(s)',step)
            page.wait_for_timeout(400);check()
        page.screenshot(path=str(out/f'wrapped-{w}x{h}.png'))
    data={'cases':len(rows),'rows':rows,'warnings':warnings,'errors':errors,'realPointerFlow':True}
    (out/'rendered-glyphs.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
    print(json.dumps({'cases':len(rows),'warnings':warnings,'errors':errors,'realPointerFlow':True},indent=2))
    assert not warnings and not errors,data
    browser.close()
server.shutdown()
