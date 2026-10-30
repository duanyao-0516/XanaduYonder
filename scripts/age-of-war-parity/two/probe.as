package {
import flash.display.MovieClip;
import flash.events.MouseEvent;
import flash.utils.setInterval;
import flash.utils.clearInterval;
import net.louissi.Alpha1.Global.GlobalVariables;
import net.louissi.Alpha1.Screens.GameScreen;
import net.louissi.Alpha1.Screens.Preloader;
import net.louissi.Alpha1.units.Unit;
import net.louissi.Moon.Managers.ScreenManager;
import net.louissi.Moon.Screens.Screen;
import net.louissi.Moon.Transitions.Transition_Fade;
internal class ProbeGame extends GameScreen {
 private var probeStarted:Boolean=false;
 private var probeFrame:int=0;
 private var probeElapsed:Number=0;
 public function ProbeGame(){super();}
 override public function render(dt:Number):void {
  if(!probeStarted){
   probeStarted=true;
   var panel:MovieClip=MovieClip(hud.getChildAt(0));
   panel.tutorial.clickBlocker.dispatchEvent(new MouseEvent(MouseEvent.CLICK));
   panel.buttons.btnU2.dispatchEvent(new MouseEvent(MouseEvent.CLICK));
   trace("AOW|HEADER,frame,dt,elapsed,gold,xp,pop,units,x,health,speed,animationFrame,animationLabel");
  }
  super.render(dt);
  probeFrame++;probeElapsed+=dt;
  var player:Unit=null;
  for each(var unit:Unit in unitsManager.units){if(unit.side==1){player=unit;break;}}
  var row:String="AOW|ROW,"+probeFrame+","+dt+","+probeElapsed+","+GlobalVariables.money+","+GlobalVariables.xp+","+GlobalVariables.pop+","+(player?1:0);
  if(player){var clip:MovieClip=MovieClip(player.getChildAt(0));row+=","+player.x+","+player.health+","+player.speed+","+clip.currentFrame+","+clip.currentLabel;}
  trace(row);
  if(probeElapsed>=4){trace("AOW|DONE,player-training-and-walk");GlobalVariables.isPaused=true;}
 }
}
var probeInterval:uint=setInterval(function():void{
 var manager:ScreenManager=ScreenManager.getInstance();
 if(manager.currentScreen is Preloader&&Screen(manager.currentScreen).isLoaded){
  clearInterval(probeInterval);GlobalVariables.difficulty=1;
  manager.loadScreen(new ProbeGame(),new Transition_Fade(.1));
 }
},100);
}
