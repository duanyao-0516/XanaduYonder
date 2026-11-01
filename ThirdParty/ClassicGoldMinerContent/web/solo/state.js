/* Versioned solo persistence boundary. No Phaser objects or callbacks cross it. */
(function (root, factory) {
    var api = factory();
    if (typeof module === 'object' && module.exports) module.exports = api;
    else root.ClassicSoloState = api;
}(typeof window !== 'undefined' ? window : this, function () {
    'use strict';
    function fail(path) { throw new Error('单人存档格式无效：' + path + '。原存档已保留。'); }
    function object(v,p) { if (!v || typeof v !== 'object' || Array.isArray(v)) fail(p); }
    function number(v,p,min,max) { if (typeof v !== 'number' || !isFinite(v) || v < min || v > max) fail(p); }
    function integer(v,p,min,max) { number(v,p,min,max); if (v % 1) fail(p); }
    function bool(v,p) { if (typeof v !== 'boolean') fail(p); }
    function choice(v,p,values) { if (values.indexOf(v)<0) fail(p); }
    function numericObject(v,p,keys,min,max) { object(v,p); keys.forEach(function(k){number(v[k],p+'.'+k,min,max);}); }
    function sprite(v,p) {
        object(v,p); numericObject(v,p,['x','y','rotation','alpha','scaleX','scaleY','anchorX','anchorY'],-10000,10000);
        ['alive','exists','visible'].forEach(function(k){bool(v[k],p+'.'+k);});
        if (typeof v.frameName !== 'string' || !/^[a-zA-Z0-9_]+\.png$/.test(v.frameName)) fail(p+'.frameName');
        if(v.animation!==null) {
            object(v.animation,p+'.animation'); choice(v.animation.name,p+'.animation.name',['Dinamite','Str','Down','Up','getAnim','dboom','boom','blick']);
            integer(v.animation.index,p+'.animation.index',0,30); number(v.animation.remaining,p+'.animation.remaining',0,10000);
            number(v.animation.delay,p+'.animation.delay',0,10000); bool(v.animation.playing,p+'.animation.playing'); bool(v.animation.loop,p+'.animation.loop'); bool(v.animation.killOnComplete,p+'.animation.killOnComplete');
        }
    }
    var tweenNumbers=['dt','percent','value','repeatCounter','duration'];
    function tween(v,p) {
        if(v===null)return; object(v,p);
        numericObject(v,p,tweenNumbers,-1000000,1000000); bool(v.inReverse,p+'.inReverse'); bool(v.running,p+'.running');
        ['vStart','vEnd','vStartCache','vEndCache'].forEach(function(k){object(v[k],p+'.'+k); var keys=Object.keys(v[k]); if(keys.length!==1 || ['x','rotation'].indexOf(keys[0])<0)fail(p+'.'+k); number(v[k][keys[0]],p+'.'+k,-10000,10000);});
    }
    function validate(s) {
        object(s,'state'); if(s.schema!==1||s.mode!=='solo')fail('schema/mode');
        choice(s.scene,'scene',['MainMenu','Goal','Game','Shop']); integer(s.level,'level',1,100000); number(s.money,'money',-100000000000,1000000000000);
        object(s.bonuses,'bonuses'); integer(s.bonuses.stick,'stick',0,1000); number(s.bonuses.strenght,'strength',0,10000);
        ['cloverBonus','rockBonus','diamondBonus'].forEach(function(k){bool(s.bonuses[k],k);});
        integer(s.goalType,'goalType',0,2); bool(s.sound,'sound');
        if(s.transition!==null){object(s.transition,'transition');choice(s.transition.next,'transition.next',['MainMenu','Goal','Game','Shop']);number(s.transition.remaining,'transition.remaining',0,500);number(s.transition.alpha,'transition.alpha',0,1);}
        if(s.scene==='Game') {
            var g=s.game; object(g,'game'); integer(g.mapIndex,'mapIndex',0,29); integer(g.seconds,'seconds',-1,61); number(g.tickRemaining,'tickRemaining',0,1000);
            if(g.viewportWidth!==undefined)integer(g.viewportWidth,'viewportWidth',640,1920);
            if(!Array.isArray(g.boulders)||g.boulders.length>100)fail('boulders');
            g.boulders.forEach(function(b,i){var p='boulder'+i;object(b,p);sprite(b.sprite,p+'.sprite');object(b.definition,p+'.definition');
                if(typeof b.definition.name!=='string'||!/^sprite\d+\.png$/.test(b.definition.name))fail(p+'.name'); numericObject(b.definition,p,['x','y'],-1000,2000);numericObject(b.definition.scale,p,['x','y'],-10,10);bool(b.definition.flip,p+'.flip');
                object(b.data,p+'.data');integer(b.data.type,p+'.type',2,14);if(b.data.price!=='power'&&b.data.price!=='dynamite')number(b.data.price,p+'.price',0,1000000);number(b.data.weight,p+'.weight',-10,20);number(b.data.bonusAdd,p+'.bonusAdd',0,1000000);
                bool(b.picked,p+'.picked');bool(b.got,p+'.got');tween(b.movement,p+'.movement');});
            object(g.pod,'pod'); sprite(g.pod.sprite,'pod.sprite');sprite(g.pod.miner,'pod.miner');sprite(g.pod.wave,'pod.wave');bool(g.pod.waveAttached,'waveAttached');
            choice(g.pod.status,'pod.status',['rotate','shoot','rewind','wait']);bool(g.pod.empty,'pod.empty');numericObject(g.pod,'pod',['slowDown','speed','maxSpeed','currentDir','startRopeX','yPoint','hotSpotX','hotSpotY'],-10000,10000);tween(g.pod.rotationTween,'pod.rotationTween');
            if(g.pod.canonicalRotation!==undefined)number(g.pod.canonicalRotation,'canonicalRotation',-Math.PI,Math.PI);
            sprite(g.boom,'boom'); bool(g.boomAttached,'boomAttached');
            if(g.reward!==null){object(g.reward,'reward');choice(g.reward.kind,'reward.kind',['money','power','dynamite']);number(g.reward.amount,'reward.amount',0,1000000);number(g.reward.elapsed,'reward.elapsed',0,g.reward.kind==='dynamite'?500:900);numericObject(g.reward,'reward',['startX','startY'],-10000,10000);}
        } else if(s.game!==null)fail('game');
        if(s.scene==='Shop') {
            object(s.shop,'shop');bool(s.shop.yes,'shop.yes');bool(s.shop.nextEnabled,'shop.nextEnabled');if(s.shop.selected!==null)integer(s.shop.selected,'selected',1,5);
            if(!Array.isArray(s.shop.items)||s.shop.items.length>5)fail('items');var seen={};s.shop.items.forEach(function(item){object(item,'item');integer(item.id,'item.id',1,5);if(seen[item.id])fail('duplicate item');seen[item.id]=true;integer(item.price,'item.price',0,100000000);bool(item.purchased,'item.purchased');});
            if(s.shop.selected!==null&&!seen[s.shop.selected])fail('selected item');
        } else if(s.shop!==null)fail('shop');
        if(s.scene==='Goal'){object(s.goal,'goal');number(s.goal.remaining,'goal.remaining',0,3000);}else if(s.goal!==null)fail('goal');
        return s;
    }
    function select(shop,id,money) {
        var item=shop.items.filter(function(x){return x.id===id;})[0];
        if(!item||item.purchased)return 'unavailable';
        if(shop.selected!==id){shop.selected=id;return 'selected';}
        return 'purchase';
    }
    function rewardStep(reward,dt) { var duration=reward.kind==='dynamite'?500:900;reward.elapsed=Math.min(duration,reward.elapsed+Math.max(0,dt));return reward.elapsed>=duration; }
    return {validate:validate,select:select,rewardStep:rewardStep};
}));
