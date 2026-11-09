/* Menu chrome owns global feedback requests; native owns durable preferences. */
(function(){
  'use strict';
  let runtime=null,token=null,sound=true,haptics=true,sequence=0,pressId=0,pending=null,timer=null,desired=null,warning='',modal=null,dom=null,lastRender='';
  const valid=v=>v&&typeof v.token==='string'&&v.token.length>0&&typeof v.soundEnabled==='boolean'&&typeof v.hapticsEnabled==='boolean';
  function sync(persist=false){if(runtime)runtime.sound(sound,persist);}
  function render(){
    if(!dom)return;
    const state=runtime&&runtime.status();
    const signature=JSON.stringify([!!(state&&state.menuAvailable),token,sound,haptics,pending,warning,modal]);if(signature===lastRender)return;lastRender=signature;
    dom.tools.hidden=!(state&&state.menuAvailable);
    for(const [button,on] of [[dom.sound,sound],[dom.haptics,haptics]]){button.setAttribute('aria-checked',String(on));button.disabled=!token||!!pending;button.querySelector('span').textContent=on?'开启':'关闭';}
    dom.warning.textContent=pending?'正在保存…':warning;dom.warning.hidden=!dom.warning.textContent;
    dom.retry.hidden=!warning||!!pending;dom.dialog.hidden=!modal;
  }
  function request(nextSound,nextHaptics,force=false){
    if(!token||pending||!runtime)return;
    if(!force&&nextSound===sound&&nextHaptics===haptics)return;
    desired={soundEnabled:nextSound,hapticsEnabled:nextHaptics};pending='feedback-'+(++sequence);warning='';render();
    const requestId=pending;timer=setTimeout(()=>{if(pending===requestId){pending=null;warning='设置保存尚未确认，请重试。';render();}},5000);
    runtime.send('feedback-settings',{token,requestId,...desired});
  }
  function close(){modal=null;if(runtime)runtime.modal(false);render();}
  function open(kind){
    if(!runtime||!runtime.status().menuAvailable)return;
    modal=kind;dom.dialog.dataset.kind=kind;runtime.modal(true);dom.title.textContent=kind==='settings'?'设置':'玩法说明';
    dom.settings.hidden=kind!=='settings';dom.guide.hidden=kind!=='help';render();
  }
  window.__levelDevilMenu=Object.freeze({
    attach(value){runtime=value;sync();render();},
    configure(value){
      if(!valid(value))return;
      if(token!==value.token){clearTimeout(timer);pending=null;desired=null;sequence=pressId=0;token=value.token;}
      sound=value.soundEnabled;haptics=value.hapticsEnabled;warning='';sync();render();
    },
    result(value){
      if(!valid(value)||value.token!==token||value.requestId!==pending||typeof value.ok!=='boolean')return;
      clearTimeout(timer);pending=null;sound=value.soundEnabled;haptics=value.hapticsEnabled;
      warning=value.ok?'':'设置保存尚未确认，请重试。';if(value.ok)desired=null;sync(true);render();
    },
    refresh:render,engineReady(){sync();render();},close,
    originalSound(){request(!sound,haptics);},
    haptic(){if(runtime&&token&&haptics&&runtime.status().active&&runtime.status().gameplayActive)runtime.send('haptic',{token,kind:'jump-press',pressId:++pressId});},
    soundEnabled:()=>sound
  });
  document.addEventListener('DOMContentLoaded',()=>{
    dom={tools:document.getElementById('level-menu-tools'),dialog:document.getElementById('level-menu-dialog'),title:document.getElementById('level-menu-title'),settings:document.getElementById('level-menu-settings'),guide:document.getElementById('level-menu-guide'),sound:document.getElementById('level-sound'),haptics:document.getElementById('level-haptics'),warning:document.getElementById('level-feedback-status'),retry:document.getElementById('level-feedback-retry')};
    document.getElementById('level-help').onclick=()=>open('help');document.getElementById('level-settings').onclick=()=>open('settings');
    document.getElementById('level-menu-close').onclick=close;document.getElementById('level-menu-done').onclick=close;
    dom.sound.onclick=()=>request(!sound,haptics);dom.haptics.onclick=()=>request(sound,!haptics);
    dom.retry.onclick=()=>{if(desired)request(desired.soundEnabled,desired.hapticsEnabled,true);};
    let backdropDown=false;
    dom.dialog.addEventListener('pointerdown',e=>{backdropDown=e.target===dom.dialog;});
    dom.dialog.addEventListener('click',e=>{if(backdropDown&&e.target===dom.dialog)close();backdropDown=false;});
    // This layer sits outside OpenFL. None of its interactions reach the engine
    // mouse/touch listeners, including the release that closes the modal.
    for(const type of ['keydown','keyup','mousedown','mouseup','mousemove','touchstart','touchmove','touchend','touchcancel','pointerdown','pointerup','click'])document.getElementById('level-menu-ui').addEventListener(type,e=>e.stopPropagation());
    render();
  });
})();
