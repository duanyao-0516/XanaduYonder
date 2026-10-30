ASSetPropFlags(_global.Math, "random", 0, 7);
_global.Math.random = function() { return 0.1; };
trace("AOW|RNG,"+Math.random()+","+Math.random()+","+_global.Math.random());
var probe=_root.createEmptyMovieClip("__parityProbe",900000);
var values=[0.01,0.049,0.05,0.099,0.1,-0.01,-0.049,-0.05,-0.099,-0.1,150.1,150.1+0.7,151.45+0.7,-151.45-0.7];
for(var vi=0;vi<values.length;vi++) {probe._x=values[vi];trace("AOW|TWIP,"+values[vi]+","+probe._x);}
probe.removeMovieClip();
_root.parityFrame=0;
_root.onEnterFrame=function() {
 if(_root.parityFrame>800)return;
 _root.parityFrame++;
 if(_root.parityFrame==1) {
  _root.menu.add_to_tray(1);
  _root.menu.add_to_tray(1);
  _root.menu.add_to_tray(1);
 }
 var p=_root.game.base_comp;
 trace("AOW|F,"+_root.parityFrame+","+_root.cash+","+_root.xp+","+_root.menu.timer+","+_root.menu.c_id+","+p.tech_timer+","+p.timer+","+p.u_timer+","+p.uof+","+_root.game.ennemies.ennemybase1.health+","+_root.game.ennemies.ennemybase2.health);
 for(var n=2;n<_root.ennemies.length;n++) {
  var a=_root.game.ennemies["ennemy"+_root.ennemies[n][0]];
  var child=a.units.anims.getInstanceAtDepth(-16383);
  if(_root.parityFrame==42) { for(var key in a.units.anims) {if(typeof(a.units.anims[key])=="movieclip")trace("AOW|CHILD,"+key+","+a.units.anims[key].getDepth()+","+a.units.anims[key]._currentframe);}}
  trace("AOW|U,"+_root.parityFrame+","+a.uid+","+a.id+","+a.side+","+a._x+","+a._y+","+a.health+","+a.dead+","+a.c+","+a.ed_id+","+a.ed+","+a.closest_dist+","+a.units.anims._currentframe+","+child._currentframe);
 }
 if(_root.parityFrame==800)trace("AOW|DONE,800");
};
trace("AOW|READY");
