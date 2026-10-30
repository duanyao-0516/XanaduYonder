package {
import flash.display.Sprite;
import flash.external.ExternalInterface;
var probe:Sprite=new Sprite();
var values:Array=[0.01,0.024,0.025,0.049,0.05,0.099,-0.01,-0.024,-0.025,-0.049,-0.05,-0.099,150.1,150.1+0.7,151.45+0.7,-151.45-0.7];
for(var i:int=0;i<values.length;i++){probe.x=values[i];probe.y=values[i];trace("AS3TWIP,"+values[i]+","+probe.x+","+probe.y);}
trace("DONE,AS3PROBE");
}
