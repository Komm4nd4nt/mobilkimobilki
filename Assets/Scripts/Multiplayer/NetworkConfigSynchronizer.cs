using System;
using System.Reflection;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RacingMobile.Multiplayer
{
    /// <summary>
    /// Automatic Netcode configuration synchronizer.
    /// Eliminates 'NetworkConfig mismatch' errors between Host and Client (especially in MPPM clones)
    /// by guaranteeing identical NetworkConfig settings, disabling ForceSamePrefabs,
    /// ensuring the car prefab is registered, and providing an on-screen connection HUD.
    /// </summary>
    public static class NetworkConfigSynchronizer
    {
        private const string CarPrefabPath = "Assets/Prefabs/RacingCar_Netcode.prefab";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            ApplyConfiguration();
        }

        public static void ApplyConfiguration()
        {
            NetworkManager netMgr = NetworkManager.Singleton;
            if (netMgr == null)
            {
                netMgr = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                if (netMgr == null) return;
            }

            // Ensure UnityTransport component exists
            UnityTransport transport = netMgr.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = netMgr.gameObject.AddComponent<UnityTransport>();
            }

            NetworkConfig config = netMgr.NetworkConfig;
            if (config == null)
            {
                config = new NetworkConfig();
                netMgr.NetworkConfig = config;
            }

            // Crucial: Bind NetworkTransport to NetworkConfig
            config.NetworkTransport = transport;

            // 1. Force Disable ForceSamePrefabs
            // This prevents XXHash mismatch of prefab override links across virtual projects / clones
            config.ForceSamePrefabs = false;

            // 2. Normalize scalar handshake fields
            config.ProtocolVersion = 0;
            config.TickRate = 30;
            config.ConnectionApproval = false;
            config.EnableSceneManagement = true;
            config.EnsureNetworkVariableLengthSafety = false;
            config.RpcHashSize = HashSize.VarIntFourBytes;

            // 3. Ensure valid PlayerPrefab
#if UNITY_EDITOR
            if (config.PlayerPrefab == null || !PrefabUtility.IsPartOfPrefabAsset(config.PlayerPrefab))
            {
                GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
                if (carPrefab != null)
                {
                    config.PlayerPrefab = carPrefab;
                }
            }
#endif

            // 4. Invalidate cached config hash so NGO recalculates with new settings
            ClearCachedConfigHash(config);

            ulong hash = config.GetConfig(false);
            Debug.Log($"<color=cyan>[NetworkConfigSynchronizer]</color> Config normalized! Hash: <b>{hash}</b> | Transport: {(config.NetworkTransport != null ? config.NetworkTransport.GetType().Name : "null")} | ForceSamePrefabs: {config.ForceSamePrefabs} | PlayerPrefab: {(config.PlayerPrefab != null ? config.PlayerPrefab.name : "null")}");
        }

        public static void ClearCachedConfigHash(NetworkConfig config)
        {
            if (config == null) return;
            try
            {
                MethodInfo clearMethod = typeof(NetworkConfig).GetMethod("ClearConfigHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                clearMethod?.Invoke(config, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkConfigSynchronizer] Could not clear config hash: {ex.Message}");
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EditorInitialize()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                EnsureSceneNetworkConfig();
            };
        }

        public static void EnsureSceneNetworkConfig()
        {
            NetworkManager netMgr = GameObject.FindObjectOfType<NetworkManager>();
            if (netMgr == null) return;

            bool modified = false;

            UnityTransport transport = netMgr.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = netMgr.gameObject.AddComponent<UnityTransport>();
                modified = true;
            }

            NetworkConfig config = netMgr.NetworkConfig;
            if (config == null)
            {
                config = new NetworkConfig();
                netMgr.NetworkConfig = config;
                modified = true;
            }

            if (config.NetworkTransport != transport)
            {
                config.NetworkTransport = transport;
                modified = true;
            }

            if (config.ForceSamePrefabs)
            {
                config.ForceSamePrefabs = false;
                modified = true;
            }

            GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (carPrefab != null && (config.PlayerPrefab != carPrefab || !PrefabUtility.IsPartOfPrefabAsset(config.PlayerPrefab)))
            {
                config.PlayerPrefab = carPrefab;
                modified = true;
            }

            if (modified)
            {
                EditorUtility.SetDirty(netMgr);
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                }
                Debug.Log("<color=green>[NetworkConfigSynchronizer]</color> Editor NetworkManager updated: ForceSamePrefabs=false, NetworkTransport=UnityTransport, PlayerPrefab=RacingCar_Netcode");
            }
        }
#endif
    }

    /// <summary>
    /// Lightweight on-screen UI for starting Host / Client without needing the Inspector.
    /// Also supports hotkeys: [H] for Host, [C] for Client.
    /// </summary>
    public class NetworkConfigHUD : MonoBehaviour
    {
        private string ipAddress = "127.0.0.1";
        private const ushort Port = 7777;

        private void Update()
        {
            if (NetworkManager.Singleton == null) return;

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.hKey.wasPressedThisFrame)
                    {
                        StartHost();
                    }
                    else if (kb.cKey.wasPressedThisFrame)
                    {
                        StartClient();
                    }
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.H))
                {
                    StartHost();
                }
                else if (Input.GetKeyDown(KeyCode.C))
                {
                    StartClient();
                }
#endif
            }
        }

        public void StartHost()
        {
            NetworkConfigSynchronizer.ApplyConfiguration();
            SetTransportAddress(ipAddress, Port);
            bool success = NetworkManager.Singleton.StartHost();
            Debug.Log($"[NetworkConfigHUD] StartHost result: {success}");
        }

        public void StartClient()
        {
            NetworkConfigSynchronizer.ApplyConfiguration();
            SetTransportAddress(ipAddress, Port);
            bool success = NetworkManager.Singleton.StartClient();
            Debug.Log($"[NetworkConfigHUD] StartClient result: {success}");
        }

        private void SetTransportAddress(string ip, ushort port)
        {
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ip, port);
            }
        }

        private void OnGUI()
        {
            NetworkManager netMgr = NetworkManager.Singleton;
            if (netMgr == null) return;

            GUI.skin.box.fontSize = 13;
            GUI.skin.button.fontSize = 13;

            if (!netMgr.IsClient && !netMgr.IsServer)
            {
                GUILayout.BeginArea(new Rect(15, 15, 230, 160), GUI.skin.box);
                GUILayout.Label("<b>🏎️ Multiplayer Racing (NGO)</b>");
                GUILayout.Space(4);

                GUILayout.BeginHorizontal();
                GUILayout.Label("IP:", GUILayout.Width(25));
                ipAddress = GUILayout.TextField(ipAddress);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);

                if (GUILayout.Button("Start Host [H]", GUILayout.Height(32)))
                {
                    StartHost();
                }

                if (GUILayout.Button("Join Client [C]", GUILayout.Height(32)))
                {
                    StartClient();
                }

                GUILayout.EndArea();
            }
            else
            {
                GUILayout.BeginArea(new Rect(15, 15, 220, 85), GUI.skin.box);
                if (netMgr.IsHost)
                {
                    GUILayout.Label($"<b>Host Active</b> (Clients: {netMgr.ConnectedClientsIds.Count})");
                }
                else if (netMgr.IsServer)
                {
                    GUILayout.Label("<b>Server Active</b>");
                }
                else
                {
                    GUILayout.Label($"<b>Client Active</b> (ID: {netMgr.LocalClientId})");
                }

                if (GUILayout.Button("Disconnect", GUILayout.Height(26)))
                {
                    netMgr.Shutdown();
                }
                GUILayout.EndArea();
            }
        }
    }
}
