/* Offline adaptation for the archived Phaser 2.4.6 solo game. */
(function () {
    'use strict';
    var host = window.ClassicHost, stateAPI = window.ClassicSoloState, layout = window.ClassicSoloLayout;
    var game, restoring = null, ready = false, active = false, paused = false, transition = null, registered = false, failure = null, runtimeSound = host.soundEnabled, reflowing = false;
    var dropButton = document.getElementById('solo-drop'), bombButton = document.getElementById('solo-bomb');
    function clone(value) { return JSON.parse(JSON.stringify(value)); }
    function error(e) {
        // An unrecoverable live-run error is different from an untouched menu.
        // Keep the first cause: later capture attempts must not acknowledge a save.
        if (!failure) failure = new Error(e && e.message || String(e));
        ready = false; paused = true;
        dropButton.disabled = true; bombButton.disabled = true;
        if (game) { game.input.enabled = false; game.paused = true; game.sound.stopAll(); }
        var box = document.getElementById('solo-error'); box.hidden = false;
        box.textContent = '游戏已暂停。当前进度未保存，原存档未改动。\n' + failure.message + '\n';
        var link = document.createElement('a'); link.href = '../index.html'; link.textContent = '返回模式选择（保留原存档）'; box.appendChild(link);
    }
    function checkpoint() { if (ready && active) host.checkpoint(); }
    function globals(s) {
        Game.currentLevelNum = s.level; Game.currentMoney = s.money; Game.bonuses = clone(s.bonuses); Goal.TYPE = s.goalType;
        runtimeSound = s.sound; if (Music.music) Music.music.SoundActive = s.sound;
    }
    function current() { return game && game.state.getCurrentState(); }
    function eventRemaining(event) {
        if (!event || event.pendingDelete) return 0;
        var t = event.timer; return Math.max(0, event.tick - (t.paused ? t._pauseStarted : game.time.time));
    }
    function setEventRemaining(event, remaining) {
        var timer=event.timer,base=timer.paused?timer._pauseStarted:game.time.time;
        event.tick=base+remaining;timer._now=base;timer.order();
    }
    function spriteCapture(sprite) {
        var a = sprite.animations.currentAnim;
        return {x:sprite.x,y:sprite.y,rotation:sprite.rotation,alpha:sprite.alpha,scaleX:sprite.scale.x,scaleY:sprite.scale.y,anchorX:sprite.anchor.x,anchorY:sprite.anchor.y,
            frameName:sprite.frameName || 'hv1.png',alive:sprite.alive,exists:sprite.exists,visible:sprite.visible,
            animation:a ? {name:a.name,index:a._frameIndex,remaining:Math.max(0,game.paused ? a._frameDiff || 0 : a._timeNextFrame-game.time.time || 0),delay:a.delay,playing:a.isPlaying,loop:a.loop,killOnComplete:a.killOnComplete} : null};
    }
    function spriteRestore(sprite,s) {
        // Phaser silently ignores unknown frame names; reject before any mutation
        // so reconstruction cannot turn corrupt input into a new valid-looking save.
        if(!game.cache.getFrameByName(Game.KEY,s.frameName))throw new Error('存档图片帧不存在：'+s.frameName);
        sprite.position.set(s.x,s.y); sprite.rotation=s.rotation; sprite.alpha=s.alpha;
        sprite.scale.set(s.scaleX,s.scaleY); sprite.anchor.set(s.anchorX,s.anchorY);
        sprite.alive=s.alive; sprite.exists=s.exists; sprite.visible=s.visible;
        sprite.animations.stop();
        if(s.animation) {
            var saved=s.animation, a=sprite.animations.getAnimation(saved.name);
            if(!a)throw new Error('存档动画不属于当前对象');
            a.play(1000/saved.delay,saved.loop,saved.killOnComplete); a._frameIndex=saved.index;
            if(a._frameIndex>=a._frames.length)throw new Error('存档动画帧超出范围');
            a.updateCurrentFrame(false,true); a._timeNextFrame=game.time.time+saved.remaining; a._frameDiff=saved.remaining; a.isPlaying=saved.playing;
        }
        sprite.frameName=s.frameName;
    }
    var tweenKeys=['dt','percent','value','repeatCounter','duration','inReverse','vStart','vEnd','vStartCache','vEndCache'];
    function tweenCapture(t) {
        if(!t)return null;var d=t.timeline[0],s={running:d.isRunning};
        tweenKeys.forEach(function(k){s[k]=clone(d[k]);});return s;
    }
    function tweenRestore(t,s) {
        if(!t||!s)return;var d=t.timeline[0];tweenKeys.forEach(function(k){d[k]=clone(s[k]);});d.isRunning=s.running;d.startTime=game.time.time;
    }
    function captureGame(s) {
        var p=s.pod;
        return {viewportWidth:s.classicViewportWidth||640,mapIndex:s.classicMapIndex,seconds:s.timeBeforeEndLevel,tickRemaining:Math.min(1000,eventRemaining(s.timer)),
            boulders:s.boulders.map(function(b){return {definition:clone(b.classicDefinition),data:{type:b.boulderData.type,price:b.boulderData.price,weight:b.boulderData.weight,bonusAdd:b.boulderData.bonusAdd||0},sprite:spriteCapture(b),picked:b.picked,got:b.gotBoulder,movement:tweenCapture(b.classicMoveTween)};}),
            pod:{sprite:spriteCapture(p),miner:spriteCapture(p.miner),wave:spriteCapture(p.waveAnim),waveAttached:p.waveAnim.parent===p,status:p.podStatus,slowDown:p.slowDown,empty:p.empty,speed:p.speed,maxSpeed:p.maxSpeed,currentDir:p.currentDir,startRopeX:p.startRopeX,yPoint:p.yPoint,hotSpotX:p.hotSpotX||0,hotSpotY:p.hotSpotY||0,canonicalRotation:p.classicSwing?p.classicSwing.rotation:p.rotation,rotationTween:tweenCapture(p.rotateTween)},
            boom:spriteCapture(s.boom),boomAttached:!!s.boom.parent,reward:s.classicReward ? clone(s.classicReward):null};
    }
    function captureShop(s) {
        return {selected:s.classicSelected||null,yes:s.yes,nextEnabled:s.nextLevelBtn.inputEnabled,items:s.btns.filter(Boolean).map(function(b){return{id:Number(b.name),price:b.price,purchased:!b.alive};})};
    }
    function capture() {
        if(active && failure)throw failure;
        if(!active||!ready)return null;
        var scene=game.state.current,s=current(),tr=null;
        if(transition)tr={next:transition.next,remaining:Math.min(500,eventRemaining(transition.event)),alpha:transition.sprite.alpha};
        else if(game.state._pendingState)tr={next:game.state._pendingState,remaining:0,alpha:1};
        var result={schema:1,mode:'solo',scene:scene,level:Game.currentLevelNum,money:Game.currentMoney,
            bonuses:{stick:Game.bonuses.stick,strenght:Game.bonuses.strenght,cloverBonus:!!Game.bonuses.cloverBonus,rockBonus:!!Game.bonuses.rockBonus,diamondBonus:!!Game.bonuses.diamondBonus},
            goalType:Goal.TYPE,sound:Music.music.SoundActive,transition:tr,game:scene==='Game'?captureGame(s):null,shop:scene==='Shop'?captureShop(s):null,
            goal:scene==='Goal'?{remaining:Math.min(3000,eventRemaining(s.classicGoalTimer.events[0]))}:null};
        return stateAPI.validate(result);
    }
    function restoreGame(s,saved) {
        var p=s.pod; s.timeBeforeEndLevel=saved.seconds;s.timerTxt.text=String(Math.min(60,saved.seconds));setEventRemaining(s.timer,saved.tickRemaining);
        s.setMoney(Game.currentMoney);s.setGoal(Game.getGoal(Game.currentLevelNum));
        ['slowDown','empty','speed','maxSpeed','currentDir','startRopeX','yPoint','hotSpotX','hotSpotY'].forEach(function(k){p[k]=saved.pod[k];});
        p.changeStateTo(saved.pod.status); tweenRestore(p.rotateTween,saved.pod.rotationTween);spriteRestore(p,saved.pod.sprite);spriteRestore(p.miner,saved.pod.miner);
        p.classicSwing.rotation=saved.pod.canonicalRotation===undefined?layout.unprojectRotation(p.rotation,(saved.viewportWidth||640)/640):saved.pod.canonicalRotation;
        if(saved.pod.miner.animation&&saved.pod.miner.animation.playing) {
            var anim=p.miner.animations.currentAnim;
            if(anim.name==='Dinamite')anim.onComplete.addOnce(p.miner.setUp,p.miner);
            if(anim.name==='Str')anim.onComplete.addOnce(p.miner.setStaticWait,p.miner);
        }
        spriteRestore(p.waveAnim,saved.pod.wave);
        if(saved.pod.waveAttached){p.addChild(p.waveAnim);p.waveAnim.animations.currentAnim.onComplete.addOnce(function(){if(p.waveAnim.parent)p.removeChild(p.waveAnim);});}
        p.rod.rotation=p.rotation;if(p.podStatus==='rotate')p.rod.height=26;else{p.rod.moveTo(p.startRopeX,p.yPoint);p.rod.lineTo(p.x,p.y);}
        spriteRestore(s.boom,saved.boom);if(saved.boomAttached){game.add.existing(s.boom);s.boom.animations.currentAnim.onComplete.addOnce(function(){if(s.boom.parent)s.boom.parent.removeChild(s.boom);});}
        s.classicReward=saved.reward?clone(saved.reward):null;s.classicRewardTick=game.time.time;if(s.classicReward)renderReward(s);
    }
    function finishCreate(scene,s) {
        if(failure)return;
        transition=null;
        if(restoring && restoring.scene===scene) {
            var saved=restoring;globals(saved);
            if(scene==='Game')restoreGame(s,saved.game);
            if(scene==='Shop') {
                s.classicSelected=saved.shop.selected;s.yes=saved.shop.yes;s.nextLevelBtn.inputEnabled=saved.shop.nextEnabled;
                saved.shop.items.forEach(function(i){if(i.purchased)s.btns[i.id].kill();});
                if(s.classicSelected)s.showDescript(s.btns[s.classicSelected]);
            }
            if(scene==='Goal')setEventRemaining(s.classicGoalTimer.events[0],saved.goal.remaining);
            restoring=null;window.ClassicSoloRestore=null;
            if(saved.transition)startTransition(saved.transition.next,saved.transition.remaining,saved.transition.alpha);
        }
        ready=true;
        if(!registered){registered=true;host.register({mode:"solo",capture:capture,pause:pause,resume:resume});}
        updateControls();checkpoint();
    }
    function updateControls() {
        var playing=ready&&game&&game.state.current==='Game'&&!paused&&!transition,s=playing&&current();
        dropButton.disabled=!playing; bombButton.disabled=!playing||!Game.bonuses||Game.bonuses.stick<=0;
        bombButton.hidden=!(ready&&game&&game.state.current==='Game');
        bombButton.textContent=Game.bonuses?String(Game.bonuses.stick):'0';
        if(bombButton.setAttribute)bombButton.setAttribute('aria-label','炸药，剩余'+(Game.bonuses?Game.bonuses.stick:0)+'个');
        document.getElementById('solo-hint').textContent=game&&game.state.current==='Shop'?'先选商品，再点同一商品购买':'点击游戏区域也可放钩';
    }
    function startTransition(next,remaining,alpha) {
        if(transition)return;
        var fade=FadeInOut.instance;fade.createBkg();fade.spr_bg.alpha=alpha||0;game.add.existing(fade.spr_bg);
        var ev=game.time.events.add(remaining,function(){transition=null;game.state.start(next);});
        setEventRemaining(ev,remaining);
        transition={next:next,event:ev,sprite:fade.spr_bg};game.add.tween(fade.spr_bg).to({alpha:1},Math.max(1,remaining),null,true);updateControls();
    }
    FadeInOut.prototype.fadeTo=function(next){startTransition(next,500,0);checkpoint();};
    function viewport() {var box=document.getElementById('gameContainer');return {width:box.clientWidth||window.innerWidth,height:box.clientHeight||window.innerHeight};}
    function sizeScene(scene) {
        var view=viewport(),width=scene==='Game'?layout.viewportWidth(view.width,view.height):640;
        ScaleManager.game=game;ScaleManager.GameWidth=width;
        game.scale.setGameSize(width,480);game.world.setBounds(0,0,width,480);
        var ratio=Math.min(view.width/width,view.height/480);
        game.scale.setUserScale(ratio,ratio,0,0);game.scale.refresh();
        return width;
    }
    function safeInsets() {
        var style=window.getComputedStyle?window.getComputedStyle(document.documentElement):null;
        var rect=game.canvas.getBoundingClientRect(),qx=rect.width/game.width,qy=rect.height/480,out={};
        ['left','right','top','bottom'].forEach(function(edge){var px=style?parseFloat(style.getPropertyValue('--classic-safe-'+edge)):8;out[edge]=(Number.isFinite(px)?px:8)/(edge==='left'||edge==='right'?qx:qy);});
        return out;
    }
    function arrangeHud(s) {
        if(!s.classicHudLabels)return;
        var safe=safeInsets(),width=s.classicViewportWidth;s.classicSafe=safe;
        s.classicHudLabels.children.forEach(function(label){if(label.classicBaseX===undefined){label.classicBaseX=label.x;label.classicBaseY=label.y;}label.x=label.classicBaseX+(label.classicBaseX<width/2?safe.left:-safe.right);label.y=label.classicBaseY+safe.top;});
        s.moneyTxt.x=s.goalTxt.x=74+safe.left;s.moneyTxt.y=10+safe.top;s.goalTxt.y=38+safe.top;
        s.timerTxt.x=s.levelTxt.x=width-60-safe.right;s.timerTxt.y=10+safe.top;s.levelTxt.y=38+safe.top;
        s.classicExitButton.x=width-155-safe.right;s.classicExitButton.y=40+safe.top;
        s.classicSoundButton.x=10+safe.left;s.classicSoundButton.y=440-safe.bottom;
        s.classicHudPanel.height=71+safe.top;
    }
    function resizeScene() {
        if(!game||!game.isBooted||reflowing||failure)return;
        if(game.state.current!=='Game'||!ready){sizeScene(game.state.current);return;}
        var view=viewport(),width=layout.viewportWidth(view.width,view.height),s=current();
        if(width===s.classicViewportWidth){sizeScene('Game');arrangeHud(s);return;}
        var wasPaused=paused;
        try {
            reflowing=true;pause();var snapshot=layout.reflowSnapshot(capture(),width);
            ready=false;restoring=snapshot;window.ClassicSoloRestore=snapshot;
            // Scene recreation is synchronous while input, timers and rendering
            // are paused. It consumes saved entities instead of rerolling a map.
            game.state.start('Game');game.state.preUpdate();
            if(!wasPaused)resume();
        } catch(e){error(e);} finally {reflowing=false;}
    }
    ScaleManager.SizeCalculation=function(){resizeScene();};
    ScaleManager.checkOrientation=function(){};
    ScaleManager.inSplash=false;
    // Sounds use the shared native preference; this adapter never reads old Unity saves.
    SaveAPI.getAccess=function(key){return key===SaveAPI.SOUND?runtimeSound:null;};
    SaveAPI.setAccess=function(){};SaveAPI.setForce=function(key,value){if(key===SaveAPI.SOUND)runtimeSound=value;};SaveAPI.clear=function(){};
    var originalMainCreate=MainMenu.prototype.create;
    MainMenu.prototype.create=function(){sizeScene('MainMenu');originalMainCreate.call(this);finishCreate('MainMenu',this);};
    MainMenu.prototype.startButtonClick=function(){if(transition)return;active=true;Game.currentMoney=0;Game.currentLevelNum=1;Game.bonuses=new Bonuses;Goal.TYPE=ScreenType.Goal;FadeInOut.instance.fadeTo('Goal');};
    var originalGoalCreate=Goal.prototype.create;
    Goal.prototype.create=function(){sizeScene('Goal');originalGoalCreate.call(this);finishCreate('Goal',this);};
    var originalPlace=Game.prototype.placeBoulders;
    Game.prototype.placeBoulders=function(level) {
        if(!restoring||restoring.scene!=='Game'){
            originalPlace.call(this,level);var ratio=this.classicViewportWidth/640;
            this.boulders.forEach(function(b){b.x*=ratio;if(b.classicMoveTween){var saved=tweenCapture(b.classicMoveTween);layout.scaleMovement(saved,ratio);tweenRestore(b.classicMoveTween,saved);}});
            return;
        }
        var s=this;restoring.game.boulders.forEach(function(saved){
            if(saved.definition.name!=='sprite215.png'&&!BoulderData.data[saved.definition.name])throw new Error('存档地图实体未知');
            var bagFactory=BoulderData.reciveSprite215,b;
            try{BoulderData.reciveSprite215=function(){return clone(saved.data);};b=new Boulder(s.game,clone(saved.definition));}finally{BoulderData.reciveSprite215=bagFactory;}
            s.game.add.existing(b);s.boulders.push(b);b.addScore.add(s.onAddScore,s);b.makeBoomBldr.add(s.boomBounler,s);
            b.boulderData=clone(saved.data);b.picked=saved.picked;b.gotBoulder=saved.got;tweenRestore(b.classicMoveTween,saved.movement);spriteRestore(b,saved.sprite);
        });
    };
    var originalGameCreate=Game.prototype.create;
    Game.prototype.create=function(){
        this.classicViewportWidth=sizeScene('Game');
        if(restoring&&restoring.scene==='Game'){restoring=layout.reflowSnapshot(restoring,this.classicViewportWidth);window.ClassicSoloRestore=restoring;}
        this.classicReward=null;this.classicRewardSprite=null;originalGameCreate.call(this);arrangeHud(this);finishCreate('Game',this);
    };
    Game.prototype.onMouseClick=function(pointer){
        if(paused||transition||!ready)return;
        // The HUD sound/level buttons must not also trigger the game-wide pointer signal.
        if(pointer&&typeof pointer.worldY==='number'){
            var safe=this.classicSafe||{left:8,right:8,top:0,bottom:8},width=this.classicViewportWidth||640,x=pointer.worldX,y=pointer.worldY;
            if(x<=safe.left||x>=width-safe.right||y<72+safe.top||y>=480-safe.bottom||(x<safe.left+60&&y>420-safe.bottom))return;
        }
        this.pod.shootPod();
    };
    Game.prototype.shutdown=function(){this.game.input.onDown.remove(this.onMouseClick,this);this.game.input.keyboard.removeKey(Phaser.Keyboard.UP);this.game.input.keyboard.removeKey(Phaser.Keyboard.DOWN);};
    Pod.prototype.createRod=function(){
        this.rod=new Rod(this.game,this.x,this.y);this.parent.addChild(this.rod);
        this.classicSwing={rotation:-Math.PI/3};
        this.rotateTween=this.game.add.tween(this.classicSwing).to({rotation:Math.PI/3},2000,Phaser.Easing.Sinusoidal.InOut,true,0,99999,true);
        this.rotation=layout.projectRotation(this.classicSwing.rotation,this.game.world.width/640);this.changeStateTo('rotate');
    };
    function projectSwing(p){if(p.podStatus==='rotate'){p.rotation=layout.projectRotation(p.classicSwing.rotation,p.game.world.width/640);p.rod.rotation=p.rotation;}}
    var originalPodUpdate=Pod.prototype.updateStatus,originalShoot=Pod.prototype.shootPod;
    Pod.prototype.updateStatus=function(){projectSwing(this);originalPodUpdate.call(this);};
    Pod.prototype.shootPod=function(){projectSwing(this);originalShoot.call(this);};
    Game.prototype.preRender=function(){projectSwing(this.pod);};
    function beginReward(s,kind,amount) {
        s.classicReward={kind:kind,amount:amount||0,elapsed:0,startX:s.pod.x,startY:s.pod.y+10};
        if(kind==='power')s.pod.miner.setAnimStr();if(kind==='money')Music.music.audioEffects(Music.music.upfinish);
        s.classicRewardTick=game.time.time;renderReward(s);
    }
    Game.prototype.scoreAnim=function(amount){beginReward(this,'money',amount);};
    Game.prototype.strAnim=function(){beginReward(this,'power',0);};
    Game.prototype.rcvDynamite=function(){beginReward(this,'dynamite',0);};
    function renderReward(s) {
        var r=s.classicReward;if(!r)return;var t=r.elapsed;
        if(r.kind==='dynamite') {
            if(!s.classicRewardSprite){s.classicRewardSprite=game.add.sprite(game.world.width/2-90,40,Game.KEY,'dyn.png');s.classicRewardSprite.anchor.set(.5,.5);}
            var d=s.classicRewardSprite,u=Math.min(1,Math.max(0,(t-400)/100)),origin=game.world.width/2-90;d.scale.set(Math.min(1,t/200));d.x=origin+(s.dynamite.x-origin)*u;d.y=40+(s.dynamite.y+15-40)*u;return;
        }
        var txt=s.addScoreTxt;txt.text=r.kind==='money'?'$'+r.amount:'STRENGTH';
        var first=Math.min(1,t/200),last=Math.min(1,Math.max(0,(t-700)/200));txt.scale.set(t<700?first:1-last);
        var center=game.world.width/2-120;txt.x=t<200?r.startX+(center-r.startX)*first:center;txt.y=t<200?r.startY+(30-r.startY)*first:30;
        if(r.kind==='money'&&t>=700){txt.x=center+(s.moneyTxt.x-center)*last;txt.y=30+(s.moneyTxt.y-30)*last;}
    }
    var originalGameUpdate=Game.prototype.update;
    Game.prototype.update=function() {
        originalGameUpdate.call(this);
        if(this.classicReward){
            var r=this.classicReward,finished=stateAPI.rewardStep(r,Math.max(0,this.game.time.time-this.classicRewardTick));this.classicRewardTick=this.game.time.time;renderReward(this);
            if(finished){this.classicReward=null;this.addScoreTxt.scale.set(0,0);
                if(r.kind==='money'){this.setMoney(Game.currentMoney+r.amount);Music.music.audioEffects(Music.music.scoreAdd);}
                else if(r.kind==='power')Game.bonuses.strenght+=5;
                else{this.dynamite.addOne();if(this.classicRewardSprite)this.classicRewardSprite.destroy();this.classicRewardSprite=null;}
                this.pod.changeStateTo('rotate');checkpoint();
            }
        }
        updateControls();
    };
    var originalCanCreate=ShopButton.canCreate,originalGetPrice=ShopButton.prototype.getPrice;
    ShopButton.canCreate=function(id){return restoring&&restoring.scene==='Shop'?restoring.shop.items.some(function(i){return i.id===id;}):originalCanCreate(id);};
    ShopButton.prototype.getPrice=function(id){return restoring&&restoring.scene==='Shop'?restoring.shop.items.filter(function(i){return i.id===id;})[0].price:originalGetPrice.call(this,id);};
    function shopTouchArea(button,qx,qy,left,right,top,bottom) {
        var sx=Math.abs(button.scale.x),sy=Math.abs(button.scale.y),frame=button.texture.frame;
        var x0=-frame.width*button.anchor.x,x1=x0+frame.width,y0=-frame.height*button.anchor.y,y1=y0+frame.height;
        // A custom Phaser 2 hit area replaces the normal child hit test. Include
        // the price label, which remains part of the same two-tap purchase target.
        button.children.forEach(function(child){x0=Math.min(x0,child.x);x1=Math.max(x1,child.x+child.width);y0=Math.min(y0,child.y);y1=Math.max(y1,child.y+child.height);});
        var width=Math.min(right-left,Math.max((x1-x0)*sx,48/qx));
        var x=Math.max(left,Math.min(right-width,button.x+(x0+x1)*sx/2-width/2));
        var y=Math.max(top,Math.min(button.y+y0*sy,button.y-24/qy));
        var end=Math.min(bottom,Math.max(button.y+y1*sy,button.y+24/qy));
        // Phaser 2 uses anchor-relative local coordinates after its inverse
        // world transform; unlike Phaser 3 it adds no display-origin offset.
        button.hitArea=new Phaser.Rectangle((x-button.x)/sx,(y-button.y)/sy,width/sx,(end-y)/sy);
    }
    function updateShopTouchAreas(shop) {
        var canvas=shop.game.canvas;if(!canvas||!canvas.getBoundingClientRect)return;
        var rect=canvas.getBoundingClientRect();if(!rect.width||!rect.height)return;
        var size=rect.width+':'+rect.height;if(shop.classicTouchSize===size)return;shop.classicTouchSize=size;
        var qx=rect.width/640,qy=rect.height/480;
        shop.btns.filter(Boolean).forEach(function(button){shopTouchArea(button,qx,qy,Math.max(0,button.x-45.5),Math.min(640,button.x+45.5),200,390);});
        shopTouchArea(shop.nextLevelBtn,qx,qy,0,640,0,190);
    }
    var originalShopCreate=Shop.prototype.create,originalBuy=Shop.prototype.buyItem,originalDescription=Shop.prototype.showDescript;
    Shop.prototype.create=function(){sizeScene('Shop');this.classicSelected=null;this.classicTouchSize=null;originalShopCreate.call(this);updateShopTouchAreas(this);finishCreate('Shop',this);};
    Shop.prototype.update=function(){updateShopTouchAreas(this);};
    Shop.prototype.showDescript=function(item){originalDescription.call(this,item);this.descript.text+='\nPrice $'+item.price+' / Cash $'+Game.currentMoney+' - '+(Number(item.name)===this.classicSelected?'Tap again to buy':'Tap to select');};
    Shop.prototype.buyItem=function(item) {
        if(paused||transition||!item.alive)return;
        var shop=captureShop(this),action=stateAPI.select(shop,Number(item.name),Game.currentMoney);this.classicSelected=shop.selected;this.showDescript(item);
        if(action==='purchase'){originalBuy.call(this,item);this.classicSelected=null;this.descript.text='Purchased';}
        checkpoint();
    };
    function actionButton(button,action) {
        button.addEventListener('pointerdown',function(e){e.preventDefault();e.stopPropagation();if(!button.disabled&&ready&&!paused){action();checkpoint();}});
        button.addEventListener('click',function(e){e.preventDefault();e.stopPropagation();if(e.detail===0&&!button.disabled){action();checkpoint();}});
    }
    actionButton(dropButton,function(){if(game.state.current==='Game')current().pod.shootPod();});
    actionButton(bombButton,function(){if(game.state.current==='Game')current().onMakeBoomKey();});
    var bombGuard=document.getElementById('solo-bomb-guard');
    if(bombGuard) ['pointerdown','touchstart','mousedown'].forEach(function(name){bombGuard.addEventListener(name,function(e){e.stopPropagation();},{passive:false});});
    function pause(){paused=true;if(game){game.input.enabled=false;game.paused=true;game.sound.pauseAll();}updateControls();}
    function resume(){if(failure)throw failure;if(!game||!ready)return;game.input.reset();game.paused=false;if(current())current().classicRewardTick=game.time.time;game.input.enabled=true;game.sound.resumeAll();paused=false;updateControls();}
    function boot() {
        try {
            var query=new URLSearchParams(window.location.search);active=query.has('new');
            if(query.has('resume')){restoring=host.read('solo');if(!restoring)throw new Error('未找到单人存档');stateAPI.validate(restoring);active=true;}
            window.ClassicSoloRestore=restoring;
            game=new Phaser.Game(640,480,Phaser.CANVAS,'gameContainer');window.ClassicSoloGame=game;
            game.state.add('Boot',{
                preload:function(){
                    var load=this.game.load;load.atlasJSONArray('atlas','assets/atlas.png','assets/atlas.json');load.bitmapFont('font','assets/font/font.png','assets/font/font.fnt');
                    ['182','309','310','322'].forEach(function(n){load.image(n+'.jpg','assets/'+n+'.jpg');});
                    ['boom','down','goal','hvBad','hvCool','hvGood','scoreAdd','up','upfinish','win'].forEach(function(n){load.audio(n,['assets/audio/'+n+'.ogg']);});
                    load.onFileError.add(function(key){error(new Error('本地游戏资源加载失败：'+key));});
                },create:function(){
                    this.stage.disableVisibilityChange=true;this.game.scale.scaleMode=Phaser.ScaleManager.USER_SCALE;this.input.maxPointers=1;
                    sizeScene('Boot');this.game.canvas.oncontextmenu=function(e){e.preventDefault();};
                    Music.music=new Music(this.game);FadeInOut.Init(this.game);Game.bonuses=new Bonuses;Goal.TYPE=ScreenType.Goal;
                    if(restoring)globals(restoring);this.game.state.start(restoring?restoring.scene:'MainMenu');
                }
            });
            game.state.add('MainMenu',MainMenu);game.state.add('Goal',Goal);game.state.add('Game',Game);game.state.add('Shop',Shop);game.state.start('Boot');
            window.addEventListener('resize',resizeScene);window.addEventListener('classic-insets',resizeScene);
        } catch(e){error(e);}
    }
    window.addEventListener('error',function(event){if(event.error)error(event.error);});
    window.addEventListener('load',boot);
}());
