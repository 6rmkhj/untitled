using System;
using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public sealed class PhysicsLoot : NetworkBehaviour
    {
        public const ulong NoCarrier = ulong.MaxValue;

        [Header("Identity")]
        [SerializeField] private string displayName = "SALVAGE";
        [SerializeField, Min(0)] private int baseValue = 100;
        [SerializeField] private bool requiredObjective;

        [Header("Carry")]
        [SerializeField, Min(.1f)] private float weight = 5f;
        [SerializeField, Range(1, 2)] private int requiredCarriers = 1;
        [SerializeField, Range(1, 2)] private int maxCarriers = 1;
        [SerializeField, Min(1f)] private float maxCarryDistance = 7f;

        [Header("Durability")]
        [SerializeField, Min(1f)] private float maxDurability = 100f;
        [SerializeField, Min(.1f)] private float impactThreshold = 7f;
        [SerializeField, Min(.1f)] private float impactDamageMultiplier = 1.2f;

        [Header("Network")]
        [SerializeField, Min(.03f)] private float syncInterval = .06f;
        [SerializeField, Min(1f)] private float remoteLerpSpeed = 18f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int BaseValue => baseValue;
        public bool IsRequiredObjective => requiredObjective;
        public float Weight => weight;
        public int RequiredCarriers => requiredCarriers;
        public int MaxCarriers => maxCarriers;
        public int CarrierCount => (carrierA.Value != NoCarrier ? 1 : 0) + (carrierB.Value != NoCarrier ? 1 : 0);
        public bool HasRequiredCarriers => CarrierCount >= requiredCarriers;
        public bool NeedsMoreCarriers => CarrierCount > 0 && CarrierCount < requiredCarriers;
        public float Durability => durability.Value;
        public float MaxDurability => maxDurability;
        public float Durability01 => maxDurability <= 0f ? 0f : Mathf.Clamp01(durability.Value / maxDurability);
        public bool Delivered => delivered.Value;
        public bool Broken => broken.Value;
        public int CurrentValue => Broken ? 0 : Mathf.RoundToInt(baseValue * Mathf.Lerp(.35f, 1f, Durability01));

        private readonly NetworkVariable<Vector3> syncedPosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> syncedRotation = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> carrierA = new(
            NoCarrier,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> carrierB = new(
            NoCarrier,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> durability = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> delivered = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> broken = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody body;
        private Renderer[] renderers;
        private Collider[] colliders;
        private Material fragmentMaterial;
        private float syncTimer;
        private float serverSpawnGraceUntil;
        private bool fragmentsSpawned;

        public void Configure(
            string itemName,
            int value,
            float itemWeight,
            float durabilityPoints,
            float collisionThreshold,
            float collisionDamageMultiplier,
            int carriersNeeded,
            int carrierLimit,
            bool objective)
        {
            displayName = itemName;
            baseValue = Mathf.Max(0, value);
            weight = Mathf.Max(.1f, itemWeight);
            maxDurability = Mathf.Max(1f, durabilityPoints);
            impactThreshold = Mathf.Max(.1f, collisionThreshold);
            impactDamageMultiplier = Mathf.Max(.1f, collisionDamageMultiplier);
            requiredCarriers = Mathf.Clamp(carriersNeeded, 1, 2);
            maxCarriers = Mathf.Clamp(carrierLimit, requiredCarriers, 2);
            requiredObjective = objective;

            if (body == null)
                body = GetComponent<Rigidbody>();
            if (body != null)
                body.mass = weight;
        }

        private void OnValidate()
        {
            requiredCarriers = Mathf.Clamp(requiredCarriers, 1, 2);
            maxCarriers = Mathf.Clamp(maxCarriers, requiredCarriers, 2);
            weight = Mathf.Max(.1f, weight);
            maxDurability = Mathf.Max(1f, maxDurability);
            impactThreshold = Mathf.Max(.1f, impactThreshold);
            impactDamageMultiplier = Mathf.Max(.1f, impactDamageMultiplier);

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
                rb.mass = weight;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = weight;
            body.isKinematic = true;

            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            if (renderers.Length > 0)
                fragmentMaterial = renderers[0].sharedMaterial;
        }

        public override void OnNetworkSpawn()
        {
            delivered.OnValueChanged += OnDeliveredChanged;
            broken.OnValueChanged += OnBrokenChanged;

            if (IsServer)
            {
                syncedPosition.Value = transform.position;
                syncedRotation.Value = transform.rotation;
                carrierA.Value = NoCarrier;
                carrierB.Value = NoCarrier;
                durability.Value = maxDurability;
                delivered.Value = false;
                broken.Value = false;
                body.isKinematic = false;
                body.useGravity = true;
                body.linearDamping = .35f;
                body.angularDamping = .2f;
                serverSpawnGraceUntil = Time.time + 1f;
            }
            else
            {
                body.isKinematic = true;
            }

            ApplyVisibilityState();
        }

        public override void OnNetworkDespawn()
        {
            delivered.OnValueChanged -= OnDeliveredChanged;
            broken.OnValueChanged -= OnBrokenChanged;
        }

        private void Update()
        {
            if (!IsSpawned || IsServer || Delivered || Broken)
                return;

            float t = 1f - Mathf.Exp(-remoteLerpSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, syncedPosition.Value, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, syncedRotation.Value, t);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || Delivered || Broken)
                return;

            ValidateCarriersServer();
            UpdateCarryPhysicsServer();

            syncTimer -= Time.fixedDeltaTime;
            if (syncTimer > 0f)
                return;

            syncTimer = syncInterval;
            syncedPosition.Value = body.position;
            syncedRotation.Value = body.rotation;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsSpawned || !IsServer || Delivered || Broken || Time.time < serverSpawnGraceUntil)
                return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed <= impactThreshold)
                return;

            float damage = (impactSpeed - impactThreshold) * impactDamageMultiplier * 12f;
            ApplyDamageServer(damage);
        }

        private void ValidateCarriersServer()
        {
            if (carrierA.Value != NoCarrier && GetPlayer(carrierA.Value) == null)
                carrierA.Value = NoCarrier;
            if (carrierB.Value != NoCarrier && GetPlayer(carrierB.Value) == null)
                carrierB.Value = NoCarrier;
        }

        private void UpdateCarryPhysicsServer()
        {
            int count = CarrierCount;
            if (count <= 0)
            {
                body.useGravity = true;
                body.linearDamping = .35f;
                body.angularDamping = .2f;
                return;
            }

            Vector3 target = Vector3.zero;
            int validCount = 0;

            if (carrierA.Value != NoCarrier && GetPlayer(carrierA.Value) is PlayerController first)
            {
                if (Vector3.Distance(body.position, first.GetServerHoldPosition()) > maxCarryDistance)
                {
                    ReleaseServer(carrierA.Value, Vector3.zero);
                }
                else
                {
                    target += first.GetServerHoldPosition();
                    validCount++;
                }
            }

            if (carrierB.Value != NoCarrier && GetPlayer(carrierB.Value) is PlayerController second)
            {
                if (Vector3.Distance(body.position, second.GetServerHoldPosition()) > maxCarryDistance)
                {
                    ReleaseServer(carrierB.Value, Vector3.zero);
                }
                else
                {
                    target += second.GetServerHoldPosition();
                    validCount++;
                }
            }

            if (validCount <= 0)
                return;

            target /= validCount;
            Vector3 delta = target - body.position;

            if (validCount < requiredCarriers)
            {
                body.useGravity = true;
                body.linearDamping = 1.2f;
                Vector3 tugVelocity = Vector3.ClampMagnitude(delta * 1.4f, 2.4f);
                tugVelocity.y = Mathf.Min(tugVelocity.y, .5f);
                body.linearVelocity = Vector3.Lerp(body.linearVelocity, tugVelocity, .18f);
                return;
            }

            body.useGravity = false;
            float weightPerCarrier = weight / Mathf.Max(1, validCount);
            float response = Mathf.Clamp(13f - weightPerCarrier * .35f, 5f, 12f);
            Vector3 desiredVelocity = Vector3.ClampMagnitude(delta * response, 11f);
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, desiredVelocity, .42f);
            body.angularVelocity *= .78f;
            body.linearDamping = Mathf.Clamp(2.2f + weightPerCarrier * .08f, 2.5f, 5f);
            body.angularDamping = 2f;
        }

        public bool IsHeldBy(ulong clientId)
        {
            return carrierA.Value == clientId || carrierB.Value == clientId;
        }

        public bool TryGrabServer(ulong clientId)
        {
            if (!IsServer || Delivered || Broken || IsHeldBy(clientId) || CarrierCount >= maxCarriers)
                return false;

            PlayerController player = GetPlayer(clientId);
            if (player == null || Vector3.Distance(body.position, player.GetServerHoldPosition()) > 4.5f)
                return false;

            if (carrierA.Value == NoCarrier)
                carrierA.Value = clientId;
            else if (carrierB.Value == NoCarrier)
                carrierB.Value = clientId;
            else
                return false;

            body.isKinematic = false;
            body.useGravity = CarrierCount < requiredCarriers;
            return true;
        }

        public void ReleaseServer(ulong clientId, Vector3 throwVelocity)
        {
            if (!IsServer || Delivered || Broken || !IsHeldBy(clientId))
                return;

            bool couldThrow = requiredCarriers == 1 && CarrierCount == 1 && weight <= 18f;

            if (carrierA.Value == clientId)
                carrierA.Value = NoCarrier;
            if (carrierB.Value == clientId)
                carrierB.Value = NoCarrier;

            body.isKinematic = false;
            body.useGravity = CarrierCount < requiredCarriers;
            body.linearDamping = .35f;
            body.angularDamping = .2f;

            if (couldThrow && CarrierCount == 0 && throwVelocity.sqrMagnitude > .01f)
            {
                float weightScale = Mathf.Clamp(10f / Mathf.Max(1f, weight), .35f, 1f);
                body.linearVelocity = Vector3.ClampMagnitude(throwVelocity * weightScale, 16f);
            }
        }

        public bool CanThrow(ulong clientId)
        {
            return IsHeldBy(clientId) && requiredCarriers == 1 && CarrierCount == 1 && weight <= 18f && !Broken && !Delivered;
        }

        public bool CanClimb(ulong clientId)
        {
            if (!IsHeldBy(clientId))
                return true;

            float share = weight / Mathf.Max(1, CarrierCount);
            return HasRequiredCarriers && share <= 12f;
        }

        public float GetCarrySpeedMultiplier(ulong clientId)
        {
            if (!IsHeldBy(clientId))
                return 1f;

            float share = weight / Mathf.Max(1, CarrierCount);
            float multiplier = Mathf.Clamp(1f - share * .015f, .46f, .96f);
            if (!HasRequiredCarriers)
                multiplier *= .58f;
            return Mathf.Clamp(multiplier, .32f, 1f);
        }

        public float GetStaminaDrainMultiplier(ulong clientId)
        {
            if (!IsHeldBy(clientId))
                return 1f;

            float share = weight / Mathf.Max(1, CarrierCount);
            float multiplier = 1f + share * .055f;
            if (!HasRequiredCarriers)
                multiplier += .6f;
            return Mathf.Clamp(multiplier, 1f, 3.2f);
        }

        public void SetMapSpawnServer(Vector3 position)
        {
            if (NetworkManager != null && !NetworkManager.IsServer)
                return;

            transform.position = position;
            transform.rotation = Quaternion.identity;

            if (body != null)
            {
                body.position = position;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (IsSpawned && IsServer)
            {
                syncedPosition.Value = position;
                syncedRotation.Value = Quaternion.identity;
                serverSpawnGraceUntil = Time.time + .75f;
            }
        }

        public bool MarkDeliveredServer()
        {
            if (!IsServer || Delivered || Broken)
                return false;

            delivered.Value = true;
            ClearCarriersServer();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            ApplyVisibilityState();
            return true;
        }

        public void ApplyDamageServer(float amount)
        {
            if (!IsServer || Delivered || Broken || amount <= 0f)
                return;

            durability.Value = Mathf.Max(0f, durability.Value - amount);
            if (durability.Value <= 0f)
                BreakServer();
        }

        private void BreakServer()
        {
            if (!IsServer || Broken || Delivered)
                return;

            broken.Value = true;
            ClearCarriersServer();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            ApplyVisibilityState();
            SpawnBreakFragments();

            if (GameManager.Instance != null)
                GameManager.Instance.ReportLootBrokenServer(this);
        }

        private void ClearCarriersServer()
        {
            carrierA.Value = NoCarrier;
            carrierB.Value = NoCarrier;
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

        private void OnDeliveredChanged(bool previousValue, bool newValue)
        {
            ApplyVisibilityState();
        }

        private void OnBrokenChanged(bool previousValue, bool newValue)
        {
            ApplyVisibilityState();
            if (newValue)
                SpawnBreakFragments();
        }

        private void ApplyVisibilityState()
        {
            bool visible = !Delivered && !Broken;

            if (renderers != null)
            {
                foreach (Renderer itemRenderer in renderers)
                    itemRenderer.enabled = visible;
            }

            if (colliders != null)
            {
                foreach (Collider itemCollider in colliders)
                    itemCollider.enabled = visible;
            }
        }

        private void SpawnBreakFragments()
        {
            if (fragmentsSpawned)
                return;

            fragmentsSpawned = true;
            int seed = unchecked((int)(NetworkObjectId ^ (ulong)baseValue ^ (ulong)Mathf.RoundToInt(weight * 100f)));
            var random = new System.Random(seed);
            Vector3 baseScale = transform.lossyScale;

            for (int i = 0; i < 7; i++)
            {
                GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fragment.name = $"{DisplayName} Fragment";
                fragment.transform.position = transform.position + RandomVector(random, .28f);
                fragment.transform.localScale = new Vector3(
                    Mathf.Max(.08f, baseScale.x * RandomRange(random, .12f, .28f)),
                    Mathf.Max(.08f, baseScale.y * RandomRange(random, .12f, .28f)),
                    Mathf.Max(.08f, baseScale.z * RandomRange(random, .12f, .28f)));

                Collider fragmentCollider = fragment.GetComponent<Collider>();
                if (fragmentCollider != null)
                    Destroy(fragmentCollider);

                Renderer fragmentRenderer = fragment.GetComponent<Renderer>();
                if (fragmentRenderer != null && fragmentMaterial != null)
                    fragmentRenderer.sharedMaterial = fragmentMaterial;

                var visual = fragment.AddComponent<LootFragment>();
                visual.Configure(
                    RandomVector(random, 3.5f) + Vector3.up * RandomRange(random, 1.5f, 4f),
                    RandomVector(random, 220f),
                    RandomRange(random, 1.4f, 2.5f));
            }
        }

        private static Vector3 RandomVector(System.Random random, float magnitude)
        {
            return new Vector3(
                RandomRange(random, -magnitude, magnitude),
                RandomRange(random, -magnitude, magnitude),
                RandomRange(random, -magnitude, magnitude));
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
