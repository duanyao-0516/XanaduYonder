/* Original HUD actors in a wider viewport; unchanged 427 x 240 game world. */
(function () {
  'use strict';
  window.__installLevelDevilControls = function (runtime) {
    const E=runtime.Engine, C=runtime.Script, classes=runtime.classes;
    const owners=new Map(), held=new Map();
    const singleFields=['_BtnLeft','_BtnRight','_BtnJump'];
    const duoFields=['_BtnP1Left','_BtnP1Right','_BtnP1Jump','_BtnP2Left','_BtnP2Right','_BtnP2Jump'];
    let hallButton=null;
    function alive(actor) { return actor && !actor.dead && !actor.recycled && actor.animationMap && actor.currAnimation; }
    function place(actor,x,y) {
      if (!alive(actor)) return;
      if (!actor.isHUD) actor.anchorToScreen();
      actor.setScreenX(x); actor.setScreenY(y);
    }
    function geometry(duo) {
      const v=runtime.viewport(); if (!v) return null;
      const left=(v.left-v.x)/v.unit, right=(v.right-v.x)/v.unit;
      const bottom=(v.bottom-v.y)/v.unit, top=(v.top-v.y)/v.unit;
      return {left,right,top,bottom, xs:duo?[left+16,left+80,left+144,right-192,right-128,right-64]:[left+16,left+104,right-74], y:bottom-(duo?36:32)};
    }
    function layout() {
      if (!E.engine || !E.engine.behaviors) return;
      for(const [name,duo] of [['Scene_Manager',false],['2P Scene_Manager',true]]) {
        if(!C.sceneHasBehavior(name)) continue;
        const manager=E.engine.behaviors.getBehavior(name), g=geometry(duo);
        if(g) (duo?duoFields:singleFields).forEach((field,index)=>place(manager[field],g.xs[index],g.y));
      }
      const g=geometry(false);
      if(g && alive(hallButton)) {
        const visible = hallButton.xanaduChoiceBack || runtime.hallEnabled();
        if (hallButton.drawActor !== visible) {
          if (visible) hallButton.enableActorDrawing(); else hallButton.disableActorDrawing();
        }
        if (!visible) return;
        const scale=Math.max(1.5,48/(16*runtime.viewport().cssUnit));
        if(hallButton.xanaduHallScale!==scale) { hallButton.xanaduHallScale=scale; hallButton.growTo(scale,scale,0); }
        // Actor scaling is centered on its original eight-pixel origin. Offset
        // the unscaled position so the visible 48 CSS-pixel target stays safe.
        const originOffset=8*(scale-1);
        place(hallButton,g.left+10+originOffset,g.top+10+originOffset);
      }
    }
    function visual(manager,key,down,duo) {
      const name=duo ? {A:'_BtnP1Left',D:'_BtnP1Right',w:'_BtnP1Jump',Left:'_BtnP2Left',Right:'_BtnP2Right',Up:'_BtnP2Jump'}[key] : {Left:'_BtnLeft',Right:'_BtnRight',Up:'_BtnJump'}[key];
      const actor=manager[name];
      if (!alive(actor)) return;
      if (duo) actor.setAnimation(down?'B':'A');
      else if (actor.getAnimation() === (down?'A_Light':'B_Light')) actor.setAnimation(down?'B_Light':'A_Light');
    }
    function relinquish(id) {
      const owner=owners.get(id); if (!owner) return;
      owners.delete(id);
      if(owner.key===null) return;
      const count=held.get(owner.key)-1;
      if (count>0) held.set(owner.key,count);
      else { held.delete(owner.key); C.simulateKeyRelease(owner.key); visual(owner.manager,owner.key,false,owner.duo); }
    }
    function hit(manager,event,duo) {
      const x=(event.stageX-E.screenOffsetX)/(E.screenScaleX*E.SCALE);
      const y=(event.stageY-E.screenOffsetY)/(E.screenScaleY*E.SCALE);
      const g=geometry(duo); if(!g || y<=120 || y>g.bottom || x<g.left || x>g.right) return null;
      if (!duo) return x<g.xs[1]-15?'Left':x<g.xs[1]+73?'Right':x>=(g.left+g.right)/2?'Up':null;
      // Preserve broad lower-half controls, with disjoint ranges following the
      // original sprite positions. The middle gap stays deliberately inactive.
      const keys=['A','D','w','Left','Right','Up'];
      for(let i=0;i<6;i++) if(x>=g.xs[i]-8 && x<g.xs[i]+56) return keys[i];
      return null;
    }
    function pointer(manager,event,duo,kind) {
      const id=event.touchPointID;
      if(kind==='up') { relinquish(id); return; }
      if(runtime.blocked() || !manager.wrapper.enabled) { relinquish(id); return; }
      // A drag from a touch canceled by the host must not create a new press.
      if(kind==='move' && !owners.has(id)) return;
      const key=hit(manager,event,duo), old=owners.get(id);
      if(old && old.key===key && old.manager===manager) return;
      relinquish(id);
      // Keep a null owner while outside a hit zone, allowing the same finger to
      // slide back in without treating stale OS pointers as fresh touches.
      if(!key) { owners.set(id,{key:null,manager,duo}); return; }
      owners.set(id,{key,manager,duo}); const count=held.get(key)||0; held.set(key,count+1);
      if(count===0) { C.simulateKeyPress(key); visual(manager,key,true,duo); if(kind==='down' && (key==='Up'||key==='w'))runtime.haptic(); }
    }
    for (const [name,duo] of [['scripts.Design_236_236_SceneManager',false],['scripts.Design_471_471_2PSceneManager',true]]) {
      const proto=classes[name].prototype;
      proto._event_TouchButtonControls=function(event){pointer(this,event,duo,'down');};
      proto._event_DragButtonControls=function(event){pointer(this,event,duo,'move');};
      proto._event_ReleaseButtonControls=function(event){pointer(this,event,duo,'up');};
    }
    // Only the new choice-menu instance returns to Unity. Map instances go
    // back to player choice; original gameplay actors keep their navigation.
    const backProto=classes['scripts.ActorEvents_759'].prototype, backUpdate=backProto._event_Updating;
    const backInit=backProto.init;
    backProto.init=function() {
      this.actor.xanaduHallBack=false; this.actor.xanaduChoiceBack=false; this.actor.xanaduHallPending=false;
      return backInit.apply(this,arguments);
    };
    backProto._event_Updating=function() {
      if(!this.actor.xanaduHallBack && !this.actor.xanaduChoiceBack) return backUpdate.apply(this,arguments);
      if(this.wrapper.enabled && (this.actor.xanaduChoiceBack || runtime.hallEnabled()) && !runtime.blocked() && !this.actor.xanaduHallPending && this.actor.isMousePressed()) {
        this.actor.xanaduHallPending=true; this.actor.setAnimation('B');
        C.playSoundOnChannel(C.getSound(727),4);
        if(!(this.actor.xanaduChoiceBack ? runtime.returnToChoice() : runtime.hallExit())) { this.actor.xanaduHallPending=false; this.actor.setAnimation(this.actor.xanaduHallAnimation); }
      }
    };
    for(const id of [49,174,125]) {
      const proto=classes['scripts.SceneEvents_'+id].prototype, init=proto.init;
      proto.init=function() {
        init.apply(this,arguments);
        C.createRecycledActor(C.getActorType(759),0,0,0);
        hallButton=C.getLastCreatedActor();
        hallButton.xanaduHallBack=id===125; hallButton.xanaduChoiceBack=id!==125; hallButton.xanaduHallPending=false;
        hallButton.xanaduHallAnimation=id===174?'A_Blue':'A_Red';
        hallButton.setAnimation(hallButton.xanaduHallAnimation);
        hallButton.growTo(1.5,1.5,0); hallButton.makeAlwaysSimulate();
        const behavior=hallButton.behaviors.behaviors.find(b=>b.script instanceof classes['scripts.ActorEvents_759']);
        if(behavior && behavior.script._TextImageInstance) C.removeImage(behavior.script._TextImageInstance);
        layout();
      };
    }
    // Draw these three small controls using engine primitives. The original
    // bitmap/video icon stays intact in the source PAK and is never displayed.
    // Click handlers, animation state, eligibility and unlock state are retained.
    for (const [name,skip] of [['scripts.ActorEvents_708',false],['scripts.ActorEvents_959',false],['scripts.ActorEvents_762',true]]) {
      const proto=classes[name].prototype, init=proto.init;
      proto.init=function() {
        init.call(this); const behavior=this, actor=this.actor;
        this.addListener(actor.whenDrawing,function(g) {
          if(actor.dead || actor.recycled || !actor.drawActor) return;
          if(actor.currAnimation) actor.currAnimation.set_visible(false);
          const pressed=actor.getAnimation()==='B' || actor.getAnimation()==='Locked_B';
          const width=skip?52:66, y=pressed?2:0;
          g.fillColor=0x191919;g.fillRect(0,y,width,16-y);
          const normalColor=skip?(behavior._RedOrBlue==='Red'?0x59201d:0x1c303b):0x2a2418;
          const pressedColor=skip?(behavior._RedOrBlue==='Red'?0x7b302b:0x2c4855):0x4b402b;
          g.fillColor=pressed?pressedColor:normalColor;g.fillRect(1,y,width-2,13);
          g.setFont(C.getFont(761));
          const text=skip?'SKIP FREE':E.engine.getGameAttribute('Speedrun_Unlocked')?'SPEEDRUN':'UNLOCK FREE';
          g.drawString(text,3,y+3);
        });
      };
    }
    return {layout, cancelHallExit:function(){
      if(alive(hallButton)) { hallButton.xanaduHallPending=false; hallButton.setAnimation(hallButton.xanaduHallAnimation); }
    }, releaseInput:function(){
      const pending=Array.from(owners.values());owners.clear();held.clear();
      for(const owner of pending) if(owner.key!==null) {
        C.simulateKeyRelease(owner.key);visual(owner.manager,owner.key,false,owner.duo);
      }
    }};
  };
})();
