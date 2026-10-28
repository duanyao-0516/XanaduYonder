(() => {
  'use strict';
  // Review-only interaction model. Production rules and saves belong in Unity/C#.
  const levels = {
    compact: { title: '01 初次搬运', rows: ['########','#  .   #','#  $   #','#  @   #','#      #','# $ .  #','#      #','########'] },
    tall: { title: '02 长仓库', rows: ['########','#  .   #','#      #','#  .   #','#      #','#      #','#      #','#      #','#  $   #','#      #','#  $   #','#  @   #','########'] }
  };
  const art = '../../../ThirdParty/Sokoban/kenney-sokoban/PNG/Default%20size/';
  let levelId = 'compact', player = 0, boxes = new Set(), goals = new Set(), walls = new Set();
  let width = 8, height = 8, moves = 0, pushes = 0, history = [];
  let zoom = 1, panX = 0, panY = 0, boardWidth = 0, boardHeight = 0, drag = null, toastTimer = null;
  const dialog = document.getElementById('modal');
  const content = document.getElementById('modal-content');
  const area = document.querySelector('.board-area');
  const board = document.querySelector('.board');
  const toast = document.querySelector('.board-toast');
  const announcer = document.getElementById('announcer');

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
  function clearFeedback() { clearTimeout(toastTimer); toast.classList.remove('visible'); toast.textContent = ''; announcer.textContent = ''; }
  function feedback(text) {
    clearFeedback(); toast.textContent = text; toast.classList.add('visible'); announcer.textContent = text;
    toastTimer=setTimeout(()=>{toast.classList.remove('visible');toast.textContent='';},2200);
  }
  function cancelDrag() {
    const pointer=drag?.id;drag=null;area.classList.remove('dragging');
    if(pointer!==undefined&&area.hasPointerCapture(pointer)) area.releasePointerCapture(pointer);
  }
  function load(id) {
    cancelDrag();clearFeedback();levelId=id;height=levels[id].rows.length;width=levels[id].rows[0].length;
    player=0;boxes=new Set();goals=new Set();walls=new Set();
    levels[id].rows.forEach((row,y)=>[...row].forEach((cell,x)=>{
      const pos=y*width+x;
      if(cell==='#')walls.add(pos);
      if(cell==='$'||cell==='*')boxes.add(pos);
      if(cell==='.'||cell==='*'||cell==='+')goals.add(pos);
      if(cell==='@'||cell==='+')player=pos;
    }));
    moves=pushes=0;history=[];zoom=1;panX=panY=0;render();
  }
  const solved = () => [...boxes].every(pos=>goals.has(pos));
  function move(direction) {
    if(dialog.open||document.hidden||solved())return;
    const delta={up:-width,down:width,left:-1,right:1}[direction],next=player+delta;
    if(next<0||next>=width*height||(Math.abs(delta)===1&&Math.floor(next/width)!==Math.floor(player/width))||walls.has(next)){feedback('前方是墙');return;}
    const pushing=boxes.has(next),after=next+delta;
    if(pushing&&(after<0||after>=width*height||walls.has(after)||boxes.has(after)||(Math.abs(delta)===1&&Math.floor(after/width)!==Math.floor(next/width)))){feedback('箱子前方没有空位');return;}
    history.push({player,boxes:[...boxes],moves,pushes});
    if(pushing){boxes.delete(next);boxes.add(after);pushes++;}
    player=next;moves++;render();if(solved())feedback('全部归位，通关！');
  }
  function render() {
    document.querySelector('[data-stat=moves]').textContent=moves;
    document.querySelector('[data-stat=pushes]').textContent=pushes;
    document.querySelector('[data-stat=goals]').textContent=`${[...boxes].filter(p=>goals.has(p)).length}/${goals.size}`;
    document.querySelector('[data-label=level]').textContent=levels[levelId].title;
    document.querySelector('[data-action=undo]').disabled=!history.length;
    document.querySelectorAll('[data-level]').forEach(e=>e.setAttribute('aria-pressed',String(e.dataset.level===levelId)));
    board.style.gridTemplateColumns=`repeat(${width},1fr)`;board.style.gridTemplateRows=`repeat(${height},1fr)`;board.innerHTML='';
    for(let pos=0;pos<width*height;pos++){
      const tile=document.createElement('div');
      tile.className='tile '+(walls.has(pos)?'wall':'floor')+(goals.has(pos)?' goal':'')+(boxes.has(pos)&&goals.has(pos)?' occupied':'');
      function img(path,cls){const el=document.createElement('img');el.src=art+path;el.alt='';el.className=cls||'';el.draggable=false;tile.append(el);}
      if(walls.has(pos))img('Blocks/block_06.png');
      if(boxes.has(pos))img(goals.has(pos)?'Crates/crate_05.png':'Crates/crate_02.png','crate');
      if(boxes.has(pos)&&goals.has(pos)){const check=document.createElement('span');check.className='goal-check';check.textContent='✓';tile.append(check);}
      if(player===pos)img('Player/player_05.png','player');board.append(tile);
    }
    board.setAttribute('aria-label',`${width}列${height}行棋盘，角色位于第${Math.floor(player/width)+1}行第${player%width+1}列，${moves}步，${pushes}次推动`);
    layoutBoard();
  }
  function layoutBoard(){
    const cell=Math.min(area.clientWidth/width,area.clientHeight/height);
    boardWidth=cell*width*zoom;boardHeight=cell*height*zoom;
    board.style.width=`${boardWidth}px`;board.style.height=`${boardHeight}px`;applyPan();
    document.querySelector('[data-action=zoom-out]').disabled=zoom<=1;
    document.querySelector('[data-action=zoom-in]').disabled=zoom>=2;
    document.querySelector('[data-action=fit]').textContent=zoom===1?'适配':`${Math.round(zoom*100)}%`;
  }
  function applyPan(){
    const maxX=Math.max(0,(boardWidth-area.clientWidth)/2),maxY=Math.max(0,(boardHeight-area.clientHeight)/2);
    panX=Math.max(-maxX,Math.min(maxX,panX));panY=Math.max(-maxY,Math.min(maxY,panY));
    board.style.transform=`translate(-50%,-50%) translate(${panX}px,${panY}px)`;
    area.classList.toggle('can-pan',maxX>0||maxY>0);
  }
  function changeZoom(value){cancelDrag();const old=zoom;zoom=Math.max(1,Math.min(2,value));panX*=zoom/old;panY*=zoom/old;layoutBoard();}
  function fit(){cancelDrag();zoom=1;panX=panY=0;layoutBoard();}
  function resized(){cancelDrag();layoutBoard();}
  function showModal(title,body){cancelDrag();clearFeedback();content.innerHTML=`<h2>${title}</h2>${body}`;dialog.showModal();}
  document.addEventListener('click',event=>{
    const button=event.target.closest('button');if(!button)return;
    if(button.dataset.level){load(button.dataset.level);if(dialog.open)dialog.close();return;}
    if(button.dataset.move){move(button.dataset.move);return;}
    switch(button.dataset.action){
      case 'undo':{cancelDrag();const last=history.pop();if(last){player=last.player;boxes=new Set(last.boxes);moves=last.moves;pushes=last.pushes;render();feedback('已撤回上一步');}break;}
      case 'restart':load(levelId);break;
      case 'zoom-out':changeZoom(zoom-.25);break;
      case 'zoom-in':changeZoom(zoom+.25);break;
      case 'fit':fit();break;
      case 'home':showModal('返回大厅','<p>这是独立页面原型，尚未接入合集大厅。</p><p>关闭弹层可继续当前演示，盘面保持不变。</p>');break;
      case 'levels':showModal('选择关卡','<button class="level-option" data-level="compact">01　初次搬运 · 8 × 8</button><button class="level-option" data-level="tall">02　长仓库 · 8 × 13</button><p class="fine">两关均为原创演示。切换关卡会重置当前盘面。</p>');break;
      case 'help':showModal('推箱子玩法','<p>用方向盘、方向键或 WASD 移动。把全部木箱推到绿色标记上即可过关。一次只能推一个箱子，不能拉；撤回不限次数。</p><p class="modal-hint">使用 − / + 按钮缩放，每次调整 25%，上限 200%。点击「适配」或倍率回到完整地图。放大后可单指拖动平移；拖动不会移动角色。</p><p class="fine">此原型不支持双指缩放、不保存进度。打开弹层、切关、失焦或调整窗口会取消正在进行的拖动。</p>');break;
      case 'settings':showModal('设置','<p>正式游戏将沿用合集的声音与触感开关。</p><p class="fine">此原型没有声音、触感或存档，仅供页面与交互评审。</p>');break;
    }
  });
  area.addEventListener('pointerdown',event=>{
    if(!event.isPrimary){cancelDrag();return;}
    if(dialog.open||document.hidden||event.button!==0)return;
    cancelDrag();drag={id:event.pointerId,x:event.clientX,y:event.clientY,panX,panY};area.setPointerCapture(event.pointerId);area.classList.add('dragging');
  });
  area.addEventListener('pointermove',event=>{if(!drag||event.pointerId!==drag.id)return;panX=drag.panX+event.clientX-drag.x;panY=drag.panY+event.clientY-drag.y;applyPan();});
  area.addEventListener('pointerup',cancelDrag);area.addEventListener('pointercancel',cancelDrag);area.addEventListener('lostpointercapture',()=>{drag=null;area.classList.remove('dragging');});
  document.getElementById('close-modal').addEventListener('click',()=>dialog.close());
  dialog.addEventListener('click',event=>{if(event.target===dialog){const r=dialog.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)dialog.close();}});
  document.addEventListener('keydown',event=>{const dir={ArrowUp:'up',ArrowDown:'down',ArrowLeft:'left',ArrowRight:'right',w:'up',s:'down',a:'left',d:'right'}[event.key];if(dir&&!event.repeat&&!dialog.open){event.preventDefault();move(dir);}});
  window.addEventListener('blur',cancelDrag);document.addEventListener('visibilitychange',()=>{if(document.hidden)cancelDrag();});
  new ResizeObserver(resized).observe(area);window.addEventListener('resize',resized);
  installIcons();load('compact');
})();
