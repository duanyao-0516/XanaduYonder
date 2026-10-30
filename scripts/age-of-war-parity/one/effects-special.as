ASSetPropFlags(_global.Math,"random",0,7);
_global.Math.random=function(){return 0.1;};
_root.parityFrame=0;
_root.__parityCreateBullet=_root.create_bullet;
_root.create_bullet=function(x,y,rot,damage,id,side){trace("AOW|D,"+_root.parityFrame+","+id+","+x+","+y);_root.__parityCreateBullet(x,y,rot,damage,id,side);};
_root.onEnterFrame=function(){
 if(_root.parityFrame>=110)return;
 _root.parityFrame++;
 if(_root.parityFrame==1){
  _root.create_particule(400,300,1,[20]);
  _root.create_particule(400,300,4,[181]);
  _root.create_particule(400,300,6,[]);
  _root.game.attachMovie("special5","special5",99999);
 }
 trace("AOW|F,"+_root.parityFrame);
 var ids=[1,4,6];
 for(var i=0;i<3;i++){
  var p=_root.game.particules["part"+i];
  if(typeof(p)=="movieclip")trace("AOW|Q,"+_root.parityFrame+","+ids[i]+","+p._x+","+p._y+","+p._alpha+","+p._xscale+","+p._rotation+","+p.ymove);
 }
 var s=_root.game.special5;
 if(typeof(s)=="movieclip")trace("AOW|S,"+_root.parityFrame+","+s._x+","+s._y+","+s._currentframe+","+s.cc);
 for(var i=0;i<_root.bi;i++){
  var b=_root.game.bullets["bullet"+i];
  if(typeof(b)=="movieclip")trace("AOW|B,"+_root.parityFrame+","+i+","+b._x+","+b._y+","+b._currentframe);
 }
 if(_root.parityFrame==110)trace("AOW|DONE,110");
};
trace("AOW|RNG,"+Math.random()+","+Math.random());
