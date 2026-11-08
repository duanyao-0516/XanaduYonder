(function () {
  'use strict';
  document.addEventListener('DOMContentLoaded',()=>{
    const runtime=window.__BombItControlsRuntime,owners=new Map(),held=new Map();
    const preferenceKey='xanaduyonder.bomb-it.controls.v1';
    const validStyle=value=>value==='joystick'||value==='dpad';
    let mode='single',style='joystick';
    try{const saved=localStorage.getItem(preferenceKey);if(validStyle(saved))style=saved;}catch(_){}
    document.body.dataset.input=style;
    const codes={arrows:{left:37,up:38,right:39,down:40,bomb:32},wasd:{left:65,up:87,right:68,down:83,bomb:32},second:{left:37,up:38,right:39,down:40,bomb:13}};
    function mapping(element,action){
      const table=mode==='single'?codes.arrows:Number(element.dataset.player)===1?codes.wasd:codes.second;
      return table[action];
    }
    function changeKey(owner,code){
      if(owner.code===code)return;
      if(owner.code!==null){const count=held.get(owner.code)-1;if(count>0)held.set(owner.code,count);else{held.delete(owner.code);runtime.key(owner.code,false);}}
      owner.code=null;
      if(code!==null){const count=held.get(code)||0;if(count || runtime.key(code,true)){held.set(code,count+1);owner.code=code;}}
    }
    function refresh(element){
      if(!element)return;
      const pressed=Array.from(owners.values()).some(owner=>owner.element===element);
      element.classList.toggle('held',pressed);
      if(!pressed){element.style.removeProperty('--stick-x');element.style.removeProperty('--stick-y');}
    }
    function releasePointer(id){
      const owner=owners.get(id);if(!owner)return;
      changeKey(owner,null);owners.delete(id);
      refresh(owner.element);
    }
    function release(){for(const id of Array.from(owners.keys()))releasePointer(id);}
    function moveStick(owner,x,y){
      const dx=x-owner.cx,dy=y-owner.cy,distance=Math.hypot(dx,dy),limit=owner.radius*.45;
      const scale=distance>limit?limit/distance:1;
      owner.element.style.setProperty('--stick-x',dx*scale+'px');owner.element.style.setProperty('--stick-y',dy*scale+'px');
      // Preserve original four-direction movement and leave a center dead zone.
      let action=null;
      if(distance>=owner.radius*.24)action=Math.abs(dx)>=Math.abs(dy)?(dx<0?'left':'right'):(dy<0?'up':'down');
      changeKey(owner,action?mapping(owner.element,action):null);
    }
    function moveDirection(owner,x,y){
      const hit=document.elementFromPoint(x,y),candidate=hit&&hit.closest('button[data-action]');
      const next=candidate&&candidate.dataset.action!=='bomb'&&candidate.dataset.player===owner.player&&
        (candidate.closest('.dpad')||candidate.parentElement)===owner.pad?candidate:null;
      const previous=owner.element;
      owner.element=next;
      changeKey(owner,next?mapping(next,next.dataset.action):null);
      refresh(previous);refresh(next);
      // Keep the captured pointer alive in gaps and outside the pad, so the
      // same thumb can return or slide across directions until it is lifted.
    }
    function setStyle(value){
      if(!validStyle(value))return style;
      release();runtime.releaseInput();style=value;document.body.dataset.input=style;
      try{localStorage.setItem(preferenceKey,style);}catch(_){}
      runtime.layout();if(!runtime.blocked())runtime.focus();return style;
    }
    document.querySelectorAll('.joystick,button[data-action]').forEach(element=>{
      element.addEventListener('pointerdown',event=>{
        if(runtime.blocked() || (event.pointerType==='mouse' && event.button!==0))return;
        event.preventDefault();
        const stick=element.classList.contains('joystick');
        if(stick && Array.from(owners.values()).some(owner=>owner.element===element))return;
        releasePointer(event.pointerId);
        const bounds=element.getBoundingClientRect();
        const owner={element,stick,code:null,player:element.dataset.player,
          pad:!stick&&element.dataset.action!=='bomb'?(element.closest('.dpad')||element.parentElement):null,
          cx:bounds.x+bounds.width/2,cy:bounds.y+bounds.height/2,radius:bounds.width/2};
        owners.set(event.pointerId,owner);element.classList.add('held');element.setPointerCapture(event.pointerId);
        if(stick)moveStick(owner,event.clientX,event.clientY);else changeKey(owner,mapping(element,element.dataset.action));
      });
      element.addEventListener('pointermove',event=>{
        const owner=owners.get(event.pointerId);if(!owner)return;event.preventDefault();
        if(owner.stick)moveStick(owner,event.clientX,event.clientY);
        else if(owner.pad)moveDirection(owner,event.clientX,event.clientY);
        else{const b=element.getBoundingClientRect();if(event.clientX<b.left||event.clientX>b.right||event.clientY<b.top||event.clientY>b.bottom)releasePointer(event.pointerId);}
      });
      for(const type of ['pointerup','pointercancel','lostpointercapture'])element.addEventListener(type,event=>{event.preventDefault();releasePointer(event.pointerId);});
      element.addEventListener('click',event=>event.preventDefault());
    });
    document.querySelectorAll('[data-mode]').forEach(button=>button.addEventListener('click',()=>{
      release();runtime.releaseInput();mode=button.dataset.mode;document.body.dataset.controls=mode;
      document.querySelectorAll('[data-mode]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));
      runtime.layout();runtime.focus();
    }));
    window.addEventListener('resize',release);
    runtime.attach({release,getStyle:()=>style,setStyle});runtime.layout();
  });
})();
