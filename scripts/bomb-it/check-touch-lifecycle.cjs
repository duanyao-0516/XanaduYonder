// Real Chrome + packaged Ruffle/SWF regression; never operates a phone.
// Run --baseline against the unmodified content to record the two known defects.
// PLAYWRIGHT_MODULE points at an existing playwright-core installation.
// Current production checks: --short, --clock-only (181 real seconds),
// --near-expiry, --lifecycle-errors, --gesture-edges. No flag runs the combined normal suite.
// --near-expiry generates its own artifacts-only 4-second fixture; generation
// alone is available as --prepare-near-expiry. Production/AAR are never written.
// Optional --rapid-exit-retry reproduces the deliberately immediate retry race.
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const prepareOnly=process.argv.includes('--prepare-near-expiry');
const root=path.resolve(__dirname,'../..'),baseline=process.argv.includes('--baseline'),nearExpiry=prepareOnly||process.argv.includes('--near-expiry'),clockOnly=process.argv.includes('--clock-only'),shortOnly=process.argv.includes('--short'),lifecycleErrors=process.argv.includes('--lifecycle-errors'),rapidRetry=process.argv.includes('--rapid-exit-retry'),gestureEdges=process.argv.includes('--gesture-edges');
const content=process.env.BOMB_IT_CONTENT||path.join(root,'artifacts/bomb-it',baseline?'touch-baseline/content':'content');
const output=process.env.BOMB_IT_CHECK_OUTPUT||path.join(root,'artifacts/bomb-it',baseline?'touch-baseline':gestureEdges?'touch-gesture-edges':nearExpiry?'touch-near-expiry':clockOnly?'touch-clock':shortOnly?'touch-short':lifecycleErrors?(rapidRetry?'touch-lifecycle-rapid':'touch-lifecycle-errors'):'touch-lifecycle');
// The explicitly requested diagnostic changes only the initial timer constant.
// It never replaces content/game.swf or the production build inputs.
const swf=nearExpiry?path.join(root,'artifacts/bomb-it/near-expiry/game.swf'):path.join(content,'game.swf');
if(nearExpiry){
  // Reuse the audited SWF parser read-only. The single changed integer belongs
  // to the original timer symbol, not to any production pause/bridge actions.
  require('child_process').execFileSync('python3',['-c',String.raw`
import hashlib,importlib.util,json,struct,sys,zlib
from pathlib import Path
sys.dont_write_bytecode=True
root,content,target=map(Path,sys.argv[1:])
spec=importlib.util.spec_from_file_location('bombit_sanitizer',root/'ThirdParty/BombItContent/sanitizer/sanitize.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
data=(content/'game.swf').read_bytes()
expected,_=m.sanitize((root/'ThirdParty/BombItContent/original/game.swf').read_bytes())
assert data==expected,'Generate current production content before preparing a fixture'
source,start=m.unpack(data);parts=[source[8:start]];changed=0
for _,code,_,payload,raw in m.tags(source,start):
    if code==39 and int.from_bytes(payload[:2],'little')==519:
        nested=[payload[:4]]
        for _,kind,_,actions,tag in m.tags(payload,4):
            if kind==12:
                needle=b'\x07\xb4\x00\x00\x00'
                assert actions.count(needle)==1,'Pinned original 180-second timer changed'
                tag=m.tag_bytes(kind,actions.replace(needle,b'\x07\x04\x00\x00\x00'));changed+=1
            nested.append(tag)
        raw=m.tag_bytes(code,b''.join(nested))
    parts.append(raw)
assert changed==1,'Expected exactly one original timer change'
body=b''.join(parts);fixture=b'CWS\x09'+struct.pack('<I',len(body)+8)+zlib.compress(body,9)
decoded,_=m.unpack(fixture)
assert len(decoded)==len(source) and sum(a!=b for a,b in zip(source,decoded))==1,'Fixture altered bytes beyond the timer integer'
assert target==root/'artifacts/bomb-it/near-expiry/game.swf'
target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(fixture)
report={'diagnosticOnly':True,'productionSha256':hashlib.sha256(data).hexdigest(),'fixtureSha256':hashlib.sha256(fixture).hexdigest(),'onlyChange':'DefineSprite519 original timeInit integer 180 replaced with 4; production bridge bytes unchanged'}
target.with_name('fixture.json').write_text(json.dumps(report,indent=2)+'\n')
`,root,content,swf],{stdio:'inherit'});
}
if(prepareOnly){console.log(fs.readFileSync(path.join(path.dirname(swf),'fixture.json'),'utf8'));process.exit(0);}
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright-core');
const origin='https://bomb-it.xanaduyonder.invalid/';
fs.mkdirSync(output,{recursive:true});
const result={baseline,nearExpiry,clockOnly,shortOnly,lifecycleErrors,rapidRetry,gestureEdges,content,swf,swfSha256:crypto.createHash('sha256').update(fs.readFileSync(swf)).digest('hex'),resourceSha256:{},checks:[],failures:[],requests:[],errors:[],pageErrorDetails:[],nativeMessages:[]};
let browser,page;
(async()=>{
  browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});
  const context=await browser.newContext({viewport:{width:844,height:390},hasTouch:true,serviceWorkers:'block'});
  await context.exposeBinding('recordNative',(_,value)=>result.nativeMessages.push(JSON.parse(value)));
  await context.addInitScript(()=>{
    window.BombItNative={postMessage:value=>window.recordNative(value)};
    window.__fsCommandHandlers=[];
    window.__pointerLog=[];
    for(const type of ['pointerdown','pointerup','mousedown','mouseup','click'])window.addEventListener(type,e=>{window.__pointerLog.push({type,x:e.clientX,y:e.clientY,target:e.target.id||e.target.tagName,manual:window.BombItHost?.status().game?.manualPaused,time:performance.now()});if(window.__pointerLog.length>80)window.__pointerLog.shift();},true);
    window.__heldKeys=new Set();window.__keyEvents=[];
    for(const kind of ['keydown','keyup'])window.addEventListener(kind,event=>{
      window.__keyEvents.push({kind,code:event.keyCode,time:performance.now()});
      if(kind==='keydown')window.__heldKeys.add(event.keyCode);else window.__heldKeys.delete(event.keyCode);
    });
  });
  await context.route('**/*',async route=>{
    const url=new URL(route.request().url()),name=decodeURIComponent(url.pathname.slice(1));
    if(url.origin!==new URL(origin).origin||route.request().method()!=='GET'||!name||name.includes('..')||name.includes('\\'))throw new Error('Unexpected network request: '+url.href);
    const file=name==='game.swf'?swf:path.join(content,name);assert(fs.existsSync(file),'unbundled resource '+name);
    if(!result.requests.includes(name))result.requests.push(name);
    let body=fs.readFileSync(file);
    result.resourceSha256[name]=crypto.createHash('sha256').update(body).digest('hex');
    if(!baseline&&name==='host.js'){
      const source=body.toString(),anchor='const r=created.ruffle();';assert.equal(source.split(anchor).length,2,'FSCommand observation anchor is unique');
      body=Buffer.from(source.replace(anchor,anchor+'const captureOriginalAdd=r.addFSCommandHandler.bind(r);r.addFSCommandHandler=handler=>{window.__fsCommandHandlers.push(handler);return captureOriginalAdd(handler);};'));
      result.observationHook='Browser response only: retain original FSCommand callback references while registering each unchanged.';
    }
    await route.fulfill({body,contentType:{'.html':'text/html','.js':'application/javascript','.css':'text/css','.wasm':'application/wasm','.swf':'application/x-shockwave-flash','.json':'application/json'}[path.extname(file)]||'text/plain'});
  });
  page=await context.newPage();page.setDefaultTimeout(45000);page.on('pageerror',e=>{result.errors.push(String(e));result.pageErrorDetails.push({message:String(e),stack:e.stack,stage:result.stage,time:Date.now()});});
  const cdp=await context.newCDPSession(page);
  const touch=async(type,points)=>{await cdp.send('Input.dispatchTouchEvent',{type,touchPoints:points});await page.waitForTimeout(50);};
  const held=()=>page.evaluate(()=>Array.from(__heldKeys).sort((a,b)=>a-b));
  const rect=selector=>page.locator(selector).boundingBox();
  const screenshot=name=>page.screenshot({path:path.join(output,name+'.png')});
  async function style(value){await page.locator('#settings').tap();await page.locator('[data-input-style="'+value+'"]').tap();await page.locator('#modal-confirm').tap();}
  async function flashTap(x,y){const b=await rect('#bombit-player');await page.touchscreen.tap(b.x+x*b.width/550,b.y+y*b.height/400);await page.waitForTimeout(300);}
  await page.goto(origin+'index.html');await page.waitForFunction(()=>window.BombItHost?.status().ready);console.log('READY '+JSON.stringify(await page.evaluate(()=>BombItHost.status())));await page.waitForTimeout(600);
  await page.evaluate(()=>BombItHost.setViewportInsets(59,0,59,21));
  if(baseline){
    const bridge=await page.evaluate(()=>{try{return{state:document.querySelector('ruffle-player').ruffle().callExternalInterface('xanaduState')??null,status:BombItHost.status()};}catch(error){return{state:null,error:String(error),status:BombItHost.status()};}});
    assert.equal(bridge.state,null,'baseline SWF lacks the authoritative state bridge');
    result.failures.push({requirement:'read authoritative original-game phase, mode and pause state',bridge});
    const layouts=[];
    for(const mode of ['single','duo']){
      await page.locator('[data-mode="'+mode+'"]').tap();
      for(const value of ['joystick','dpad']){await style(value);layouts.push({mode,style:value,stage:await rect('#player-frame')});await screenshot(mode+'-'+value+'-safe59');}
    }
    result.layouts=layouts;
    const joystick=layouts.find(x=>x.mode==='single'&&x.style==='joystick').stage,dpad=layouts.find(x=>x.mode==='single'&&x.style==='dpad').stage;
    assert(dpad.width<joystick.width,'baseline must reproduce a smaller dpad viewport');
    result.failures.push({requirement:'same viewport for both input styles',joystick,dpad,areaLoss:1-dpad.width*dpad.height/(joystick.width*joystick.height)});
    await page.locator('[data-mode="single"]').tap();await style('joystick');
    await flashTap(451,260);await flashTap(451,239);await flashTap(450,215);await page.locator('.p2-bomb').tap();await page.waitForTimeout(500);
    const b=await rect('.left .joystick'),point={x:b.x+b.width*.78,y:b.y+b.height/2,id:1};
    await touch('touchStart',[point]);const pressed=await held();
    assert.deepEqual(pressed,[39],'baseline eccentric DOWN alone emits ArrowRight');
    result.failures.push({requirement:'initial pointerdown stays neutral until movement',point,heldKeysAfterDown:pressed,events:await page.evaluate(()=>__keyEvents)});
    await screenshot('eccentric-down-moves');await touch('touchCancel',[]);assert.deepEqual(await held(),[]);
    result.checks.push('Known regressions reproduced on real Ruffle content; no adapter changes.');
  }else{
    const state=()=>page.evaluate(()=>document.querySelector('ruffle-player').ruffle().callExternalInterface('xanaduState'));
    const waitPhase=phase=>page.waitForFunction(expected=>{
      try{return document.querySelector('ruffle-player').ruffle().callExternalInterface('xanaduState')?.phase===expected;}catch(_){return false;}
    },phase);
    const note=(name,details={})=>{result.checks.push({name,...details});fs.writeFileSync(path.join(output,'progress.json'),JSON.stringify(result,null,2)+'\n');console.log('PASS '+name);};
    const center=async(selector,id,dx=0,dy=0)=>{const b=await rect(selector);return{x:b.x+b.width/2+dx,y:b.y+b.height/2+dy,id};};
    const clickPause=async()=>{await page.locator('#pause').tap();await page.waitForTimeout(100);};
    const copyRect=b=>Object.fromEntries(['x','y','width','height'].map(k=>[k,Math.round(b[k]*100)/100]));
    const sameRect=(a,b)=>{for(const k of ['x','y','width','height'])assert(Math.abs(a[k]-b[k])<.15,'viewport '+k+' differs');};
    async function switchMode(mode,confirm=true){
      const before=await state();await page.evaluate(()=>window.__previousPlayer=document.querySelector('ruffle-player'));
      await page.locator('[data-mode="'+mode+'"]').tap();assert.equal(await page.locator('#modal').isVisible(),true,'mode changes require confirmation');
      await page.locator(confirm?'#modal-confirm':'#modal-cancel').tap();
      if(confirm){await page.waitForFunction(()=>document.querySelector('ruffle-player')!==window.__previousPlayer&&BombItHost.status().ready);await waitPhase('difficulty');assert.equal((await state()).mode,mode==='duo'?2:1);}
      else{assert.equal(await page.evaluate(()=>document.querySelector('ruffle-player')===window.__previousPlayer),true);assert.equal((await state()).mode,before.mode);assert.equal((await state()).phase,before.phase);}
    }
    async function enterBattle(mode){
      let s=await state();
      if(s.phase==='menu'){
        await flashTap(451,260);s=await state();
        if(s.phase==='menu')await flashTap(451,mode===2?315:239);
      }
      await waitPhase('difficulty');await flashTap(450,215);await waitPhase('select');
      assert.equal((await state()).mode,mode);await screenshot('select-'+mode);
      if(mode===2)await page.locator('.p1-bomb').tap();
      await page.locator('.p2-bomb').tap();await waitPhase('game');await page.waitForTimeout(100);
      assert.equal((await state()).mode,mode);assert.equal(await page.evaluate(()=>BombItHost.status().game.phase),'game','host receives the original battle phase, not stale difficulty');note('original difficulty and character menus enter '+mode+'-player battle',await state());
    }
    await page.waitForFunction(()=>{try{return document.querySelector('ruffle-player').ruffle().callExternalInterface('xanaduState')?.version===1;}catch(_){return false;}});
    const initial=await state();assert([0,1,2].includes(initial.mode));result.initialState=initial;
    if(gestureEdges){
      // Android system-gesture bands are not untouchable display cutouts. A
      // delivered tap on a visible control must work; only an unclaimed blank
      // edge must decline to start a floating joystick.
      await page.evaluate(()=>BombItHost.setViewportInsets(0,0,0,0));
      await enterBattle(1);await style('dpad');
      const outer=async(selector,id,side)=>{const b=await rect(selector);return{x:side==='left'?b.x+6:b.x+b.width-6,y:b.y+b.height/2,id};};
      const gesture=async value=>page.evaluate(v=>BombItHost.setGestureInsets(v,0,v,24),value);
      const cancel=async()=>{await touch('touchCancel',[]);assert.deepEqual(await held(),[],'cancel releases every held key');};
      const singleLeft=await outer('.left .dpad',41,'left');
      await gesture(0);await touch('touchStart',[singleLeft]);assert.deepEqual(await held(),[37]);await cancel();
      await gesture(48);await touch('touchStart',[singleLeft]);
      assert.deepEqual(await held(),[37],'visible single-player direction outer edge survives the system gesture inset');await cancel();
      const singleFire=await outer('.p2-bomb',42,'right');
      await touch('touchStart',[singleFire]);assert.deepEqual(await held(),[32],'visible single-player bomb outer edge works');await cancel();
      await screenshot('single-explicit-edges');note('single-player radial and bomb outer edges remain usable with gesture48',{singleLeft,singleFire});

      await style('joystick');
      const singleStick=await outer('.left .joystick',43,'left');
      await touch('touchStart',[singleStick]);assert.deepEqual(await held(),[],'visible joystick outer-edge DOWN is neutral');
      await touch('touchMove',[{...singleStick,x:singleStick.x+32}]);assert.deepEqual(await held(),[39],'visible joystick outer edge owns its drag');await cancel();
      const stickFire=await outer('.p2-bomb',44,'right');await touch('touchStart',[stickFire]);assert.deepEqual(await held(),[32]);await cancel();
      const blank={x:8,y:120,id:45};
      await page.evaluate(()=>{window.__unclaimedDowns=0;window.addEventListener('pointerdown',()=>++window.__unclaimedDowns);});
      await touch('touchStart',[blank]);assert.equal(await page.evaluate(()=>__unclaimedDowns),1,'blank system edge is not consumed by the adapter');
      await touch('touchMove',[{...blank,x:120}]);assert.deepEqual(await held(),[],'unclaimed edge cannot acquire a stick later in the same touch');
      assert.equal(await page.locator('.joystick.held').count(),0);await cancel();
      await gesture(0);await touch('touchStart',[blank]);await touch('touchMove',[{...blank,x:40}]);assert.deepEqual(await held(),[39],'same blank point works when it is not a system edge');await cancel();await gesture(48);
      note('explicit idle joystick works at the edge; blank system-edge origins remain unclaimed');

      await switchMode('duo');await enterBattle(2);await style('dpad');
      const dl=await outer('.left .dpad',51,'left'),dr=await outer('.right .dpad',52,'right');
      const df1=await outer('.p1-bomb',53,'left'),df2=await outer('.p2-bomb',54,'right');
      await touch('touchStart',[dl,dr,df1,df2]);assert.deepEqual(await held(),[13,32,39,65],'all four explicit duo outer edges respond together');
      const dc=await center('.left .dpad',51);await touch('touchMove',[{...dc,x:dc.x+48},dr,df1,df2]);assert.deepEqual(await held(),[13,32,39,68],'sector sliding keeps the original player ownership');
      await screenshot('duo-explicit-edges');await cancel();note('duo radial and bomb outer edges support four fingers and cancellation',{dl,dr,df1,df2});

      await style('joystick');
      const jl=await outer('.left .joystick',61,'left'),jr=await outer('.right .joystick',62,'right');
      const jf1=await outer('.p1-bomb',63,'left'),jf2=await outer('.p2-bomb',64,'right');
      await touch('touchStart',[jl,jr,jf1,jf2]);assert.deepEqual(await held(),[13,32],'two edge joystick origins stay neutral while bombs fire');
      const lm={...jl,x:jl.x+32},rm={...jr,x:jr.x-32};await touch('touchMove',[lm,rm,jf1,jf2]);assert.deepEqual(await held(),[13,32,37,68]);
      await touch('touchMove',[{...lm,x:550},rm,jf1,jf2]);assert.deepEqual(await held(),[13,32,37,68],'crossing the screen retains the DOWN player');
      await touch('touchEnd',[{...lm,x:550}]);assert.deepEqual(await held(),[13,32,37],'one finger releases without releasing the other player');await cancel();
      await touch('touchStart',[jl]);await touch('touchMove',[{...jl,x:jl.x+32}]);assert.deepEqual(await held(),[68],'cancelled movement owner can be reacquired');await cancel();
      await gesture(0);assert.deepEqual(result.errors,[]);note('duo edge joysticks preserve ownership, individual UP, cancel and re-entry');result.ok=true;return;
    }
    if(lifecycleErrors){
      await enterBattle(1);const before=await state();
      result.stage='exit removal fault and cancellation';
      await page.evaluate(()=>{window.__faultPlayer=document.querySelector('ruffle-player');window.__faultRemove=__faultPlayer.remove;__faultPlayer.remove=()=>{throw new Error('injected exit remove failure');};BombItHost.requestExit('remove-fails');});
      assert(result.nativeMessages.some(m=>m.type==='exit-error'&&m.requestId==='remove-fails'));assert.equal(await page.evaluate(()=>BombItHost.status().exited),false);
      await page.evaluate(()=>{__faultPlayer.remove=__faultRemove;BombItHost.cancelExit();});assert.equal((await state()).session,before.session);assert.equal(await page.evaluate(()=>BombItHost.status().error),null);assert.equal(await page.evaluate(()=>BombItHost.status().active),true);
      // Observe an actual resumed frame before the separate user retry, rather
      // than destroy an AudioContext inside its still-pending resume microtask.
      if(!rapidRetry)await page.waitForTimeout(150);assert.equal(await page.evaluate(()=>BombItHost.status().active),true);
      result.stage='exit retry after '+(rapidRetry?'zero delay':'150ms resumed observation');
      await page.evaluate(()=>BombItHost.requestExit('remove-retry'));assert(result.nativeMessages.some(m=>m.type==='exit-ready'&&m.requestId==='remove-retry'));assert.equal(await page.locator('ruffle-player').count(),0);note('failed exit removal preserves session; cancel resumes and retry succeeds');
      result.stage='reload after successful exit';await page.reload();await page.waitForFunction(()=>BombItHost.status().ready);note('new player reaches ready after successful exit retry');
      result.stage='mode removal fault';
      await page.evaluate(()=>{window.__faultPlayer=document.querySelector('ruffle-player');window.__faultRemove=__faultPlayer.remove;__faultPlayer.remove=()=>{throw new Error('injected mode remove failure');};});
      await page.locator('[data-mode="duo"]').tap();await page.locator('#modal-confirm').tap();await page.waitForTimeout(100);
      assert.match(await page.evaluate(()=>BombItHost.status().error),/injected mode remove failure/);assert.deepEqual(result.errors,[],'mode removal failure is handled, not an unhandled promise');await page.evaluate(()=>__faultPlayer.remove=__faultRemove);note('mode restart removal failure enters explicit error state');result.ok=true;return;
    }
    if(nearExpiry){
      await enterBattle(1);
      await page.waitForFunction(()=>{const s=document.querySelector('ruffle-player').ruffle().callExternalInterface('xanaduState');return s.phase==='game'&&s.remainingTime<=2.1;});
      const before=await state();await clickPause();const frozen=await state();await page.waitForTimeout(1700);assert.equal((await state()).remainingTime,frozen.remainingTime);
      await clickPause();const resumed=await state();assert.equal(resumed.phase,'game');assert(Math.abs(resumed.remainingTime-frozen.remainingTime)<.3,'near-expiry resume preserves its active fraction');
      await waitPhase('gameover');const ended=await state();assert.equal(ended.remainingTime,0);note('near-expiry pause resumes remaining fraction then reaches real timeout',{fixture:'only original timeInit 180 -> 4; production timer and bridge byte-identical',before,frozen,resumed,ended});
      assert.deepEqual(result.errors,[]);result.ok=true;return;
    }
    if(!clockOnly){
    // Inspect production geometry in all combinations, before battle captures
    // can affect menu taps. Changes use the actual confirmation and original flow.
    const layouts=[];
    for(const mode of ['single','duo']){
      if((await state()).mode!==(mode==='duo'?2:1))await switchMode(mode);
      for(const safe of [0,59]){
        await page.evaluate(s=>BombItHost.setViewportInsets(s,0,s,21),safe);
        for(const value of ['joystick','dpad']){
          await style(value);const b=copyRect(await rect('#player-frame'));layouts.push({mode,style:value,safe,stage:b});
          const prior=layouts.find(x=>x.safe===safe);sameRect(prior.stage,b);
          await screenshot(mode+'-'+value+'-safe'+safe);
        }
      }
    }
    result.layouts=layouts;note('same viewport in both styles and both modes, with and without safe59');
    await page.evaluate(()=>BombItHost.setViewportInsets(59,0,59,21));await style('joystick');
    if((await state()).mode!==1)await switchMode('single');await enterBattle(1);
    await switchMode('duo',false);note('mode cancellation preserves original player and battle');
    // Actual initial contact is the origin, including eccentric edge presses.
    let stage=await rect('#player-frame'),start={x:stage.x+stage.width*.33,y:stage.y+stage.height*.57,id:1};
    await touch('touchStart',[start]);assert.deepEqual(await held(),[],'first touch on battle area is neutral');
    let right={...start,x:start.x+36};await touch('touchMove',[right]);assert.deepEqual(await held(),[39]);
    await touch('touchMove',[{...start,y:start.y-36}]);assert.deepEqual(await held(),[38]);
    await touch('touchMove',[start]);assert.deepEqual(await held(),[],'return to actual origin releases');
    await touch('touchMove',[right]);await touch('touchCancel',[]);assert.deepEqual(await held(),[]);
    const edgeBounds=await rect('.left .joystick'),edge={x:edgeBounds.x+edgeBounds.width*.78,y:edgeBounds.y+edgeBounds.height/2,id:2};
    await touch('touchStart',[edge]);assert.deepEqual(await held(),[],'eccentric sidebar contact is neutral');
    await touch('touchMove',[{...edge,x:edge.x+32}]);assert.deepEqual(await held(),[39]);await touch('touchCancel',[]);
    note('floating origin, first-down neutral, turn, return to center and cancel');
    await switchMode('duo');await enterBattle(2);stage=await rect('#player-frame');
    const left={x:stage.x+stage.width*.32,y:stage.y+stage.height*.55,id:11},rightOrigin={x:stage.x+stage.width*.77,y:stage.y+stage.height*.55,id:13};
    const b1=await center('.p1-bomb',12),b2=await center('.p2-bomb',14);
    await touch('touchStart',[left,rightOrigin,b1,b2]);assert.deepEqual(await held(),[13,32]);
    const lMove={...left,x:left.x+30},rMove={...rightOrigin,x:rightOrigin.x-30};
    await touch('touchMove',[lMove,rMove,b1,b2]);assert.deepEqual(await held(),[13,32,37,68],'four independent fingers');await screenshot('duo-four-fingers');
    await touch('touchMove',[{...lMove,x:stage.x+stage.width*.8},rMove,b1,b2]);assert.deepEqual(await held(),[13,32,37,68],'crossing midpoint preserves first-down player ownership');
    await touch('touchCancel',[]);assert.deepEqual(await held(),[]);note('four-finger movement/bombs and cross-half ownership');
    const playerBeforeStyle=await page.evaluate(()=>{window.__stylePlayer=document.querySelector('ruffle-player');return true;});assert(playerBeforeStyle);
    await style('dpad');assert.equal(await page.evaluate(()=>window.__stylePlayer===document.querySelector('ruffle-player')),true,'style never restarts');
    const d1=await center('.left .dpad',21,48),d2=await center('.right .dpad',23,-48),db1=await center('.p1-bomb',22),db2=await center('.p2-bomb',24);
    await touch('touchStart',[d1,d2,db1,db2]);assert.deepEqual(await held(),[13,32,37,68]);
    const neutral=await center('.left .dpad',21);await touch('touchMove',[neutral,d2,db1,db2]);assert.deepEqual(await held(),[13,32,37],'radial center is neutral');
    await touch('touchMove',[{...neutral,y:neutral.y-48},d2,db1,db2]);assert.deepEqual(await held(),[13,32,37,87],'radial sector changes without lifting');
    await screenshot('duo-dpad-four-fingers');await touch('touchCancel',[]);assert.deepEqual(await held(),[]);note('radial four-sector controls, center and four fingers; style does not restart');
    await style('joystick');await switchMode('single');await enterBattle(1);
    // A real original HUD tap must reach AVM1, not merely toggle shell state.
    await flashTap(68,370);assert.equal((await state()).manualPaused,true,'original HUD pause reaches game bridge');
    const originalPaused=await state();await page.locator('#settings').tap();await page.locator('#modal-confirm').tap();assert.equal((await state()).manualPaused,true,'settings do not clear original pause');
    await flashTap(68,370);result.originalPauseSecondTap=await state();
    result.hudEvents=await page.evaluate(()=>__pointerLog.slice(-30));
    console.log('HUD_SECOND_TAP '+JSON.stringify(result.originalPauseSecondTap));
    if((await state()).paused)await clickPause();assert.equal((await state()).paused,false,'shell resumes an original HUD pause');
    note('original HUD click reaches AVM1 and settings preserve manual pause',{before:originalPaused,secondHudTap:result.originalPauseSecondTap});
    if(shortOnly){assert.deepEqual(result.errors,[]);result.ok=true;return;}
    }else await enterBattle(1);
    // Hold the real production clock for over a complete 180-second round.
    await clickPause();assert.equal((await state()).paused,true);const beforeLong=await state(),wallStart=Date.now();
    result.longPause={before:beforeLong,wallStart};console.log('LONG_PAUSE_STARTED '+JSON.stringify(beforeLong));
    while(Date.now()-wallStart<181000){await page.waitForTimeout(Math.min(10000,181000-(Date.now()-wallStart)));const s=await state();assert.equal(s.remainingTime,beforeLong.remainingTime,'remaining time stays frozen during long real pause');}
    result.longPause.elapsedMs=Date.now()-wallStart;await clickPause();await page.waitForTimeout(100);const afterLong=await state();result.longPause.after=afterLong;
    assert.equal(afterLong.phase,'game');assert(afterLong.remainingTime>=beforeLong.remainingTime-1,'resume preserves remaining time after >180 wall seconds');note('real 181-second pause preserves original remaining time',result.longPause);
    // Frequent temporary pauses must accumulate fractional active time correctly.
    const shortBefore=await state(),shortStart=Date.now();let pausedMs=0;
    for(let i=0;i<12;i++){
      await page.waitForTimeout(90);await clickPause();const t=Date.now(),frozen=await state();await page.waitForTimeout(120);assert.equal((await state()).remainingTime,frozen.remainingTime);await clickPause();pausedMs+=Date.now()-t;
    }
    const shortAfter=await state(),activeSeconds=(Date.now()-shortStart-pausedMs)/1000,loss=shortBefore.remainingTime-shortAfter.remainingTime;
    assert(loss>=Math.floor(activeSeconds)-1&&loss<=Math.ceil(activeSeconds)+1,'repeated short pauses neither consume pause time nor reset fractional active time');note('repeated fractional pauses retain active-time progress',{shortBefore,shortAfter,activeSeconds,loss});
    await page.evaluate(()=>BombItHost.setHostState(false,false));const bg=await state();assert.equal(bg.paused,true);await page.waitForTimeout(500);await page.evaluate(()=>BombItHost.setHostState(true,true));assert.equal((await state()).paused,true,'background return requires explicit resume');
    await page.locator('#settings').tap();await page.locator('#modal-confirm').tap();assert.equal((await state()).paused,true);await clickPause();assert.equal((await state()).paused,false);note('background plus settings remains paused until explicit resume');
    const oldState=await state(),newMode=oldState.mode===1?2:1,newModeName=newMode===1?'single':'duo';await switchMode(newModeName);const fresh=await state();assert.notEqual(fresh.session,oldState.session,'player generation changes');
    await page.evaluate(()=>{const count=window.__fsCommandHandlers.length;if(count<2)throw new Error('Old FSCommand handler was not captured');window.__fsCommandHandlers[count-2]('xanadu-state','');});assert.equal((await state()).mode,newMode);assert.equal(await page.evaluate(()=>document.body.dataset.controls),newModeName,'stale old player callback cannot change controls');await enterBattle(newMode);note('confirmed mode changes real mode via original menus; stale callback cannot change new player');
    await page.evaluate(()=>BombItHost.requestExit('touch-lifecycle'));assert.equal(await page.locator('ruffle-player').count(),0);assert(result.nativeMessages.some(x=>x.type==='exit-ready'&&x.requestId==='touch-lifecycle'));note('exit destroys player and acknowledges');
  }
  assert.deepEqual(result.errors,[]);result.ok=true;
})().catch(async error=>{result.ok=false;result.error=String(error.stack||error);process.exitCode=1;
  if(page){try{result.failureState=await page.evaluate(()=>({host:window.BombItHost?.status(),swf:document.querySelector('ruffle-player')?.ruffle().callExternalInterface('xanaduState'),events:window.__pointerLog}));await page.screenshot({path:path.join(output,'failure.png')});}catch(_){} }
}).finally(async()=>{
  if(browser)await browser.close();fs.writeFileSync(path.join(output,'result.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result,null,2));
});
