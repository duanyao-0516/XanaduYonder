using System;

namespace XanaduYonder.Core.AgeOfWar
{
    public enum AgeOfWarAction { Train, BuyTurret, PlaceTurret, Cancel, SellTurret, BuyTurretSlot, Evolve, Special, Upgrade, Pause, Speed }
    public struct AgeOfWarCommand
    {
        public AgeOfWarAction Action;
        public int Id, Slot;
        public double X, Y;
        public AgeOfWarCommand(AgeOfWarAction action, int id = 0, int slot = 0) { Action = action; Id = id; Slot = slot; X = Y = 0; }
    }
    // Presentation copies only. Each edition owns its original rules and update order.
    public sealed class AgeOfWarOffer
    {
        public int Id, Age, Cost, ExperienceCost, Level, MaximumLevel;
        public string Name = "", Description = "", Icon = "";
        public bool Enabled = true;
        public bool LockedAppearance;
        public string Requirement = "";
    }
    public sealed class AgeOfWarVisual
    {
        public long InstanceId;
        public string Clip = "", Animation = "idle";
        public string Text = "", TextColor = "", TextScene = "", TextField = "";
        public double X, Y, Frame;
        public double ScaleX = 1, ScaleY = 1, Rotation, Alpha = 1;
        public int Depth;
        public double HealthFraction = -1;
        public double HealthAlpha, HealthY, HitWidth, HitHeight;
    }
    public sealed class AgeOfWarTurretSlot
    {
        public int Slot, TurretId;
        public double X, Y;
        public double SellFrame, Scale = 1;
        public bool Enemy, Purchased;
    }
    public sealed class AgeOfWarEvent
    {
        public string Kind = "", Sound = "", Message = "";
        public double X, Y;
        public int Value;
    }
    public sealed class AgeOfWarFrame
    {
        public int Edition, Age, EnemyAge, Gold, Experience, Population, MaximumPopulation, Speed = 1;
        public long Tick;
        public bool Paused, Ended, Victory, SpecialReady;
        public double PlayerHealth, PlayerMaxHealth, EnemyHealth, EnemyMaxHealth;
        public double SpecialProgress, TrainingProgress;
        public int NextEvolutionExperience, NextTurretSlotCost, SpecialExperienceCost, PendingTurretId;
        public double WorldLeft, WorldRight, GroundY;
        public double BuildTurretFrame;
        public AgeOfWarVisual[] Visuals = new AgeOfWarVisual[0];
        public AgeOfWarTurretSlot[] TurretSlots = new AgeOfWarTurretSlot[0];
        public int[] TrainingQueue = new int[0];
        public AgeOfWarOffer[] Units = new AgeOfWarOffer[0], Turrets = new AgeOfWarOffer[0], Upgrades = new AgeOfWarOffer[0];
    }
    public interface IAgeOfWarBattle
    {
        int Edition { get; }
        int TickRate { get; }
        bool Execute(AgeOfWarCommand command);
        void Tick();
        AgeOfWarFrame Capture();
        byte[] SaveState();
        AgeOfWarEvent[] DrainEvents();
    }
}
