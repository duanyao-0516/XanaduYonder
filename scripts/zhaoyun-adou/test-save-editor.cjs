'use strict';
// Real original game, local assets, Chromium touchscreen. NODE_PATH may point to a
// separately installed playwright-core. Use --source DIR to check a candidate build.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright-core');
const root = path.resolve(__dirname, '../..');
const sourceArg = process.argv.indexOf('--source');
if (sourceArg >= 0 && !process.argv[sourceArg + 1]) throw new Error('--source requires a directory');
const content = sourceArg < 0 ? path.join(root, 'artifacts/zhaoyun-adou/content') : path.resolve(process.argv[sourceArg + 1]);
const evidence = path.join(root, 'artifacts/zhaoyun-adou/browser-evidence');
const origin = 'https://zhaoyun-adou.xanadu.invalid';
const key = 'xanadu.zhaoyun-adou.player.v1';
const mime = {'.html':'text/html','.css':'text/css','.js':'text/javascript','.json':'application/json','.ls':'application/json','.lh':'application/json','.png':'image/png','.mp3':'audio/mpeg','.wav':'audio/wav','.atlas':'text/plain','.txt':'text/plain'};
const executablePath = process.env.CHROMIUM_PATH || ['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome','/usr/bin/chromium','/usr/bin/chromium-browser'].find(fs.existsSync);
const report = {passed:false, content, phases:[], touches:[], fixtures:[], remote:[], missing:[], errors:[]};
const note = name => { report.phases.push(name); console.log('PASS ' + name); };
const checkOk = result => assert.equal(result?.ok, true, JSON.stringify(result));

async function launchGame(browser, width, height, save) {
  // Seed Chromium's genuine origin storage before the original PlayerData.init reads it.
  const storageState = save && {cookies:[], origins:[{origin,localStorage:[{name:key,value:JSON.stringify(save)}]}]};
  const context = await browser.newContext({viewport:{width,height},deviceScaleFactor:1,isMobile:true,hasTouch:true,storageState});
  const page = await context.newPage();
  page.on('pageerror', e => report.errors.push(e.message));
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (url.origin !== origin) { report.remote.push(url.href); return route.abort(); }
    const relative = decodeURIComponent(url.pathname).replace(/^\/+/, '') || 'index.html';
    const file = path.resolve(content, relative);
    if (!file.startsWith(content + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
      report.missing.push(relative); return route.fulfill({status:404,body:'Not found'});
    }
    return route.fulfill({contentType:mime[path.extname(file)] || 'application/octet-stream',body:fs.readFileSync(file)});
  });
  await page.goto(origin + '/index.html');
  await activate(page);
  return {context,page};
}

async function activate(page) {
  await page.waitForFunction(() => window.XanaduZhaoyun?.status().ready, null, {timeout:45000});
  const state = await page.evaluate(() => [window.XanaduZhaoyun.configure({soundEnabled:false}),window.XanaduZhaoyun.setActive(true),window.XanaduZhaoyun.status()]);
  checkOk(state[0]); checkOk(state[1]); assert.equal(state[2].scene, 'MainScene');
}

async function touchScene(page, sceneName, field) {
  const point = await page.evaluate(({sceneName,field}) => {
    const scene = window.__XanaduZhaoyunInternals.SceneMgr.instance().co(sceneName), node = scene[field];
    if (!node?.parent || !node.visible || (field.startsWith('offline') && !node.mouseEnabled)) return null;
    const p = node.localToGlobal(new Laya.Point(node.width / 2, node.height / 2));
    const rect = document.querySelector('canvas').getBoundingClientRect();
    return {x:rect.left + p.x * rect.width / Laya.stage.width,y:rect.top + p.y * rect.height / Laya.stage.height};
  }, {sceneName,field});
  assert(point, sceneName + '.' + field + ' must be a visible, enabled game entry');
  const {width,height} = page.viewportSize();
  assert(point.x > 0 && point.x < width && point.y > 0 && point.y < height, 'offscreen ' + field + ': ' + JSON.stringify(point));
  report.touches.push({width,height,field,...point});
  await page.touchscreen.tap(point.x, point.y);
}

const touchMain = (page,field) => touchScene(page,'MainScene',field);

async function openEditor(page) {
  await touchMain(page, 'offlineSaveEditorButton');
  const dialog = page.getByRole('dialog', {name:'存档管理',exact:true});
  await dialog.waitFor({state:'visible',timeout:8000});
  return dialog;
}

const disk = page => page.evaluate(key => JSON.parse(localStorage.getItem(key)), key);
const catalog = page => page.evaluate(() => window.XanaduSaveEditorData.catalog());
const apply = (page, op) => page.evaluate(op => window.XanaduSaveEditorData.apply(op), op);

const byId = (rows, id) => rows.find(row => row.id === id);
const propOp = (id, level = 1, replaceId) => ({kind:'prop',id,level,replaceId});
const button = (panel, name) => panel.getByRole('button',{name,exact:true});
const field = (panel, name) => panel.getByLabel(name, {exact:false});

async function submit(page, panel, name) {
  // The real panel debounces repeated changes for 450 ms across all four tabs.
  await page.waitForTimeout(460);
  await button(panel, name).tap();
}

async function closeEditor(panel) {
  await button(panel,'返回游戏').tap();
  await panel.waitFor({state:'hidden'});
}

function progress(data) {
  const keys = ['_win','_lose','_winDay','_loseDay','_curStar','_lastStar','_seasonRound',
    '_mergedGCount','_mergedGenerals','_specialGeneralFragments','_unlockedSpecialGenerals','_equip','_isGetLastRankReward'];
  return Object.fromEntries(keys.map(k => [k,data[k]]));
}

async function rejected(page, operation, fault = false) {
  const actual = await page.evaluate(({operation,key,fault}) => {
    const player = window.__XanaduZhaoyunInternals.DataMgr.instance().player;
    const storage = Laya.LocalStorage, original = storage.setItem;
    const raw = () => ({disk:storage.getItem(key),memory:JSON.stringify(player._data)});
    const before = raw();
    let writeAttempts = 0;
    if (fault) storage.setItem = () => { writeAttempts++; }; // emulate quota/no-op storage, trigger readback mismatch
    try {
      const result = window.XanaduSaveEditorData.apply(operation);
      return {unchanged:JSON.stringify(before) === JSON.stringify(raw()),result,writeAttempts};
    } finally { storage.setItem = original; }
  },{operation,key,fault});
  assert.equal(actual.result?.ok,false,JSON.stringify(operation));
  assert(actual.result.error, 'failure must be reported');
  assert(actual.unchanged,'a rejected command mutated disk or memory: ' + JSON.stringify(operation));
  if (fault) {
    assert.equal(actual.writeAttempts,1,'one verified commit per edit');
    checkOk(await page.evaluate(() => {
      const p = window.__XanaduZhaoyunInternals.DataMgr.instance().player;
      return window.__XanaduZhaoyunPersistPlayerData(p,p._data);
    }));
  }
}

async function editWeapons(page, panel, initial) {
  const definitions = await page.evaluate(() =>
    [...window.__XanaduZhaoyunInternals.DataMgr.instance().wy.my].map(([id,def]) => ({id,threshold:def.fragmentNum || 1})));
  assert.equal(initial.weapons.length,46);
  assert.deepEqual(initial.weapons.map(w=>w.id).sort((a,b)=>a-b),definitions.map(w=>w.id).sort((a,b)=>a-b));
  const sample = initial.weapons.find(w=>!w.count && byId(definitions,w.id).threshold>1);
  assert(sample);
  await button(panel,'武器').tap();
  await field(panel,'选择武器').selectOption(String(sample.id));
  await field(panel,'整把数量').fill('2');
  await submit(page,panel,'添加武器');
  assert.equal(byId((await catalog(page)).weapons,sample.id).count,2);
  // All 46 definitions pass through the actual data module; normal UI touch already granted the sample twice.
  // Snapshot excludes only today's startup gift; consecutive grants must be additive, including special weapons.
  const before = await disk(page);
  const edits = await page.evaluate(ids => ids.map(id=>window.XanaduSaveEditorData.apply({kind:'weapon',id,quantity:1})),definitions.map(w=>w.id));
  edits.forEach(checkOk);
  const after = await disk(page);
  assert.deepEqual(after._newWeaponIds,[...before._newWeaponIds,...definitions.map(w=>w.id)]);
  for (const {id,threshold} of definitions) {
    const prev = before._weaponFragments.find(([weaponId])=>weaponId===id)?.[1] || 0;
    assert.equal(after._weaponFragments.find(([weaponId])=>weaponId===id)[1],prev+threshold);
  }
  const delta = after._newWeaponIds.filter(id=>id===sample.id);
  assert.equal(delta.length,3); assert.equal(byId((await catalog(page)).weapons,sample.id).count,3);
  await rejected(page,{kind:'weapon',id:sample.id,quantity:1},true);
  await rejected(page,{kind:'weapon',id:sample.id,quantity:0});
  note('all 46 weapons grant exact original fragments and new-instance IDs; repeat grants add copies');

  // A cache refresh happens after the verified write. Don't offer a retry for an already-granted weapon.
  const beforeWarning = byId((await catalog(page)).weapons, sample.id).count;
  await page.evaluate(() => {
    const manager = window.__XanaduZhaoyunInternals.WeaponMgr.instance();
    const refresh = manager.refresh;
    manager.refresh = function () {
      manager.refresh = refresh;
      throw new Error('CACHE_REFRESH_FIXTURE');
    };
  });
  await field(panel,'整把数量').fill('1');
  await submit(page,panel,'添加武器');
  assert.match(await panel.getByRole('status').innerText(), /修改已保存/);
  assert(await button(panel,'添加武器').isDisabled());
  assert.equal(byId((await catalog(page)).weapons, sample.id).count, beforeWarning + 1);
}

async function editProps(page,panel) {
  const rows = (await catalog(page)).props, active = rows.filter(p=>p.kind==='active'), passive = rows.filter(p=>p.kind==='passive');
  assert.equal(rows.length,22); assert.equal(active.length,10); assert.equal(passive.length,12);
  assert(rows.every(p=>p.level===0)); assert.equal(byId(rows,22).maxLevel,3);
  const [a,b,c] = active;
  for (const item of [a,b,...passive.slice(0,6)]) checkOk(await apply(page,propOp(item.id)));
  await rejected(page,propOp(c.id)); await rejected(page,propOp(passive[6].id));
  await rejected(page,propOp(c.id,1,passive[0].id));
  await rejected(page,propOp(c.id,1,a.id),true);
  await button(panel,'道具').tap();
  // Reopen after API fixture grants, so the panel takes a fresh inventory snapshot for replacement.
  await closeEditor(panel);
  panel = await openEditor(page);
  await button(panel,'道具').tap();
  await field(panel,'选择道具').selectOption(String(c.id));
  await submit(page,panel,'保存道具');
  assert.match(await panel.getByRole('status').innerText(),/替换/);
  assert.equal(byId((await catalog(page)).props,c.id).level,0);
  await field(panel,'替换道具').selectOption(String(a.id));
  await submit(page,panel,'保存道具');
  checkOk(await apply(page,propOp(22,3,passive[0].id)));
  await rejected(page,propOp(22,4));
  const held = (await catalog(page)).props.filter(p=>p.level);
  assert.deepEqual(held.filter(x=>x.kind==='active').map(x=>x.id).sort((x,y)=>x-y),[b.id,c.id].sort((x,y)=>x-y));
  assert.equal(held.filter(x=>x.kind==='passive').length,6);
  assert.equal(byId(held,22).level,3); assert(!byId(held,passive[0].id));
  note('real shop data enforces 2 active and 6 passive slots; explicit same-kind replacement and valid levels survive');
  return panel;
}

async function editGenerals(page,panel) {
  const initial = await catalog(page);
  assert.equal(initial.generals.length,15);
  assert(initial.generals.filter(g=>g.id<12).every(g=>g.recruitable));
  assert.deepEqual(initial.generals.filter(g=>!g.recruitable).map(g=>g.id).sort((a,b)=>a-b),[12,13,14]);
  await button(panel,'武将').tap();
  await field(panel,'选择武将').selectOption('2');
  assert(await button(panel,'授予招募资格').isDisabled(), 'ordinary Ma Chao is already available');
  const before = await disk(page);
  checkOk(await apply(page,{kind:'general',id:2}));
  assert.deepEqual((await disk(page))._xanaduManualRecruitIds,before._xanaduManualRecruitIds);
  await rejected(page,{kind:'general',id:12},true);
  for (const id of [12,13,14]) {
    await field(panel,'选择武将').selectOption(String(id));
    await submit(page,panel,'授予招募资格');
    assert(await button(panel,'授予招募资格').isDisabled());
    checkOk(await apply(page,{kind:'general',id}));
  }
  const after = await disk(page);
  assert.deepEqual([...after._xanaduManualRecruitIds].sort((a,b)=>a-b),[12,13,14]);
  assert.deepEqual(progress(after),progress(before));
  assert((await catalog(page)).generals.every(g=>g.recruitable));
  note('three manual special grants are durable and idempotent; ordinary Ma Chao, rank, wins, fragments and merged history stay unchanged');
}

async function verifyBattle(page) {
  await page.evaluate(() => {
    // Record the actual jQ/$Q arguments supplied by the original CardPool.startGame. No RNG or card injection.
    const special = window.__XanaduZhaoyunInternals.SpecialGeneralMgr.instance(), original = special.PL;
    special.PL = function(player,ai) {
      const basePlayer = player.slice(), baseAI = ai.slice(), result = original.apply(this,arguments);
      window.__saveTestPools = {basePlayer,baseAI,player:player.slice(),ai:ai.slice(),
        aiRank:window.__XanaduZhaoyunInternals.DataMgr.instance().Qf.Ks.id};
      special.PL = original;
      return result;
    };
  });
  await touchMain(page,'playBtn');
  await page.waitForFunction(()=>window.XanaduZhaoyun.status().battle && window.__saveTestPools,null,{timeout:45000});
  const pools = await page.evaluate(()=>window.__saveTestPools);
  assert(pools.aiRank<50);
  const added = [...'孙尚香甄宓貂蝉'];
  assert.deepEqual(pools.player.slice().sort(),[...pools.basePlayer,...added].sort(),
    'ordinary letters retain their original weights; each special character gains exactly one ticket');
  assert.deepEqual(pools.ai,pools.baseAI);
  assert.equal(await page.evaluate(()=>window.XanaduSaveEditorUI.open()),false);
  assert.equal(await page.getByRole('dialog',{name:'存档管理'}).count(),0);
  const hasBattleEntry = await page.evaluate(()=>Boolean(window.__XanaduZhaoyunInternals.SceneMgr.instance().co('BattleScene').offlineSaveEditorButton));
  assert.equal(hasBattleEntry,false);
  report.pools = {originalPlayer:pools.basePlayer.length,player:pools.player.length,ai:pools.ai.length,aiRank:pools.aiRank,specialCharacters:added};
  note('real battle starts by touch, only the player pool adds seven special letters; editor is absent during battle');
}

async function fresh(browser) {
  const {context,page} = await launchGame(browser,320,568);
  try {
    let panel = await openEditor(page);
    await closeEditor(panel);
    note('fresh 320×568 opens and closes the menu editor by touch');
    panel = await openEditor(page);
    const initial = await catalog(page), first = await disk(page);
    assert.equal(initial.keepSkills,true);
    await field(panel,'金币').fill('718');
    await submit(page,panel,'保存设置');
    assert.equal((await disk(page))._gold,718);
    await rejected(page,{kind:'settings',gold:719,keepSkills:false},true);
    await editWeapons(page,panel,initial);
    panel = await editProps(page,panel);
    await editGenerals(page,panel);
    await closeEditor(panel);
    const expected = await catalog(page), saved = await disk(page);
    assert.deepEqual(progress(saved),progress(first));
    await page.reload(); await activate(page);
    assert.deepEqual(await catalog(page),expected);
    assert.deepEqual(progress(await disk(page)),progress(first));
    note('verified edits reload, including flags and all 46 weapon counts; rejected writes leave memory and disk intact');
    await verifyBattle(page);
    return first;
  } finally { await context.close(); }
}

function previousDay() {
  const date = new Date(); date.setDate(date.getDate()-1); date.setHours(12,0,0,0); return date.getTime();
}

async function dates(browser, first) {
  // Historical, full origin save without either new field. Original supports
  // contemporary dated triples and legacy bare IDs / ID+level tuples.
  const then = previousDay();
  const old = {...first,_openTime:then,_saveTime:then,_registerTime:then,_winDay:2,_loseDay:1,
    _isGetLastRankReward:1,_lastStar:-1,_props:[[2,1,then],[22,3,then],11,[12,1]]};
  delete old._xanaduManualRecruitIds; delete old._xanaduKeepSkills;
  report.fixtures.push({kind:'previous calendar day full save, absent editor fields',props:old._props,winDay:2,loseDay:1,rewardReceipt:1});
  const game = await launchGame(browser,360,640,old);
  let today;
  try {
    const {page} = game, panel = await openEditor(page);
    const cat = await catalog(page), loaded = await disk(page);
    assert(cat.keepSkills); assert.equal(cat.gold,old._gold);
    assert.equal(byId(cat.props,2).level,1); assert.equal(byId(cat.props,22).level,3);
    assert.equal(byId(cat.props,11).level,1); assert.equal(byId(cat.props,12).level,1);
    assert(loaded._props.every(row=>Array.isArray(row) && row.length===3 && Number.isFinite(row[2])));
    assert.equal(loaded._winDay,0); assert.equal(loaded._loseDay,0); assert.equal(loaded._isGetLastRankReward,0);
    assert.equal(loaded._curStar,first._curStar); assert.equal(loaded._lastStar,loaded._curStar);
    assert.equal(loaded._win,first._win); assert.equal(loaded._lose,first._lose);
    assert(await field(panel,'跨天保留技能道具').isChecked());
    await field(panel,'跨天保留技能道具').tap();
    await submit(page,panel,'保存设置');
    assert(!(await catalog(page)).keepSkills);
    await closeEditor(panel);
    await touchMain(page,'rankBtn');
    const claim = page.getByRole('button',{name:/领取每日段位奖励/});
    await claim.tap();
    const receipt = await disk(page);
    assert.equal(receipt._isGetLastRankReward,1); assert(receipt._gold>loaded._gold);
    assert(await page.getByRole('button',{name:'每日段位奖励已领取',exact:true}).isDisabled());
    await page.getByRole('button',{name:'返回游戏',exact:true}).tap();
    today = await disk(page);
    note('360×640 legacy load keeps dated and legacy skills, resets daily stats and permits exactly one real rank reward');
  } finally { await game.context.close(); }
  const tomorrow = {...today,_openTime:then,_saveTime:then,
    _props:[[2,1,then],[22,3,then],[11,1,Date.now()]]};
  report.fixtures.push({kind:'later boot with saved keepSkills=false and mixed acquisition dates',props:tomorrow._props});
  const expired = await launchGame(browser,360,640,tomorrow);
  try {
    const state = await catalog(expired.page);
    assert.equal(state.keepSkills,false);
    assert.equal(byId(state.props,2).level,0); assert.equal(byId(state.props,22).level,0);
    assert.equal(byId(state.props,11).level,1);
    const actual = await disk(expired.page);
    assert.deepEqual(actual._props,[[11,1,tomorrow._props[2][2]]]);
    assert.equal(actual._gold,today._gold); assert.equal(actual._isGetLastRankReward,0);
    note('persisted opt-out restores upstream expiry: old skills removed, today’s skill retained, reward receipt resets again');
  } finally { await expired.context.close(); }
}

async function rackState(page) {
  return page.evaluate(() => {
    const i = window.__XanaduZhaoyunInternals, d = i.DataMgr.instance(), c = i.ContainersMgr.instance();
    const rack = side => Array.from({length:d.map.Ya}, (_, index) => {
      const u = c.bP(3,side).getItem(index);
      return u && {id:u.id,word:u.HS,rendered:!!u.km?.parent,slot:u.kS.x};
    });
    return {player:rack(true),ai:rack(false),personalRank:d.rank.iL.id,aiRank:d.Qf.Ks.id,
      recruitment:d.player._data._xanaduManualRecruitIds.slice(),unlocked:d.player.unlockedSpecialGenerals.slice(),
      realGroup:i.SpecialGeneralMgr.instance().Jd,gold:d.Qf.gold,price:d.Qf.Gs};
  });
}

async function openAid(page,id) {
  await touchScene(page,'BattleScene','offlineGeneralAidButton');
  const panel = page.getByRole('dialog',{name:'战斗援将',exact:true});
  await panel.waitFor({state:'visible',timeout:6000});
  await field(panel,'选择武将').selectOption(String(id));
  assert.equal(await field(panel,'选择武将').locator('option').count(),15);
  return panel;
}

async function dragLetter(page,cdp,unit,cell) {
  const {from,to} = await page.evaluate(({unit,cell}) => {
    const i = window.__XanaduZhaoyunInternals, scene = i.SceneMgr.instance().co('BattleScene'), m=i.DataMgr.instance().map;
    const rect = document.querySelector('canvas').getBoundingClientRect();
    const css = p => ({x:rect.left+p.x*rect.width/Laya.stage.width,y:rect.top+p.y*rect.height/Laya.stage.height});
    return {from:css(scene.refreshBox.localToGlobal(new Laya.Point(scene.refreshBox.width/m.Ya*(unit.slot+0.5),scene.refreshBox.height/2))),
      to:css(scene.map.localToGlobal(new Laya.Point((cell.x+0.5)*m.yh,(cell.y+0.5)*m.gridHei+10)))};
  },{unit,cell});
  const {width,height} = page.viewportSize();
  for(const point of [from,to]) assert(point.x>0 && point.y>0 && point.x<width && point.y<height);
  report.touches.push({width,height,kind:'drag',word:unit.word,id:unit.id,from,to});
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{...from,id:1}]});
  await page.waitForTimeout(70);
  for(let n=1;n<=12;n++) {
    await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:from.x+(to.x-from.x)*n/12,y:from.y+(to.y-from.y)*n/12,id:1}]});
    await page.waitForTimeout(35);
  }
  await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  await page.waitForFunction(({slot,id,cell}) => {
    const c=window.__XanaduZhaoyunInternals.ContainersMgr.instance();
    return !c.bP(3,true).getItem(slot) && c.bP(1,true).getItem(cell.x,cell.y)?.id===id;
  },{slot:unit.slot,id:unit.id,cell},{timeout:10000});
}

async function aidFailureBoundaries(page) {
  // Model three original in-flight refresh animations. The visible rack is empty,
  // but these slots have valid reservations; receiving a group must respect them.
  const pending = await page.evaluate(() => {
    const slots=window.__XanaduZhaoyunInternals.SlotsMgr.instance();
    return [0,2,4].map(i=>slots.kB(true,i));
  });
  assert(pending.every(Boolean));
  report.fixtures.push({kind:'existing original SlotsMgr reservations',slots:pending.map(p=>p.index)});
  const reserved = await openAid(page,12);
  await button(reserved,'发放姓名字').tap();
  assert.match(await reserved.getByRole('status').innerText(),/腾出.*1.*格/);
  assert((await rackState(page)).player.every(x=>!x));
  await button(reserved,'返回战斗').tap(); await reserved.waitFor({state:'hidden'});
  const stayedReserved=await page.evaluate(reservations=>{
    const slots=window.__XanaduZhaoyunInternals.SlotsMgr.instance();
    const result=reservations.every(r=>slots.wB(3,true,r.index,0));
    for(const r of reservations)slots.release(r.token);
    return result;
  },pending);
  assert(stayedReserved,'failure must not release other work’s reservations');
  const panel=await openAid(page,12);
  const suspension=await page.evaluate(()=>{
    const host=window.XanaduZhaoyun;
    return [host.setActive(false),host.setActive(true),host.status()];
  });
  checkOk(suspension[0]); checkOk(suspension[1]); assert(suspension[2].awaitingContinue);
  await page.getByRole('button',{name:'继续游戏',exact:true}).tap();
  assert(await page.evaluate(()=>window.XanaduZhaoyun.status().paused),'aid panel retains its game pause');
  const before = await page.evaluate(()=>{
    const i=window.__XanaduZhaoyunInternals, manager=i.SoldierMgr.instance();
    const scene=i.SceneMgr.instance().co('BattleScene');
    const stats=()=>({ids:[...manager.$D.keys()],children:scene.refreshBox.numChildren,reservations:i.SlotsMgr.instance().yB.size});
    const original=manager.BB; let calls=0;
    manager.BB=function(...args){
      const unit=original.apply(this,args);
      if(++calls===2)throw new Error('AID_FIXTURE_AFTER_ORIGINAL_CREATION');
      return unit;
    };
    window.__restoreAidFixture=()=>{manager.BB=original;return {...stats(),calls};};
    return stats();
  });
  await button(panel,'发放姓名字').tap();
  assert.match(await panel.getByRole('status').innerText(),/AID_FIXTURE_AFTER_ORIGINAL_CREATION/);
  const after=await page.evaluate(()=>window.__restoreAidFixture());
  assert.equal(after.calls,2);
  assert.deepEqual(after.ids,before.ids); assert.equal(after.children,before.children);
  assert.equal(after.reservations,before.reservations); assert((await rackState(page)).player.every(x=>!x));
  report.fixtures.push({kind:'throw after second original BB created/rendered actual unit',calls:after.calls});
  note('existing recruit reservations are preserved; mid-group creation error rolls back units, sprites and slots; foreground continue preserves panel pause');
  return panel;
}

async function battleAid(browser) {
  // Independent fresh player: no permanent grants, so this is a genuine low-rank special merge.
  const {context,page} = await launchGame(browser,320,568);
  try {
    assert((await catalog(page)).generals.filter(g=>g.id>=12).every(g=>!g.recruitable));
    await touchMain(page,'playBtn');
    await page.waitForFunction(()=>window.XanaduZhaoyun.status().battle,null,{timeout:30000});
    // battle=true precedes release of the original scene-transition input mask.
    await page.waitForTimeout(2200);
    const before = await rackState(page), originalSave = await disk(page);
    assert(before.player.every(x=>!x)); assert(before.personalRank<50);
    let panel = await aidFailureBoundaries(page);
    const paused = await rackState(page);
    await button(panel,'发放姓名字').tap();
    await panel.waitFor({state:'hidden'});
    const after = await rackState(page);
    assert.equal(await page.evaluate(()=>window.XanaduZhaoyun.status().paused),false,'successful aid closes and resumes');
    assert.deepEqual(after.player.filter(Boolean).map(u=>u.word),[...'孙尚香']);
    assert(after.player.filter(Boolean).every(u=>u.rendered));
    assert.deepEqual(after.ai,paused.ai);
    assert.equal(after.gold,before.gold); assert.equal(after.price,before.price);
    assert.deepEqual(after.recruitment,before.recruitment); assert.deepEqual(after.unlocked,before.unlocked);
    assert.equal(after.realGroup,before.realGroup);
    assert.deepEqual(progress(await disk(page)),progress(originalSave));
    panel=await openAid(page,12);
    await button(panel,'发放姓名字').tap();
    assert.match(await panel.getByRole('status').innerText(),/腾出.*1.*格/);
    const failed=await rackState(page);
    assert.deepEqual(failed.player,after.player,'second three-letter request must not overwrite or partially fill either empty slot');
    await button(panel,'返回战斗').tap();
    await panel.waitFor({state:'hidden'});
    note('fresh low-rank battle aid creates three rendered player cards and rejects insufficient space without changing either occupied cards or progress');
    const cells = await page.evaluate(() => {
      const i=window.__XanaduZhaoyunInternals, m=i.DataMgr.instance().map, board=i.ContainersMgr.instance().bP(1,true);
      for (let y=0;y<m.Ga[0].length;y++) for(let x=0;x+2<m.Ga.length;x++){
        const cells=[0,1,2].map(dx=>({x:x+dx,y}));
        if(cells.every(c=>m.Ga[c.x][c.y]==='1_0' && !board.getItem(c.x,c.y))) return cells;
      }
      return null;
    });
    assert(cells,'first map provides three free adjacent player cells');
    const cdp=await context.newCDPSession(page);
    for (let j=0;j<3;j++) await dragLetter(page,cdp,after.player[j],cells[j]);
    await page.waitForFunction(cells=>{
      const i=window.__XanaduZhaoyunInternals, board=i.ContainersMgr.instance().bP(1,true);
      const ids=cells.map(c=>board.getItem(c.x,c.y)?.Zb);
      return ids[0]>=0 && ids.every(id=>id===ids[0]) && i.DataMgr.instance().player.mergedGenerals.includes(12);
    },cells,{timeout:10000});
    const merged=await disk(page);
    assert.deepEqual(merged._unlockedSpecialGenerals,originalSave._unlockedSpecialGenerals);
    assert.deepEqual(merged._xanaduManualRecruitIds,originalSave._xanaduManualRecruitIds);
    assert.equal(merged._curStar,originalSave._curStar);
    await page.reload(); await activate(page);
    assert.equal(byId((await catalog(page)).generals,12).recruitable,false);
    note('three real touch drags merge Sun Shangxiang on adjacent cells; new session keeps only legitimate merge history and no permanent eligibility');
  } finally { await context.close(); }
}

async function run(browser) {
  const first = await fresh(browser);
  await dates(browser,first);
  await battleAid(browser);
}

(async () => {
  const browser = await chromium.launch({executablePath,headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try {
    report.contentDigest = JSON.parse(fs.readFileSync(path.join(content,'../content-build.json'),'utf8')).contentDigest;
    report.browserVersion = browser.version();
    await run(browser);
    assert.deepEqual(report.errors, []); assert.deepEqual(report.missing, []); assert.deepEqual(report.remote, []);
    report.passed = true;
  } catch (error) {
    report.error = error.stack; process.exitCode = 1;
  } finally {
    await browser.close();
    fs.mkdirSync(evidence,{recursive:true});
    const basename = process.argv.includes('--baseline') ? 'save-editor-baseline.json' : 'save-editor.json';
    fs.writeFileSync(path.join(evidence,basename),JSON.stringify(report,null,2));
    console.log(JSON.stringify(report,null,2));
  }
})().catch(error => {console.error(error);process.exitCode=1;});
