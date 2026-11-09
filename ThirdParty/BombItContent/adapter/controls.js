(function () {
  'use strict';
  document.addEventListener('DOMContentLoaded',()=>{
    const runtime=window.__BombItControlsRuntime,shell=document.getElementById('shell');
    const owners=new Map(),held=new Map(),cancelled=new Set(),clicks=new Set();
    const preferenceKey='xanaduyonder.bomb-it.controls.v1';
    const validStyle=value=>value==='joystick'||value==='dpad';
    const codes={single:{left:37,up:38,right:39,down:40,bomb:32},first:{left:65,up:87,right:68,down:83,bomb:32},second:{left:37,up:38,right:39,down:40,bomb:13}};
    const actions=['right','down','left','up'];
    const pads=[1,2].map(player=>({player,stick:document.querySelector('.joystick[data-player="'+player+'"]'),
      ring:document.querySelector('.dpad[data-player="'+player+'"]'),bomb:document.querySelector('.p'+player+'-bomb'),
      label:document.querySelector('.controls.'+(player===1?'left':'right')+' .player-label')}));
    let mode='single',style='joystick',lastPhase=null,geometry='',zones=null,lastClick=null;
    try{const saved=localStorage.getItem(preferenceKey);if(validStyle(saved))style=saved;}catch(_){}
    document.body.dataset.input=style;
    function phase(){return runtime.phase();}
    function code(player,action){return (mode==='single'?codes.single:player===1?codes.first:codes.second)[action];}
    function consume(event){event.preventDefault();event.stopImmediatePropagation();}
    function rect(element){const b=element.getBoundingClientRect();return {left:b.left,top:b.top,right:b.right,bottom:b.bottom,width:b.width,height:b.height};}
    function contains(box,x,y,pad=0){return x>=box.left-pad&&x<=box.right+pad&&y>=box.top-pad&&y<=box.bottom+pad;}
    function containsBomb(element,x,y,pad=0){
      const box=rect(element);
      if(mode!=='single')return contains(box,x,y,pad);
      return Math.hypot(x-(box.left+box.right)/2,y-(box.top+box.bottom)/2)<=Math.min(box.width,box.height)/2+pad;
    }
    function overlaps(a,b){return a.left<b.right&&a.right>b.left&&a.top<b.bottom&&a.bottom>b.top;}
    function changeKey(owner,next){
      if(owner.code===next)return;
      if(owner.code!==null){const count=held.get(owner.code)-1;if(count>0)held.set(owner.code,count);else{held.delete(owner.code);runtime.key(owner.code,false);}}
      owner.code=null;
      if(next!==null){const count=held.get(next)||0;if(count||runtime.key(next,true)){held.set(next,count+1);owner.code=next;}}
    }
    function refreshBomb(element){element.classList.toggle('held',Array.from(owners.values()).some(o=>o.element===element));}
    function paintDirection(owner,action){
      owner.action=action;
      changeKey(owner,action?code(owner.player,action):null);
      if(owner.kind==='ring')for(const sector of owner.element.querySelectorAll('[data-action]'))sector.classList.toggle('held',sector.dataset.action===action);
    }
    function endPointer(id,forced=false){
      const owner=owners.get(id);if(!owner)return;
      changeKey(owner,null);owners.delete(id);if(forced)cancelled.add(id);
      if(owner.kind==='bomb')refreshBomb(owner.element);
      else{
        owner.element.classList.remove('held');owner.element.style.removeProperty('--stick-x');owner.element.style.removeProperty('--stick-y');
        for(const sector of owner.element.querySelectorAll('[data-action]'))sector.classList.remove('held');
        if(owner.kind==='stick'){
          owner.element.style.removeProperty('clip-path');
          place(owner.element,owner.home.x,owner.home.y,owner.home.size);
        }
      }
      if(shell.hasPointerCapture(id))shell.releasePointerCapture(id);
    }
    function release(){for(const id of Array.from(owners.keys()))endPointer(id,true);}
    function direction(owner,dx,dy){
      const angle=(Math.atan2(dy,dx)*180/Math.PI+360)%360;
      let next=Math.floor((angle+45)/90)%4;
      if(owner.action){const old=actions.indexOf(owner.action),difference=Math.abs(((angle-old*90+540)%360)-180);if(difference<=53)next=old;}
      return actions[next];
    }
    function move(owner,x,y){
      if(owner.kind==='bomb'){
        if(!containsBomb(owner.element,x,y))endPointer(owner.id,true);
        return;
      }
      const dx=x-owner.cx,dy=y-owner.cy,distance=Math.hypot(dx,dy);
      if(owner.kind==='stick'){
        const limit=owner.size*.25,scale=distance>limit?limit/distance:1;
        owner.element.style.setProperty('--stick-x',dx*scale+'px');owner.element.style.setProperty('--stick-y',dy*scale+'px');
        paintDirection(owner,distance<(owner.action?7:10)?null:direction(owner,dx,dy));
      }else{
        const logical=distance*144/owner.size;
        paintDirection(owner,logical<20||logical>72?null:direction(owner,dx,dy));
      }
    }
    function place(element,x,y,size,height=size){
      element.style.left=(x-size/2)+'px';element.style.top=(y-height/2)+'px';element.style.width=size+'px';element.style.height=height+'px';
    }
    function clipStick(owner){
      const size=owner.size,left=owner.cx-size/2,top=owner.cy-size/2;
      const hole={left:Math.max(0,zones.hud.left-left),top:Math.max(0,zones.hud.top-top),
        right:Math.min(size,zones.hud.right-left),bottom:Math.min(size,zones.hud.bottom-top)};
      if(hole.left>=hole.right||hole.top>=hole.bottom){owner.element.style.removeProperty('clip-path');return;}
      const id='stick-clip-'+owner.player;
      document.querySelector('#'+id+' path').setAttribute('d',
        'M0 0H'+size+'V'+size+'H0Z M'+hole.left+' '+hole.top+'H'+hole.right+'V'+hole.bottom+'H'+hole.left+'Z');
      owner.element.style.clipPath='url(#'+id+')';
    }
    function measure(){
      const areaStyle=getComputedStyle(document.getElementById('play-area')),railStyle=getComputedStyle(document.querySelector('.rail'));
      const number=(style,key)=>parseFloat(style.getPropertyValue(key))||0;
      const safe={left:number(areaStyle,'padding-left'),right:number(areaStyle,'padding-right'),top:number(railStyle,'padding-top'),bottom:number(railStyle,'padding-bottom')};
      const frame=rect(document.getElementById('player-frame'));
      const hud={left:frame.left,top:frame.top+frame.height*350/400,right:frame.left+frame.width*92/550,bottom:frame.bottom};
      const tools=Array.from(document.querySelectorAll('.tools')).map(rect);
      return {safe,frame,hud,tools,width:innerWidth,height:innerHeight,top:Math.max(...tools.map(b=>b.bottom))+6};
    }
    function update(){
      const nextPhase=phase(),next=measure();
      const signature=JSON.stringify([next,mode,style,nextPhase]);
      if(lastPhase!==nextPhase||geometry!==signature||runtime.blocked())release();
      document.body.dataset.phase=nextPhase;lastPhase=nextPhase;
      if(geometry===signature)return;
      geometry=signature;zones=next;
      const portrait=next.height>next.width;
      const small=next.height<=360,bombHeight=small?44:48,gap=small?6:12,bottom=next.height-next.safe.bottom-(small?4:8);
      const railBounds=[rect(document.querySelector('.rail.left')),rect(document.querySelector('.rail.right'))];
      for(const pad of pads){
        const rail=railBounds[pad.player-1];
        const sideCenter=(rail.left+rail.right)/2;
        // Keep the same homes in menus, character selection and battle. The
        // host reserves these edges without changing the SWF viewport size.
        const available=Math.max(44,bottom-next.top-bombHeight-gap);
        const size=Math.min(144,available);
        const x=!portrait?(pad.player===1?next.safe.left+size/2+4:next.width-next.safe.right-size/2-4):sideCenter;
        let y=bottom-bombHeight-gap-size/2;
        const box={left:x-size/2,right:x+size/2,top:y-size/2,bottom:y+size/2};
        if(overlaps(box,next.hud))y=Math.min(y,next.hud.top-size/2-8);
        const stickSize=Math.min(96,size);
        pad.home={x,y,size:stickSize};pad.ringHome={x,y,size};
        place(pad.stick,x,y,stickSize);place(pad.ring,x,y,size);
        pad.label.style.left=x+'px';pad.label.style.top=(y-size/2-22)+'px';
        pad.label.hidden=false;
        const labelBox=rect(pad.label);
        pad.label.hidden=labelBox.top<next.top||next.tools.some(tool=>overlaps(labelBox,tool))||overlaps(labelBox,next.hud);
        const fireSize=mode==='single'?(style==='dpad'?size:stickSize):Math.min(64,size);
        const fireHeight=mode==='single'?fireSize:bombHeight;
        // Single-player controls are equal circles on one horizontal line.
        // Duo bombs align with each group's outer edge, clear of the SWF HUD.
        const fireX=mode==='single'?x:x+(pad.player===1?-1:1)*(size-fireSize)/2;
        place(pad.bomb,fireX,mode==='single'?y:bottom-fireHeight/2,fireSize,fireHeight);
      }
    }
    function setStyle(value){
      if(!validStyle(value))return style;
      release();runtime.releaseInput();style=value;document.body.dataset.input=style;
      try{localStorage.setItem(preferenceKey,style);}catch(_){}
      geometry='';runtime.layout();update();if(!runtime.blocked())runtime.focus();return style;
    }
    function setMode(value){
      if(value!=='single'&&value!=='duo')return mode;
      if(mode!==value){release();runtime.releaseInput();mode=value;geometry='';}
      document.body.dataset.controls=mode;
      for(const button of document.querySelectorAll('[data-mode]'))button.setAttribute('aria-pressed',String(button.dataset.mode===mode));
      update();return mode;
    }
    function movementOwner(player){return Array.from(owners.values()).some(o=>o.player===player&&o.kind!=='bomb');}
    function begin(event,player,kind,element,home){
      consume(event);clicks.add(event.pointerId);
      if(kind!=='bomb'&&movementOwner(player)){cancelled.add(event.pointerId);return;}
      const owner={id:event.pointerId,player,kind,element,home,size:home?home.size:0,code:null,action:null};
      if(kind==='stick'){
        owner.cx=event.clientX;owner.cy=event.clientY;
        // The real contact remains the origin, even when its visual disk clips.
        place(element,owner.cx,owner.cy,owner.size);
        clipStick(owner);
      }else if(kind==='ring'){owner.cx=home.x;owner.cy=home.y;}
      owners.set(event.pointerId,owner);element.classList.add('held');shell.setPointerCapture(event.pointerId);
      if(kind==='bomb'){
        const key=code(player,'bomb'),firstPress=!held.has(key);
        changeKey(owner,key);
        if(firstPress&&owner.code===key&&typeof runtime.haptic==='function')runtime.haptic('bomb-press');
      }
      else if(kind==='ring')move(owner,event.clientX,event.clientY);
      runtime.focus();
    }
    window.addEventListener('pointerdown',event=>{
      clicks.delete(event.pointerId);cancelled.delete(event.pointerId);lastClick=null;
      if(runtime.blocked()||(event.pointerType==='mouse'&&event.button!==0))return;
      update();const x=event.clientX,y=event.clientY;
      if(event.composedPath().some(e=>e instanceof Element&&e.matches('.tools,#modal,.sheet,[data-ui-control]'))||contains(zones.hud,x,y))return;
      for(const pad of pads){
        if(pad.bomb.offsetWidth&&containsBomb(pad.bomb,x,y))return begin(event,pad.player,'bomb',pad.bomb,null);
      }
      const activePads=mode==='single'?[pads[0]]:pads;
      for(const pad of activePads){
        const home=style==='dpad'?pad.ringHome:pad.home;
        if(Math.hypot(x-home.x,y-home.y)<=home.size/2){
          return begin(event,pad.player,style==='dpad'?'ring':'stick',style==='dpad'?pad.ring:pad.stick,home);
        }
      }
      // System-gesture insets are not display cutouts: taps delivered to a
      // visible control still work. Only blank-edge floating origins opt out;
      // an OS gesture that takes over later releases through pointercancel.
      const gesture=typeof runtime.gestureInsets==='function'?runtime.gestureInsets():{};
      if(x<(gesture.left||0)||x>zones.width-(gesture.right||0)||y<(gesture.top||0)||y>zones.height-(gesture.bottom||0))return;
      if(style!=='joystick'||phase()!=='battle'||y<zones.top||x<zones.safe.left+4||x>zones.width-zones.safe.right-4||y>zones.height-zones.safe.bottom-4)return;
      for(const pad of pads)if(pad.bomb.offsetWidth&&containsBomb(pad.bomb,x,y,10))return;
      const player=x<zones.width/2?1:2;if(mode==='single'&&player!==1)return;
      const pad=pads[player-1];begin(event,player,'stick',pad.stick,pad.home);
    },{capture:true,passive:false});
    window.addEventListener('pointermove',event=>{
      const owner=owners.get(event.pointerId);
      if(cancelled.has(event.pointerId)){consume(event);return;}
      if(!owner)return;consume(event);if(runtime.blocked()){endPointer(event.pointerId,true);return;}move(owner,event.clientX,event.clientY);
    },{capture:true,passive:false});
    for(const type of ['pointerup','pointercancel','lostpointercapture'])window.addEventListener(type,event=>{
      if(!owners.has(event.pointerId)&&!cancelled.has(event.pointerId))return;
      consume(event);
      if(type==='pointerup'){lastClick={x:event.clientX,y:event.clientY,until:performance.now()+400};endPointer(event.pointerId);cancelled.delete(event.pointerId);}
      else if(type==='pointercancel'){endPointer(event.pointerId);cancelled.delete(event.pointerId);clicks.delete(event.pointerId);}
      else endPointer(event.pointerId,true);
    },{capture:true,passive:false});
    window.addEventListener('click',event=>{
      if(clicks.has(event.pointerId)||(lastClick&&performance.now()<lastClick.until&&Math.hypot(event.clientX-lastClick.x,event.clientY-lastClick.y)<10)){
        consume(event);clicks.delete(event.pointerId);lastClick=null;
      }
    },{capture:true,passive:false});
    for(const button of document.querySelectorAll('[data-mode]'))button.addEventListener('click',()=>runtime.requestMode(button.dataset.mode));
    window.addEventListener('resize',()=>{release();geometry='';update();});
    window.addEventListener('blur',release);
    document.addEventListener('visibilitychange',()=>{if(document.hidden)release();});
    runtime.attach({release,getStyle:()=>style,setStyle,setMode,update});update();
  });
})();
