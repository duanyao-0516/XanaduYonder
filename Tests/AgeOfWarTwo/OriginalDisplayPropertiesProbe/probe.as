package {
import flash.display.Sprite;
var probe:Sprite=new Sprite();
var alphaValues:Array=[-2,-0.1,0,0.0001,0.5,0.9999,1,1.1,2];
for(var i:int=0;i<alphaValues.length;i++){probe.alpha=alphaValues[i];trace("AS3ALPHA,"+alphaValues[i]+","+probe.alpha);}
var rotationValues:Array=[-720,-181,-180,-30.1,0,30.1,180,181,720];
for(var j:int=0;j<rotationValues.length;j++){probe.rotation=rotationValues[j];trace("AS3ROTATION,"+rotationValues[j]+","+probe.rotation);}
probe.rotation=0;
var scaleValues:Array=[-2,-1.2,-0.1,0,0.1,0.3333333333333333,1.0001,1.2,2];
for(var k:int=0;k<scaleValues.length;k++){probe.scaleX=scaleValues[k];probe.scaleY=scaleValues[k];trace("AS3SCALE,"+scaleValues[k]+","+probe.scaleX+","+probe.scaleY);}
}
