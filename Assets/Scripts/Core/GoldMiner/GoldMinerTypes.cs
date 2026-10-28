using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.GoldMiner
{
    public enum GoldMinerMode
    {
        Solo,
        Coop
    }

    public enum GoldMinerPhase
    {
        Ready,
        Running,
        Draining,
        Won,
        Failed,
        Shop,
        Completed
    }

    public enum GoldMinerKind
    {
        GoldSmall,
        GoldMedium,
        GoldLarge,
        RockSmall,
        RockLarge,
        Diamond,
        MysteryBag,
        Mole,
        DiamondMole
    }

    public enum GoldMinerHookState
    {
        Swinging,
        Extending,
        RetractingEmpty,
        RetractingLoaded
    }

    public enum GoldMinerEntityStatus
    {
        Available,
        Held,
        Collected,
        Destroyed
    }

    public enum GoldMinerItem
    {
        Dynamite,
        Strength,
        Clover,
        RockBook,
        DiamondOil
    }

    public enum GoldMinerOutcome
    {
        Won,
        Failed,
        Completed
    }

    public enum GoldMinerEventKind
    {
        Launched,
        Caught,
        ReturnedEmpty,
        Collected,
        Dynamite,
        Countdown,
        SettlementReady
    }

    [Flags]
    public enum GoldMinerSuspendReason
    {
        None = 0,
        NotVisible = 1,
        AppPaused = 2,
        FocusLost = 4,
        OrientationPending = 8,
        BlockingModal = 16,
        NeedsExplicitContinue = 32
    }

    [Serializable]
    public sealed class GoldMinerBuffs
    {
        public bool Strength, Clover, RockBook, DiamondOil;
        public bool IsEmpty
        {
            get
            {
                return !Strength && !Clover && !RockBook && !DiamondOil;
            }
        }

        public GoldMinerBuffs Copy()
        {
            return (GoldMinerBuffs)MemberwiseClone();
        }

        public bool Same(GoldMinerBuffs b)
        {
            return b != null && Strength == b.Strength && Clover == b.Clover && RockBook == b.RockBook && DiamondOil == b.DiamondOil;
        }
    }

    [Serializable]
    public sealed class GoldMinerPlayer
    {
        public int PlayerId, Bombs;
        public long TotalCollected, LevelCollected;
        public GoldMinerPlayer Copy()
        {
            return (GoldMinerPlayer)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class GoldMinerOpeningPlayer
    {
        public int PlayerId, Bombs;
        public long TotalCollected;
        public GoldMinerOpeningPlayer Copy()
        {
            return (GoldMinerOpeningPlayer)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class GoldMinerHook
    {
        public int PlayerId, SwingDirection;
        public GoldMinerHookState State;
        public double AngleRadians, Length;
        public int? HeldEntityId;
        public GoldMinerHook Copy()
        {
            return (GoldMinerHook)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class GoldMinerEntity
    {
        public int Id, MoveDirection;
        public GoldMinerKind Kind;
        public GoldMinerEntityStatus Status;
        public double X, Y;
        public long? BagValue;
        public int? OwnerPlayerId;
        public GoldMinerEntity Copy()
        {
            return (GoldMinerEntity)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class GoldMinerRecord
    {
        public int BestClearedLevel;
        public long BestRunGrossIncome, CompletionCount;
        public GoldMinerRecord Copy()
        {
            return (GoldMinerRecord)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class GoldMinerAllocation
    {
        public int PlayerId, Count;
        public GoldMinerAllocation Copy()
        {
            return (GoldMinerAllocation)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class GoldMinerBombStock
    {
        public int PlayerId, Bombs;
        public GoldMinerBombStock Copy()
        {
            return (GoldMinerBombStock)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class GoldMinerOffer
    {
        public GoldMinerItem ItemId;
        public long Price;
        public int Remaining, Purchased;
        public GoldMinerAllocation[] Allocations;
        public GoldMinerOffer Copy()
        {
            var c = (GoldMinerOffer)MemberwiseClone();
            c.Allocations = GoldMinerCopies.Array(Allocations, x => x.Copy());
            return c;
        }
    }

    [Serializable]
    public sealed class GoldMinerShop
    {
        public string Id;
        public long EntryCash, EntryShopSpent, Spent;
        public GoldMinerBombStock[] EntryBombs;
        public GoldMinerOffer[] Offers;
        public GoldMinerShop Copy()
        {
            var c = (GoldMinerShop)MemberwiseClone();
            c.EntryBombs = GoldMinerCopies.Array(EntryBombs, x => x.Copy());
            c.Offers = GoldMinerCopies.Array(Offers, x => x.Copy());
            return c;
        }
    }

    [Serializable]
    public sealed class GoldMinerContribution
    {
        public int PlayerId;
        public long Amount;
        public GoldMinerContribution Copy()
        {
            return (GoldMinerContribution)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class GoldMinerSettlement
    {
        public string Id;
        public GoldMinerOutcome Outcome;
        public long Cash, Target, RunGrossIncome;
        public GoldMinerContribution[] Contributions;
        public bool RecordCommitted;
        public GoldMinerSettlement Copy()
        {
            var c = (GoldMinerSettlement)MemberwiseClone();
            c.Contributions = GoldMinerCopies.Array(Contributions, x => x.Copy());
            return c;
        }
    }

    public sealed class GoldMinerEvent
    {
        public GoldMinerEventKind Kind { get; private set; }
        public int PlayerId { get; private set; }
        public int? EntityId { get; private set; }
        public long Amount { get; private set; }

        public GoldMinerEvent(GoldMinerEventKind kind, int playerId = -1, int? entityId = null, long amount = 0)
        {
            Kind = kind;
            PlayerId = playerId;
            EntityId = entityId;
            Amount = amount;
        }
    }

    public sealed class GoldMinerStepResult
    {
        private readonly GoldMinerEvent[] events;
        public IReadOnlyList<GoldMinerEvent> Events
        {
            get
            {
                return System.Array.AsReadOnly(events);
            }
        }

        public bool SettlementReady { get; private set; }
        public bool Advanced { get; private set; }

        internal GoldMinerStepResult(bool advanced, bool settlementReady, List<GoldMinerEvent> events)
        {
            Advanced = advanced;
            SettlementReady = settlementReady;
            this.events = events.ToArray();
        }
    }

    internal static class GoldMinerCopies
    {
        internal static T[] Array<T>(T[] value, Func<T, T> clone)
            where T : class
        {
            if (value == null)
                return null;
            var result = new T[value.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = value[i] == null ? null : clone(value[i]);
            return result;
        }
    }
}
