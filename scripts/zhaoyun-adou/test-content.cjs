'use strict';
// Real Chromium + original game bundle. Install playwright-core externally or set NODE_PATH.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright-core');
const root = path.resolve(__dirname, '../..');
const sourceArg = process.argv.indexOf('--source');
const baseline = process.argv.includes('--baseline');
const content = sourceArg >= 0 ? path.resolve(process.argv[sourceArg + 1]) : path.join(root, 'artifacts/zhaoyun-adou/content');
const evidence = path.join(root, 'artifacts/zhaoyun-adou/browser-evidence');
fs.mkdirSync(evidence, { recursive: true });
const origin = 'https://zhaoyun-adou.xanadu.invalid';
const browserPath = process.env.CHROMIUM_PATH || ['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser'].find(fs.existsSync);
const mime = {'.html':'text/html','.css':'text/css','.js':'text/javascript','.json':'application/json','.ls':'application/json','.lh':'application/json','.png':'image/png','.mp3':'audio/mpeg','.wav':'audio/wav','.txt':'text/plain','.atlas':'text/plain'};
(async () => {
  const browser = await chromium.launch({executablePath:browserPath, headless:true, args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  const context = await browser.newContext({viewport:{width:360,height:640},deviceScaleFactor:1,hasTouch:true,isMobile:true});
  const page = await context.newPage(); const remote=[], missing=[], errors=[], phases=[], haptics=[];
  await page.exposeFunction('__recordVibration',args=>haptics.push(args));
  await page.addInitScript(()=>{navigator.vibrate=(...args)=>{window.__recordVibration(args);return false;};});
  page.on('pageerror', e=>errors.push(e.message));
  page.on('console',m=>{if(m.type()==='error'&&/MIME type|Content Security Policy/.test(m.text()))errors.push(m.text());});
  let early=null;
  await page.route('**/*', async route=>{
    const url=new URL(route.request().url());
    if(url.origin!==origin){remote.push(url.href);return route.abort();}
    const relative=decodeURIComponent(url.pathname).replace(/^\/+/, '') || 'index.html';
    const file=path.resolve(content,relative);
    if(!file.startsWith(content+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()){missing.push(relative);return route.fulfill({status:404,body:'Not found'});}
    if(!baseline&&relative==='js/index.js'){
      early=await page.evaluate(()=>({before:window.XanaduZhaoyun.status(),mutedBefore:Laya.SoundManager.muted,configured:window.XanaduZhaoyun.configure({soundEnabled:false}),inactive:window.XanaduZhaoyun.setActive(false)}));
    }
    await route.fulfill({status:200,contentType:mime[path.extname(file)]||'application/octet-stream',body:fs.readFileSync(file)});
  });
  async function phase(name, fn){await fn();phases.push(name);console.log('PASS '+name);}
  async function status(){return page.evaluate(()=>window.XanaduZhaoyun.status());}
  async function waitScene(name){await page.waitForFunction(n=>window.XanaduZhaoyun?.status().scene===n,name,{timeout:30000});}
  async function tapNode(sceneName,nodeName){
    const point=await page.evaluate(({sceneName,nodeName})=>{
      const scene=window.__XanaduZhaoyunInternals.SceneMgr.instance().co(sceneName),node=scene[nodeName];
      const p=node.localToGlobal(new Laya.Point(node.width/2,node.height/2));
      const rect=document.querySelector('canvas').getBoundingClientRect();
      return{x:rect.left+p.x*rect.width/Laya.stage.width,y:rect.top+p.y*rect.height/Laya.stage.height};
    },{sceneName,nodeName});
    const viewport=page.viewportSize();assert(point.x>0&&point.y>0&&point.x<viewport.width&&point.y<viewport.height,`${sceneName}.${nodeName} is outside the touch viewport: ${JSON.stringify(point)}`);
    await page.touchscreen.tap(point.x,point.y);
  }
  let result;
  try {
    await page.goto(origin+(baseline?'/gameIndex.html':'/index.html'));
    if(baseline)await page.waitForTimeout(8000);
    assert.equal(await page.evaluate(()=>typeof window.XanaduZhaoyun),'object','original package lacks the required offline lifecycle/save API');
    await page.waitForFunction(()=>window.XanaduZhaoyun.status().ready,null,{timeout:30000});
    await phase('early configure/inactive survives asynchronous initialization',async()=>{
      assert.equal(early.before.ready,false);assert.equal(early.before.active,false);assert.equal(early.before.soundEnabled,false);assert.equal(early.before.paused,true);assert.equal(early.mutedBefore,true);assert.equal(early.configured.ok,true);assert.equal(early.inactive.ok,true);
      assert.equal((await status()).paused,true);
      assert.equal(await page.evaluate(()=>Laya.SoundManager.muted),true);
      assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.setActive(true))).ok,true);
      assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.configure({soundEnabled:true}))).ok,true);
      assert.equal((await status()).paused,false);assert.equal(await page.evaluate(()=>Laya.SoundManager.muted),false);
    });
    await phase('new local game starts without platform UI or missing resources',async()=>{
      assert.equal((await status()).scene,'MainScene');assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);assert.deepEqual(missing,[]);
      assert.equal(await page.evaluate(()=>Boolean(window.h5api||window.gamebox)),false);
      const hidden=await page.evaluate(()=>{const s=window.__XanaduZhaoyunInternals.SceneMgr.instance().co('MainScene');return ['dySidebarBtn','followBtn','addDeskBtn','addStaminaBtn'].every(k=>!s[k].visible);});assert.equal(hidden,true);
      const labels=await page.evaluate(()=>{const s=window.__XanaduZhaoyunInternals.SceneMgr.instance().co('MainScene');return ['rankBtn','noticeBtn'].every(k=>s[k].getChildByName('Text').visible===false);});assert.equal(labels,true,'upstream leaderboard/notice captions must be removed');
      await page.screenshot({path:path.join(evidence,'main-360.png')});
    });
    await phase('real touch enters first battle without spending stamina',async()=>{
      const before=await page.evaluate(()=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.stamina);
      await tapNode('MainScene','playBtn');
      await page.waitForFunction(()=>window.XanaduZhaoyun.status().battle,null,{timeout:30000});
      assert.equal(await page.evaluate(()=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.stamina),before);
      await page.screenshot({path:path.join(evidence,'battle-360.png')});
    });
    await phase('background freezes original AI/time and foreground requires continue',async()=>{
      assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.setActive(false))).ok,true);
      const sample=()=>page.evaluate(()=>{const i=window.__XanaduZhaoyunInternals,g=i.DataMgr.instance();return{time:i.TimeMgr.instance().pr,wave:g.Qf.Rs,gold:g.Qf.gold,aiGold:g.Qf.Mi};});
      const before=await sample();await page.waitForTimeout(600);assert.deepEqual(await sample(),before);
      assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.setActive(true))).ok,true);
      assert.equal((await status()).paused,true);assert.equal((await status()).awaitingContinue,true);
      await page.getByRole('button',{name:'继续游戏',exact:true}).tap();assert.equal((await status()).awaitingContinue,false);
    });
    await phase('user pause and own audio preference survive host toggles',async()=>{
      await page.evaluate(()=>{const i=window.__XanaduZhaoyunInternals;i.TimeMgr.instance().pause(false);i.SoundMgr.instance().setMusicVolume(0);i.SoundMgr.instance().setSoundVolume(0);const p=i.DataMgr.instance().player;p.setSettingData({...p.settingData,musicVolume:0,soundVolume:0});});
      await page.evaluate(()=>{window.XanaduZhaoyun.setActive(false);window.XanaduZhaoyun.setActive(true);});
      await page.getByRole('button',{name:'继续游戏',exact:true}).tap();assert.equal((await status()).paused,true);
      await page.evaluate(()=>{window.XanaduZhaoyun.configure({soundEnabled:false});window.XanaduZhaoyun.configure({soundEnabled:true});});
      const volumes=await page.evaluate(()=>({music:Laya.SoundManager.musicVolume,sound:Laya.SoundManager.soundVolume}));
      assert.equal(volumes.music,0);assert.equal(volumes.sound,0);
      await page.evaluate(()=>window.__XanaduZhaoyunInternals.TimeMgr.instance().resume());
    });
    await phase('failed persistence is reported while the game remains paused/muted',async()=>{
      const failed=await page.evaluate(()=>{window.__testOriginalStorageSet=Laya.LocalStorage.setItem;Laya.LocalStorage.setItem=()=>{};const i=window.__XanaduZhaoyunInternals;i.DataMgr.instance().player._data._gold+=1;return window.XanaduZhaoyun.setActive(false);});
      assert.equal(failed.ok,false);assert.equal(failed.paused,true);assert.equal(await page.evaluate(()=>Laya.SoundManager.muted),true);
      await page.evaluate(()=>{Laya.LocalStorage.setItem=window.__testOriginalStorageSet;delete window.__testOriginalStorageSet;});
      assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.setActive(false))).ok,true);
      await page.evaluate(()=>window.XanaduZhaoyun.setActive(true));await page.getByRole('button',{name:'继续游戏',exact:true}).tap();
    });
    let rewardFixture;
    await phase('original victory awards standard coins and independent weapon fragments',async()=>{
      rewardFixture=await page.evaluate(()=>{
        const i=window.__XanaduZhaoyunInternals,g=i.DataMgr.instance(),p=g.player;
        // Deterministic progression fixture, not a claim that the test won by playing.
        p._data._registerTime=Date.now()-3*86400000;p.openProps=true;
        const weapon=[...g.wy.my].find(([id,w])=>id>=0&&w.fragmentNum>0)[0];
        const previous={gold:p.gold,weapon,fragments:p.getWeaponFragmentCount(weapon)};
        g.Qf.pi=[{weaponId:weapon,fragmentNum:1}];i.GameMgr.instance().gameOver(true,false,false);
        return previous;
      });
      await waitScene('GameOverScene');
      await page.waitForFunction(()=>window.__XanaduZhaoyunInternals.SceneMgr.instance().co('GameOverScene').getBtn.visible,null,{timeout:30000});
      const visible=await page.evaluate(()=>window.__XanaduZhaoyunInternals.SceneMgr.instance().co('GameOverScene').getBtnAd.visible);assert.equal(visible,false);
      await page.screenshot({path:path.join(evidence,'victory.png')});await tapNode('GameOverScene','getBtn');
      await page.waitForFunction(gold=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.gold===gold,rewardFixture.gold+20,{timeout:20000});
      assert.equal(await page.evaluate(id=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.getWeaponFragmentCount(id),rewardFixture.weapon),rewardFixture.fragments+1);
      await waitScene('ShopScene');
    });
    let afterPurchase;
    await phase('coin store retains item prices, capacity rules and persistent inventory',async()=>{
      // Scene attachment precedes the upstream scale-in animation. Wait for the
      // real button transform to settle before computing the touch coordinate.
      await page.waitForFunction(()=>{
        const s=window.__XanaduZhaoyunInternals.SceneMgr.instance().co('ShopScene'),item=s?._ft?.find(x=>x.visible),b=item?.getChildByName('btn');if(!b)return false;
        const p=b.localToGlobal(new Laya.Point(b.width/2,b.height/2)),key=[p.x,p.y,b.globalScaleX,b.globalScaleY].join(':');
        if(window.__shopTransform?.key===key)return performance.now()-window.__shopTransform.since>400;
        window.__shopTransform={key,since:performance.now()};return false;
      },null,{timeout:15000});
      const purchase=await page.evaluate(()=>{
        const i=window.__XanaduZhaoyunInternals,g=i.DataMgr.instance(),shop=i.SceneMgr.instance().co('ShopScene');g.player.gold=300;shop.spt();
        const item=shop._ft.find(x=>x.visible),button=item.getChildByName('btn');
        const price=Number(button.getChildByName('goldBox').getChildByName('price').text);
        const before=g.player.getPropsData().length;
        const hidden=!shop.lotteryBox.visible&&!shop.lotteryLabel.visible&&!button.getChildByName('adBox').visible;
        const point=button.localToGlobal(new Laya.Point(button.width/2,button.height/2)),rect=document.querySelector('canvas').getBoundingClientRect();
        return{price,before,hidden,point:{x:rect.left+point.x*rect.width/Laya.stage.width,y:rect.top+point.y*rect.height/Laya.stage.height}};
      });
      assert.equal(purchase.hidden,true);const vp=page.viewportSize();assert(purchase.point.x>0&&purchase.point.y>0&&purchase.point.x<vp.width&&purchase.point.y<vp.height);await page.touchscreen.tap(purchase.point.x,purchase.point.y);
      await page.waitForFunction(before=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.getPropsData().length>before,purchase.before,{timeout:10000});
      afterPurchase=await page.evaluate(()=>{const p=window.__XanaduZhaoyunInternals.DataMgr.instance().player;return{gold:p.gold,props:p.getPropsData(),fragments:p.getWeaponFragments?p.getWeaponFragments():p._data._weaponFragments};});
      assert.equal(afterPurchase.gold,300-purchase.price);
      await page.screenshot({path:path.join(evidence,'shop.png')});
      assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.exit())).ok,true);
    });
    await phase('reload restores progress and local daily reward commits exactly once',async()=>{
      await page.reload();await page.waitForFunction(()=>window.XanaduZhaoyun.status().ready,null,{timeout:30000});
      await page.evaluate(()=>window.XanaduZhaoyun.setActive(true));
      const restored=await page.evaluate(()=>{const p=window.__XanaduZhaoyunInternals.DataMgr.instance().player;return{gold:p.gold,props:p.getPropsData(),fragments:p._data._weaponFragments};});assert.deepEqual(restored,afterPurchase);
      await tapNode('MainScene','rankBtn');
      assert.equal(await page.locator('.xanadu-local-panel').evaluate(n=>getComputedStyle(n).position),'fixed','the test server must serve the actual CSS');
      const claim=page.getByRole('button',{name:/领取每日段位奖励/});
      assert.equal(await claim.count(),1);
      {
        const before=await page.evaluate(()=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.gold);
        const oldSave=await page.evaluate(()=>{const p=window.__XanaduZhaoyunInternals.DataMgr.instance().player;window.__testOriginalStorageSet=Laya.LocalStorage.setItem;Laya.LocalStorage.setItem=()=>{};return localStorage.getItem(p.key);});
        await claim.tap();
        const failed=await page.evaluate(()=>{const p=window.__XanaduZhaoyunInternals.DataMgr.instance().player;return{gold:p.gold,claimed:p.isGetLastRankReward,saved:localStorage.getItem(p.key)};});
        assert.deepEqual(failed,{gold:before,claimed:0,saved:oldSave});assert.equal(await claim.isEnabled(),true);
        await page.evaluate(()=>{Laya.LocalStorage.setItem=window.__testOriginalStorageSet;delete window.__testOriginalStorageSet;});
        await claim.tap();
        const snapshot=await page.evaluate(()=>{const p=window.__XanaduZhaoyunInternals.DataMgr.instance().player;return{gold:p.gold,claimed:p.isGetLastRankReward,saved:JSON.parse(localStorage.getItem(p.key))};});
        assert(snapshot.gold>before);assert.equal(snapshot.claimed,1);assert.equal(snapshot.saved._gold,snapshot.gold);assert.equal(snapshot.saved._isGetLastRankReward,1);
        assert.equal(await page.getByRole('button',{name:'每日段位奖励已领取',exact:true}).isDisabled(),true);
      }
      await page.getByRole('button',{name:'返回游戏',exact:true}).tap();
    });
    await phase('320px touch controls and failed battle exit preserve pause and settle only once',async()=>{
      await page.setViewportSize({width:320,height:568});await page.waitForTimeout(300);await page.screenshot({path:path.join(evidence,'main-320.png')});
      await tapNode('MainScene','rankBtn');await page.getByRole('button',{name:'返回游戏',exact:true}).tap();
      await tapNode('MainScene','playBtn');await page.waitForFunction(()=>window.XanaduZhaoyun.status().battle,null,{timeout:30000});
      await tapNode('BattleScene','refreshBtn');
      const failed=await page.evaluate(()=>{window.__testOriginalStorageSet=Laya.LocalStorage.setItem;Laya.LocalStorage.setItem=()=>{};window.__XanaduZhaoyunInternals.DataMgr.instance().player._data._gold+=1;return window.XanaduZhaoyun.exit();});
      assert.equal(failed.ok,false);assert.equal(failed.paused,true);assert.equal(await page.evaluate(()=>Laya.SoundManager.muted),true);
      const progression=()=>page.evaluate(()=>{const d={...window.__XanaduZhaoyunInternals.DataMgr.instance().player._data};delete d._saveTime;return d;});
      const settled=await progression();
      await page.evaluate(()=>{Laya.LocalStorage.setItem=window.__testOriginalStorageSet;delete window.__testOriginalStorageSet;});
      assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.exit())).ok,true);assert.deepEqual(await progression(),settled);
      assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.exit())).ok,true);assert.deepEqual(await progression(),settled);assert.equal((await status()).paused,true);
    });
    await phase('ordinary defeat retains the original five-coin reward',async()=>{
      await page.reload();await page.waitForFunction(()=>window.XanaduZhaoyun.status().ready,null,{timeout:30000});await page.evaluate(()=>window.XanaduZhaoyun.setActive(true));
      const before=await page.evaluate(()=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.gold);
      await tapNode('MainScene','playBtn');await page.waitForFunction(()=>window.XanaduZhaoyun.status().battle,null,{timeout:30000});
      await page.evaluate(()=>window.__XanaduZhaoyunInternals.GameMgr.instance().gameOver(false,false,false));
      await waitScene('GameOverScene');await page.waitForFunction(()=>window.__XanaduZhaoyunInternals.SceneMgr.instance().co('GameOverScene').getBtn.visible,null,{timeout:30000});
      assert.equal(await page.evaluate(()=>window.__XanaduZhaoyunInternals.SceneMgr.instance().co('GameOverScene').getBtnAd.visible),false);
      await tapNode('GameOverScene','getBtn');await page.waitForFunction(gold=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.gold===gold,before+5,{timeout:20000});
      await waitScene('ShopScene');assert.equal((await page.evaluate(()=>window.XanaduZhaoyun.exit())).ok,true);
    });
    assert.deepEqual(haptics,[],'offline gameplay must not request haptics');
    assert.deepEqual(remote,[],'no external request is permitted');
    assert.deepEqual(missing,[],'no missing reachable resource is permitted');
    assert.deepEqual(errors,[],'no uncaught game error is permitted');
    result={passed:true,content,contentDigest:JSON.parse(fs.readFileSync(path.join(content,'../content-build.json'),'utf8')).contentDigest,browserVersion:browser.version(),phases,status:await status(),remote,missing,errors,haptics,notes:['Victory and advanced progression use explicit fixture outcomes through original game managers; first battle is entered by touch.','Native WebView gesture/inset/back checks belong to device verification.']};
  } catch(error) {result={passed:false,content,error:error.stack,phases,early,status:await page.evaluate(()=>window.XanaduZhaoyun?.status()).catch(()=>null),remote,missing,errors};}
  await page.screenshot({path:path.join(evidence,baseline?'baseline.png':'content.png')}).catch(()=>{});
  fs.writeFileSync(path.join(evidence,baseline?'baseline.json':'content.json'),JSON.stringify(result,null,2));
  await browser.close();console.log(JSON.stringify(result,null,2));process.exitCode=result.passed?0:1;
})().catch(error=>{console.error(error);process.exitCode=1;});
