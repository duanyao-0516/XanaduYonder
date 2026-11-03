/* Business changes for the pinned 4399 H5 release. Never emulate an ad or login SDK. */
(function () {
  'use strict';
  const I = window.__XanaduZhaoyunInternals;
  if (!I) throw new Error('Pinned game exports are missing');
  let startupGiftError=null;
  const ids = {
    Load:'nFCDlT3GRD-9N62vwVVE4Q', Main:'dKvUsPTsTBGGfiZxHMSqtg',
    Battle:'a1VsRozfQfKce35jblVR3w', GameOver:'36WnNn_bSKilkYpbnYn_9A',
    Shop:'NsqY3ju_Tc-HfHc8t5EVgA', Setting:'6DGqZNBLRhuPudJfulUNKw'
  };
  const scene = name => {
    const ctor = Laya.ClassUtils.getClass(ids[name]);
    if (!ctor) throw new Error('Pinned scene is missing: '+name);
    return ctor.prototype;
  };
  function wrap(object, key, after) {
    const original=object[key];
    if(typeof original!=='function') throw new Error('Pinned method is missing: '+key);
    object[key]=function(...args){const result=original.apply(this,args);after.call(this,...args);return result;};
  }
  function hide(node) {if(node){node.visible=false;node.mouseEnabled=false;node.offAll(Laya.Event.CLICK);}}
  function label(node, text, name='offlineLabel') {
    if(!node)return;
    let caption=node.getChildByName(name);
    if(!caption){caption=new Laya.Text();caption.name=name;caption.fontSize=21;caption.color='#ffffff';caption.bgColor='#624321';caption.align='center';caption.valign='middle';caption.mouseEnabled=false;node.addChild(caption);}
    caption.text=text;caption.width=Math.max(node.width,90);caption.height=30;caption.y=node.height-30;
  }
  function localButton(node,text){
    if(!node)return;
    for(let n=0;n<node.numChildren;n++){const child=node.getChildAt(n);if(child.name!=='offlineLabel')hide(child);}
    node.skin='';node.graphics.clear();node.graphics.drawRect(0,0,node.width,node.height,'#d7bb79','#533a21',3);
    label(node,text);const caption=node.getChildByName('offlineLabel');caption.y=0;caption.height=node.height;caption.bgColor='';caption.color='#382511';
  }
  function unavailable(success, fail) {if(typeof fail==='function')fail('Unavailable in the offline edition');}
  const noop=()=>{};

  // Select capabilities explicitly; the upstream editor fallback reports ads/shares as successful.
  I.PlatMgr.prototype.init=function(){
    this.t0=new I.BasePlatform();
    Object.assign(this.t0,{Nr:false,canShare:false,kr:false,Mr:false,_r:false,channelAppId:0,
      Dr:unavailable,share:unavailable,Fr:unavailable,Er:noop,Gr:noop,Yr:noop,Ur:noop,
      openAwemeUserProfile:noop,setClipboardData:noop,Hr:noop});
    this.t0.init();
  };
  I.PlatMgr.prototype.Dr=unavailable;
  I.PlatMgr.prototype.share=unavailable;
  I.PlatMgr.prototype.Er=noop; I.PlatMgr.prototype.i0=noop;
  I.PlatMgr.prototype.reportScene=noop;
  I.PlatMgr.prototype.Nr=()=>false;I.PlatMgr.prototype.MZ=()=>false;
  I.PolicyMgr.prototype.HZ=()=>({requirePrivacyAgreement:false,exitOnPrivacyReject:false,
    privacyDialogPolicyLinks:false,mainSceneAgeBadge:false,isShowRank:true});
  I.PolicyMgr.prototype.slt=()=> 'xanadu-offline';
  I.PolicyMgr.prototype.clt=()=>Promise.resolve();
  I.PolicyMgr.prototype.olt=()=>Promise.resolve(true);
  for(const method of ['mlt','wlt','dlt','Llt'])I.PolicyMgr.prototype[method]=noop;
  I.ApiMgr.prototype.request=function(_path,_data,callbacks){if(callbacks&&callbacks.fail)callbacks.fail('Offline game');};
  for(const method of ['k0','M0','P0','Xc','Hc','Yc','B0','track','D0'])I.ApiMgr.prototype[method]=noop;
  for(const method of Object.getOwnPropertyNames(I.AnalyticsMgr.prototype)){
    if(method!=='constructor'&&typeof I.AnalyticsMgr.prototype[method]==='function')I.AnalyticsMgr.prototype[method]=noop;
  }
  I.AdPointMgr.prototype.go=function(_point,_success,fail){unavailable(null,fail);};
  I.AdPointMgr.prototype.bind=noop;I.AdPointMgr.prototype.setIcon=(_point,node)=>hide(node);
  I.CdnConfigMgr.prototype.loadShareConfig=()=>Promise.resolve();
  I.CdnConfigMgr.prototype.shouldAutoShowNotice=()=>false;
  I.CdnConfigMgr.prototype.getNotice=()=> '离线玩法说明\n\n拖动文字合成士兵与武将，布阵保护阿斗。对手由本机 AI 控制。\n\n开局不限体力；结算领取普通奖励。商店使用游戏金币，每局各有一次铲子和推土机援助。武器、武将和段位按原规则成长。\n\n进度仅保存在本机。切到后台会暂停；返回大厅或关闭应用后，下次从主菜单开始。';
  I.GameMgr.prototype._tt=noop;I.GameMgr.prototype.xtt=noop;
  const disabledDialogs=new Set(['GMDialog','GetStaminaDialog','AuthorizeDialog','PrivacyPolicyDialog','SidebarDialog','AddDeskDialog','ShareLpDialog']);
  const openDialog=I.SceneMgr.prototype.uo;
  I.SceneMgr.prototype.uo=function(name,...args){if(disabledDialogs.has(name))return Promise.resolve(null);return openDialog.call(this,name,...args);};
  const openScene=I.SceneMgr.prototype.ao;
  I.SceneMgr.prototype.ao=function(name,...args){
    if(name==='RankScene'){showPersonalRank();return;}
    if(name==='SoldierRangeScene')return;
    return openScene.call(this,name,...args);
  };

  // Resource loading remains original; platform initialization and remote save/config are removed.
  const load=scene('Load'), loadAssets=load.Ilt;
  load.Ilt=function(){
    const preload=I.PreloadMgr.instance();
    preload.Z7=preload.Z7.filter(item=>!item.url.startsWith('data/agreement'));
    return loadAssets.call(this);
  };
  load.Flt=function(){
    this.blt=1;this.Plt=1;this.Rlt();this.Alt='准备本地进度';this.Blt=0;this.Elt();
    I.DataMgr.instance().init();I.DataMgr.instance().player.finalizeRankAfterLoad();this.onComplete();
  };
  load.onComplete=function(){
    Laya.timer.clearAll(this);I.GameMgr.instance().init();I.SeasonMgr.instance().ztt();
    I.SceneMgr.instance().ao('MainScene',true,null,()=>{
      if(startupGiftError)return; // Host sees ready:false + the verified-save error and can reload.
      I.SceneMgr.instance().Lo('LoadScene');document.getElementById('offline-loading')?.remove();
      window.__XanaduZhaoyunBridgeReady();
    });
  };

  // Capability gates move to day one. Original dates, daily resets, rank checks,
  // starter equipment initialization, fragment costs and upgrade rules remain active.
  if(I.WeaponMgr.Em!==3||I.SpecialGeneralMgr.GL!==3)throw new Error('Pinned day gates changed');
  I.WeaponMgr.Em=1;I.SpecialGeneralMgr.GL=1;
  const originalStarterGift=I.WeaponMgr.prototype.Im;
  I.WeaponMgr.prototype.Im=function(){
    const player=I.DataMgr.instance().player;
    if(player.weaponFree)return {ok:true};
    const current=player._data,ownSave=Object.getOwnPropertyDescriptor(player,'setData');
    let candidate;
    try{
      // Im is synchronous and only mutates PlayerData through fragment/new-ID
      // setters. Reuse its original selection while deferring those nested saves.
      player._data=JSON.parse(JSON.stringify(current));
      Object.defineProperty(player,'setData',{value:()=>true,writable:true,configurable:true});
      originalStarterGift.call(this);
      player._data._weaponFree=true;candidate=player._data;
    }finally{
      player._data=current;
      if(ownSave)Object.defineProperty(player,'setData',ownSave);else delete player.setData;
    }
    return window.__XanaduZhaoyunPersistPlayerData(player,candidate);
  };

  // player.round is persisted lifetime wins + losses; roundDay resets daily.
  // Rounds 1–21 keep the upstream first-day order, then map 3 joins the rotation.
  I.DataMgr.prototype.i5=function(){return [0,1,2,3][Math.floor(Math.max(0,this.player.round)/7)%4];};
  const avatarRules=Object.getOwnPropertyDescriptor(I.AvatarMgr.prototype,'X0');
  Object.defineProperty(I.AvatarMgr.prototype,'X0',{...avatarRules,get:function(){
    const rules=avatarRules.get.call(this);
    for(const id of [3,5])rules.set(id,{...rules.get(id),check:()=>true,desc:'开局可用',progress:()=>({j0:1,target:1})});
    return rules;
  }});

  // No energy gate, spending, recovery timer, or useless stamina product.
  I.StaminaMgr.prototype.init=noop;I.StaminaMgr.prototype.itt=()=>true;
  I.StaminaMgr.prototype.htt=noop;I.StaminaMgr.prototype.ntt=noop;I.StaminaMgr.prototype.ctt=noop;
  const dataInit=I.DataMgr.prototype.init;
  I.DataMgr.prototype.init=function(){dataInit.call(this);this.props.hr=[0,0,0,0];};
  const resetProps=I.DataMgr.prototype.v0;
  I.DataMgr.prototype.v0=function(){resetProps.call(this);this.props.hr=[0,0,0,0];};
  const propsPool=I.PropsMgr.prototype.OD;
  I.PropsMgr.prototype.OD=function(){return propsPool.call(this).filter(id=>id!==23);};
  const main=scene('Main');
  const originalMainOpen=main.onOpened;
  const toolsKey = 'xanadu.zhaoyun-adou.tools-visible.v1';
  let toolsVisible = false;
  try { toolsVisible = localStorage.getItem(toolsKey) === '1'; } catch (_) { /* session only */ }
  function syncToolButton(button) {
    button.visible = toolsVisible;
    button.mouseEnabled = toolsVisible;
  }
  // The menu is cached. Bind only while it is open, and discard unfinished gestures on exit.
  let unbindCoin = null;
  function bindCoin(menu) {
    unbindCoin?.();
    const coin = menu.goldTxt.parent;
    const events = Laya.Event;
    const owner = {};
    let timer, pressedId = null, startX, startY, taps = 0, firstTap = 0;
    function endPress() {
      clearTimeout(timer);
      pressedId = null;
    }
    function reset() { endPress(); taps = 0; }
    function toggle() {
      reset();
      const status = window.XanaduZhaoyun.status();
      if (!status.active || status.paused || status.scene !== 'MainScene') return;
      toolsVisible = !toolsVisible;
      try { localStorage.setItem(toolsKey, toolsVisible ? '1' : '0'); } catch (_) { /* session only */ }
      syncToolButton(menu.offlineSaveEditorButton);
      I.TipMgr.instance().showTip(toolsVisible ? '存档和援将已显示' : '存档和援将已隐藏');
    }
    coin.mouseEnabled = true;
    coin.on(events.MOUSE_DOWN, owner, event => {
      if (pressedId !== null || event.touches.length > 1) { reset(); return; }
      pressedId = event.touchId;
      startX = event.stageX;
      startY = event.stageY;
      timer = setTimeout(toggle, 800);
    });
    coin.on(events.MOUSE_DRAG, owner, event => {
      if (event.touchId === pressedId &&
          (Math.abs(event.stageX - startX) > 10 || Math.abs(event.stageY - startY) > 10)) reset();
    });
    coin.on(events.MOUSE_OUT, owner, () => { if (pressedId !== null) reset(); });
    coin.on(events.MOUSE_UP, owner, event => {
      // A completed hold already consumed the press. Its release must not start another tap sequence.
      if (event.touchId !== pressedId) return;
      endPress();
      if (event.nativeEvent?.type === 'touchcancel') { reset(); return; }
      const now = performance.now();
      if (!taps || now - firstTap > 850) { firstTap = now; taps = 0; }
      if (++taps === 3) toggle();
    });
    menu.on(events.MOUSE_DOWN, owner, event => {
      if (!coin.contains(event.target)) reset();
    });
    window.addEventListener('blur', reset);
    document.addEventListener('visibilitychange', reset);
    unbindCoin = () => {
      reset();
      coin.offAllCaller(owner);
      menu.offAllCaller(owner);
      window.removeEventListener('blur', reset);
      document.removeEventListener('visibilitychange', reset);
      unbindCoin = null;
    };
  }
  function addPanelButton(scene, parent, field, title, x, y, panel) {
    if (scene[field]) { syncToolButton(scene[field]); return; }
    const button = new Laya.Box();
    button.name = field;
    button.size(136, 88);
    button.pos(x, y);
    syncToolButton(button);
    button.graphics.drawRect(4, 12, 128, 63, '#ead9b4', '#624321', 3);
    const caption = new Laya.Text();
    caption.text = title;
    caption.fontSize = 30;
    caption.bold = true;
    caption.color = '#48321c';
    caption.align = 'center';
    caption.valign = 'middle';
    caption.size(button.width, button.height);
    caption.mouseEnabled = false;
    button.addChild(caption);
    parent.addChild(button);
    scene[field] = button;
    button.on(Laya.Event.CLICK, scene, () => panel.open());
    button.on(Laya.Event.UNDISPLAY, scene, () => panel.close());
  }
  main.onOpened=function(...args){
    const weapons=I.WeaponMgr.instance(),player=I.DataMgr.instance().player;
    if(weapons.Dm()&&!player.weaponFree){
      const result=weapons.Im();
      if(!result.ok){startupGiftError=result.error;return;}
    }
    startupGiftError=null;
    const result = originalMainOpen.apply(this,args);
    // The removed stamina meter left a slot beside Settings in this scaled header.
    addPanelButton(this, this.settingBtn.parent, 'offlineSaveEditorButton', '存档', 100, 102, window.XanaduSaveEditorUI);
    bindCoin(this);
    return result;
  };
  wrap(main, 'onClosed', () => { unbindCoin?.(); window.XanaduSaveEditorUI.close(); });

  main.jct=()=>Promise.resolve();main.fct=noop;main.Rn=showPersonalRank;
  function mainOfflineUI(){
    for(const key of ['dySidebarBtn','followBtn','addDeskBtn','addStaminaBtn','btnStaminaImg'])hide(this[key]);
    hide(this.staminaImg?.parent);hide(this.staminaTxt?.parent);
    if(this.rankBtn){this.rankBtn.visible=true;this.rankBtn.mouseEnabled=true;localButton(this.rankBtn,'个人\n段位');}
    localButton(this.noticeBtn,'玩法\n说明');
  }
  wrap(main,'onAwake',mainOfflineUI);wrap(main,'onOpened',mainOfflineUI);

  // Reward doubling is removed at both display and action boundaries.
  const over=scene('GameOver'), claim=over.xht;
  over.xht=function(event){return claim.call(this,event,false);};
  // A loss with unchanged stars uses the original minimum-rank sequence:
  // keep the stars on screen and proceed to the normal reward or close button.
  const lossStep = over.Xot, lossPhases = over.constructor.Yot;
  over.Xot = function (...args) {
    if (this.Nrt === lossPhases.Hot) {
      const rank = I.DataMgr.instance().rank;
      const current = rank.iL;
      if (current.id === rank.d1.id && current.level === rank.d1.level) this.Nrt = lossPhases.Wot;
    }
    return lossStep.apply(this, args);
  };
  wrap(over,'onAwake',function(){hide(this.getBtnAd);});
  wrap(over,'wot',function(){hide(this.getBtnAd);this.getBtn.y=this.Att?960:1090;});

  // All store items use the original per-item/per-level coin price. No wheel or ad route.
  const shop=scene('Shop'), shopOpen=shop.onOpened, buy=shop.mpt, tab=shop.cpt;
  function shopOfflineUI(){
    for(const key of ['lotteryLabel','lotteryLabelRect','lotteryBox','luckyBtn','redPoint'])hide(this[key]);
    hide(this.staminaImg?.parent);hide(this.staminaTxt?.parent);
    this.shopBox.visible=true;
  }
  shop.onOpened=function(pool){shopOpen.call(this,(pool||I.PropsMgr.instance().OD()).filter(id=>id!==23));shopOfflineUI.call(this);};
  shop.mpt=function(id,name,_ad,price,image){return buy.call(this,id,name,false,price,image);};
  shop.cpt=function(){tab.call(this,'shop');shopOfflineUI.call(this);};
  shop.upt=noop;shop.hpt=noop;shop.npt=noop;shop.rpt=noop;shop.Ept=noop;
  I.BorderEffect.addEffect=()=>Promise.resolve(null);
  I.EffectsMgr.by=['resources/img/shop/light1.png','resources/img/shop/light2.png'];
  I.PikeDefinitions.get(19).bF.mO='resources/img/gameObject/soldier/pikeEff2.png';
  I.FactoryMgr.prototype.weaponFragmentRewardCreateFunc=function(){
    const tile=new Laya.Image('resources/img/weaponBag/weaponBg1_1.png');tile.size(54,54);
    const icon=new Laya.Image();icon.name='icon';icon.size(54,54);icon.anchor(.5,.5);icon.pos(27,27);tile.addChild(icon);
    const count=new Laya.Text();count.name='num';count.fontSize=35;count.size(54,54);count.pos(0,45);count.align='center';count.valign='middle';count.stroke=5;tile.addChild(count);return tile;
  };

  // The original map flags enforce one use of each aid per battle. No global 'ad success'.
  const battle=scene('Battle');
  function addBattleSpeedButton(scene) {
    if (scene.offlineSpeedButton) return;
    const button = new Laya.Box();
    button.name = 'offlineSpeedButton';
    // The rightmost active AI prop ends at x=494; the passive row begins at y=118.
    button.pos(508, 4);
    button.size(116, 108);
    button.zIndex = scene.round.zIndex;
    button.mouseEnabled = true;
    button.graphics.drawRect(3, 8, 110, 92, '#ead9b4', '#624321', 3);
    const caption = new Laya.Text();
    caption.name = 'speedCaption';
    caption.text = '倍速\n1×';
    caption.fontSize = 28;
    caption.bold = true;
    caption.color = '#48321c';
    caption.align = 'center';
    caption.valign = 'middle';
    caption.size(button.width, button.height);
    caption.mouseEnabled = false;
    button.addChild(caption);
    // The parent box also listens for pointer input to pick up soldiers and props.
    const stop = event => event.stopPropagation();
    for (const type of [Laya.Event.MOUSE_DOWN, Laya.Event.MOUSE_MOVE, Laya.Event.MOUSE_UP]) {
      button.on(type, scene, stop);
    }
    button.on(Laya.Event.CLICK, scene, event => {
      stop(event);
      caption.text = '倍速\n' + window.XanaduBattleSpeed.toggle() + '×';
    });
    scene.xBtn.parent.addChild(button);
    scene.offlineSpeedButton = button;
  }
  wrap(battle, 'onAwake', function () {
    addPanelButton(this, this.xBtn.parent, 'offlineGeneralAidButton', '援将', 112, 12, window.XanaduBattleAidUI);
    this.offlineGeneralAidButton.zIndex = this.round.zIndex;
    addBattleSpeedButton(this);
  });
  wrap(battle, 'onOpened', function () {
    syncToolButton(this.offlineGeneralAidButton);
    window.XanaduBattleSpeed.begin();
    this.offlineSpeedButton.getChildByName('speedCaption').text = '倍速\n1×';
  });
  wrap(battle, 'onClosed', () => {
    try { window.XanaduBattleAidUI.close(); }
    finally { window.XanaduBattleSpeed.end(); }
  });
  battle.Lnt=function(){
    const game=this.Gw;
    if(game.map.Na&&!game.map.$a){
      this.WD.length=0;const slots=I.ContainersMgr.instance().bP(3).wP;
      for(let n=0;n<slots.length;n++)if(I.SlotsMgr.instance().vB(true,n))this.WD.push(n);
      if(!this.WD.length){I.TipMgr.instance().showTip('当前刷新栏无空格');return;}
      const count=Math.min(2,this.WD.length),point=new Laya.Point(this.shovelAd.width/2,this.shovelAd.height/2);
      this.shovelAd.localToGlobal(point);
      for(let n=0;n<count;n++)this.Srt(point.x,point.y,this.WD[n]);
      this.shovelAd.visible=false;game.map.Na=false;game.map.$a=true;
      I.TipMgr.instance().showTip('本局援助：获得'+count+'把铲子');
    }
    if(game.map.qa&&!game.map.za){
      const road=game.map.Wa;if(!road||road.length<2)return;
      const cell=road[road.length-2];if(!cell)return;
      const start=new Laya.Point(this.shovelAd.width/2,this.shovelAd.height/2);this.shovelAd.localToGlobal(start);
      const end=new Laya.Point(cell.x*game.map.yh+game.map.yh/2,cell.y*game.map.gridHei+game.map.gridHei/2);this.map.localToGlobal(end);
      // Reserve before animation, preventing a second tap while the aid is in flight.
      this.shovelAd.visible=false;game.map.qa=false;game.map.za=true;
      I.EffectsMgr.instance().ap(start.x,start.y,end.x,end.y,500,()=>{if(!this.urt())I.PropsMgr.instance().FD(true,1).QP(null);},'#ffffff','resources/img/battleUI/ad/bulldozer.png');
      I.TipMgr.instance().showTip('本局援助：推土机出动');
    }
  };
  wrap(battle,'Vnt',function(){hide(this.shovelAd?.getChildByName('adImg'));label(this.shovelAd,'本局援助');});
  wrap(scene('Setting'),'onOpened',function(){hide(this.viewTxt);hide(this.uidTxt);hide(this.copyBtn);});

  // An actual local progression screen, with one storage commit for reward and receipt.
  function showPersonalRank(){
    document.getElementById('xanadu-personal-rank')?.remove();
    const game=I.DataMgr.instance(),player=game.player;
    const prior=I.RankDataMgr.instance().R0(player.lastStar),entry=game.rank.I0.get(prior.rank);
    const panel=document.createElement('div');panel.id='xanadu-personal-rank';panel.className='xanadu-local-panel';
    const box=document.createElement('section'),title=document.createElement('h2'),description=document.createElement('p'),message=document.createElement('p');
    title.textContent='个人段位';description.textContent='当前段位：'+game.rank.iL.rank+' · '+game.rank.iL.level+'星。对手为本机 AI，进度保存在本机。';
    const reward=document.createElement('button');reward.type='button';
    function state(){reward.disabled=player.isGetLastRankReward!==0;reward.textContent=reward.disabled?'每日段位奖励已领取':'领取每日段位奖励 · '+entry.reward+'金币';}
    state();reward.addEventListener('click',()=>{
      if(player.isGetLastRankReward!==0)return;
      const next={...player._data,_gold:player.gold+entry.reward,_isGetLastRankReward:1,_saveTime:Date.now()};
      const result=window.__XanaduZhaoyunPersistPlayerData(player,next);
      if(!result.ok){message.textContent='保存失败，奖励尚未领取：'+result.error;return;}
      message.textContent='已领取'+entry.reward+'金币';const main=I.SceneMgr.instance().co('MainScene');if(main&&main.goldTxt)main.goldTxt.text=player.gold.toString();state();
    });
    const close=document.createElement('button');close.type='button';close.textContent='返回游戏';close.addEventListener('click',()=>panel.remove());
    box.append(title,description,reward,message,close);panel.append(box);document.body.append(panel);
  }
})();
