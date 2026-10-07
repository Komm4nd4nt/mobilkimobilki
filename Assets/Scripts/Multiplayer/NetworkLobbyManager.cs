using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Collections;
using RacingMobile.Core;

namespace RacingMobile.Multiplayer
{
    [Serializable]
    public class LobbyPlayerData
    {
        public ulong clientId;
        public string nickname;
        public bool isHost;

        public LobbyPlayerData() { }

        public LobbyPlayerData(ulong id, string name, bool host)
        {
            clientId = id;
            nickname = name;
            isHost = host;
        }
    }

    [Serializable]
    public class LobbySyncPayload
    {
        public string roomName;
        public string roomCode;
        public int selectedTrackIndex;
        public int maxPlayers;
        public bool isPrivate;
        public List<LobbyPlayerData> players = new List<LobbyPlayerData>();
    }

    [Serializable]
    public class PlayerJoinPayload
    {
        public ulong clientId;
        public string nickname;
        public string enteredRoomCode;
        public bool isQuickJoin;
    }

    [Serializable]
    public class KickNoticePayload
    {
        public string reason;
    }

    [Serializable]
    public class DiscoveredRoomInfo
    {
        public string roomCode;
        public string ipAddress;
        public ushort port;
        public bool isPrivate;
        public string roomName;
        public float lastSeenTime;
    }

    /// <summary>
    /// Core Network Lobby Manager using Unity Netcode for GameObjects.
    /// Manages Host & Client lobby lifecycle, real-time player list synchronization,
    /// host kicking capabilities, track changes, and synchronized race launch.
    /// </summary>
    public class NetworkLobbyManager : MonoBehaviour
    {
        public static NetworkLobbyManager Instance { get; private set; }

        public const string MSG_PLAYER_JOIN = "LOBBY_MSG_PLAYER_JOIN";
        public const string MSG_LOBBY_SYNC = "LOBBY_MSG_SYNC";
        public const string MSG_KICK_PLAYER = "LOBBY_MSG_KICK";
        public const string MSG_TRACK_CHANGE = "LOBBY_MSG_TRACK";

        [Header("Runtime State")]
        [SerializeField] private bool isInLobby = false;
        [SerializeField] private bool isInLobbyRoom = false;
        [SerializeField] private List<LobbyPlayerData> currentPlayers = new List<LobbyPlayerData>();

        public bool IsInLobby => isInLobby;
        public bool IsInLobbyRoom => isInLobbyRoom;
        public IReadOnlyList<LobbyPlayerData> CurrentPlayers => currentPlayers;
        public bool IsLocalHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        // Events for UI updating
        public event Action OnLobbyStateUpdated;
        public event Action OnLobbyJoinedSuccess;
        public event Action OnLobbyDiscoveryUpdated;
        public event Action<string> OnPlayerKickedNotice;
        public event Action<string> OnLobbyClosedNotice;
        public event Action<string> OnConnectionFailedNotice;

        private const int LAN_DISCOVERY_PORT = 7779;
        private Coroutine hostBeaconCoroutine;
        private UdpClient discoveryReceiver;
        private Coroutine clientDiscoveryCoroutine;
        private readonly Dictionary<string, DiscoveredRoomInfo> discoveredRooms = new Dictionary<string, DiscoveredRoomInfo>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, DiscoveredRoomInfo> DiscoveredRooms => discoveredRooms;

        private Coroutine clientTimeoutCoroutine;
        private bool isKicked = false;
        private string lastEnteredCode = "";
        private bool lastIsQuickJoin = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            EnsureNetworkManager();
            StartLanDiscovery();
        }

        /// <summary>
        /// Guarantees that a valid Netcode NetworkManager is present in the scene.
        /// </summary>
        public static NetworkManager EnsureNetworkManager()
        {
            NetworkManager netMgr = NetworkManager.Singleton;
            if (netMgr == null)
            {
                netMgr = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            }

            if (netMgr == null)
            {
                GameObject netObj = new GameObject("[NetworkManager]");
                netMgr = netObj.AddComponent<NetworkManager>();
                DontDestroyOnLoad(netObj);
            }

            UnityTransport transport = netMgr.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = netMgr.gameObject.AddComponent<UnityTransport>();
            }

            if (netMgr.NetworkConfig == null)
            {
                netMgr.NetworkConfig = new NetworkConfig();
            }

            netMgr.NetworkConfig.NetworkTransport = transport;

            // Apply standard configuration
            NetworkConfigSynchronizer.ApplyConfiguration();

            return netMgr;
        }

        #region Host Lobby Creation

        public bool StartLobbyHost(string roomName, int maxPlayers, int trackIndex, ushort port = 7777, bool isPrivate = false)
        {
            NetworkManager netMgr = EnsureNetworkManager();
            if (netMgr == null)
            {
                Debug.LogError("[NetworkLobby] NetworkManager not found!");
                return false;
            }

            // Ensure transport is set on NetworkConfig
            UnityTransport transport = netMgr.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = netMgr.gameObject.AddComponent<UnityTransport>();
            }

            if (netMgr.NetworkConfig == null)
            {
                netMgr.NetworkConfig = new NetworkConfig();
            }

            netMgr.NetworkConfig.NetworkTransport = transport;
            transport.SetConnectionData("0.0.0.0", port);

            if (netMgr.IsListening)
            {
                try
                {
                    netMgr.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetworkLobby] Warning during shutdown before host start: {ex.Message}");
                }
            }

            // Setup LobbySessionData
            LobbySessionData.SetupNewHostLobby(roomName, maxPlayers, isPrivate, trackIndex);
            LobbySessionData.IsPrivate = isPrivate;
            LobbySessionData.ServerPort = port;
            LobbySessionData.ServerAddress = GetLocalIPAddress();

            isKicked = false;
            currentPlayers.Clear();

            // Unsubscribe prior callbacks to avoid duplication
            netMgr.OnClientConnectedCallback -= HandleServerClientConnected;
            netMgr.OnClientDisconnectCallback -= HandleServerClientDisconnected;

            netMgr.OnClientConnectedCallback += HandleServerClientConnected;
            netMgr.OnClientDisconnectCallback += HandleServerClientDisconnected;

            bool success = false;
            try
            {
                success = netMgr.StartHost();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLobby] Exception during StartHost: {ex.Message}\n{ex.StackTrace}");
                success = false;
            }

            if (success)
            {
                isInLobby = true;
                isInLobbyRoom = true;
                RegisterNamedMessageHandlers();

                // Add Host player to lobby
                string hostNick = PlayerProfileManager.Nickname;
                if (string.IsNullOrWhiteSpace(hostNick)) hostNick = "Host";
                currentPlayers.Add(new LobbyPlayerData(netMgr.LocalClientId, hostNick, true));

                // Start LAN UDP Beacon so other devices on WiFi can find this room by code
                StartHostBeacon();

                Debug.Log($"<color=green>[NetworkLobby]</color> Host lobby started: '{roomName}' (Code: {LobbySessionData.CurrentRoomCode}) on port {port}. Local IP: {LobbySessionData.ServerAddress}");
                OnLobbyStateUpdated?.Invoke();
                return true;
            }
            else
            {
                Debug.LogError("[NetworkLobby] Failed to start Host!");
                isInLobby = false;
                isInLobbyRoom = false;
                return false;
            }
        }

        #endregion

        #region LAN Discovery Beacon (UDP Broadcast)

        private void StartHostBeacon()
        {
            StopHostBeacon();
            hostBeaconCoroutine = StartCoroutine(HostBeaconRoutine());
        }

        private void StopHostBeacon()
        {
            if (hostBeaconCoroutine != null)
            {
                StopCoroutine(hostBeaconCoroutine);
                hostBeaconCoroutine = null;
            }
        }

        private IEnumerator HostBeaconRoutine()
        {
            while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                try
                {
                    using (UdpClient broadcaster = new UdpClient())
                    {
                        broadcaster.EnableBroadcast = true;
                        string myIp = LobbySessionData.ServerAddress;
                        string payload = $"MOBILKI_LOBBY|v1|{LobbySessionData.CurrentRoomCode}|{myIp}|{LobbySessionData.ServerPort}|{LobbySessionData.IsPrivate}|{LobbySessionData.CurrentRoomName}";
                        byte[] data = Encoding.UTF8.GetBytes(payload);
                        broadcaster.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, LAN_DISCOVERY_PORT));
                    }
                }
                catch (Exception)
                {
                    // Ignore transient UDP broadcast exceptions
                }
                yield return new WaitForSeconds(1.0f);
            }
        }

        public void StartLanDiscovery()
        {
            if (discoveryReceiver != null) return;

            try
            {
                discoveryReceiver = new UdpClient();
                discoveryReceiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                discoveryReceiver.ExclusiveAddressUse = false;
                discoveryReceiver.Client.Bind(new IPEndPoint(IPAddress.Any, LAN_DISCOVERY_PORT));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkLobby] LAN Discovery bind notice: {ex.Message}");
                return;
            }

            if (clientDiscoveryCoroutine != null) StopCoroutine(clientDiscoveryCoroutine);
            clientDiscoveryCoroutine = StartCoroutine(ClientDiscoveryRoutine());
        }

        public void StopLanDiscovery()
        {
            if (clientDiscoveryCoroutine != null)
            {
                StopCoroutine(clientDiscoveryCoroutine);
                clientDiscoveryCoroutine = null;
            }
            if (discoveryReceiver != null)
            {
                try { discoveryReceiver.Close(); } catch { }
                discoveryReceiver = null;
            }
        }

        private IEnumerator ClientDiscoveryRoutine()
        {
            while (discoveryReceiver != null)
            {
                try
                {
                    while (discoveryReceiver != null && discoveryReceiver.Available > 0)
                    {
                        IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                        byte[] bytes = discoveryReceiver.Receive(ref remoteEp);
                        string text = Encoding.UTF8.GetString(bytes);
                        ProcessDiscoveredBeacon(text, remoteEp.Address.ToString());
                    }
                }
                catch (Exception)
                {
                    // Ignore transient UDP drops
                }

                // Cleanup stale rooms
                float now = Time.time;
                var staleKeys = new List<string>();
                foreach (var kvp in discoveredRooms)
                {
                    if (now - kvp.Value.lastSeenTime > 4.5f)
                    {
                        staleKeys.Add(kvp.Key);
                    }
                }
                foreach (var k in staleKeys)
                {
                    discoveredRooms.Remove(k);
                }

                yield return new WaitForSeconds(0.4f);
            }
        }

        private void ProcessDiscoveredBeacon(string beacon, string senderIp)
        {
            if (string.IsNullOrWhiteSpace(beacon) || !beacon.StartsWith("MOBILKI_LOBBY|v1|")) return;

            string[] parts = beacon.Split('|');
            if (parts.Length < 7) return;

            string code = parts[2].Trim().ToUpperInvariant();
            string advertisedIp = parts[3].Trim();
            if (!ushort.TryParse(parts[4], out ushort port)) port = 7777;
            bool.TryParse(parts[5], out bool isPrivate);
            string roomName = parts[6].Trim();

            string resolvedIp = (advertisedIp == "0.0.0.0" || string.IsNullOrWhiteSpace(advertisedIp)) ? senderIp : advertisedIp;

            discoveredRooms[code] = new DiscoveredRoomInfo
            {
                roomCode = code,
                ipAddress = resolvedIp,
                port = port,
                isPrivate = isPrivate,
                roomName = roomName,
                lastSeenTime = Time.time
            };

            OnLobbyDiscoveryUpdated?.Invoke();
        }

        public bool TryGetFirstPublicDiscoveredRoom(out DiscoveredRoomInfo publicRoom)
        {
            foreach (var kvp in discoveredRooms)
            {
                if (!kvp.Value.isPrivate)
                {
                    publicRoom = kvp.Value;
                    return true;
                }
            }
            publicRoom = null;
            return false;
        }

        #endregion

        #region Client Join Lobby

        public bool JoinLobbyClient(string ipOrCode, ushort port = 7777, string enteredCode = "", bool isQuickJoin = false)
        {
            lastEnteredCode = enteredCode != null ? enteredCode.Trim().ToUpperInvariant() : "";
            lastIsQuickJoin = isQuickJoin;

            NetworkManager netMgr = EnsureNetworkManager();
            if (netMgr == null)
            {
                Debug.LogError("[NetworkLobby] NetworkManager not found!");
                return false;
            }

            string cleanInput = string.IsNullOrWhiteSpace(ipOrCode) ? "127.0.0.1" : ipOrCode.Trim();

            string targetIp = cleanInput;
            ushort targetPort = port;

            // Check if input contains IP:port or IP:code
            if (cleanInput.Contains(":") && cleanInput.Contains("."))
            {
                string[] parts = cleanInput.Split(':');
                targetIp = parts[0].Trim();
                if (parts.Length > 1)
                {
                    if (ushort.TryParse(parts[1], out ushort p)) targetPort = p;
                    else lastEnteredCode = parts[1].Trim().ToUpperInvariant();
                }
            }

            // If cleanInput is NOT an IP address (no dots, not localhost):
            // Then cleanInput is the Room Code!
            if (!targetIp.Contains(".") && !targetIp.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                string code = targetIp.ToUpperInvariant();
                lastEnteredCode = code;

                if (discoveredRooms.TryGetValue(code, out var room))
                {
                    targetIp = room.ipAddress;
                    targetPort = room.port;
                    Debug.Log($"<color=green>[NetworkLobby]</color> Room Code '{code}' resolved via LAN Discovery to {targetIp}:{targetPort}");
                }
                else
                {
                    // Fallback to localhost (for testing on the same machine)
                    targetIp = "127.0.0.1";
                    targetPort = port;
                    Debug.Log($"<color=yellow>[NetworkLobby]</color> Room Code '{code}' not yet discovered on LAN. Probing localhost (127.0.0.1:{targetPort})...");
                }
            }

            if (targetIp.Equals("localhost", StringComparison.OrdinalIgnoreCase)) targetIp = "127.0.0.1";

            UnityTransport transport = netMgr.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = netMgr.gameObject.AddComponent<UnityTransport>();
            }

            if (netMgr.NetworkConfig == null)
            {
                netMgr.NetworkConfig = new NetworkConfig();
            }

            netMgr.NetworkConfig.NetworkTransport = transport;
            transport.SetConnectionData(targetIp, targetPort);

            if (netMgr.IsListening)
            {
                try
                {
                    netMgr.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetworkLobby] Warning during shutdown before client start: {ex.Message}");
                }
            }

            LobbySessionData.SetupClientJoin(targetIp);
            LobbySessionData.ServerAddress = targetIp;
            LobbySessionData.ServerPort = targetPort;

            isKicked = false;
            isInLobbyRoom = false;
            currentPlayers.Clear();

            netMgr.OnClientConnectedCallback -= HandleClientConnectedSuccess;
            netMgr.OnClientDisconnectCallback -= HandleClientDisconnected;

            netMgr.OnClientConnectedCallback += HandleClientConnectedSuccess;
            netMgr.OnClientDisconnectCallback += HandleClientDisconnected;

            bool success = false;
            try
            {
                success = netMgr.StartClient();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLobby] Exception during StartClient: {ex.Message}\n{ex.StackTrace}");
                success = false;
            }

            if (success)
            {
                isInLobby = true;
                RegisterNamedMessageHandlers();

                if (clientTimeoutCoroutine != null) StopCoroutine(clientTimeoutCoroutine);
                clientTimeoutCoroutine = StartCoroutine(ClientConnectionTimeoutRoutine(5f));

                Debug.Log($"<color=cyan>[NetworkLobby]</color> Client connecting to {targetIp}:{targetPort} (Code: '{lastEnteredCode}', QuickJoin: {isQuickJoin})...");
                return true;
            }
            else
            {
                Debug.LogError("[NetworkLobby] Failed to initiate Client connection!");
                isInLobby = false;
                isInLobbyRoom = false;
                return false;
            }
        }

        private IEnumerator ClientConnectionTimeoutRoutine(float timeout)
        {
            yield return new WaitForSeconds(timeout);
            var netMgr = NetworkManager.Singleton;
            if (netMgr != null && netMgr.IsClient && !isInLobbyRoom)
            {
                Debug.LogWarning("[NetworkLobby] Client connection timed out.");
                LeaveLobby();
                OnConnectionFailedNotice?.Invoke("Nie znaleziono pokoju! Upewnij się, że kod/IP jest poprawny, gospodarz uruchomił grę i jesteście w tej samej sieci.");
            }
            clientTimeoutCoroutine = null;
        }

        #endregion

        #region Messaging Handlers (CustomMessagingManager)

        private void RegisterNamedMessageHandlers()
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null || netMgr.CustomMessagingManager == null) return;

            // 1. Join Payload (Sent by Client to Server)
            netMgr.CustomMessagingManager.RegisterNamedMessageHandler(MSG_PLAYER_JOIN, (ulong senderId, FastBufferReader reader) =>
            {
                if (!netMgr.IsServer) return;
                try
                {
                    reader.ReadValueSafe(out string json);
                    var data = JsonUtility.FromJson<PlayerJoinPayload>(json);
                    HandleClientJoinDataReceived(senderId, data?.nickname, data?.enteredRoomCode, data != null && data.isQuickJoin);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkLobby] Error registering joined player: {ex.Message}");
                }
            });

            // 2. Lobby Sync (Sent by Server to Clients)
            netMgr.CustomMessagingManager.RegisterNamedMessageHandler(MSG_LOBBY_SYNC, (ulong senderId, FastBufferReader reader) =>
            {
                try
                {
                    reader.ReadValueSafe(out string json);
                    var payload = JsonUtility.FromJson<LobbySyncPayload>(json);
                    if (payload != null)
                    {
                        bool wasInRoom = isInLobbyRoom;
                        LobbySessionData.CurrentRoomName = payload.roomName;
                        LobbySessionData.CurrentRoomCode = payload.roomCode;
                        LobbySessionData.SelectedTrackIndex = payload.selectedTrackIndex;
                        LobbySessionData.MaxPlayers = payload.maxPlayers;
                        LobbySessionData.IsPrivate = payload.isPrivate;

                        currentPlayers = payload.players ?? new List<LobbyPlayerData>();
                        isInLobby = true;
                        isInLobbyRoom = true;

                        if (clientTimeoutCoroutine != null)
                        {
                            StopCoroutine(clientTimeoutCoroutine);
                            clientTimeoutCoroutine = null;
                        }

                        if (!wasInRoom)
                        {
                            OnLobbyJoinedSuccess?.Invoke();
                        }

                        OnLobbyStateUpdated?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkLobby] Error parsing lobby sync payload: {ex.Message}");
                }
            });

            // 3. Kick Notice (Sent by Server to Kicked Client)
            netMgr.CustomMessagingManager.RegisterNamedMessageHandler(MSG_KICK_PLAYER, (ulong senderId, FastBufferReader reader) =>
            {
                try
                {
                    reader.ReadValueSafe(out string reason);
                    isKicked = true;
                    Debug.LogWarning($"[NetworkLobby] Received kick notice: {reason}");
                    if (clientTimeoutCoroutine != null)
                    {
                        StopCoroutine(clientTimeoutCoroutine);
                        clientTimeoutCoroutine = null;
                    }
                    OnPlayerKickedNotice?.Invoke(string.IsNullOrWhiteSpace(reason) ? "Zostałeś usunięty z lobby przez gospodarza." : reason);
                    LeaveLobby();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkLobby] Error processing kick notice: {ex.Message}");
                }
            });

            // 4. Track Change Sync
            netMgr.CustomMessagingManager.RegisterNamedMessageHandler(MSG_TRACK_CHANGE, (ulong senderId, FastBufferReader reader) =>
            {
                try
                {
                    reader.ReadValueSafe(out int trackIndex);
                    LobbySessionData.SelectedTrackIndex = trackIndex;
                    OnLobbyStateUpdated?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkLobby] Error processing track change: {ex.Message}");
                }
            });
        }

        #endregion

        #region Server Callbacks & Player Management

        private void HandleServerClientConnected(ulong clientId)
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null || !netMgr.IsServer) return;

            if (clientId == netMgr.LocalClientId)
            {
                // Local host already added in StartLobbyHost
                return;
            }

            // Check max players limit
            if (currentPlayers.Count >= LobbySessionData.MaxPlayers)
            {
                Debug.LogWarning($"[NetworkLobby] Lobby is full! Rejecting client {clientId}");
                KickPlayer(clientId, "Pokój jest pełny!");
                return;
            }

            Debug.Log($"<color=yellow>[NetworkLobby]</color> Client {clientId} connected to server. Awaiting profile data...");
        }

        private void HandleClientJoinDataReceived(ulong clientId, string nickname, string enteredCode, bool isQuickJoin)
        {
            if (LobbySessionData.IsPrivate)
            {
                bool hasMatchingCode = !string.IsNullOrWhiteSpace(enteredCode) &&
                    enteredCode.Trim().Equals(LobbySessionData.CurrentRoomCode, StringComparison.OrdinalIgnoreCase);

                if (isQuickJoin)
                {
                    Debug.LogWarning($"[NetworkLobby] Odrzucono gracza {clientId}: Pokój jest prywatny! Próba szybkiego dołączenia.");
                    KickPlayer(clientId, "To lobby jest prywatne! Musisz podać poprawny kod pokoju, aby dołączyć.");
                    return;
                }

                if (!hasMatchingCode)
                {
                    Debug.LogWarning($"[NetworkLobby] Odrzucono gracza {clientId}: Pokój jest prywatny! (Wpisany kod: '{enteredCode}', Wymagany: '{LobbySessionData.CurrentRoomCode}')");
                    KickPlayer(clientId, "Niepoprawny kod pokoju! Wpisz właściwy kod do prywatnego lobby.");
                    return;
                }
            }

            string cleanNick = string.IsNullOrWhiteSpace(nickname) ? $"Gracz_{clientId}" : nickname.Trim();

            // Check if player already exists
            var existing = currentPlayers.Find(p => p.clientId == clientId);
            if (existing != null)
            {
                existing.nickname = cleanNick;
            }
            else
            {
                currentPlayers.Add(new LobbyPlayerData(clientId, cleanNick, false));
            }

            Debug.Log($"[NetworkLobby] Player registered: '{cleanNick}' (ID: {clientId})");
            BroadcastLobbySync();
            OnLobbyStateUpdated?.Invoke();
        }

        private void HandleServerClientDisconnected(ulong clientId)
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null || !netMgr.IsServer) return;

            int removed = currentPlayers.RemoveAll(p => p.clientId == clientId);
            if (removed > 0)
            {
                Debug.Log($"[NetworkLobby] Player {clientId} left the lobby.");
                BroadcastLobbySync();
                OnLobbyStateUpdated?.Invoke();
            }
        }

        public void BroadcastLobbySync()
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null || !netMgr.IsServer || netMgr.CustomMessagingManager == null) return;

            try
            {
                var payload = new LobbySyncPayload
                {
                    roomName = LobbySessionData.CurrentRoomName,
                    roomCode = LobbySessionData.CurrentRoomCode,
                    selectedTrackIndex = LobbySessionData.SelectedTrackIndex,
                    maxPlayers = LobbySessionData.MaxPlayers,
                    isPrivate = LobbySessionData.IsPrivate,
                    players = currentPlayers
                };

                string json = JsonUtility.ToJson(payload);
                int bufferSize = (json.Length * 2) + 512;

                using (var writer = new FastBufferWriter(bufferSize, Allocator.Temp, 65536))
                {
                    writer.WriteValueSafe(json);
                    netMgr.CustomMessagingManager.SendNamedMessageToAll(MSG_LOBBY_SYNC, writer);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLobby] Exception in BroadcastLobbySync: {ex.Message}");
            }
        }

        #endregion

        #region Client Callbacks

        private void HandleClientConnectedSuccess(ulong clientId)
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null) return;

            Debug.Log($"<color=green>[NetworkLobby]</color> Successfully established socket as Client {clientId}! Sending handshake to host...");

            // Send local nickname and room code to host
            SendNicknameToHost();
        }

        private void SendNicknameToHost()
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null || !netMgr.IsClient || netMgr.CustomMessagingManager == null) return;

            try
            {
                string myNick = PlayerProfileManager.Nickname;
                if (string.IsNullOrWhiteSpace(myNick)) myNick = $"Driver_{netMgr.LocalClientId}";

                var data = new PlayerJoinPayload
                {
                    clientId = netMgr.LocalClientId,
                    nickname = myNick,
                    enteredRoomCode = lastEnteredCode,
                    isQuickJoin = lastIsQuickJoin
                };

                string json = JsonUtility.ToJson(data);
                int bufferSize = (json.Length * 2) + 512;

                using (var writer = new FastBufferWriter(bufferSize, Allocator.Temp, 65536))
                {
                    writer.WriteValueSafe(json);
                    netMgr.CustomMessagingManager.SendNamedMessage(MSG_PLAYER_JOIN, NetworkManager.ServerClientId, writer);
                }

                Debug.Log($"<color=cyan>[NetworkLobby]</color> Handshake sent to Host (Code='{lastEnteredCode}', Quick={lastIsQuickJoin})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLobby] Exception in SendNicknameToHost: {ex.Message}");
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null) return;

            // If we are a client and disconnected from the host
            if (!netMgr.IsServer && clientId == netMgr.LocalClientId)
            {
                isInLobby = false;
                isInLobbyRoom = false;
                currentPlayers.Clear();

                if (isKicked)
                {
                    // Handled by MSG_KICK_PLAYER notice
                }
                else
                {
                    OnLobbyClosedNotice?.Invoke("Utracono połączenie z serwerem lub lobby zostało zamknięte.");
                }

                OnLobbyStateUpdated?.Invoke();
            }
        }

        #endregion

        #region Host Actions: Kick Player & Change Track

        /// <summary>
        /// Kicks a connected player from the lobby. Available exclusively to the Host!
        /// </summary>
        public void KickPlayer(ulong targetClientId, string reason = "Zostałeś usunięty z lobby przez gospodarza.")
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null || !netMgr.IsServer)
            {
                Debug.LogWarning("[NetworkLobby] Only Host can kick players!");
                return;
            }

            if (targetClientId == netMgr.LocalClientId)
            {
                Debug.LogWarning("[NetworkLobby] Host cannot kick themselves!");
                return;
            }

            Debug.Log($"<color=red>[NetworkLobby]</color> Kicking Client {targetClientId} from lobby. Reason: {reason}");

            // Send notice to client before disconnecting
            if (netMgr.CustomMessagingManager != null)
            {
                try
                {
                    int bufferSize = (reason.Length * 2) + 512;
                    using (var writer = new FastBufferWriter(bufferSize, Allocator.Temp, 65536))
                    {
                        writer.WriteValueSafe(reason);
                        netMgr.CustomMessagingManager.SendNamedMessage(MSG_KICK_PLAYER, targetClientId, writer);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetworkLobby] Could not send kick message to client before disconnect: {ex.Message}");
                }
            }

            // Remove and broadcast update
            currentPlayers.RemoveAll(p => p.clientId == targetClientId);
            BroadcastLobbySync();
            OnLobbyStateUpdated?.Invoke();

            // Disconnect client after brief delay so message arrives
            StartCoroutine(DeferredDisconnectClient(targetClientId));
        }

        private IEnumerator DeferredDisconnectClient(ulong clientId)
        {
            yield return new WaitForSeconds(0.12f);
            var netMgr = NetworkManager.Singleton;
            if (netMgr != null && netMgr.IsServer && netMgr.ConnectedClients.ContainsKey(clientId))
            {
                netMgr.DisconnectClient(clientId);
            }
        }

        public void ChangeTrack(int trackIndex)
        {
            if (!IsLocalHost) return;

            LobbySessionData.SelectedTrackIndex = Mathf.Clamp(trackIndex, 0, LobbySessionData.AvailableTracks.Count - 1);
            BroadcastLobbySync();
            OnLobbyStateUpdated?.Invoke();
        }

        public void NextTrack()
        {
            if (!IsLocalHost) return;
            LobbySessionData.NextTrack();
            BroadcastLobbySync();
            OnLobbyStateUpdated?.Invoke();
        }

        public void PrevTrack()
        {
            if (!IsLocalHost) return;
            LobbySessionData.PrevTrack();
            BroadcastLobbySync();
            OnLobbyStateUpdated?.Invoke();
        }

        #endregion

        #region Start Race & Scene Transition

        /// <summary>
        /// Initiates race for all connected players via Netcode SceneManager. Available exclusively to the Host!
        /// </summary>
        public void StartRace()
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null || !netMgr.IsServer)
            {
                Debug.LogWarning("[NetworkLobby] Only the Host can start the race!");
                return;
            }

            string sceneName = LobbySessionData.SelectedTrack.SceneName;
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                sceneName = "SampleScene";
            }

            Debug.Log($"<color=green>[NetworkLobby]</color> Starting race on scene: {sceneName} for {currentPlayers.Count} players!");

            if (netMgr.SceneManager != null)
            {
                netMgr.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        #endregion

        #region Leave Lobby & Shutdown

        public void LeaveLobby()
        {
            StopHostBeacon();
            isInLobby = false;
            isInLobbyRoom = false;
            currentPlayers.Clear();

            UnregisterNamedMessageHandlers();

            var netMgr = NetworkManager.Singleton;
            if (netMgr != null)
            {
                netMgr.OnClientConnectedCallback -= HandleServerClientConnected;
                netMgr.OnClientDisconnectCallback -= HandleServerClientDisconnected;
                netMgr.OnClientConnectedCallback -= HandleClientConnectedSuccess;
                netMgr.OnClientDisconnectCallback -= HandleClientDisconnected;

                if (netMgr.IsListening)
                {
                    try
                    {
                        netMgr.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NetworkLobby] Warning during shutdown on LeaveLobby: {ex.Message}");
                    }
                }
            }

            Debug.Log("[NetworkLobby] Left lobby.");
            OnLobbyStateUpdated?.Invoke();
        }

        private void OnDestroy()
        {
            LeaveLobby();
        }

        private void UnregisterNamedMessageHandlers()
        {
            var netMgr = NetworkManager.Singleton;
            if (netMgr == null || netMgr.CustomMessagingManager == null) return;

            netMgr.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_PLAYER_JOIN);
            netMgr.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_LOBBY_SYNC);
            netMgr.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_KICK_PLAYER);
            netMgr.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_TRACK_CHANGE);
        }

        #endregion

        #region Utilities

        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("127."))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        #endregion
    }
}
