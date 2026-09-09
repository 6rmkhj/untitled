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
        public PhysicsLoot HeldItem => heldItem;

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
        private PhysicsLoot heldItem;
        private Vector3 spawnPoint;
        private Vector3 externalVelocity;
        private Vector3 serverLastSubmittedPosition;
        private float serverLastSubmittedTime;
        private float serverNextMovementNoiseAt;
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
            controller.enabled = false;

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
                serverLastSubmittedPosition = transform.position;
                serverLastSubmittedTime = Time.time;
                serverNextMovementNoiseAt = Time.time + .35f;
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
            controller.enabled = false;
            if (playerCamera != null)
                playerCamera.enabled = false;
            if (audioListener != null)
                audioListener.enabled = false;

            if (LocalPlayer != this)
                return;

            LocalPlayer = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
            UpdateHeldItemState();
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

            float elapsed = Mathf.Max(.02f, Time.time - serverLastSubmittedTime);
            float speed = Vector3.Distance(position, serverLastSubmittedPosition) / elapsed;

            if (Time.time >= serverNextMovementNoiseAt && speed > 1.25f)
            {
                float radius = speed > 5.4f ? 11f : 5.5f;
                MonsterNoiseSystem.EmitServer(position, radius, OwnerClientId, MonsterNoiseKind.Movement);
                serverNextMovementNoiseAt = Time.time + (speed > 5.4f ? .28f : .5f);
            }

            serverLastSubmittedPosition = position;
            serverLastSubmittedTime = Time.time;
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

            float carrySpeed = heldItem != null ? heldItem.GetCarrySpeedMultiplier(OwnerClientId) : 1f;
            float carryStamina = heldItem != null ? heldItem.GetStaminaDrainMultiplier(OwnerClientId) : 1f;
            bool enoughCarriers = heldItem == null || heldItem.HasRequiredCarriers;

            bool sprint = Input.GetKey(KeyCode.LeftShift) && Stamina > 1f && input.y > .1f && enoughCarriers;
            float speed = (sprint ? 7.2f : 4.6f) * carrySpeed;

            if (sprint)
            {
                Stamina = Mathf.Max(0f, Stamina - 22f * carryStamina * Time.deltaTime);
            }
            else
            {
                float regen = (grounded ? 18f : 8f) / Mathf.Sqrt(carryStamina);
                Stamina = Mathf.Min(100f, Stamina + regen * Time.deltaTime);
            }

            bool climb = false;
            bool canClimbWithLoad = heldItem == null || heldItem.CanClimb(OwnerClientId);
            if (!grounded && canClimbWithLoad && Input.GetKey(KeyCode.Space) && Stamina > 0f)
            {
                if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, 1.25f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
                {
                    climb = true;
                    verticalVelocity = 3.5f;
                    Stamina = Mathf.Max(0f, Stamina - 28f * carryStamina * Time.deltaTime);
                }
            }

            if (grounded && enoughCarriers && Input.GetKeyDown(KeyCode.Space) && Stamina > 8f * carryStamina)
            {
                verticalVelocity = 7.2f;
                Stamina -= 8f * carryStamina;
            }

            if (!climb)
                verticalVelocity += Physics.gravity.y * 2f * Time.deltaTime;

            fallPeak = Mathf.Min(fallPeak, verticalVelocity);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;
            controller.Move((horizontal + externalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, 9f * Time.deltaTime);
        }

        private void HandleGrabInput()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (heldItem != null)
                    ReleaseHeldItem(Vector3.zero);
                else
                    TryGrab();
            }

            if (Input.GetKeyDown(KeyCode.Q) && heldItem != null)
            {
                Vector3 throwVelocity = heldItem.CanThrow(OwnerClientId)
                    ? playerCamera.transform.forward * 12f + Vector3.up * 2f
                    : Vector3.zero;
                ReleaseHeldItem(throwVelocity);
            }
        }

        private void TryGrab()
        {
            if (!Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, 3.8f, ~0, QueryTriggerInteraction.Ignore))
                return;

            PhysicsLoot item = hit.collider.GetComponentInParent<PhysicsLoot>();
            if (item == null || !item.IsSpawned || item.Delivered || item.Broken)
                return;

            heldItem = item;
            pendingGrabUntil = Time.time + .5f;
            RequestGrabRpc(item.NetworkObjectId);
        }

        private void UpdateHeldItemState()
        {
            if (heldItem == null)
                return;

            if (heldItem.Delivered || heldItem.Broken)
            {
                heldItem = null;
                return;
            }

            if (!heldItem.IsHeldBy(OwnerClientId) && Time.time > pendingGrabUntil)
                heldItem = null;
        }

        private void ReleaseHeldItem(Vector3 throwVelocity)
        {
            if (heldItem == null)
                return;

            ulong id = heldItem.NetworkObjectId;
            heldItem = null;
            RequestReleaseRpc(id, throwVelocity);
        }

        public void Drop()
        {
            if (IsOwner)
                ReleaseHeldItem(Vector3.zero);
        }

        [Rpc(SendTo.Server)]
        private void RequestGrabRpc(ulong itemNetworkObjectId, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkObjectId, out NetworkObject networkObject) &&
                networkObject.TryGetComponent(out PhysicsLoot item))
            {
                item.TryGrabServer(OwnerClientId);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestReleaseRpc(ulong itemNetworkObjectId, Vector3 throwVelocity, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkObjectId, out NetworkObject networkObject) &&
                networkObject.TryGetComponent(out PhysicsLoot item))
            {
                item.ReleaseServer(OwnerClientId, throwVelocity);
            }
        }

        private void UpdatePrompt()
        {
            if (heldItem != null)
            {
                string carryState = heldItem.HasRequiredCarriers
                    ? $"운반 {heldItem.CarrierCount}/{heldItem.RequiredCarriers}명"
                    : $"도움 필요 {heldItem.CarrierCount}/{heldItem.RequiredCarriers}명";
                string action = heldItem.CanThrow(OwnerClientId) ? "E 놓기 / Q 던지기" : "E 또는 Q 놓기";
                ContextPrompt = $"{heldItem.DisplayName}  {heldItem.Weight:0.#}kg  ${heldItem.CurrentValue}  내구도 {Mathf.CeilToInt(heldItem.Durability)}/{Mathf.CeilToInt(heldItem.MaxDurability)}\n{carryState}  |  {action}";
                return;
            }

            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, 3.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                PhysicsLoot item = hit.collider.GetComponentInParent<PhysicsLoot>();
                if (item != null && !item.Delivered && !item.Broken)
                {
                    string crew = item.RequiredCarriers > 1 ? $"  {item.CarrierCount}/{item.RequiredCarriers}명 필요" : string.Empty;
                    string full = item.CarrierCount >= item.MaxCarriers ? "  [운반 인원 가득 참]" : string.Empty;
                    ContextPrompt = $"E {item.DisplayName} 잡기  |  {item.Weight:0.#}kg  ${item.CurrentValue}  내구도 {Mathf.CeilToInt(item.Durability)}/{Mathf.CeilToInt(item.MaxDurability)}{crew}{full}";
                    return;
                }
            }

            ContextPrompt = string.Empty;
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
            ReleaseAnyHeldItemServer();

            if (health.Value <= 0f)
                GameManager.Instance.EndGameServer(false);
        }

        public void ApplyMonsterHitServer(float damage, Vector3 knockback)
        {
            if (!IsServer || !IsSpawned)
                return;

            ApplyDamageServer(Mathf.Clamp(damage, 0f, 60f));
            ReceiveMonsterHitRpc(Vector3.ClampMagnitude(knockback, 16f));
        }

        [Rpc(SendTo.Owner)]
        private void ReceiveMonsterHitRpc(Vector3 knockback)
        {
            heldItem = null;
            externalVelocity += new Vector3(knockback.x, 0f, knockback.z);
            verticalVelocity = Mathf.Max(verticalVelocity, knockback.y);
        }

        public void RespawnWithDamage(float damage)
        {
            if (!IsOwner || respawnCooldown > 0f)
                return;

            respawnCooldown = 1f;
            ReleaseHeldItem(Vector3.zero);
            controller.enabled = false;
            transform.position = spawnPoint;
            controller.enabled = true;
            verticalVelocity = 0f;
            fallPeak = 0f;
            externalVelocity = Vector3.zero;
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

        private void ReleaseAnyHeldItemServer()
        {
            if (!IsServer || NetworkManager == null)
                return;

            foreach (NetworkObject spawnedObject in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (spawnedObject.TryGetComponent(out PhysicsLoot item) && item.IsHeldBy(OwnerClientId))
                    item.ReleaseServer(OwnerClientId, Vector3.zero);
            }
        }

        public Vector3 GetServerHoldPosition()
        {
            Quaternion viewRotation = Quaternion.Euler(syncedPitch.Value, syncedRotation.Value.eulerAngles.y, 0f);
            return syncedPosition.Value + Vector3.up * 1.45f + viewRotation * Vector3.forward * 2.35f;
        }
    }
}
