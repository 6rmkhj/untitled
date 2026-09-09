using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(CharacterController), typeof(NetworkObject))]
    public sealed class PlayerController : NetworkBehaviour
    {
        public static PlayerController LocalPlayer { get; private set; }

        [Header("Scene References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform holdPoint;

        [Header("Network")]
        [SerializeField, Min(.03f)] private float syncInterval = .08f;
        [SerializeField, Min(1f)] private float remoteLerpSpeed = 16f;

        public float Health => health.Value;
        public float Stamina { get; private set; } = 100f;
        public string ContextPrompt { get; private set; } = string.Empty;
        public float SyncedPitch => syncedPitch.Value;

        private readonly NetworkVariable<Vector3> syncedPosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> syncedRotation = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> syncedPitch = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> health = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController controller;
        private AudioListener audioListener;
        private Renderer[] bodyRenderers;
        private SignalCore heldCore;
        private Vector3 spawnPoint;
        private float pitch;
        private float verticalVelocity;
        private float fallPeak;
        private float syncTimer;
        private float pendingGrabUntil;
        private float respawnCooldown;

        public void Configure(Camera cameraReference, Transform holdPointReference)
        {
            playerCamera = cameraReference;
            holdPoint = holdPointReference;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>(true);

            if (holdPoint == null && playerCamera != null)
                holdPoint = playerCamera.transform.Find("HoldPoint");

            if (playerCamera != null)
                audioListener = playerCamera.GetComponent<AudioListener>();

            bodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        public override void OnNetworkSpawn()
        {
            spawnPoint = transform.position;

            if (IsServer)
            {
                syncedPosition.Value = transform.position;
                syncedRotation.Value = transform.rotation;
                syncedPitch.Value = 0f;
                health.Value = 100f;
            }

            controller.enabled = IsOwner;

            if (playerCamera != null)
                playerCamera.enabled = IsOwner;
            if (audioListener != null)
                audioListener.enabled = IsOwner;

            if (bodyRenderers != null)
            {
                foreach (Renderer bodyRenderer in bodyRenderers)
                    bodyRenderer.enabled = !IsOwner;
            }

            if (!IsOwner)
                return;

            LocalPlayer = this;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnNetworkDespawn()
        {
            if (LocalPlayer == this)
                LocalPlayer = null;
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (!IsOwner)
            {
                UpdateRemoteTransform();
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.Ended || playerCamera == null)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            respawnCooldown -= Time.deltaTime;
            UpdateHeldCoreState();
            Look();
            Move();
            HandleGrabInput();
            UpdatePrompt();
            SendTransformState();

            if (transform.position.y < -18f && respawnCooldown <= 0f)
                RespawnWithDamage(25f);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }
        }

        private void UpdateRemoteTransform()
        {
            float t = 1f - Mathf.Exp(-remoteLerpSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, syncedPosition.Value, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, syncedRotation.Value, t);
        }

        private void SendTransformState()
        {
            syncTimer -= Time.deltaTime;
            if (syncTimer > 0f)
                return;

            syncTimer = syncInterval;
            SubmitTransformRpc(transform.position, transform.rotation, pitch);
        }

        [Rpc(SendTo.Server)]
        private void SubmitTransformRpc(Vector3 position, Quaternion rotation, float viewPitch, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            syncedPosition.Value = position;
            syncedRotation.Value = rotation;
            syncedPitch.Value = Mathf.Clamp(viewPitch, -85f, 85f);
        }

        private void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            float mx = Input.GetAxis("Mouse X") * 2.2f;
            float my = Input.GetAxis("Mouse Y") * 2.2f;
            transform.Rotate(Vector3.up * mx);
            pitch = Mathf.Clamp(pitch - my, -85f, 85f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Move()
        {
            bool grounded = controller.isGrounded;
            if (grounded && verticalVelocity < 0f)
            {
                if (fallPeak < -16f)
                    RequestDamage(Mathf.Clamp((-fallPeak - 15f) * 3.2f, 5f, 55f));

                fallPeak = 0f;
                verticalVelocity = -2f;
            }

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);

            bool sprint = Input.GetKey(KeyCode.LeftShift) && Stamina > 1f && input.y > .1f;
            float speed = sprint ? 7.2f : 4.6f;

            if (sprint)
                Stamina = Mathf.Max(0f, Stamina - 22f * Time.deltaTime);
            else
                Stamina = Mathf.Min(100f, Stamina + (grounded ? 18f : 8f) * Time.deltaTime);

            bool climb = false;
            if (!grounded && Input.GetKey(KeyCode.Space) && Stamina > 0f)
            {
                if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, 1.25f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
                {
                    climb = true;
                    verticalVelocity = 3.5f;
                    Stamina = Mathf.Max(0f, Stamina - 28f * Time.deltaTime);
                }
            }

            if (grounded && Input.GetKeyDown(KeyCode.Space) && Stamina > 8f)
            {
                verticalVelocity = 7.2f;
                Stamina -= 8f;
            }

            if (!climb)
                verticalVelocity += Physics.gravity.y * 2f * Time.deltaTime;

            fallPeak = Mathf.Min(fallPeak, verticalVelocity);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;
            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void HandleGrabInput()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (heldCore != null)
                    ReleaseHeldCore(Vector3.zero);
                else
                    TryGrab();
            }

            if (Input.GetKeyDown(KeyCode.Q) && heldCore != null)
            {
                Vector3 throwVelocity = playerCamera.transform.forward * 12f + Vector3.up * 2f;
                ReleaseHeldCore(throwVelocity);
            }
        }

        private void TryGrab()
        {
            if (!Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, 3.3f, ~0, QueryTriggerInteraction.Ignore))
                return;

            var core = hit.collider.GetComponentInParent<SignalCore>();
            if (core == null || !core.IsSpawned || core.Delivered)
                return;

            heldCore = core;
            pendingGrabUntil = Time.time + .4f;
            RequestGrabRpc(core.NetworkObjectId);
        }

        private void UpdateHeldCoreState()
        {
            if (heldCore == null)
                return;

            if (heldCore.Delivered)
            {
                heldCore = null;
                return;
            }

            if (!heldCore.IsHeldBy(OwnerClientId) && Time.time > pendingGrabUntil)
                heldCore = null;
        }

        private void ReleaseHeldCore(Vector3 throwVelocity)
        {
            if (heldCore == null)
                return;

            ulong id = heldCore.NetworkObjectId;
            heldCore = null;
            RequestReleaseRpc(id, throwVelocity);
        }

        public void Drop()
        {
            if (IsOwner)
                ReleaseHeldCore(Vector3.zero);
        }

        [Rpc(SendTo.Server)]
        private void RequestGrabRpc(ulong coreNetworkObjectId, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(coreNetworkObjectId, out NetworkObject networkObject) &&
                networkObject.TryGetComponent(out SignalCore core))
            {
                core.TryGrabServer(OwnerClientId);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestReleaseRpc(ulong coreNetworkObjectId, Vector3 throwVelocity, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(coreNetworkObjectId, out NetworkObject networkObject) &&
                networkObject.TryGetComponent(out SignalCore core))
            {
                core.ReleaseServer(OwnerClientId, throwVelocity);
            }
        }

        private void UpdatePrompt()
        {
            if (heldCore != null)
            {
                ContextPrompt = "E 놓기  /  Q 던지기";
                return;
            }

            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, 3.3f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<SignalCore>() is SignalCore core && !core.Delivered)
            {
                ContextPrompt = "E SIGNAL CORE 잡기";
            }
            else
            {
                ContextPrompt = string.Empty;
            }
        }

        private void RequestDamage(float amount)
        {
            if (amount <= 0f)
                return;

            RequestDamageRpc(amount);
        }

        [Rpc(SendTo.Server)]
        private void RequestDamageRpc(float amount, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            ApplyDamageServer(Mathf.Clamp(amount, 0f, 60f));
        }

        public void ApplyDamageServer(float amount)
        {
            if (!IsServer || amount <= 0f || GameManager.Instance == null || GameManager.Instance.Ended)
                return;

            health.Value = Mathf.Max(0f, health.Value - amount);
            ReleaseAnyHeldCoreServer();

            if (health.Value <= 0f)
                GameManager.Instance.EndGameServer(false);
        }

        public void RespawnWithDamage(float damage)
        {
            if (!IsOwner || respawnCooldown > 0f)
                return;

            respawnCooldown = 1f;
            ReleaseHeldCore(Vector3.zero);
            controller.enabled = false;
            transform.position = spawnPoint;
            controller.enabled = true;
            verticalVelocity = 0f;
            fallPeak = 0f;
            RequestRespawnRpc(Mathf.Clamp(damage, 0f, 60f));
        }

        [Rpc(SendTo.Server)]
        private void RequestRespawnRpc(float damage, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            syncedPosition.Value = spawnPoint;
            syncedRotation.Value = transform.rotation;
            ApplyDamageServer(damage);
        }

        private void ReleaseAnyHeldCoreServer()
        {
            if (!IsServer || NetworkManager == null)
                return;

            foreach (NetworkObject spawnedObject in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (spawnedObject.TryGetComponent(out SignalCore core) && core.IsHeldBy(OwnerClientId))
                {
                    core.ReleaseServer(OwnerClientId, Vector3.zero);
                    return;
                }
            }
        }

        public Vector3 GetServerHoldPosition()
        {
            Quaternion viewRotation = Quaternion.Euler(syncedPitch.Value, syncedRotation.Value.eulerAngles.y, 0f);
            return syncedPosition.Value + Vector3.up * 1.45f + viewRotation * Vector3.forward * 2.35f;
        }
    }
}
