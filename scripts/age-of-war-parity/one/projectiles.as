ASSetPropFlags(_global.Math,"random",0,7);
_global.Math.random=function(){return 0.1;};
_root.parityFrame=0;
_root.__parityCreatePart=_root.create_particule;
_root.create_particule=function(x,y,id,params){_root.__parityCreatePart(x,y,id,params);trace("AOW|C,"+_root.parityFrame+","+id+","+x+","+y);};
_root.onEnterFrame=function(){
 if(_root.parityFrame>=40)return;
 _root.parityFrame++;
 if(_root.parityFrame==1){for(var id=1;id<=12;id++)_root.create_bullet(400,400,30,10,id,1);}
 trace("AOW|F,"+_root.parityFrame);
 for(var i=0;i<12;i++){
  var b=_root.game.bullets["bullet"+i];
  if(typeof(b)=="movieclip")trace("AOW|B,"+_root.parityFrame+","+(i+1)+","+b._x+","+b._y+","+b._rotation+","+b.xs+","+b.ys+","+b.g);
 }
 if(_root.parityFrame==40)trace("AOW|DONE,40");
};
trace("AOW|RNG,"+Math.random()+","+Math.random());
