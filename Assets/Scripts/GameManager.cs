using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalHaul
{
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
}
