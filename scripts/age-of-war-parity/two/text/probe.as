package {
import flash.display.MovieClip;
import flash.display.BitmapData;
import flash.display.DisplayObject;
import flash.display.DisplayObjectContainer;
import flash.geom.Matrix;
import flash.text.TextField;
import flash.text.TextLineMetrics;
import flash.text.TextFormat;
import flash.utils.setInterval;
import flash.utils.clearInterval;
import net.louissi.Alpha1.Global.GlobalVariables;
import net.louissi.Alpha1.Screens.GameScreen;
import net.louissi.Alpha1.Screens.Preloader;
import net.louissi.Moon.Managers.ScreenManager;
import net.louissi.Moon.Screens.Screen;
import net.louissi.Moon.Transitions.Transition_Fade;
internal class ProbeGame extends GameScreen {
 private var started:Boolean=false;
 public function ProbeGame(){super();}
 private function findText(parent:DisplayObjectContainer,name:String):TextField {
  for(var i:int=0;i<parent.numChildren;i++){
   var child:DisplayObject=parent.getChildAt(i);
   if(child is TextField&&child.name==name)return TextField(child);
   if(child is DisplayObjectContainer){var found:TextField=findText(DisplayObjectContainer(child),name);if(found)return found;}
  }
  return null;
 }
 private function sample(kind:String,field:TextField):void {
  if(!field){trace("AOW|ERROR,missing,"+kind);return;}
  field.text=kind=="window-M"?"M\nM\nM":"123456";field.x=0;field.y=0;
  var metric:TextLineMetrics=field.getLineMetrics(0);
  trace("AOW|PROP,"+stage.quality+","+kind+","+field.antiAliasType+","+field.gridFitType+","+field.sharpness+","+field.thickness+","+field.embedFonts+","+field.getTextFormat().font+","+field.getTextFormat().size+","+metric.ascent+","+metric.descent+","+metric.leading+","+metric.height);
  var bitmap:BitmapData=new BitmapData(160,40,true,0);bitmap.draw(field,new Matrix());
  var pixels:String="";var partial:int=0;var opaque:int=0;
  for(var y:int=0;y<40;y++)for(var x:int=0;x<160;x++){
   var pixel:uint=bitmap.getPixel32(x,y);var a:uint=pixel>>>24;if(a>0&&a<255)partial++;if(a==255)opaque++;
   var value:String=pixel.toString(16);while(value.length<8)value="0"+value;pixels+=value;
  }
  trace("AOW|PIXELS,"+stage.quality+","+kind+","+partial+","+opaque+","+pixels);bitmap.dispose();
 }
 override public function render(dt:Number):void {
  if(started)return;started=true;
  var windowClass:Class=windowsSWF.loaderInfo.applicationDomain.getDefinition("unitInfo") as Class;
  var window:MovieClip=new windowClass();
  var damageClass:Class=GlobalVariables.particules.loaderInfo.applicationDomain.getDefinition("damage") as Class;
  var damage:MovieClip=new damageClass();
  var error:TextField=findText(hud,"errorMsg");var money:TextField=findText(hud,"moneyTXT");var cost:TextField=findText(window,"costTXT");var number:TextField=damage.damageNorm.damageTXT;
  for each(var quality:String in ["LOW","MEDIUM","HIGH"]){stage.quality=quality;sample("hud-money",money);sample("hud-error",error);sample("window-cost",cost);sample("damage",number);}
  cost.multiline=true;cost.wordWrap=true;cost.width=200;cost.height=100;cost.text="M\nM\nM";
  for(var n:int=0;n<cost.numLines;n++){var m:TextLineMetrics=cost.getLineMetrics(n);trace("AOW|LINES,"+n+","+m.ascent+","+m.descent+","+m.leading+","+m.height+","+cost.getCharBoundaries(cost.getLineOffset(n)).y);}
  sample("window-M",cost);
  var format:TextFormat=number.getTextFormat();format.size=14.05;number.defaultTextFormat=format;number.setTextFormat(format);sample("damage-fractional-size-probe",number);
  trace("AOW|DONE,text-quality-metrics");
 }
}
var interval:uint=setInterval(function():void{var manager:ScreenManager=ScreenManager.getInstance();if(manager.currentScreen is Preloader&&Screen(manager.currentScreen).isLoaded){clearInterval(interval);manager.loadScreen(new ProbeGame(),new Transition_Fade(.1));}},100);
}
