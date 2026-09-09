using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private Vector3 spawnPoint;

        public float Health { get; private set; } = 100f;
        public float Stamina { get; private set; } = 100f;
        public string ContextPrompt { get; private set; }

        private CharacterController controller;
        private Rigidbody heldBody;
        private float pitch;
        private float verticalVelocity;
        private float fallPeak;
        private float damageCooldown;

        public void Configure(Camera cameraReference, Transform holdPointReference, Vector3 initialSpawnPoint)
        {
            playerCamera = cameraReference;
            holdPoint = holdPointReference;
            spawnPoint = initialSpawnPoint;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>(true);

            if (holdPoint == null && playerCamera != null)
                holdPoint = playerCamera.transform.Find("HoldPoint");
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.Ended || playerCamera == null)
                return;

            damageCooldown -= Time.deltaTime;
            Look();
            Move();
            HandleGrabInput();
            UpdatePrompt();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }
        }

        private void FixedUpdate()
        {
            if (heldBody == null || holdPoint == null)
                return;

            Vector3 delta = holdPoint.position - heldBody.position;
            heldBody.linearVelocity = Vector3.Lerp(heldBody.linearVelocity, delta * 11f, .42f);
            heldBody.angularVelocity *= .82f;

            if (delta.magnitude > 5f)
                Drop();
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
                    TakeDamage(Mathf.Clamp((-fallPeak - 15f) * 3.2f, 5f, 55f));

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
                if (heldBody != null)
                    Drop();
                else
                    TryGrab();
            }

            if (Input.GetKeyDown(KeyCode.Q) && heldBody != null)
            {
                var body = heldBody;
                Drop();
                body.AddForce(playerCamera.transform.forward * 12f + Vector3.up * 2f, ForceMode.VelocityChange);
            }
        }

        private void TryGrab()
        {
            if (!Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, 3.3f, ~0, QueryTriggerInteraction.Ignore))
                return;

            var body = hit.rigidbody;
            if (body == null || body.isKinematic || body.GetComponent<SignalCore>() == null)
                return;

            heldBody = body;
            heldBody.useGravity = false;
            heldBody.linearDamping = 5f;
        }

        public void Drop()
        {
            if (heldBody == null)
                return;

            heldBody.useGravity = true;
            heldBody.linearDamping = .35f;
            heldBody = null;
        }

        private void UpdatePrompt()
        {
            if (heldBody != null)
            {
                ContextPrompt = "E 놓기  /  Q 던지기";
                return;
            }

            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, 3.3f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.rigidbody != null && hit.rigidbody.GetComponent<SignalCore>() != null)
            {
                ContextPrompt = "E SIGNAL CORE 잡기";
            }
            else
            {
                ContextPrompt = string.Empty;
            }
        }

        public void TakeDamage(float amount)
        {
            if (damageCooldown > 0f || GameManager.Instance == null || GameManager.Instance.Ended)
                return;

            damageCooldown = .55f;
            Health -= amount;
            Drop();

            if (Health <= 0f)
                GameManager.Instance.EndGame(false);
        }

        public void RespawnWithDamage(float damage)
        {
            Drop();
            controller.enabled = false;
            transform.position = spawnPoint;
            controller.enabled = true;
            verticalVelocity = 0f;
            fallPeak = 0f;
            TakeDamage(damage);
        }
    }
}
