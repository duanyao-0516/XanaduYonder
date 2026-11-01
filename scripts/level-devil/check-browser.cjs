// Exercises the real release in Chrome. Private engine access is injected only
// into the response used by this test; it is never packaged in the AAR.
const fs = require('fs'), path = require('path'), assert = require('assert/strict'), crypto = require('crypto');
const {chromium} = require(process.env.PLAYWRIGHT_MODULE || '/tmp/4399-package-tools/node_modules/playwright-core');
const root = path.resolve(__dirname, '../..');
const baseline = process.argv.includes('--baseline');
const fallback = process.argv.includes('--prompt-smoke');
const uiSmoke = process.argv.includes('--ui-smoke');
const content = path.join(root, baseline ? 'ThirdParty/LevelDevil/original' : 'ThirdParty/LevelDevil/generated');
const output = process.env.LEVEL_DEVIL_CHECK_OUTPUT || path.join(root,'artifacts/research/only-one-door-243372/runtime-validation');
fs.mkdirSync(output,{recursive:true});
const origin = 'https://level-devil.xanaduyonder.invalid/assets/level-devil/';
(async () => {
  const browser = await chromium.launch({executablePath: process.env.CHROME_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true});
  try {
    const context = await browser.newContext({viewport: {width:854,height:480}, hasTouch:true, serviceWorkers:'block'});
    const blocked = [], errors = [], messages = [], requests = new Set();
    await context.exposeBinding('recordNative', (_, value) => messages.push(JSON.parse(value)));
    await context.addInitScript(fallback => { if(!fallback)window.LevelDevilNative = {postMessage: value => window.recordNative(value)};window.addEventListener('error',e=>console.log('RAWERROR',e.error&&e.error.get_message?e.error.get_message():e.message,JSON.stringify(e.error)));window.addEventListener('unhandledrejection',e=>console.log('RAWREJECT',String(e.reason))); }, fallback);
    await context.route('**/*', async route => {
      const url = route.request().url();
      if (!url.startsWith(origin) || route.request().method() !== 'GET') {blocked.push(url); return route.abort();}
      const name = decodeURIComponent(url.slice(origin.length));requests.add(name);
      if (name.includes('..')) return route.abort();
      const file = path.join(content, name);
      let data;
      if (baseline && name === 'index.html') data = Buffer.from('<div id="openfl-content"></div><script src="Level Devil.js"></script><script>lime.embed("Level Devil","openfl-content",854,480,{parameters:{}})</script>');
      else if (fs.existsSync(file)) data = fs.readFileSync(file);
      else {blocked.push(url); return route.abort();}
      if (name === 'Level Devil.js') data = Buffer.from(data.toString().replace('Jb.period=3E-4;', 'Jb.period=3E-4;window.__test={E:k,C:c,Q:Q,classes:l,Sg:Sg,Rn:Rn,SO:nd,Serializer:He,Unserializer:Pe};'));
      const mime = name.endsWith('.css') ? 'text/css' : name.endsWith('.js') ? 'application/javascript' : name.endsWith('.html') ? 'text/html' : name.endsWith('.json') ? 'application/json' : name.endsWith('.mp3') ? 'audio/mpeg' : name.endsWith('.png') ? 'image/png' : 'application/octet-stream';
      return route.fulfill({body:data, contentType:mime});
    });
    const page = await context.newPage(); page.setDefaultTimeout(60000); page.on('pageerror', e => {errors.push(String(e));console.log('PAGEERROR',e.stack);});page.on('console',m=>{console.log('CONSOLE',m.text());});
    if(fallback)page.on('dialog',async dialog=>{assert(dialog.message().startsWith('xanadu-level-devil:'));messages.push(JSON.parse(dialog.message().slice('xanadu-level-devil:'.length)));await dialog.accept('');});
    await page.goto(origin+'index.html',{timeout:60000});
    await page.waitForTimeout(1500);
    assert.equal(await page.evaluate(() => typeof window.LevelDevilHost), 'object', 'release must expose an offline host adapter');
    await page.waitForFunction(() => window.__test.E.engine?.scene?.name === 'Z_Choose_1P_2P', null, {timeout:60000});
    await page.waitForFunction(() => window.__test.E.engine.sceneInitialized && !window.__test.E.engine.isTransitioning());
    await page.waitForTimeout(100);
    assert(messages.some(m => m.type === 'ready'), 'ready must follow interactive menu');
    if(fallback){assert.deepEqual(blocked,[]);assert.deepEqual(errors,[]);const report={ok:true,checks:['API26 prompt transport startup'],messages};fs.writeFileSync(path.join(output,'prompt-result.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));return;}
    await page.mouse.click(285,333);
    await page.waitForFunction(() => window.__test.E.engine.scene.name !== 'Z_Choose_1P_2P' && !window.__test.E.engine.isTransitioning(), null, {timeout:60000});
    assert.equal(await page.evaluate(() => window.__test.E.engine.scene.name),'Pits_1');
    await page.screenshot({path:path.join(output,'single-player.png')});
    if(uiSmoke) {
      for(const type of [708,959]) {
        await page.evaluate(type=>{
          __test.E.engine.gameAttributes.h.Speedrun_Unlocked=false;
          __test.C.createRecycledActor(__test.C.getActorType(type),40,40,0);
          window.__rewardActor=__test.C.getLastCreatedActor();__rewardActor.setScreenX(40);__rewardActor.setScreenY(40);
        },type);
        await page.waitForTimeout(100);await page.screenshot({path:path.join(output,'free-unlock-'+type+'.png')});
        await page.evaluate(type=>__rewardActor.behaviors.behaviors.find(b=>b.script instanceof __test.classes['scripts.ActorEvents_'+type]).script._event_Clicked(3),type);
        await page.waitForTimeout(80);
        assert.equal(await page.evaluate(()=>__test.E.engine.getGameAttribute('Speedrun_Unlocked')),true);
        assert.equal(await page.evaluate(()=>__test.E.paused),false);
        await page.screenshot({path:path.join(output,'free-unlock-'+type+'-pressed.png')});
        await page.waitForTimeout(1200);
        await page.waitForFunction(()=>!__test.E.engine.isTransitioning());
      }
      await page.evaluate(()=>__test.C.createRecycledActor(__test.C.getActorType(762),367,-16,0));
      await page.waitForTimeout(700);await page.screenshot({path:path.join(output,'free-skip.png')});
      await page.mouse.click(786,40);
      await page.waitForFunction(()=>__test.C.getActorsOfType(__test.C.getActorType(762)).some(a=>a.behaviors.behaviors.some(b=>b.script instanceof __test.classes['scripts.ActorEvents_762']&&b.script._Clicked)));
      await page.screenshot({path:path.join(output,'free-skip-pressed.png')});
      await page.evaluate(()=>LevelDevilHost.requestExit('ui-skip'));
      for(let i=0;i<100&&!messages.some(m=>m.type==='exit-ready'&&m.requestId==='ui-skip');i++)await page.waitForTimeout(100);
      assert.equal(messages.filter(m=>m.type==='exit-ready'&&m.requestId==='ui-skip').length,1);
      assert.equal(await page.evaluate(()=>new __test.Unserializer(localStorage.getItem('UnicycleLegend:mySave')).unserialize().Recent_Level),'Pits_2');
      assert.deepEqual(blocked,[]);assert.deepEqual(errors,[]);
      const report={ok:true,aarSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'Assets/Plugins/Android/LevelDevilContent.aar'))).digest('hex'),checks:['both free unlock controls and pressed feedback','free skip control and pressed feedback','original unlock and skip actions','skip exit persistence'],messages,blocked,errors};
      fs.writeFileSync(path.join(output,'ui-result.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));return;
    }
    const cdp=await context.newCDPSession(page);
    await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:300,y:360,id:1},{x:320,y:360,id:2}]});
    assert.equal(await page.evaluate(()=>__test.C.isKeyDown('Right')),true,'two real touch points press Right');
    await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[{x:300,y:360,id:1}]});
    assert.equal(await page.evaluate(()=>__test.C.isKeyDown('Right')),true,'releasing one real pointer preserves the other');
    await cdp.send('Input.dispatchTouchEvent',{type:'touchCancel',touchPoints:[]});
    assert.equal(await page.evaluate(()=>__test.C.isKeyDown('Right')),false,'real OS touch cancellation releases controls');
    const input = await page.evaluate(() => {
      const {E,C,Q}=__test, manager=E.engine.behaviors.getBehavior('Scene_Manager');
      function event(id,x,y){return {touchPointID:id,stageX:x*E.screenScaleX*E.SCALE+E.screenOffsetX,stageY:y*E.screenScaleY*E.SCALE+E.screenOffsetY};}
      manager._event_TouchButtonControls(event(1,40,180)); manager._event_ReleaseButtonControls(event(1,300,40));
      const slidOut=!C.isKeyDown('Left');
      manager._event_TouchButtonControls(event(1,150,180));manager._event_TouchButtonControls(event(2,160,180));manager._event_ReleaseButtonControls(event(2,160,180));
      const sharedHeld=C.isKeyDown('Right');LevelDevilHost.releaseInput();
      return {slidOut,sharedHeld,released:!C.isKeyDown('Right')};
    });
    assert.deepEqual(input,{slidOut:true,sharedHeld:true,released:true});
    const paused = await page.evaluate(() => { __test.E.engine.pause(); LevelDevilHost.setHostState(false,false); return __test.E.totalElapsedTime; });
    await page.waitForTimeout(300);
    assert.equal(await page.evaluate(() => __test.E.totalElapsedTime),paused,'host pause freezes engine tick');
    assert.equal(await page.evaluate(() => {LevelDevilHost.setHostState(true,true);return __test.E.paused;}),true,'host resume preserves original game pause');
    await page.evaluate(() => __test.E.engine.unpause());
    const before = await page.evaluate(() => {__test.E.engine.gameAttributes.h.Costume_B=1;__test.C.saveGame('mySave');return localStorage.getItem('UnicycleLegend:mySave');});
    assert(before && before.length>100,'original serializer writes full checkpoint');
    const quota = await page.evaluate(() => { const set=Storage.prototype.setItem;window.__restoreStorage=()=>Storage.prototype.setItem=set;Storage.prototype.setItem=function(){throw new DOMException('quota','QuotaExceededError');};LevelDevilHost.requestExit('quota');return localStorage.getItem('UnicycleLegend:mySave');});
    assert.equal(quota,before,'write failure preserves previous save');
    await page.waitForTimeout(100);
    assert(messages.some(m=>m.type==='exit-error' && m.requestId==='quota'),'quota failure must not silently exit');
    assert(!messages.some(m=>m.type==='exit-ready' && m.requestId==='quota'));
    await page.evaluate(() => {__restoreStorage();LevelDevilHost.cancelExit();});
    const completion = await page.evaluate(() => { __test.C.shoutToScene('_customEvent_level_complete'); LevelDevilHost.requestExit('complete'); return {at:performance.now(),before:localStorage.getItem('UnicycleLegend:mySave')}; });
    await page.waitForTimeout(1000);
    assert(!messages.some(m=>m.type==='exit-ready' && m.requestId==='complete'),'exit must wait through original completion update');
    for(let i=0;i<100 && !messages.some(m=>m.type==='exit-ready' && m.requestId==='complete');i++) await page.waitForTimeout(100);
    assert.equal(messages.filter(m=>m.type==='exit-ready' && m.requestId==='complete').length,1,'completion exit acknowledges once after saving');
    const after=await page.evaluate(() => localStorage.getItem('UnicycleLegend:mySave'));
    assert.notEqual(after,completion.before,'completion progress survives exit transaction');
    assert.equal(await page.evaluate(() => new __test.Unserializer(localStorage.getItem('UnicycleLegend:mySave')).unserialize().Recent_Level),'Pits_2','exit saves the new scene checkpoint');
    await page.evaluate(()=>{LevelDevilHost.cancelExit();__test.C.shoutToScene('_customEvent_level_complete');});
    await page.keyboard.press('r');await page.waitForTimeout(3500);
    assert.equal(await page.evaluate(()=>__test.E.engine.scene.name),'Pits_2','original retry cancels completion');
    await page.evaluate(()=>LevelDevilHost.requestExit('interrupted-completion'));
    for(let i=0;i<30&&!messages.some(m=>m.type==='exit-ready'&&m.requestId==='interrupted-completion');i++)await page.waitForTimeout(100);
    assert(messages.some(m=>m.type==='exit-ready'&&m.requestId==='interrupted-completion'),'canceled completion must not poison future exit');
    assert.equal(await page.evaluate(()=>new __test.Unserializer(localStorage.getItem('UnicycleLegend:mySave')).unserialize().Recent_Level),'Pits_2','retry does not grant completion');
    await page.evaluate(()=>LevelDevilHost.cancelExit());
    for(const type of [708,959]) {
      await page.evaluate(type=>{__test.E.engine.gameAttributes.h.Speedrun_Unlocked=false;__test.C.createRecycledActor(__test.C.getActorType(type),40,40,0);window.__rewardActor=__test.C.getLastCreatedActor();__rewardActor.setScreenX(40);__rewardActor.setScreenY(40);},type);
      await page.waitForTimeout(100);await page.screenshot({path:path.join(output,'free-unlock-'+type+'.png')});
      await page.evaluate(type=>{const behavior=__rewardActor.behaviors.behaviors.find(b=>b.script instanceof __test.classes['scripts.ActorEvents_'+type]).script;behavior._event_Clicked(3);},type);
      await page.waitForTimeout(100);
      assert.equal(await page.evaluate(()=>__test.E.engine.getGameAttribute('Speedrun_Unlocked')),true,'original speedrun '+type+' action unlocks locally');
      assert.equal(await page.evaluate(()=>__test.E.paused),false,'local reward continuation unpauses game');
      await page.waitForTimeout(1200);
    }
    await page.evaluate(()=>{__test.C.createRecycledActor(__test.C.getActorType(762),367,-16,0);});
    await page.waitForTimeout(700);
    await page.screenshot({path:path.join(output,'free-skip.png')});
    await page.mouse.click(786,40);
    await page.waitForFunction(()=>__test.C.getActorsOfType(__test.C.getActorType(762)).some(a=>a.behaviors.behaviors.some(b=>b.script instanceof __test.classes['scripts.ActorEvents_762']&&b.script._Clicked)));
    await page.evaluate(()=>LevelDevilHost.requestExit('skip'));
    await page.waitForTimeout(300);
    assert(!messages.some(m=>m.type==='exit-ready' && m.requestId==='skip'),'exit waits for original skip timer');
    for(let i=0;i<100 && !messages.some(m=>m.type==='exit-ready' && m.requestId==='skip');i++) await page.waitForTimeout(100);
    assert.equal(messages.filter(m=>m.type==='exit-ready' && m.requestId==='skip').length,1,'skip completes and saves before exit');
    assert.equal(await page.evaluate(()=>new __test.Unserializer(localStorage.getItem('UnicycleLegend:mySave')).unserialize().Recent_Level),'Pits_3');
    await page.evaluate(()=>{LevelDevilHost.cancelExit();const E=__test.E,m=E.engine.behaviors.getBehavior('Scene_Manager');m._event_TouchButtonControls({touchPointID:99,stageX:40*E.screenScaleX*E.SCALE+E.screenOffsetX,stageY:180*E.screenScaleY*E.SCALE+E.screenOffsetY});__test.C.switchScene(106,null,null);});
    await page.waitForFunction(()=>__test.E.engine.scene.name==='2P_1_Blah'&&!__test.E.engine.isTransitioning());
    await page.waitForTimeout(500);
    assert.equal(await page.evaluate(()=>__test.C.isKeyDown('Left')),false,'held input clears before destroyed scene actors');
    await page.screenshot({path:path.join(output,'two-player.png')});
    const duo=await page.evaluate(()=>{
      const {E,C}=__test,manager=E.engine.behaviors.behaviors.find(b=>b.script instanceof __test.Rn).script;
      const event=(id,x,y)=>({touchPointID:id,stageX:x*E.screenScaleX*E.SCALE+E.screenOffsetX,stageY:y*E.screenScaleY*E.SCALE+E.screenOffsetY});
      manager._event_TouchButtonControls(event(1,160,180));manager._event_DragButtonControls(event(1,210,180));manager._event_ReleaseButtonControls(event(1,210,180));return !C.isKeyDown('w');
    });assert(duo,'duo jump releases after sliding outside its button');
    const checkpoint=await page.evaluate(()=>{__test.C.saveGame('mySave');return localStorage.getItem('UnicycleLegend:mySave');});
    await page.evaluate(()=>{localStorage.setItem('UnicycleLegend:mySave','broken');});
    const messagesBeforeRecovery=messages.length;
    await page.reload({timeout:60000});await page.waitForFunction(()=>window.__test?.E.engine?.scene?.name==='Z_Choose_1P_2P'&&!__test.E.engine.isTransitioning(),null,{timeout:60000});
    assert(messages.slice(messagesBeforeRecovery).some(m=>m.type==='error'&&m.recoverable===true),'backup recovery is explicitly reported');
    assert.equal(await page.evaluate(()=>localStorage.getItem('UnicycleLegend:mySave:damaged')),'broken','corrupt original is retained');
    await page.evaluate(()=>{localStorage.setItem('UnicycleLegend:mySave','bad-primary');localStorage.setItem('UnicycleLegend:mySave:last-good','bad-backup');});
    const messagesBeforeCorruption=messages.length;
    await page.reload({timeout:60000});
    for(let i=0;i<200&&!messages.slice(messagesBeforeCorruption).some(m=>m.type==='error'&&!m.recoverable);i++)await page.waitForTimeout(100);
    assert(messages.slice(messagesBeforeCorruption).some(m=>m.type==='error'&&!m.recoverable),'invalid primary and backup block load');
    assert(!messages.slice(messagesBeforeCorruption).some(m=>m.type==='ready'),'corrupt data must never become a fresh ready game');
    assert.equal(await page.evaluate(()=>localStorage.getItem('UnicycleLegend:mySave')),'bad-primary','corrupt data is not silently reset');
    assert.deepEqual(blocked, [], 'all requests must be packaged resources');
    assert.deepEqual(errors.filter(e=>!e.includes('存档损坏')), [], 'game has no unexpected script errors');
    const report={ok:true, aarSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'Assets/Plugins/Android/LevelDevilContent.aar'))).digest('hex'), checks:['sdk-free offline startup','interactive ready','pointer ownership','host pause independent from game pause','write failure preserves checkpoint','completion exit waits for original save and new scene','real multitouch cancellation','retry cancels completion without advancing','held touch during scene replacement','both speedrun unlock branches','optional skip exit','duo drag release','backup recovery','corrupt saves fail closed'], messages, requests:Array.from(requests).sort(), blocked, errors};
    fs.writeFileSync(path.join(output,'browser-result.json'),JSON.stringify(report,null,2)+'\n');
    console.log(JSON.stringify(report,null,2));
  } finally { await browser.close(); }
})().catch(e => {console.error(e);process.exitCode=1;});
