using System;
using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    public enum MonsterArchetype : byte
    {
        Stalker = 0,
        Brute = 1,
        Scavenger = 2
    }

    public enum MonsterAiState : byte
    {
        Idle = 0,
        Investigating = 1,
        ChasingPlayer = 2,
        HuntingLoot = 3,
        Attacking = 4
    }

    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public sealed class MonsterController : NetworkBehaviour
    {
        [Header("Identity")]
        [SerializeField] private MonsterArchetype archetype = MonsterArchetype.Stalker;

        [Header("Perception")]
        [SerializeField, Min(1f)] private float playerDetectionRange = 8f;
        [SerializeField, Min(1f)] private float hearingRange = 20f;
        [SerializeField, Min(.1f)] private float noiseMemory = 3f;
        [SerializeField, Min(1f)] private float lootDetectionRange = 18f;

        [Header("Movement")]
        [SerializeField, Min(.5f)] private float moveSpeed = 4f;
        [SerializeField, Min(.1f)] private float patrolRadius = 3f;
        [SerializeField, Min(0f)] private float hoverHeight = .9f;

        [Header("Attack")]
        [SerializeField, Min(.5f)] private float attackDistance = 1.7f;
        [SerializeField, Min(.1f)] private float attackCooldown = 1.5f;
        [SerializeField, Min(0f)] private float playerDamage = 12f;
        [SerializeField, Min(0f)] private float knockbackForce = 4f;
        [SerializeField, Min(0f)] private float lootDamage = 20f;

        [Header("Network")]
        [SerializeField, Min(.03f)] private float syncInterval = .08f;
        [SerializeField, Min(1f)] private float remoteLerpSpeed = 14f;

        public MonsterArchetype Archetype => archetype;
        public MonsterAiState State => (MonsterAiState)state.Value;

        private readonly NetworkVariable<Vector3> syncedPosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> syncedRotation = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> state = new(
            (byte)MonsterAiState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody body;
        private Vector3 homePosition;
        private Vector3 investigatePosition;
        private float investigateUntil;
        private float retargetAt;
        private float attackReadyAt;
        private float syncTimer;
        private float patrolPhase;
        private PlayerController playerTarget;
        private PhysicsLoot lootTarget;

        public void Configure(MonsterArchetype type)
        {
            archetype = type;
            ApplyArchetypeDefaults();
        }

        private void OnValidate()
        {
            ApplyArchetypeDefaults();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            homePosition = transform.position;
            syncedPosition.Value = transform.position;
            syncedRotation.Value = transform.rotation;
            state.Value = (byte)MonsterAiState.Idle;
            patrolPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (!IsServer)
            {
                float t = 1f - Mathf.Exp(-remoteLerpSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, syncedPosition.Value, t);
                transform.rotation = Quaternion.Slerp(transform.rotation, syncedRotation.Value, t);
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.Ended)
                return;

            if (Time.time >= retargetAt)
            {
                retargetAt = Time.time + .18f;
                AcquireTargetServer();
            }

            UpdateServerMovementAndAttack();
            SyncTransformServer();
        }

        private void AcquireTargetServer()
        {
            if (archetype == MonsterArchetype.Scavenger)
            {
                lootTarget = FindBestLootTarget();
                if (lootTarget != null)
                {
                    playerTarget = null;
                    state.Value = (byte)MonsterAiState.HuntingLoot;
                    return;
                }
            }

            float directRange = archetype == MonsterArchetype.Stalker
                ? playerDetectionRange * .7f
                : playerDetectionRange;

            PlayerController nearest = FindNearestPlayer(directRange, out _);
            if (nearest != null)
            {
                playerTarget = nearest;
                lootTarget = null;
                state.Value = (byte)MonsterAiState.ChasingPlayer;
                return;
            }

            if (MonsterNoiseSystem.TryGetLoudest(transform.position, hearingRange, noiseMemory, out MonsterNoiseEvent noise))
            {
                investigatePosition = noise.Position + Vector3.up * hoverHeight;
                investigateUntil = Time.time + noiseMemory;
                state.Value = (byte)MonsterAiState.Investigating;

                if (noise.SourceClientId != ulong.MaxValue)
                {
                    PlayerController source = GetPlayer(noise.SourceClientId);
                    if (source != null && Vector3.Distance(transform.position, source.transform.position) <= hearingRange * .75f)
                        playerTarget = source;
                }

                return;
            }

            if (Time.time > investigateUntil)
            {
                playerTarget = null;
                lootTarget = null;
                state.Value = (byte)MonsterAiState.Idle;
            }
        }

        private void UpdateServerMovementAndAttack()
        {
            if (lootTarget != null && (lootTarget.Broken || lootTarget.Delivered))
                lootTarget = null;
            if (playerTarget != null && playerTarget.Health <= 0f)
                playerTarget = null;

            if (archetype == MonsterArchetype.Scavenger && lootTarget != null)
            {
                Vector3 target = lootTarget.transform.position + Vector3.up * .6f;
                MoveToward(target, moveSpeed);

                if (Vector3.Distance(transform.position, target) <= attackDistance)
                    AttackLootServer(lootTarget);
                return;
            }

            if (playerTarget != null)
            {
                Vector3 target = playerTarget.transform.position + Vector3.up * 1f;
                float distance = Vector3.Distance(transform.position, target);

                if (distance <= Mathf.Max(attackDistance, playerDetectionRange * 1.6f))
                {
                    MoveToward(target, moveSpeed);
                    if (distance <= attackDistance)
                        AttackPlayerServer(playerTarget);
                    return;
                }

                playerTarget = null;
            }

            if (Time.time <= investigateUntil)
            {
                MoveToward(investigatePosition, moveSpeed * .9f);
                if (Vector3.Distance(transform.position, investigatePosition) < .8f)
                    investigateUntil = Time.time;
                return;
            }

            Vector3 patrolTarget = homePosition + new Vector3(
                Mathf.Cos(Time.time * .45f + patrolPhase) * patrolRadius,
                Mathf.Sin(Time.time * .7f + patrolPhase) * .65f,
                Mathf.Sin(Time.time * .45f + patrolPhase) * patrolRadius);
            MoveToward(patrolTarget, moveSpeed * .42f);
        }

        private void AttackPlayerServer(PlayerController target)
        {
            if (target == null || Time.time < attackReadyAt)
                return;

            attackReadyAt = Time.time + attackCooldown;
            state.Value = (byte)MonsterAiState.Attacking;

            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .01f)
                direction = transform.forward;
            direction.Normalize();

            float upward = archetype == MonsterArchetype.Brute ? 4.5f : 1.8f;
            Vector3 knockback = direction * knockbackForce + Vector3.up * upward;
            target.ApplyMonsterHitServer(playerDamage, knockback);

            float noiseRadius = archetype == MonsterArchetype.Brute ? 18f : 10f;
            MonsterNoiseSystem.EmitServer(transform.position, noiseRadius, ulong.MaxValue, MonsterNoiseKind.MonsterAttack);
        }

        private void AttackLootServer(PhysicsLoot target)
        {
            if (target == null || Time.time < attackReadyAt || target.Broken || target.Delivered)
                return;

            attackReadyAt = Time.time + attackCooldown;
            state.Value = (byte)MonsterAiState.Attacking;

            Vector3 direction = target.transform.position - transform.position;
            if (direction.sqrMagnitude < .01f)
                direction = transform.forward;
            direction.Normalize();

            Vector3 impulse = direction * Mathf.Max(4f, knockbackForce) + Vector3.up * 2.2f;
            target.ApplyMonsterImpactServer(lootDamage, impulse);
            MonsterNoiseSystem.EmitServer(target.transform.position, 15f, ulong.MaxValue, MonsterNoiseKind.LootImpact);
        }

        private void MoveToward(Vector3 target, float speed)
        {
            Vector3 next = Vector3.MoveTowards(transform.position, target, Mathf.Max(.1f, speed) * Time.deltaTime);
            Vector3 direction = next - transform.position;
            transform.position = next;

            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > .0001f)
            {
                Quaternion desired = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, 8f * Time.deltaTime);
            }
        }

        private PlayerController FindNearestPlayer(float range, out float nearestDistance)
        {
            nearestDistance = float.PositiveInfinity;
            PlayerController nearest = null;

            if (NetworkManager == null || !NetworkManager.IsServer)
                return null;

            foreach (NetworkClient client in NetworkManager.ConnectedClients.Values)
            {
                if (client.PlayerObject == null)
                    continue;

                PlayerController player = client.PlayerObject.GetComponent<PlayerController>();
                if (player == null || player.Health <= 0f)
                    continue;

                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance > range || distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = player;
            }

            return nearest;
        }

        private PhysicsLoot FindBestLootTarget()
        {
            PhysicsLoot[] loot = FindObjectsByType<PhysicsLoot>(FindObjectsSortMode.None);
            PhysicsLoot best = null;
            float bestScore = float.NegativeInfinity;

            foreach (PhysicsLoot item in loot)
            {
                if (item == null || item.Broken || item.Delivered)
                    continue;

                float distance = Vector3.Distance(transform.position, item.transform.position);
                if (distance > lootDetectionRange)
                    continue;

                float carryBonus = item.CarrierCount > 0 ? 9f : 0f;
                float objectiveBonus = item.IsRequiredObjective ? 3f : 0f;
                float valueScore = item.CurrentValue / 140f;
                float score = carryBonus + objectiveBonus + valueScore - distance * .45f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = item;
            }

            return best;
        }

        private PlayerController GetPlayer(ulong clientId)
        {
            if (NetworkManager == null || !NetworkManager.IsServer ||
                !NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return null;
            }

            return client.PlayerObject.GetComponent<PlayerController>();
        }

        public void SetMapHomeServer(Vector3 position)
        {
            if (NetworkManager != null && !NetworkManager.IsServer)
                return;

            homePosition = position;
            transform.position = position;
            transform.rotation = Quaternion.identity;

            if (IsSpawned && IsServer)
            {
                syncedPosition.Value = position;
                syncedRotation.Value = Quaternion.identity;
            }
        }

        private void SyncTransformServer()
        {
            syncTimer -= Time.deltaTime;
            if (syncTimer > 0f)
                return;

            syncTimer = syncInterval;
            syncedPosition.Value = transform.position;
            syncedRotation.Value = transform.rotation;
        }

        private void ApplyArchetypeDefaults()
        {
            switch (archetype)
            {
                case MonsterArchetype.Brute:
                    playerDetectionRange = 12f;
                    hearingRange = 13f;
                    noiseMemory = 2.4f;
                    lootDetectionRange = 6f;
                    moveSpeed = 5.1f;
                    patrolRadius = 2.4f;
                    hoverHeight = .7f;
                    attackDistance = 2.1f;
                    attackCooldown = 1.8f;
                    playerDamage = 20f;
                    knockbackForce = 10.5f;
                    lootDamage = 8f;
                    break;

                case MonsterArchetype.Scavenger:
                    playerDetectionRange = 6f;
                    hearingRange = 14f;
                    noiseMemory = 3f;
                    lootDetectionRange = 22f;
                    moveSpeed = 5.8f;
                    patrolRadius = 4f;
                    hoverHeight = 1f;
                    attackDistance = 1.8f;
                    attackCooldown = 1.25f;
                    playerDamage = 7f;
                    knockbackForce = 4f;
                    lootDamage = 24f;
                    break;

                default:
                    playerDetectionRange = 8f;
                    hearingRange = 24f;
                    noiseMemory = 3.5f;
                    lootDetectionRange = 5f;
                    moveSpeed = 4.4f;
                    patrolRadius = 3.3f;
                    hoverHeight = 1.1f;
                    attackDistance = 1.65f;
                    attackCooldown = 1.35f;
                    playerDamage = 12f;
                    knockbackForce = 4.8f;
                    lootDamage = 5f;
                    break;
            }
        }
    }
}
