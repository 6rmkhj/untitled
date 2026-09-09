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
        public int SalvageValue => salvageValue.Value;
        public int RecoveredLoot => recoveredLoot.Value;
        public int BrokenLoot => brokenLoot.Value;
        public float TimeLeft => timeLeft.Value;
        public bool Ended => ended.Value;
        public bool Won => won.Value;
        public int MapSeed => mapSeed.Value;

        private readonly NetworkVariable<int> delivered = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> salvageValue = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> recoveredLoot = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> brokenLoot = new(
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

        private readonly NetworkVariable<int> mapSeed = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float serverTimeLeft;
        private float timerSyncAccumulator;
        private RandomMapGenerator mapGenerator;

        public void Configure(int coreCount, float duration)
        {
            totalCores = Mathf.Max(1, coreCount);
            roundDuration = Mathf.Max(30f, duration);
        }

        private void Awake()
        {
            Instance = this;
            mapGenerator = GetComponent<RandomMapGenerator>();
            if (mapGenerator == null)
                mapGenerator = gameObject.AddComponent<RandomMapGenerator>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            mapSeed.OnValueChanged += OnMapSeedChanged;

            if (IsServer)
            {
                if (totalCores <= 0)
                    totalCores = FindObjectsByType<SignalCore>(FindObjectsSortMode.None).Length;

                delivered.Value = 0;
                salvageValue.Value = 0;
                recoveredLoot.Value = 0;
                brokenLoot.Value = 0;
                serverTimeLeft = roundDuration;
                timeLeft.Value = serverTimeLeft;
                ended.Value = false;
                won.Value = false;
                timerSyncAccumulator = 0f;

                int newSeed;
                do
                {
                    newSeed = Random.Range(1, int.MaxValue);
                }
                while (newSeed == 0 || newSeed == mapSeed.Value);
                mapSeed.Value = newSeed;
            }

            if (mapSeed.Value != 0)
                ApplyMapSeed(mapSeed.Value);
        }

        public override void OnNetworkDespawn()
        {
            mapSeed.OnValueChanged -= OnMapSeedChanged;
        }

        private void OnMapSeedChanged(int previousValue, int newValue)
        {
            if (newValue != 0)
                ApplyMapSeed(newValue);
        }

        private void ApplyMapSeed(int seed)
        {
            if (mapGenerator == null)
                return;

            if (mapGenerator.CurrentSeed == seed && mapGenerator.GeneratedLevelCount > 0)
                return;

            mapGenerator.Generate(seed, IsServer);
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

        public void DeliverLootServer(PhysicsLoot item)
        {
            if (!IsServer || ended.Value || item == null || item.Delivered || item.Broken)
                return;

            int recoveredValue = item.CurrentValue;
            bool wasObjective = item.IsRequiredObjective;
            if (!item.MarkDeliveredServer())
                return;

            salvageValue.Value += recoveredValue;
            recoveredLoot.Value++;

            if (wasObjective)
            {
                delivered.Value++;
                if (delivered.Value >= totalCores)
                    EndGameServer(true);
            }
        }

        public void DeliverCoreServer(SignalCore core)
        {
            if (core != null)
                DeliverLootServer(core.Loot);
        }

        public void ReportLootBrokenServer(PhysicsLoot item)
        {
            if (!IsServer || item == null)
                return;

            brokenLoot.Value++;
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

            GUI.Box(new Rect(18f, 18f, 390f, 224f), string.Empty);
            GUI.Label(new Rect(32f, 28f, 350f, 28f), "SIGNAL HAUL", style);
            GUI.Label(new Rect(32f, 62f, 350f, 24f), $"CORE  {Delivered}/{totalCores}");
            GUI.Label(new Rect(32f, 88f, 350f, 24f), $"SALVAGE  ${SalvageValue}   RECOVERED {RecoveredLoot}");
            GUI.Label(new Rect(32f, 114f, 350f, 24f), $"BROKEN  {BrokenLoot}");
            GUI.Label(new Rect(32f, 140f, 350f, 24f), $"STORM  {Mathf.CeilToInt(TimeLeft)}s");
            GUI.Label(new Rect(32f, 166f, 350f, 24f), $"MAP  {MapSeed}");

            if (localPlayer != null)
                GUI.Label(new Rect(32f, 192f, 350f, 24f), $"HP  {Mathf.CeilToInt(localPlayer.Health)}   STAMINA  {Mathf.CeilToInt(localPlayer.Stamina)}");

            GUI.Label(new Rect(18f, Screen.height - 48f, 900f, 30f), "WASD 이동  |  SHIFT 달리기  |  SPACE 점프/벽등반  |  E 잡기/놓기  |  Q 던지기/놓기  |  V 음성  |  M 음소거");

            if (localPlayer != null && !Ended)
            {
                if (!string.IsNullOrEmpty(localPlayer.ContextPrompt))
                {
                    var centered = new GUIStyle(style)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 17,
                        wordWrap = true
                    };
                    GUI.Box(new Rect(Screen.width * .5f - 300f, Screen.height * .70f, 600f, 72f), string.Empty);
                    GUI.Label(new Rect(Screen.width * .5f - 286f, Screen.height * .70f + 6f, 572f, 60f), localPlayer.ContextPrompt, centered);
                }

                GUI.Label(new Rect(Screen.width / 2f - 8f, Screen.height / 2f - 15f, 20f, 30f), "+", style);
            }

            if (!Ended)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var big = new GUIStyle(style) { fontSize = 42, alignment = TextAnchor.MiddleCenter };
            var small = new GUIStyle(style) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width / 2f - 300f, Screen.height / 2f - 130f, 600f, 260f), string.Empty);
            GUI.Label(new Rect(Screen.width / 2f - 280f, Screen.height / 2f - 86f, 560f, 65f), Won ? "SIGNAL RESTORED" : "STORM CLAIMED THE CREW", big);
            GUI.Label(new Rect(Screen.width / 2f - 280f, Screen.height / 2f - 10f, 560f, 35f), $"Recovered ${SalvageValue}  |  Broken {BrokenLoot}", small);
            GUI.Label(new Rect(Screen.width / 2f - 280f, Screen.height / 2f + 42f, 560f, 55f), "Disconnect 후 다시 Host하면 새 맵/라운드가 시작됩니다.", small);
        }
    }
}
