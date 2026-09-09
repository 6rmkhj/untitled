using System.Collections.Generic;
using UnityEngine;

namespace SignalHaul
{
    public enum MonsterNoiseKind : byte
    {
        Movement = 0,
        Voice = 1,
        LootImpact = 2,
        MonsterAttack = 3
    }

    public readonly struct MonsterNoiseEvent
    {
        public MonsterNoiseEvent(Vector3 position, float radius, ulong sourceClientId, MonsterNoiseKind kind, float time)
        {
            Position = position;
            Radius = radius;
            SourceClientId = sourceClientId;
            Kind = kind;
            Time = time;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
        public ulong SourceClientId { get; }
        public MonsterNoiseKind Kind { get; }
        public float Time { get; }
    }

    public static class MonsterNoiseSystem
    {
        private const int MaxEvents = 48;
        private const float RetentionSeconds = 4f;
        private static readonly List<MonsterNoiseEvent> Events = new(MaxEvents);

        public static void EmitServer(
            Vector3 position,
            float radius,
            ulong sourceClientId = ulong.MaxValue,
            MonsterNoiseKind kind = MonsterNoiseKind.Movement)
        {
            if (radius <= .1f)
                return;

            PruneOldEvents();
            if (Events.Count >= MaxEvents)
                Events.RemoveAt(0);

            Events.Add(new MonsterNoiseEvent(
                position,
                Mathf.Clamp(radius, .5f, 40f),
                sourceClientId,
                kind,
                Time.time));
        }

        public static bool TryGetLoudest(
            Vector3 listenerPosition,
            float hearingRange,
            float maxAge,
            out MonsterNoiseEvent result)
        {
            PruneOldEvents();
            result = default;

            float bestScore = float.NegativeInfinity;
            float allowedAge = Mathf.Clamp(maxAge, .1f, RetentionSeconds);
            float listenerRange = Mathf.Max(.5f, hearingRange);

            for (int i = Events.Count - 1; i >= 0; i--)
            {
                MonsterNoiseEvent noise = Events[i];
                float age = Time.time - noise.Time;
                if (age > allowedAge)
                    continue;

                float effectiveRadius = Mathf.Min(listenerRange, noise.Radius);
                float distance = Vector3.Distance(listenerPosition, noise.Position);
                if (distance > effectiveRadius)
                    continue;

                float distanceScore = 1f - distance / Mathf.Max(.01f, effectiveRadius);
                float freshnessScore = 1f - age / allowedAge;
                float kindBonus = noise.Kind == MonsterNoiseKind.Voice ? .18f : noise.Kind == MonsterNoiseKind.LootImpact ? .12f : 0f;
                float score = distanceScore * .7f + freshnessScore * .3f + kindBonus;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                result = noise;
            }

            return bestScore > float.NegativeInfinity;
        }

        private static void PruneOldEvents()
        {
            float cutoff = Time.time - RetentionSeconds;
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Events[i].Time < cutoff)
                    Events.RemoveAt(i);
            }
        }
    }
}
