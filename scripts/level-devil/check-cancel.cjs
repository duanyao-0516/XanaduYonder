// Exercise the real release's Lime/OpenFL cancellation path. CDP supplies the
// full native-like sequence; DOM TouchEvents cover partial-cancel cases that
// CDP's touchCancel command cannot express. No test facade enters the AAR.
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'/tmp/4399-package-tools/node_modules/playwright-core');
const root=path.resolve(__dirname,'../..'),content=path.join(root,'ThirdParty/LevelDevil/generated');
const output=path.join(root,'artifacts/research/only-one-door-243372/runtime-validation');
const origin='https://level-devil.xanaduyonder.invalid/assets/level-devil/';
(async()=>{
  const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});
  try {
    const context=await browser.newContext({viewport:{width:854,height:480},hasTouch:true,serviceWorkers:'block'});
    const errors=[],blocked=[],states=[];
    await context.addInitScript(()=>{window.__native=[];window.LevelDevilNative={postMessage:raw=>__native.push(JSON.parse(raw))};});
    await context.route('**/*',async route=>{
      const url=route.request().url();if(!url.startsWith(origin)){blocked.push(url);return route.abort();}
      const name=decodeURIComponent(url.slice(origin.length)),file=path.join(content,name);
      if(name.includes('..')||!fs.existsSync(file)){blocked.push(url);return route.abort();}
      let data=fs.readFileSync(file);
      if(name==='Level Devil.js')data=Buffer.from(data.toString().replace('Jb.period=3E-4;','Jb.period=3E-4;window.__test={E:k,C:c,Q:Q,classes:l};'));
      await route.fulfill({body:data,contentType:name.endsWith('.js')?'application/javascript':name.endsWith('.html')?'text/html':name.endsWith('.css')?'text/css':name.endsWith('.mp3')?'audio/mpeg':'application/octet-stream'});
    });
    const page=await context.newPage();page.setDefaultTimeout(60000);page.on('pageerror',error=>errors.push(String(error)));
    await page.goto(origin+'index.html');await page.waitForFunction(()=>__native.some(message=>message.type==='ready'));
    const cdp=await context.newCDPSession(page);
    async function duo(){await page.evaluate(()=>__test.C.switchScene(106,null,null));await page.waitForFunction(()=>__test.E.engine.scene.name==='2P_1_Blah'&&!__test.E.engine.isTransitioning());await page.waitForTimeout(700);}
    async function state(name){const value=await page.evaluate(name=>({name,scene:__test.E.engine.scene.name,mouseDown:__test.Q.mouseDown,mousePressed:__test.Q.mousePressed,mouseReleased:__test.Q.mouseReleased,touches:__test.Q.numTouches,keys:[__test.C.isKeyDown('w'),__test.C.isKeyDown('Up')]}),name);states.push(value);return value;}
    async function touch(type,points){await cdp.send('Input.dispatchTouchEvent',{type,touchPoints:points});}
    async function dom(events){await page.evaluate(events=>{const canvas=document.querySelector('canvas');for(const event of events){if(event.pause)LevelDevilHost.setHostState(false,false);const make=point=>new Touch({identifier:point.id,target:canvas,clientX:point.x,clientY:point.y,pageX:point.x,pageY:point.y,screenX:point.x,screenY:point.y});canvas.dispatchEvent(new TouchEvent(event.type,{bubbles:true,cancelable:true,changedTouches:event.changed.map(make),touches:event.remaining.map(make),targetTouches:event.remaining.map(make)}));}},events);}
    async function backPosition(){return page.evaluate(()=>{const {E,C}=__test,a=C.getActorsOfType(C.getActorType(759)).find(a=>!a.dead&&!a.recycled);return{x:(a.getScreenX()+8)*E.SCALE*E.screenScaleX+E.screenOffsetX,y:(a.getScreenY()+8)*E.SCALE*E.screenScaleY+E.screenOffsetY};});}
    async function realBack(){const pos=await backPosition();await touch('touchStart',[{...pos,id:3}]);await page.waitForTimeout(80);await touch('touchEnd',[]);await page.waitForTimeout(1200);}
    await duo();await touch('touchStart',[{x:336,y:432,id:1},{x:774,y:432,id:2}]);await page.waitForTimeout(80);
    assert.deepEqual((await state('two real pointers down')).keys,[true,true]);await touch('touchCancel',[]);
    const full=await state('after real full cancel');assert.equal(full.mouseDown,false,'primary cancel clears simulated mouse latch');assert.equal(full.touches,0);assert.deepEqual(full.keys,[false,false]);
    await realBack();assert.equal((await state('first 80ms Back after cancel')).scene,'Z_Title');
    await duo();const primary={id:101,x:336,y:432},secondary={id:102,x:774,y:432};
    await dom([{type:'touchstart',changed:[primary,secondary],remaining:[primary,secondary]}]);await page.waitForTimeout(50);
    await dom([{type:'touchcancel',changed:[primary],remaining:[secondary]}]);const partialPrimary=await state('cancel only primary');
    assert.equal(partialPrimary.mouseDown,false);assert.equal(partialPrimary.touches,1);assert.deepEqual(partialPrimary.keys,[false,true]);
    const newPrimary={...await backPosition(),id:103};await dom([{type:'touchstart',changed:[newPrimary],remaining:[secondary,newPrimary]}]);await page.waitForTimeout(80);
    assert.equal((await state('new Back press preserves remaining finger')).keys[1],true);
    await dom([{type:'touchend',changed:[newPrimary],remaining:[secondary]}]);await page.waitForTimeout(1200);
    assert.equal((await state('Back after partial primary cancel')).scene,'Z_Title');await dom([{type:'touchcancel',changed:[secondary],remaining:[]}]);
    await duo();const p2={id:201,x:336,y:432},s2={id:202,x:774,y:432};
    await dom([{type:'touchstart',changed:[p2,s2],remaining:[p2,s2]}]);await page.waitForTimeout(50);
    await dom([{type:'touchcancel',changed:[s2],remaining:[p2]}]);const partialSecondary=await state('cancel only secondary');
    assert.equal(partialSecondary.mouseDown,true);assert.equal(partialSecondary.touches,1);assert.deepEqual(partialSecondary.keys,[true,false]);
    await dom([{type:'touchcancel',changed:[p2],remaining:[]}]);
    // Both events occur before an engine update, so CANCEL must not manufacture
    // a mouse release/click or allow the canceled DOWN to activate original Back.
    const canceledBack={...await backPosition(),id:301};
    await dom([{type:'touchstart',changed:[canceledBack],remaining:[canceledBack]},{type:'touchcancel',changed:[canceledBack],remaining:[]}]);
    const canceled=await state('Back canceled before update');assert.equal(canceled.mousePressed,false);assert.equal(canceled.mouseReleased,false);
    await page.waitForTimeout(1200);assert.equal((await state('canceled Back did not navigate')).scene,'2P_1_Blah');
    // A host pause blocks gameplay input, but an OS cancellation must still
    // reach Lime so its primary-touch identity does not survive the pause.
    const held={id:401,x:336,y:432};await dom([{type:'touchstart',changed:[held],remaining:[held]}]);
    await page.evaluate(()=>LevelDevilHost.setHostState(false,false));await dom([{type:'touchcancel',changed:[held],remaining:[]}]);
    await page.evaluate(()=>LevelDevilHost.setHostState(true,false));await realBack();
    assert.equal((await state('first Back after paused cancellation')).scene,'Z_Title','paused cancellation must retire Lime primary identity');
    await duo();await page.evaluate(()=>{window.__cleanupClicks=0;window.__cleanupReleases=0;__test.E.stage.addEventListener('click',()=>__cleanupClicks++);__test.E.stage.addEventListener('releaseOutside',()=>__cleanupReleases++);});
    const endedBack={...await backPosition(),id:501};
    await dom([{type:'touchstart',changed:[endedBack],remaining:[endedBack]},{type:'touchend',changed:[endedBack],remaining:[],pause:true}]);
    assert.deepEqual(await page.evaluate(()=>[__cleanupClicks,__cleanupReleases]),[0,0],'host-canceled normal END emits no click/releaseOutside');
    const pausedEnd=await state('normal END during host pause');assert.equal(pausedEnd.mouseDown,false);assert.equal(pausedEnd.mousePressed,false);assert.equal(pausedEnd.mouseReleased,false);
    await page.evaluate(()=>LevelDevilHost.setHostState(true,false));await page.waitForTimeout(800);
    assert.equal((await state('host-canceled END did not navigate')).scene,'2P_1_Blah');
    await realBack();assert.equal((await state('first Back after paused normal END')).scene,'Z_Title');
    assert.deepEqual(errors,[]);assert.deepEqual(blocked,[]);
    const report={ok:true,aarSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'Assets/Plugins/Android/LevelDevilContent.aar'))).digest('hex'),checks:['real two-pointer CANCEL releases mouse and keys','first real 80ms Back press works after cancel','partial primary cancel preserves secondary key and permits new click','partial secondary cancel preserves primary mouse and key','cancel before update never clicks Back','paused cancellation retires primary identity','paused normal END retires identity without click/releaseOutside and next Back works'],states,errors,blocked};
    fs.writeFileSync(path.join(output,'cancel-result.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
