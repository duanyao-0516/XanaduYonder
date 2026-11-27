// Computer-only integration checks. Real Ruffle/SWF; native feedback ACKs are
// explicitly simulated. This does not verify physical audio or phone vibration.
const { settingsMetrics, swipeSettings } = require('../../Tests/WebDialogs/metrics.cjs');
const { sheetMotion } = require('../../Tests/WebDialogs/motion.cjs');
const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright-core');
const root=path.resolve(__dirname,'../..'),content=path.join(root,'artifacts/bomb-it/content');
const output=process.env.BOMB_IT_CHECK_OUTPUT||path.join(root,'artifacts/bomb-it/feedback-browser');
const origin='https://bomb-it.xanaduyonder.invalid/',report={nativeFeedbackMock:true,checks:[],errors:[],requests:[]};
fs.mkdirSync(output,{recursive:true});
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
  try{
    const context=await browser.newContext({viewport:{width:844,height:390},hasTouch:true,serviceWorkers:'block'});
    let page,token='browser-native-1',sound=true,haptics=true,failNext=false,hold=false;
    const messages=[];
    await context.exposeBinding('nativePost',async(_,raw)=>{
      const m=JSON.parse(raw);messages.push(m);
      if(m.type!=='feedback-settings'||hold)return;
      const ok=!failNext;failNext=false;
      if(ok){sound=m.soundEnabled;haptics=m.hapticsEnabled;}
      await page.evaluate(v=>{BombItHost.setHostState(true,v.soundEnabled);BombItHost.feedbackSettingsResult(v);},
        {token:m.token,requestId:m.requestId,ok,soundEnabled:sound,hapticsEnabled:haptics});
    });
    await context.addInitScript(()=>{window.BombItNative={postMessage:value=>window.nativePost(value)};});
    await context.route('**/*',async route=>{
      const url=new URL(route.request().url()),name=decodeURIComponent(url.pathname.slice(1));
      assert(url.origin===new URL(origin).origin&&!name.includes('..')&&!name.includes('\\'),'unexpected external request');
      report.requests.push(name);await route.fulfill({path:path.join(content,name)});
    });
    page=await context.newPage();page.setDefaultTimeout(30000);page.on('pageerror',e=>report.errors.push(String(e)));
    const state=()=>page.evaluate(()=>document.querySelector('ruffle-player').ruffle().callExternalInterface('xanaduState'));
    const ready=async()=>{
      await page.waitForFunction(()=>BombItHost.status().ready);
      await page.evaluate(v=>{BombItHost.configureFeedback(v);BombItHost.setHostState(true,v.soundEnabled);}, {token,soundEnabled:sound,hapticsEnabled:haptics});
    };
    const shot=name=>page.screenshot({path:path.join(output,name+'.png')});
    const note=(name,details={})=>{report.checks.push({name,...details});console.log('PASS '+name);};
    const waitSound=value=>page.waitForFunction(v=>document.querySelector('ruffle-player').ruffle().callExternalInterface('xanaduState').soundEnabled===v,value);
    await page.goto(origin+'index.html');await ready();
    for(const [width,height] of [[844,390],[740,328],[640,360],[915,412]]){
      await page.setViewportSize({width,height});await page.evaluate(()=>BombItHost.setViewportInsets(24,0,0,21));
      const tools=await page.locator('.tool-grid button').evaluateAll(list=>list.map(e=>{const b=e.getBoundingClientRect();return {x:b.x,y:b.y,right:b.right,bottom:b.bottom};}));
      assert(tools.every(b=>Math.abs(b.y-tools[0].y)<1&&b.x>=0&&b.right<=width),'tools fit one visible row');
      assert(tools.every((b,i)=>!i||b.x>tools[i-1].right),'tool buttons do not overlap');
      const spacing=await page.evaluate(()=>document.querySelector('#mode-switch').getBoundingClientRect().top-document.querySelector('.brand-row').getBoundingClientRect().bottom);
      assert(spacing>=8,'brand and player-mode group need visible separation');
      await page.locator('#settings').tap();
      const checkExit = await sheetMotion(page.locator('.sheet'));
      assert.equal(await page.locator('#modal').evaluate(e=>getComputedStyle(e).backgroundColor),'rgba(0, 0, 0, 0.4)');
      const box=await page.locator('.sheet-scroll').evaluate(e=>({height:e.clientHeight,scroll:e.scrollHeight}));
      const settingsCard=await page.locator('.sheet').elementHandle();
      const shape=await page.locator('.sheet').evaluate(e=>({width:e.getBoundingClientRect().width,bottom:e.getBoundingClientRect().bottom,topRadius:getComputedStyle(e).borderTopLeftRadius,bottomRadius:getComputedStyle(e).borderBottomLeftRadius}));
      assert.equal(shape.width,width);assert.equal(shape.bottom,height);assert.equal(shape.topRadius,'20px');assert.equal(shape.bottomRadius,'0px');
      assert.notEqual(await page.locator('.sheet').evaluate(e=>getComputedStyle(e).backgroundColor),'rgba(0, 0, 0, 0)','sheet must have an opaque readable surface');
      box.typography=await settingsMetrics(page.locator('.sheet'),'#modal-confirm',24);
      await shot('settings-'+width+'x'+height);
      const beforeSound=sound;
      await page.locator('#sound-label').tap();await waitSound(!beforeSound);
      await page.locator('#sound-setting').tap();await waitSound(beforeSound);
      if(box.scroll>box.height+1) box.afterSwipe=await swipeSettings(page,page.locator('.sheet-scroll'));
      await page.locator('#direction-diameter').scrollIntoViewIfNeeded();
      assert(await settingsCard.evaluate(card=>card===document.querySelector('[role=dialog]')&&card.parentElement.dataset.kind==='settings'));
      assert(await page.locator('#direction-diameter').isVisible());
      await shot('control-settings-'+width+'x'+height);
      await page.touchscreen.tap(5,5);await page.locator('#modal').waitFor({state:'hidden'});
      await checkExit();
      note('compact settings and backdrop '+width+'x'+height,box);
    }
    await page.setViewportSize({width:320,height:568});
    await page.locator('#settings').tap();
    const portraitExit = await sheetMotion(page.locator('.sheet'));
    await shot('settings-320x568');
    await page.locator('#direction-diameter').scrollIntoViewIfNeeded();
    await shot('control-settings-320x568');
    await page.locator('#modal-confirm').tap();
    await page.locator('#modal').waitFor({state:'hidden'});
    await portraitExit();
    await page.locator('#settings').tap();
    const interrupted = await page.locator('#modal').evaluate(layer => {
      const panel = layer.firstElementChild;
      for (const animation of [layer,panel].flatMap(node => node.getAnimations())) {
        animation.pause();
        animation.currentTime = XanaduDialogs.durationMs() / 3;
      }
      const offset = () => new DOMMatrix(getComputedStyle(panel).transform).m42;
      const before = offset(), shade = getComputedStyle(layer).backgroundColor;
      layer.querySelector('#modal-close').click();
      const closing = [layer,panel].flatMap(node => node.getAnimations());
      for (const animation of closing) {
        animation.pause();
        animation.currentTime = 0;
      }
      const after = offset(), sameShade = getComputedStyle(layer).backgroundColor === shade;
      const guarded = panel.inert && document.elementFromPoint(4,4) === layer;
      XanaduDialogs.dismissTop();
      closing.forEach(animation => animation.play());
      return {before,after,sameShade,guarded,height:panel.clientHeight};
    });
    assert(interrupted.before > 0 && interrupted.before < interrupted.height);
    assert(Math.abs(interrupted.before - interrupted.after) < .5 && interrupted.sameShade && interrupted.guarded);
    await page.locator('#modal').waitFor({state:'hidden'});
    await page.emulateMedia({reducedMotion:'reduce'});
    await page.locator('#settings').tap();
    const reduced = await page.locator('#modal').evaluate(layer => {
      const card = layer.firstElementChild;
      const playing = card.getAnimations().length + layer.getAnimations().length;
      const duration = XanaduDialogs.durationMs();
      layer.querySelector('#modal-confirm').click();
      return {playing,duration,closed:layer.hidden};
    });
    assert.deepEqual(reduced,{playing:0,duration:0,closed:true});
    await page.emulateMedia({reducedMotion:'no-preference'});
    await page.setViewportSize({width:844,height:390});await page.evaluate(()=>BombItHost.setViewportInsets(24,0,0,21));
    for(const id of ['hall','settings','help'])assert.notEqual(await page.locator('#'+id).evaluate(e=>getComputedStyle(e).backgroundColor),'rgba(0, 0, 0, 0)');
    await page.locator('#settings').tap();await page.locator('#sound-setting').tap();await waitSound(false);
    assert.equal(sound,false);assert.equal(await page.locator('#sound-setting').getAttribute('aria-checked'),'false');
    failNext=true;await page.locator('#sound-setting').tap();await page.waitForFunction(()=>document.querySelector('#feedback-status').textContent.includes('尚未确认'));
    assert.equal((await state()).soundEnabled,false);assert.equal(sound,false);
    await page.locator('#feedback-retry').tap();await waitSound(true);
    await page.locator('#haptics-setting').tap();await page.waitForFunction(()=>document.querySelector('#haptics-setting').getAttribute('aria-checked')==='false');
    assert.equal(haptics,false);await page.locator('#haptics-setting').tap();await page.waitForFunction(()=>document.querySelector('#haptics-setting').getAttribute('aria-checked')==='true');
    note('native ACK confirms sound/haptics; rejected save retains prior values');
    hold=true;await page.locator('#sound-setting').tap();const late=messages.filter(m=>m.type==='feedback-settings').at(-1);
    token='browser-native-2';await page.evaluate(v=>BombItHost.configureFeedback(v),{token,soundEnabled:true,hapticsEnabled:true});
    await page.evaluate(v=>BombItHost.feedbackSettingsResult(v),{...late,ok:true,soundEnabled:false,hapticsEnabled:false});
    assert.equal(await page.locator('#sound-setting').getAttribute('aria-checked'),'true');hold=false;
    await page.locator('#modal-close').tap();note('old native token cannot overwrite current preferences');
    const exitsBefore=messages.filter(m=>m.type==='exit-intent').length;
    await page.locator('#hall').tap();await shot('confirm-return');await page.touchscreen.tap(4,4);
    await page.locator('#modal').waitFor({state:'hidden'});
    assert.equal(messages.filter(m=>m.type==='exit-intent').length,exitsBefore);
    const modeBefore=(await state()).mode;
    await page.locator('[data-mode="single"]').tap();await shot('confirm-mode');await page.touchscreen.tap(4,4);
    await page.locator('#modal').waitFor({state:'hidden'});assert.equal((await state()).mode,modeBefore);
    await page.locator('[data-mode="single"]').tap();
    await page.evaluate(()=>{
      const player=document.querySelector('ruffle-player'),create=player.ruffle;
      player.ruffle=function(...args){
        const api=create.apply(this,args),invoke=api.callExternalInterface;
        api.callExternalInterface=function(method,...values){
          if(method==='xanaduFlushSave'){
            player.ruffle=create;
            return {saveOk:false};
          }
          return invoke.call(this,method,...values);
        };
        return api;
      };
    });
    await page.locator('#modal-confirm').tap();
    await page.locator('#modal[data-kind="save-error"]').waitFor();
    await shot('save-error');await page.touchscreen.tap(4,4);
    await page.locator('#modal').waitFor({state:'hidden'});
    assert.equal((await state()).mode,modeBefore,'a failed save cannot switch players');
    await page.locator('[data-mode="single"]').tap();await page.locator('#modal-confirm').tap();await ready();
    const tapSwf=async(x,y)=>{const b=await page.locator('#player-frame').boundingBox();await page.touchscreen.tap(b.x+x*b.width/550,b.y+y*b.height/400);await page.waitForTimeout(200);};
    await tapSwf(450,215);await page.waitForFunction(()=>BombItHost.status().phase==='select');
    await page.locator('.p2-bomb').tap();await page.waitForFunction(()=>BombItHost.status().phase==='battle');
    assert((await state()).continueInfo);await shot('single-idle');
    const count=()=>messages.filter(m=>m.type==='haptic').length;
    const before=count();await page.locator('.p2-bomb').tap();await page.waitForTimeout(150);assert.equal(count(),before+1);
    await page.locator('#pause').tap();await page.locator('#help').tap();
    assert((await page.locator('#modal-copy').textContent()).includes('同一难度内不会随关数加强电脑'));
    for(const word of ['火箭','地雷','手榴弹','汽弹','冰箭','火焰箭'])assert((await page.locator('#modal-copy').textContent()).includes(word));
    await shot('help');await page.touchscreen.tap(5,5);assert.equal((await state()).manualPaused,true);
    await page.locator('#help').tap();
    assert(await page.locator('#modal-confirm').evaluate(e=>e.getBoundingClientRect().top>=document.querySelector('.sheet-scroll').getBoundingClientRect().bottom),'help close action follows the long content instead of occupying a fixed footer');
    await page.locator('#modal-confirm').scrollIntoViewIfNeeded();await shot('help-end');
    await page.locator('#modal-confirm').tap();assert.equal((await state()).manualPaused,true);
    await page.locator('#settings').tap();await page.locator('#haptics-setting').tap();await page.waitForFunction(()=>document.querySelector('#haptics-setting').getAttribute('aria-checked')==='false');
    await page.touchscreen.tap(5,5);assert.equal((await state()).manualPaused,true);
    await page.locator('#pause').tap();const noHaptic=count();await page.locator('.p2-bomb').tap();await page.waitForTimeout(150);assert.equal(count(),noHaptic);
    note('help/backdrop preserves manual pause; bomb DOWN obeys haptics preference');
    const hudState=(await state()).soundEnabled;
    await tapSwf(22,371);await waitSound(!hudState);await page.waitForTimeout(150);assert.equal(sound,!hudState);
    note('original SWF music button synchronizes the same native preference');
    const saved=(await state()).continueInfo;assert.equal((await page.evaluate(()=>document.querySelector('ruffle-player').ruffle().callExternalInterface('xanaduFlushSave'))).saveOk,true);
    token='browser-native-3';await page.reload();await ready();assert(await page.locator('#continue-run').isVisible());await shot('continue-menu');
    assert.equal(await page.locator('#continue-run').textContent(),'继续闯关 · '+({easy:'简单',normal:'普通',hard:'困难'})[saved.difficulty]+' · 第'+saved.stage+'/'+saved.totalStages+'关');
    await page.locator('#continue-run').tap();assert((await page.locator('#modal-copy').textContent()).includes('地图重新生成'));
    await shot('confirm-continue');await page.touchscreen.tap(4,4);await page.locator('#modal').waitFor({state:'hidden'});
    assert.deepEqual((await state()).continueInfo,saved);
    await page.locator('#continue-run').tap();await page.locator('#modal-confirm').tap();await page.waitForFunction(()=>BombItHost.status().phase==='battle'&&!BombItHost.status().game.restoring);
    const resumed=await state();assert.equal(resumed.stage,saved.stage);assert.equal(resumed.mode,saved.mode);assert(resumed.remainingTime>175);
    assert.equal(resumed.continueInfo.totalScore1,saved.totalScore1);await shot('continued-battle');
    note('real checkpoint survives document reload and continue UI restores the saved stage', {saved,resumed});
    assert.deepEqual(report.errors,[]);report.ok=true;
  }catch(e){report.ok=false;report.failure=e.stack;throw e;}
  finally{fs.writeFileSync(path.join(output,'result.json'),JSON.stringify(report,null,2)+'\n');await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
