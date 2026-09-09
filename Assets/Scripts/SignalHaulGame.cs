using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalHaul
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            var root = new GameObject("SIGNAL_HAUL");
            root.AddComponent<GameManager>();
        }
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public PlayerController Player { get; private set; }
        public int Delivered { get; private set; }
        public int TotalCores { get; private set; } = 3;
        public float TimeLeft { get; private set; } = 240f;
        public bool Ended { get; private set; }
        public bool Won { get; private set; }

        private readonly List<Material> materials = new();

        private void Awake()
        {
            Instance = this;
            BuildWorld();
        }

        private void Update()
        {
            if (!Ended)
            {
                TimeLeft = Mathf.Max(0, TimeLeft - Time.deltaTime);
                if (TimeLeft <= 0) EndGame(false);
                if (Player != null && Player.transform.position.y < -18f)
                    Player.RespawnWithDamage(25f);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                var scene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(scene.name);
            }
        }

        private Material Mat(Color color, float metallic = 0f, float smoothness = .25f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            materials.Add(mat);
            return mat;
        }

        private GameObject Cube(string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private void BuildWorld()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.06f, .09f, .12f);
            RenderSettings.fogDensity = .018f;
            RenderSettings.ambientLight = new Color(.18f, .22f, .28f);

            var steel = Mat(new Color(.16f, .2f, .23f), .65f, .35f);
            var deck = Mat(new Color(.25f, .29f, .3f), .45f, .3f);
            var hazard = Mat(new Color(.95f, .55f, .08f), .15f, .3f);
            var cyan = Mat(new Color(.05f, .8f, 1f), .25f, .65f);
            var red = Mat(new Color(.85f, .08f, .08f), .25f, .4f);
            var dark = Mat(new Color(.035f, .045f, .06f), .1f, .15f);

            var lightGO = new GameObject("Storm Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(.65f, .75f, .95f);
            lightGO.transform.rotation = Quaternion.Euler(52, -28, 0);

            Cube("Ground", new Vector3(0, -1, 0), new Vector3(42, 2, 42), dark);
            Cube("BaseDeck", new Vector3(0, .2f, 0), new Vector3(13, .4f, 13), deck);

            var spawn = new Vector3(-4.5f, 1.15f, -4.5f);
            CreatePlayer(spawn);
            CreateExtraction(new Vector3(4.5f, .55f, -4.5f), cyan);

            const int levels = 10;
            for (int i = 0; i < levels; i++)
            {
                float y = 2.2f + i * 3.1f;
                float x = (i % 2 == 0 ? -2.8f : 2.8f);
                float z = ((i / 2) % 2 == 0 ? 1.8f : -1.8f);
                Cube($"Deck_{i}", new Vector3(x, y, z), new Vector3(6.2f, .35f, 5.3f), deck);

                float bridgeX = i % 2 == 0 ? 1.1f : -1.1f;
                Cube($"Bridge_{i}", new Vector3(bridgeX, y + 1.45f, z * .45f), new Vector3(2.1f, .28f, 2.8f), hazard);

                var wall = Cube($"ClimbWall_{i}", new Vector3(0, y + 1.5f, z), new Vector3(.45f, 3f, 4.6f), steel);
                wall.tag = "Untagged";

                if (i % 2 == 0)
                {
                    Cube($"RailA_{i}", new Vector3(x - 3f, y + .65f, z), new Vector3(.15f, 1.3f, 5.4f), steel);
                    Cube($"RailB_{i}", new Vector3(x + 3f, y + .65f, z), new Vector3(.15f, 1.3f, 5.4f), steel);
                }
            }

            CreateCore(new Vector3(-2.8f, 15.2f, 1.8f), cyan, "CORE-ALPHA");
            CreateCore(new Vector3(2.8f, 24.5f, -1.8f), cyan, "CORE-BETA");
            CreateCore(new Vector3(-2.8f, 33.8f, 1.8f), cyan, "CORE-GAMMA");

            CreateDrone(new Vector3(0, 11, 0), red, 4.5f);
            CreateDrone(new Vector3(0, 21, 0), red, 5.8f);
            CreateDrone(new Vector3(0, 31, 0), red, 6.5f);
        }

        private void CreatePlayer(Vector3 position)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = .35f;
            controller.center = new Vector3(0, .9f, 0);
            Player = go.AddComponent<PlayerController>();
            Player.SpawnPoint = position;
        }

        private void CreateExtraction(Vector3 position, Material mat)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Extraction Pad";
            pad.transform.position = position;
            pad.transform.localScale = new Vector3(2.8f, .15f, 2.8f);
            pad.GetComponent<Renderer>().material = mat;
            var collider = pad.GetComponent<Collider>();
            collider.isTrigger = true;
            var rb = pad.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            pad.AddComponent<ExtractionPad>();
        }

        private void CreateCore(Vector3 position, Material mat, string label)
        {
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = label;
            core.transform.position = position;
            core.transform.localScale = Vector3.one * .75f;
            core.GetComponent<Renderer>().material = mat;
            var rb = core.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.linearDamping = .35f;
            rb.angularDamping = .2f;
            core.AddComponent<SignalCore>();
        }

        private void CreateDrone(Vector3 position, Material mat, float radius)
        {
            var drone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drone.name = "Storm Drone";
            drone.transform.position = position;
            drone.transform.localScale = new Vector3(1.25f, .55f, 1.25f);
            drone.GetComponent<Renderer>().material = mat;
            var col = drone.GetComponent<Collider>();
            col.isTrigger = true;
            var rb = drone.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var ai = drone.AddComponent<DroneHazard>();
            ai.OrbitRadius = radius;
        }

        public void DeliverCore(SignalCore core)
        {
            if (Ended || core.Delivered) return;
            core.Delivered = true;
            Delivered++;
            Destroy(core.gameObject);
            if (Delivered >= TotalCores) EndGame(true);
        }

        public void EndGame(bool win)
        {
            if (Ended) return;
            Ended = true;
            Won = win;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(18, 18, 330, 146), "");
            GUI.Label(new Rect(32, 28, 290, 28), "SIGNAL HAUL", style);
            GUI.Label(new Rect(32, 62, 290, 24), $"CORE  {Delivered}/{TotalCores}");
            GUI.Label(new Rect(32, 88, 290, 24), $"STORM  {Mathf.CeilToInt(TimeLeft)}s");
            if (Player != null)
            {
                GUI.Label(new Rect(32, 114, 290, 24), $"HP  {Mathf.CeilToInt(Player.Health)}   STAMINA  {Mathf.CeilToInt(Player.Stamina)}");
            }
            GUI.Label(new Rect(18, Screen.height - 48, 760, 30), "WASD 이동  |  SHIFT 달리기  |  SPACE 점프/벽등반  |  E 잡기/놓기  |  Q 던지기");

            if (Player != null && !Ended)
            {
                var prompt = Player.ContextPrompt;
                if (!string.IsNullOrEmpty(prompt))
                {
                    var centered = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
                    GUI.Label(new Rect(Screen.width * .5f - 220, Screen.height * .72f, 440, 32), prompt, centered);
                }
                GUI.Label(new Rect(Screen.width / 2 - 8, Screen.height / 2 - 15, 20, 30), "+", style);
            }

            if (Ended)
            {
                var big = new GUIStyle(style) { fontSize = 42, alignment = TextAnchor.MiddleCenter };
                var small = new GUIStyle(style) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                GUI.Box(new Rect(Screen.width / 2 - 260, Screen.height / 2 - 115, 520, 230), "");
                GUI.Label(new Rect(Screen.width / 2 - 240, Screen.height / 2 - 70, 480, 65), Won ? "SIGNAL RESTORED" : "STORM CLAIMED YOU", big);
                GUI.Label(new Rect(Screen.width / 2 - 240, Screen.height / 2 + 12, 480, 40), "R 키로 다시 시작", small);
            }
        }
    }

    public class PlayerController : MonoBehaviour
    {
        public Vector3 SpawnPoint { get; set; }
        public float Health { get; private set; } = 100f;
        public float Stamina { get; private set; } = 100f;
        public string ContextPrompt { get; private set; }

        private CharacterController controller;
        private Camera cam;
        private Transform holdPoint;
        private Rigidbody heldBody;
        private float pitch;
        private float verticalVelocity;
        private float fallPeak;
        private float damageCooldown;

        private void Start()
        {
            controller = GetComponent<CharacterController>();
            var camGO = new GameObject("Camera");
            camGO.transform.SetParent(transform);
            camGO.transform.localPosition = new Vector3(0, 1.55f, 0);
            cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 78f;
            camGO.AddComponent<AudioListener>();

            holdPoint = new GameObject("HoldPoint").transform;
            holdPoint.SetParent(cam.transform);
            holdPoint.localPosition = new Vector3(0, -.08f, 2.35f);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (GameManager.Instance.Ended) return;
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
            if (heldBody == null) return;
            Vector3 delta = holdPoint.position - heldBody.position;
            heldBody.linearVelocity = Vector3.Lerp(heldBody.linearVelocity, delta * 11f, .42f);
            heldBody.angularVelocity *= .82f;
            if (delta.magnitude > 5f) Drop();
        }

        private void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            float mx = Input.GetAxis("Mouse X") * 2.2f;
            float my = Input.GetAxis("Mouse Y") * 2.2f;
            transform.Rotate(Vector3.up * mx);
            pitch = Mathf.Clamp(pitch - my, -85f, 85f);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        private void Move()
        {
            bool grounded = controller.isGrounded;
            if (grounded && verticalVelocity < 0)
            {
                if (fallPeak < -16f) TakeDamage(Mathf.Clamp((-fallPeak - 15f) * 3.2f, 5f, 55f));
                fallPeak = 0;
                verticalVelocity = -2f;
            }

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            bool sprint = Input.GetKey(KeyCode.LeftShift) && Stamina > 1f && input.y > .1f;
            float speed = sprint ? 7.2f : 4.6f;
            if (sprint) Stamina = Mathf.Max(0, Stamina - 22f * Time.deltaTime);
            else Stamina = Mathf.Min(100f, Stamina + (grounded ? 18f : 8f) * Time.deltaTime);

            bool climb = false;
            if (!grounded && Input.GetKey(KeyCode.Space) && Stamina > 0f)
            {
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 1.25f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
                {
                    climb = true;
                    verticalVelocity = 3.5f;
                    Stamina = Mathf.Max(0, Stamina - 28f * Time.deltaTime);
                }
            }

            if (grounded && Input.GetKeyDown(KeyCode.Space) && Stamina > 8f)
            {
                verticalVelocity = 7.2f;
                Stamina -= 8f;
            }

            if (!climb) verticalVelocity += Physics.gravity.y * 2f * Time.deltaTime;
            fallPeak = Mathf.Min(fallPeak, verticalVelocity);

            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;
            Vector3 motion = horizontal + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        private void HandleGrabInput()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (heldBody != null) Drop();
                else TryGrab();
            }
            if (Input.GetKeyDown(KeyCode.Q) && heldBody != null)
            {
                var body = heldBody;
                Drop();
                body.AddForce(cam.transform.forward * 12f + Vector3.up * 2f, ForceMode.VelocityChange);
            }
        }

        private void TryGrab()
        {
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 3.3f, ~0, QueryTriggerInteraction.Ignore)) return;
            var body = hit.rigidbody;
            if (body == null || body.isKinematic || body.GetComponent<SignalCore>() == null) return;
            heldBody = body;
            heldBody.useGravity = false;
            heldBody.linearDamping = 5f;
        }

        public void Drop()
        {
            if (heldBody == null) return;
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
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 3.3f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != null && hit.rigidbody.GetComponent<SignalCore>() != null)
                ContextPrompt = "E SIGNAL CORE 잡기";
            else
                ContextPrompt = string.Empty;
        }

        public void TakeDamage(float amount)
        {
            if (damageCooldown > 0f || GameManager.Instance.Ended) return;
            damageCooldown = .55f;
            Health -= amount;
            Drop();
            if (Health <= 0) GameManager.Instance.EndGame(false);
        }

        public void RespawnWithDamage(float damage)
        {
            controller.enabled = false;
            transform.position = SpawnPoint;
            controller.enabled = true;
            verticalVelocity = 0;
            TakeDamage(damage);
        }
    }

    public class SignalCore : MonoBehaviour
    {
        public bool Delivered { get; set; }
    }

    public class ExtractionPad : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            var core = other.GetComponent<SignalCore>();
            if (core != null) GameManager.Instance.DeliverCore(core);
        }
    }

    public class DroneHazard : MonoBehaviour
    {
        public float OrbitRadius { get; set; } = 5f;
        private Vector3 center;
        private float phase;
        private float hitCooldown;

        private void Start()
        {
            center = transform.position;
            phase = Random.Range(0f, 6f);
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.Player == null) return;
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
                target = center + new Vector3(Mathf.Cos(t) * OrbitRadius, Mathf.Sin(t * 1.7f) * .8f, Mathf.Sin(t) * OrbitRadius);
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
