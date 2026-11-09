(function () {
  'use strict';
  window.RufflePlayer = window.RufflePlayer || {};
  // Ruffle 0.7.1 couples ExternalInterface to networking="all". Only the
  // hash-locked local bridge uses script access; CSP, denied navigation, native
  // manifest allowlists and the permission-free APK still block network exits.
  window.RufflePlayer.config = {publicPath:'ruffle/',polyfills:false,autoplay:'off',unmuteOverlay:'hidden',
    splashScreen:false,contextMenu:'off',showSwfDownload:false,allowScriptAccess:true,
    allowNetworking:'all',openUrlMode:'deny',socketProxy:[],credentialAllowList:[],
    backgroundExecutionMode:'none',scale:'showAll',forceScale:true,letterbox:'on',logLevel:'error'};

  let player=null, ready=false, error=null, active=true, audible=true, resumeRequired=false;
  let controls=null, game=null, modalKind=null, exitRequest=null, exited=false, generation=0;
  let bootTimer=null, applying=false, scheduled=false, resumePointerArmed=false;
  let gestures={left:0,top:0,right:0,bottom:0};
  const keys=new Set();
  const phases=new Set(['loading','menu','difficulty','select','game','gameover']);
  const difficultyNames={easy:'简单',normal:'普通',hard:'困难'};
  const keyData={37:['ArrowLeft','ArrowLeft'],38:['ArrowUp','ArrowUp'],39:['ArrowRight','ArrowRight'],40:['ArrowDown','ArrowDown'],
    32:[' ','Space'],13:['Enter','Enter'],65:['a','KeyA'],87:['w','KeyW'],68:['d','KeyD'],83:['s','KeyS'],80:['p','KeyP']};
  const feedback=window.createBombItFeedback({post,state:()=>game,
    setSound:value=>invoke('xanaduSetSoundEnabled',value),changed:scheduleApply,
    canHaptic:()=>phase()==='battle'&&!blocked()});

  function post(message) {
    if(window.BombItNative && typeof window.BombItNative.postMessage==='function') window.BombItNative.postMessage(JSON.stringify(message));
    else if(/Android|iPhone|iPad|iPod/.test(navigator.userAgent)) window.prompt('xanadu-bomb-it:'+JSON.stringify(message),'');
  }
  function api(){return player && player.ruffle();}
  function phase(){
    if(!ready || !game)return 'loading';
    return game.phase==='game'?'battle':game.phase==='gameover'?'result':game.phase==='select'?'select':'menu';
  }
  function hostStopped(){return !active || document.hidden || resumeRequired || !!modalKind || !!exitRequest || exited || !!error;}
  function blocked(){return !ready || hostStopped() || !!(game && (game.manualPaused||game.restoring));}
  function focus(){if(player && !exited && !modalKind)player.focus({preventScroll:true});}
  function key(code,down) {
    if(down && blocked())return false;
    if(!down && !keys.has(code))return false;
    if(down && keys.has(code))return true;
    const descriptor=keyData[code];if(!descriptor)return false;
    focus();
    const event=new KeyboardEvent(down?'keydown':'keyup',{key:descriptor[0],code:descriptor[1],keyCode:code,bubbles:true,cancelable:true});
    for(const name of ['keyCode','which'])if(event[name]!==code)Object.defineProperty(event,name,{get:()=>code});
    if(down)keys.add(code);else keys.delete(code);
    window.dispatchEvent(event);return true;
  }
  function releaseInput(){if(controls)controls.release();for(const code of Array.from(keys))key(code,false);}
  function acceptState(value) {
    if(!value || value.version!==1 || String(value.session)!==String(generation) || !phases.has(value.phase))return false;
    if(![0,1,2].includes(value.mode) || typeof value.manualPaused!=='boolean' || typeof value.hostPaused!=='boolean')return false;
    if(!game || value.phase!==game.phase || value.mode!==game.mode || value.paused!==game.paused)releaseInput();
    const previous=game,previousMode=game && game.mode;game=value;
    if(controls && value.mode!==previousMode && (value.mode===1 || value.mode===2))controls.setMode(value.mode===1?'single':'duo');
    feedback.observe(value,previous);
    return true;
  }
  function invoke(name,...args) {
    const r=api();if(!r)return null;
    const value=r.callExternalInterface(name,...args);
    if(!acceptState(value))throw new Error('Offline game bridge returned invalid state');
    return value;
  }
  function updateUi() {
    const pause=document.getElementById('pause'),paused=resumeRequired || !!(game && game.manualPaused);
    if(pause){pause.setAttribute('aria-pressed',String(paused));pause.setAttribute('aria-label',paused?'继续游戏':'暂停');pause.title=paused?'继续游戏':'暂停';pause.disabled=!ready || !!exitRequest || !!error;}
    if(controls && controls.update)controls.update();
    for(const button of document.querySelectorAll('[data-mode]')){
      button.setAttribute('aria-pressed',String(!!game && game.mode===(button.dataset.mode==='duo'?2:1)));
      button.disabled=!ready || !!exitRequest || !!error;
    }
    const frame=document.getElementById('player-frame');
    // Original HUD remains accessible for manual pause; modal/background blocks it.
    if(frame)frame.style.pointerEvents=ready&&!hostStopped()&&!game.restoring?'auto':'none';
    const resume=document.getElementById('continue-run'),info=game&&game.continueInfo;
    if(resume){
      resume.hidden=!(ready&&game.phase==='menu'&&info&&!game.restoring);
      resume.disabled=blocked();
      if(info)resume.textContent='继续闯关 · '+difficultyNames[info.difficulty]+' · 第'+info.stage+'/'+info.totalStages+'关';
    }
    const warning=document.getElementById('save-warning');
    if(warning)warning.hidden=!(ready&&game.saveOk===false);
    feedback.render();
    updateResumeHit();
  }
  function updateResumeHit(){
    const hit=document.getElementById('original-pause-resume'),frame=document.getElementById('player-frame');
    if(!hit || !frame)return;
    hit.hidden=!(ready && game && game.phase==='game' && game.manualPaused && !hostStopped());
    if(hit.hidden){resumePointerArmed=false;return;}
    // Original symbol 533's exact transformed bounds in the 550 x 400 stage.
    // Suspended Ruffle cannot dispatch its mouse event, so mirror only resume.
    const b=frame.getBoundingClientRect();
    hit.style.left=(b.left+b.width*48.15/550)+'px';hit.style.top=(b.top+b.height*360.65/400)+'px';
    hit.style.width=(b.width*39.65/550)+'px';hit.style.height=(b.height*19.8/400)+'px';
  }
  function apply() {
    if(applying)return;applying=true;
    try{
      if(blocked())releaseInput();
      const r=api();
      if(r && ready && !exited && !error){
        const stop=hostStopped();
        if(game.hostPaused!==stop)invoke('xanaduSetHostPaused',stop);
        r.volume=!blocked()&&audible&&feedback.soundEnabled()?1:0;
        // Restoration needs original timeline ticks; only inputs stay blocked.
        if(hostStopped()||game.manualPaused)r.suspend();else r.resume();
      }
      updateUi();
    }catch(cause){fail(cause);}finally{applying=false;}
  }
  function scheduleApply(){
    if(scheduled)return;scheduled=true;
    queueMicrotask(()=>{scheduled=false;if(!exited)apply();});
  }
  function fail(cause){
    if(error || exited)return;
    error=String(cause && cause.message || cause || '离线游戏未能打开');
    clearTimeout(bootTimer);releaseInput();
    try{if(api()){api().volume=0;api().suspend();}}catch(_){}
    const loading=document.getElementById('loading');
    if(loading){loading.hidden=false;loading.textContent='游戏暂时无法打开，请返回大厅重试。';}
    updateUi();post({type:'error',message:error});
  }
  function layout(){
    releaseInput();
    const region=document.getElementById('game-region'),frame=document.getElementById('player-frame');if(!region||!frame)return;
    const metadata=api() && api().metadata;
    const ratio=metadata && metadata.width && metadata.height?metadata.width/metadata.height:550/400;
    let width,shift=0;
    if(window.matchMedia('(orientation:landscape)').matches){
      const area=document.getElementById('play-area'),style=getComputedStyle(area),bounds=area.getBoundingClientRect();
      const value=name=>parseFloat(style.getPropertyValue(name))||0;
      // Fixed side allowance, independent of input style and player count.
      const available=bounds.width-value('padding-left')-value('padding-right')-2*value('column-gap')-188;
      width=Math.max(1,Math.min(available,bounds.height*ratio));area.style.setProperty('--stage-width',width+'px');
      const rail=(available+188-width)/2;
      shift=Math.min(24,Math.max(0,(144-rail)/2));
    }else{
      const bounds=region.getBoundingClientRect();width=Math.max(1,Math.min(bounds.width,bounds.height*ratio));
    }
    frame.style.width=width+'px';frame.style.height=(width/ratio)+'px';
    frame.style.transform='translateX('+(-shift)+'px)';
    if(controls && controls.update)controls.update();
    updateResumeHit();
  }
  function updateInputSettings(){
    const style=controls?controls.getStyle():'joystick';
    for(const button of document.querySelectorAll('[data-input-style]')){
      button.setAttribute('aria-pressed',String(button.dataset.inputStyle===style));button.disabled=!controls;
    }
  }
  function closeModal(){modalKind=null;document.getElementById('modal').hidden=true;apply();if(!blocked())focus();}
  function showModal(kind,target){
    if(exited || exitRequest)return;
    releaseInput();modalKind=kind;apply();
    const modal=document.getElementById('modal'),title=document.getElementById('modal-title'),copy=document.getElementById('modal-copy');
    const confirm=document.getElementById('modal-confirm'),cancel=document.getElementById('modal-cancel');
    const modeName=target==='duo'?'双人':'单人';
    modal.dataset.kind=kind;
    // Help's action follows the text and scrolls with it. Confirmations and
    // settings keep their actions visible even if their content must scroll.
    modal.querySelector(kind==='help'?'.sheet-scroll':'.sheet').append(modal.querySelector('.sheet-actions'));
    title.textContent=kind==='exit'?'返回大厅？':kind==='settings'?'设置':kind==='mode'?'切换为'+modeName+'并重新开始？':kind==='continue'?'继续闯关':kind==='save-error'?'进度尚未保存':'玩法说明';
    copy.replaceChildren();document.getElementById('settings-content').hidden=kind!=='settings';updateInputSettings();
    const info=game&&game.continueInfo,difficulty=info&&difficultyNames[info.difficulty];
    const paragraphs=kind==='exit'?['返回大厅前会保存闯关进度。下次从本关开局继续，地图重新生成；本关中途的位置和道具不保留。']:
      kind==='mode'?['确认后进入'+modeName+'模式的难度与角色选择。新对局正式开始时，将替换原闯关存档。']:
      kind==='continue'&&info?[difficulty+' · '+(info.mode===2?'双人':'单人')+' · 第 '+info.stage+' / '+info.totalStages+' 关',
        '玩家一：生命 '+info.life1+'，累计分 '+info.totalScore1+(info.mode===2?'；玩家二：生命 '+info.life2+'，累计分 '+info.totalScore2:'')+'。',
        '本关地图重新生成，从开局继续；不会恢复退出瞬间的位置、炸弹和剩余时间。']:
      kind==='save-error'?['未能确认闯关进度已保存，请留在游戏重试。现有对局不会被关闭。']:[];
    for(const text of paragraphs){const p=document.createElement('p');p.textContent=text;copy.append(p);}
    if(kind==='help')window.renderBombItGuide(copy);
    cancel.hidden=!['exit','mode','continue'].includes(kind);cancel.textContent='取消';cancel.onclick=closeModal;
    confirm.textContent=kind==='exit'?'保存并返回':kind==='mode'?'切换并重开':kind==='continue'?'从本关开局继续':kind==='settings'?'完成':'知道了';
    confirm.onclick=()=>{
      if(kind==='mode'){modalKind=null;modal.hidden=true;startPlayer(target);}
      else if(kind==='continue'){
        modalKind=null;modal.hidden=true;
        try{invoke('xanaduSetHostPaused',false);invoke('xanaduContinue');apply();}catch(cause){fail(cause);}
      }
      else if(kind==='exit'){post({type:'exit-intent'});if(!window.BombItNative&&!/Android|iPhone|iPad|iPod/.test(navigator.userAgent))closeModal();}
      else closeModal();
    };
    modal.hidden=false;
    document.querySelector('.sheet-scroll').scrollTop=0;feedback.render();
  }
  function requestMode(mode){
    if(!ready || !active || document.hidden || modalKind || exitRequest || error)return;
    if(mode!=='single' && mode!=='duo')return;
    if(game.mode===(mode==='duo'?2:1))return;
    showModal('mode',mode);
  }
  function togglePause(){
    if(!ready || modalKind || exitRequest || error || !active || document.hidden)return;
    releaseInput();
    try{
      const paused=resumeRequired || game.manualPaused;resumeRequired=false;
      invoke('xanaduSetManualPaused',!paused);apply();if(!blocked())focus();
    }catch(cause){fail(cause);}
  }
  function background(){if(ready && phase()==='battle')resumeRequired=true;releaseInput();}
  function bridgeReady(value,token){
    if(token!==generation || exited || error || !acceptState(value))return;
    if(!ready && value.booted){
      ready=true;clearTimeout(bootTimer);document.getElementById('loading').hidden=true;layout();post({type:'ready'});
    }
    scheduleApply();
  }
  // Ruffle's outbound ExternalInterface uses eval. Its fixed FSCommand handler
  // gives us a notification without weakening CSP or reentering the AVM1 call.
  function stateChanged(token){
    queueMicrotask(()=>{
      if(token!==generation || exited || error || !api())return;
      // A notification can be queued before a later menu/pause transition.
      // Read the current engine state rather than applying an older snapshot.
      try{bridgeReady(api().callExternalInterface('xanaduState'),token);}catch(cause){fail(cause);}
    });
  }
  async function startPlayer(mode){
    if(player&&ready&&!exited){
      try{if(!invoke('xanaduFlushSave').saveOk){showModal('save-error');return;}}
      catch(cause){showModal('save-error');return;}
    }
    const token=++generation;
    clearTimeout(bootTimer);releaseInput();ready=false;game=null;error=null;resumeRequired=false;
    try{
      if(player){player.remove();player=null;}
      document.getElementById('loading').hidden=false;document.getElementById('loading').textContent='正在打开离线游戏…';updateUi();
      if(!window.RufflePlayer.newest)throw new Error('Ruffle runtime missing');
      const created=window.RufflePlayer.newest().createPlayer();player=created;created.id='bombit-player';created.tabIndex=0;
      document.getElementById('player-frame').append(created);layout();
      const r=created.ruffle();let bootStarted=false,elapsed=0;
      r.addFSCommandHandler((command,args)=>{
        if(command==='xanadu-state' && args==='')stateChanged(token);
      });
      const boot=()=>{
        if(bootStarted || token!==generation || exited || error)return;bootStarted=true;r.volume=0;r.resume();
        const poll=()=>{
          if(token!==generation || exited || error || ready)return;
          try{const value=r.callExternalInterface('xanaduState');if(value)bridgeReady(value,token);}catch(cause){fail(cause);return;}
          if(ready)return;
          if(active&&!document.hidden)elapsed+=100;
          if(elapsed>=30000){fail(new Error('Offline game bridge initialization timed out'));return;}
          bootTimer=setTimeout(poll,100);
        };
        bootTimer=setTimeout(poll,100);
      };
      created.addEventListener('loadeddata',boot,{once:true});
      const parameters={xanaduSession:String(token),xanaduSoundEnabled:feedback.soundEnabled()?'1':'0'};if(mode)parameters.xanaduMode=mode==='duo'?'2':'1';
      await r.load({url:'game.swf',parameters});
      if(token!==generation || exited)return;if(r.readyState===2)boot();
    }catch(cause){if(token===generation)fail(cause);}
  }
  window.BombItHost=Object.freeze({
    configureFeedback:feedback.configure,feedbackSettingsResult:feedback.result,
    setHostState(value,sound){if(!value)background();active=!!value;audible=!!sound;apply();},
    releaseInput,
    setViewportInsets(left,top,right,bottom){
      releaseInput();for(const [side,value] of Object.entries({left,top,right,bottom}))document.documentElement.style.setProperty('--safe-'+side,Math.max(0,Number(value)||0)+'px');layout();
    },
    setGestureInsets(left,top,right,bottom){
      releaseInput();gestures=Object.fromEntries(Object.entries({left,top,right,bottom}).map(([k,v])=>[k,Math.max(0,Number(v)||0)]));
    },
    status(){return {ready,error,active:!blocked(),exited,generation,phase:phase(),resumeRequired,game};},
    requestExit(id){
      if(exited){post({type:'exit-ready',requestId:String(id)});return;}
      if(exitRequest)return;
      exitRequest=String(id);releaseInput();apply();
      try{
        if(ready&&!invoke('xanaduFlushSave').saveOk)throw new Error('闯关进度未能保存');
        if(player)player.remove();player=null;
        ++generation;clearTimeout(bootTimer);
        exited=true;ready=false;post({type:'exit-ready',requestId:exitRequest});
      }catch(cause){const request=exitRequest;exitRequest=null;post({type:'exit-error',requestId:request,message:String(cause)});}
    },
    cancelExit(){if(!exited){exitRequest=null;modalKind=null;document.getElementById('modal').hidden=true;apply();}}
  });
  window.__BombItControlsRuntime={key,releaseInput,focus,blocked,phase,requestMode,haptic:feedback.haptic,gestureInsets:()=>gestures,
    attach(value){controls=value;updateInputSettings();updateUi();},layout};
  window.addEventListener('blur',releaseInput);
  document.addEventListener('visibilitychange',()=>{if(document.hidden)background();apply();});
  window.addEventListener('pagehide',()=>{background();active=false;apply();});
  window.addEventListener('resize',layout);
  window.addEventListener('keydown',event=>{
    if((event.key==='p'||event.key==='P'||event.keyCode===80)&&ready){event.preventDefault();event.stopImmediatePropagation();if(!event.repeat)togglePause();}
  },true);
  document.addEventListener('contextmenu',event=>event.preventDefault());
  document.addEventListener('DOMContentLoaded',()=>{
    const resumeHit=document.createElement('button');resumeHit.id='original-pause-resume';resumeHit.type='button';
    resumeHit.hidden=true;resumeHit.setAttribute('aria-label','继续游戏');
    resumeHit.style.cssText='position:fixed;z-index:6;min-height:0;padding:0;border:0;border-radius:0;background:transparent;box-shadow:none;';
    // The first tap creates this hitbox during the SWF's pointer-up action.
    // Its subsequent compatibility click must not immediately undo that pause.
    resumeHit.onpointerdown=()=>{resumePointerArmed=true;};
    resumeHit.onpointercancel=()=>{resumePointerArmed=false;};
    resumeHit.onclick=event=>{
      const deliberate=resumePointerArmed || event.detail===0;resumePointerArmed=false;
      if(deliberate)togglePause();else event.preventDefault();
    };
    document.getElementById('shell').append(resumeHit);
    document.getElementById('hall').onclick=()=>showModal('exit');document.getElementById('help').onclick=()=>showModal('help');
    document.getElementById('settings').onclick=()=>showModal('settings');
    document.getElementById('modal-close').onclick=closeModal;
    document.getElementById('modal').onclick=event=>{if(event.target===event.currentTarget)closeModal();};
    document.getElementById('continue-run').onclick=()=>showModal('continue');
    document.getElementById('save-warning').onclick=()=>{try{invoke('xanaduFlushSave');apply();}catch(cause){showModal('save-error');}};
    for(const button of document.querySelectorAll('[data-input-style]'))button.onclick=()=>{if(controls)controls.setStyle(button.dataset.inputStyle);updateInputSettings();};
    document.getElementById('pause').onclick=togglePause;feedback.bind();startPlayer(null);
  });
})();
