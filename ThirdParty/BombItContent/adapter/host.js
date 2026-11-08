(function () {
  'use strict';
  window.RufflePlayer = window.RufflePlayer || {};
  // Start suspended so a host pause received during WASM loading cannot leak audio.
  window.RufflePlayer.config = {publicPath:'ruffle/',polyfills:false,autoplay:'off',unmuteOverlay:'hidden',
    splashScreen:false,contextMenu:'off',showSwfDownload:false,allowScriptAccess:false,
    allowNetworking:'internal',openUrlMode:'deny',socketProxy:[],credentialAllowList:[],
    backgroundExecutionMode:'none',scale:'showAll',forceScale:true,letterbox:'on',logLevel:'error'};

  let player=null, ready=false, error=null, active=true, audible=true, uiPaused=false, modalOpen=false, exitRequest=null, exited=false;
  let controls=null;
  const keys=new Set();
  const keyData={37:['ArrowLeft','ArrowLeft'],38:['ArrowUp','ArrowUp'],39:['ArrowRight','ArrowRight'],40:['ArrowDown','ArrowDown'],
    32:[' ','Space'],13:['Enter','Enter'],65:['a','KeyA'],87:['w','KeyW'],68:['d','KeyD'],83:['s','KeyS'],80:['p','KeyP']};
  function post(message) {
    if(window.BombItNative && typeof window.BombItNative.postMessage==='function') window.BombItNative.postMessage(JSON.stringify(message));
    else if(/Android|iPhone|iPad|iPod/.test(navigator.userAgent)) window.prompt('xanadu-bomb-it:'+JSON.stringify(message),'');
  }
  function api(){return player && player.ruffle();}
  function blocked(){return !ready || !active || document.hidden || uiPaused || modalOpen || !!exitRequest || exited || !!error;}
  function focus(){if(player && !exited)player.focus({preventScroll:true});}
  function key(code,down) {
    if(down && blocked())return false;
    if(!down && !keys.has(code))return false;
    if(down && keys.has(code))return true;
    focus();
    const descriptor=keyData[code];if(!descriptor)return false;
    const event=new KeyboardEvent(down?'keydown':'keyup',{key:descriptor[0],code:descriptor[1],keyCode:code,which:code,bubbles:true,cancelable:true});
    // Legacy Flash keyboard reads use these values; WebKit's constructor may ignore them.
    for(const name of ['keyCode','which'])if(event[name]!==code)Object.defineProperty(event,name,{get:()=>code});
    if(down)keys.add(code);else keys.delete(code);
    window.dispatchEvent(event);
    return true;
  }
  function releaseInput(){if(controls)controls.release();for(const code of Array.from(keys))key(code,false);}
  function apply(){
    const stopped=blocked();
    const frame=document.getElementById('player-frame');if(frame)frame.style.pointerEvents=stopped?'none':'auto';
    if(stopped)releaseInput();
    const r=api();if(!r || !ready || exited)return;
    r.volume=(!blocked() && audible)?1:0;
    if(blocked())r.suspend();else r.resume();
  }
  function fail(cause){
    error=String(cause && cause.message || cause || '离线游戏未能打开');releaseInput();
    try{if(api()){api().volume=0;api().suspend();}}catch(_){}
    const loading=document.getElementById('loading');if(loading){loading.hidden=false;loading.textContent='游戏暂时无法打开，请返回大厅重试。';}
    post({type:'error',message:error});
  }
  function layout(){
    const region=document.getElementById('game-region'),frame=document.getElementById('player-frame');if(!region||!frame)return;
    const metadata=api() && api().metadata;
    const ratio=metadata && metadata.width && metadata.height?metadata.width/metadata.height:550/400;
    let width;
    if(window.matchMedia('(orientation:landscape)').matches){
      const area=document.getElementById('play-area'),style=getComputedStyle(area),bounds=area.getBoundingClientRect();
      const value=name=>parseFloat(style.getPropertyValue(name))||0;
      // Reserve usable controls and cutout insets, then give the complete height
      // to the original stage. Bottom gesture space belongs to the side controls.
      const available=bounds.width-value('padding-left')-value('padding-right')-2*value('column-gap')-2*value('--rail-min');
      width=Math.max(1,Math.min(available,bounds.height*ratio));
      area.style.setProperty('--stage-width',width+'px');
    }else{
      const bounds=region.getBoundingClientRect();width=Math.max(1,Math.min(bounds.width,bounds.height*ratio));
    }
    frame.style.width=width+'px';frame.style.height=(width/ratio)+'px';
  }
  function updateInputSettings(){
    const style=controls?controls.getStyle():'joystick';
    for(const button of document.querySelectorAll('[data-input-style]')){
      button.setAttribute('aria-pressed',String(button.dataset.inputStyle===style));button.disabled=!controls;
    }
  }
  function closeModal(){modalOpen=false;document.getElementById('modal').hidden=true;apply();focus();}
  function showModal(kind){
    releaseInput();modalOpen=true;apply();
    const modal=document.getElementById('modal'),title=document.getElementById('modal-title'),copy=document.getElementById('modal-copy');
    const confirm=document.getElementById('modal-confirm'),cancel=document.getElementById('modal-cancel');
    title.textContent=kind==='exit'?'返回大厅？':kind==='settings'?'设置':'玩法说明';
    copy.replaceChildren();
    document.getElementById('input-settings').hidden=kind!=='settings';updateInputSettings();
    const paragraphs=kind==='exit'?['当前对局不会保存，下次进入将从原版菜单开始。']:kind==='settings'?['操作方式','摇杆或方向按键均可用于单人、双人，下次进入会保留选择。']:['在原版菜单选择单人或双人、难度和角色。左侧单人／双人头像只切换手机操作布局，请与游戏模式保持一致。',
      '单人：左侧移动，右侧放弹。双人：左右各一套移动和放弹操作，可同时游玩。点击齿轮可切换摇杆或上下左右按键。',
      '摇杆按四方向移动，松手停止；方向按键可按住或滑动换向。选角色时移动再松手切换，点放弹确认，双人须分别确认。菜单也可以直接触摸。',
      '炸开障碍、收集道具，避开自己和对手的爆炸。切后台会暂停；返回大厅或结束应用后，当前对局会重开。'];
    for(const text of paragraphs){const p=document.createElement('p');p.textContent=text;copy.append(p);}
    cancel.hidden=kind!=='exit';cancel.onclick=closeModal;confirm.textContent=kind==='exit'?'返回大厅':kind==='settings'?'完成':'知道了';
    confirm.onclick=()=>{if(kind==='exit'){post({type:'exit-intent'});if(!window.BombItNative&&!/Android|iPhone|iPad|iPod/.test(navigator.userAgent))closeModal();}else closeModal();};
    modal.hidden=false;
  }
  window.BombItHost=Object.freeze({
    setHostState(value,sound){active=!!value;audible=!!sound;apply();},
    releaseInput,
    setViewportInsets(left,top,right,bottom){for(const [side,value] of Object.entries({left,top,right,bottom}))document.documentElement.style.setProperty('--safe-'+side,Math.max(0,Number(value)||0)+'px');layout();},
    status(){return {ready,error,active:!blocked(),exited};},
    requestExit(id){
      if(exited){post({type:'exit-ready',requestId:String(id)});return;}
      if(exitRequest)return;
      exitRequest=String(id);releaseInput();apply();
      try{
        // Removing the element invokes Ruffle's destroy and SharedObject flush.
        // This preserves only saves the original SWF writes, never a battle snapshot.
        if(player)player.remove();
        exited=true;ready=false;post({type:'exit-ready',requestId:exitRequest});
      }catch(e){const request=exitRequest;exitRequest=null;post({type:'exit-error',requestId:request,message:String(e)});}
    },
    cancelExit(){if(!exited){exitRequest=null;modalOpen=false;document.getElementById('modal').hidden=true;apply();}},
  });
  window.__BombItControlsRuntime={key,releaseInput,focus,blocked,attach(value){controls=value;updateInputSettings();},layout};
  window.addEventListener('blur',releaseInput);
  document.addEventListener('visibilitychange',apply);
  window.addEventListener('pagehide',()=>{active=false;apply();});
  window.addEventListener('resize',layout);
  document.addEventListener('contextmenu',e=>e.preventDefault());
  document.addEventListener('DOMContentLoaded',async()=>{
    document.getElementById('hall').onclick=()=>showModal('exit');
    document.getElementById('help').onclick=()=>showModal('help');
    document.getElementById('settings').onclick=()=>showModal('settings');
    for(const button of document.querySelectorAll('[data-input-style]'))button.onclick=()=>{
      if(controls)controls.setStyle(button.dataset.inputStyle);updateInputSettings();
    };
    document.getElementById('pause').onclick=()=>{
      uiPaused=!uiPaused;const b=document.getElementById('pause');
      b.setAttribute('aria-label',uiPaused?'继续':'暂停');b.title=uiPaused?'继续':'暂停';
      b.setAttribute('aria-pressed',String(uiPaused));apply();if(!uiPaused)focus();
    };
    try{
      if(!window.RufflePlayer.newest)throw new Error('Ruffle runtime missing');
      player=window.RufflePlayer.newest().createPlayer();player.id='bombit-player';player.tabIndex=0;
      document.getElementById('player-frame').append(player);layout();
      player.addEventListener('loadeddata',()=>{
        if(exited)return;ready=true;document.getElementById('loading').hidden=true;layout();apply();post({type:'ready'});
      },{once:true});
      await api().load({url:'game.swf'});
      if(api().readyState===2 && !ready){ready=true;document.getElementById('loading').hidden=true;layout();apply();post({type:'ready'});}
    }catch(e){fail(e);}
  });
})();
