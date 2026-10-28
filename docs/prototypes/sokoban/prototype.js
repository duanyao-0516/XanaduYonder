(() => {
  'use strict';
  // Review-only UI model. It never reads or writes player saves or browser storage.
  const fallbackLevels = [
    { levelId:'tutorial-001',chapter:'tutorial',order:1,title:'初次搬运',rows:['########','#  .   #','#  $   #','#  @   #','#      #','# $ .  #','#      #','########'] },
    { levelId:'tutorial-008',chapter:'tutorial',order:8,title:'长仓库',rows:['########','#  .   #','#      #','#  .   #','#      #','#      #','#      #','#      #','#  $   #','#      #','#  $   #','#  @   #','########'] }
  ];
  const art = '../../../ThirdParty/Sokoban/kenney-sokoban/PNG/Default%20size/';
  let levels=fallbackLevels, levelId='tutorial-001', chapter='tutorial', modalChapter='tutorial';
  let view='entry', hasStarted=false, player=0, boxes=new Set(), goals=new Set(), walls=new Set();
  let width=8,height=8,moves=0,pushes=0,history=[],toastTimer=null;
  const completed=new Map();
  const dialog=document.getElementById('modal'),content=document.getElementById('modal-content');
  const area=document.querySelector('.board-area'),board=document.querySelector('.board');
  const toast=document.querySelector('.board-toast'),announcer=document.getElementById('announcer');
  const entry=document.getElementById('entry-page'),game=document.getElementById('game-page');
  const current=()=>levels.find(level=>level.levelId===levelId)||fallbackLevels[0];
  const number=level=>level.chapter==='tutorial'?level.order:levels.filter(item=>item.chapter!=='tutorial').findIndex(item=>item.levelId===level.levelId)+1;
  const label=level=>level.chapter==='tutorial'?`${String(number(level)).padStart(2,'0')} ${level.title}`:`第 ${number(level)} 关`;
  const filled=()=>[...boxes].filter(position=>goals.has(position)).length;
  const solved=()=>filled()===goals.size&&goals.size>0;
  const escape=value=>String(value).replace(/[&<>"']/g,char=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));
  const setLabel=(name,value)=>document.querySelectorAll(`[data-label="${name}"]`).forEach(node=>{node.textContent=value;});
  function chapters(){return [...new Set(levels.map(level=>level.chapter))];}
  function chapterName(id){
    if(id==='tutorial')return '入门教学';
    const group=levels.filter(level=>level.chapter===id);
    return `第 ${number(group[0])}–${number(group[group.length-1])} 关`;
  }
  // Equivalent SVG paths use the existing UiIconGraphic.cs mesh coordinates.
  function installIcons() {
    const points = a => a.map(p=>p.join(',')).join(' ');
    const path = a => `<polyline points="${points(a)}"/>`;
    const paths = {
      Home: path([[-.42,.01],[0,.4],[.42,.01]]) + path([[-.3,.09],[-.3,-.36],[-.11,-.36],[-.11,-.1],[.11,-.1],[.11,-.36],[.3,-.36],[.3,.09]]),
      Book: path([[0,-.32],[-.18,-.24],[-.42,-.24],[-.42,.34],[-.18,.34],[0,.26],[.18,.34],[.42,.34],[.42,-.24],[.18,-.24],[0,-.32],[0,.26]]) + [0,1].map(i=>path([[-.3,.16-i*.16],[-.13,.12-i*.16]])+path([[.13,.12-i*.16],[.3,.16-i*.16]])).join(''),
      Undo: path([[-.12,.38],[-.37,.14],[-.12,-.1]])+path([[-.36,.14],[.1,.14],[.25,.1],[.35,-.01],[.37,-.16],[.31,-.29],[.18,-.35],[-.03,-.35]])
    };
    paths.Restart = [[20,155],[200,335]].map(([start,end])=>{
      const arc=Array.from({length:19},(_,i)=>{const a=(start+(end-start)*i/18)*Math.PI/180;return [Math.cos(a)*.34,Math.sin(a)*.34];});
      const a=end*Math.PI/180,p=arc[18],d=[-Math.sin(a),Math.cos(a)],n=[-d[1],d[0]];
      return path(arc)+path([[p[0]-d[0]*.17+n[0]*.12,p[1]-d[1]*.17+n[1]*.12],p,[p[0]-d[0]*.17-n[0]*.12,p[1]-d[1]*.17-n[1]*.12]]);
    }).join('');
    const gear=Array.from({length:64},(_,i)=>{const a=i*Math.PI*2/64,r=i%8<2||i%8>5?.35:.47;return `${Math.cos(a)*r},${Math.sin(a)*r}`;}).join(' L');
    paths.Settings=`<path d="M${gear} Z M.17,0 A.17,.17 0 1,0 -.17,0 A.17,.17 0 1,0 .17,0 Z" fill="currentColor" fill-rule="evenodd" stroke="none"/>`;
    document.querySelectorAll('[data-icon]').forEach(el=>{el.innerHTML=`<svg viewBox="-.5 -.5 1 1" aria-hidden="true" focusable="false"><g transform="scale(1,-1)" fill="none" stroke="currentColor" stroke-width=".07" stroke-linecap="round" stroke-linejoin="round">${paths[el.dataset.icon]}</g></svg>`;});
  }
  function clearFeedback(){clearTimeout(toastTimer);toast.classList.remove('visible');toast.textContent='';announcer.textContent='';}
  function feedback(message){clearFeedback();toast.textContent=message;announcer.textContent=message;toast.classList.add('visible');toastTimer=setTimeout(clearFeedback,2200);}
  function closeModal(){if(dialog.open)dialog.close();dialog.classList.remove('level-dialog');}
  function showView(next){
    closeModal();clearFeedback();view=next;entry.hidden=next!=='entry';game.hidden=next!=='game';document.querySelector('.phone').dataset.view=next;
    document.querySelectorAll('[data-preview]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.preview===(next==='entry'?'entry':levelId==='tutorial-008'?'tall':'game'))));
    renderEntry();if(next==='game')requestAnimationFrame(layoutBoard);
  }
  function load(id,start=true){
    const level=levels.find(item=>item.levelId===id);if(!level)return;
    clearFeedback();levelId=id;hasStarted=start;height=level.rows.length;width=level.rows[0].length;
    player=0;boxes=new Set();goals=new Set();walls=new Set();moves=pushes=0;history=[];
    level.rows.forEach((row,y)=>[...row].forEach((cell,x)=>{const position=y*width+x;if(cell==='#')walls.add(position);if(cell==='$'||cell==='*')boxes.add(position);if('.+*'.includes(cell))goals.add(position);if(cell==='@'||cell==='+')player=position;}));
    render();
  }
  function startLevel(id){closeModal();if(id!==levelId||!hasStarted||solved())load(id,true);showView('game');}
  function move(direction,{seed=false}={}){
    if(!seed&&(view!=='game'||dialog.open||document.hidden||solved()))return;
    const delta={up:-width,down:width,left:-1,right:1}[direction],next=player+delta;
    const outside=(a,b)=>b<0||b>=width*height||(Math.abs(delta)===1&&Math.floor(a/width)!==Math.floor(b/width));
    if(outside(player,next)||walls.has(next)){if(!seed)feedback('前方是墙');return;}
    const pushing=boxes.has(next),after=next+delta;
    if(pushing&&(outside(next,after)||walls.has(after)||boxes.has(after))){if(!seed)feedback('箱子前方没有空位');return;}
    history.push({player,boxes:[...boxes],moves,pushes});if(pushing){boxes.delete(next);boxes.add(after);pushes++;}player=next;moves++;hasStarted=true;
    if(solved()){
      const old=completed.get(levelId);if(!old||moves<old.moves||(moves===old.moves&&pushes<old.pushes))completed.set(levelId,{moves,pushes});
    }
    render();if(solved()&&!seed)showResult();
  }
  function appendTiles(target,mini=false){
    target.replaceChildren();target.style.gridTemplateColumns=`repeat(${width},1fr)`;target.style.gridTemplateRows=`repeat(${height},1fr)`;
    for(let position=0;position<width*height;position++){
      const tile=document.createElement('span');tile.className='tile '+(walls.has(position)?'wall':'floor')+(goals.has(position)?' goal':'')+(boxes.has(position)&&goals.has(position)?' occupied':'');
      function image(file,cls){const img=document.createElement('img');img.src=art+file;img.alt='';img.className=cls||'';img.draggable=false;tile.append(img);}
      if(walls.has(position))image('Blocks/block_06.png');
      if(boxes.has(position))image(goals.has(position)?'Crates/crate_05.png':'Crates/crate_02.png','crate');
      if(boxes.has(position)&&goals.has(position)&&!mini){const check=document.createElement('span');check.className='goal-check';check.textContent='✓';tile.append(check);}
      if(player===position)image('Player/player_05.png','player');target.append(tile);
    }
  }
  function render(){
    document.querySelector('[data-stat=moves]').textContent=moves;document.querySelector('[data-stat=pushes]').textContent=pushes;document.querySelector('[data-stat=goals]').textContent=`${filled()}/${goals.size}`;
    setLabel('level',label(current()));document.querySelector('[data-action=undo]').disabled=!history.length||solved();
    document.querySelectorAll('[data-move]').forEach(button=>{button.disabled=solved();});
    appendTiles(board);board.setAttribute('aria-label',`${width}列${height}行棋盘，角色位于第${Math.floor(player/width)+1}行第${player%width+1}列，${moves}步，${pushes}次推动`);
    renderEntry();layoutBoard();
  }
  function layoutBoard(){
    if(view!=='game'||area.clientWidth===0||area.clientHeight===0)return;
    const cell=Math.max(0,Math.min((area.clientWidth-24)/width,(area.clientHeight-24)/height));board.style.width=`${cell*width}px`;board.style.height=`${cell*height}px`;
  }
  function chapterTabs(selected,attribute){
    return chapters().map(id=>`<button role="tab" data-${attribute}="${escape(id)}" aria-selected="${id===selected}">${escape(chapterName(id))}</button>`).join('');
  }
  function numberButton(level){
    const active=hasStarted&&!solved()&&level.levelId===levelId,record=completed.get(level.levelId);
    const description=label(level)+(active?'，继续当前进度':'')+(record?`，已完成，纪录 ${record.moves} 步`:'');
    return `<button class="number-level${active?' is-active':''}${record?' is-complete':''}" data-start="${escape(level.levelId)}" aria-label="${escape(description)}"${active?' aria-current="true"':''} title="${escape(description)}">${String(number(level)).padStart(2,'0')}</button>`;
  }
  function renderEntry(){
    const active=hasStarted&&!solved(),level=current(),hasNext=levels.findIndex(item=>item.levelId===levelId)<levels.length-1;
    setLabel('continue-eyebrow',active?'继续上次的搬运':solved()?'这一关已全部归位':'从第一只箱子开始');
    setLabel('continue-title',active?label(level):solved()?label(level):'先来一次轻松的搬运');
    setLabel('continue-stats',active?`${moves} 步 · ${pushes} 次推动 · ${filled()}/${goals.size} 归位`:solved()?`${moves} 步完成 · 已记入本次演示纪录`:'8 关教学 · 不计时，随时撤回');
    const action=active?'继续游戏':solved()?(hasNext?'挑战下一关':'再玩这一关'):'开始入门教学';
    setLabel('continue-action',action);
    document.querySelector('[data-action=continue]').setAttribute('aria-label',active?`继续 ${label(level)}，${moves} 步`:action);
    appendTiles(document.querySelector('.mini-board'),true);
    const mini=document.querySelector('.mini-board');mini.style.aspectRatio=`${width}/${height}`;mini.style.height=height>width?'91px':'auto';mini.style.width=height>width?`${91*width/height}px`:'';
    setLabel('completed',completed.size);setLabel('total',levels.length===2?'2（离线示例）':levels.length);document.querySelector('.progress-track>span').style.width=`${completed.size/levels.length*100}%`;
    if(!chapters().includes(chapter))chapter=chapters()[0];
    document.getElementById('entry-chapters').innerHTML=chapterTabs(chapter,'chapter');
    const selected=levels.filter(item=>item.chapter===chapter);setLabel('chapter-title',chapterName(chapter));setLabel('chapter-count',`${selected.length} 关`);
    const grid=document.getElementById('entry-level-grid');grid.classList.toggle('is-number-grid',chapter!=='tutorial');
    grid.innerHTML=selected.map(item=>{
      if(item.chapter!=='tutorial')return numberButton(item);
      const activeCard=hasStarted&&!solved()&&item.levelId===levelId,done=completed.get(item.levelId);
      return `<button class="level-card${activeCard?' is-active':''}${done?' is-complete':''}" data-start="${escape(item.levelId)}" aria-label="${escape(label(item))}${done?'，已完成':''}${activeCard?'，继续当前进度':''}"><span class="level-number">${String(number(item)).padStart(2,'0')}</span>${done?'<span class="completed-check" aria-hidden="true">✓</span>':''}<span class="level-name">${escape(item.title)}</span><span class="level-state">${activeCard?'进行中':done?`${done.moves} 步完成`:''}</span></button>`;
    }).join('');
  }
  function showModal(title,body,type=''){
    clearFeedback();content.innerHTML=`<h2>${escape(title)}</h2>${body}`;dialog.classList.toggle('level-dialog',type==='levels');if(!dialog.open)dialog.showModal();
  }
  function renderLevelModal(){
    const selected=levels.filter(level=>level.chapter===modalChapter);
    showModal('选择关卡',`<div class="chapter-tabs modal-chapters" role="tablist" aria-label="选关章节">${chapterTabs(modalChapter,'modal-chapter')}</div><div class="modal-level-list${modalChapter!=='tutorial'?' is-number-grid':''}">${selected.map(level=>{
      if(level.chapter!=='tutorial')return numberButton(level);
      const record=completed.get(level.levelId),active=level.levelId===levelId;
      return `<button class="level-option${active?' is-active':''}" data-start="${escape(level.levelId)}"><span><strong>${escape(label(level))}</strong>${record?`<small>纪录 ${record.moves} 步 · ${record.pushes} 次推动</small>`:active?`<small>当前关 · ${moves} 步 · ${filled()}/${goals.size} 归位</small>`:''}</span>${record?'<span class="completed-label">✓ 已完成</span>':''}</button>`;
    }).join('')}</div><div class="modal-links"><button data-action="entry">查看关卡入口</button><span>${completed.size} / ${levels.length} 已完成</span></div>`,'levels');
  }
  function showResult(){
    const index=levels.findIndex(level=>level.levelId===levelId),next=levels[index+1];
    showModal('全部归位',`<p>${escape(label(current()))}<br>${moves} 步 · ${pushes} 次推动</p><button class="confirm" ${next?`data-start="${escape(next.levelId)}"`:'data-action="entry"'}>${next?'下一关':'返回关卡入口'}</button><button class="confirm secondary-confirm" data-action="replay">重玩本关</button>`);
  }
  function continueGame(){
    if(solved()){const index=levels.findIndex(level=>level.levelId===levelId);if(levels[index+1])startLevel(levels[index+1].levelId);else{load(levelId,true);showView('game');}return;}
    if(!hasStarted)load('tutorial-001',true);showView('game');
  }
  document.addEventListener('click',event=>{
    const button=event.target.closest('button');if(!button||button.disabled)return;
    if(button.dataset.preview){
      if(button.dataset.preview==='entry')showView('entry');
      else if(button.dataset.preview==='tall'){load('tutorial-008',true);showView('game');}
      else continueGame();return;
    }
    if(button.dataset.chapter){chapter=button.dataset.chapter;renderEntry();return;}
    if(button.dataset.modalChapter){modalChapter=button.dataset.modalChapter;renderLevelModal();return;}
    if(button.dataset.start){startLevel(button.dataset.start);return;}
    if(button.dataset.move){move(button.dataset.move);return;}
    switch(button.dataset.action){
      case 'continue':continueGame();break;
      case 'entry':showView('entry');break;
      case 'undo':{const last=history.pop();if(last){player=last.player;boxes=new Set(last.boxes);moves=last.moves;pushes=last.pushes;render();feedback('已撤回上一步');}break;}
      case 'restart':showModal('重新开始这一关？','<p>本次尝试的步数与撤回记录将清空。<br>已完成纪录会保留在本次演示中。</p><button class="confirm" data-action="replay">重新开始</button><button class="confirm secondary-confirm" data-action="close">继续游戏</button>');break;
      case 'replay':closeModal();load(levelId,true);showView('game');break;
      case 'close':closeModal();break;
      case 'levels':modalChapter=current().chapter;renderLevelModal();break;
      case 'home':showModal('返回大厅','<p>这是独立交互示例，尚未接入合集大厅。</p><button class="confirm" data-action="entry">返回推箱子关卡入口</button><button class="confirm secondary-confirm" data-action="close">留在当前页面</button>');break;
      case 'help':showModal('推箱子玩法','<p>用方向盘、方向键或 WASD 移动。把全部木箱推到绿色标记上即可过关。一次只能推一个箱子，不能拉。</p><p class="modal-hint">棋盘始终完整显示，自动适配可用空间。卡住时，可以连续撤回，或重新开始这一关。</p><p>关卡入口可以按章节挑选，也可以直接继续上次的搬运。游戏中点击关卡名可快速切换。</p><p class="fine">这是 UI 交互示例；离开或刷新页面后，演示进度不会保留。</p>');break;
      case 'settings':showModal('设置','<p>正式游戏沿用合集的声音与触感开关。</p><p class="fine">此示例不播放声音，不触发振动，也不写入正式游戏存档。</p>');break;
      case 'preview-new':completed.clear();load('tutorial-001',false);chapter='tutorial';showView('entry');break;
    }
  });
  document.getElementById('close-modal').addEventListener('click',closeModal);
  dialog.addEventListener('click',event=>{if(event.target===dialog){const rect=dialog.getBoundingClientRect();if(event.clientX<rect.left||event.clientX>rect.right||event.clientY<rect.top||event.clientY>rect.bottom)closeModal();}});
  document.addEventListener('keydown',event=>{
    const direction={ArrowUp:'up',ArrowDown:'down',ArrowLeft:'left',ArrowRight:'right',w:'up',s:'down',a:'left',d:'right'}[event.key];
    if(direction&&!event.repeat&&!dialog.open&&view==='game'){event.preventDefault();move(direction);}
  });
  new ResizeObserver(layoutBoard).observe(area);window.addEventListener('resize',layoutBoard);
  installIcons();load('tutorial-001',false);
  if(new URLSearchParams(location.search).get('state')!=='new')move('up',{seed:true});
  showView('entry');
  fetch('../../../Assets/Resources/Sokoban/campaign.json').then(response=>{if(!response.ok)throw new Error('HTTP '+response.status);return response.json();}).then(campaign=>{
    if(!Array.isArray(campaign.levels)||campaign.levels.length!==108)throw new Error('Invalid campaign');
    levels=campaign.levels;render();if(location.hash==='#game')showView('game');else if(location.hash==='#tall'){load('tutorial-008',true);showView('game');}
    document.querySelector('.phone').dataset.catalog='ready';
  }).catch(()=>{
    const notice=document.getElementById('catalog-notice');notice.hidden=false;notice.textContent='未能载入完整关卡目录。当前仅展示两张原创地图，请通过本地 HTTP 地址打开此示例。';document.querySelector('.phone').dataset.catalog='fallback';renderEntry();
  });
})();
