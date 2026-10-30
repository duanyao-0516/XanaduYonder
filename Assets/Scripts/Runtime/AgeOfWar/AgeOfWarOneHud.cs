using System;
using UnityEngine;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.One;

namespace XanaduYonder.Runtime.AgeOfWar
{
    // Dynamic layers omitted from the static sprite212 menu: source sprites33/46/78/885.
    // Root View continues to own menu text, queue, production progress and ordinary menu buttons.
    internal sealed class AgeOfWarOneHud : IDisposable
    {
        private readonly AgeOfWarAssets _assets;
        private readonly RectTransform _root;
        private readonly AgeOfWarSceneView _player, _enemy, _cursor;
        private readonly AgeOfWarImage _turretCursor;
        private AgeOfWarSceneView _special;
        private int _age;
        private Vector2 _pointer;
        private bool _pointerVisible, _pointerDown;
        private float _viewportWidth=650;
        internal bool Selling { get; set; }

        internal AgeOfWarOneHud(Transform parent, AgeOfWarAssets assets)
        {
            _assets=assets;
            _root=AgeOfWarDrawing.Rect(parent,"One original dynamic HUD");
            _player=new AgeOfWarSceneView(_root,assets,"build-slots");
            _enemy=new AgeOfWarSceneView(_root,assets,"one-base-enemy");
            HideBaseArt(_player);HideBaseArt(_enemy);
            _cursor=new AgeOfWarSceneView(_root,assets,"one-cursor");
            _cursor.Named("tu",false);
            _turretCursor=new AgeOfWarImage(_cursor.Root,assets,"Selected original turret");
            // Source tu is depth1, below ar/se at6/8.
            _turretCursor.Root.SetAsFirstSibling();
            _cursor.Root.gameObject.SetActive(false);
        }
        private static void HideBaseArt(AgeOfWarSceneView scene)
        {
            scene.Named("base",false);scene.Named("bu",false);
            scene.Named("e1",false);scene.Named("e2",false);scene.Named("e3",false);
            for(int slot=1;slot<=4;++slot){scene.Named("b"+slot,false);scene.Named("s"+slot,false);}
        }
        internal void Pointer(Vector2 stagePoint,bool visible,bool down=false)
        {
            _pointer=stagePoint;_pointerVisible=visible;_pointerDown=down;
            ApplyHover();
        }

        internal void SetViewportWidth(float width)
        {
            _viewportWidth=Mathf.Max(650,width);
            if(_special!=null)_special.Root.anchoredPosition=new Vector2(_viewportWidth-35.8f,-85.9f);
        }

        private void ApplyHover()
        {
            _player.Hover(_pointerVisible?_player.Hit(_pointer):null,_pointerDown);
            if(_special!=null)_special.Hover(_pointerVisible?_special.Hit(_pointer):null,_pointerDown);
        }

        internal void Render(AgeOfWarOneBattle battle,AgeOfWarFrame frame,float cameraX,float cameraY)
        {
            if(battle==null||frame==null)throw new ArgumentNullException(battle==null?"battle":"frame");
            _player.Root.anchoredPosition=new Vector2(cameraX,-425-cameraY);
            _enemy.Root.anchoredPosition=new Vector2(1000+cameraX,-425-cameraY);
            RenderHealth(_player,frame.PlayerHealth,frame.PlayerMaxHealth);
            RenderHealth(_enemy,frame.EnemyHealth,frame.EnemyMaxHealth);
            foreach(var slot in frame.TurretSlots)
            {
                if(slot.Enemy)continue;
                _player.Named("b"+slot.Slot,frame.PendingTurretId!=0&&slot.Purchased&&slot.TurretId==0);
                _player.Named("s"+slot.Slot,Selling&&slot.TurretId!=0);
            }
            if(_special==null||_age!=frame.Age)
            {
                if(_special!=null)_special.Dispose();
                _age=frame.Age;_special=new AgeOfWarSceneView(_root,_assets,"one-special/"+_age,_viewportWidth-35.8f,85.9f);
            }
            _special.Root.gameObject.SetActive(!frame.Paused);
            // Sprite78 uses Math.round(timer/2) as one-based sprite69 frame.
            int chargeFrame=battle.SpecialChargeFrame;
            _special.Named("spt",true,chargeFrame);
            _cursor.Root.gameObject.SetActive(_pointerVisible&&!frame.Ended&&!Application.isMobilePlatform);
            _cursor.Root.anchoredPosition=new Vector2(_pointer.x,-_pointer.y);
            _cursor.Named("ar",true);_cursor.Named("se",Selling);_cursor.Named("tu",false);
            _turretCursor.Root.gameObject.SetActive(frame.PendingTurretId!=0);
            if(frame.PendingTurretId!=0)_turretCursor.Set("turret/"+frame.PendingTurretId,"idle",0,0,0,1,1,0,92/256.0);
            _cursor.Root.SetAsLastSibling();
            // Named above restores source layers, so reapply pointer state after visibility changes.
            ApplyHover();
        }
        private static void RenderHealth(AgeOfWarSceneView scene,double health,double maximum)
        {
            scene.Value("h",Math.Floor(health+.5).ToString(System.Globalization.CultureInfo.InvariantCulture));
            for(int i=0;i<scene.Images.Count;++i)
            {
                if(scene.Definition.layers[i].name!="hb")continue;
                var definition=scene.Definition.layers[i];
                scene.Images[i].Root.localScale=new Vector3(definition.scaleX,(float)(health/maximum),1);
            }
        }
        internal AgeOfWarHotspot Hit(Vector2 stagePoint)
        {
            if(_special!=null&&_special.Root.gameObject.activeSelf)
            {
                var special=_special.Hit(stagePoint);
                if(special!=null)return new AgeOfWarHotspot{action="special",id=_age,name=special.name,x=special.x,y=special.y,w=special.w,h=special.h};
            }
            var source=_player.Hit(stagePoint);
            if(source==null||string.IsNullOrEmpty(source.name)||source.name.Length!=2)return null;
            int slot;
            if(!int.TryParse(source.name.Substring(1),out slot)||slot<1||slot>4)return null;
            if(source.name[0]!='b'&&source.name[0]!='s')return null;
            return new AgeOfWarHotspot{action=source.name[0]=='b'?"build-slot":"sell-slot",id=slot,name=source.name,x=source.x,y=source.y,w=source.w,h=source.h};
        }
        public void Dispose()
        {_root.gameObject.SetActive(false);UnityEngine.Object.Destroy(_root.gameObject);}
    }
}
