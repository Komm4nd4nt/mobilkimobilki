using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RacingMobile.Core;

namespace RacingMobile.Multiplayer
{
    /// <summary>
    /// Test utility that simulates network transmission between a local car and a remote car.
    /// Allows testing multiplayer interpolation, latency (ping), and packet jitter right inside the Editor!
    /// </summary>
    public class MultiplayerTestBridge : MonoBehaviour
    {
        [Header("Car References")]
        [SerializeField] private CarNetworkSync localPlayerCar;
        [SerializeField] private CarNetworkSync remoteOpponentCar;

        [Header("Network Simulation Parameters")]
        [Tooltip("Simulated one-way latency in milliseconds (e.g. 80ms ping)")]
        [SerializeField] private float simulatedLatencyMs = 80f;

        [Tooltip("Simulated packet drop rate (0% to 100%)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float simulatedPacketLoss = 0.05f;

        private void Start()
        {
            if (localPlayerCar != null)
            {
                localPlayerCar.OnSnapshotGenerated += HandleLocalSnapshot;
            }
        }

        private void OnDestroy()
        {
            if (localPlayerCar != null)
            {
                localPlayerCar.OnSnapshotGenerated -= HandleLocalSnapshot;
            }
        }

        private void HandleLocalSnapshot(CarNetworkSnapshot snapshot)
        {
            if (remoteOpponentCar == null) return;

            // Simulate packet loss
            if (Random.value < simulatedPacketLoss)
            {
                return; // Dropped packet
            }

            // Simulate network latency delay
            StartCoroutine(DeliverSnapshotDelayed(snapshot, simulatedLatencyMs / 1000f));
        }

        private IEnumerator DeliverSnapshotDelayed(CarNetworkSnapshot snapshot, float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            if (remoteOpponentCar != null)
            {
                remoteOpponentCar.ReceiveNetworkSnapshot(snapshot);
            }
        }
    }
}
