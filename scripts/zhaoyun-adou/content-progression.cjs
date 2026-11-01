'use strict';
// Original local game + real Chromium touch. Only explicitly recorded progression fixtures.
const assert=require('node:assert/strict'), fs=require('node:fs'), path=require('node:path');
const {chromium}=require('playwright-core');
const root=path.resolve(__dirname,'../..'), sourceArg=process.argv.indexOf('--source'), baseline=process.argv.includes('--baseline'), giftBaseline=process.argv.includes('--gift-baseline');
if(sourceArg>=0&&!process.argv[sourceArg+1])throw new Error('--source requires a content directory');
const content=sourceArg>=0?path.resolve(process.argv[sourceArg+1]):path.join(root,'artifacts/zhaoyun-adou/content');
const evidence=path.join(root,'artifacts/zhaoyun-adou/browser-evidence'), origin='https://zhaoyun-adou.xanadu.invalid';
const browserPath=process.env.CHROMIUM_PATH||['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome','/usr/bin/chromium','/usr/bin/chromium-browser'].find(fs.existsSync);
const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.json':'application/json','.ls':'application/json','.lh':'application/json','.png':'image/png','.mp3':'audio/mpeg','.wav':'audio/wav','.atlas':'text/plain','.txt':'text/plain'};
fs.mkdirSync(evidence,{recursive:true});
(async()=>{
  const browser=await chromium.launch({executablePath:browserPath,headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  const context=await browser.newContext({viewport:{width:360,height:640},deviceScaleFactor:1,hasTouch:true,isMobile:true});
  const page=await context.newPage(), cdp=await context.newCDPSession(page);
  const result={passed:false,baseline,content,phases:[],fixtures:[],touches:[],remote:[],missing:[],errors:[],screenshots:[]};
  page.on('pageerror',error=>result.errors.push(error.message));
  await page.route('**/*',async route=>{
    const u=new URL(route.request().url());if(u.origin!==origin){result.remote.push(u.href);return route.abort();}
    const relative=decodeURIComponent(u.pathname).replace(/^\/+/, '')||'index.html', file=path.resolve(content,relative);
    if(!file.startsWith(content+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()){result.missing.push(relative);return route.fulfill({status:404,body:'Not found'});}
    return route.fulfill({status:200,contentType:mime[path.extname(file)]||'application/octet-stream',body:fs.readFileSync(file)});
  });
  const phase=name=>{result.phases.push(name);console.log('PASS '+name);};
  const status=()=>page.evaluate(()=>window.XanaduZhaoyun.status());
  const waitScene=name=>page.waitForFunction(n=>window.XanaduZhaoyun.status().scene===n,name,{timeout:30000});
  const waitDialog=async(name,open=true)=>{await page.waitForFunction(({name,open})=>{
    const d=window.__XanaduZhaoyunInternals.SceneMgr.instance().ho.get(name);return Boolean(d&&!d.destroyed&&d.parent&&d.visible)!==!open;
  },{name,open},{timeout:20000});if(open)await page.waitForTimeout(450);};
  async function ready(){await page.waitForFunction(()=>window.XanaduZhaoyun?.status().ready,null,{timeout:45000});
    assert((await page.evaluate(()=>window.XanaduZhaoyun.configure({soundEnabled:false}))).ok);
    assert((await page.evaluate(()=>window.XanaduZhaoyun.setActive(true))).ok);}
  async function shot(name){const file=path.join(evidence,name+'.png');await page.screenshot({path:file});result.screenshots.push(file);}
  const snapshot=()=>page.evaluate(()=>{
    const i=window.__XanaduZhaoyunInternals,d=i.DataMgr.instance(),p=d.player;
    return{status:window.XanaduZhaoyun.status(),registerTime:p.registerTime,loginDays:p.consecutiveLoginDays,day:i.TimeMgr.instance().vr(),round:p.round,
      weaponOpen:i.WeaponMgr.instance().Dm(),weaponFree:p.weaponFree,inventory:[...d.wy.my].map(([id])=>({id,count:p.getWeaponFragmentCount(id)})).filter(x=>x.count>0),
      equip:p.equip.slice(),gameAvatar:p.gameAvatar,avatars:[3,5,6,11].map(id=>({id,unlocked:p.isAvatarUnlocked(id)})),rank:d.rank.iL.id,openProps:p.openProps,mapChoice:d.i5()};
  });
  function validPoint(p){assert(p.x>0&&p.x<360&&p.y>0&&p.y<640,'touch outside viewport: '+JSON.stringify(p));return p;}
  async function point(name,field,index,scope='scene',entry){return validPoint(await page.evaluate(({name,field,index,scope,entry})=>{
    const m=window.__XanaduZhaoyunInternals.SceneMgr.instance(),s=scope==='dialog'?m.ho.get(name):m.co(name);
    let n=s[field];if(index!==undefined)n=n[index];if(entry)n=n[entry];
    const p=n.localToGlobal(new Laya.Point(n.width/2,n.height/2)),r=document.querySelector('canvas').getBoundingClientRect();
    return{x:r.left+p.x*r.width/Laya.stage.width,y:r.top+p.y*r.height/Laya.stage.height};
  },{name,field,index,scope,entry}));}
  async function tap(name,field,index,scope='scene',entry){const p=await point(name,field,index,scope,entry);result.touches.push({kind:'tap',name,field,index,point:p});await page.touchscreen.tap(p.x,p.y);}
  async function drag(from,to,label){validPoint(from);validPoint(to);result.touches.push({kind:'drag',label,from,to});
    await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:from.x,y:from.y,id:1}]});await page.waitForTimeout(80);
    for(let step=1;step<=12;step++){await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:from.x+(to.x-from.x)*step/12,y:from.y+(to.y-from.y)*step/12,id:1}]});await page.waitForTimeout(35);}
    await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  }
  async function collectNewWeapon(){await waitDialog('NewWeaponDialog');await page.waitForTimeout(450);await tap('NewWeaponDialog','getBtn',undefined,'dialog');await waitDialog('NewWeaponDialog',false);await page.waitForTimeout(400);}
  async function equipWeapon(weaponId,generalIndex){
    await page.waitForFunction(id=>{const s=window.__XanaduZhaoyunInternals.SceneMgr.instance().co('WeaponScene');return s.iyt.some(x=>x.type==='group'&&x.weaponId===id)&&!s.yyt;},weaponId,{timeout:20000});
    const item=await page.evaluate(({weaponId,generalIndex})=>{const i=window.__XanaduZhaoyunInternals,s=i.SceneMgr.instance().co('WeaponScene'),index=s.iyt.findIndex(x=>x.type==='group'&&x.weaponId===weaponId),w=s.iyt[index].Qs[0];return{index,instanceId:w.id,allowed:i.WeaponMgr.instance().xm(w.id,generalIndex,0)};},{weaponId,generalIndex});
    assert(item.allowed,'original weapon rules must permit the chosen general');
    await drag(await point('WeaponScene','hyt',item.index),await point('WeaponScene','myt',generalIndex),'equip weapon '+weaponId);
    await page.waitForFunction(({weaponId,generalIndex})=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.equip[generalIndex]===weaponId,{weaponId,generalIndex},{timeout:15000});
  }
  async function selectAvatar(id){
    await tap('MainScene','avatarArea');await waitScene('AvatarSettingScene');
    await page.waitForFunction(()=>{const s=window.__XanaduZhaoyunInternals.SceneMgr.instance().co('AvatarSettingScene');return !s['$et']&&s.avatarPanel.mouseEnabled;},null,{timeout:20000});
    for(let attempt=0;attempt<12;attempt++){
      const geometry=await page.evaluate(id=>{
        const s=window.__XanaduZhaoyunInternals.SceneMgr.instance().co('AvatarSettingScene'),index=s.Fet.findIndex(x=>x.avatarEnum===id),n=s.Fet[index].box,r=document.querySelector('canvas').getBoundingClientRect();
        const css=p=>({x:r.left+p.x*r.width/Laya.stage.width,y:r.top+p.y*r.height/Laya.stage.height});
        return{index,selectedIndex:s.selectedIndex,stripX:s.rat.x,target:css(n.localToGlobal(new Laya.Point(n.width/2,n.height/2))),left:css(s.avatarPanel.localToGlobal(new Laya.Point(0,s.avatarPanel.height/2))),right:css(s.avatarPanel.localToGlobal(new Laya.Point(s.avatarPanel.width,s.avatarPanel.height/2)))};
      },id);
      (result.avatarScroll||(result.avatarScroll=[])).push({id,attempt,...geometry});
      if(geometry.target.x>geometry.left.x+15&&geometry.target.x<geometry.right.x-15){await tap('AvatarSettingScene','Fet',geometry.index,'scene','box');break;}
      const center=(geometry.left.x+geometry.right.x)/2,a={x:center+24,y:geometry.right.y},b={x:center-24,y:geometry.left.y};
      await drag(geometry.target.x>center?a:b,geometry.target.x>center?b:a,'scroll avatar carousel');await page.waitForTimeout(500);
    }
    await page.waitForFunction(id=>window.__XanaduZhaoyunInternals.SceneMgr.instance().co('AvatarSettingScene').iat===id,id,{timeout:10000});
    await page.waitForFunction(()=>!window.__XanaduZhaoyunInternals.SceneMgr.instance().co('AvatarSettingScene')['$et']);await tap('AvatarSettingScene','confirmBtn');await waitScene('MainScene');
    assert.equal(await page.evaluate(()=>window.__XanaduZhaoyunInternals.DataMgr.instance().player.gameAvatar),id);
  }

  async function starterWriteFailure(){
    const c=await browser.newContext({viewport:{width:360,height:640},deviceScaleFactor:1,hasTouch:true,isMobile:true}),p=await c.newPage();
    let rejectGift=true;
    const fault={rejected:null,reloaded:null,writes:[],retryWrites:[],remote:[],missing:[],errors:[]};result.starterTransaction=fault;
    p.on('pageerror',error=>fault.errors.push(error.message));
    await p.route('**/*',async route=>{
      const u=new URL(route.request().url());if(u.origin!==origin){fault.remote.push(u.href);return route.abort();}
      const relative=decodeURIComponent(u.pathname).replace(/^\/+/, '')||'index.html', file=path.resolve(content,relative);
      if(!file.startsWith(content+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()){fault.missing.push(relative);return route.fulfill({status:404,body:'Not found'});}
      let body=fs.readFileSync(file);
      if(relative==='js/index.js')body=Buffer.concat([Buffer.from(`
        (()=>{const original=Laya.LocalStorage.setItem;window.__progressionWrites=[];
          Laya.LocalStorage.setItem=function(key,value){
            if(key==='xanadu.zhaoyun-adou.player.v1'){
              const data=JSON.parse(value),row={weaponFree:!!data._weaponFree,fragments:JSON.parse(JSON.stringify(data._weaponFragments||[])),newIds:(data._newWeaponIds||[]).slice(),rejected:${rejectGift}&&data._weaponFree===true};
              window.__progressionWrites.push(row);if(row.rejected)throw new Error('PROGRESSION_FIXTURE_GIFT_WRITE_REJECTED');
            }
            return original.apply(this,arguments);
          };
        })();\n`),body]);
      await route.fulfill({status:200,contentType:mime[path.extname(file)]||'application/octet-stream',body});
    });
    const capture=()=>p.evaluate(()=>{
      const i=window.__XanaduZhaoyunInternals,d=i.DataMgr.instance(),player=d.player,disk=JSON.parse(Laya.LocalStorage.getItem('xanadu.zhaoyun-adou.player.v1'));
      return{status:window.XanaduZhaoyun.status(),memory:{weaponFree:player.weaponFree,fragments:player.weaponFragments},disk:{weaponFree:!!disk._weaponFree,fragments:disk._weaponFragments||[]},expected:[2,1,0,3].map(type=>{const[id,w]=[...d.wy.my].find(([,w])=>w.type===type&&w.rarity===0);return[id,w.fragmentNum];}).sort((a,b)=>a[0]-b[0]),writes:window.__progressionWrites};
    });
    try{
      await p.goto(origin+'/index.html');await p.waitForFunction(()=>window.__progressionWrites?.some(x=>x.rejected),null,{timeout:45000});await p.waitForTimeout(300);
      const rejected=await capture();fault.rejected=rejected;fault.writes=rejected.writes;delete rejected.writes;
      fault.hostCalls=await p.evaluate(()=>({configure:window.XanaduZhaoyun.configure({soundEnabled:false}),setActive:window.XanaduZhaoyun.setActive(false),status:window.XanaduZhaoyun.status()}));
      const failureShot=path.join(evidence,giftBaseline?'progression-gift-baseline-rejected.png':'progression-gift-rejected.png');await p.screenshot({path:failureShot});result.screenshots.push(failureShot);
      rejectGift=false;await p.reload();await p.waitForFunction(()=>window.XanaduZhaoyun?.status().ready,null,{timeout:45000});
      const reloaded=await capture();fault.reloaded=reloaded;fault.retryWrites=reloaded.writes;delete reloaded.writes;
      // Reject only the final gift flag write; old split writes leave a partial gift and duplicate it on reload.
      assert.equal(rejected.status.ready,false,'gift commit failure must keep initialization unready');assert(rejected.status.error,'gift failure must be observable');
      assert.equal(fault.hostCalls.status.ready,false);assert(fault.hostCalls.status.error,'host configure/deactivation must preserve gift error');
      assert.equal(rejected.memory.weaponFree,false);assert.deepEqual(rejected.memory.fragments,[]);assert.equal(rejected.disk.weaponFree,false);assert.deepEqual(rejected.disk.fragments,[]);
      assert.equal(fault.writes.filter(x=>x.rejected).length,1,'gift should attempt one complete transaction');
      const attempted=fault.writes.find(x=>x.rejected);assert.deepEqual(attempted.fragments.slice().sort((a,b)=>a[0]-b[0]),rejected.expected);assert.equal(attempted.newIds.length,4);
      assert.deepEqual(reloaded.disk.fragments.slice().sort((a,b)=>a[0]-b[0]),reloaded.expected);assert.equal(reloaded.disk.weaponFree,true);
      const fragmentCommits=fault.retryWrites.filter(x=>x.fragments.some(([,count])=>count>0));
      assert(fragmentCommits.length>0);assert(fragmentCommits.every(x=>x.weaponFree&&JSON.stringify(x.fragments.slice().sort((a,b)=>a[0]-b[0]))===JSON.stringify(reloaded.expected)),'no partial gift may be committed');
      assert.deepEqual(fault.remote,[]);assert.deepEqual(fault.missing,[]);assert.deepEqual(fault.errors,[]);phase('starter gift write failure preserves empty save and reload grants the four weapons exactly once');
    }finally{const file=path.join(evidence,giftBaseline?'progression-gift-baseline.png':'progression-gift-retry.png');await p.screenshot({path:file}).catch(()=>{});result.screenshots.push(file);await c.close();}
  }
  try{
    result.contentDigest=JSON.parse(fs.readFileSync(path.join(content,'../content-build.json'),'utf8')).contentDigest;result.browserVersion=browser.version();
    if(giftBaseline){await starterWriteFailure();result.passed=true;}else{
    const started=Date.now();await page.goto(origin+'/index.html');await ready();const initial=await snapshot();result.initial=initial;
    assert(initial.registerTime>=started-1000&&initial.registerTime<=Date.now(),'registration time must remain real');assert.equal(initial.loginDays,1);assert.equal(initial.day,1);
    await tap('MainScene','weaponBtn');await page.waitForTimeout(1200);
    result.afterWeaponTap=await status();
    assert.equal(initial.weaponOpen,true,'fresh day-one player must pass the original weapon gate');
    await waitScene('WeaponScene');
    const expected=await page.evaluate(()=>{const d=window.__XanaduZhaoyunInternals.DataMgr.instance();return[2,1,0,3].map(type=>{const [id,w]=[...d.wy.my].find(([,w])=>w.type===type&&w.rarity===0);return{id,count:w.fragmentNum};}).sort((a,b)=>a.id-b.id);});
    assert.equal(initial.weaponFree,true);assert.deepEqual(initial.inventory.slice().sort((a,b)=>a.id-b.id),expected);
    await collectNewWeapon();phase('day-one real touch opens weapons and collects the original four starter weapons');
    await shot('progression-weapons');
    const spear=await page.evaluate(()=>{const d=window.__XanaduZhaoyunInternals.DataMgr.instance();return[...d.wy.my].find(([,w])=>w.type===1&&w.rarity===0)[0];});
    await equipWeapon(spear,0);phase('real drag equips the starter spear through original compatibility rules');
    await shot('progression-equipped');
    await tap('WeaponScene','xBtn');await waitScene('MainScene');
    const fixture=await page.evaluate(spear=>{
      const i=window.__XanaduZhaoyunInternals,d=i.DataMgr.instance(),old=d.wy.nm(spear),[id,w]=[...d.wy.my].find(([,w])=>w.type===old.type&&w.rarity>old.rarity&&w.addAttPower>old.addAttPower&&w.fragmentNum>0);
      const previous=d.player.getWeaponFragmentCount(id);i.WeaponMgr.instance().setWeaponFragments(id,w.fragmentNum-previous);
      return{kind:'original WeaponMgr fragment grant',id,name:w.txt,added:w.fragmentNum-previous,threshold:w.fragmentNum,beforeAttack:old.addAttPower,afterAttack:w.addAttPower};
    },spear);result.fixtures.push(fixture);
    await tap('MainScene','weaponBtn');await waitScene('WeaponScene');await collectNewWeapon();
    const upgradeIndex=await page.evaluate(id=>window.__XanaduZhaoyunInternals.SceneMgr.instance().co('WeaponScene').iyt.findIndex(x=>x.type==='group'&&x.weaponId===id),fixture.id);
    await tap('WeaponScene','hyt',upgradeIndex);await waitDialog('WeaponIntroDialog');
    const detail=await page.evaluate(()=>{const d=window.__XanaduZhaoyunInternals.SceneMgr.instance().ho.get('WeaponIntroDialog');return{name:d.weaponName.text,attack:Number(d.attTxt.text)};});
    assert.equal(detail.name,fixture.name);assert.equal(detail.attack,fixture.afterAttack);await tap('WeaponIntroDialog','xBtn',undefined,'dialog');await waitDialog('WeaponIntroDialog',false);
    await equipWeapon(fixture.id,0);assert(fixture.afterAttack>fixture.beforeAttack);result.equipmentImprovement={weaponId:fixture.id,fromAttack:fixture.beforeAttack,toAttack:fixture.afterAttack};
    phase('original fragment completion, collection, details and touch replacement improve weapon attack');await shot('progression-improved');
    await tap('WeaponScene','xBtn');await waitScene('MainScene');
    const gates=await page.evaluate(()=>{const i=window.__XanaduZhaoyunInternals,d=i.DataMgr.instance(),s=i.SpecialGeneralMgr.instance();return{rank:d.rank.iL.id,openProps:d.player.openProps,avatars:[3,5,6,11].map(id=>({id,unlocked:d.player.isAvatarUnlocked(id)})),specialRankThreshold:i.PlayerData.In,aiLowRank:s.qL().map(meta=>s.YL(meta,1)),specialPlayer:s.hL().map(type=>s.nL(type))};});
    assert(gates.avatars.find(x=>x.id===3).unlocked);assert(gates.avatars.find(x=>x.id===5).unlocked);
    assert.equal(gates.avatars.find(x=>x.id===6).unlocked,false);assert.equal(gates.avatars.find(x=>x.id===11).unlocked,false);
    assert.equal(gates.specialRankThreshold,50);assert(gates.aiLowRank.length>0);assert(gates.aiLowRank.every(x=>x===0));assert(gates.specialPlayer.every(x=>x===false));assert.equal(gates.openProps,false);result.gates=gates;
    await selectAvatar(3);await selectAvatar(5);phase('day-one avatars are selectable while rank and special-general conditions remain intact');await shot('progression-avatar');
    const beforeReload=await snapshot();assert.equal(beforeReload.registerTime,initial.registerTime);assert.equal(beforeReload.loginDays,1);
    assert((await page.evaluate(()=>window.XanaduZhaoyun.setActive(false))).ok);await page.reload();await ready();const restored=await snapshot();
    assert.equal(restored.weaponFree,true);assert.deepEqual(restored.inventory,beforeReload.inventory);assert.deepEqual(restored.equip,beforeReload.equip);assert.equal(restored.registerTime,initial.registerTime);assert.equal(restored.loginDays,1);assert.equal(restored.gameAvatar,5);
    phase('existing weaponFree save reloads without duplicate starter weapons or forged dates');
    // Explicit match-count fixture, not simulated wins, rank, date or a forged map result.
    const mapFixture=await page.evaluate(()=>{const i=window.__XanaduZhaoyunInternals,p=i.DataMgr.instance().player,previous={win:p._data._win,lose:p._data._lose},next={...p._data,_win:0,_lose:21},saved=window.__XanaduZhaoyunPersistPlayerData(p,next);return{kind:'21 completed matches',previous,next:{win:0,lose:21},saved,choice:i.DataMgr.instance().i5(),registerTime:p.registerTime};});
    assert(mapFixture.saved.ok);assert.equal(mapFixture.choice,3);assert.equal(mapFixture.registerTime,initial.registerTime);result.fixtures.push(mapFixture);
    await tap('MainScene','playBtn');await page.waitForFunction(()=>window.XanaduZhaoyun.status().battle,null,{timeout:30000});
    await page.waitForFunction(()=>window.__XanaduZhaoyunInternals.DataMgr.instance().map.mapIndex===3,null,{timeout:15000});await page.waitForTimeout(2000);
    phase('map three loads in a real battle without waiting for another calendar day');await shot('progression-map3');result.final=await snapshot();
    assert.deepEqual(result.remote,[]);assert.deepEqual(result.missing,[]);assert.deepEqual(result.errors,[]);await starterWriteFailure();result.passed=true;}
  }catch(error){result.error=error.stack;result.final=await snapshot().catch(()=>null);}
  finally{await shot(baseline?'progression-baseline':'progression-final').catch(()=>{});await context.close();await browser.close();}
  if(result.remote.length||result.missing.length||result.errors.length)result.passed=false;
  fs.writeFileSync(path.join(evidence,baseline?'progression-baseline.json':giftBaseline?'progression-gift-baseline.json':'progression.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));process.exitCode=result.passed?0:1;
})().catch(error=>{console.error(error);process.exitCode=1;});
