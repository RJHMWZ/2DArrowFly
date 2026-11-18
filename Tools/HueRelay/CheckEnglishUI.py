"""Check English UI and text bounds in a local Playworks 7.1 package.

Requires Python Playwright and Chrome. Optional argument: extracted package path.
Reports/screenshots are saved under Logs/EnglishUI. No data is uploaded.
"""
from pathlib import Path
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from functools import partial
import json, sys, threading
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[2]
package = Path(sys.argv[1]).resolve() if len(sys.argv)>1 else root/'LunaTemp/stage4/develop'
out = root/'Logs/EnglishUI'; out.mkdir(parents=True,exist_ok=True)
class Handler(SimpleHTTPRequestHandler):
    def log_message(self,*args): pass
server=ThreadingHTTPServer(('127.0.0.1',0),partial(Handler,directory=str(package)))
threading.Thread(target=server.serve_forever,daemon=True).start()
with sync_playwright() as p:
    browser=p.chromium.launch(channel='chrome',headless=True,args=['--enable-webgl','--ignore-gpu-blocklist','--use-angle=swiftshader'])
    page=browser.new_page(viewport={'width':390,'height':844})
    errors=[]; rendering_warnings=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.on('console',lambda m:rendering_warnings.append(m.text) if m.type=='warning' and 'Text will be hidden' in m.text else None)
    page.route('**/*',lambda r:r.continue_() if r.request.url.startswith(f'http://127.0.0.1:{server.server_port}/') and not r.request.url.endswith('/tracing') else r.fulfill(status=200,content_type='application/json',body='{}',headers={'Access-Control-Allow-Origin':'*'}))
    entry='iframe.html' if (package/'iframe.html').exists() else 'index.html'
    page.goto(f'http://127.0.0.1:{server.server_port}/{entry}')
    if entry=='index.html':page.evaluate("window.postMessage(JSON.stringify({name:'setPlaygroundAssetOverrides',data:'{}'}),'*')")
    page.wait_for_function('window.UIManager?.Instance && !UIManager.Instance.IsLoading',timeout=60000)
    page.evaluate(r'''()=>{
      window.g=UnityEngine.Object.FindObjectOfType(HueRelay.HueGame);g.enabled=false;
      UIManager.Instance.endAfterLastLevel=false;UIManager.Instance.endOnFailure=false;
      window.uiBounds=rt=>{
        const c=Array.from({length:4},()=>new UnityEngine.Vector3());rt.GetWorldCorners(c);
        const p=c.map(v=>UnityEngine.RectTransformUtility.WorldToScreenPoint(null,v));
        return {left:Math.min(...p.map(v=>v.x)),right:Math.max(...p.map(v=>v.x)),bottom:Math.min(...p.map(v=>v.y)),top:Math.max(...p.map(v=>v.y))};
      };
      window.checkEnglishUI=(mode)=>{
        const v=g.sceneView, problems=[], labels=[];
        for(const name of ['score','best','levelTitle','moves','combo','milestoneNumber','milestoneCaption','tutorialText','tutorialNextText','resultTitle','resultScore','resultDetail','resultPrimaryText','resultSecondaryText']){
          const t=v[name];if(!t.gameObject.activeInHierarchy)continue;
          const generator=t.textGenerator,characters=generator?._characters?.length??0,lines=generator?._lines?.length??0;
          const renderedHeight=(generator?._lines??[]).reduce((sum,line)=>sum+line.height,0);
          const expectedCharacters=t.text.replace(/\s/g,'').length,expectedLines=t.text.split('\n').length;
          const r=t.rectTransform.rect,b=uiBounds(t.rectTransform),wrap=name==='tutorialText'||name==='resultDetail';
          if(/[\u3400-\u9fff]/.test(t.text))problems.push(name+': Chinese text');
          // Explicit line breaks must fit too; measuring only preferredHeight
          // misses exported single-line text that visually runs under a button.
          if(t.preferredWidth>r.width+1||wrap&&t.preferredHeight>r.height+1)problems.push(name+': text exceeds its box');
          if(wrap&&(characters<expectedCharacters||lines<expectedLines))problems.push(name+': rendered text is missing or incomplete');
          if(wrap&&renderedHeight>r.height+1)problems.push(name+': rendered line boxes exceed the text height');
          if(b.left<-.5||b.right>innerWidth+.5)problems.push(name+': outside screen horizontally');
          labels.push({name,text:t.text,font:t.fontSize,width:r.width,preferredWidth:t.preferredWidth,height:r.height,preferredHeight:t.preferredHeight,characters,lines,renderedHeight,expectedCharacters,expectedLines,bounds:b});
        }
        for(const name of ['tutorialGroup','resultGroup']){
          if(!v[name].gameObject.activeInHierarchy)continue;
          const b=uiBounds(v[name].transform);
          if(b.left<-.5||b.right>innerWidth+.5||b.bottom<-.5||b.top>innerHeight+.5)problems.push(name+': card outside screen');
        }
        return {mode,size:[innerWidth,innerHeight],labels,problems};
      };
    }''')
    results=[]
    for w,h in [(320,480),(360,640),(390,844),(768,1024),(568,320),(844,390),(1920,900)]:
        page.set_viewport_size({'width':w,'height':h});page.wait_for_timeout(100)
        page.evaluate('''()=>{g.StartLevel(0);g.now=UnityEngine.Time.unscaledTime+2;g.Layout();g.Render()}''')
        results.append(page.evaluate("checkEnglishUI('hud')"))
        page.evaluate('g.StartTutorial()')
        for step in range(6):
            page.evaluate('''step=>{g.TutorialStep(step);g.now=g.tutStepAt+.6;g.hintAt=g.now-.6;g.Layout();g.Render()}''',step)
            page.wait_for_timeout(40)
            results.append(page.evaluate("checkEnglishUI('tutorial')"))
            if step==5 and w in [320,390,568]:page.screenshot(path=str(out/f'tutorial-{w}x{h}.png'))
        for level,won,endless,record in [(0,True,False,True),(4,True,False,False),(0,False,False,False),(0,True,True,True)]:
            page.evaluate('''s=>{
              g.StartLevel(s.level);g.now=UnityEngine.Time.unscaledTime+3;g.Layout();
              g.Endless=s.endless;g.Won=s.won;g.Over=true;g.record=s.record;g.Score=9999999;g.ds=g.Score;g.bestScore=9999999;g.Taps=99;g.Limit=100;
              g.remaining=12;g.sheetVisible=true;g.modalAt=g.now-1;g.cachedModal=-100;g.Render();
            }''',{'level':level,'won':won,'endless':endless,'record':record})
            page.wait_for_timeout(40)
            results.append(page.evaluate("checkEnglishUI('result')"))
            if won and record and not endless and w in [320,390,568]:page.screenshot(path=str(out/f'result-{w}x{h}.png'))
        page.evaluate('''()=>{g.StartLevel(3);g.now=UnityEngine.Time.unscaledTime+3;g.chainN=14;g.mileN=12;g.comboAt=g.now-.3;g.mileAt=g.now-.8;g.Render()}''')
        results.append(page.evaluate("checkEnglishUI('chain')"))
        if w in [320,390]:page.screenshot(path=str(out/f'chain-{w}x{h}.png'))
    page.evaluate('UIManager.Instance.CompletePlayable()');page.wait_for_timeout(800)
    for w,h in [(320,480),(390,844),(568,320),(1920,900)]:
        page.set_viewport_size({'width':w,'height':h});page.wait_for_timeout(150)
        state=page.evaluate('''()=>{
          const p=UIManager.Instance.presentation,t=p.endSubtitleText,b=uiBounds(t.rectTransform);
          return {mode:'endcard',size:[innerWidth,innerHeight],problems:t.preferredHeight>t.rectTransform.rect.height+1||b.left<0||b.right>innerWidth?['Subtitle overflow']:[],text:t.text,bounds:b};
        }''');results.append(state)
        if w in [320,390,568]:page.screenshot(path=str(out/f'end-{w}x{h}.png'))
    data={'cases':results,'errors':errors,'renderingWarnings':rendering_warnings}
    (out/'bounds.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
    failed=[r for r in results if r['problems']]
    print(json.dumps({'cases':len(results),'failures':failed,'errors':errors,'renderingWarnings':rendering_warnings},indent=2))
    assert not failed and not errors and not rendering_warnings,'English UI rendering/bounds check failed'
    browser.close()
server.shutdown()
