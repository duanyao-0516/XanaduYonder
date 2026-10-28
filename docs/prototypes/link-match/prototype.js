(() => {
  'use strict';
  // A presentation model only: no solver, timer, random deal or persistence.
  const fruits = [
    ['strawberry','草莓'],['red-apple','红苹果'],['tangerine','橘子'],['lemon','柠檬'],
    ['banana','香蕉'],['watermelon','西瓜'],['grapes','葡萄'],['cherries','樱桃'],
    ['peach','桃子'],['pear','梨'],['pineapple','菠萝'],['avocado','牛油果']
  ];
  const levels = [
    { id:1, columns:4, rows:6, kinds:6, seconds:90 },
    { id:2, columns:6, rows:6, kinds:9, seconds:120 },
    { id:3, columns:6, rows:8, kinds:12, seconds:150 }
  ];
  const params = new URLSearchParams(location.search);
  const state = { view:'entry', mode:'relaxed', level:2, profile:'resume', overlay:null, saveError:false, selected:[], showPath:true, variant:0, isResume:true, sound:true, haptics:true };
  const screen = document.querySelector('.screen');
  const board = document.getElementById('board');
  const area = document.querySelector('.board-area');
  const overlay = document.getElementById('overlay');
  const modalContent = document.getElementById('modal-content');
  const modal = document.querySelector('.modal');
  const previewState = document.getElementById('preview-state');
  const previewSize = document.getElementById('preview-size');
  const previewProfile = document.getElementById('preview-profile');
  const previewLevel = document.getElementById('preview-level');
  let fixture = [];
  let previousFocus = null;
  const level = () => levels.find(item => item.id === state.level);
  const total = () => level().columns * level().rows;
  const number = id => String(id).padStart(2,'0');
  const modeName = () => state.mode === 'relaxed' ? '轻松' : '挑战';
  const setText = (id,value) => { document.getElementById(id).textContent = value; };
  const fruitImage = (index,alt='') => `<img src="assets/${fruits[index][0]}.png" alt="${alt}" draggable="false">`;
  const endpoints = () => [level().columns, level().columns * 4];
  const clockText = seconds => `${String(Math.floor(seconds / 60)).padStart(2,'0')}:${String(seconds % 60).padStart(2,'0')}`;

  // Uses the same normalized Home/Book/Settings geometry as the Sokoban prototype.
  function installIcons(root=document) {
    const path = points => `<polyline points="${points.map(point=>point.join(',')).join(' ')}"/>`;
    const paths = {
      Home:path([[-.42,.01],[0,.4],[.42,.01]])+path([[-.3,.09],[-.3,-.36],[-.11,-.36],[-.11,-.1],[.11,-.1],[.11,-.36],[.3,-.36],[.3,.09]]),
      Book:path([[0,-.32],[-.18,-.24],[-.42,-.24],[-.42,.34],[-.18,.34],[0,.26],[.18,.34],[.42,.34],[.42,-.24],[.18,-.24],[0,-.32],[0,.26]])+[0,1].map(i=>path([[-.3,.16-i*.16],[-.13,.12-i*.16]])+path([[.13,.12-i*.16],[.3,.16-i*.16]])).join(''),
      Pause:path([[-.16,.3],[-.16,-.3]])+path([[.16,.3],[.16,-.3]]),
      Shuffle:path([[-.4,.22],[-.2,.22],[.23,-.22],[.4,-.22]])+path([[-.4,-.22],[-.2,-.22],[.23,.22],[.4,.22]])+path([[.25,.36],[.4,.22],[.25,.08]])+path([[.25,-.08],[.4,-.22],[.25,-.36]]),
      Hint:'<path d="M-.16,-.25 L.16,-.25 M-.12,-.36 L.12,-.36 M-.18,-.13 C-.18,-.02 -.32,.02 -.32,.2 A.32,.32 0 0,0 .32,.2 C.32,.02 .18,-.02 .18,-.13 Z"/>'
    };
    const gear = Array.from({length:64},(_,i)=>{const a=i*Math.PI*2/64,r=i%8<2||i%8>5?.35:.47;return `${Math.cos(a)*r},${Math.sin(a)*r}`;}).join(' L');
    paths.Settings=`<path d="M${gear} Z M.17,0 A.17,.17 0 1,0 -.17,0 A.17,.17 0 1,0 .17,0 Z" fill="currentColor" fill-rule="evenodd" stroke="none"/>`;
    root.querySelectorAll('[data-icon]').forEach(node=>{node.innerHTML=`<svg viewBox="-.5 -.5 1 1" aria-hidden="true" focusable="false"><g transform="scale(1,-1)" fill="none" stroke="currentColor" stroke-width=".07" stroke-linecap="round" stroke-linejoin="round">${paths[node.dataset.icon] || ''}</g></svg>`;});
  }

  function makeFixture() {
    const current=level();
    const tiles=Array.from({length:current.columns*current.rows},(_,i)=>i%current.kinds);
    // Fixed permutations retain exactly four copies of every fruit.
    let seed=1709+state.level*37+state.variant*97;
    for(let i=tiles.length-1;i>0;i--){seed=(seed*1664525+1013904223)>>>0;const j=seed%(i+1);[tiles[i],tiles[j]]=[tiles[j],tiles[i]];}
    const reserved=[];
    for(const target of endpoints()){
      const source=tiles.findIndex((value,index)=>value===0&&!reserved.includes(index));
      [tiles[target],tiles[source]]=[tiles[source],tiles[target]];reserved.push(target);
    }
    return tiles;
  }

  function renderBoard() {
    fixture=makeFixture();
    if(state.showPath)state.selected=endpoints();
    board.style.gridTemplateColumns=`repeat(${level().columns},1fr)`;
    board.style.gridTemplateRows=`repeat(${level().rows},1fr)`;
    board.innerHTML=fixture.map((fruit,index)=>`<button class="fruit-slot${state.selected.includes(index)?' selected':''}" data-tile="${index}" aria-label="第${Math.floor(index/level().columns)+1}行第${index%level().columns+1}列，${fruits[fruit][1]}" aria-pressed="${state.selected.includes(index)}">${fruitImage(fruit)}</button>`).join('');
    board.setAttribute('aria-label',`${level().columns}列${level().rows}行，${total()}张水果，${level().kinds}种图案，每种4张；固定设计样例`);
    requestAnimationFrame(layoutBoard);
  }

  function layoutBoard() {
    if(state.view!=='game'||!area.clientWidth||!area.clientHeight)return;
    const padding=12;
    const slot=Math.min((area.clientWidth-padding*2)/level().columns,(area.clientHeight-padding*2)/level().rows);
    const width=slot*level().columns,height=slot*level().rows;
    board.style.width=`${width}px`;board.style.height=`${height}px`;
    board.dataset.slot=slot.toFixed(2);
    const left=(area.clientWidth-width)/2,top=(area.clientHeight-height)/2;
    const y1=top+slot*1.5,y2=top+slot*4.5,x=left+slot*.5,rail=left-8;
    const d=state.showPath?`M ${x} ${y1} H ${rail} V ${y2} H ${x}`:'';
    document.querySelectorAll('.path-layer path').forEach(path=>path.setAttribute('d',d));
    document.querySelector('.path-layer').setAttribute('viewBox',`0 0 ${area.clientWidth} ${area.clientHeight}`);
    screen.dataset.slot=slot.toFixed(2);
  }

  function renderEntry() {
    const resume=state.profile==='resume';
    setText('continue-eyebrow',resume?'接着上次的轻松':'从第一对水果开始');
    setText('continue-title',resume?'第 02 关':`来一局${modeName()}连连看`);
    setText('continue-detail',resume?'36 张待配 · 已用时 00:32':`第 01 关 · 24 张 · ${state.mode==='relaxed'?'不限时':'90 秒'}`);
    setText('continue-action',resume?'继续游戏':`开始${modeName()}第一关`);
    document.getElementById('continue-card').setAttribute('aria-label',resume?'继续轻松模式第02关，36张待配，已用时32秒':`开始${modeName()}模式第01关`);
    document.querySelectorAll('[data-mode]').forEach(button=>button.setAttribute('aria-selected',String(button.dataset.mode===state.mode)));
    setText('mode-summary',state.mode==='relaxed'?'不限时 · 提示和洗牌随心用':'限时完成 · 每关 3 次提示、2 次洗牌');
    document.getElementById('level-list').innerHTML=levels.map(item=>{
      const active=resume&&state.mode==='relaxed'&&item.id===2;
      return `<button class="level-card${active?' is-current':''}" data-level="${item.id}" aria-label="第${number(item.id)}关，${item.columns*item.rows}张，${item.kinds}种水果${state.mode==='challenge'?`，${item.seconds}秒`:''}${active?'，可继续':''}"><span class="level-number">${number(item.id)}</span><span class="level-detail"><strong>${item.columns*item.rows} 张 · ${item.kinds} 种水果</strong><small>${item.columns} × ${item.rows}${state.mode==='challenge'?` · ${item.seconds} 秒`:' · 不限时'}</small></span>${active?'<span class="level-state">继续</span>':'<span class="level-chevron" aria-hidden="true">›</span>'}</button>`;
    }).join('');
  }

  function renderGame() {
    setText('game-level',`第 ${number(state.level)} 关`);setText('game-mode',modeName());setText('remaining',total());
    setText('time-label',state.mode==='relaxed'?'用时':'倒计时');
    setText('game-time',state.overlay==='timeout'?'00:00':state.mode==='relaxed'?(state.isResume?'00:32':'00:00'):clockText(level().seconds));
    setText('hint-count',state.mode==='relaxed'?'∞':'3');setText('shuffle-count',state.mode==='relaxed'?'∞':'2');
    document.getElementById('save-warning').hidden=!state.saveError;
    screen.dataset.mode=state.mode;screen.dataset.level=String(state.level);screen.dataset.saveError=String(state.saveError);
    renderBoard();
  }

  function render() {
    screen.dataset.view=state.view;
    ['lobby','entry','game','assets'].forEach(view=>{document.getElementById(`${view}-page`).hidden=view!==state.view;});
    setText('page-title',state.view==='assets'?'水果素材板':'连连看');
    setText('heading-tag',state.view==='game'?'':state.view==='lobby'?'XANADU YONDER':state.view==='assets'?'FLUENT / 3D':'闲下来，连一连');
    renderEntry();if(state.view==='game')renderGame();
    previewLevel.value=String(state.level);previewProfile.value=state.profile;
  }

  function closeOverlay() {
    overlay.hidden=true;state.overlay=null;screen.dataset.overlay='false';
    document.querySelector('.screen-content').inert=false;
    if(previousFocus&&previousFocus.isConnected)previousFocus.focus();
  }

  function dismissOverlay() {
    if(!state.overlay||['complete','timeout'].includes(state.overlay))return;
    if(state.overlay==='pause')showView('entry');
    else if(state.overlay==='restart'||state.view==='game'&&['help','settings'].includes(state.overlay)){
      openOverlay('pause');previewState.value='pause';
    }else{
      closeOverlay();previewState.value=state.view;
    }
  }

  function openOverlay(kind) {
    if(!state.overlay)previousFocus=document.activeElement;
    state.overlay=kind;screen.dataset.overlay='true';overlay.hidden=false;
    document.querySelector('.screen-content').inert=true;
    modal.classList.toggle('center',['pause','restart','complete','timeout'].includes(kind));
    document.querySelector('.modal-close').hidden=['restart','complete','timeout'].includes(kind);
    const button=(text,action,secondary=false)=>`<button class="modal-button${secondary?' secondary':''}" data-action="${action}">${text}</button>`;
    if(kind==='pause')modalContent.innerHTML=`<p class="modal-kicker">${modeName()} · 第 ${number(state.level)} 关</p><h2 id="modal-title">歇一会儿</h2><p>水果在这里等你。<br>回来时，从刚才那一步继续。</p>${button('继续游戏','close')}${button('返回选关','entry',true)}<button class="modal-text-link" data-action="restart">重新开始本关</button>`;
    if(kind==='restart')modalContent.innerHTML=`<p class="modal-kicker">${modeName()} · 第 ${number(state.level)} 关</p><h2 id="modal-title">重新开始这一关？</h2><p>将替换这一关的当前进度。其他模式和关卡不受影响。</p>${button('重新开始','confirm-restart')}${button('保留当前进度','pause',true)}`;
    if(kind==='timeout'){
      setText('game-time','00:00');
      modalContent.innerHTML=`<p class="modal-kicker">挑战 · 第 ${number(state.level)} 关</p><h2 id="modal-title">时间到</h2><p>本关未完成。<br>再试一次，慢慢找到自己的节奏。</p>${button('重试本关','replay')}${button('返回选关','entry',true)}`;
    }
    if(kind==='complete'){
      const last=state.level===3,continueNext=state.profile==='resume'&&state.mode==='relaxed'&&state.level===1;
      modalContent.innerHTML=`<img class="modal-hero" src="assets/${last?'pineapple':'strawberry'}.png" alt=""><p class="modal-kicker">${modeName()} · 第 ${number(state.level)} 关</p><h2 id="modal-title">${last?'第 03 关完成':'全部连起来了'}</h2><p>${last?'最后一关完成，再来一次也很开心。':'小小的配对，刚刚好的成就感。'}</p><div class="result-stats"><span><b>00:58</b>完成用时</span><span><b>${total()/2}</b>成功配对</span></div>${button(last?'再玩这一关':continueNext?'继续下一关':'下一关',last?'replay':'next')}${button('返回选关','entry',true)}`;
    }
    if(kind==='help')modalContent.innerHTML=`<p class="modal-kicker">一条路，两次转弯</p><h2 id="modal-title">连连看玩法</h2><div class="help-demo"><img src="assets/strawberry.png" alt="草莓"><svg viewBox="0 0 250 63"><path d="M42 27 V8 H208 V27"/></svg><img src="assets/strawberry.png" alt="草莓"></div><p>点击两张相同水果。连线不穿过其他牌，最多转弯两次，就能配成一对。棋盘外圈也可以走。</p><p class="help-line">配完所有水果就过关。轻松模式不限时；挑战模式需要在时间内完成。</p>${button('明白了','dismiss')}`;
    if(kind==='settings')modalContent.innerHTML=`<p class="modal-kicker">合集共用</p><h2 id="modal-title">设置</h2><div class="setting-row"><span>声音</span><button class="switch" data-setting="sound" role="switch" aria-label="声音示例开关" aria-checked="${state.sound}"></button></div><div class="setting-row"><span>触感</span><button class="switch" data-setting="haptics" role="switch" aria-label="触感示例开关" aria-checked="${state.haptics}"></button></div><p class="setting-note">开关状态为演示，不播放声音或触发振动。</p>${button('完成','dismiss')}`;
    requestAnimationFrame(()=>{(modalContent.querySelector('button')||document.querySelector('.modal-close')).focus();});
  }

  function showView(view) {
    closeOverlay();state.view=view;render();
    previewState.value=view==='game'?state.mode:view;
  }

  function startLevel(id) {
    state.level=id;state.isResume=state.profile==='resume'&&state.mode==='relaxed'&&id===2;
    state.saveError=false;state.selected=[];state.variant=0;state.showPath=true;showView('game');
  }

  function restartFixture() {
    // Reset this in-memory display only; no saved slot or running timer exists.
    state.isResume=false;state.saveError=false;state.selected=[];state.variant=0;state.showPath=false;
    showView('game');
    document.getElementById('announcer').textContent='本关演示已重新开始，进度不保存';
  }

  function setPreview(value) {
    closeOverlay();state.saveError=false;state.showPath=true;state.selected=[];
    if(['lobby','entry','assets'].includes(value)){state.view=value;render();}
    else {
      state.view='game';
      if(value==='relaxed'||value==='challenge')state.mode=value;
      if(value==='timeout')state.mode='challenge';
      if(value==='last')state.level=3;
      if(value==='save-error')state.saveError=true;
      state.isResume=state.profile==='resume'&&state.mode==='relaxed'&&state.level===2;
      render();
      if(value==='pause')openOverlay('pause');
      if(value==='restart')openOverlay('restart');
      if(value==='timeout')openOverlay('timeout');
      if(value==='complete'||value==='last')openOverlay('complete');
    }
    previewState.value=value;
  }

  document.addEventListener('click',event=>{
    const button=event.target.closest('button');if(!button||button.disabled)return;
    if(button.dataset.mode){state.mode=button.dataset.mode;renderEntry();return;}
    if(button.dataset.level){startLevel(Number(button.dataset.level));return;}
    if(button.dataset.setting){const setting=button.dataset.setting;state[setting]=!state[setting];button.setAttribute('aria-checked',String(state[setting]));return;}
    if(button.dataset.tile!==undefined){
      const index=Number(button.dataset.tile);state.showPath=false;state.selected=state.selected[0]===index?[]:[index];renderBoard();
      document.getElementById('announcer').textContent=state.selected.length?`已选中${fruits[fixture[index]][1]}`:'已取消选中';return;
    }
    switch(button.dataset.action){
      case 'overview':toggleOverview();break;
      case 'home':showView('lobby');break;
      case 'entry':showView('entry');break;
      case 'continue':if(state.profile==='resume')state.mode='relaxed';startLevel(state.profile==='resume'?2:1);break;
      case 'help':openOverlay('help');break;
      case 'settings':openOverlay('settings');break;
      case 'pause':openOverlay('pause');previewState.value='pause';break;
      case 'restart':openOverlay('restart');previewState.value='restart';break;
      case 'confirm-restart':restartFixture();break;
      case 'dismiss':dismissOverlay();break;
      case 'close':closeOverlay();previewState.value=state.view==='game'?(state.saveError?'save-error':state.mode):state.view;break;
      case 'hint':state.showPath=true;renderBoard();document.getElementById('announcer').textContent='两张草莓可经左侧外圈连通，转弯两次';break;
      case 'shuffle':state.variant=1-state.variant;state.selected=[];state.showPath=false;renderBoard();document.getElementById('announcer').textContent='已切换固定洗牌示例，水果总数不变';break;
      case 'next':startLevel(Math.min(3,state.level+1));break;
      case 'replay':startLevel(state.level);break;
      case 'retry-save':document.getElementById('announcer').textContent='演示保存仍未成功，未保存提示继续保留';break;
    }
  });
  document.addEventListener('keydown',event=>{
    if(!state.overlay)return;
    if(event.key==='Escape'&&!['complete','timeout'].includes(state.overlay)){event.preventDefault();dismissOverlay();}
    if(event.key==='Tab'){
      const buttons=[...modal.querySelectorAll('button:not([hidden])')];const first=buttons[0],last=buttons[buttons.length-1];
      if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}
      else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
    }
  });
  previewState.addEventListener('change',()=>setPreview(previewState.value));
  previewLevel.addEventListener('change',()=>{state.level=Number(previewLevel.value);state.selected=[];render();if(['restart','complete','timeout'].includes(state.overlay))openOverlay(state.overlay);});
  previewProfile.addEventListener('change',()=>{state.profile=previewProfile.value;renderEntry();});
  function resize(){screen.dataset.size=previewSize.value;screen.setAttribute('aria-label',`连连看安全内容区，${previewSize.value==='compact'?'360乘640':'388乘784'}`);requestAnimationFrame(layoutBoard);}
  previewSize.addEventListener('change',resize);
  function layoutOverview(){
    const section=document.getElementById('overview');
    const availableWidth=document.querySelector('.review').clientWidth-64;
    const availableHeight=window.innerHeight-section.getBoundingClientRect().top-72;
    const scale=Math.max(.2,Math.min(1,(availableWidth-40)/(388*3),availableHeight/784));
    section.style.setProperty('--overview-scale',String(scale));
  }
  function toggleOverview(force){
    const active=typeof force==='boolean'?force:!document.body.classList.contains('overview-mode');
    document.body.classList.toggle('overview-mode',active);document.getElementById('overview').hidden=!active;
    document.querySelector('[data-action=overview]').setAttribute('aria-pressed',String(active));
    if(active){
      document.querySelectorAll('[data-overview-src]').forEach(frame=>{
        if(!frame.getAttribute('src'))frame.src=`index.html?embed=1&view=${frame.dataset.overviewSrc}&profile=resume&level=${frame.dataset.overviewSrc==='challenge'?'3':'2'}`;
      });layoutOverview();
    }
  }
  window.addEventListener('resize',layoutOverview);
  new ResizeObserver(layoutBoard).observe(area);
  document.getElementById('asset-grid').innerHTML=fruits.map((fruit,index)=>`<div class="asset-item">${fruitImage(index,fruit[1])}<span>${fruit[1]}</span></div>`).join('');
  if(params.get('embed')==='1')document.body.classList.add('embed');
  if(params.get('size')==='compact')previewSize.value='compact';
  if(params.get('profile')==='new')state.profile='new';
  if(['1','2','3'].includes(params.get('level')))state.level=Number(params.get('level'));
  if(params.get('mode')==='challenge')state.mode='challenge';
  const requested=params.get('view');
  const views=[...previewState.options].map(option=>option.value);
  installIcons();resize();setPreview(views.includes(requested)?requested:'entry');
  if(params.get('overview')==='1')toggleOverview(true);
})();
