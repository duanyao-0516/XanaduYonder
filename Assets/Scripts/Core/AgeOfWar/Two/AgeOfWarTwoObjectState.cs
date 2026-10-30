using System;
using System.IO;
using System.Linq;

namespace XanaduYonder.Core.AgeOfWar.Two
{
    public sealed partial class AgeOfWarTwoBattle
    {
        private static Animation ReadAnimation(TwoStateReader reader, Animation[] animations, bool optional = false)
        {
            int index = reader.Int(optional ? -1 : 0, animations.Length - 1);
            return index == -1 ? null : animations[index];
        }
        private static string StateChoice(TwoStateReader reader, bool nullable, params string[] choices)
        {
            string value = reader.Text(nullable);
            TwoStateReader.Need(nullable && value == null || choices.Contains(value), "state name");
            return value;
        }

        internal sealed partial class Unit
        {
            internal void WriteState(BinaryWriter writer, int animation)
            {
                writer.Write(InstanceId); writer.Write(TypeId); writer.Write(Side); writer.Write(Multi); writer.Write(Pop); writer.Write(Reward); writer.Write(DamageMin); writer.Write(DamageMax);
                writer.Write(Width); writer.Write(Height); writer.Write(MaxHealth); writer.Write(OriginalSpeed); writer.Write(Range); writer.Write(Scale);
                TwoStateReader.Text(writer, Type); TwoStateReader.Text(writer, ProjectileName); TwoStateReader.Text(writer, meleeSound); TwoStateReader.Text(writer, rangedSound); TwoStateReader.Text(writer, deathSound);
                writer.Write(shootProjectile); writer.Write(animation);
                // Preserve stored DisplayObject values directly: assigning the public setters would quantize them a second time.
                writer.Write(x); writer.Write(healthAlpha); writer.Write(Health); writer.Write(Speed); writer.Write(DisplayHealthFraction); writer.Write(Wait); writer.Write(ShowHealth);
                writer.Write(Alive); writer.Write(Dead); writer.Write(Stopped); writer.Write(OnBaseMelee); writer.Write(OnBaseRanged); writer.Write(OnUnitMelee); writer.Write(OnUnitRange); writer.Write(OnMelee); writer.Write(CanWalk);
                TwoStateReader.Text(writer, state);
            }
            internal Unit(AgeOfWarTwoBattle battle, TwoStateReader reader, Animation[] animations)
            {
                game = battle;
                InstanceId = reader.Long(1, battle.nextId); TypeId = reader.Int(1, 29); Side = reader.Int(1, 2); Multi = reader.Int(-1, 1);
                Pop = reader.Int(1, 10); Reward = reader.Int(0, int.MaxValue); DamageMin = reader.Int(0, int.MaxValue); DamageMax = reader.Int(DamageMin, int.MaxValue);
                Width = reader.Number(0, 10000); Height = reader.Number(0, 10000); MaxHealth = reader.Number(double.Epsilon, 1e9); OriginalSpeed = reader.Number(0, 10000); Range = reader.Number(0, 10000); Scale = reader.Number(double.Epsilon, 1000);
                Type = reader.Text(); ProjectileName = reader.Text(); meleeSound = reader.Text(); rangedSound = reader.Text(); deathSound = reader.Text();
                shootProjectile = reader.Flag(); Animation = ReadAnimation(reader, animations);
                x = reader.Number(); healthAlpha = reader.Number(); Health = reader.Number(-1e12, MaxHealth); Speed = reader.Number(0, 10000); DisplayHealthFraction = reader.Number(0, 1); Wait = reader.Number(); ShowHealth = reader.Flag();
                Alive = reader.Flag(); Dead = reader.Flag(); Stopped = reader.Flag(); OnBaseMelee = reader.Flag(); OnBaseRanged = reader.Flag(); OnUnitMelee = reader.Flag(); OnUnitRange = reader.Flag(); OnMelee = reader.Flag(); CanWalk = reader.Flag();
                state = StateChoice(reader, false, "idle", "walk", "attackMelee", "attackRanged", "walkAttack", "die");
                var definition = game.Definition("units", TypeId);
                TwoStateReader.Need(Multi == (Side == 1 ? 1 : -1) && Animation.Key == "u" + TypeId, "unit orientation or animation");
                TwoStateReader.Need(Pop == I(definition, "pop") && Type == S(definition, "unitType") && ProjectileName == S(definition, "projectileName"), "unit source identity");
                TwoStateReader.Need(DamageMin == I(definition, "damageMin") && DamageMax == I(definition, "damageMax") && Width == N(definition, "sizeW") && Height == N(definition, "sizeH") && Scale == N(definition, "scale") && OriginalSpeed == N(definition, "speed") / 3.2, "unit source properties");
                TwoStateReader.Need(meleeSound == S(definition, "hitMeleeSound") && rangedSound == S(definition, "hitRangedSound") && deathSound == S(definition, "deathSound") && shootProjectile == (I(definition, "shootProjectile") == 1), "unit audio or projectile mode");
                TwoStateReader.Need(!Dead || Health <= 0, "unit death phase");
                // Reward, maximum health and range belong to the upgrades at this unit's birth, not today's upgrades.
            }
        }

        internal sealed partial class Turret
        {
            internal void WriteState(BinaryWriter writer, int animation)
            {
                writer.Write(InstanceId); writer.Write(TypeId); writer.Write(Side); writer.Write(Spot); writer.Write(Multi); writer.Write(animation);
                writer.Write(X); writer.Write(Y); writer.Write(Range); writer.Write(Scale); writer.Write(min); writer.Write(max);
                TwoStateReader.Text(writer, projectile); TwoStateReader.Text(writer, sounds);
                writer.Write(Alive); writer.Write(rotation); writer.Write(SellFrame); TwoStateReader.Text(writer, state);
            }
            internal Turret(AgeOfWarTwoBattle battle, TwoStateReader reader, Animation[] animations)
            {
                game = battle;
                InstanceId = reader.Long(1, battle.nextId); TypeId = reader.Int(1, 17); Side = reader.Int(1, 2); Spot = reader.Int(1, 4); Multi = reader.Int(-1, 1); Animation = ReadAnimation(reader, animations);
                X = reader.Number(); Y = reader.Number(); Range = reader.Number(0, 10000); Scale = reader.Number(double.Epsilon, 1000); min = reader.Int(0, int.MaxValue); max = reader.Int(min, int.MaxValue);
                projectile = reader.Text(); sounds = reader.Text(); Alive = reader.Flag(); rotation = reader.Number(-180, 180); SellFrame = reader.Number(0, 30); state = StateChoice(reader, true, "idle", "shoot");
                var definition = game.Definition("turrets", TypeId);
                TwoStateReader.Need(Multi == (Side == 1 ? 1 : -1) && X == (Side == 1 ? -624 : 620) && Y == -146 - (Spot - 1) * 47 && Animation.Key == "t" + TypeId, "turret position or animation");
                TwoStateReader.Need(min == I(definition, "damageMin") && max == I(definition, "damageMax") && Scale == N(definition, "scale") && projectile == S(definition, "projectileName") && sounds == S(definition, "shootSound"), "turret source properties");
            }
        }

        internal sealed partial class Projectile
        {
            internal void WriteState(BinaryWriter writer)
            {
                writer.Write(InstanceId); writer.Write(nativeStart); TwoStateReader.Text(writer, Name); TwoStateReader.Text(writer, Type); TwoStateReader.Text(writer, sounds);
                writer.Write(Side); writer.Write(Multi); writer.Write(Min); writer.Write(Max);
                writer.Write(x); writer.Write(y); writer.Write(rotation); writer.Write(XSpeed); writer.Write(YSpeed); writer.Write(RotationSpeed); writer.Write(Mass); writer.Write(Scale); writer.Write(ExplosionScale);
                writer.Write(Alive); writer.Write(Remove); writer.Write(Follow);
            }
            internal Projectile(AgeOfWarTwoBattle battle, TwoStateReader reader)
            {
                game = battle;
                InstanceId = reader.Long(1, battle.nextId); nativeStart = reader.Long(0, battle.NativeTick);
                Name = reader.Text(); Type = StateChoice(reader, false, "", "Support", "Infantry", "Anti-Armor", "Armored", "Special", "Turret"); sounds = reader.Text();
                Side = reader.Int(1, 2); Multi = reader.Int(-1, 1); Min = reader.Int(0, int.MaxValue); Max = reader.Int(Min, int.MaxValue);
                x = reader.Number(); y = reader.Number(); rotation = reader.Number(-180, 180); XSpeed = reader.Number(); YSpeed = reader.Number(); RotationSpeed = reader.Number(); Mass = reader.Number(0); Scale = reader.Number(double.Epsilon, 1000); ExplosionScale = reader.Number(0, 1000);
                Alive = reader.Flag(); Remove = reader.Flag(); Follow = reader.Flag();
                var definition = game.Data.Element("projectiles").Elements().FirstOrDefault(item => S(item, "name") == Name);
                TwoStateReader.Need(definition != null && Multi == (Side == 1 ? -1 : 1), "projectile identity");
                TwoStateReader.Need(Mass == N(definition, "mass") && Scale == N(definition, "size") && ExplosionScale == N(definition, "explosion") && sounds == S(definition, "hitSound") && Follow == (S(definition, "rotSpeed") == "follow"), "projectile source properties");
                TwoStateReader.Need(!Remove, "removed projectile in active list");
            }
        }

        internal sealed partial class Effect
        {
            internal void WriteState(BinaryWriter writer, int animation)
            {
                writer.Write(InstanceId); writer.Write(Age); writer.Write(Side); writer.Write(Multi);
                writer.Write(Alive); writer.Write(Removed); writer.Write(Emitter); writer.Write(Ground); writer.Write(DamageText); writer.Write(Critical); writer.Write(Amount);
                TwoStateReader.Text(writer, Clip); writer.Write(animation);
                writer.Write(x); writer.Write(y); writer.Write(rotation); writer.Write(alpha); writer.Write(Scale); writer.Write(Timer); writer.Write(Interval); writer.Write(IntervalTimer); writer.Write(TrailTimer); writer.Write(XSpeed); writer.Write(YSpeed); writer.Write(Frame);
            }
            internal Effect(AgeOfWarTwoBattle battle, TwoStateReader reader, Animation[] animations)
            {
                game = battle;
                InstanceId = reader.Long(1, battle.nextId); Age = reader.Int(0, 7); Side = reader.Int(0, 2); Multi = reader.Int(-1, 1);
                Alive = reader.Flag(); Removed = reader.Flag(); Emitter = reader.Flag(); Ground = reader.Flag(); DamageText = reader.Flag(); Critical = reader.Flag(); Amount = reader.Int(0, int.MaxValue);
                Clip = reader.Text(true); Animation = ReadAnimation(reader, animations, true);
                x = reader.Number(); y = reader.Number(); rotation = reader.Number(-180, 180); alpha = reader.Number(); Scale = reader.Number(double.Epsilon, 1000);
                Timer = reader.Number(-1e12, 1e12, Age == 6 && Clip == "special/6_plane" || Age == 7 && Emitter);
                Interval = reader.Number(0); IntervalTimer = reader.Number(0); TrailTimer = reader.Number(0); XSpeed = reader.Number(); YSpeed = reader.Number(); Frame = reader.Int(0, 6);
                TwoStateReader.Need(!Removed, "removed effect in active list");
                if (Age == 0)
                {
                    TwoStateReader.Need(Side == 0 && Multi == 0 && !Emitter && !Ground, "cosmetic ownership");
                    if (DamageText) TwoStateReader.Need(Clip == null && Animation == null, "damage text kind");
                    else TwoStateReader.Need((Clip == "explosion" || Clip == "firetrail") && Animation != null && Animation.Key == Clip, "cosmetic animation");
                }
                else
                {
                    TwoStateReader.Need(Side >= 1 && Multi == (Side == 1 ? 1 : -1) && !DamageText, "special ownership");
                    TwoStateReader.Need(!Ground || Age == 4, "ground special");
                    if (Emitter) TwoStateReader.Need(Age != 6 && Clip == null && Animation == null, "special emitter");
                    else if (Age == 6) TwoStateReader.Need((Clip == "special/6_plane" || Clip == "special/6_bomb") && Animation == null, "plane effect");
                    else
                    {
                        TwoStateReader.Need(Clip == "special/" + Age, "special display");
                        TwoStateReader.Need(Age == 3 ? Animation != null && Animation.Key == "special3" : Animation == null, "special animation");
                    }
                }
            }
        }
    }
}
