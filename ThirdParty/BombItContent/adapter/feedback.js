(function(){
  'use strict';
  // Native owns persistence and actual feedback; the UI only confirms ACKed values.
  window.createBombItFeedback=function(runtime){
    let token=null,sound=true,haptics=true,pending=null,desired=null,sequence=0,pressId=0,timer=null,engineSession=null,syncing=false,message='';
    function valid(value){return value&&typeof value.token==='string'&&value.token.length>0&&typeof value.soundEnabled==='boolean'&&typeof value.hapticsEnabled==='boolean';}
    function render(){
      for(const [id,enabled] of [['sound-setting',sound],['haptics-setting',haptics]]){
        const button=document.getElementById(id);if(!button)continue;
        button.setAttribute('aria-checked',String(enabled));button.disabled=!token||!!pending;
        button.querySelector('.switch-value').textContent=enabled?'开启':'关闭';
      }
      const status=document.getElementById('feedback-status');
      if(status){status.textContent=pending?'正在保存…':message;status.hidden=!status.textContent;}
      const retry=document.getElementById('feedback-retry');if(retry)retry.hidden=!desired||!!pending||!message;
    }
    function syncEngine(){
      const state=runtime.state();if(!state||!state.booted||state.soundEnabled===sound)return;
      syncing=true;try{runtime.setSound(sound);}finally{syncing=false;}
    }
    function configure(value){
      if(!valid(value))return;
      if(token!==value.token){clearTimeout(timer);pending=null;desired=null;sequence=0;pressId=0;token=value.token;}
      sound=value.soundEnabled;haptics=value.hapticsEnabled;message='';
      syncEngine();render();runtime.changed();
    }
    function request(nextSound,nextHaptics,force=false){
      if(!token||pending)return;
      if(!force&&nextSound===sound&&nextHaptics===haptics){syncEngine();return;}
      const requestId='feedback-'+(++sequence);pending=requestId;desired={sound:nextSound,haptics:nextHaptics};message='';render();
      timer=setTimeout(()=>{
        if(pending!==requestId)return;
        pending=null;message='设置尚未确认，请重试。';syncEngine();render();
      },5000);
      runtime.post({type:'feedback-settings',token,requestId,soundEnabled:nextSound,hapticsEnabled:nextHaptics});
    }
    function result(value){
      if(!valid(value)||value.token!==token||value.requestId!==pending||typeof value.ok!=='boolean')return;
      clearTimeout(timer);pending=null;sound=value.soundEnabled;haptics=value.hapticsEnabled;
      if(value.ok)desired=null;
      message=value.ok?'':'设置保存尚未确认，请重试。';syncEngine();render();runtime.changed();
    }
    function observe(state,previous){
      if(!state.booted)return;
      if(engineSession!==state.session){engineSession=state.session;if(token)syncEngine();return;}
      if(syncing||!token||!previous||state.soundEnabled===previous.soundEnabled)return;
      // The original music button and the app switch share one preference.
      if(state.soundEnabled!==sound){request(state.soundEnabled,haptics);syncEngine();}
    }
    return Object.freeze({configure,result,observe,render,soundEnabled:()=>sound,
      haptic(kind){if(token&&haptics&&kind==='bomb-press'&&runtime.canHaptic())runtime.post({type:'haptic',token,kind,pressId:++pressId});},
      bind(){
        document.getElementById('sound-setting').onclick=()=>request(!sound,haptics);
        document.getElementById('haptics-setting').onclick=()=>request(sound,!haptics);render();
        document.getElementById('feedback-retry').onclick=()=>{if(desired)request(desired.sound,desired.haptics,true);};
      }});
  };
})();
