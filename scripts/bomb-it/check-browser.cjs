// Runs the packaged SWF in real Chrome; no phone, Unity or network access needed.
// PLAYWRIGHT_MODULE selects an existing playwright-core install. Optional
// --diagnostic substitutes artifacts/bomb-it/diagnostic.swf in this test response
// only, allowing an AS2 trace probe to prove Key.isDown and player movement.
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright-core');
const root=path.resolve(__dirname,'../..'),content=path.join(root,'artifacts/bomb-it/content');
const output=process.env.BOMB_IT_CHECK_OUTPUT || path.join(root,'artifacts/bomb-it/browser');
const diagnostic=process.argv.includes('--diagnostic'),promptSmoke=process.argv.includes('--prompt-smoke');
const origin='https://bomb-it.xanaduyonder.invalid/';
fs.mkdirSync(output,{recursive:true});
(async()=>{
  const browser=await chromium.launch({executablePath:process.env.CHROME_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});
  try{
    const context=await browser.newContext({viewport:{width:844,height:390},hasTouch:true,serviceWorkers:'block',
      ...(promptSmoke?{userAgent:'Mozilla/5.0 (Linux; Android 8.0) AppleWebKit/537.36 Chrome/140.0.0.0 Mobile Safari/537.36'}:{})});
    const messages=[],blocked=[],errors=[],requests=new Set(),traces=[];
    await context.exposeBinding('recordNative',(_,value)=>messages.push(JSON.parse(value)));
    await context.addInitScript(fallback=>{
      if(!fallback)window.BombItNative={postMessage:value=>window.recordNative(value)};
      window.__heldKeys=new Set();window.__keyEvents=[];
      for(const kind of ['keydown','keyup'])window.addEventListener(kind,event=>{
        window.__keyEvents.push([kind,event.keyCode]);
        if(kind==='keydown')window.__heldKeys.add(event.keyCode);else window.__heldKeys.delete(event.keyCode);
      });
    },promptSmoke);
    await context.route('**/*',async route=>{
      const url=new URL(route.request().url());
      if(url.origin!==new URL(origin).origin || route.request().method()!=='GET'){blocked.push(url.href);return route.abort();}
      const name=decodeURIComponent(url.pathname.slice(1));requests.add(name);
      if(!name || name.includes('..') || name.includes('\\')){blocked.push(url.href);return route.abort();}
      const filename=diagnostic&&name==='game.swf'?path.join(root,'artifacts/bomb-it/diagnostic.swf'):path.join(content,name);
      if(!fs.existsSync(filename)){blocked.push(url.href);return route.abort();}
      let body=fs.readFileSync(filename);
      // Exercise native pause before WASM/SWF load completes.
      if(name==='host.js')body=Buffer.concat([body,Buffer.from('\nBombItHost.setHostState(false,false);\n')]);
      const mime={'.html':'text/html','.js':'application/javascript','.css':'text/css','.wasm':'application/wasm','.swf':'application/x-shockwave-flash','.json':'application/json'}[path.extname(filename)] || 'text/plain';
      return route.fulfill({body,contentType:mime});
    });
    const page=await context.newPage();page.setDefaultTimeout(60000);
    page.on('pageerror',e=>errors.push(String(e)));
    page.on('console',m=>{if(m.text().startsWith('XANADU_BOMBIT_DIAG'))traces.push(m.text());});
    if(promptSmoke)page.on('dialog',async dialog=>{
      assert(dialog.message().startsWith('xanadu-bomb-it:'));messages.push(JSON.parse(dialog.message().slice('xanadu-bomb-it:'.length)));await dialog.accept('');
    });
    const cdp=await context.newCDPSession(page);
    const touch=async(type,touchPoints)=>{await cdp.send('Input.dispatchTouchEvent',{type,touchPoints});await page.waitForTimeout(40);};
    const point=async(selector,id,dx=0,dy=0)=>{const b=await page.locator(selector).boundingBox();return{x:b.x+b.width/2+dx,y:b.y+b.height/2+dy,id};};
    const held=()=>page.evaluate(()=>Array.from(__heldKeys).sort((a,b)=>a-b));
    const engine=()=>page.evaluate(()=>{const r=document.querySelector('ruffle-player').ruffle();return{suspended:r.suspended,volume:r.volume};});
    const screenshot=async name=>page.screenshot({path:path.join(output,(diagnostic?'diagnostic-':'')+name+'.png')});
    const flashTap=async(x,y)=>{const b=await page.locator('#bombit-player').boundingBox();await page.touchscreen.tap(b.x+x*b.width/550,b.y+y*b.height/400);await page.waitForTimeout(350);};
    async function chooseStyle(style){
      await page.locator('#settings').tap();await page.locator('[data-input-style="'+style+'"]').tap();
      assert.equal(await page.evaluate(()=>document.body.dataset.input),style);
      assert.equal(await page.evaluate(()=>localStorage.getItem('xanaduyonder.bomb-it.controls.v1')),style);
      if(style==='dpad')await screenshot('settings');
      await page.locator('#modal-confirm').tap();
    }
    async function load(){
      await page.goto(origin+'index.html');await page.waitForFunction(()=>window.BombItHost?.status().ready);
      assert.deepEqual(await engine(),{suspended:true,volume:0},'host pause during cold load is honored');
      assert.equal(await page.evaluate(()=>BombItHost.status().active),false);
      if(diagnostic)await page.evaluate(()=>document.querySelector('ruffle-player').ruffle().traceObserver=text=>console.log(text));
      await page.evaluate(()=>BombItHost.setHostState(true,true));await page.waitForTimeout(700);
      const metadata=await page.evaluate(()=>document.querySelector('ruffle-player').ruffle().metadata);
      assert.equal(metadata.width,550);assert.equal(metadata.height,400);
    }
    async function start(mode){
      if(mode==='duo')await page.locator('[data-mode=duo]').tap();
      await flashTap(451,260);await flashTap(451,mode==='duo'?315:239);await flashTap(450,215);
      await screenshot(mode+'-character');
      if(mode==='duo')await page.locator('.p1-bomb').tap();
      await page.locator('.p2-bomb').tap();await page.waitForTimeout(700);await screenshot(mode+'-game');
      if(diagnostic){await page.waitForTimeout(300);assert(traces.some(t=>t.includes('frame=9')&&t.includes('players='+(mode==='duo'?2:1))),'original SWF enters '+mode+' battle from touch controls');}
    }
    await load();assert(messages.some(m=>m.type==='ready'));
    if(promptSmoke){await page.evaluate(()=>BombItHost.requestExit('prompt'));await page.waitForTimeout(100);assert(messages.some(m=>m.type==='exit-ready'&&m.requestId==='prompt'));}
    else{
      await screenshot('menu');await start('single');
      const stick=await point('.left .joystick',1),fire=await point('.p2-bomb',2);
      await touch('touchStart',[stick]);assert.deepEqual(await held(),[],'center dead zone does not move');
      const right={...stick,x:stick.x+42};await touch('touchMove',[right]);assert.deepEqual(await held(),[39]);
      await touch('touchStart',[right,fire]);assert.deepEqual(await held(),[32,39],'movement and bomb are independent fingers');
      if(diagnostic)await page.waitForTimeout(250);
      await touch('touchEnd',[fire]);assert.deepEqual(await held(),[39],'releasing bomb preserves movement');
      await touch('touchMove',[{...stick,x:stick.x+105}]);assert.deepEqual(await held(),[39],'pointer capture follows outside joystick');
      await touch('touchMove',[stick]);assert.deepEqual(await held(),[],'return to dead zone releases direction');
      await touch('touchMove',[{...stick,y:stick.y-42}]);assert.deepEqual(await held(),[38],'same pointer leaves dead zone again');
      await touch('touchCancel',[]);assert.deepEqual(await held(),[],'OS cancellation releases all keys');
      await touch('touchStart',[right,fire]);await page.evaluate(()=>BombItHost.setHostState(false,false));
      assert.deepEqual(await held(),[]);assert.deepEqual(await engine(),{suspended:true,volume:0});
      const stage=page.locator('#player-frame');await page.waitForTimeout(60);const frozen=await stage.screenshot();await page.waitForTimeout(350);
      assert.deepEqual(await stage.screenshot(),frozen,'background pause freezes actual SWF pixels');
      await touch('touchEnd',[]);await page.evaluate(()=>BombItHost.setHostState(true,true));assert.deepEqual(await held(),[]);
      await page.locator('#pause').tap();assert.equal((await engine()).suspended,true);
      await flashTap(275,200);assert.equal((await engine()).suspended,true,'Ruffle play overlay cannot bypass shell pause');
      await page.locator('#pause').tap();assert.equal((await engine()).suspended,false);
      await page.evaluate(()=>BombItHost.setHostState(true,false));assert.deepEqual(await engine(),{suspended:false,volume:0});
      await page.evaluate(()=>BombItHost.setHostState(true,true));
      await touch('touchStart',[right]);await page.evaluate(()=>window.dispatchEvent(new Event('blur')));assert.deepEqual(await held(),[]);await touch('touchEnd',[]);
      await chooseStyle('dpad');
      const leftKey=await point('button[data-player="1"][data-action="left"]',21),secondLeft={...leftKey,id:22,x:leftKey.x+5};
      const upKey=await point('button[data-player="1"][data-action="up"]',21),rightKey=await point('button[data-player="1"][data-action="right"]',21),gap=await point('.left .dpad',21);
      await touch('touchStart',[leftKey,secondLeft]);assert.deepEqual(await held(),[37]);
      await touch('touchEnd',[secondLeft]);assert.deepEqual(await held(),[37],'same direction remains pressed by its other pointer');
      await touch('touchMove',[gap]);assert.deepEqual(await held(),[],'direction-pad center gap releases key');
      await touch('touchMove',[upKey]);assert.deepEqual(await held(),[38],'captured pointer crosses the gap into another direction');
      await touch('touchMove',[{...gap,y:gap.y+110}]);assert.deepEqual(await held(),[],'sliding outside the direction pad releases key');
      await touch('touchMove',[rightKey]);assert.deepEqual(await held(),[39],'returning to the direction pad reacquires a direction');
      await touch('touchStart',[rightKey,await point('.p2-bomb',23)]);assert.deepEqual(await held(),[32,39],'direction buttons and bomb support independent fingers');
      await screenshot('single-dpad');await touch('touchCancel',[]);assert.deepEqual(await held(),[]);
      await page.evaluate(()=>BombItHost.requestExit('single-exit'));await page.waitForTimeout(100);
      assert(messages.some(m=>m.type==='exit-ready'&&m.requestId==='single-exit'));assert.equal(await page.locator('ruffle-player').count(),0);
      await load();assert.equal(await page.evaluate(()=>document.body.dataset.input),'dpad','control preference survives reloading the offline game');
      await chooseStyle('joystick');await start('duo');
      const p1=await point('.left .joystick',11,42),b1=await point('.p1-bomb',12),p2=await point('.right .joystick',13,-42),b2=await point('.p2-bomb',14);
      await touch('touchStart',[p1,b1,p2,b2]);assert.deepEqual(await held(),[13,32,37,68],'two joysticks and both bombs operate simultaneously');
      if(diagnostic)await page.waitForTimeout(300);
      await screenshot('duo-four-fingers');
      await touch('touchEnd',[p1,b1]);assert.deepEqual(await held(),[13,37],'player two stays held after player one releases');
      await touch('touchCancel',[]);assert.deepEqual(await held(),[]);
      await chooseStyle('dpad');
      const d1=await point('button[data-player="1"][data-action="right"]',31),db1=await point('.p1-bomb',32),d2=await point('button[data-player="2"][data-action="left"]',33),db2=await point('.p2-bomb',34);
      await touch('touchStart',[d1,db1,d2,db2]);assert.deepEqual(await held(),[13,32,37,68],'both direction pads and bombs retain the original player key mappings');
      await screenshot('duo-dpad');await touch('touchCancel',[]);assert.deepEqual(await held(),[]);await chooseStyle('joystick');
      await touch('touchStart',[p1]);await page.locator('[data-mode=single]').evaluate(e=>e.click());assert.deepEqual(await held(),[],'mode change releases old WASD keys');await touch('touchEnd',[]);
      await page.locator('[data-mode=duo]').tap();
      await page.evaluate(()=>BombItHost.setHostState(false,false));
      for(const style of ['joystick','dpad']){
        await chooseStyle(style);
        for(const [name,width,height,safe] of [['phone-safe-area',844,390,59],['small-landscape',667,375,0],['portrait',390,844,0],['tablet',1024,768,0]]){
          await page.setViewportSize({width,height});await page.evaluate(s=>BombItHost.setViewportInsets(s,0,s,21),safe);await page.waitForTimeout(80);
          const bounds=await page.evaluate(()=>{
            const box=e=>{const b=e.getBoundingClientRect();return{label:e.id||e.getAttribute('aria-label'),left:b.left,right:b.right,top:b.top,bottom:b.bottom,width:b.width,height:b.height};};
            return{stage:box(document.querySelector('#player-frame')),controls:Array.from(document.querySelectorAll('.joystick,.bomb,.direction,.icon-button')).filter(e=>e.offsetWidth).map(box)};
          });
          assert(Math.abs(bounds.stage.width/bounds.stage.height-550/400)<.01,'original aspect ratio is preserved');
          if(width>height)assert(Math.abs(bounds.stage.bottom-height)<.1,'landscape game bottom is flush with the screen');
          const separate=(a,b)=>a.right<=b.left+.1||a.left>=b.right-.1||a.top>=b.bottom-.1||a.bottom<=b.top+.1;
          for(let i=0;i<bounds.controls.length;i++){
            const b=bounds.controls[i];assert(b.left>=0&&b.right<=width+.1&&b.top>=0&&b.bottom<=height+.1,style+' '+name+' control stays on screen: '+b.label);
            assert(separate(b,bounds.stage),style+' '+name+' control never covers game: '+b.label);
            for(const other of bounds.controls.slice(i+1))assert(separate(b,other),style+' '+name+' controls do not overlap: '+b.label+' / '+other.label);
          }
          await screenshot(style+'-'+name);
        }
      }
      await page.evaluate(()=>BombItHost.setHostState(true,true));
      await page.locator('#hall').tap();await page.locator('#modal-confirm').tap();await page.waitForTimeout(100);assert(messages.some(m=>m.type==='exit-intent'));
      await page.evaluate(()=>{BombItHost.setHostState(false,false);BombItHost.requestExit('duo-exit');});await page.waitForTimeout(100);
      assert(messages.some(m=>m.type==='exit-ready'&&m.requestId==='duo-exit'));assert.equal(await page.locator('ruffle-player').count(),0);
    }
    assert.deepEqual(blocked,[],'only bundled local resources are requested');assert.deepEqual(errors,[],'no runtime script errors');
    const result={ok:true,diagnostic,promptSmoke,swfSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(content,'game.swf'))).digest('hex'),checks:promptSmoke?['old Android prompt bridge ready and exit']:['original menus and single/duo character confirmation','multi-pointer joysticks and bombs','dead zone and captured movement','settings switch and persisted input style','direction-pad sliding across gaps and outside/reentry','direction-pad pointer reference counts and both player mappings','cancellation, blur, mode change and host releases','cold background load','Ruffle suspend/resume and volume','pause overlay bypass prevented','phone, portrait and tablet layout','offline resource requests','synchronous Ruffle destruction and exit ack'],messages,requests:[...requests],blocked,errors,traces};
    fs.writeFileSync(path.join(output,(promptSmoke?'prompt':diagnostic?'diagnostic':'browser')+'-result.json'),JSON.stringify(result,null,2)+'\n');
    console.log(JSON.stringify({...result,traces:traces.length},null,2));
  }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
