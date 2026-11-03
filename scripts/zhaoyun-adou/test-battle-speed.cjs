'use strict';
// Real Laya game and touchscreen input. The probe reads the original clocks without driving them.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require('playwright-core');
const root = path.resolve(__dirname, '../..');
const arg = process.argv.indexOf('--source');
if (arg >= 0 && !process.argv[arg+1]) throw new Error('--source requires a generated content directory');
const content = arg >= 0 ? path.resolve(process.argv[arg+1]) : path.join(root, 'artifacts/zhaoyun-adou/content');
const evidence = path.join(path.dirname(content),'browser-evidence');
const origin = 'https://zhaoyun-adou.xanadu.invalid';
const executablePath = process.env.CHROMIUM_PATH || ['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome','/usr/bin/chromium','/usr/bin/chromium-browser'].find(fs.existsSync);
const mime = {'.html':'text/html','.js':'text/javascript','.css':'text/css','.json':'application/json','.ls':'application/json','.lh':'application/json','.png':'image/png','.mp3':'audio/mpeg','.wav':'audio/wav','.atlas':'text/plain','.txt':'text/plain'};
const report = {passed:false,content,sizes:[],remote:[],missing:[],errors:[]};
fs.mkdirSync(evidence,{recursive:true});

const waitBattle = page => page.waitForFunction(() => {
  const i=window.__XanaduZhaoyunInternals;
  const state=window.XanaduZhaoyun.status();
  return state.battle && !state.paused && i.TimeMgr.instance().yr.has('BattleMgr');
},null,{timeout:30000});
const waitScene = (page,name) => page.waitForFunction(n=>window.XanaduZhaoyun.status().scene===n,name,{timeout:30000});
const caption = page => page.evaluate(()=>window.__XanaduZhaoyunInternals.SceneMgr.instance().co('BattleScene')?.offlineSpeedButton?.getChildByName('speedCaption')?.text);
async function assertSpeed(page,speed) {
  assert.equal((await caption(page))?.trim().split(/\s+/).pop(), speed+'×', 'visible speed caption');
}
async function point(page,owner,field) {
  const coords = await page.evaluate(({owner,field})=>{
    const scenes=window.__XanaduZhaoyunInternals.SceneMgr.instance();
    const s=owner==='PauseDialog' ? scenes.ho.get(owner) : scenes.co(owner);
    const node=field==='coin' ? s?.goldTxt?.parent?.getChildAt(0) : s?.[field];
    if (!node?.parent || !node.visible) return null;
    const p=node.localToGlobal(new Laya.Point(node.width/2,node.height/2));
    const bounds=document.querySelector('canvas').getBoundingClientRect();
    return {x:bounds.left+p.x*bounds.width/Laya.stage.width,y:bounds.top+p.y*bounds.height/Laya.stage.height};
  },{owner,field});
  const {width,height}=page.viewportSize();
  assert(coords && coords.x>0 && coords.x<width && coords.y>0 && coords.y<height, owner+'.'+field+' is not on screen: '+JSON.stringify(coords));
  if (field==='offlineSpeedButton') assert(coords.x>width/2 && coords.y<height/3,'speed button belongs in the top right');
  return coords;
}
async function tap(page,row,owner,field) {
  const p=await point(page,owner,field);
  row.touches.push({owner,field,...p});
  await page.touchscreen.tap(p.x,p.y);
}

// x9 belongs to the live wave controller registered by BattleMgr.startGame. It resets at
// phase/wave boundaries, so compare its deltas with pr only between consecutive stable samples.
async function clock(page,row,label,speed,battle=true,probeAttacks=false) {
  const sample=await page.evaluate(async ({battle,probeAttacks})=>{
    const i=window.__XanaduZhaoyunInternals,time=i.TimeMgr.instance(),wave=time.yr.get('BattleMgr')?.caller;
    if (battle && !wave) throw new Error('The battle wave controller has not started');
    // Exercise the pinned R9 soldier and general cooldowns without inserting synthetic units
    // into the real battle. Both sides have a target and a 200 ms attack interval in game time.
    const hits={playerSoldier:[],enemySoldier:[],playerGeneral:[],enemyGeneral:[]}, target=[{}];
    function attacker(player,log) {
      return {pk:true,TS:false,Jm:player,Aw:-Infinity,ck:.2,currentState:'UnitAttack',
        km:{x:0,y:0,width:1,height:1},Mn:1,$b:target,wN(){this.$b=target;},attack(){log.push(performance.now());}};
    }
    const fixture=probeAttacks && {
      jD:[attacker(true,hits.playerSoldier),attacker(false,hits.enemySoldier)],
      jA:[attacker(true,hits.playerGeneral),attacker(false,hits.enemyGeneral)],
      _k:{uP:()=>target}
    };
    const probeKey='xanadu-test:R9'; let steps=0,maxStepMs=0;
    if (fixture) time.Lr(probeKey,fixture,function(delta) {
      steps++;maxStepMs=Math.max(maxStepMs,delta);
      wave.R9.call(this,delta);
    });
    function read(){return {wall:performance.now(),date:Date.now(),game:time.pr,timer:wave?.x9,phase:wave?.k9,wave:i.DataMgr.instance().Qf.Rs};}
    const start=read();let prev=start, current, stableGame=0, stableWave=0,frames=0,changedFrames=0,pausedFrames=0;
    try {
      do {
        await new Promise(requestAnimationFrame);
        current=read();
        if (time.ur) pausedFrames++;
        if (current.game!==prev.game) changedFrames++;
        if (battle && current.phase===prev.phase && current.wave===prev.wave && current.timer>=prev.timer) {
          stableGame+=current.game-prev.game;stableWave+=current.timer-prev.timer;
        }
        prev=current;frames++;
      } while (current.wall-start.wall<1250);
    } finally {
      if (fixture) time.wr(probeKey);
    }
    const attacks=fixture && Object.fromEntries(Object.entries(hits).map(([kind,times])=>[kind,{
      hits:times.length,meanIntervalMs:times.length>1?(times.at(-1)-times[0])/(times.length-1):null}]));
    return {realMs:current.wall-start.wall,dateMs:current.date-start.date,gameMs:current.game-start.game,stableWave,stableGame,frames,changedFrames,pausedFrames,timerScale:Laya.timer.scale,attacks,steps,maxStepMs,
      fromWave:start.wave,toWave:current.wave};
  },{battle,probeAttacks});
  sample.rate=sample.gameMs/sample.realMs;row.clocks.push({label,...sample});
  assert(sample.rate>=speed*.72 && sample.rate<=speed*1.17,label+' real-time rate: '+JSON.stringify(sample));
  assert(Math.abs(sample.dateMs-sample.realMs)<50,label+' Date.now advanced at the wrong rate: '+JSON.stringify(sample));
  if (battle) {
    assert(sample.stableGame>=400 && Math.abs(sample.stableWave-sample.stableGame)<=sample.stableGame*.025,
      label+' wave clock did not follow the gameplay clock: '+JSON.stringify(sample));
  }
  if (probeAttacks && speed>1) {
    assert(sample.steps>=sample.changedFrames*speed && sample.maxStepMs<=20.1,label+' combat substeps: '+JSON.stringify(sample));
  }
  return sample;
}
async function frozen(page,label) {
  const state=()=>page.evaluate(()=>{const i=window.__XanaduZhaoyunInternals,t=i.TimeMgr.instance(),w=t.yr.get('BattleMgr').caller;
    return {paused:window.XanaduZhaoyun.status().paused,clock:t.pr,wave:i.DataMgr.instance().Qf.Rs,timer:w.x9};});
  const before=await state();assert(before.paused,label+' was not paused');
  await page.waitForTimeout(250);assert.deepEqual(await state(),before,label+' continued advancing');
  // The original pause dialog animates with Laya.timer still running. Its elapsed UI
  // time must not become an immediate catch-up step at the next TimeMgr.update.
  const pending=await page.evaluate(()=>{
    const time=window.__XanaduZhaoyunInternals.TimeMgr.instance();
    return Laya.timer.scale===1 ? Laya.timer.currTimer-time.gr : 0;
  });
  assert(Math.abs(pending)<40,label+' left paused UI time queued for combat: '+pending);
}
async function pause(page,row) {
  await tap(page,row,'BattleScene','xBtn');
  await page.waitForFunction(()=>window.XanaduZhaoyun.status().paused && window.__XanaduZhaoyunInternals.SceneMgr.instance().ho.get('PauseDialog')?.parent,null,{timeout:10000});
  // Laya animates the dialog into its final touch position.
  await page.waitForTimeout(350);
}
async function exitBattle(page,row) {
  await pause(page,row);await tap(page,row,'PauseDialog','yesBtn');await waitScene(page,'GameOverScene');
  await page.waitForFunction(()=>{const button=window.__XanaduZhaoyunInternals.SceneMgr.instance().co('GameOverScene')?.getBtn;
    return button?.parent && button.visible && button.scaleX>=.98 && button.scaleY>=.98;},null,{timeout:20000});
  await tap(page,row,'GameOverScene','getBtn');await waitScene(page,'MainScene');
}

async function checkResumes(page,row,speed,withAid) {
  await pause(page,row);await frozen(page,'original pause dialog');
  await tap(page,row,'PauseDialog','noBtn');await assertSpeed(page,speed);
  await clock(page,row,'after pause dialog '+speed+'×',speed);
  if (withAid) {
    await tap(page,row,'BattleScene','offlineGeneralAidButton');
    const aid=page.getByRole('dialog',{name:'战斗援将',exact:true});
    await aid.waitFor({state:'visible'});await frozen(page,'battle aid dialog');
    await aid.getByRole('button',{name:'返回战斗',exact:true}).tap();
    await assertSpeed(page,speed);await clock(page,row,'after battle aid '+speed+'×',speed);
  }
  const saveStart=Date.now();
  assert((await page.evaluate(()=>XanaduZhaoyun.setActive(false))).ok);
  const savedAt=await page.evaluate(()=>JSON.parse(Laya.LocalStorage.getItem(XanaduZhaoyun.status().saveKey))._saveTime);
  const save={speed,start:saveStart,savedAt,end:Date.now()};
  row.saves.push(save);
  assert(savedAt>=save.start-50 && savedAt<=save.end+50,'background save timestamp must use the real clock');
  await frozen(page,'background');
  assert((await page.evaluate(()=>XanaduZhaoyun.setActive(true))).ok);await frozen(page,'waiting for explicit Continue');
  await page.getByRole('button',{name:'继续游戏',exact:true}).tap();
  await assertSpeed(page,speed);await clock(page,row,'after foreground Continue '+speed+'×',speed);
}

async function runSize(browser,width,height,withAid,exitSpeed) {
  const context=await browser.newContext({viewport:{width,height},deviceScaleFactor:1,isMobile:true,hasTouch:true});
  const page=await context.newPage(),row={width,height,touches:[],clocks:[],saves:[]};report.sizes.push(row);
  page.on('pageerror',err=>report.errors.push(err.message));
  await page.route('**/*',async route=>{
    const u=new URL(route.request().url());
    if (u.origin!==origin){report.remote.push(u.href);return route.abort();}
    const name=decodeURIComponent(u.pathname).replace(/^\/+/, '')||'index.html',file=path.resolve(content,name);
    if (!file.startsWith(content+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()) {
      report.missing.push(name);return route.fulfill({status:404,body:'Missing asset'});
    }
    return route.fulfill({contentType:mime[path.extname(file)]||'application/octet-stream',body:fs.readFileSync(file)});
  });
  try {
    await page.goto(origin+'/index.html');
    await page.waitForFunction(()=>window.XanaduZhaoyun?.status().ready,null,{timeout:45000});
    const results=await page.evaluate(()=>[XanaduZhaoyun.configure({soundEnabled:false}),XanaduZhaoyun.setActive(true)]);
    assert(results.every(r=>r.ok));
    if (withAid) for(let i=0;i<3;i++) await tap(page,row,'MainScene','coin');
    await tap(page,row,'MainScene','playBtn');await waitBattle(page);
    await assertSpeed(page,1);
    const one=await clock(page,row,'initial 1×',1,true,true);
    let previous=one;
    for (const speed of [2,3]) {
      await tap(page,row,'BattleScene','offlineSpeedButton');await assertSpeed(page,speed);
      const unloadedSpine=await page.evaluate(()=>{
        // The battle can attach a Spine node while its template is still loading.
        const node=new Laya.Spine2DRenderNode();
        const hasKeeper=Boolean(node._timeKeeper);
        node.onUpdate();
        return {hasKeeper,updated:true};
      });
      assert.deepEqual(unloadedSpine,{hasKeeper:false,updated:true});
      const sample=await clock(page,row,'touch selected '+speed+'×',speed,true,true);
      const rateRatio=sample.rate/one.rate;
      assert(rateRatio>speed*.775 && rateRatio<speed*1.275,speed+'×/1× game clock: '+rateRatio);
      assert(sample.rate/previous.rate>1.3,speed+'× too close to previous speed '+previous.rate);
      for (const kind of Object.keys(one.attacks)) {
        const baseline=one.attacks[kind],fast=sample.attacks[kind];
        const intervalRatio=baseline.meanIntervalMs/fast.meanIntervalMs;
        assert(baseline.hits>=4 && fast.hits>previous.attacks[kind].hits && intervalRatio>speed*.75 && intervalRatio<speed*1.3,
          kind+' '+speed+'×/1× attack cooldown: '+JSON.stringify({baseline,fast}));
      }
      await page.screenshot({path:path.join(evidence,'speed-'+width+'x'+height+'-'+speed+'x.png')});
      await checkResumes(page,row,speed,withAid);
      previous=sample;
    }
    await tap(page,row,'BattleScene','offlineSpeedButton');await assertSpeed(page,1);await clock(page,row,'touch back to 1×',1);
    for (let speed=2;speed<=exitSpeed;speed++) {
      await tap(page,row,'BattleScene','offlineSpeedButton');await assertSpeed(page,speed);
    }
    await exitBattle(page,row);
    await clock(page,row,'main menu after '+exitSpeed+'× battle',1,false);
    await tap(page,row,'MainScene','playBtn');await waitBattle(page);await assertSpeed(page,1);
    await clock(page,row,'next battle defaults to 1×',1);
    console.log('PASS '+width+'×'+height+' touch, clock, pauses and reset');
  } catch (error) {
    await page.screenshot({path:path.join(evidence,'speed-'+width+'x'+height+'-failure.png')}).catch(()=>{});
    throw error;
  } finally { await context.close(); }
}

(async()=>{
  const browser=await chromium.launch({executablePath,headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try {
    report.chromium=browser.version();
    report.contentDigest=JSON.parse(fs.readFileSync(path.resolve(content,'../content-build.json'),'utf8')).contentDigest;
    await runSize(browser,320,568,true,3);
    await runSize(browser,360,640,false,2);
    assert.deepEqual(report.remote,[]);assert.deepEqual(report.missing,[]);assert.deepEqual(report.errors,[]);
    report.passed=true;
  } catch(error) {report.error=error.stack;console.error(error);}
  finally {
    await browser.close();
    fs.writeFileSync(path.join(evidence,'battle-speed.json'), JSON.stringify(report,null,2));
    process.exitCode=report.passed?0:1;
  }
})().catch(e=>{console.error(e);process.exitCode=1;});
