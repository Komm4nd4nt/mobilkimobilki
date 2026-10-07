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
            if (spawnGridPoints == null || spawnGridPoints.Count == 0)
            {
                spawnGridPoints = new List<Transform>();
                for (int i = 1; i <= 8; i++)
                {
                    var slot = GameObject.Find($"GridSlot_{i}");
                    if (slot != null) spawnGridPoints.Add(slot.transform);
                }
            }

            ResolveCarPrefab();
            SetupUIButtons();
            if (connectionPanel != null) connectionPanel.SetActive(false);

            // Auto-start session (Solo test drive or Host/Client configured from Main Menu)
            AutoStartSession();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleSceneEvent;
                }
            }
        }

        private void AutoStartSession()
        {
            if (NetworkManager.Singleton == null) return;

            // If session was already started in Main Menu Lobby:
            if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            {
                ResolveCarPrefab();
                CleanupScenePlaceholders();

                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                    NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;

                    if (NetworkManager.Singleton.SceneManager != null)
                    {
                        NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleSceneEvent;
                        NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleSceneEvent;
                    }

                    foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        HandleClientConnected(clientId);
                    }
                }
                return;
            }

            // Direct scene launch fallback:
            if (!LobbySessionData.IsHost && !string.IsNullOrEmpty(LobbySessionData.ServerAddress) && LobbySessionData.CurrentRoomCode == "DIRECT_IP")
            {
                SetTransportAddress(LobbySessionData.ServerAddress, LobbySessionData.ServerPort);
                StartClient();
            }
            else
            {
                // Default: Start Host so local player's car is immediately spawned on the track!
                StartHost();
            }
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

            NetworkConfigSynchronizer.ApplyConfiguration();
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

            NetworkConfigSynchronizer.ApplyConfiguration();
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

        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
            {
                ulong clientId = sceneEvent.ClientId;
                if (!spawnedClients.Contains(clientId))
                {
                    HandleClientConnected(clientId);
                }
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // Avoid duplicate spawns for the same client ID
            if (spawnedClients.Contains(clientId)) return;

            ResolveCarPrefab();

            if (networkCarPrefab == null)
            {
                UpdateStatus("Error: Car Prefab not found! Cannot spawn vehicle.");
                return;
            }

            // Check if client already has a valid spawned PlayerObject in the current scene
            NetworkObject existingPlayerObj = null;
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            {
                existingPlayerObj = clientData.PlayerObject;
            }

            // Spawn car for connected client at the next grid position
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (spawnGridPoints.Count > 0)
            {
                int index = nextSpawnIndex % spawnGridPoints.Count;
                if (spawnGridPoints[index] != null)
                {
                    // Raise spawn position by 0.55m so WheelColliders don't spawn buried inside the ground and explode upwards
                    spawnPos = spawnGridPoints[index].position + Vector3.up * 0.55f;
                    spawnRot = spawnGridPoints[index].rotation;
                }
                nextSpawnIndex++;
            }
            else
            {
                // Default grid offset
                float offset = (float)clientId;
                spawnPos = new Vector3(offset * 4f, 0.55f, -offset * 6f);
            }

            if (existingPlayerObj != null && existingPlayerObj.IsSpawned)
            {
                existingPlayerObj.gameObject.SetActive(true);
                existingPlayerObj.transform.position = spawnPos;
                existingPlayerObj.transform.rotation = spawnRot;
                var rb = existingPlayerObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = spawnPos;
                    rb.rotation = spawnRot;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                spawnedClients.Add(clientId);
                Debug.Log($"[RacingNet] Repositioned existing player vehicle for Client {clientId} at {spawnPos}");
                return;
            }

            spawnedClients.Add(clientId);
            GameObject playerCar = Instantiate(networkCarPrefab, spawnPos, spawnRot);
            playerCar.SetActive(true);
            Rigidbody carRb = playerCar.GetComponent<Rigidbody>();
            if (carRb != null)
            {
                carRb.position = spawnPos;
                carRb.rotation = spawnRot;
                carRb.linearVelocity = Vector3.zero;
                carRb.angularVelocity = Vector3.zero;
            }

            NetworkObject netObj = playerCar.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                try
                {
                    netObj.SpawnAsPlayerObject(clientId);
                }
                catch
                {
                    netObj.SpawnWithOwnership(clientId);
                }
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
    }
}
