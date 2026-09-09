using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SignalHaul
{
    public sealed class NetworkSessionManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private Transform[] spawnPoints;

        [Header("LAN Settings")]
        [SerializeField, Range(2, 4)] private int maxPlayers = 4;
        [SerializeField] private ushort port = 7777;
        [SerializeField] private string address = "127.0.0.1";

        private string statusMessage = "Host a game or enter the host IP to join.";

        public void Configure(NetworkManager manager, UnityTransport unityTransport, Transform[] playerSpawnPoints, int playerLimit, ushort gamePort)
        {
            networkManager = manager;
            transport = unityTransport;
            spawnPoints = playerSpawnPoints;
            maxPlayers = Mathf.Clamp(playerLimit, 2, 4);
            port = gamePort;
        }

        private void Awake()
        {
            if (networkManager == null)
                networkManager = GetComponent<NetworkManager>();

            if (transport == null)
                transport = GetComponent<UnityTransport>();

            if (networkManager == null || transport == null)
            {
                enabled = false;
                Debug.LogError("SIGNAL HAUL: NetworkSessionManager requires NetworkManager and UnityTransport.");
                return;
            }

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback += ApprovalCheck;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (networkManager == null)
                return;

            networkManager.ConnectionApprovalCallback -= ApprovalCheck;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool hasRoom = networkManager.ConnectedClients.Count < maxPlayers;
            response.Approved = hasRoom;
            response.CreatePlayerObject = hasRoom;
            response.PlayerPrefabHash = null;
            response.Pending = false;
            response.Reason = hasRoom ? string.Empty : $"Lobby is full ({maxPlayers}/{maxPlayers}).";

            if (!hasRoom || spawnPoints == null || spawnPoints.Length == 0)
                return;

            int index = (int)(request.ClientNetworkId % (ulong)spawnPoints.Length);
            Transform spawn = spawnPoints[index];
            response.Position = spawn.position;
            response.Rotation = spawn.rotation;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager.IsServer)
                statusMessage = $"Player connected. {networkManager.ConnectedClients.Count}/{maxPlayers}";
            else if (clientId == networkManager.LocalClientId)
                statusMessage = "Connected to host.";
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId == networkManager.LocalClientId)
            {
                string reason = networkManager.DisconnectReason;
                statusMessage = string.IsNullOrWhiteSpace(reason) ? "Disconnected." : reason;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (networkManager.IsServer)
            {
                statusMessage = $"Player disconnected. {networkManager.ConnectedClients.Count}/{maxPlayers}";
            }
        }

        private void StartHost()
        {
            if (networkManager.IsListening)
                return;

            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            statusMessage = networkManager.StartHost() ? "Starting host..." : "Failed to start host.";
        }

        private void StartClient()
        {
            if (networkManager.IsListening)
                return;

            string target = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            transport.SetConnectionData(target, port);
            statusMessage = networkManager.StartClient() ? $"Connecting to {target}:{port}..." : "Failed to start client.";
        }

        private void Disconnect()
        {
            if (networkManager.IsListening)
                networkManager.Shutdown();

            statusMessage = "Disconnected.";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (networkManager == null)
                return;

            if (!networkManager.IsListening)
            {
                DrawLobbyPanel();
                return;
            }

            DrawSessionPanel();
        }

        private void DrawLobbyPanel()
        {
            float width = 430f;
            float height = 250f;
            float x = Screen.width * .5f - width * .5f;
            float y = Screen.height * .5f - height * .5f;

            GUI.Box(new Rect(x, y, width, height), string.Empty);

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            title.normal.textColor = Color.white;

            GUI.Label(new Rect(x + 20f, y + 15f, width - 40f, 42f), "SIGNAL HAUL - LAN CO-OP", title);
            GUI.Label(new Rect(x + 30f, y + 70f, 100f, 25f), "Host IP");
            address = GUI.TextField(new Rect(x + 130f, y + 68f, 265f, 28f), address, 45);

            if (GUI.Button(new Rect(x + 30f, y + 115f, 175f, 42f), "HOST GAME"))
                StartHost();

            if (GUI.Button(new Rect(x + 220f, y + 115f, 175f, 42f), "JOIN GAME"))
                StartClient();

            GUI.Label(new Rect(x + 30f, y + 172f, width - 60f, 28f), $"Port {port}  |  Max {maxPlayers} players");
            GUI.Label(new Rect(x + 30f, y + 202f, width - 60f, 32f), statusMessage);
        }

        private void DrawSessionPanel()
        {
            const float width = 220f;
            float x = Screen.width - width - 18f;
            float y = 18f;

            GUI.Box(new Rect(x, y, width, 92f), string.Empty);
            string role = networkManager.IsHost ? "HOST" : networkManager.IsServer ? "SERVER" : "CLIENT";
            string count = networkManager.IsServer ? $"  {networkManager.ConnectedClients.Count}/{maxPlayers}" : string.Empty;
            GUI.Label(new Rect(x + 14f, y + 12f, 190f, 24f), $"{role}{count}");

            if (GUI.Button(new Rect(x + 14f, y + 48f, 192f, 28f), "DISCONNECT"))
                Disconnect();
        }
    }
}
