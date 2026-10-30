using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.AgeOfWar.One
{
    /// <summary>Native port of the fixed 7k7k SWF's AVM1 rules, at its original 40 frames/second.</summary>
    public sealed partial class AgeOfWarOneBattle : IAgeOfWarBattle
    {
        private sealed class Actor
        {
            internal long Uid;
            internal int Id, Side, ScanCounter=3, DeathCounter, AnimationFrame=1, Timeline, Flame1, Flame2;
            internal double X,Y=425,Health,MaximumHealth,Damage,RangedDamage,Left,Top,Right,Bottom;
            internal double ClosestDistance,EnemyDistance,Limit=100;
            internal Actor Target;
            internal bool Dead, Removed, AnimationStopped, AnimationEntered, Initialized;
            internal string Animation="idle";
            internal double Width {get {return Right-Left;}}
            internal double Height {get {return Bottom-Top;}}
        }
        private sealed class Turret
        {
            internal int Id, Side, Slot, ScanCounter, ShootTimer, AnimationFrame=1;
            internal long Uid;
            internal double X,Y,Rotation,EnemyDistance=100000,EnemyHeight;
            internal Actor Target;
            internal bool Playing;
        }
        private readonly OneRandom _random;
        private readonly int _difficulty;
        private readonly List<Actor> _units=new List<Actor>();
        private readonly List<Turret> _turrets=new List<Turret>();
        private readonly List<int> _queue=new List<int>();
        private readonly List<AgeOfWarEvent> _events=new List<AgeOfWarEvent>();
        private readonly Actor _playerBase, _enemyBase;
        private int _gold=175,_experience,_age=1,_enemyAge=1,_addons,_enemyAddons,_specialTimer=1999,_pendingTurret,_specialChargeFrame;
        private int _training=-1,_trainingTotal,_techTimer,_unitLevel=1,_aiTimer,_enemyTraining=-1,_enemyTrainingId,_enemyPopulation;
        private bool _aiPending,_aiTrain,_paused,_ended,_victory,_healing;
        private long _tick,_nextUnit=1,_nextVisual=100000;
        public int Edition {get {return 1;}}
        public int TickRate {get {return 40;}}
        public int SpecialChargeFrame {get {return _specialChargeFrame;}}
        public AgeOfWarOneBattle(int difficulty,uint seed)
        {
            if(difficulty<1||difficulty>3)throw new ArgumentOutOfRangeException("difficulty");
            _difficulty=difficulty;_random=new OneRandom(seed);
            _playerBase=new Actor{Uid=-1,Side=1,X=50,Health=500,MaximumHealth=500,Left=-67.34635620117187,Top=-75.5,Right=119.45282897949217,Bottom=1};
            _enemyBase=new Actor{Uid=-2,Side=2,X=950,Health=500,MaximumHealth=500,Left=-95.23863067626954,Top=-75.5,Right=106.01419754028319,Bottom=1};
        }
        public bool Execute(AgeOfWarCommand command)
        {
            if(_ended)return false;
            if(command.Action==AgeOfWarAction.Pause){_paused=!_paused;return true;}
            // Space hides the menu; already-visible base build/sell buttons are outside it.
            if(_paused&&command.Action!=AgeOfWarAction.PlaceTurret&&command.Action!=AgeOfWarAction.SellTurret)return false;
            int id=command.Id;
            switch(command.Action)
            {
                case AgeOfWarAction.Train:
                    if(!UnitOffered(id)||_queue.Count==5||_gold<OneData.Units[id].Cost)return false;
                    _gold-=OneData.Units[id].Cost;_queue.Add(id);
                    if(_queue.Count==1)StartTraining(id);
                    return true;
                case AgeOfWarAction.BuyTurret:
                    if(command.Slot==0)
                    {
                        if(id<(_age-1)*3+1||id>_age*3||_gold<OneData.Turrets[id].Cost)return false;
                        _pendingTurret=id;return true;
                    }
                    if(id<(_age-1)*3+1||id>_age*3||command.Slot<1||command.Slot>_addons+1||FindTurret(1,command.Slot)!=null||_gold<OneData.Turrets[id].Cost)return false;
                    _gold-=OneData.Turrets[id].Cost;CreateTurret(id,1,command.Slot);return true;
                case AgeOfWarAction.PlaceTurret:
                    if(_pendingTurret==0||command.Slot<1||command.Slot>_addons+1||FindTurret(1,command.Slot)!=null)return false;
                    _gold-=OneData.Turrets[_pendingTurret].Cost;CreateTurret(_pendingTurret,1,command.Slot);_pendingTurret=0;return true;
                case AgeOfWarAction.Cancel:
                    bool pending=_pendingTurret!=0;_pendingTurret=0;return pending;
                case AgeOfWarAction.SellTurret:
                    var turret=FindTurret(1,command.Slot);if(turret==null)return false;
                    _gold+=Round(OneData.Turrets[turret.Id].Cost/2.0);_turrets.Remove(turret);return true;
                case AgeOfWarAction.BuyTurretSlot:
                    if(_addons==3||_gold<OneData.SlotCosts[_addons])return false;
                    _gold-=OneData.SlotCosts[_addons++];return true;
                case AgeOfWarAction.Evolve:
                    if(_age==5||_experience<OneData.Evolution[_age-1])return false;
                    _age++;_playerBase.Health+=300*_age;_playerBase.MaximumHealth+=300*_age;
                    Event("evolve",0,425,_age);return true;
                case AgeOfWarAction.Special:
                    if(_specialTimer!=2000)return false;
                    StartSpecial();_specialTimer=0;return true;
                default:return false;
            }
        }
        private bool UnitOffered(int id) {return id>=(_age-1)*3+1&&id<=(_age==5?16:_age*3);}
        private static int Round(double value) {return (int)Math.Floor(value+0.5);}
        private void Event(string kind,double x,double y,int value=0,string sound="") {_events.Add(new AgeOfWarEvent{Kind=kind,X=x,Y=y,Value=value,Sound=sound});}
        private void StartTraining(int id) {_training=_trainingTotal=OneData.Units[id].Training;}
        private void TickTraining()
        {
            if(_training>0)--_training;
            if(_training==0)
            {
                Spawn(_queue[0],1);_queue.RemoveAt(0);_training=-1;_trainingTotal=0;
                if(_queue.Count>0)StartTraining(_queue[0]);
            }
        }
        // AVM1 DisplayObject setters store integral twips. Ordinary rule numbers remain doubles.
        private static double DisplayPosition(double value) {return Math.Truncate(value*20)/20;}
        private static double DisplayAlphaPercent(double value) {return Math.Truncate(value/100*256)/256;}
        private static double DisplayRotation(double value) {value%=360;if(value>180)value-=360;else if(value< -180)value+=360;return value;}
        private Actor Spawn(int id,int side)
        {
            var unit=new Actor{Uid=_nextUnit++,Id=id,Side=side,X=side==1?150:860};
            _units.Add(unit);Event("spawn",unit.X,unit.Y,id);return unit;
        }
        private void InitializeActor(Actor unit)
        {
            var d=OneData.Units[unit.Id];double scale=unit.Side==1||_difficulty==1?1:_difficulty==2?1.3:2;
            unit.X=DisplayPosition(unit.X+_random.Next());unit.Health=d.Health*scale;unit.MaximumHealth=d.Health*scale;
            unit.Damage=d.Damage*scale;unit.RangedDamage=d.RangedDamage*scale;
            unit.Left=d.Left;unit.Top=d.Top;unit.Right=d.Right;unit.Bottom=d.Bottom;
            unit.ClosestDistance=unit.EnemyDistance=unit.Side==1?300:-300;
            unit.Timeline=OneData.UnitAnimations[unit.Id]["idle"];unit.AnimationEntered=true;unit.Initialized=true;
        }
        // Roster iteration is newest-to-oldest units, enemy base, player base, as in the AS array loops.
        private List<Actor> Roster()
        {
            var roster=new List<Actor>(_units.Count+2);
            for(int i=_units.Count-1;i>=0;--i)if(!_units[i].Dead&&!_units[i].Removed)roster.Add(_units[i]);
            roster.Add(_enemyBase);roster.Add(_playerBase);return roster;
        }
        public void Tick()
        {
            if(_ended)return;
            ++_tick;
            InitializeSpecial();
            // Observed AVM1 phases: old-to-new frame actions, then new-to-old enterFrame.
            // Clips attached by the menu/AI below first initialize in the following frame.
            for(int i=0;i<_units.Count;++i)
            {
                if(!_units[i].Initialized)InitializeActor(_units[i]);
                AdvanceUnitAnimation(_units[i]);
            }
            for(int i=_units.Count-1;i>=0;--i)TickUnit(_units[i]);
            _specialChargeFrame=Math.Max(0,Math.Min(999,Round(_specialTimer/2.0)-1));
            if(_specialTimer<2000)++_specialTimer;
            TickTraining();
            // Menu callbacks and special controllers deliberately keep running under PAUSED.
            TickSpecial();
            if(Round(_playerBase.Health)<0){End(false);return;}
            if(!_paused)
            {
                if(Round(_enemyBase.Health)<0){End(true);return;}
                TickEnemyAi();
            }
            for(int i=_turrets.Count-1;i>=0;--i)TickTurret(_turrets[i]);
            TickProjectiles();TickParticles();
            _units.RemoveAll(u=>u.Removed);
        }
        private void End(bool victory) {_ended=true;_victory=victory;Event(victory?"victory":"defeat",0,0);}
        private void TickUnit(Actor unit)
        {
            if(_paused){ChangeAnimation(unit,"idle");if(unit.AnimationEntered)AdvanceUnitAnimation(unit);AdvanceUnitFlames(unit);return;}
            if(unit.Health>0)
            {
                if(_healing&&unit.Side==1&&unit.Health<unit.MaximumHealth)unit.Health++;
                bool moving=Math.Abs(unit.ClosestDistance)>unit.Limit;
                if(moving)unit.X=DisplayPosition(unit.X+(unit.Side==1?0.7:-0.7));
                var def=OneData.Units[unit.Id];
                bool melee=Math.Abs(unit.EnemyDistance)<def.MeleeRange;
                bool ranged=!melee&&Math.Abs(unit.EnemyDistance)<def.ShootRange;
                if(!melee&&!moving&&!ranged)ChangeAnimation(unit,"idle");
                else if(melee&&!moving)ChangeAnimation(unit,"attack");
                else if(ranged&&moving)ChangeAnimation(unit,"shootwalk");
                else if(ranged&&!moving)ChangeAnimation(unit,"shoot");
                else if(!melee&&moving&&!ranged)ChangeAnimation(unit,"walk");
                if(--unit.ScanCounter==0){Scan(unit);unit.ScanCounter=5;}
            }
            else
            {
                if(!unit.Dead)
                {
                    unit.Dead=true;int reward=Round(OneData.Units[unit.Id].Cost*1.3);
                    if(unit.Side==2){--_enemyPopulation;_gold+=reward;_experience+=reward*2;CreateParticle(unit.X,unit.Y-unit.Height,1,0,reward);}
                    else _experience+=Round(reward/2.0);
                    Blood(unit,9);Event("death",unit.X,unit.Y,unit.Id);
                }
                ChangeAnimation(unit,"die");
                if(++unit.DeathCounter>80)unit.Removed=true;
            }
            if(unit.AnimationEntered)AdvanceUnitAnimation(unit);AdvanceUnitFlames(unit);
        }
        private void Scan(Actor unit)
        {
            unit.Target=null;unit.ClosestDistance=unit.EnemyDistance=unit.Side==1?300:-300;
            foreach(var other in Roster())
            {
                double distance=other.X-unit.X;
                if(unit.Side==1&&distance>0)
                {
                    if(unit.ClosestDistance>distance){unit.ClosestDistance=distance;unit.Limit=unit.Width/2+other.Width/2+2;}
                    if(other.Side!=unit.Side&&unit.EnemyDistance>distance-other.Width/2){unit.EnemyDistance=distance-other.Width/2;unit.Target=other;}
                }
                else if(unit.Side==2&&distance<0)
                {
                    if(unit.ClosestDistance<distance){unit.ClosestDistance=distance;unit.Limit=unit.Width/2+other.Width/2+2;}
                    if(other.Side!=unit.Side&&unit.EnemyDistance<distance+other.Width/2){unit.EnemyDistance=distance+other.Width/2;unit.Target=other;}
                }
            }
        }
        private void ChangeAnimation(Actor unit,string animation)
        {
            if(unit.Animation==animation)return;
            unit.Animation=animation;unit.Timeline=OneData.UnitAnimations[unit.Id][animation];unit.AnimationFrame=1;unit.AnimationStopped=false;unit.AnimationEntered=true;unit.Flame1=unit.Flame2=0;
            if(animation=="die"&&unit.Id!=3&&unit.Id!=6&&unit.Id!=12&&unit.Id!=15)Event("sound",unit.X,unit.Y,0,"sound/"+(239+_random.Next(5)));
        }
        private void AdvanceUnitAnimation(Actor unit)
        {
            if(unit.AnimationStopped)return;
            var timeline=OneData.Timelines[unit.Timeline];
            if(!unit.AnimationEntered){++unit.AnimationFrame;if(unit.AnimationFrame>timeline.Frames)unit.AnimationFrame=1;}
            unit.AnimationEntered=false;
            for(int jump=0;jump<8;++jump)
            {
                int destination=0;
                foreach(var e in timeline.Events)
                {
                    if(e.Frame!=unit.AnimationFrame)continue;
                    if(e.Kind=="melee"||e.Kind=="ranged")Hit(unit,e.Kind=="ranged");
                    else if(e.Kind=="stop")unit.AnimationStopped=true;
                    else if(e.Kind=="goto")destination=e.A;
                    else if(e.Kind=="variant")destination=_random.Next(2)==0?e.A:e.B;
                    else if(e.Kind=="sound")Event("sound",unit.X,unit.Y,0,"sound/"+e.A);
                }
                if(destination==0)break;unit.AnimationFrame=destination;
            }
        }
        private void Hit(Actor unit,bool ranged)
        {
            // ed_id is a cached reference, not revalidated for distance/liveness by hit().
            double damage=(ranged?unit.RangedDamage:unit.Damage)-_random.Next()*2;
            var target=unit.Target;if(target==null||target.Removed)return;
            target.Health-=damage;Event(ranged?"ranged-hit":"melee-hit",target.X,target.Y,unit.Id);
            if(target.Id!=0&&target.Id!=12)Blood(target,5);
        }
        private Turret FindTurret(int side,int slot) {return _turrets.Find(t=>t.Side==side&&t.Slot==slot);}
        private void CreateTurret(int id,int side,int slot)
        {
            // Source's enemy fourth-slot condition accidentally checks slot three.
            if(side==2&&slot==4&&FindTurret(2,3)!=null)return;
            if(FindTurret(side,slot)!=null)return;
            _turrets.Add(new Turret{Uid=_nextVisual++,Id=id,Side=side,Slot=slot,X=side==1?52:948,Y=425+new[]{-80,-125,-172,-220}[slot-1],Rotation=side==1?0:-180});
        }
        private void RemoveEnemyTurret(int slot){var t=FindTurret(2,slot);if(t!=null)_turrets.Remove(t);}
        private void TickTurret(Turret turret)
        {
            var def=OneData.Turrets[turret.Id];
            if(!_paused)
            {
                if(++turret.ScanCounter==10)
                {
                    turret.EnemyDistance=100000;
                    foreach(var enemy in Roster())
                    {
                        double dx=enemy.X-turret.X,dy=enemy.Y-turret.Y,dist=Math.Sqrt(dx*dx+dy*dy);
                        if(enemy.Side!=turret.Side&&turret.EnemyDistance>dist){turret.EnemyDistance=dist;turret.Target=enemy;turret.EnemyHeight=enemy.Height;}
                    }
                    turret.ScanCounter=0;
                }
                if(turret.EnemyDistance<def.Range)
                {
                    if(turret.Id!=6&&turret.Target!=null&&!turret.Target.Removed)turret.Rotation=Round(Math.Atan2(turret.Target.Y-turret.EnemyHeight/1.3-turret.Y,turret.Target.X-turret.X)*180/Math.PI);
                    if(++turret.ShootTimer>=def.Interval){turret.Playing=true;turret.ShootTimer=0;}
                }
            }
            // Child timeline play is independent of the turret's PAUSED-gated onEnterFrame.
            if(turret.Playing)
            {
                var timeline=OneData.Timelines[def.Timeline];
                if(++turret.AnimationFrame>timeline.Frames)turret.AnimationFrame=1;
                foreach(var e in timeline.Events)
                {
                    if(e.Frame!=turret.AnimationFrame)continue;
                    if(e.Kind=="stop")turret.Playing=false;
                    if(e.Kind=="shoot"){CreateProjectile(turret.X,turret.Y,turret.Rotation,def.Damage,def.Bullet.ToString(),turret.Side);Event("turret-fire",turret.X,turret.Y,turret.Id);}
                    if(e.Kind=="sound")Event("sound",turret.X,turret.Y,0,"sound/"+e.A);
                }
            }
        }
        private void TickEnemyAi()
        {
            ++_techTimer;
            if(_unitLevel==1&&_techTimer==1500)++_unitLevel;
            else if(_unitLevel==2&&_techTimer==5000)++_unitLevel;
            if(_techTimer==8000&&_enemyAge!=5){++_enemyAge;_enemyBase.Health+=300*_enemyAge;_enemyBase.MaximumHealth+=300*_enemyAge;_unitLevel=1;_techTimer=0;}
            switch(_enemyAge)
            {
                case 1:
                    if(_techTimer==1000)CreateTurret(1,2,1);
                    else if(_techTimer==4000){RemoveEnemyTurret(1);CreateTurret(2,2,1);}
                    else if(_techTimer==6000){RemoveEnemyTurret(1);CreateTurret(3,2,1);}break;
                case 2:
                    if(_techTimer==1000){RemoveEnemyTurret(1);CreateTurret(4,2,1);}
                    else if(_techTimer==4000){_enemyAddons=1;RemoveEnemyTurret(1);CreateTurret(6,2,1);}
                    else if(_techTimer==6000)CreateTurret(5,2,2);break;
                case 3:
                    if(_techTimer==1000){RemoveEnemyTurret(1);CreateTurret(7,2,1);}
                    else if(_techTimer==4000){_enemyAddons=2;RemoveEnemyTurret(2);CreateTurret(7,2,2);}
                    else if(_techTimer==6000){RemoveEnemyTurret(2);RemoveEnemyTurret(1);CreateTurret(9,2,3);}break;
                case 4:
                    if(_techTimer==5000)CreateTurret(10,2,1);
                    else if(_techTimer==7000){_enemyAddons=2;RemoveEnemyTurret(3);RemoveEnemyTurret(1);CreateTurret(11,2,2);}break;
                case 5:
                    if(_techTimer==5000)CreateTurret(13,2,1);
                    else if(_techTimer==12000){RemoveEnemyTurret(2);RemoveEnemyTurret(1);RemoveEnemyTurret(3);CreateTurret(14,2,2);}
                    else if(_techTimer==20000){_enemyAddons=3;RemoveEnemyTurret(2);RemoveEnemyTurret(1);RemoveEnemyTurret(3);CreateTurret(15,2,4);}break;
            }
            if(++_aiTimer==40){_aiTrain=_random.Next()<0.3;_aiPending=true;_aiTimer=0;}
            if(_aiPending&&_aiTrain&&_enemyTraining<-5&&_enemyPopulation<6)
            {
                _enemyTrainingId=_random.Next(_unitLevel)+1+(_enemyAge-1)*3;
                _enemyTraining=OneData.Units[_enemyTrainingId].Training;++_enemyPopulation;_aiPending=false;
            }
            if(_enemyTraining==0)Spawn(_enemyTrainingId,2);
            --_enemyTraining;_random.Next(); // Original unused make_unit draw still consumes the stream.
        }
        public AgeOfWarEvent[] DrainEvents(){var result=_events.ToArray();_events.Clear();return result;}
        public AgeOfWarFrame Capture()
        {
            var visuals=new List<AgeOfWarVisual>();
            visuals.Add(new AgeOfWarVisual{InstanceId=-1,Clip="base/"+_age,X=0,Y=374.25,Depth=10});
            visuals.Add(new AgeOfWarVisual{InstanceId=-2,Clip="base/"+_enemyAge,X=1002.25,Y=375,ScaleX=-1.0052490234375,Depth=11});
            for(int side=1;side<=2;++side)
                for(int addon=1;addon<=(side==1?_addons:_enemyAddons);++addon)
                    visuals.Add(new AgeOfWarVisual{InstanceId=-10-side*4-addon,Clip="base-extension/"+addon+"/"+(side==1?_age:_enemyAge),X=side==1?30.75:970.75,Y=276.5-47*(addon-1),ScaleX=side==1?1:-1,Depth=12});
            foreach(var unit in _units)
            {
                bool split=unit.Id==15&&unit.Animation=="die";
                visuals.Add(new AgeOfWarVisual{InstanceId=unit.Uid,Clip="unit/"+(unit.Initialized?unit.Id:1),Animation=split?"die-back":unit.Animation,Frame=unit.AnimationFrame-1,X=unit.X,Y=unit.Y,ScaleX=unit.Side==1?1:-1,Depth=100000+(int)unit.Uid*10,HealthFraction=unit.Initialized?unit.Health/unit.MaximumHealth:-1});
                if(split)visuals.Add(new AgeOfWarVisual{InstanceId=-5000000-unit.Uid,Clip="unit/15",Animation="die-front",Frame=unit.AnimationFrame-1,X=unit.X,Y=unit.Y,ScaleX=unit.Side==1?1:-1,Depth=100002+(int)unit.Uid*10});
                if(_healing&&unit.Side==1&&!unit.Dead)visuals.Add(new AgeOfWarVisual{InstanceId=-100000-unit.Uid,Clip="aura",X=unit.X,Y=unit.Y-20.1,Frame=_tick,ScaleX=unit.Width/18.45,Depth=100000+(int)unit.Uid*10});
            }
            foreach(var turret in _turrets)visuals.Add(new AgeOfWarVisual{InstanceId=turret.Uid,Clip="turret/"+turret.Id,Frame=turret.AnimationFrame-1,X=turret.X,Y=turret.Y,Rotation=turret.Rotation,ScaleY=turret.Side==1?1:-1,Depth=20+turret.Slot});
            AddEffectsVisuals(visuals);AddNestedVisuals(visuals);
            var slots=new List<AgeOfWarTurretSlot>();
            for(int side=1;side<=2;++side)for(int slot=1;slot<=4;++slot){var t=FindTurret(side,slot);slots.Add(new AgeOfWarTurretSlot{Slot=slot,Enemy=side==2,Purchased=slot<=(side==1?_addons:_enemyAddons)+1,TurretId=t==null?0:t.Id,X=side==1?52:948,Y=425+new[]{-80,-125,-172,-220}[slot-1]});}
            var units=new List<AgeOfWarOffer>();var turrets=new List<AgeOfWarOffer>();
            for(int id=(_age-1)*3+1;id<=(_age==5?16:_age*3);++id){var d=OneData.Units[id];units.Add(new AgeOfWarOffer{Id=id,Age=_age,Name=d.Name,Cost=d.Cost,Icon="unit/"+id,Enabled=!_paused&&!_ended&&_gold>=d.Cost&&_queue.Count<5,Description=d.Health+" HP; "+d.Damage+" melee; "+d.RangedDamage+" ranged; "+d.Training+" frames"});}
            for(int id=(_age-1)*3+1;id<=_age*3;++id){var d=OneData.Turrets[id];turrets.Add(new AgeOfWarOffer{Id=id,Age=_age,Name=d.Name,Cost=d.Cost,Icon="turret/"+id,Enabled=!_paused&&!_ended&&_gold>=d.Cost,Description=d.Damage+" damage; "+d.Range+" range"});}
            return new AgeOfWarFrame{Edition=1,Tick=_tick,Age=_age,EnemyAge=_enemyAge,Gold=_gold,Experience=_experience,Population=_units.FindAll(u=>u.Side==1&&!u.Dead).Count,MaximumPopulation=0,Paused=_paused,Ended=_ended,Victory=_victory,SpecialReady=_specialTimer==2000,SpecialProgress=_specialTimer/2000.0,TrainingProgress=_trainingTotal==0?0:1-_training/(double)_trainingTotal,PlayerHealth=_playerBase.Health,PlayerMaxHealth=_playerBase.MaximumHealth,EnemyHealth=_enemyBase.Health,EnemyMaxHealth=_enemyBase.MaximumHealth,NextEvolutionExperience=_age<5?OneData.Evolution[_age-1]:0,NextTurretSlotCost=_addons<3?OneData.SlotCosts[_addons]:0,PendingTurretId=_pendingTurret,WorldLeft=0,WorldRight=1000,GroundY=425,Visuals=visuals.ToArray(),TurretSlots=slots.ToArray(),TrainingQueue=_queue.ToArray(),Units=units.ToArray(),Turrets=turrets.ToArray()};
        }
    }
}
