using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace XanaduYonder.Core.AgeOfWar.One
{
    public sealed partial class AgeOfWarOneBattle
    {
        // Little-endian, explicit schema. Events are delivery queues, not resumable state.
        private const uint StateMagic = 0x31574F41; // AOW1
        private const int StateVersion = 1;
        private const uint StateEnd = 0x31444E45; // END1
        private const int StateMaximumBytes = 64 * 1024 * 1024;
        private const int StateMaximumActors = 65536;
        private const int StateMaximumEffects = 65536;
        private const long StateMaximumIdentity = long.MaxValue / 16;

        /// <summary>Capture between commands/ticks on the simulation's owning thread. Does not drain events or pause.</summary>
        public byte[] SaveState()
        {
            var actors = CollectStateActors();
            if (_units.Count > StateMaximumActors - 2 || _turrets.Count > 8 || _queue.Count > 5 ||
                _projectiles.Count > StateMaximumEffects || _particles.Count > StateMaximumEffects)
                throw new InvalidDataException("Age of War One state exceeds the supported object limit.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(StateMagic); writer.Write(StateVersion);
                writer.Write(_difficulty); writer.Write(_random.State);
                WriteStateGlobals(writer);
                writer.Write(actors.Count);
                foreach (var actor in actors) WriteStateActor(writer, actor);
                writer.Write(_units.Count);
                foreach (var actor in _units) writer.Write(actor.Uid);
                writer.Write(_turrets.Count);
                foreach (var turret in _turrets) WriteStateTurret(writer, turret);
                writer.Write(_projectiles.Count);
                foreach (var projectile in _projectiles) WriteStateProjectile(writer, projectile);
                writer.Write(_particles.Count);
                foreach (var particle in _particles) WriteStateParticle(writer, particle);
                writer.Write(_special != null);
                if (_special != null) WriteStateSpecial(writer, _special);
                writer.Write(StateEnd); writer.Flush();
                if (stream.Length > StateMaximumBytes - 32) throw InvalidState("size");
                var payload = stream.ToArray();
                byte[] digest;
                using (var sha = SHA256.Create()) digest = sha.ComputeHash(payload);
                var result = new byte[payload.Length + digest.Length];
                Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
                Buffer.BlockCopy(digest, 0, result, payload.Length, digest.Length);
                return result;
            }
        }

        /// <summary>Restore without simulation callbacks. Unknown versions are preserved by callers, not treated as new games.</summary>
        public static AgeOfWarOneBattle RestoreState(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (bytes.Length < 8 || bytes.Length > StateMaximumBytes) throw InvalidState("size");
            try
            {
                using (var prefix = new BinaryReader(new MemoryStream(bytes, false)))
                {
                    if (prefix.ReadUInt32() != StateMagic) throw InvalidState("magic");
                    if (prefix.ReadInt32() != StateVersion)
                        throw new NotSupportedException("Unsupported Age of War One state version.");
                }
                if (bytes.Length < 44) throw InvalidState("size");
                int payloadLength = bytes.Length - 32;
                byte[] digest;
                using (var sha = SHA256.Create()) digest = sha.ComputeHash(bytes, 0, payloadLength);
                int mismatch = 0;
                for (int i = 0; i < digest.Length; ++i) mismatch |= digest[i] ^ bytes[payloadLength + i];
                if (mismatch != 0) throw InvalidState("checksum");
                using (var stream = new MemoryStream(bytes, 0, payloadLength, false))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadUInt32(); reader.ReadInt32();
                    int difficulty = StateInt(reader, 1, 3, "difficulty");
                    uint random = reader.ReadUInt32();
                    if (random == 0) throw InvalidState("random stream");
                    // This constructor only allocates the two bases and RNG; it draws no random values.
                    var battle = new AgeOfWarOneBattle(difficulty, random);
                    battle.ReadStateGlobals(reader);
                    var actors = new Dictionary<long, Actor>();
                    int actorCount = StateInt(reader, 2, StateMaximumActors, "actor count");
                    var orderedActors = new List<Actor>(actorCount);
                    var targets = new long[actorCount];
                    for (int i = 0; i < actorCount; ++i)
                    {
                        long id = reader.ReadInt64();
                        if ((id != -1 && id != -2 && (id < 1 || id >= battle._nextUnit)) || actors.ContainsKey(id))
                            throw InvalidState("actor identity");
                        var actor = id == -1 ? battle._playerBase : id == -2 ? battle._enemyBase : new Actor();
                        actor.Uid = id;
                        targets[i] = ReadStateActor(reader, actor);
                        actors.Add(id, actor); orderedActors.Add(actor);
                    }
                    if (!actors.ContainsKey(-1) || !actors.ContainsKey(-2)) throw InvalidState("bases");
                    for (int i = 0; i < actorCount; ++i)
                    {
                        var actor = orderedActors[i];
                        actor.Target = StateTarget(actors, targets[i]);
                        if (actor.Target != null && (actor.Target.Side == actor.Side || actor.Id == 0))
                            throw InvalidState("actor target");
                    }
                    var activeIds = new HashSet<long>();
                    int unitCount = StateInt(reader, 0, actorCount - 2, "unit count");
                    for (int i = 0; i < unitCount; ++i)
                    {
                        long id = reader.ReadInt64();
                        var actor = StateTarget(actors, id);
                        if (actor == null || actor.Id == 0 || !activeIds.Add(id)) throw InvalidState("active unit");
                        battle._units.Add(actor);
                    }
                    var visualIds = new HashSet<long>();
                    var turretSlots = new HashSet<int>();
                    int turretCount = StateInt(reader, 0, 8, "turret count");
                    for (int i = 0; i < turretCount; ++i)
                    {
                        var turret = ReadStateTurret(reader, actors);
                        battle.CheckStateVisualId(turret.Uid, visualIds);
                        if (!turretSlots.Add(turret.Side * 4 + turret.Slot)) throw InvalidState("turret slot");
                        if (turret.Slot > (turret.Side == 1 ? battle._addons : battle._enemyAddons) + 1)
                            throw InvalidState("unbuilt turret slot");
                        battle._turrets.Add(turret);
                    }
                    int projectileCount = StateInt(reader, 0, StateMaximumEffects, "projectile count");
                    for (int i = 0; i < projectileCount; ++i)
                    {
                        var projectile = ReadStateProjectile(reader, battle._tick);
                        battle.CheckStateVisualId(projectile.Uid, visualIds);
                        battle._projectiles.Add(projectile);
                    }
                    int particleCount = StateInt(reader, 0, StateMaximumEffects, "particle count");
                    for (int i = 0; i < particleCount; ++i)
                    {
                        var particle = ReadStateParticle(reader, battle._tick);
                        battle.CheckStateVisualId(particle.Uid, visualIds);
                        battle._particles.Add(particle);
                    }
                    if (StateBool(reader))
                    {
                        battle._special = ReadStateSpecial(reader);
                        battle.CheckStateVisualId(battle._special.Uid, visualIds);
                    }
                    if (reader.ReadUInt32() != StateEnd || stream.Position != stream.Length) throw InvalidState("trailing data");
                    battle.ValidateStateGraph(actors, activeIds);
                    battle._events.Clear();
                    return battle;
                }
            }
            catch (EndOfStreamException e) { throw new InvalidDataException("Truncated Age of War One state.", e); }
            catch (OverflowException e) { throw new InvalidDataException("Invalid Age of War One numeric state.", e); }
        }

        private List<Actor> CollectStateActors()
        {
            var result = new List<Actor>();
            var seen = new HashSet<Actor>();
            AddStateActor(result, seen, _playerBase); AddStateActor(result, seen, _enemyBase);
            foreach (var unit in _units) AddStateActor(result, seen, unit);
            foreach (var turret in _turrets) AddStateActor(result, seen, turret.Target);
            // Iterative transitive closure handles dead targets, cycles and deep chains without recursion.
            for (int i = 0; i < result.Count; ++i) AddStateActor(result, seen, result[i].Target);
            return result;
        }

        private static void AddStateActor(List<Actor> result, HashSet<Actor> seen, Actor actor)
        {
            if (actor == null || !seen.Add(actor)) return;
            if (result.Count == StateMaximumActors) throw InvalidState("actor count");
            result.Add(actor);
        }

        private void WriteStateGlobals(BinaryWriter w)
        {
            w.Write(_gold); w.Write(_experience); w.Write(_age); w.Write(_enemyAge);
            w.Write(_addons); w.Write(_enemyAddons); w.Write(_specialTimer); w.Write(_pendingTurret); w.Write(_specialChargeFrame);
            w.Write(_training); w.Write(_trainingTotal); w.Write(_techTimer); w.Write(_unitLevel); w.Write(_aiTimer);
            w.Write(_enemyTraining); w.Write(_enemyTrainingId); w.Write(_enemyPopulation);
            w.Write(_aiPending); w.Write(_aiTrain); w.Write(_paused); w.Write(_ended); w.Write(_victory); w.Write(_healing);
            w.Write(_tick); w.Write(_nextUnit); w.Write(_nextVisual);
            w.Write(_queue.Count); foreach (int id in _queue) w.Write(id);
        }

        private void ReadStateGlobals(BinaryReader r)
        {
            // Selecting a turret does not reserve cash: placing it can legitimately make gold negative.
            _gold = r.ReadInt32(); _experience = r.ReadInt32();
            _age = StateInt(r, 1, 5, "age"); _enemyAge = StateInt(r, 1, 5, "enemy age");
            _addons = StateInt(r, 0, 3, "turret extensions"); _enemyAddons = StateInt(r, 0, 3, "enemy turret extensions");
            _specialTimer = StateInt(r, 0, 2000, "special recharge"); _pendingTurret = StateInt(r, 0, 15, "pending turret");
            _specialChargeFrame = StateInt(r, 0, 999, "charge frame");
            _training = StateInt(r, -1, 300, "training"); _trainingTotal = StateInt(r, 0, 300, "training total");
            _techTimer = StateInt(r, 0, int.MaxValue - 1, "technology timer"); _unitLevel = StateInt(r, 1, 3, "enemy unit level");
            _aiTimer = StateInt(r, 0, 39, "AI timer");
            _enemyTraining = r.ReadInt32(); _enemyTrainingId = StateInt(r, 0, 15, "enemy training unit");
            _enemyPopulation = StateInt(r, 0, 6, "enemy population");
            _aiPending = StateBool(r); _aiTrain = StateBool(r); _paused = StateBool(r);
            _ended = StateBool(r); _victory = StateBool(r); _healing = StateBool(r);
            _tick = StateLong(r, 0, long.MaxValue - 1, "tick");
            _nextUnit = StateLong(r, 1, StateMaximumIdentity, "next unit identity");
            _nextVisual = StateLong(r, 100000, StateMaximumIdentity, "next visual identity");
            int queueCount = StateInt(r, 0, 5, "training queue");
            for (int i = 0; i < queueCount; ++i) _queue.Add(StateInt(r, 1, _age == 5 ? 16 : _age * 3, "queued unit"));
            if (queueCount == 0 ? _training != -1 || _trainingTotal != 0 :
                _training < 1 || _training > _trainingTotal || _trainingTotal != OneData.Units[_queue[0]].Training)
                throw InvalidState("training queue timing");
            if (_victory && !_ended) throw InvalidState("victory state");
            if (_enemyTraining > 0 && (_enemyTrainingId == 0 || _enemyTraining > OneData.Units[_enemyTrainingId].Training))
                throw InvalidState("enemy training timing");
            if (_enemyTraining == 0 && _enemyTrainingId == 0) throw InvalidState("enemy training unit");
        }

        private static void WriteStateActor(BinaryWriter w, Actor a)
        {
            w.Write(a.Uid); w.Write(a.Id); w.Write(a.Side); w.Write(a.ScanCounter); w.Write(a.DeathCounter);
            w.Write(a.AnimationFrame); w.Write(a.Timeline); w.Write(a.Flame1); w.Write(a.Flame2);
            w.Write(a.X); w.Write(a.Y); w.Write(a.Health); w.Write(a.MaximumHealth); w.Write(a.Damage); w.Write(a.RangedDamage);
            w.Write(a.Left); w.Write(a.Top); w.Write(a.Right); w.Write(a.Bottom);
            w.Write(a.ClosestDistance); w.Write(a.EnemyDistance); w.Write(a.Limit);
            w.Write(a.Target == null ? 0L : a.Target.Uid);
            w.Write(a.Dead); w.Write(a.Removed); w.Write(a.AnimationStopped); w.Write(a.AnimationEntered); w.Write(a.Initialized);
            w.Write(StateAnimationCode(a.Animation));
        }

        private static long ReadStateActor(BinaryReader r, Actor a)
        {
            a.Id = StateInt(r, 0, 16, "unit type"); a.Side = StateInt(r, 1, 2, "unit side");
            a.ScanCounter = StateInt(r, 1, 5, "unit scan"); a.DeathCounter = StateInt(r, 0, 81, "death timer");
            a.AnimationFrame = StateInt(r, 1, 1000, "unit frame"); a.Timeline = StateInt(r, 0, 1000, "unit timeline");
            a.Flame1 = StateInt(r, 0, 30, "unit flame"); a.Flame2 = StateInt(r, 0, 30, "unit flame");
            a.X = StateDouble(r); a.Y = StateDouble(r); a.Health = StateDouble(r); a.MaximumHealth = StateDouble(r);
            a.Damage = StateDouble(r); a.RangedDamage = StateDouble(r);
            a.Left = StateDouble(r); a.Top = StateDouble(r); a.Right = StateDouble(r); a.Bottom = StateDouble(r);
            a.ClosestDistance = StateDouble(r); a.EnemyDistance = StateDouble(r); a.Limit = StateDouble(r);
            long target = r.ReadInt64();
            a.Dead = StateBool(r); a.Removed = StateBool(r); a.AnimationStopped = StateBool(r);
            a.AnimationEntered = StateBool(r); a.Initialized = StateBool(r); a.Animation = StateAnimation(r.ReadByte());
            if (a.Uid < 0)
            {
                if (a.Id != 0 || a.Side != (a.Uid == -1 ? 1 : 2) || a.Timeline != 0 || a.Animation != "idle" || a.AnimationFrame != 1 || a.MaximumHealth <= 0)
                    throw InvalidState("base actor");
            }
            else
            {
                if (a.Id == 0) throw InvalidState("unit type");
                int timeline;
                if (!OneData.UnitAnimations[a.Id].TryGetValue(a.Animation, out timeline)) throw InvalidState("unit animation");
                if (a.Initialized)
                {
                    if (a.Timeline != timeline || a.AnimationFrame > OneData.Timelines[timeline].Frames || a.MaximumHealth <= 0)
                        throw InvalidState("unit timeline");
                }
                else if (a.Timeline != 0 || a.Animation != "idle" || a.AnimationFrame != 1 || a.MaximumHealth != 0)
                    throw InvalidState("uninitialized unit");
            }
            if (a.Right < a.Left || a.Bottom < a.Top || a.Limit < 0 || (a.Removed && !a.Dead)) throw InvalidState("unit bounds");
            if ((a.Id != 12 && a.Id != 15 && a.Flame1 != 0) || (a.Id != 12 && a.Flame2 != 0) ||
                (a.Id == 15 && a.Flame1 != 0 && (a.Animation != "die" || a.AnimationFrame < 4)))
                throw InvalidState("unit nested animation");
            return target;
        }

        private static void WriteStateTurret(BinaryWriter w, Turret t)
        {
            w.Write(t.Uid); w.Write(t.Id); w.Write(t.Side); w.Write(t.Slot); w.Write(t.ScanCounter); w.Write(t.ShootTimer); w.Write(t.AnimationFrame);
            w.Write(t.X); w.Write(t.Y); w.Write(t.Rotation); w.Write(t.EnemyDistance); w.Write(t.EnemyHeight);
            w.Write(t.Target == null ? 0L : t.Target.Uid); w.Write(t.Playing);
        }

        private static Turret ReadStateTurret(BinaryReader r, Dictionary<long, Actor> actors)
        {
            var t = new Turret(); t.Uid = r.ReadInt64(); t.Id = StateInt(r, 1, 15, "turret type");
            t.Side = StateInt(r, 1, 2, "turret side"); t.Slot = StateInt(r, 1, 4, "turret slot");
            t.ScanCounter = StateInt(r, 0, 9, "turret scan");
            t.ShootTimer = StateInt(r, 0, OneData.Turrets[t.Id].Interval - 1, "turret timer");
            t.AnimationFrame = StateInt(r, 1, OneData.Timelines[OneData.Turrets[t.Id].Timeline].Frames, "turret frame");
            t.X = StateDouble(r); t.Y = StateDouble(r); t.Rotation = StateDouble(r);
            t.EnemyDistance = StateDouble(r); t.EnemyHeight = StateDouble(r);
            t.Target = StateTarget(actors, r.ReadInt64()); t.Playing = StateBool(r);
            if (t.EnemyDistance < 0 || t.EnemyHeight < 0 || (t.Target != null && t.Target.Side == t.Side)) throw InvalidState("turret target");
            return t;
        }

        private static void WriteStateProjectile(BinaryWriter w, Projectile p)
        {
            w.Write(p.Uid); w.Write(p.BornTick); w.Write(StateProjectileCode(p.Id));
            w.Write(p.Side); w.Write(p.Age); w.Write(p.NativeAge); w.Write(p.Counter); w.Write(p.Timer); w.Write(p.Flame);
            w.Write(p.X); w.Write(p.Y); w.Write(p.Xs); w.Write(p.Ys); w.Write(p.Gravity); w.Write(p.GravityFactor);
            w.Write(p.Rotation); w.Write(p.Spin); w.Write(p.Damage); w.Write(p.Scale); w.Write(p.Removed); w.Write(p.Initialized);
        }

        private static Projectile ReadStateProjectile(BinaryReader r, long tick)
        {
            var p = new Projectile(); p.Uid = r.ReadInt64(); p.BornTick = StateLong(r, 0, tick, "projectile birth");
            p.Id = StateProjectile(r.ReadByte()); p.Side = StateInt(r, 1, 2, "projectile side");
            p.Age = StateInt(r, 0, int.MaxValue - 1, "projectile age"); p.NativeAge = StateInt(r, 0, int.MaxValue - 1, "projectile clock");
            p.Counter = StateInt(r, 0, 1, "projectile counter"); p.Timer = StateInt(r, 0, 40, "projectile timer"); p.Flame = StateInt(r, 0, 30, "projectile flame");
            p.X = StateDouble(r); p.Y = StateDouble(r); p.Xs = StateDouble(r); p.Ys = StateDouble(r);
            p.Gravity = StateDouble(r); p.GravityFactor = StateDouble(r); p.Rotation = StateDouble(r); p.Spin = StateDouble(r);
            p.Damage = StateDouble(r); p.Scale = StateDouble(r); p.Removed = StateBool(r); p.Initialized = StateBool(r);
            if (p.Age > p.NativeAge || p.Scale <= 0 || (p.Flame != 0 && p.Id != "special1" && p.Id != "special2"))
                throw InvalidState("projectile state");
            if (!p.Initialized && (p.Age != 0 || p.NativeAge != 0)) throw InvalidState("projectile initialization");
            return p;
        }

        private static void WriteStateParticle(BinaryWriter w, Particle p)
        {
            w.Write(p.Uid); w.Write(p.BornTick); w.Write(p.Id); w.Write(p.Side); w.Write(p.Age); w.Write(p.NativeAge); w.Write(p.Variant); w.Write(p.Flame);
            w.Write(p.X); w.Write(p.Y); w.Write(p.Xs); w.Write(p.Ys); w.Write(p.Gravity); w.Write(p.GravityFactor);
            w.Write(p.Spin); w.Write(p.Rotation); w.Write(p.Damage); w.Write(p.Scale); w.Write(p.Alpha); w.Write(p.Removed); w.Write(p.Initialized);
        }

        private static Particle ReadStateParticle(BinaryReader r, long tick)
        {
            var p = new Particle(); p.Uid = r.ReadInt64(); p.BornTick = StateLong(r, 0, tick, "particle birth");
            p.Id = StateInt(r, 1, 12, "particle type"); p.Side = StateInt(r, 0, 2, "particle side");
            p.Age = StateInt(r, 0, int.MaxValue - 1, "particle age"); p.NativeAge = StateInt(r, 0, int.MaxValue - 1, "particle clock");
            p.Variant = StateInt(r, 0, 11, "particle variant"); p.Flame = StateInt(r, 0, 30, "particle flame");
            p.X = StateDouble(r); p.Y = StateDouble(r); p.Xs = StateDouble(r); p.Ys = StateDouble(r);
            p.Gravity = StateDouble(r); p.GravityFactor = StateDouble(r); p.Spin = StateDouble(r); p.Rotation = StateDouble(r);
            p.Damage = StateDouble(r); p.Scale = StateDouble(r); p.Alpha = StateDouble(r);
            p.Removed = StateBool(r); p.Initialized = StateBool(r);
            if (p.Age > p.NativeAge || p.Scale <= 0 || (p.Flame != 0 && p.Id != 6)) throw InvalidState("particle state");
            if (!p.Initialized && (p.Age != 0 || p.NativeAge != 0)) throw InvalidState("particle initialization");
            return p;
        }

        private static void WriteStateSpecial(BinaryWriter w, Special s)
        {
            w.Write(s.Uid); w.Write(s.Age); w.Write(s.Counter); w.Write(s.Timer); w.Write(s.Frame); w.Write(s.Initialized); w.Write(s.X); w.Write(s.Y);
        }

        private static Special ReadStateSpecial(BinaryReader r)
        {
            var s = new Special(); s.Uid = r.ReadInt64(); s.Age = StateInt(r, 1, 5, "special age");
            s.Counter = StateInt(r, 0, int.MaxValue - 1, "special counter"); s.Timer = r.ReadInt32();
            s.Frame = StateInt(r, 1, int.MaxValue - 1, "special frame"); s.Initialized = StateBool(r); s.X = StateDouble(r); s.Y = StateDouble(r);
            // Age4 continues after its initial timer becomes negative; its X position controls removal.
            if (s.Frame > 301 || s.Counter > 300 || s.Timer < -100 || s.Timer > 300) throw InvalidState("special timing");
            return s;
        }

        private void ValidateStateGraph(Dictionary<long, Actor> actors, HashSet<long> activeIds)
        {
            var reachable = CollectStateActors();
            if (reachable.Count != actors.Count) throw InvalidState("unreachable actor");
            foreach (var actor in actors.Values)
                if (actor.Id != 0 && !activeIds.Contains(actor.Uid) && !actor.Removed) throw InvalidState("detached live actor");
            if (_healing != (_special != null && _special.Initialized && _special.Age == 3)) throw InvalidState("healing state");
            if (_special != null && _special.Age > _age) throw InvalidState("special era");
            if (_playerBase.MaximumHealth != StateBaseHealth(_age) || _enemyBase.MaximumHealth != StateBaseHealth(_enemyAge))
                throw InvalidState("base maximum health");
        }

        private static double StateBaseHealth(int age)
        {
            double health = 500;
            for (int i = 2; i <= age; ++i) health += 300 * i;
            return health;
        }

        private void CheckStateVisualId(long id, HashSet<long> ids)
        {
            if (id < 100000 || id >= _nextVisual || !ids.Add(id)) throw InvalidState("visual identity");
        }

        private static Actor StateTarget(Dictionary<long, Actor> actors, long id)
        {
            if (id == 0) return null;
            Actor actor;
            if (!actors.TryGetValue(id, out actor)) throw InvalidState("actor reference");
            return actor;
        }

        private static byte StateAnimationCode(string animation)
        {
            switch (animation)
            {
                case "idle": return 0; case "walk": return 1; case "attack": return 2;
                case "die": return 3; case "shoot": return 4; case "shootwalk": return 5;
                default: throw InvalidState("animation");
            }
        }

        private static string StateAnimation(byte value)
        {
            switch (value)
            {
                case 0: return "idle"; case 1: return "walk"; case 2: return "attack";
                case 3: return "die"; case 4: return "shoot"; case 5: return "shootwalk";
                default: throw InvalidState("animation");
            }
        }

        private static byte StateProjectileCode(string id)
        {
            switch (id)
            {
                case "special1": return 21; case "special2": return 22;
                case "special4": return 24; case "special5": return 25;
            }
            int value;
            if (!int.TryParse(id, out value) || value < 1 || value > 12 || id != value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw InvalidState("projectile type");
            return (byte)value;
        }

        private static string StateProjectile(byte code)
        {
            if (code >= 1 && code <= 12) return code.ToString(System.Globalization.CultureInfo.InvariantCulture);
            switch (code)
            {
                case 21: return "special1"; case 22: return "special2";
                case 24: return "special4"; case 25: return "special5";
                default: throw InvalidState("projectile type");
            }
        }

        private static bool StateBool(BinaryReader r)
        {
            byte value = r.ReadByte();
            if (value > 1) throw InvalidState("boolean");
            return value != 0;
        }

        private static int StateInt(BinaryReader r, int minimum, int maximum, string field)
        {
            int value = r.ReadInt32();
            if (value < minimum || value > maximum) throw InvalidState(field);
            return value;
        }

        private static long StateLong(BinaryReader r, long minimum, long maximum, string field)
        {
            long value = r.ReadInt64();
            if (value < minimum || value > maximum) throw InvalidState(field);
            return value;
        }

        private static double StateDouble(BinaryReader r)
        {
            double value = r.ReadDouble();
            // This port represents AVM1 part8's ignored NaN display assignment with unchanged finite X.
            // No persisted One field uses NaN as a sentinel; reject non-finite payload numbers.
            if (double.IsNaN(value) || double.IsInfinity(value)) throw InvalidState("non-finite number");
            return value;
        }

        private static InvalidDataException InvalidState(string field)
        {
            return new InvalidDataException("Invalid Age of War One state: " + field + ".");
        }
    }
}
