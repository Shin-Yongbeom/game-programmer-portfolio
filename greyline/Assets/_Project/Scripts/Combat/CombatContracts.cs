using UnityEngine;

namespace Greyline.Combat
{
    [System.Flags]
    public enum CombatHitFlags
    {
        None = 0,
        Counterable = 1 << 0,
        EnvironmentalCandidate = 1 << 1,
        Uninterruptible = 1 << 2,
    }

    public enum HitReactionType
    {
        Light,
        Heavy,
        Knockdown,
    }

    public readonly struct DamageInfo
    {
        public DamageInfo(
            GameObject source,
            float amount,
            Vector3 point,
            Vector3 direction,
            HitReactionType reactionType,
            float knockbackDistance,
            float knockbackDuration)
            : this(source, null, 0, amount, point, direction, reactionType,
                knockbackDistance, knockbackDuration, CombatHitFlags.None)
        {
        }

        public DamageInfo(
            GameObject source,
            string attackId,
            int hitIndex,
            float amount,
            Vector3 point,
            Vector3 direction,
            HitReactionType reactionType,
            float knockbackDistance,
            float knockbackDuration,
            CombatHitFlags flags)
        {
            Source = source;
            AttackId = attackId;
            HitIndex = hitIndex;
            Amount = amount;
            Point = point;
            Direction = direction;
            ReactionType = reactionType;
            KnockbackDistance = knockbackDistance;
            KnockbackDuration = knockbackDuration;
            Flags = flags;
        }

        public GameObject Source { get; }
        public string AttackId { get; }
        public int HitIndex { get; }
        public float Amount { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public HitReactionType ReactionType { get; }
        public float KnockbackDistance { get; }
        public float KnockbackDuration { get; }
        public CombatHitFlags Flags { get; }
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        void ApplyDamage(DamageInfo damage);
    }

    /// <summary>Local defense can reject or reduce a hit before health/reaction events fire.</summary>
    public interface IDamageFilter
    {
        bool FilterDamage(DamageInfo incoming, out DamageInfo accepted);
    }

    public interface IHitReactionReceiver
    {
        void RequestHitReaction(DamageInfo damage);
    }

    public readonly struct CounterRequest
    {
        public CounterRequest(GameObject defender, DamageInfo incomingDamage)
        {
            Defender = defender;
            IncomingDamage = incomingDamage;
        }

        public GameObject Defender { get; }
        public DamageInfo IncomingDamage { get; }
    }

    public interface ICounterable
    {
        bool CanBeCountered { get; }
        void ReceiveCounter(CounterRequest request);
    }

    public readonly struct EnvironmentalTakedownRequest
    {
        public EnvironmentalTakedownRequest(GameObject attacker, Vector3 contactPoint, Vector3 surfaceNormal)
        {
            Attacker = attacker;
            ContactPoint = contactPoint;
            SurfaceNormal = surfaceNormal;
        }

        public GameObject Attacker { get; }
        public Vector3 ContactPoint { get; }
        public Vector3 SurfaceNormal { get; }
    }

    public interface IEnvironmentalTakedownTarget
    {
        bool CanReceiveEnvironmentalTakedown { get; }
        void ReceiveEnvironmentalTakedown(EnvironmentalTakedownRequest request);
    }
}
