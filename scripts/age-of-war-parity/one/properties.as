_root.onEnterFrame=function(){
 delete _root.onEnterFrame;
 var probe=_root.createEmptyMovieClip("__propertyProbe",1000001);
 var values=[-200,-10,0,0.01,50,98,99.99,100,110,200];
 for(var i=0;i<values.length;i++){probe._alpha=values[i];trace("AOW|PROP,alpha,"+values[i]+","+probe._alpha);}
 values=[-720,-181,-180,-30.1,0,30.1,180,181,720];
 for(var i=0;i<values.length;i++){probe._rotation=values[i];trace("AOW|PROP,rotation,"+values[i]+","+probe._rotation);}
 values=[-200,-120,-10,0,10,33.33333333333333,100.01,120,200];
 for(var i=0;i<values.length;i++){probe._xscale=values[i];probe._yscale=values[i];trace("AOW|PROP,scale,"+values[i]+","+probe._xscale+","+probe._yscale);}
 probe._alpha=100;
 for(var i=1;i<=52;i++){probe._alpha-=2;trace("AOW|FADE2,"+i+","+probe._alpha);}
 probe._alpha=100;
 for(var i=1;i<=102;i++){probe._alpha-=1;trace("AOW|FADE1,"+i+","+probe._alpha);}
 probe.removeMovieClip();
 trace("AOW|DONE,properties");
};
