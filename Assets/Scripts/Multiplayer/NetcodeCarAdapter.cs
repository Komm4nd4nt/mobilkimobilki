using UnityEngine;
using RacingMobile.Core;
using RacingMobile.Vehicle;

#if UNITY_NETCODE_GAMEOBJECTS
using Unity.Netcode;
#endif

namespace RacingMobile.Multiplayer
{
    /// <summary>
    /// Official Unity Netcode for GameObjects (NGO) Adapter for Racing Vehicle.
    /// When com.unity.netcode.gameobjects is installed in the project, this component
    /// automatically synchronizes car state across the server and connected clients.
    /// </summary>
#if UNITY_NETCODE_GAMEOBJECTS
    [RequireComponent(typeof(CarPhysicsController), typeof(CarNetworkSync))]
    public class NetcodeCarAdapter : NetworkBehaviour
    {
        private CarPhysicsController car;
        private CarNetworkSync networkSync;

        // Synchronized network state using NetworkVariable
        private readonly NetworkVariable<CarNetworkSnapshot> netState = new NetworkVariable<CarNetworkSnapshot>(
            writePerm: NetworkVariableWritePermission.Owner,
            readPerm: NetworkVariableReadPermission.Everyone
        );

        private void Awake()
        {
            car = GetComponent<CarPhysicsController>();
            networkSync = GetComponent<CarNetworkSync>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                // Local player owns this vehicle
                car.IsLocallyControlled = true;
                networkSync.ConfigureForLocalOrRemote();
                networkSync.OnSnapshotGenerated += HandleSnapshotGenerated;
            }
            else
            {
                // Remote client car
                car.IsLocallyControlled = false;
                networkSync.ConfigureForLocalOrRemote();
                netState.OnValueChanged += HandleNetworkStateChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                networkSync.OnSnapshotGenerated -= HandleSnapshotGenerated;
            }
            else
            {
                netState.OnValueChanged -= HandleNetworkStateChanged;
            }
        }

        private void HandleSnapshotGenerated(CarNetworkSnapshot snapshot)
        {
            // Update the NetworkVariable so all other clients receive this snapshot
            netState.Value = snapshot;
        }

        private void HandleNetworkStateChanged(CarNetworkSnapshot previous, CarNetworkSnapshot current)
        {
            // Feed snapshot into the interpolation buffer on remote client
            networkSync.ReceiveNetworkSnapshot(current);
        }
    }
#else
    /// <summary>
    /// Standalone placeholder when com.unity.netcode.gameobjects is not yet imported.
    /// Once you add Netcode for GameObjects from Package Manager, define UNITY_NETCODE_GAMEOBJECTS
    /// in Project Settings -> Player -> Scripting Define Symbols to activate this script.
    /// </summary>
    public class NetcodeCarAdapter : MonoBehaviour
    {
        [Tooltip("Install 'com.unity.netcode.gameobjects' in Package Manager to activate full Unity Netcode integration.")]
        [TextArea(3, 6)]
        public string instructions = "To enable Netcode for GameObjects integration:\n" +
                                     "1. Open Window -> Package Manager\n" +
                                     "2. Install 'Netcode for GameObjects'\n" +
                                     "3. Add 'UNITY_NETCODE_GAMEOBJECTS' to Scripting Define Symbols in Player Settings.";
    }
#endif
}
