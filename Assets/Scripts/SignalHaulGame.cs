using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalHaul
{
    public sealed class SignalHaulSceneMarker : MonoBehaviour
    {
        public const int CurrentVersion = 3;

        [SerializeField, HideInInspector] private int sceneVersion = CurrentVersion;
        public int SceneVersion => sceneVersion;

        public void InitializeVersion()
        {
            sceneVersion = CurrentVersion;
        }
    }

    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private PlayerController player;

        [Header("Round Settings")]
        [SerializeField] private int totalCores = 3;
        [SerializeField] private float roundDuration = 240f;

        public PlayerController Player => player;
        public int Delivered { get; private set; }
        public int TotalCores => totalCores;
        public float TimeLeft { get; private set; }
        public bool Ended { get; private set; }
        public bool Won { get; private set; }

        public void Configure(PlayerController playerController, int coreCount, float duration)
        {
            player = playerController;
            totalCores = coreCount;
            roundDuration = duration;
        }

        private void Awake()
        {
            Instance = this;
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();

            if (totalCores <= 0)
                totalCores = FindObjectsByType<SignalCore>(FindObjectsSortMode.None).Length;

            TimeLeft = roundDuration;
        }

        private void Update()
        {
            if (!Ended)
            {
                TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
                if (TimeLeft <= 0f)
                    EndGame(false);

                if (player != null && player.transform.position.y < -18f)
                    player.RespawnWithDamage(25f);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        public void DeliverCore(SignalCore core)
        {
            if (Ended || core == null || core.Delivered)
                return;

            core.Delivered = true;
            Delivered++;
            Destroy(core.gameObject);

            if (Delivered >= totalCores)
                EndGame(true);
        }

        public void EndGame(bool win)
        {
            if (Ended)
                return;

            Ended = true;
            Won = win;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            GUI.Box(new Rect(18, 18, 330, 146), string.Empty);
            GUI.Label(new Rect(32, 28, 290, 28), "SIGNAL HAUL", style);
            GUI.Label(new Rect(32, 62, 290, 24), $"CORE  {Delivered}/{totalCores}");
            GUI.Label(new Rect(32, 88, 290, 24), $"STORM  {Mathf.CeilToInt(TimeLeft)}s");

            if (player != null)
                GUI.Label(new Rect(32, 114, 290, 24), $"HP  {Mathf.CeilToInt(player.Health)}   STAMINA  {Mathf.CeilToInt(player.Stamina)}");

            GUI.Label(new Rect(18, Screen.height - 48, 760, 30), "WASD 이동  |  SHIFT 달리기  |  SPACE 점프/벽등반  |  E 잡기/놓기  |  Q 던지기");

            if (player != null && !Ended)
            {
                if (!string.IsNullOrEmpty(player.ContextPrompt))
                {
                    var centered = new GUIStyle(style)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 18
                    };
                    GUI.Label(new Rect(Screen.width * .5f - 220, Screen.height * .72f, 440, 32), player.ContextPrompt, centered);
                }

                GUI.Label(new Rect(Screen.width / 2 - 8, Screen.height / 2 - 15, 20, 30), "+", style);
            }

            if (!Ended)
                return;

            var big = new GUIStyle(style) { fontSize = 42, alignment = TextAnchor.MiddleCenter };
            var small = new GUIStyle(style) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width / 2 - 260, Screen.height / 2 - 115, 520, 230), string.Empty);
            GUI.Label(new Rect(Screen.width / 2 - 240, Screen.height / 2 - 70, 480, 65), Won ? "SIGNAL RESTORED" : "STORM CLAIMED YOU", big);
            GUI.Label(new Rect(Screen.width / 2 - 240, Screen.height / 2 + 12, 480, 40), "R 키로 다시 시작", small);
        }
    }

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

    public sealed class SignalCore : MonoBehaviour
    {
        public bool Delivered { get; set; }
    }

    public sealed class ExtractionPad : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            var core = other.GetComponent<SignalCore>();
            if (core != null && GameManager.Instance != null)
                GameManager.Instance.DeliverCore(core);
        }
    }

    public sealed class DroneHazard : MonoBehaviour
    {
        [SerializeField] private float orbitRadius = 5f;

        private Vector3 center;
        private float phase;
        private float hitCooldown;

        public void Configure(float radius)
        {
            orbitRadius = radius;
        }

        private void Start()
        {
            center = transform.position;
            phase = Random.Range(0f, 6f);
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.Player == null)
                return;

            hitCooldown -= Time.deltaTime;
            var player = GameManager.Instance.Player.transform;
            float dist = Vector3.Distance(transform.position, player.position);
            Vector3 target;

            if (dist < 8f)
            {
                target = player.position + Vector3.up * 1.1f;
                transform.position = Vector3.MoveTowards(transform.position, target, 4.2f * Time.deltaTime);
            }
            else
            {
                float t = Time.time * .65f + phase;
                target = center + new Vector3(Mathf.Cos(t) * orbitRadius, Mathf.Sin(t * 1.7f) * .8f, Mathf.Sin(t) * orbitRadius);
                transform.position = Vector3.Lerp(transform.position, target, 2.2f * Time.deltaTime);
            }

            if (dist < 1.45f && hitCooldown <= 0f)
            {
                hitCooldown = 1.2f;
                GameManager.Instance.Player.TakeDamage(14f);
            }
        }
    }
}
