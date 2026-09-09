using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public sealed class SignalCore : NetworkBehaviour
    {
        public const ulong NoHolder = ulong.MaxValue;

        [SerializeField, Min(.03f)] private float syncInterval = .06f;
        [SerializeField, Min(1f)] private float remoteLerpSpeed = 18f;

        public bool Delivered => delivered.Value;
        public ulong HeldByClientId => heldByClientId.Value;

        private readonly NetworkVariable<Vector3> syncedPosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> syncedRotation = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> heldByClientId = new(
            NoHolder,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> delivered = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody body;
        private Renderer[] renderers;
        private Collider[] colliders;
        private float syncTimer;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);

            // Keep scene cores from falling while the LAN lobby is waiting for a host.
            body.isKinematic = true;
        }

        public override void OnNetworkSpawn()
        {
            delivered.OnValueChanged += OnDeliveredChanged;

            if (IsServer)
            {
                syncedPosition.Value = transform.position;
                syncedRotation.Value = transform.rotation;
                heldByClientId.Value = NoHolder;
                delivered.Value = false;
                body.isKinematic = false;
                body.useGravity = true;
            }
            else
            {
                body.isKinematic = true;
            }

            ApplyDeliveredState(delivered.Value);
        }

        public override void OnNetworkDespawn()
        {
            delivered.OnValueChanged -= OnDeliveredChanged;
        }

        private void Update()
        {
            if (!IsSpawned || IsServer || delivered.Value)
                return;

            float t = 1f - Mathf.Exp(-remoteLerpSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, syncedPosition.Value, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, syncedRotation.Value, t);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || delivered.Value)
                return;

            if (heldByClientId.Value != NoHolder)
            {
                PlayerController holder = GetPlayer(heldByClientId.Value);
                if (holder == null)
                {
                    ReleaseServer(heldByClientId.Value, Vector3.zero);
                }
                else
                {
                    Vector3 target = holder.GetServerHoldPosition();
                    Vector3 delta = target - body.position;
                    body.linearVelocity = Vector3.Lerp(body.linearVelocity, delta * 12f, .48f);
                    body.angularVelocity *= .8f;

                    if (delta.sqrMagnitude > 49f)
                        ReleaseServer(heldByClientId.Value, Vector3.zero);
                }
            }

            syncTimer -= Time.fixedDeltaTime;
            if (syncTimer > 0f)
                return;

            syncTimer = syncInterval;
            syncedPosition.Value = body.position;
            syncedRotation.Value = body.rotation;
        }

        public bool IsHeldBy(ulong clientId)
        {
            return heldByClientId.Value == clientId;
        }

        public bool TryGrabServer(ulong clientId)
        {
            if (!IsServer || delivered.Value || heldByClientId.Value != NoHolder)
                return false;

            PlayerController player = GetPlayer(clientId);
            if (player == null)
                return false;

            if (Vector3.Distance(body.position, player.GetServerHoldPosition()) > 4.5f)
                return false;

            heldByClientId.Value = clientId;
            body.isKinematic = false;
            body.useGravity = false;
            body.linearDamping = 5f;
            body.angularDamping = 2f;
            return true;
        }

        public void ReleaseServer(ulong clientId, Vector3 throwVelocity)
        {
            if (!IsServer || delivered.Value || heldByClientId.Value != clientId)
                return;

            heldByClientId.Value = NoHolder;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearDamping = .35f;
            body.angularDamping = .2f;
            body.linearVelocity = Vector3.ClampMagnitude(throwVelocity, 18f);
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
            }
        }

        public void MarkDeliveredServer()
        {
            if (!IsServer || delivered.Value)
                return;

            delivered.Value = true;
            heldByClientId.Value = NoHolder;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            ApplyDeliveredState(true);
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
            ApplyDeliveredState(newValue);
        }

        private void ApplyDeliveredState(bool isDelivered)
        {
            if (renderers != null)
            {
                foreach (Renderer coreRenderer in renderers)
                    coreRenderer.enabled = !isDelivered;
            }

            if (colliders != null)
            {
                foreach (Collider coreCollider in colliders)
                    coreCollider.enabled = !isDelivered;
            }
        }
    }
}
