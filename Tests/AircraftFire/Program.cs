using System;
using XanaduYonder.Core.AircraftFire;

internal static class Program
{
    private static int passed;
    private static int Main()
    {
        try {
            Run("boss completes burst before moving again", BossBurst);
            Run("classic crazy applies before time progression", CrazyProgression);
            Run("eased enemies leave the field", EasedDeparture);
            Run("stage medium fires aimed projectiles once", MediumFire);
            Run("bullets hit the nearest target and award once", BulletHits);
            Run("fresh game and safe movement", FreshGame);
            Run("fixed step partition and bounded delta", TimeSteps);
            Run("pause freezes simulation and input", Pause);
            Run("automatic fire and double weapon consumption", Weapons);
            Run("bomb awards each kill once and damages boss", Bomb);
            Run("collision grace, game over and one revive", Lives);
            Run("first stage boss and next stage transition", Stage);
            Run("resize preserves relative layout", Resize);
            Console.WriteLine("PASS: " + passed + " Aircraft Fire scenarios."); return 0;
        } catch(Exception error) { Console.Error.WriteLine("FAIL: " + error.Message); return 1; }
    }
    private static AircraftGame New(AircraftMode mode = AircraftMode.Classic) { return new AircraftGame(mode, false, 123, 1136); }
    private static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS: " + name); }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Near(double actual, double expected, string label) { Check(Math.Abs(actual-expected)<.01, label+": expected "+expected+", got "+actual); }
    private static void Step(AircraftGame game, float seconds) { for (int i=0;i<(int)(seconds*120);i++) game.Advance(1f/120); }
    private static void BossBurst()
    {
        var g=New(AircraftMode.Level); var boss=g.SpawnEnemy(AircraftEnemyKind.Boss,320,959,100);
        boss.BurstRemaining=1; boss.BurstTimer=0; Step(g,.01f); var x=boss.X;
        Step(g,.2f); Near(boss.X,x,"boss holds position through final burst delay");
        Step(g,.2f); Check(Math.Abs(boss.X-x)>1,"boss resumes movement after burst delay");
    }
    private static void CrazyProgression()
    {
        Near(AircraftRules.ClassicSpeed(AircraftEnemyKind.Small,true,600,0),1000,"crazy small speed base at600sec");
        Near(AircraftRules.ClassicSpeed(AircraftEnemyKind.Medium,true,600,0),900,"crazy medium speed base at600sec");
        Near(AircraftRules.ClassicSpeed(AircraftEnemyKind.Large,true,600,0),400,"crazy large speed base at600sec");
        Near(AircraftRules.ClassicInterval(AircraftEnemyKind.Small,true,600,.5f),.125,"crazy small interval floor");
        Near(AircraftRules.ClassicInterval(AircraftEnemyKind.Medium,true,600,.5f),.125,"crazy medium interval floor");
        Near(AircraftRules.ClassicInterval(AircraftEnemyKind.Large,true,600,.5f),.125,"crazy large interval floor");
        Near(AircraftRules.ClassicInterval(AircraftEnemyKind.Large,false,600,.5f),7,"normal large interval");
        Near(AircraftRules.ClassicSpeed(AircraftEnemyKind.Small,false,600,.5f),1000,"normal small speed");
    }
    private static void EasedDeparture()
    {
        var g=New(AircraftMode.Level); var e=g.SpawnEnemy(AircraftEnemyKind.Small,50,500,100);
        e.Ease=2; e.TravelDuration=.1f; Step(g,.12f);
        foreach(var live in g.Enemies) Check(live.Id!=e.Id,"completed eased path must be removed");
    }
    private static void MediumFire()
    {
        var g=New(AircraftMode.Level); var e=g.SpawnEnemy(AircraftEnemyKind.Medium,500,900,250);
        e.ShotTimer=0; Step(g,.01f); int count=0;
        foreach(var b in g.Bullets) if(!b.Friendly){count++;Near(b.Width,12,"hostile bullet width");Near(b.Height,12,"hostile bullet height");}
        Check(count>=1 && count<=3,"stage medium emits1..3 aimed bullets");
        Step(g,.02f); int after=0; foreach(var b in g.Bullets) if(!b.Friendly){after++; Check(b.X<500,"medium aims toward opposite side");}
        Check(after==count,"medium fires only once");
        var classic=New(); e=classic.SpawnEnemy(AircraftEnemyKind.Medium,500,900,250); e.ShotTimer=0; Step(classic,.1f);
        foreach(var b in classic.Bullets) Check(b.Friendly,"classic medium does not fire");
    }
    private static void BulletHits()
    {
        var g=New(); var far=g.SpawnEnemy(AircraftEnemyKind.Small,320,390,0); var near=g.SpawnEnemy(AircraftEnemyKind.Small,320,290,0);
        Step(g,.25f); Check(near.Hp==0 && far.Hp==1 && g.Score==1000,"first bullet selects nearer enemy");
        g.DamageEnemy(near,1); Check(g.Score==1000,"removed enemy cannot award twice");
        var medium=g.SpawnEnemy(AircraftEnemyKind.Medium,100,500,0); g.DamageEnemy(medium,2);Check(medium.Hp==1 && g.Score==1000,"partial damage does not score");
        g.DamageEnemy(medium,1); Check(g.Score==7000,"third hit awards medium score");
    }
    private static void FreshGame()
    {
        var g=New(); Check(g.Lives==3 && g.Bombs==0 && g.Stage==1 && g.Phase==AircraftPhase.Running, "initial resources/phase");
        Near(g.HeroX,320,"hero center");
        g.MoveHero(-10000,-10000); Near(g.HeroX,68,"left clamp"); Near(g.HeroY,84,"bottom clamp");
        g.SetHeroPosition(10000,10000); Near(g.HeroX,572,"right clamp"); Near(g.HeroY,1052,"top clamp");
        g.MoveHero(float.NaN,0); g.SetHeroPosition(0,float.PositiveInfinity); Near(g.HeroX,572,"reject invalid input");
    }
    private static void TimeSteps()
    {
        var a=New(); var b=New(); for(int i=0;i<60;i++) a.Advance(1f/60); for(int i=0;i<120;i++) b.Advance(1f/120);
        Near(a.ElapsedTime,b.ElapsedTime,"partition time"); Check(a.Bullets.Count==b.Bullets.Count && a.Enemies.Count==b.Enemies.Count,"partition entities");
        for(int i=0;i<a.Enemies.Count;i++) { Near(a.Enemies[i].X,b.Enemies[i].X,"seeded x"); Near(a.Enemies[i].Y,b.Enemies[i].Y,"seeded y"); }
        var before=a.ElapsedTime; a.Advance(60); Check(a.ElapsedTime-before<=.251,"long frame must cap catchup");
        before=a.ElapsedTime; a.Advance(float.NaN); a.Advance(-1); a.Advance(float.PositiveInfinity); Near(a.ElapsedTime,before,"invalid time ignored");
    }
    private static void Pause()
    {
        var g=New(); Step(g,.5f); g.Pause(); var x=g.HeroX; var t=g.ElapsedTime; var count=g.Bullets.Count;
        g.Advance(.2f); g.MoveHero(50,50); Check(g.Phase==AircraftPhase.Paused && g.Bullets.Count==count,"paused entities"); Near(g.ElapsedTime,t,"paused clock");Near(g.HeroX,x,"paused input");
        g.Resume(); Step(g,.1f); Check(g.ElapsedTime>t,"resume");
    }
    private static void Weapons()
    {
        var g=New(); Step(g,.21f); Check(g.Bullets.Count==1 && g.Bullets[0].Friendly,"first automatic shot"); Near(g.Bullets[0].Width,12,"friendly bullet width"); Near(g.Bullets[0].Height,28,"friendly bullet height");
        g.SpawnPickup(AircraftPickupKind.DoubleBullet,g.HeroX,g.HeroY); Step(g,.01f); Check(g.DoubleShots==90,"double pickup grants90 volleys");
        Step(g,.2f); Check(g.DoubleShots==89,"one double volley consumes one charge");
        int doubleCount=0; foreach(var bullet in g.Bullets) if(bullet.Doubled) { doubleCount++; Near(bullet.X,g.HeroX,"wide double centers on hero");Near(bullet.Width,96,"wide double width"); }
        Check(doubleCount==1,"double weapon creates one wide projectile with power1");
        var power=New(); power.SpawnPickup(AircraftPickupKind.DoubleBullet,power.HeroX,power.HeroY);
        var target=power.SpawnEnemy(AircraftEnemyKind.Medium,power.HeroX,300,0); Step(power,.25f);
        Check(target.Hp==2,"one wide projectile inflicts one point of damage at its center");
    }
    private static void Bomb()
    {
        var g=New(AircraftMode.Level); Check(!g.UseBomb(),"empty bomb unavailable");
        g.SpawnPickup(AircraftPickupKind.Bomb,g.HeroX,g.HeroY); Step(g,.01f); Check(g.Bombs==1,"bomb collected");
        g.SpawnEnemy(AircraftEnemyKind.Small,50,500,0); g.SpawnEnemy(AircraftEnemyKind.Medium,150,500,0); g.SpawnEnemy(AircraftEnemyKind.Large,350,700,0);
        var boss=g.SpawnEnemy(AircraftEnemyKind.Boss,320,950,0); var hp=boss.Hp;
        Check(g.UseBomb(),"bomb use"); Check(g.Bombs==0 && boss.Hp==hp-30,"bomb boss damage"); Check(g.Score==37000,"bomb score");
        Check(!g.UseBomb() && g.Score==37000,"no repeated score");
    }
    private static void Lives()
    {
        var g=New(); for(int life=2;life>=0;life--) {
            g.SpawnEnemy(AircraftEnemyKind.Small,g.HeroX,g.HeroY+24,0); Step(g,.01f); Check(g.Lives==life,"life lost");
            if(life>0) { var saved=g.Lives; g.SpawnEnemy(AircraftEnemyKind.Small,g.HeroX,g.HeroY+24,0); Step(g,.1f); Check(g.Lives==saved,"grace protects"); Step(g,3.1f); }
        }
        Check(g.Phase==AircraftPhase.GameOver && g.CanRevive,"gameover revive available"); var t=g.ElapsedTime; Step(g,1);Near(g.ElapsedTime,t,"gameover frozen");
        Check(g.Revive() && g.Lives==3 && g.HeroInvulnerability>0 && g.Phase==AircraftPhase.Running,"revive reset");
        Check(g.Explosions.Count==0,"revive discards completed death effects");
        Check(!g.Revive() && !g.CanRevive,"only one revive per run");
    }
    private static void Stage()
    {
        var g=New(AircraftMode.Level); g.SetHeroPosition(68,84);
        // Remain away from hazards while exercising the real 60-second first wave.
        for(int i=0;i<60*120+1;i++) { foreach(var enemy in g.Enemies) if(enemy.Kind!=AircraftEnemyKind.Boss) enemy.X=600; g.Advance(1f/120); }
        Check(g.Phase==AircraftPhase.Running && g.BossActive,"boss appears at60sec");
        AircraftEnemy boss=null; foreach(var enemy in g.Enemies)if(enemy.Kind==AircraftEnemyKind.Boss)boss=enemy;
        Check(boss!=null && boss.MaxHp==300,"first boss HP"); g.DamageEnemy(boss,300);
        Check(g.Stage==2 && !g.BossActive && g.Phase==AircraftPhase.Running,"stage advances on boss kill");
        for(int i=2;i<=7;i++) { boss=g.SpawnEnemy(AircraftEnemyKind.Boss,320,950,0); g.DamageEnemy(boss,boss.MaxHp); }
        Check(g.Stage==7 && g.Phase==AircraftPhase.Completed,"seventh boss completes campaign");
    }
    private static void Resize()
    {
        var g=New(); g.SetHeroPosition(320,500); var enemy=g.SpawnEnemy(AircraftEnemyKind.Small,200,800,0);
        g.ResizeHeight(1600); Near(g.HeroY,500f*1600/1136,"hero resize"); Near(enemy.Y,800f*1600/1136,"enemy resize");
        g.ResizeHeight(float.NaN); g.ResizeHeight(10); Near(g.Height,1600,"invalid resize ignored");
    }
}
