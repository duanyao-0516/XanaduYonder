using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace XanaduYonder.Core.AgeOfWar.Two
{
    /// <summary>Port of Alpha1 GameScreen, Hud, AI and Moon's seconds-based update contract.</summary>
    public sealed partial class AgeOfWarTwoBattle : IAgeOfWarBattle
    {
        internal readonly XElement Data = XElement.Parse(AgeOfWarTwoSourceData.Data);
        internal readonly XElement AiData = XElement.Parse(AgeOfWarTwoSourceData.Ai);
        internal readonly List<Unit> Units = new List<Unit>();
        internal readonly List<Turret> Turrets = new List<Turret>();
        internal readonly List<Projectile> Projectiles = new List<Projectile>();
        internal readonly List<Effect> Effects = new List<Effect>();
        internal readonly List<Animation> Animations = new List<Animation>();
        internal readonly List<int> Queue = new List<int>();
        internal readonly int[,] Upgrades = new int[2,18];
        readonly List<AgeOfWarEvent> events = new List<AgeOfWarEvent>();
        readonly List<XElement> aiEvents = new List<XElement>();
        XElement aiUnits;
        double aiTimer, aiUnitTimer, aiMin, aiMax, buildCrossFrame;
        internal int Age = 1, EnemyAge = 1, Gold = 2500, Experience = 1000, Population, EnemyPopulation;
        internal int Spots, EnemySpots, Building;
        internal int PlayerHealth = 1000, EnemyHealth = 1000, PlayerMaximumHealth = 1000, EnemyMaximumHealth = 1000;
        internal double BuildTimer, BuildMaximum, SpecialTimer = 60;
        internal readonly double Difficulty;
        long tick, nextId;
        internal long NativeTick;
        uint randomState;
        bool paused, ended, victory;
        int speed = 1, frameRate = 150;
        internal bool ShowDamage;
        int pendingTurret, pendingRefund;
        public int Edition { get { return 2; } }
        public int TickRate { get { return frameRate; } }
        public void SetFrameRate(int value) { if(value<25||value>300) throw new ArgumentOutOfRangeException("value"); frameRate=value; }
        public void SetShowDamage(bool value) { ShowDamage=value; }
        public void HoverUnit(long instanceId) { foreach(var unit in Units) unit.ShowHealth=unit.InstanceId==instanceId; }
        internal static bool Counter(string attacker,string target)
        {
            return attacker=="Support"&&target=="Anti-Armor"||attacker=="Infantry"&&target=="Support"||attacker=="Anti-Armor"&&target=="Armored"||attacker=="Armored"&&target=="Infantry";
        }
        internal void ApplyDamage(Unit target,int amount,bool critical=false)
        {
            target.Health-=amount;
            if(ShowDamage) Effects.Add(new Effect(this,target.X,-target.Height,amount,critical));
        }
        public AgeOfWarTwoBattle(double difficulty, uint seed)
        {
            if(difficulty != .6 && difficulty != 1 && difficulty != 1.5 && difficulty != 2) throw new ArgumentOutOfRangeException("difficulty");
            Difficulty = difficulty; randomState = seed == 0 ? 0x9e3779b9u : seed;
            var start = Data.Element("start");
            Gold = I(start,"money"); Experience = I(start,"xp"); Age = I(start,"age"); EnemyAge = I(start,"ageCPU");
            PlayerHealth = EnemyHealth = PlayerMaximumHealth = EnemyMaximumHealth = I(Data.Element("bases").Elements().First(),"health");
            LoadAiAge();
        }
        internal long Id() { return ++nextId; }
        internal double Random()
        {
            randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
            return randomState / 4294967296.0;
        }
        // AVM2 DisplayObject x/y stores signed integer twips; each property write truncates toward zero.
        // Keep the multiplication before truncation: binary Number boundaries are observable in the original.
        internal static double DisplayPosition(double value) { return Math.Truncate(value*20)/20; }
        internal static double DisplayAlpha(double value) { return Math.Truncate(value*256)/256; }
        internal static double DisplayRotation(double value) { value%=360;if(value>180)value-=360;else if(value< -180)value+=360;return value; }
        internal static int Round(double n) { return (int)Math.Floor(n + .5); }
        internal static double N(XElement x,string name) { var a=x==null?null:x.Attribute(name); double n; return a!=null && double.TryParse(a.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out n)?n:0; }
        internal static int I(XElement x,string name) { return (int)N(x,name); }
        internal static string S(XElement x,string name) { return x==null?"":(string)x.Attribute(name)??""; }
        internal XElement Definition(string group,int id) { return Data.Element(group).Elements().ElementAt(id-1); }
        internal double Bonus(string name,int side)
        {
            var defs=Data.Element("upgrades").Elements().ToArray();
            for(int i=0;i<defs.Length;i++) if(S(defs[i],"name")==name && Upgrades[side-1,i]!=0) return N(defs[i].Elements().ElementAt(Upgrades[side-1,i]),"bonus");
            return 0;
        }
        internal int Damage(string attacker,string target,int min,int max,int side)
        {
            double bonus=attacker=="Support"||attacker=="Infantry"||attacker=="Anti-Armor"||attacker=="Armored"?Bonus(attacker+" Damage",side):0;
            double amount=Round((min+Random()*(max-min))*(1+bonus));
            if(Counter(attacker,target)) amount*=1.5;
            return (int)Math.Ceiling(amount);
        }
        internal void Sound(string csv,double x)
        {
            if(csv.Length==0) return;
            var sounds=csv.Split(','); Emit("sound",x,0,0,sounds[Round(Random()*(sounds.Length-1))]);
        }
        internal void Emit(string kind,double x=0,double y=0,int value=0,string sound="") { events.Add(new AgeOfWarEvent{Kind=kind,X=x,Y=y,Value=value,Sound=sound}); }
        public AgeOfWarEvent[] DrainEvents() { var result=events.ToArray(); events.Clear(); return result; }
        internal bool VisibleUnit(int id) { return id >= (Age==7?24:(Age-1)*4+1) && id <= Math.Min(Age*4,29)+(Age==7?1:0); }
        internal bool LockedUnit(int id)
        {
            // Hud.checkUnitButtonAvail resets unavailableUnits on each iteration. Only the final visible unit remains blocked.
            int last=Age==7?29:Age*4; if(id!=last) return false;
            return RequirementLocked(id);
        }
        internal bool RequirementLocked(int id)
        {
            string required=S(Definition("units",id),"needUpgrade");
            var defs=Data.Element("upgrades").Elements().ToArray();
            for(int i=0;i<defs.Length;i++) if(S(defs[i],"name")==required) return Upgrades[0,i]==0;
            return false;
        }
        internal static int TurretAge(int id) { return id<=3?1:id==4?2:id<=6?3:id<=9?4:id<=11?5:id<=14?6:7; }
        bool Reject(string message) { events.Add(new AgeOfWarEvent {Kind="error",Message=message}); return false; }
        public bool Execute(AgeOfWarCommand command)
        {
            if(ended) return false;
            if(command.Action==AgeOfWarAction.Pause) { paused=!paused; return true; }
            if(command.Action==AgeOfWarAction.Speed) { speed=speed==1?2:1; return true; }
            if(paused) return false;
            int id=command.Id;
            switch(command.Action)
            {
                case AgeOfWarAction.Train:
                    if(id<1||id>29||!VisibleUnit(id)) return false; if(LockedUnit(id)) return Reject("Unit locked");
                    var unit=Definition("units",id);
                    if(Queue.Count(x=>x==id)>4) return Reject("Unit queue full"); if(Population+I(unit,"pop")>10) return Reject("Not enough population room"); if(Gold<I(unit,"cost")) return Reject("Not enough Gold");
                    Gold-=I(unit,"cost"); Population+=I(unit,"pop"); Queue.Add(id); Emit("queued",0,0,id); return true;
                case AgeOfWarAction.Upgrade:
                    if(id<1||id>18||(id<=7&&id!=Age)) return false;
                    var upgrade=Definition("upgrades",id).Elements().ToArray(); int level=Upgrades[0,id-1];
                    if(level>=upgrade.Length-1) return Reject("Upgrade is at the maximum level"); if(Gold<I(upgrade[level],"cost")) return Reject("Not enough money");
                    Gold-=I(upgrade[level],"cost"); Upgrades[0,id-1]++; Emit("upgrade",0,0,id); return true;
                case AgeOfWarAction.BuyTurretSlot:
                    if(Spots>=4) return false; int cost=I(Definition("towers",Spots+1),"cost"); if(Gold<cost) return Reject("Not enough Gold");
                    Gold-=cost; Spots++; return true;
                case AgeOfWarAction.BuyTurret:
                    if(id<1||id>17||TurretAge(id)!=Age) return false;
                    int price=I(Definition("turrets",id),"cost"); if(Gold<price) return Reject("Not enough Gold");
                    if(Turrets.Count(t=>t.Alive&&t.Side==1)>=Spots) return Reject("No more free turret spot");
                    Gold-=price; pendingTurret=id; pendingRefund=price; return true;
                case AgeOfWarAction.PlaceTurret:
                    int spot=command.Slot;
                    if(pendingTurret==0||spot<1||spot>Spots||Turrets.Any(t=>t.Alive&&t.Side==1&&t.Spot==spot)) return false;
                    CreateTurret(pendingTurret,1,spot); pendingTurret=0; pendingRefund=0; return true;
                case AgeOfWarAction.Cancel:
                    if(pendingTurret==0) return false;
                    Gold+=pendingRefund; pendingTurret=0; pendingRefund=0; return true;
                case AgeOfWarAction.SellTurret:
                    var turret=Turrets.FirstOrDefault(t=>t.Alive&&t.Side==1&&t.Spot==command.Slot); if(turret==null) return Reject("You have no turret to sell");
                    Sell(turret); return true;
                case AgeOfWarAction.Evolve:
                    if(Age>=7) return false; int xp=I(Definition("evolutions",Age),"cost"); if(Experience<xp) return Reject("Not enough XP");
                    Experience-=xp; Evolve(1); return true;
                case AgeOfWarAction.Special:
                    if(SpecialTimer!=0) return Reject("Special attack is unavailable"); int charge=I(Definition("specials",Age),"cost"); if(Experience<charge) return Reject("Not enough XP");
                    Experience-=charge; CreateSpecial(1,Age); SpecialTimer=60; return true;
                default: return false;
            }
        }
        internal void Evolve(int side)
        {
            if(side==1) { Age++; int hp=I(Definition("bases",Age),"health"); PlayerHealth+=hp-PlayerMaximumHealth; PlayerMaximumHealth=hp; }
            else { EnemyAge++; int hp=I(Definition("bases",EnemyAge),"health"); EnemyHealth+=hp-EnemyMaximumHealth; EnemyMaximumHealth=hp; }
            Emit("evolve",0,0,side);
        }
        internal void DamageBase(int side,int amount)
        {
            if(side==1) EnemyHealth=Math.Max(0,EnemyHealth-amount); else PlayerHealth=Math.Max(0,PlayerHealth-amount);
            if(ShowDamage) Effects.Add(new Effect(this,side==1?580:-580,-140,amount,false));
            if((side==1?EnemyHealth:PlayerHealth)==0) { ended=true; victory=side==1; Emit(victory?"victory":"defeat"); }
        }
        public void Tick() { Step(1.0/TickRate); }
        /// <summary>One actual Flash stage event. Moon receives elapsed seconds; native clips advance once.</summary>
        public void Step(double elapsedSeconds,Action<double> cameraPhase=null)
        {
            if(double.IsNaN(elapsedSeconds)||double.IsInfinity(elapsedSeconds)||elapsedSeconds<0)throw new ArgumentOutOfRangeException("elapsedSeconds");
            if(ended) return; NativeTick++; if(paused) return; tick++; double dt=speed*elapsedSeconds;
            // GameScreen: HUD -> animations -> units -> turrets -> projectiles -> dynamic objects -> AI.
            if(SpecialTimer>0) SpecialTimer-=dt; else if(SpecialTimer!=0) SpecialTimer=0;
            if(Queue.Count>0&&Building==0) { Building=Queue[0]; Queue.RemoveAt(0); BuildMaximum=BuildTimer=N(Definition("units",Building),"creationtime"); }
            if(Building!=0) { if(BuildTimer>0) BuildTimer-=dt; else { CreateUnit(Building,1); Building=0; } }
            // Hidden cross clips stay registered with AnimationManager; their clocks freeze only on game pause.
            buildCrossFrame=(buildCrossFrame+dt*60)%30;
            foreach(var turret in Turrets)turret.SellFrame=(turret.SellFrame+dt*30)%30;
            for(int i=0;i<Animations.Count;i++) Animations[i].Render(dt);
            int count=Units.Count;
            for(int i=0;i<count;i++) if(Units[i].Alive) Units[i].Render(dt); else Units[i].Destroy();
            Units.RemoveAll(u=>!u.Alive);
            count=Turrets.Count;
            for(int i=0;i<count;i++) if(Turrets[i].Alive) Turrets[i].Render();
            Turrets.RemoveAll(t=>!t.Alive);
            if(cameraPhase!=null)cameraPhase(dt);
            count=Projectiles.Count;
            for(int i=0;i<count;i++) if(Projectiles[i].Alive) Projectiles[i].Render(dt); else Projectiles[i].Remove=true;
            // Managers remove objects only on the following render after isAlive changes.
            Projectiles.RemoveAll(p=>p.Remove);
            count=Effects.Count;
            for(int i=0;i<count;i++) if(Effects[i].Alive) Effects[i].Render(dt); else Effects[i].Destroy();
            Effects.RemoveAll(e=>!e.Alive&&e.Removed);
            RenderAi(dt);
        }
        internal Unit CreateUnit(int id,int side) { var u=new Unit(this,id,side); Units.Add(u); Emit("spawn",u.X,0,id); return u; }
        internal Turret CreateTurret(int id,int side,int spot) { var t=new Turret(this,id,side,spot); Turrets.Add(t); return t; }
        internal void Sell(Turret t) { t.Alive=false; if(t.Side==1) Gold+=I(Definition("turrets",t.TypeId),"cost")/2; }
        void LoadAiAge() { aiEvents.Clear(); aiEvents.AddRange(AiData.Element("ai").Elements().ElementAt(EnemyAge-1).Elements("event")); NextAiEvent(); }
        void NextAiEvent()
        {
            if(aiEvents.Count==0) { if(EnemyAge<7) { Evolve(2); LoadAiAge(); } return; }
            var e=aiEvents[0]; aiEvents.RemoveAt(0);
            foreach(var sell in e.Element("sell").Elements("turret")) { var t=Turrets.FirstOrDefault(v=>v.Alive&&v.Side==2&&v.Spot==I(sell,"spot")); if(t!=null) Sell(t); }
            foreach(var tower in e.Element("build").Elements("tower")) EnemySpots++;
            foreach(var t in e.Element("build").Elements("turret")) CreateTurret(I(t,"id"),2,I(t,"spot"));
            foreach(var special in e.Element("build").Elements("special")) CreateSpecial(2,EnemyAge);
            aiTimer=N(e,"duration"); aiUnits=e.Element("units"); aiMin=N(aiUnits,"min"); aiMax=N(aiUnits,"max"); aiUnitTimer=Random()*(aiMax-aiMin)+aiMin;
        }
        void RenderAi(double dt)
        {
            aiTimer-=dt; aiUnitTimer-=dt;
            if(aiTimer<=0) NextAiEvent();
            if(aiUnitTimer<=0)
            {
                double n=Random(), sum=0;
                foreach(var choice in aiUnits.Elements("unit"))
                {
                    double ratio=N(choice,"ratio");
                    if(n<sum+ratio) { int id=I(choice,"id"), pop=I(Definition("units",id),"pop"); if(EnemyPopulation+pop<=10) { EnemyPopulation+=pop; CreateUnit(id,2); } break; }
                    sum+=ratio;
                }
                aiUnitTimer=Random()*(aiMax-aiMin)+aiMin;
            }
        }
        public AgeOfWarFrame Capture()
        {
            var visuals=new List<AgeOfWarVisual>();
            visuals.Add(new AgeOfWarVisual { InstanceId=-1, Clip="base/"+Age, Animation="idle", X=-491.3, Y=100, Depth=0 });
            visuals.Add(new AgeOfWarVisual { InstanceId=-2, Clip="base/"+EnemyAge, Animation="idle", X=490.7, Y=100, ScaleX=-1, Depth=0 });
            double[] towerY={-243.75,-290.2,-336.6,-383};
            for(int side=1;side<=2;side++) for(int slot=1;slot<=(side==1?Spots:EnemySpots);slot++) visuals.Add(new AgeOfWarVisual {InstanceId=-10-side*4-slot,Clip="base-extension/"+slot+"/"+(side==1?Age:EnemyAge),Animation="idle",X=side==1?-621.8:621.2,Y=100+towerY[slot-1],ScaleX=side==1?.75:-.75,ScaleY=.75,Depth=1});
            foreach(var u in Units) visuals.Add(u.Visual());
            foreach(var t in Turrets) if(t.Alive) { visuals.Add(t.BaseVisual()); visuals.Add(t.Visual()); }
            foreach(var p in Projectiles) visuals.Add(p.Visual());
            foreach(var e in Effects) if(!e.Emitter) visuals.Add(e.Visual());
            var slots=new List<AgeOfWarTurretSlot>();
            for(int side=1;side<=2;side++) for(int s=1;s<=4;s++) { var t=Turrets.FirstOrDefault(v=>v.Alive&&v.Side==side&&v.Spot==s); slots.Add(new AgeOfWarTurretSlot { Slot=s, Enemy=side==2, Purchased=s<=(side==1?Spots:EnemySpots), TurretId=t==null?0:t.TypeId,X=side==1?-624:620,Y=-146-(s-1)*47,SellFrame=t==null?0:(int)t.SellFrame,Scale=t==null?1:t.Scale }); }
            var offers=new List<AgeOfWarOffer>();
            for(int id=1;id<=29;id++) if(VisibleUnit(id)) { var d=Definition("units",id); offers.Add(new AgeOfWarOffer {Id=id,Age=Math.Min(7,(id-1)/4+1),Name=S(d,"name"),Description=S(d,"desc")+" ["+S(d,"unitType")+"]",Requirement=S(d,"needUpgrade")=="none"?"":S(d,"needUpgrade"),LockedAppearance=RequirementLocked(id),Cost=I(d,"cost"),Icon="unit/"+id,Enabled=!LockedUnit(id)&&Gold>=I(d,"cost")&&Population+I(d,"pop")<=10&&Queue.Count(v=>v==id)<=4}); }
            var turretOffers=new List<AgeOfWarOffer>();
            for(int id=1;id<=17;id++) if(TurretAge(id)==Age) { var d=Definition("turrets",id); turretOffers.Add(new AgeOfWarOffer {Id=id,Age=Age,Name=S(d,"name"),Description=S(d,"desc"),Cost=I(d,"cost"),Icon="turret/"+id,Enabled=Gold>=I(d,"cost")&&Turrets.Count(t=>t.Alive&&t.Side==1)<Spots}); }
            var upgrades=new List<AgeOfWarOffer>();
            for(int id=1;id<=18;id++) if(id==Age||id>7) { var d=Definition("upgrades",id); int level=Upgrades[0,id-1]; var levels=d.Elements().ToArray(); upgrades.Add(new AgeOfWarOffer {Id=id,Age=id<=7?id:0,Name=S(d,"name"),Description=S(levels[level],"desc"),Cost=I(levels[level],"cost"),Level=level,MaximumLevel=levels.Length-1,Enabled=level<levels.Length-1&&Gold>=I(levels[level],"cost")}); }
            var queue=new List<int>(); if(Building!=0) queue.Add(Building); queue.AddRange(Queue);
            return new AgeOfWarFrame { Edition=2,Tick=tick,Age=Age,EnemyAge=EnemyAge,Gold=Gold,Experience=Experience,Population=Population,MaximumPopulation=10,Speed=speed,Paused=paused,Ended=ended,Victory=victory,PlayerHealth=PlayerHealth,PlayerMaxHealth=PlayerMaximumHealth,EnemyHealth=EnemyHealth,EnemyMaxHealth=EnemyMaximumHealth,SpecialReady=SpecialTimer==0,SpecialProgress=Math.Max(0,Math.Min(1,1-SpecialTimer/60)),TrainingProgress=Building==0?0:Math.Max(0,Math.Min(1,1-BuildTimer/BuildMaximum)),NextEvolutionExperience=Age<7?I(Definition("evolutions",Age),"cost"):0,NextTurretSlotCost=Spots<4?I(Definition("towers",Spots+1),"cost"):0,SpecialExperienceCost=I(Definition("specials",Age),"cost"),PendingTurretId=pendingTurret,BuildTurretFrame=(int)buildCrossFrame,WorldLeft=-700,WorldRight=700,GroundY=0,Visuals=visuals.ToArray(),TurretSlots=slots.ToArray(),TrainingQueue=queue.ToArray(),Units=offers.ToArray(),Turrets=turretOffers.ToArray(),Upgrades=upgrades.ToArray() };
        }
    }
}
