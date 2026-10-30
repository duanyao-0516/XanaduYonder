if(!_root.__probeInstalled){
 _root.__probeInstalled=true;
 _root.__openingFrame=0;
 _root.__identity=0;
 _root.onEnterFrame=function(){
  if(_root._currentframe<=4)return;
  _root.__openingFrame++;
  var intro=_root.getInstanceAtDepth(-16382);
  trace("AOW|F,"+_root.__openingFrame+","+_root._currentframe+","+intro._currentframe);
  if(_root._currentframe==7){
   var depths=[27,53];
   for(var i=0;i<2;i++){
    var holder=intro.getInstanceAtDepth(depths[i]-16384);
    if(holder.__probeIdentity==undefined)holder.__probeIdentity=++_root.__identity;
    for(var j=1;j<=25;j+=2){
     var fire=holder.getInstanceAtDepth(j-16384);
     if(fire.__probeIdentity==undefined)fire.__probeIdentity=++_root.__identity;
     trace("AOW|I,"+_root.__openingFrame+","+intro._currentframe+","+depths[i]+","+holder.__probeIdentity+","+j+","+fire.__probeIdentity+","+fire._currentframe);
    }
   }
  }
  if(_root._currentframe>=8){trace("AOW|DONE,opening");delete _root.onEnterFrame;}
 };
}
