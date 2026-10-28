(() => {
  'use strict';
  // Review-only interaction model. Production rules and saves belong in Unity/C#.
  const levels = {
    compact: { title: '教学 01 · 第一次搬运', short: '教学 01', rows: ['########','#  .   #','#  $   #','#  @   #','#      #','# $ .  #','#      #','########'] },
    tall: { title: '教学 02 · 长长的仓库', short: '教学 02', rows: ['########','#  .   #','#      #','#  .   #','#      #','#      #','#      #','#      #','#  $   #','#      #','#  $   #','#  @   #','########'] }
  };
  const art = '../../../ThirdParty/Sokoban/kenney-sokoban/PNG/Default%20size/';
  let levelId = 'compact', player = 0, boxes = new Set(), goals = new Set(), walls = new Set();
  let width = 8, height = 8, moves = 0, pushes = 0, history = [], message = '';
  const dialog = document.getElementById('modal');
  const content = document.getElementById('modal-content');
  const directions = { up: -8, down: 8, left: -1, right: 1 };
  function load(id) {
    levelId = id; height = levels[id].rows.length; width = levels[id].rows[0].length;
    player = 0; boxes = new Set(); goals = new Set(); walls = new Set();
    levels[id].rows.forEach((row,y) => [...row].forEach((cell,x) => {
      const pos = y*width+x;
      if(cell === '#') walls.add(pos);
      if(cell === '$' || cell === '*') boxes.add(pos);
      if(cell === '.' || cell === '*' || cell === '+') goals.add(pos);
      if(cell === '@' || cell === '+') player = pos;
    }));
    moves = pushes = 0; history = [];
    message = '把木箱推到绿色标记上';
    render();
  }
  const solved = () => [...boxes].every(pos => goals.has(pos));
  function move(direction) {
    if(dialog.open || solved()) return;
    const delta = directions[direction], next = player + delta;
    if (next < 0 || next >= width*height || (Math.abs(delta)===1 && Math.floor(next/width)!==Math.floor(player/width)) || walls.has(next)) { message='这里是墙，换个方向试试'; render(); return; }
    const pushing = boxes.has(next), after = next+delta;
    if(pushing && (after<0 || after>=width*height || walls.has(after) || boxes.has(after) || (Math.abs(delta)===1 && Math.floor(after/width)!==Math.floor(next/width)))) { message='一次只能推动一个箱子'; render(); return; }
    history.push({player,boxes:[...boxes],moves,pushes});
    if(pushing) { boxes.delete(next); boxes.add(after); pushes++; }
    player=next; moves++;
    message = solved() ? '全部归位，做得好！可以撤回或再来一次。' : pushing ? '箱子往前一步，离目标也更近一步。' : '走错了也没关系，可以随时撤回';
    render();
  }
  function render() {
    document.querySelectorAll('[data-stat=moves]').forEach(e=>e.textContent=moves);
    document.querySelectorAll('[data-stat=pushes]').forEach(e=>e.textContent=pushes);
    document.querySelectorAll('[data-stat=goals]').forEach(e=>e.textContent=`${[...boxes].filter(p=>goals.has(p)).length} / ${goals.size}`);
    document.querySelectorAll('[data-label=level]').forEach(e=>e.textContent=levels[levelId].title);
    document.querySelectorAll('[data-label=short-level]').forEach(e=>e.textContent=levels[levelId].short);
    document.querySelectorAll('[data-label=message]').forEach(e=>e.textContent=message);
    document.querySelectorAll('[data-action=undo]').forEach(e=>e.disabled=!history.length);
    document.querySelectorAll('[data-level]').forEach(e=>e.setAttribute('aria-pressed',String(e.dataset.level===levelId)));
    document.getElementById('comparison-note').textContent = levelId==='compact' ? '方形关卡在两种布局中大小相同：限制来自屏幕宽度。切换「高地图」可以看到专注模式的收益。' : '高地图受可用高度限制：专注模式腾出的纵向空间能放大格子，同时保持整张地图可见。';
    document.querySelectorAll('.board').forEach(board=>{
      board.style.gridTemplateColumns=`repeat(${width},1fr)`;
      board.style.gridTemplateRows=`repeat(${height},1fr)`;
      board.innerHTML='';
      for(let pos=0;pos<width*height;pos++){
        const tile=document.createElement('div');
        tile.className='tile '+(walls.has(pos)?'wall':'floor')+(goals.has(pos)?' goal':'')+(boxes.has(pos)&&goals.has(pos)?' occupied':'');
        function img(path,cls){const el=document.createElement('img');el.src=art+path;el.alt='';el.className=cls||'';el.draggable=false;tile.append(el);}
        if(walls.has(pos)) img('Blocks/block_06.png');
        if(boxes.has(pos)) img(goals.has(pos)?'Crates/crate_05.png':'Crates/crate_02.png','crate');
        if(boxes.has(pos)&&goals.has(pos)){const check=document.createElement('span');check.className='goal-check';check.textContent='✓';tile.append(check);}
        if(player===pos) img('Player/player_05.png','player');
        board.append(tile);
      }
      board.setAttribute('aria-label',`${width}列${height}行棋盘，角色位于第${Math.floor(player/width)+1}行第${player%width+1}列，${moves}步，${pushes}次推动`);
    });
    resize();
  }
  function resize(){document.querySelectorAll('.board').forEach(board=>{const area=board.parentElement;const size=Math.min(area.clientWidth/width,area.clientHeight/height);board.style.width=`${size*width}px`;board.style.height=`${size*height}px`;});}
  function showModal(title,body){content.innerHTML=`<h2>${title}</h2>${body}`;dialog.showModal();}
  function choosePreview(mode){
    document.querySelector('.comparison').dataset.preview=mode;
    document.querySelectorAll('button[data-preview]').forEach(e=>e.setAttribute('aria-pressed',String(e.dataset.preview===mode)));
    resize();
    const phone=document.querySelector(`.phone.${mode}`);phone.classList.remove('highlight');void phone.offsetWidth;phone.classList.add('highlight');
  }
  document.addEventListener('click',event=>{
    const button=event.target.closest('button');if(!button)return;
    if(button.dataset.level){load(button.dataset.level);if(dialog.open)dialog.close();return;}
    if(button.dataset.preview){choosePreview(button.dataset.preview);return;}
    if(button.dataset.move){move(button.dataset.move);return;}
    switch(button.dataset.action){
      case 'undo':{const last=history.pop();if(last){player=last.player;boxes=new Set(last.boxes);moves=last.moves;pushes=last.pushes;message='已撤回上一步';render();}break;}
      case 'restart':load(levelId);break;
      case 'focus':choosePreview('focus');break;
      case 'standard':choosePreview('standard');break;
      case 'levels':case 'back':showModal('选一个仓库', '<p>原型提供两关原创演示，用于比较地图比例。</p><button class="level-option" data-level="compact">01　第一次搬运 · 8 × 8</button><button class="level-option" data-level="tall">02　长长的仓库 · 8 × 13</button><p class="fine">切换关卡会重置演示。正式版本将提供独立关卡进度。</p>');break;
      case 'help':showModal('每个箱子，都有自己的位置', '<p>用方向盘、方向键或 WASD 移动角色。把全部木箱推到绿色菱形标记上就能过关。</p><p class="modal-hint">一次只能推一个箱子，不能拉。别把箱子推入没有目标的角落。</p><p>撤回不限次数；这两关仅用于演示，不保存进度。布局切换会保留当前盘面。</p>');break;
      case 'settings':showModal('演示设置', '<p>正式游戏将沿用合集的声音与触感开关。</p><p>此原型没有声音、触感或存档，只演示布局、移动、推动和撤回。</p>');break;
    }
  });
  document.getElementById('close-modal').addEventListener('click',()=>dialog.close());
  dialog.addEventListener('click',event=>{if(event.target===dialog){const r=dialog.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)dialog.close();}});
  document.addEventListener('keydown',event=>{const dir={ArrowUp:'up',ArrowDown:'down',ArrowLeft:'left',ArrowRight:'right',w:'up',s:'down',a:'left',d:'right'}[event.key];if(dir&&!event.repeat&&!dialog.open){event.preventDefault();move(dir);}});
  new ResizeObserver(resize).observe(document.querySelector('.comparison'));
  window.addEventListener('resize',resize);
  load('compact');
})();
