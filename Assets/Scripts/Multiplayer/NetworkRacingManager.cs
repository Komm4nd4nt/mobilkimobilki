using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace RacingMobile.Multiplayer
{
    /// <summary>
    /// Multiplayer match manager for Netcode for GameObjects.
    /// Manages Host/Client connection, player grid spawn points, and mobile connection HUD.
    /// Ensures every connected player gets their own dedicated vehicle.
    /// </summary>
    public class NetworkRacingManager : MonoBehaviour
    {
        public static NetworkRacingManager Instance { get; private set; }

        [Header("Player Vehicle Prefab")]
        [Tooltip("Car prefab registered in NetworkManager NetworkPrefabs list")]
        [SerializeField] private GameObject networkCarPrefab;

        [Header("Starting Grid Spawn Positions")]
        [SerializeField] private List<Transform> spawnGridPoints = new List<Transform>();

        [Header("Mobile Connection HUD (Optional UI)")]
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private InputField ipInputField;
        [SerializeField] private Text statusText;

        [Header("Network Settings")]
        [SerializeField] private string defaultIp = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;

        private int nextSpawnIndex = 0;
        private readonly HashSet<ulong> spawnedClients = new HashSet<ulong>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            ResolveCarPrefab();
            SetupUIButtons();
            UpdateStatus("Ready to connect. Choose Host or Client.");
        }

        public void ResolveCarPrefab()
        {
            if (networkCarPrefab != null) return;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null)
            {
                networkCarPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
                return;
            }

#if UNITY_EDITOR
            networkCarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RacingCar_Netcode.prefab");
#endif
        }

        private void SetupUIButtons()
        {
            if (hostButton != null) hostButton.onClick.AddListener(StartHost);
            if (clientButton != null) clientButton.onClick.AddListener(StartClient);
            if (ipInputField != null) ipInputField.text = defaultIp;
        }

        public void StartHost()
        {
            if (NetworkManager.Singleton == null)
            {
                UpdateStatus("Error: NetworkManager not found in scene!");
                return;
            }

            ResolveCarPrefab();
            CleanupScenePlaceholders();
            SetTransportAddress(GetIpInput(), defaultPort);

            spawnedClients.Clear();
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            bool success = NetworkManager.Singleton.StartHost();

            if (success)
            {
                UpdateStatus("Host started! Waiting for players...");
                if (connectionPanel != null) connectionPanel.SetActive(false);

                // Ensure local host player gets their car spawned immediately
                HandleClientConnected(NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                UpdateStatus("Failed to start Host.");
            }
        }

        public void StartClient()
        {
            if (NetworkManager.Singleton == null)
            {
                UpdateStatus("Error: NetworkManager not found in scene!");
                return;
            }

            ResolveCarPrefab();
            CleanupScenePlaceholders();
            SetTransportAddress(GetIpInput(), defaultPort);

            bool success = NetworkManager.Singleton.StartClient();
            if (success)
            {
                UpdateStatus($"Connecting to {GetIpInput()}:{defaultPort}...");
                if (connectionPanel != null) connectionPanel.SetActive(false);
            }
            else
            {
                UpdateStatus("Failed to connect as Client.");
            }
        }

        private void CleanupScenePlaceholders()
        {
            // Remove unmanaged scene vehicle so only true networked cars exist
            GameObject placeholder = GameObject.Find("Player_RacingCar");
            if (placeholder != null)
            {
                Destroy(placeholder);
            }
        }

        private void SetTransportAddress(string ip, ushort port)
        {
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ip, port);
            }
        }

        private string GetIpInput()
        {
            if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
            {
                return ipInputField.text.Trim();
            }
            return defaultIp;
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // Avoid duplicate spawns for the same client ID
            if (spawnedClients.Contains(clientId)) return;
            spawnedClients.Add(clientId);

            ResolveCarPrefab();

            if (networkCarPrefab == null)
            {
                UpdateStatus("Error: Car Prefab not found! Cannot spawn vehicle.");
                return;
            }

            // Spawn car for connected client at the next grid position
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (spawnGridPoints.Count > 0)
            {
                int index = nextSpawnIndex % spawnGridPoints.Count;
                if (spawnGridPoints[index] != null)
                {
                    spawnPos = spawnGridPoints[index].position;
                    spawnRot = spawnGridPoints[index].rotation;
                }
                nextSpawnIndex++;
            }
            else
            {
                // Default grid offset
                float offset = (float)clientId;
                spawnPos = new Vector3(offset * 4f, 0.5f, -offset * 6f);
            }

            GameObject playerCar = Instantiate(networkCarPrefab, spawnPos, spawnRot);
            NetworkObject netObj = playerCar.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnWithOwnership(clientId);
                Debug.Log($"[RacingNet] Spawned dedicated car for Client {clientId} at {spawnPos}");
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[RacingNet] {message}");
        }

        private void OnGUI()
        {
            // Simple OnGUI fallback in case the canvas is hidden
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                GUILayout.BeginArea(new Rect(15, 15, 170, 130), GUI.skin.box);
                GUILayout.Label("Racing Multiplayer (NGO)");

                if (GUILayout.Button("Start Host"))
                {
                    StartHost();
                }

                if (GUILayout.Button("Join Client"))
                {
                    StartClient();
                }

                if (GUILayout.Button("Start Server"))
                {
                    NetworkManager.Singleton.StartServer();
                }

                GUILayout.EndArea();
            }
        }
    }
}
