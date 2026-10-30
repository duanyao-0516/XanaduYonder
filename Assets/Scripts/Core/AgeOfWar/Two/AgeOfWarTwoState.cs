using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace XanaduYonder.Core.AgeOfWar.Two
{
    // This format stores values and explicit object references, never delegates or runtime type names.
    // Changing simulation fields or callback semantics requires a new format version.
    internal sealed class TwoStateReader : IDisposable
    {
        internal const int MaximumBytes = 32 * 1024 * 1024;
        internal const int MaximumObjects = 32768;
        internal readonly BinaryReader Reader;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal TwoStateReader(byte[] bytes)
        {
            Need(bytes != null && bytes.Length >= 12 && bytes.Length <= MaximumBytes, "payload length");
            Reader = new BinaryReader(new MemoryStream(bytes, false), Utf8);
        }
        internal static void Need(bool valid, string field)
        { if (!valid) throw new InvalidDataException("Invalid Age of War 2 state: " + field); }
        internal int Int(int minimum, int maximum)
        { int value = Reader.ReadInt32(); Need(value >= minimum && value <= maximum, "integer range"); return value; }
        internal long Long(long minimum, long maximum)
        { long value = Reader.ReadInt64(); Need(value >= minimum && value <= maximum, "identifier or clock"); return value; }
        internal bool Flag()
        { byte value = Reader.ReadByte(); Need(value <= 1, "boolean"); return value == 1; }
        internal double Number(double minimum = -1e12, double maximum = 1e12, bool allowNaN = false)
        {
            double value = Reader.ReadDouble();
            Need(allowNaN && double.IsNaN(value) || !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum, "number range");
            return value;
        }
        internal string Text(bool nullable = false)
        {
            int count = Int(nullable ? -1 : 0, 1024);
            if (count == -1) return null;
            Need(count <= Reader.BaseStream.Length - Reader.BaseStream.Position, "string length");
            return Utf8.GetString(Reader.ReadBytes(count));
        }
        internal static void Text(BinaryWriter writer, string value)
        {
            if (value == null) { writer.Write(-1); return; }
            byte[] bytes = Utf8.GetBytes(value); Need(bytes.Length <= 1024, "string length");
            writer.Write(bytes.Length); writer.Write(bytes);
        }
        internal void End() { Need(Reader.BaseStream.Position == Reader.BaseStream.Length, "trailing data"); }
        public void Dispose() { Reader.Dispose(); }
    }

    internal sealed partial class Animation
    {
        internal void WriteState(BinaryWriter writer)
        {
            TwoStateReader.Text(writer, Key); TwoStateReader.Text(writer, Label);
            writer.Write(Frame); writer.Write(Current); writer.Write(loop); writer.Write(stopped); writer.Write(callback != null);
        }
        internal static Animation ReadState(TwoStateReader reader, out bool hasCallback)
        {
            string key = reader.Text();
            TwoStateReader.Need(SourceTimelines.All.ContainsKey(key), "animation timeline");
            var value = new Animation(key);
            value.Label = reader.Text(); value.Frame = reader.Int(0, 1000000); value.Current = reader.Number(0);
            value.loop = reader.Flag(); value.stopped = reader.Flag(); hasCallback = reader.Flag();
            if (value.Label.Length == 0)
                TwoStateReader.Need(value.Frame == 0 && value.Current == 0 && value.stopped && !hasCallback, "empty animation");
            else
            {
                TwoStateReader.Need(value.Timeline.Labels.TryGetValue(value.Label, out value.section), "animation label");
                TwoStateReader.Need(value.Frame < value.section.Length && value.Current < value.section.Length, "animation frame");
            }
            TwoStateReader.Need(!hasCallback || !value.loop && !value.stopped, "animation callback phase");
            return value;
        }
        internal void RestoreOwner(IAnimationOwner owner, bool hasCallback)
        {
            Owner = owner;
            var semanticCallback = owner.CallbackForLabel(Label);
            TwoStateReader.Need(hasCallback == (!loop && !stopped && semanticCallback != null), "animation callback identity");
            TwoStateReader.Need(loop ? semanticCallback == null : stopped || semanticCallback != null, "animation playback phase");
            callback = hasCallback ? semanticCallback : null;
        }
    }

    public sealed partial class AgeOfWarTwoBattle
    {
        private const int StateVersion = 1;
        private static readonly byte[] StateMagic = Encoding.ASCII.GetBytes("AOW2STAT");

        // Restore deliberately skips LoadAiAge, object spawning and all random/audio/damage paths.
        private AgeOfWarTwoBattle(double difficulty) { Difficulty = difficulty; }

        public byte[] SaveState()
        {
            var savedUnits = new List<Unit>(Units);
            var savedTurrets = new List<Turret>(Turrets);
            var savedEffects = new List<Effect>(Effects);
            var animationTable = new List<Animation>(Animations);
            foreach (var animation in Animations)
            {
                var unit = animation.Owner as Unit;
                var turret = animation.Owner as Turret;
                var effect = animation.Owner as Effect;
                if (unit != null && !savedUnits.Contains(unit)) savedUnits.Add(unit);
                else if (turret != null && !savedTurrets.Contains(turret)) savedTurrets.Add(turret);
                else if (effect != null && !savedEffects.Contains(effect)) savedEffects.Add(effect);
                TwoStateReader.Need(unit != null || turret != null || effect != null, "unowned registered animation");
            }
            foreach (var unit in savedUnits) AddAnimation(animationTable, unit.Animation);
            foreach (var turret in savedTurrets) AddAnimation(animationTable, turret.Animation);
            foreach (var effect in savedEffects) AddAnimation(animationTable, effect.Animation);
            var animationIds = new Dictionary<Animation, int>();
            for (int index = 0; index < animationTable.Count; index++)
            { TwoStateReader.Need(!animationIds.ContainsKey(animationTable[index]), "duplicate animation registration"); animationIds.Add(animationTable[index], index); }
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(StateMagic); writer.Write(StateVersion);
                writer.Write(Difficulty); writer.Write(randomState); writer.Write(tick); writer.Write(NativeTick); writer.Write(nextId);
                writer.Write(paused); writer.Write(ended); writer.Write(victory); writer.Write(speed); writer.Write(frameRate); writer.Write(ShowDamage);
                writer.Write(Age); writer.Write(EnemyAge); writer.Write(Gold); writer.Write(Experience); writer.Write(Population); writer.Write(EnemyPopulation);
                writer.Write(Spots); writer.Write(EnemySpots); writer.Write(Building);
                writer.Write(PlayerHealth); writer.Write(EnemyHealth); writer.Write(PlayerMaximumHealth); writer.Write(EnemyMaximumHealth);
                writer.Write(BuildTimer); writer.Write(BuildMaximum); writer.Write(SpecialTimer); writer.Write(buildCrossFrame);
                writer.Write(pendingTurret); writer.Write(pendingRefund);
                for (int side = 0; side < 2; side++) for (int upgrade = 0; upgrade < 18; upgrade++) writer.Write(Upgrades[side, upgrade]);
                WriteCount(writer, Queue.Count); foreach (int id in Queue) writer.Write(id);
                var sourceEvents = AiData.Element("ai").Elements().ElementAt(EnemyAge - 1).Elements("event").ToArray();
                writer.Write(Array.IndexOf(sourceEvents, aiUnits.Parent));
                WriteCount(writer, aiEvents.Count); foreach (var item in aiEvents) writer.Write(Array.IndexOf(sourceEvents, item));
                writer.Write(aiTimer); writer.Write(aiUnitTimer); writer.Write(aiMin); writer.Write(aiMax);

                WriteCount(writer, animationTable.Count);
                foreach (var animation in animationTable)
                {
                    animation.WriteState(writer);
                    if (animation.Owner is Unit) { writer.Write(1); writer.Write(((Unit)animation.Owner).InstanceId); }
                    else if (animation.Owner is Turret) { writer.Write(2); writer.Write(((Turret)animation.Owner).InstanceId); }
                    else if (animation.Owner is Effect) { writer.Write(3); writer.Write(((Effect)animation.Owner).InstanceId); }
                    else throw new InvalidDataException("Cannot save an animation without its battle owner.");
                }
                WriteCount(writer, savedUnits.Count);
                foreach (var unit in savedUnits) { writer.Write(Units.Contains(unit)); unit.WriteState(writer, animationIds[unit.Animation]); }
                WriteCount(writer, savedTurrets.Count);
                foreach (var turret in savedTurrets) { writer.Write(Turrets.Contains(turret)); turret.WriteState(writer, animationIds[turret.Animation]); }
                WriteCount(writer, Projectiles.Count); foreach (var projectile in Projectiles) projectile.WriteState(writer);
                WriteCount(writer, savedEffects.Count);
                foreach (var effect in savedEffects) { writer.Write(Effects.Contains(effect)); effect.WriteState(writer, effect.Animation == null ? -1 : animationIds[effect.Animation]); }
                WriteCount(writer, Animations.Count); foreach (var animation in Animations) writer.Write(animationIds[animation]);
                writer.Flush(); TwoStateReader.Need(stream.Length <= TwoStateReader.MaximumBytes, "payload length");
                return stream.ToArray();
            }
        }
        private static void AddAnimation(List<Animation> values, Animation value) { if (value != null && !values.Contains(value)) values.Add(value); }
        private static void WriteCount(BinaryWriter writer, int value)
        { TwoStateReader.Need(value >= 0 && value <= TwoStateReader.MaximumObjects, "object count"); writer.Write(value); }

        public static AgeOfWarTwoBattle RestoreState(byte[] bytes)
        {
            try
            {
                using (var reader = new TwoStateReader(bytes))
                {
                    TwoStateReader.Need(reader.Reader.ReadBytes(StateMagic.Length).SequenceEqual(StateMagic), "magic");
                    int version = reader.Reader.ReadInt32();
                    if (version != StateVersion) throw new NotSupportedException("Unsupported Age of War 2 state version: " + version);
                    double difficulty = reader.Number();
                    TwoStateReader.Need(difficulty == .6 || difficulty == 1 || difficulty == 1.5 || difficulty == 2, "difficulty");
                    var battle = new AgeOfWarTwoBattle(difficulty);
                    battle.randomState = reader.Reader.ReadUInt32(); TwoStateReader.Need(battle.randomState != 0, "random state");
                    battle.tick = reader.Long(0, long.MaxValue - 1); battle.NativeTick = reader.Long(battle.tick, long.MaxValue - 1); battle.nextId = reader.Long(0, long.MaxValue - 1);
                    battle.paused = reader.Flag(); battle.ended = reader.Flag(); battle.victory = reader.Flag();
                    battle.speed = reader.Int(1, 2); battle.frameRate = reader.Int(25, 300); battle.ShowDamage = reader.Flag();
                    battle.Age = reader.Int(1, 7); battle.EnemyAge = reader.Int(1, 7); battle.Gold = reader.Int(0, int.MaxValue); battle.Experience = reader.Int(0, int.MaxValue);
                    battle.Population = reader.Int(0, 10); battle.EnemyPopulation = reader.Int(0, 10); battle.Spots = reader.Int(0, 4); battle.EnemySpots = reader.Int(0, 4); battle.Building = reader.Int(0, 29);
                    battle.PlayerHealth = reader.Int(0, 1000000000); battle.EnemyHealth = reader.Int(0, 1000000000);
                    battle.PlayerMaximumHealth = reader.Int(1, 1000000000); battle.EnemyMaximumHealth = reader.Int(1, 1000000000);
                    battle.BuildTimer = reader.Number(); battle.BuildMaximum = reader.Number(0); battle.SpecialTimer = reader.Number(-1e12, 60); battle.buildCrossFrame = reader.Number(0, 30);
                    battle.pendingTurret = reader.Int(0, 17); battle.pendingRefund = reader.Int(0, int.MaxValue);
                    for (int side = 0; side < 2; side++) for (int upgrade = 0; upgrade < 18; upgrade++)
                        battle.Upgrades[side, upgrade] = reader.Int(0, battle.Definition("upgrades", upgrade + 1).Elements().Count() - 1);
                    int count = reader.Int(0, 64); for (int index = 0; index < count; index++) battle.Queue.Add(reader.Int(1, 29));
                    var sourceEvents = battle.AiData.Element("ai").Elements().ElementAt(battle.EnemyAge - 1).Elements("event").ToArray();
                    int currentEvent = reader.Int(0, sourceEvents.Length - 1); battle.aiUnits = sourceEvents[currentEvent].Element("units");
                    count = reader.Int(0, sourceEvents.Length);
                    TwoStateReader.Need(count == sourceEvents.Length - currentEvent - 1, "AI remaining event count");
                    for (int index = 0; index < count; index++)
                    { int sourceIndex = reader.Int(0, sourceEvents.Length - 1); TwoStateReader.Need(sourceIndex == currentEvent + index + 1, "AI event order"); battle.aiEvents.Add(sourceEvents[sourceIndex]); }
                    battle.aiTimer = reader.Number(); battle.aiUnitTimer = reader.Number(); battle.aiMin = reader.Number(0); battle.aiMax = reader.Number(battle.aiMin);
                    TwoStateReader.Need(battle.aiMin == N(battle.aiUnits, "min") && battle.aiMax == N(battle.aiUnits, "max"), "AI unit interval");

                    count = reader.Int(0, TwoStateReader.MaximumObjects);
                    var animations = new Animation[count]; var callbacks = new bool[count]; var ownerKinds = new int[count]; var ownerIds = new long[count];
                    for (int index = 0; index < count; index++)
                    { animations[index] = Animation.ReadState(reader, out callbacks[index]); ownerKinds[index] = reader.Int(1, 3); ownerIds[index] = reader.Long(1, battle.nextId); }
                    var identifiers = new HashSet<long>(); var owners = new Dictionary<long, IAnimationOwner>();
                    var ownerAnimations = new Dictionary<long, Animation>(); var kinds = new Dictionary<long, int>();
                    count = reader.Int(0, TwoStateReader.MaximumObjects);
                    for (int index = 0; index < count; index++)
                    {
                        bool active = reader.Flag(); var unit = new Unit(battle, reader, animations);
                        AddOwner(identifiers, owners, ownerAnimations, kinds, unit.InstanceId, unit, unit.Animation, 1);
                        TwoStateReader.Need(active, "unregistered unit owner"); battle.Units.Add(unit);
                    }
                    count = reader.Int(0, TwoStateReader.MaximumObjects);
                    for (int index = 0; index < count; index++)
                    {
                        bool active = reader.Flag(); var turret = new Turret(battle, reader, animations);
                        AddOwner(identifiers, owners, ownerAnimations, kinds, turret.InstanceId, turret, turret.Animation, 2);
                        TwoStateReader.Need(active || !turret.Alive, "live detached turret"); if (active) battle.Turrets.Add(turret);
                    }
                    count = reader.Int(0, TwoStateReader.MaximumObjects);
                    for (int index = 0; index < count; index++)
                    { var projectile = new Projectile(battle, reader); TwoStateReader.Need(identifiers.Add(projectile.InstanceId), "duplicate object ID"); battle.Projectiles.Add(projectile); }
                    count = reader.Int(0, TwoStateReader.MaximumObjects);
                    for (int index = 0; index < count; index++)
                    {
                        bool active = reader.Flag(); var effect = new Effect(battle, reader, animations);
                        AddOwner(identifiers, owners, ownerAnimations, kinds, effect.InstanceId, effect, effect.Animation, 3);
                        TwoStateReader.Need(active, "unregistered effect owner"); battle.Effects.Add(effect);
                    }
                    for (int index = 0; index < animations.Length; index++)
                    {
                        IAnimationOwner owner; Animation owned; int kind;
                        TwoStateReader.Need(owners.TryGetValue(ownerIds[index], out owner) && kinds.TryGetValue(ownerIds[index], out kind) && kind == ownerKinds[index], "animation owner");
                        TwoStateReader.Need(ownerAnimations.TryGetValue(ownerIds[index], out owned) && ReferenceEquals(owned, animations[index]), "animation reference");
                        animations[index].RestoreOwner(owner, callbacks[index]);
                    }
                    count = reader.Int(0, animations.Length); var registered = new HashSet<int>();
                    for (int index = 0; index < count; index++)
                    { int id = reader.Int(0, animations.Length - 1); TwoStateReader.Need(registered.Add(id), "duplicate animation registration"); battle.Animations.Add(animations[id]); }
                    TwoStateReader.Need(count == animations.Length, "missing registered animation");
                    battle.ValidateRestoredState(); reader.End();
                    return battle;
                }
            }
            catch (EndOfStreamException error) { throw new InvalidDataException("Truncated Age of War 2 state.", error); }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid Age of War 2 state text.", error); }
        }
        private static void AddOwner(HashSet<long> ids, Dictionary<long, IAnimationOwner> owners, Dictionary<long, Animation> animations, Dictionary<long, int> kinds, long id, IAnimationOwner owner, Animation animation, int kind)
        {
            TwoStateReader.Need(ids.Add(id), "duplicate object ID");
            if (animation != null)
            {
                TwoStateReader.Need(animation.Owner == null, "animation shared by multiple owners");
                animation.Owner = owner; // Bind identity now; bind the callback only after the complete graph exists.
            }
            owners.Add(id, owner); animations.Add(id, animation); kinds.Add(id, kind);
        }
        private void ValidateRestoredState()
        {
            TwoStateReader.Need(PlayerMaximumHealth == I(Definition("bases", Age), "health") && EnemyMaximumHealth == I(Definition("bases", EnemyAge), "health"), "base maximum health");
            TwoStateReader.Need(PlayerHealth <= PlayerMaximumHealth && EnemyHealth <= EnemyMaximumHealth, "base health");
            TwoStateReader.Need(!victory || ended, "victory without terminal state");
            TwoStateReader.Need(ended ? PlayerHealth == 0 || EnemyHealth == 0 : PlayerHealth > 0 && EnemyHealth > 0, "terminal bases");
            TwoStateReader.Need(pendingTurret == 0 ? pendingRefund == 0 : pendingRefund == I(Definition("turrets", pendingTurret), "cost"), "turret escrow");
            TwoStateReader.Need(Building == 0 || BuildMaximum == N(Definition("units", Building), "creationtime"), "training duration");
            var spots = new HashSet<int>();
            foreach (var turret in Turrets) if (turret.Alive)
                TwoStateReader.Need(turret.Spot <= (turret.Side == 1 ? Spots : EnemySpots) && spots.Add(turret.Side * 10 + turret.Spot), "occupied turret slot");
            int playerPop = Queue.Sum(id => I(Definition("units", id), "pop")) + (Building == 0 ? 0 : I(Definition("units", Building), "pop"));
            playerPop += Units.Where(unit => unit.Side == 1 && !unit.Dead).Sum(unit => unit.Pop);
            int enemyPop = Units.Where(unit => unit.Side == 2 && !unit.Dead).Sum(unit => unit.Pop);
            TwoStateReader.Need(playerPop == Population && enemyPop == EnemyPopulation, "population accounting");
        }
    }
}
