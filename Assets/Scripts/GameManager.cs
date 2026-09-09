using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Round Settings")]
        [SerializeField] private int totalCores = 3;
        [SerializeField] private float roundDuration = 240f;

        public int Delivered => delivered.Value;
        public int TotalCores => totalCores;
        public float TimeLeft => timeLeft.Value;
        public bool Ended => ended.Value;
        public bool Won => won.Value;

        private readonly NetworkVariable<int> delivered = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> timeLeft = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> ended = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> won = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float serverTimeLeft;
        private float timerSyncAccumulator;

        public void Configure(int coreCount, float duration)
        {
            totalCores = Mathf.Max(1, coreCount);
            roundDuration = Mathf.Max(30f, duration);
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            if (totalCores <= 0)
                totalCores = FindObjectsByType<SignalCore>(FindObjectsSortMode.None).Length;

            delivered.Value = 0;
            serverTimeLeft = roundDuration;
            timeLeft.Value = serverTimeLeft;
            ended.Value = false;
            won.Value = false;
            timerSyncAccumulator = 0f;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || ended.Value)
                return;

            serverTimeLeft = Mathf.Max(0f, serverTimeLeft - Time.deltaTime);
            timerSyncAccumulator += Time.deltaTime;

            if (timerSyncAccumulator >= .1f || serverTimeLeft <= 0f)
            {
                timeLeft.Value = serverTimeLeft;
                timerSyncAccumulator = 0f;
            }

            if (serverTimeLeft <= 0f)
                EndGameServer(false);
        }

        public void DeliverCoreServer(SignalCore core)
        {
            if (!IsServer || ended.Value || core == null || core.Delivered)
                return;

            core.MarkDeliveredServer();
            delivered.Value++;

            if (delivered.Value >= totalCores)
                EndGameServer(true);
        }

        public void EndGameServer(bool win)
        {
            if (!IsServer || ended.Value)
                return;

            ended.Value = true;
            won.Value = win;
        }

        private void OnGUI()
        {
            if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return;

            PlayerController localPlayer = PlayerController.LocalPlayer;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            GUI.Box(new Rect(18f, 18f, 330f, 146f), string.Empty);
            GUI.Label(new Rect(32f, 28f, 290f, 28f), "SIGNAL HAUL", style);
            GUI.Label(new Rect(32f, 62f, 290f, 24f), $"CORE  {Delivered}/{totalCores}");
            GUI.Label(new Rect(32f, 88f, 290f, 24f), $"STORM  {Mathf.CeilToInt(TimeLeft)}s");

            if (localPlayer != null)
                GUI.Label(new Rect(32f, 114f, 290f, 24f), $"HP  {Mathf.CeilToInt(localPlayer.Health)}   STAMINA  {Mathf.CeilToInt(localPlayer.Stamina)}");

            GUI.Label(new Rect(18f, Screen.height - 48f, 760f, 30f), "WASD 이동  |  SHIFT 달리기  |  SPACE 점프/벽등반  |  E 잡기/놓기  |  Q 던지기  |  ESC 커서");

            if (localPlayer != null && !Ended)
            {
                if (!string.IsNullOrEmpty(localPlayer.ContextPrompt))
                {
                    var centered = new GUIStyle(style)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 18
                    };
                    GUI.Label(new Rect(Screen.width * .5f - 220f, Screen.height * .72f, 440f, 32f), localPlayer.ContextPrompt, centered);
                }

                GUI.Label(new Rect(Screen.width / 2f - 8f, Screen.height / 2f - 15f, 20f, 30f), "+", style);
            }

            if (!Ended)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var big = new GUIStyle(style) { fontSize = 42, alignment = TextAnchor.MiddleCenter };
            var small = new GUIStyle(style) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width / 2f - 280f, Screen.height / 2f - 115f, 560f, 230f), string.Empty);
            GUI.Label(new Rect(Screen.width / 2f - 260f, Screen.height / 2f - 70f, 520f, 65f), Won ? "SIGNAL RESTORED" : "STORM CLAIMED THE CREW", big);
            GUI.Label(new Rect(Screen.width / 2f - 260f, Screen.height / 2f + 12f, 520f, 55f), "Disconnect 후 다시 Host하면 새 라운드가 시작됩니다.", small);
        }
    }
}
