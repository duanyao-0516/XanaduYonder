// Real OpenFL engine checks; private access is response-only and never packaged.
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'/tmp/4399-package-tools/node_modules/playwright-core');
const root=path.resolve(__dirname,'../..'),content=path.join(root,'ThirdParty/LevelDevil/generated');
const output=path.join(root,'artifacts/research/only-one-door-243372/runtime-validation');
const origin='https://level-devil.xanaduyonder.invalid/assets/level-devil/';
(async()=>{
  const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});
  try {
    const context=await browser.newContext({viewport:{width:1280,height:600},deviceScaleFactor:3,hasTouch:true,serviceWorkers:'block'});
    const errors=[],blocked=[],messages=[],layouts=[];
    await context.exposeBinding('recordNative',(_,message)=>messages.push(message));
    await context.addInitScript(()=>{window.__native=[];window.__exitNumber=0;window.LevelDevilNative={postMessage:raw=>{const message=JSON.parse(raw);__native.push(message);window.recordNative(message);if(message.type==='exit-intent')setTimeout(()=>LevelDevilHost.requestExit('hall-'+(++__exitNumber)),0);}};});
    await context.route('**/*',async route=>{
      const url=route.request().url();if(!url.startsWith(origin)||route.request().method()!=='GET'){blocked.push(url);return route.abort();}
      const name=decodeURIComponent(url.slice(origin.length)),file=path.join(content,name);
      if(name.includes('..')||!fs.existsSync(file)){blocked.push(url);return route.abort();}
      let data=fs.readFileSync(file);
      if(name==='Level Devil.js')data=Buffer.from(data.toString().replace('Jb.period=3E-4;','Jb.period=3E-4;window.__test={E:k,C:c,Q:Q,F:F,classes:l};'));
      await route.fulfill({body:data,contentType:name.endsWith('.js')?'application/javascript':name.endsWith('.css')?'text/css':name.endsWith('.html')?'text/html':name.endsWith('.mp3')?'audio/mpeg':'application/octet-stream'});
    });
    const page=await context.newPage();page.setDefaultTimeout(60000);page.on('pageerror',e=>errors.push(String(e)));
    await page.goto(origin+'index.html',{timeout:60000});await page.waitForFunction(()=>__native.some(m=>m.type==='ready'));
    await page.evaluate(()=>LevelDevilHost.setViewportInsets(32,8,24,12));await page.waitForTimeout(700);
    async function dimensions(){return page.evaluate(()=>{const E=__test.E,e=E.engine,c=document.querySelector('canvas'),r=c.getBoundingClientRect();return{viewport:[innerWidth,innerHeight],canvasCss:[r.x,r.y,r.width,r.height],logical:[E.screenWidth,E.screenHeight],stage:[E.stage.stageWidth,E.stage.stageHeight],scale:E.SCALE,unit:E.screenScaleX*E.SCALE,scaleY:E.screenScaleY*E.SCALE,offset:[E.screenOffsetX,E.screenOffsetY],masterClip:e.master.get_scrollRect()&&[e.master.get_scrollRect().width,e.master.get_scrollRect().height]};});}
    async function switchScene(id){await page.evaluate(id=>{LevelDevilHost.cancelExit();__test.C.switchScene(id,null,null);},id);await page.waitForFunction(id=>__test.E.engine.scene.ID===id&&!__test.E.engine.isTransitioning(),id);await page.waitForTimeout(700);await page.waitForFunction(()=>{const C=__test.C;return C.getActorsOfType(C.getActorType(710)).every(a=>a.dead||a.recycled||a.getScreenY()<=-135.9)&&C.getActorsOfType(C.getActorType(712)).every(a=>a.dead||a.recycled||a.getScreenY()>=C.getScreenHeight()-.1);});}
    async function hallPosition(){return page.evaluate(()=>{const {C,E}=__test,a=C.getActorsOfType(C.getActorType(759)).find(a=>a.xanaduHallBack&&!a.recycled&&!a.dead);return {x:(a.getScreenX()+8)*E.SCALE*E.screenScaleX+E.screenOffsetX,y:(a.getScreenY()+8)*E.SCALE*E.screenScaleY+E.screenOffsetY,animation:a.getAnimation(),width:a.getWidth()*a.realScaleX*E.SCALE*E.screenScaleX,left:(a.getScreenX()-a.currOrigin.x*(a.realScaleX-1))*E.SCALE*E.screenScaleX+E.screenOffsetX,top:(a.getScreenY()-a.currOrigin.y*(a.realScaleY-1))*E.SCALE*E.screenScaleY+E.screenOffsetY,scene:E.engine.scene.name};});}
    for(const id of [125,49,174]){
      if(id!==125)await switchScene(id);
      const p=await hallPosition();await page.screenshot({path:path.join(output,'viewport-menu-'+id+'.png'),scale:'css'});
      const before=await page.evaluate(()=>__native.filter(m=>m.type==='exit-ready').length);await page.mouse.click(p.x,p.y);
      await page.waitForFunction(before=>__native.filter(m=>m.type==='exit-ready').length===before+1,before);
      assert.equal(await page.evaluate(()=>__test.E.engine.scene.name),p.scene,'hall action does not invoke original map navigation');
      assert.equal(await page.evaluate(()=>__native.filter(m=>m.type==='exit-intent').length),id===125?1:id===49?2:3);
      await page.evaluate(()=>LevelDevilHost.cancelExit());
    }
    // A real menu click must preserve the checkpoint and stay in the game on
    // write failure, then become clickable again after native cancel/retry.
    const before=await page.evaluate(()=>{__test.C.saveGame('mySave');const saved=localStorage.getItem('UnicycleLegend:mySave'),set=Storage.prototype.setItem;window.__restoreStorage=()=>Storage.prototype.setItem=set;Storage.prototype.setItem=function(){throw new DOMException('quota','QuotaExceededError');};return saved;});
    let p=await hallPosition();await page.mouse.click(p.x,p.y);
    await page.waitForFunction(()=>__native.some(m=>m.type==='exit-error'&&m.requestId==='hall-4'));
    assert.equal(await page.evaluate(()=>localStorage.getItem('UnicycleLegend:mySave')),before);
    assert.equal(await page.evaluate(()=>__native.some(m=>m.type==='exit-ready'&&m.requestId==='hall-4')),false);
    await page.evaluate(()=>{__restoreStorage();LevelDevilHost.cancelExit();});p=await hallPosition();await page.mouse.click(p.x,p.y);
    await page.waitForFunction(()=>__native.some(m=>m.type==='exit-ready'&&m.requestId==='hall-5'));await page.evaluate(()=>LevelDevilHost.cancelExit());
    // Resize/insets preserve the exact world; the canvas itself fills the page.
    for(const size of [{width:1280,height:600},{width:854,height:480},{width:640,height:360},{width:600,height:960}]){
      await page.setViewportSize(size);await page.waitForTimeout(100);const d=await dimensions();layouts.push(d);
      assert.deepEqual(d.logical,[427,240]);assert.deepEqual(d.canvasCss,[0,0,size.width,size.height]);assert.equal(d.unit,d.scaleY);
      assert(d.offset[0]>=31.9&&d.offset[1]>=7.9);assert(d.offset[0]+427*d.unit<=size.width-23.9);assert(d.offset[1]+240*d.unit<=size.height-11.9);
      assert.deepEqual(d.masterClip,[427*d.scale,240*d.scale]);const icon=await hallPosition();assert(icon.width>=47.99);assert(icon.left>=32&&icon.top>=8);
    }
    await page.setViewportSize({width:1280,height:600});await page.waitForTimeout(100);
    await page.evaluate(()=>{const scenes=Object.values(__test.F.get().scenes.h);__test.C.switchScene(scenes.find(s=>s.name==='Pits_1').getID(),null,null);});
    await page.waitForFunction(()=>__test.E.engine.scene.name==='Pits_1'&&!__test.E.engine.isTransitioning());await page.waitForTimeout(700);
    async function buttons(duo){return page.evaluate(duo=>{const {E}=__test,m=E.engine.behaviors.getBehavior(duo?'2P Scene_Manager':'Scene_Manager'),fields=duo?['_BtnP1Left','_BtnP1Right','_BtnP1Jump','_BtnP2Left','_BtnP2Right','_BtnP2Jump']:['_BtnLeft','_BtnRight','_BtnJump'];return fields.map(field=>{let a=m[field];return{x:(a.getScreenX()+a.getWidth()/2)*E.SCALE*E.screenScaleX+E.screenOffsetX,y:(a.getScreenY()+a.getHeight()/2)*E.SCALE*E.screenScaleY+E.screenOffsetY,left:a.getScreenX()*E.SCALE*E.screenScaleX+E.screenOffsetX,right:(a.getScreenX()+a.getWidth())*E.SCALE*E.screenScaleX+E.screenOffsetX};});},duo);}
    const cdp=await context.newCDPSession(page);let b=await buttons(false),d=await dimensions();assert(b[0].left<d.offset[0]);assert(b[2].right>d.offset[0]+427*d.unit);
    await page.screenshot({path:path.join(output,'viewport-single.png'),scale:'css'});
    await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:b[0].x,y:b[0].y,id:1},{x:b[2].x,y:b[2].y,id:2}]});
    assert.deepEqual(await page.evaluate(()=>[__test.C.isKeyDown('Left'),__test.C.isKeyDown('Up')]),[true,true]);
    await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[{x:b[0].x,y:b[0].y,id:1}]});assert.deepEqual(await page.evaluate(()=>[__test.C.isKeyDown('Left'),__test.C.isKeyDown('Up')]),[false,true]);
    await cdp.send('Input.dispatchTouchEvent',{type:'touchCancel',touchPoints:[]});
    const originalBack=await page.evaluate(()=>{const {C,E}=__test,a=C.getActorsOfType(C.getActorType(759)).find(a=>!a.dead&&!a.recycled);return{marked:!!a.xanaduHallBack,x:(a.getScreenX()+8)*E.SCALE*E.screenScaleX+E.screenOffsetX,y:(a.getScreenY()+8)*E.SCALE*E.screenScaleY+E.screenOffsetY};});assert.equal(originalBack.marked,false);const intentCount=await page.evaluate(()=>__native.filter(m=>m.type==='exit-intent').length);await page.mouse.click(originalBack.x,originalBack.y);await page.waitForFunction(()=>__test.E.engine.scene.name==='Z_Title'&&!__test.E.engine.isTransitioning());assert.equal(await page.evaluate(()=>__native.filter(m=>m.type==='exit-intent').length),intentCount);await switchScene(106);b=await buttons(true);await page.screenshot({path:path.join(output,'viewport-duo.png'),scale:'css'});
    const keys=['A','D','w','Left','Right','Up'];for(let i=0;i<6;i++){await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:b[i].x,y:b[i].y,id:1}]});assert.equal(await page.evaluate(key=>__test.C.isKeyDown(key),keys[i]),true,keys[i]+' original sprite matches hit zone');await cdp.send('Input.dispatchTouchEvent',{type:'touchCancel',touchPoints:[]});}
    await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:b[2].x,y:b[2].y,id:1},{x:b[5].x,y:b[5].y,id:2}]});assert.deepEqual(await page.evaluate(()=>[__test.C.isKeyDown('w'),__test.C.isKeyDown('Up')]),[true,true]);
    await page.evaluate(()=>LevelDevilHost.setViewportInsets(40,8,24,12));assert.deepEqual(await page.evaluate(()=>[__test.C.isKeyDown('w'),__test.C.isKeyDown('Up')]),[false,false]);await cdp.send('Input.dispatchTouchEvent',{type:'touchCancel',touchPoints:[]});
    assert.deepEqual(errors,[]);assert.deepEqual(blocked,[]);
    const report={ok:true,aarSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'Assets/Plugins/Android/LevelDevilContent.aar'))).digest('hex'),checks:['original back icon clicks on 125/49/174 use native save handshake','quota failure preserves checkpoint and cancel permits retry','full canvas at DPR 3 with safe insets','427x240 world and uniform scale across four viewports','original single/duo sprites at viewport edges','real touchscreen wing controls and independent pointer release','six duo hit zones and resize cancellation','hall icon at least 48 CSS pixels and original gameplay back unchanged'],layouts,messages,errors,blocked};fs.writeFileSync(path.join(output,'viewport-result.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));
  }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
