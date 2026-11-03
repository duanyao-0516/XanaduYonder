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
const evidence = path.join(path.dirname(content), 'browser-evidence');
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

async function scenePoint(page, sceneName, field) {
  const point = await page.evaluate(({sceneName,field}) => {
    const scene = window.__XanaduZhaoyunInternals.SceneMgr.instance().co(sceneName);
    const node = field === 'coin' ? scene.goldTxt.parent.getChildAt(0) : scene[field];
    if (!node?.parent || !node.visible || (field.startsWith('offline') && !node.mouseEnabled)) return null;
    const p = node.localToGlobal(new Laya.Point(node.width / 2, node.height / 2));
    const rect = document.querySelector('canvas').getBoundingClientRect();
    return {x:rect.left + p.x * rect.width / Laya.stage.width,y:rect.top + p.y * rect.height / Laya.stage.height};
  }, {sceneName,field});
  assert(point, sceneName + '.' + field + ' must be a visible, enabled game entry');
  const {width,height} = page.viewportSize();
  assert(point.x > 0 && point.x < width && point.y > 0 && point.y < height, 'offscreen ' + field + ': ' + JSON.stringify(point));
  report.touches.push({width,height,field,...point});
  return point;
}

async function touchScene(page, sceneName, field) {
  const point = await scenePoint(page, sceneName, field);
  await page.touchscreen.tap(point.x, point.y);
}

const touchMain = (page,field) => touchScene(page,'MainScene',field);

const saveVisible = page => page.evaluate(() => {
  const button = window.__XanaduZhaoyunInternals.SceneMgr.instance().co('MainScene').offlineSaveEditorButton;
  if (button.visible !== button.mouseEnabled) throw new Error('button visibility and input disagree');
  return button.visible;
});

async function tripleTapCoin(page) {
  const point = await scenePoint(page,'MainScene','coin');
  for (let i=0; i<3; ++i) await page.touchscreen.tap(point.x,point.y);
}

async function checkCoinGestures(page) {
  assert.equal(await saveVisible(page),false);
  const coin = await scenePoint(page,'MainScene','coin');
  const tap = () => page.touchscreen.tap(coin.x,coin.y);
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{...coin,id:1}]});
  await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:coin.x+28,y:coin.y,id:1}]});
  await page.waitForTimeout(950);
  await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  assert.equal(await saveVisible(page),false,'dragging across the coin cannot trigger a hold');
  await tap(); await tap();
  assert.equal(await saveVisible(page),false);
  await page.waitForTimeout(950);
  await tap();
  assert.equal(await saveVisible(page),false,'a late third tap cannot toggle tools');
  await tap(); await tap();
  assert.equal(await saveVisible(page),true);
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{...coin,id:1}]});
  await page.waitForFunction(() => !window.__XanaduZhaoyunInternals.SceneMgr.instance().co('MainScene').offlineSaveEditorButton.visible,null,{timeout:3500});
  await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  await tap(); await tap();
  assert.equal(await saveVisible(page),false,'lifting the long press must not count as another tap');
  await tap();
  assert.equal(await saveVisible(page),true);
  await cdp.detach();
  note('coin icon toggles both ways by real long press or three fast taps; short and expired sequences stay hidden');
}

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

async function checkItemDetails(row, item) {
  const text = (await row.innerText()).replace(/\s+/g, ' ');
  for (const detail of [item.typeLabel, item.description]) {
    assert(detail && text.includes(detail.replace(/\s+/g, ' ')), 'choice lacks type or description for ' + item.name);
  }
  const quality = row.locator('.panel-option-quality');
  if (!item.rarityName) {
    assert.equal(await quality.count(), 0, 'no invented general rarity');
    return;
  }
  assert.equal(await quality.innerText(), item.rarityName);
  const rgb = item.rarityColor.slice(1).match(/../g).map(component => parseInt(component,16));
  assert.equal(await quality.evaluate(el => getComputedStyle(el).color), 'rgb(' + rgb.join(', ') + ')');
}

async function choose(panel, label, value, expectedCount, item) {
  const trigger = panel.getByRole('combobox', {name:label, exact:true});
  await trigger.tap();
  const list = panel.getByRole('listbox', {name:label, exact:true});
  await list.waitFor({state:'visible'});
  // Android's native picker used to vanish when its separate window paused the host.
  await panel.page().waitForTimeout(200);
  assert(await list.isVisible(), label + ' should remain open until a choice is made');
  assert.equal(await panel.page().evaluate(() => window.XanaduZhaoyun.status().active), true);
  if (label === '选择道具') {
    assert.equal(await panel.getByRole('combobox',{name:'道具等级'}).count(),0);
    const promoted = list.getByRole('option').filter({hasText:'升职令'});
    assert.deepEqual(await promoted.locator('.panel-option-quality').allTextContents(),
      ['卓越 · 1级','史诗 · 2级','传说 · 3级']);
    assert.deepEqual(await promoted.locator('.panel-option-description').allTextContents(),
      ['刷出的兵有5%概率升到2级','刷出的兵有10%概率升到2级','刷出的兵有15%概率升到2级']);
    const ownedLevel = byId((await catalog(panel.page())).props,22).level;
    const ownedRow = promoted.filter({hasText:'已装备'});
    assert.equal(await ownedRow.count(),ownedLevel ? 1 : 0);
    if (ownedLevel) assert.equal(await ownedRow.getAttribute('value'),'22:' + ownedLevel);
    const singles = list.getByRole('option').filter({hasNotText:'升职令'});
    assert.equal(await singles.count(),21);
    assert((await singles.locator('.panel-option-quality').allTextContents()).every(text=>!text.includes('级')));
  }
  if (expectedCount !== undefined) {
    assert.equal(await list.getByRole('option').count(),expectedCount);
    if (item) {
      const typedRows = await list.getByRole('option').evaluateAll(rows => rows.filter(row =>
        [...row.querySelectorAll('.panel-option-type')].some(label => label.textContent && label.textContent !== '已装备')).length);
      assert.equal(typedRows,expectedCount);
      assert.equal(await list.locator('.panel-option-description').count(),expectedCount);
      assert.equal(await list.locator('.panel-option-quality').count(),item.rarityName ? expectedCount : 0);
    }
  }
  if (label === '选择武器' && expectedCount === 46) {
    await list.scrollIntoViewIfNeeded();
    const rect = await list.boundingBox(), x = rect.x + rect.width / 2;
    const cdp = await panel.page().context().newCDPSession(panel.page());
    try {
      await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x,y:rect.y+rect.height*0.75}]});
      for (let step=1;step<=6;step++) {
        await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:rect.y+rect.height*(0.75-step*0.08)}]});
        await panel.page().waitForTimeout(30);
      }
      await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
    } finally { await cdp.detach(); }
    assert(await list.evaluate(el=>el.scrollHeight>el.clientHeight && el.scrollTop>0),
      'long weapon list must scroll by touch on the small viewport');
  }
  const option = list.locator('button[value="' + value + '"]');
  const text = await option.innerText();
  if (label === '替换道具') assert(!/ · [123]级/.test(await option.innerText()));
  if (item) await checkItemDetails(option,item);
  assert(await list.evaluate(el => el.scrollWidth <= el.clientWidth + 1),'no horizontal scrolling');
  await option.tap();
  await list.waitFor({state:'hidden'});
  assert((await trigger.innerText()).includes(text));
  if (item) await checkItemDetails(trigger,item);
}

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

async function checkRank(page, id, stars) {
  const actual = await page.evaluate(({id,stars,key})=>{
    const i=window.__XanaduZhaoyunInternals, game=i.DataMgr.instance();
    const rank=i.RankDataMgr.instance(), main=i.SceneMgr.instance().co('MainScene');
    const points=rank.C0(id,stars), expected=rank.R0(points);
    const ordinary=[main.star0,main.star1,main.star2,main.star3,main.star4];
    return {points,disk:JSON.parse(localStorage.getItem(key))._curStar,memory:game.player.curStar,
      expected,live:game.rank.iL,name:game.rank.I0.get(expected.rank).rank,caption:main.rankTxt.text,
      ordinaryVisible:ordinary.map(s=>s.visible),goldStars:ordinary.filter(s=>s.skin.endsWith('/star4.png')).length,
      emperorVisible:main.star5.visible,emperorText:main.starNum.text};
  },{id,stars,key});
  assert.equal(actual.disk,actual.points);assert.equal(actual.memory,actual.points);
  assert.equal(actual.live.id,actual.expected.rank);assert.equal(actual.live.level,stars);
  assert.equal(actual.caption,actual.name,'the main menu must refresh immediately');
  assert(actual.ordinaryVisible.every(v=>v===(id!==50)));
  assert.equal(actual.emperorVisible,id===50);
  if (id===50) assert.equal(actual.emperorText,'x'+stars);
  else assert.equal(actual.goldStars,stars);
  const cat=await catalog(page);
  assert.equal(cat.rankId,id);assert.equal(cat.rankStars,stars);
  return actual.name;
}

async function editLegacyRank(page) {
  const initial=await disk(page), panel=await openEditor(page);
  const ranks=await page.evaluate(()=>{
    const rows=[...window.__XanaduZhaoyunInternals.DataMgr.instance().rank.I0.values()];
    const firstByName=new Map();
    for (const row of rows) if (!firstByName.has(row.rank)) firstByName.set(row.rank,{id:row.id,name:row.rank});
    return [...firstByName.values()];
  });
  assert.equal(ranks.length,51);
  const cat=await catalog(page);
  assert.deepEqual(cat.ranks.map(({id,name})=>({id,name})),ranks);
  await checkRank(page,50,103);
  const stars=field(panel,'段内星数');
  const combo=panel.getByRole('combobox',{name:'个人段位',exact:true});
  assert((await combo.innerText()).includes(ranks[50].name));assert.equal(await stars.inputValue(),'103');
  await combo.tap();
  const list=panel.getByRole('listbox',{name:'个人段位',exact:true});
  const options=await list.getByRole('option').evaluateAll(rows=>rows.map(r=>({id:Number(r.value),label:r.innerText})));
  assert.deepEqual(options.map(row=>row.id),ranks.map(r=>r.id));
  assert(options.every((row,n)=>row.label.includes(ranks[n].name)));
  await list.getByRole('option',{selected:true}).tap();
  await choose(panel,'个人段位','7',51);assert.equal(await stars.inputValue(),'1');
  await stars.fill('4');
  await choose(panel,'个人段位','50',51);assert.equal(await stars.inputValue(),'103');
  assert.deepEqual(await disk(page),initial,'previewing an edit must not write the player');
  await rejected(page,{kind:'rank',id:7,stars:6});
  await rejected(page,{kind:'rank',id:7,stars:0});
  await rejected(page,{kind:'rank',id:7,stars:4},true);

  const immutable = data => {
    const state=progress(data);delete state._curStar;
    return {...state,gold:data._gold,keepSkills:data._xanaduKeepSkills,streak:data._winStreak,
      season:data._seasonId,finalRanks:data._seasonFinalRanks};
  };
  // Leave unsaved settings in the form. The rank button commits only the rank command.
  await field(panel,'金币').fill('99999');
  await field(panel,'跨天保留技能道具').tap();
  for (const [id,level] of [[7,4],[7,2],[50,101]]) {
    await choose(panel,'个人段位',String(id),51);
    await stars.fill(String(level));await submit(page,panel,'保存段位');
    await checkRank(page,id,level);
    assert.deepEqual(immutable(await disk(page)),immutable(initial));
  }
  await closeEditor(panel);
  await touchMain(page,'rankBtn');
  const rankPanel=page.locator('#xanadu-personal-rank');
  assert((await rankPanel.innerText()).includes('大帝 · 101星'));
  await rankPanel.getByRole('button',{name:'返回游戏'}).tap();
  await page.reload();await activate(page);
  await checkRank(page,50,101);assert.deepEqual(immutable(await disk(page)),immutable(initial));
  note('legacy Emperor 103 stars maps to one of 51 original rank names; previews, promotion, demotion and atomic saves refresh the real menu and reload without changing gold, records or yesterday’s reward');
}

async function rankUnlocks(browser) {
  const {context,page}=await launchGame(browser,320,568);
  try {
    const unlocked=()=>page.evaluate(()=>{
      const p=window.__XanaduZhaoyunInternals.DataMgr.instance().player;
      return [p.openProps,p.isAvatarUnlocked(11),p.isAvatarUnlocked(6)];
    });
    assert.deepEqual(await unlocked(),[false,false,false]);
    const steps=[
      [0,5,[false,false,false]], [1,1,[true,false,false]],
      [9,5,[true,false,false]], [10,1,[true,true,false]],
      [49,5,[true,true,false]], [50,1,[true,true,true]], [0,1,[true,true,true]]
    ];
    for (const [id,stars,expected] of steps) {
      checkOk(await apply(page,{kind:'rank',id,stars}));
      assert.deepEqual(await unlocked(),expected,'permanent unlocks after rank '+id);
    }
    await page.reload();await activate(page);
    await checkRank(page,0,1);assert.deepEqual(await unlocked(),[true,true,true]);
    note('rank-granted shop access and the two rank avatars unlock at 军士.二, 少将 and 大帝 and survive demotion and reload');
  } finally { await context.close(); }
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
  await choose(panel,'选择武器',String(sample.id),46,sample);
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
  assert(rows.every(p=>p.level===0));
  const original = await page.evaluate(() => {
    const rows = [];
    window.__XanaduZhaoyunInternals.DataMgr.instance().props.Fn.forEach((prop,id) => {
      if (![0,1,23].includes(id)) rows.push({id,levels:prop.Yn?.length || 1});
    });
    return rows;
  });
  assert.equal(original.filter(item=>item.levels===1).length,21);
  assert.deepEqual(original.filter(item=>item.levels>1),[{id:22,levels:3}]);
  assert.deepEqual(rows.map(p=>({id:p.id,levels:p.maxLevel})),original);
  report.propLevels={single:21,multiple:[{id:22,name:byId(rows,22).name,levels:3}]};
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
  await choose(panel,'选择道具',String(c.id),24,c);
  assert.equal(await panel.getByRole('combobox',{name:'道具等级'}).count(),0,'single-level skills must hide the level picker');
  await submit(page,panel,'保存道具');
  assert.match(await panel.getByRole('status').innerText(),/替换/);
  assert.equal(byId((await catalog(page)).props,c.id).level,0);
  await choose(panel,'替换道具',String(a.id),undefined,a);
  await choose(panel,'选择道具',String(active[3].id));
  await choose(panel,'选择道具',String(c.id),24,c);
  assert.match(await panel.getByRole('combobox',{name:'替换道具'}).innerText(),/请选择要替换的道具/);
  await choose(panel,'替换道具',String(a.id),undefined,a);
  await submit(page,panel,'保存道具');
  assert.equal(await panel.getByRole('status').innerText(),'已装备' + c.name + '。');
  assert(!(await panel.getByRole('region',{name:'道具'}).innerText()).includes('1 级'));
  const promotion=byId(rows,22);
  const preview = async n => {
    const detail = promotion.levelDetails[n-1];
    await choose(panel,'选择道具','22:' + n,24,{...promotion,...detail,rarityName:detail.rarityName + ' · ' + n + '级'});
  };
  for (const n of [1,2,3]) {
    await preview(n);
    assert.equal(await panel.locator('.panel-option-description:visible').count(),1,'one selected effect preview');
    assert.equal(byId((await catalog(page)).props,22).level,0,'preview must not equip the item');
  }
  await choose(panel,'替换道具',String(passive[0].id),undefined,passive[0]);
  await preview(2);
  assert((await panel.getByRole('combobox',{name:'替换道具'}).innerText()).includes(passive[0].name));
  await submit(page,panel,'保存道具');
  assert.equal(byId((await catalog(page)).props,22).level,2);
  await preview(3);
  await submit(page,panel,'保存道具');
  assert.equal(byId((await catalog(page)).props,22).level,3);
  assert.equal(await panel.getByRole('status').innerText(),'已装备升职令。');
  await choose(panel,'选择道具',String(c.id),24,c);
  await submit(page,panel,'保存道具');
  assert.equal(byId((await catalog(page)).props,c.id).level,1,'a single-level skill cannot inherit level 3');
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
  await choose(panel,'选择武将','2',15,byId(initial.generals,2));
  assert(await button(panel,'授予招募资格').isDisabled(), 'ordinary Ma Chao is already available');
  const before = await disk(page);
  checkOk(await apply(page,{kind:'general',id:2}));
  assert.deepEqual((await disk(page))._xanaduManualRecruitIds,before._xanaduManualRecruitIds);
  await rejected(page,{kind:'general',id:12},true);
  for (const id of [12,13,14]) {
    await choose(panel,'选择武将',String(id),15,byId(initial.generals,id));
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
  assert.equal(await page.evaluate(()=>{
    const button=window.__XanaduZhaoyunInternals.SceneMgr.instance().co('BattleScene').offlineGeneralAidButton;
    return button.visible || button.mouseEnabled;
  }),false,'hiding the tools on the menu also hides battle aid');
  report.pools = {originalPlayer:pools.basePlayer.length,player:pools.player.length,ai:pools.ai.length,aiRank:pools.aiRank,specialCharacters:added};
  note('real battle starts by touch, only the player pool adds seven special letters; editor is absent during battle');
}

async function fresh(browser) {
  const {context,page} = await launchGame(browser,320,568);
  try {
    await checkCoinGestures(page);
    let panel = await openEditor(page);
    await closeEditor(panel);
    note('fresh 320×568 opens and closes the menu editor by touch');
    panel = await openEditor(page);
    const initial = await catalog(page), first = await disk(page);
    assert.equal(initial.keepSkills,true);
    const saveRank=button(panel,'保存段位');
    assert(await saveRank.isVisible());assert.equal(await field(panel,'段内星数').inputValue(),'1');
    if (await saveRank.isEnabled()) await submit(page,panel,'保存段位');
    assert.equal((await disk(page))._curStar,first._curStar,'unchanged starting rank must preserve raw zero');
    await choose(panel,'个人段位','1',51);
    await field(panel,'段内星数').fill('4');
    await saveRank.scrollIntoViewIfNeeded();
    fs.mkdirSync(evidence,{recursive:true});
    await page.screenshot({path:path.join(evidence,'save-rank-320.png')});
    await field(panel,'金币').fill('718');
    await submit(page,panel,'保存设置');
    assert.equal((await disk(page))._gold,718);
    assert.equal((await disk(page))._curStar,first._curStar,'saving settings cannot apply the selected rank');
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
    assert.equal(await saveVisible(page),true,'tool visibility is remembered independently of player progress');
    await tripleTapCoin(page);
    assert.equal(await saveVisible(page),false);
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
  const old = {...first,_openTime:then,_saveTime:then,_registerTime:then,_curStar:353,_win:8,_lose:3,_winDay:2,_loseDay:1,
    _isGetLastRankReward:1,_lastStar:-1,_props:[[2,1,then],[22,3,then],11,[12,1]]};
  delete old._xanaduManualRecruitIds; delete old._xanaduKeepSkills;
  report.fixtures.push({kind:'previous calendar day full save, absent editor fields',props:old._props,winDay:2,loseDay:1,rewardReceipt:1});
  const game = await launchGame(browser,360,640,old);
  let today;
  try {
    const {page} = game;
    await tripleTapCoin(page);
    let panel = await openEditor(page);
    const cat = await catalog(page), loaded = await disk(page);
    assert(cat.keepSkills); assert.equal(cat.gold,old._gold);
    assert.equal(byId(cat.props,2).level,1); assert.equal(byId(cat.props,22).level,3);
    assert.equal(byId(cat.props,11).level,1); assert.equal(byId(cat.props,12).level,1);
    assert(loaded._props.every(row=>Array.isArray(row) && row.length===3 && Number.isFinite(row[2])));
    assert.equal(loaded._winDay,0); assert.equal(loaded._loseDay,0); assert.equal(loaded._isGetLastRankReward,0);
    assert.equal(loaded._curStar,old._curStar); assert.equal(loaded._lastStar,loaded._curStar);
    assert.equal(loaded._win,old._win); assert.equal(loaded._lose,old._lose);
    await button(panel,'道具').tap();
    const promotion = byId(cat.props,22), beforeProps = (await disk(page))._props;
    await choose(panel,'选择道具','22:1',24,{...promotion,...promotion.levelDetails[0],rarityName:'卓越 · 1级'});
    await closeEditor(panel);
    assert.deepEqual((await disk(page))._props,beforeProps,'closing a preview cannot change a saved level 3');
    panel = await openEditor(page);
    await button(panel,'道具').tap();
    await choose(panel,'选择道具','11',24,byId(cat.props,11));
    await choose(panel,'选择道具','22:3',24,{...promotion,...promotion.levelDetails[2],rarityName:'传说 · 3级'});
    assert.equal(byId((await catalog(page)).props,22).level,3);
    await button(panel,'设置').tap();
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
    await editLegacyRank(page);
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
  const general = await page.evaluate(id=>window.XanaduSaveEditorData.generals().find(item=>item.id===id),id);
  await choose(panel,'选择武将',String(id),15,general);
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
    await tripleTapCoin(page);
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
  await rankUnlocks(browser);
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
