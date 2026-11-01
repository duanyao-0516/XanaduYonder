'use strict';
// Chromium touch input against the original local game. No synthetic Laya events.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright-core');
const root = path.resolve(__dirname, '../..');
const sourceArg = process.argv.indexOf('--source');
if (sourceArg >= 0 && !process.argv[sourceArg + 1]) throw new Error('--source requires a content directory');
const content = sourceArg >= 0 ? path.resolve(process.argv[sourceArg + 1]) : path.join(root, 'artifacts/zhaoyun-adou/content');
const evidence = path.join(root, 'artifacts/zhaoyun-adou/browser-evidence');
const origin = 'https://zhaoyun-adou.xanadu.invalid';
const browserPath = process.env.CHROMIUM_PATH || ['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser'].find(fs.existsSync);
const mime = {'.html':'text/html','.js':'text/javascript','.json':'application/json','.ls':'application/json','.lh':'application/json','.png':'image/png','.mp3':'audio/mpeg','.wav':'audio/wav','.atlas':'text/plain','.css':'text/css','.txt':'text/plain'};
const baseWords = ['刀', '弓', '枪', '骑'];
fs.mkdirSync(evidence, {recursive:true});

async function verifyViewport(browser, width, height) {
  // Independent contexts have independent localStorage and therefore fresh tutorials.
  const context = await browser.newContext({viewport:{width,height},deviceScaleFactor:1,hasTouch:true,isMobile:true});
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  const result = {width,height,passed:false,phases:[],touches:[],fundingFixtures:[],remote:[],missing:[],errors:[]};
  page.on('pageerror', error => result.errors.push(error.message));
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (url.origin !== origin) { result.remote.push(url.href); return route.abort(); }
    const relative = decodeURIComponent(url.pathname).replace(/^\/+/, '') || 'index.html';
    const file = path.resolve(content, relative);
    if (!file.startsWith(content + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
      result.missing.push(relative); return route.fulfill({status:404,body:'Not found'});
    }
    return route.fulfill({status:200,contentType:mime[path.extname(file)] || 'application/octet-stream',body:fs.readFileSync(file)});
  });
  const phase = name => { result.phases.push(name); console.log(`PASS ${width}x${height}: ${name}`); };
  const snapshot = () => page.evaluate(() => {
    const i = window.__XanaduZhaoyunInternals, d = i.DataMgr.instance(), c = i.ContainersMgr.instance();
    const unit = u => u && ({id:u.id,word:u.HS,level:u.level,objectType:u.objectType});
    return {status:window.XanaduZhaoyun.status(),gold:d.Qf.gold,price:d.Qf.Gs,
      refresh:c.bP(3,true).wP.map(unit),board:c.bP(1,true).wP.map(col => col.map(unit))};
  });
  async function point(kind, arg) {
    const p = await page.evaluate(({kind,arg}) => {
      const i = window.__XanaduZhaoyunInternals;
      const scene = i.SceneMgr.instance().co(kind === 'play' ? 'MainScene' : 'BattleScene');
      let n, global;
      if (kind === 'play' || kind === 'recruit') {
        n = scene[kind === 'play' ? 'playBtn' : 'refreshBtn'];
        global = n.localToGlobal(new Laya.Point(n.width/2,n.height/2));
      } else if (kind === 'refresh') {
        n = scene.refreshBox;
        global = n.localToGlobal(new Laya.Point(n.width/i.DataMgr.instance().map.Ya*(arg+0.5),n.height/2));
      } else {
        const map = i.DataMgr.instance().map;
        // Original map hit testing subtracts offsetY=10 before selecting a row.
        global = scene.map.localToGlobal(new Laya.Point((arg.x+0.5)*map.yh,(arg.y+0.5)*map.gridHei+10));
      }
      const rect = document.querySelector('canvas').getBoundingClientRect();
      return {x:rect.left+global.x*rect.width/Laya.stage.width,y:rect.top+global.y*rect.height/Laya.stage.height};
    }, {kind,arg});
    assert(p.x > 0 && p.y > 0 && p.x < width && p.y < height, `${kind} touch outside viewport: ${JSON.stringify(p)}`);
    return p;
  }
  async function tap(kind) {
    const p = await point(kind);
    result.touches.push({kind,point:p});
    await page.touchscreen.tap(p.x,p.y);
  }
  async function drag(from, to, description) {
    result.touches.push({kind:'drag',description,from,to});
    await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:from.x,y:from.y,id:1}]});
    await page.waitForTimeout(80);
    for (let step=1; step<=12; step++) {
      await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{
        x:from.x+(to.x-from.x)*step/12,y:from.y+(to.y-from.y)*step/12,id:1}]});
      await page.waitForTimeout(35);
    }
    await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  }
  const waitCards = () => page.waitForFunction(() => {
    const i = window.__XanaduZhaoyunInternals;
    return i.ContainersMgr.instance().bP(3,true).wP.filter(Boolean).length === i.DataMgr.instance().map.Ya;
  }, null, {timeout:45000});

  try {
    await page.goto(origin + '/index.html');
    await page.waitForFunction(() => window.XanaduZhaoyun?.status().ready, null, {timeout:45000});
    const activate = await page.evaluate(() => [window.XanaduZhaoyun.configure({soundEnabled:false}),window.XanaduZhaoyun.setActive(true)]);
    assert(activate.every(x => x.ok), 'host bridge must activate the fresh game');
    await tap('play');
    await page.waitForFunction(() => window.XanaduZhaoyun.status().battle, null, {timeout:30000});
    // battle=true precedes the original scene transition's input mask removal.
    await page.waitForTimeout(3000);
    phase('real touch starts a fresh battle');
    const before = await snapshot();
    await tap('recruit');
    await waitCards();
    const first = await snapshot();
    assert.equal(first.gold, before.gold-before.price, 'first recruit must charge its original price');
    assert(first.refresh.every(Boolean), 'the original draw animation must finish all slots');
    phase('real touch recruits five original cards');

    let drawNumber=1;
    async function fundedRecruit() {
      // The original recruit action rejects a completely full rack. Legal random
      // draws can contain only general letters, so place one before drawing again.
      const space=await page.evaluate(()=>{
        const i=window.__XanaduZhaoyunInternals,c=i.ContainersMgr.instance(),rack=c.bP(3,true).wP;
        if(rack.some(x=>!x))return null;
        const slot=rack.findIndex(x=>typeof x?.HS==='string'&&x.HS.length>0);
        if(slot<0)return{error:'No placeable word in a full recruit rack'};
        const unit=rack[slot],m=i.DataMgr.instance().map,board=c.bP(1,true).wP,kind=unit.HS==='农'?'2_0':'1_0';
        for(let x=0;x<m.Ga.length;x++)for(let y=0;y<m.Ga[x].length;y++)if(m.Ga[x][y]===kind&&!board[x][y])return{slot,id:unit.id,word:unit.HS,cell:{x,y}};
        return{error:'No free board cell for the recruit fallback'};
      });
      if(space){
        assert(!space.error,space.error);
        await drag(await point('refresh',space.slot),await point('board',space.cell),`make room with ${space.word} id=${space.id}`);
        await page.waitForFunction(slot=>!window.__XanaduZhaoyunInternals.ContainersMgr.instance().bP(3,true).wP[slot],space.slot,{timeout:15000});
      }
      // Explicit fixture: fund only the original recruit price. Cards and merge
      // outcomes are produced entirely by the original game via real touch input.
      const funding=await page.evaluate(() => {
        const q=window.__XanaduZhaoyunInternals.DataMgr.instance().Qf, previous=q.gold;
        q.gold=Math.max(q.gold,q.Gs); return {previous,next:q.gold,added:q.gold-previous,price:q.Gs};
      });
      result.fundingFixtures.push(funding);
      await tap('recruit');
      await page.waitForFunction(price=>window.__XanaduZhaoyunInternals.DataMgr.instance().Qf.Gs===price+2,funding.price,{timeout:15000});
      await waitCards();
      const cards=await snapshot();
      if(++drawNumber===2) assert.deepEqual(cards.refresh.slice(0,4).map(u=>u.word),['刀','弓','黄','忠'],'second draw uses the original fixed tutorial cards');
      return cards;
    }
    const basicSoldiers = draw => draw.refresh.map((u,slot)=>({...u,slot})).filter(u=>baseWords.includes(u.word));
    let available=first, soldiers=basicSoldiers(available);
    // The first draw is random. A draw without basic soldiers is legal; the
    // second original tutorial draw always supplies a knife and a bow.
    if(!soldiers.length) { available=await fundedRecruit(); soldiers=basicSoldiers(available); }
    const duplicate=soldiers.find(a=>soldiers.some(b=>a.slot!==b.slot&&a.word===b.word&&a.level===b.level));
    const selected=duplicate||soldiers.find(u=>['刀','弓'].includes(u.word))||soldiers[0];
    assert(selected,'original recruits must provide a basic soldier');
    const cell = await page.evaluate(() => {
      const i=window.__XanaduZhaoyunInternals, m=i.DataMgr.instance().map, board=i.ContainersMgr.instance().bP(1,true).wP;
      for(let x=0;x<m.Ga.length;x++) for(let y=0;y<m.Ga[x].length;y++) if(m.Ga[x][y]==='1_0'&&!board[x][y]) return{x,y};
      return null;
    });
    assert(cell, 'fresh battle has a free player soldier cell');
    await drag(await point('refresh',selected.slot),await point('board',cell),`place ${selected.word} id=${selected.id}`);
    await page.waitForFunction(({slot,id,cell}) => {
      const c=window.__XanaduZhaoyunInternals.ContainersMgr.instance();
      return !c.bP(3,true).wP[slot] && c.bP(1,true).wP[cell.x][cell.y]?.id===id;
    }, {...selected,cell}, {timeout:15000});
    phase('touch drag moves the same soldier from recruit slot onto the board');

    let partner=soldiers.find(u=>u.slot!==selected.slot&&u.word===selected.word&&u.level===selected.level);
    // Prefer an existing pair. Otherwise use genuine further recruits, with an
    // explicit bound, until the original random draw supplies a matching unit.
    while(!partner&&drawNumber<10) {
      available=await fundedRecruit();
      partner=basicSoldiers(available).find(u=>u.word===selected.word&&u.level===selected.level);
    }
    assert(partner,'no matching soldier appeared within ten original recruits');
    result.recruitCount=drawNumber;
    await drag(await point('refresh',partner.slot),await point('board',cell),`merge ${partner.word} id=${partner.id} into id=${selected.id}`);
    await page.waitForFunction(({slot,id,level,cell}) => {
      const c=window.__XanaduZhaoyunInternals.ContainersMgr.instance(), u=c.bP(1,true).wP[cell.x][cell.y];
      return !c.bP(3,true).wP[slot] && u?.id===id && u.level===level+1;
    }, {slot:partner.slot,id:selected.id,level:selected.level,cell}, {timeout:15000});
    result.merge={word:selected.word,targetId:selected.id,consumedId:partner.id,cell,beforeLevel:selected.level,afterLevel:selected.level+1};
    phase('real touch merges matching soldiers and raises the original target level');
    result.final=await snapshot();
    assert.deepEqual(result.remote,[],'all requests must remain on the local game origin');
    assert.deepEqual(result.missing,[],'all reachable resources must exist');
    assert.deepEqual(result.errors,[],'the game must not raise uncaught errors');
    result.passed=true;
  } catch (error) {
    result.error=error.stack;
    result.final=await snapshot().catch(()=>null);
  } finally {
    const screenshot=path.join(evidence,`gestures-${width}x${height}.png`);
    await page.screenshot({path:screenshot}).then(()=>{result.screenshot=screenshot;}).catch(error=>{result.screenshotError=error.message;result.passed=false;});
    await context.close();
  }
  if (result.remote.length || result.missing.length || result.errors.length) result.passed=false;
  return result;
}

(async () => {
  const browser=await chromium.launch({executablePath:browserPath,headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  let result;
  try {
    const build=JSON.parse(fs.readFileSync(path.join(content,'../content-build.json'),'utf8'));
    const viewports=[];
    for(const [width,height] of [[360,640],[320,568]]) viewports.push(await verifyViewport(browser,width,height));
    result={passed:viewports.every(x=>x.passed),content,contentDigest:build.contentDigest,browserVersion:browser.version(),viewports,
      notes:['Every play/recruit/drag/merge action uses Chromium touch input. No Laya events or unit/result fixtures are injected.',
        'When needed, the funding fixture adds only enough battle currency to pay the next original recruit price.',
        'Fresh browser contexts test both sizes independently. Android WebView insets, native back and physical touch require device validation.']};
  } catch(error) { result={passed:false,content,error:error.stack}; }
  finally { await browser.close(); }
  fs.writeFileSync(path.join(evidence,'gestures.json'),JSON.stringify(result,null,2));
  console.log(JSON.stringify(result,null,2));
  process.exitCode=result.passed?0:1;
})().catch(error=>{console.error(error);process.exitCode=1;});
