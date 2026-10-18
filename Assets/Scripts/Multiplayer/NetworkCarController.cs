using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using RacingMobile.Core;
using RacingMobile.Vehicle;
using RacingMobile.MobileUI;
using RacingMobile.Camera;

namespace RacingMobile.Multiplayer
{
    /// <summary>
    /// Core Netcode for GameObjects component for racing vehicle synchronization.
    /// Manages ownership, physics activation, mobile input linking, camera attachment,
    /// and snapshot interpolation buffer for remote multiplayer opponents.
    /// </summary>
    [RequireComponent(typeof(CarPhysicsController), typeof(NetworkObject))]
    public class NetworkCarController : NetworkBehaviour
    {
        [Header("Vehicle Reference")]
        [SerializeField] private CarPhysicsController car;

        [Header("Network Sync Settings")]
        [Tooltip("How many snapshots to send per second from owner")]
        [SerializeField] private int snapshotSendRate = 25;

        [Tooltip("Time delay buffer in seconds to smooth over mobile network jitter (e.g. 0.08s - 0.12s)")]
        [SerializeField] private float interpolationBufferDelay = 0.09f;

        [Tooltip("Distance in meters above which the car will instantly snap instead of lerping")]
        [SerializeField] private float snapDistanceThreshold = 12f;

        [Tooltip("Max seconds to extrapolate position during mobile packet loss")]
        [SerializeField] private float maxExtrapolationTime = 0.4f;

        // Synchronized Netcode state
        private readonly NetworkVariable<CarNetworkSnapshot> netState = new NetworkVariable<CarNetworkSnapshot>(
            writePerm: NetworkVariableWritePermission.Owner,
            readPerm: NetworkVariableReadPermission.Everyone
        );

        private Rigidbody rb;
        private float lastSendTime;
        private bool hasReceivedSnapshot;

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 targetVelocity;
        private float targetSteerAngle;
        private float targetWheelRPM;

        private void Awake()
        {
            if (car == null) car = GetComponent<CarPhysicsController>();
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // Initialize target transforms to current transform to prevent snapping to Vector3.zero
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                car.IsLocallyControlled = false;
                if (rb != null) rb.isKinematic = true;
                SetWheelCollidersEnabled(false);
                return;
            }

            // Race track scene loaded (e.g. SampleScene)
            if (IsServer || IsOwner)
            {
                PositionCarOnStartingGrid();
            }

            if (IsOwner)
            {
                SetupLocalPlayer();
                StartCoroutine(DeferredSetupLocalPlayer());
            }
            else
            {
                SetupRemoteOpponent();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Only disable if this was an in-scene placed placeholder (not dynamically spawned)
            if (NetworkObject != null && NetworkObject.IsSceneObject == true)
            {
                gameObject.SetActive(false);
                return;
            }

            // If spawned while still in MainMenu, keep stationary / kinematic
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
            {
                car.IsLocallyControlled = false;
                if (rb != null) rb.isKinematic = true;
                SetWheelCollidersEnabled(false);
                return;
            }

            if (IsServer || IsOwner)
            {
                PositionCarOnStartingGrid();
            }

            if (IsOwner)
            {
                SetupLocalPlayer();
                StartCoroutine(DeferredSetupLocalPlayer());
            }
            else
            {
                SetupRemoteOpponent();
            }
        }

        private System.Collections.IEnumerator DeferredSetupLocalPlayer()
        {
            yield return null;
            if (IsOwner) SetupLocalPlayer();
            yield return new WaitForEndOfFrame();
            if (IsOwner) SetupLocalPlayer();
        }

        public void PositionCarOnStartingGrid()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
            {
                return;
            }

            // Determine slot index from Lobby player list or OwnerClientId
            int slotIndex = 0;
            if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.CurrentPlayers.Count > 0)
            {
                var list = NetworkLobbyManager.Instance.CurrentPlayers;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].clientId == OwnerClientId)
                    {
                        slotIndex = i;
                        break;
                    }
                }
            }
            else
            {
                slotIndex = (int)(OwnerClientId % 4);
            }

            int slotNumber = (slotIndex % 4) + 1;
            GameObject slotObj = GameObject.Find($"GridSlot_{slotNumber}");

            Vector3 spawnPos;
            Quaternion spawnRot;

            if (slotObj != null)
            {
                spawnPos = slotObj.transform.position + Vector3.up * 0.55f;
                spawnRot = slotObj.transform.rotation;
            }
            else
            {
                float xOffset = (slotIndex % 2 == 0) ? -3.5f : 3.5f;
                float zOffset = (slotIndex / 2) * 10f;
                spawnPos = new Vector3(xOffset, 0.55f, -zOffset);
                spawnRot = Quaternion.identity;
            }

            transform.position = spawnPos;
            transform.rotation = spawnRot;

            if (rb != null)
            {
                rb.position = spawnPos;
                rb.rotation = spawnRot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            targetPosition = spawnPos;
            targetRotation = spawnRot;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsOwner)
            {
                netState.OnValueChanged -= HandleRemoteSnapshotReceived;
            }
        }

        public void SetupLocalPlayer()
        {
            // 1. Enable local physical control
            car.IsLocallyControlled = true;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // Enable WheelColliders for active local physics
            SetWheelCollidersEnabled(true);

            // 2. Connect Mobile UI input manager
            MobileInputManager inputMgr = FindFirstObjectByType<MobileInputManager>();
            if (inputMgr != null)
            {
                inputMgr.SetTargetCar(car);
            }

            // 3. Connect Speedometer HUD
            SpeedometerUI hud = FindFirstObjectByType<SpeedometerUI>();
            if (hud != null)
            {
                hud.SetTargetCar(car);
            }

            // 4. Connect Camera to follow this local vehicle immediately
            SmoothFollowCamera cam = SmoothFollowCamera.Instance ?? FindFirstObjectByType<SmoothFollowCamera>();
            if (cam != null)
            {
                cam.SetTarget(transform, snapImmediately: true);
            }
        }

        public void SetupRemoteOpponent()
        {
            // 1. Disable local control and set Rigidbody to kinematic
            car.IsLocallyControlled = false;
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }

            // 2. Disable WheelColliders on remote vehicle so they don't fight kinematic movement or glitch in PhysX
            SetWheelCollidersEnabled(false);

            // 3. Ensure local input manager and camera NEVER target this remote car!
            MobileInputManager inputMgr = FindFirstObjectByType<MobileInputManager>();
            if (inputMgr != null && inputMgr.TargetCar == car)
            {
                inputMgr.SetTargetCar(null);
            }

            SmoothFollowCamera cam = SmoothFollowCamera.Instance ?? FindFirstObjectByType<SmoothFollowCamera>();
            if (cam != null && cam.Target == transform)
            {
                cam.SetTarget(null);
            }

            targetPosition = transform.position;
            targetRotation = transform.rotation;
            hasReceivedSnapshot = false;

            // 4. Listen for incoming Netcode state changes
            netState.OnValueChanged -= HandleRemoteSnapshotReceived;
            netState.OnValueChanged += HandleRemoteSnapshotReceived;

            if (netState.Value.Timestamp > 0)
            {
                HandleRemoteSnapshotReceived(netState.Value, netState.Value);
            }
        }

        private void SetWheelCollidersEnabled(bool isEnabled)
        {
            if (car == null) return;

            if (car.FrontAxle != null)
            {
                if (car.FrontAxle.LeftWheelCollider != null) car.FrontAxle.LeftWheelCollider.enabled = isEnabled;
                if (car.FrontAxle.RightWheelCollider != null) car.FrontAxle.RightWheelCollider.enabled = isEnabled;
            }

            if (car.RearAxle != null)
            {
                if (car.RearAxle.LeftWheelCollider != null) car.RearAxle.LeftWheelCollider.enabled = isEnabled;
                if (car.RearAxle.RightWheelCollider != null) car.RearAxle.RightWheelCollider.enabled = isEnabled;
            }
        }

        private void Update()
        {
            // If not spawned on network yet (standalone testing, editor playmode),
            // leave vehicle to normal local physics and do NOT interpolate or overwrite transform!
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                // Send state snapshots at network tick rate
                if (Time.time - lastSendTime >= 1f / snapshotSendRate)
                {
                    lastSendTime = Time.time;
                    BroadcastLocalSnapshot();
                }
            }
            else
            {
                // Interpolate remote opponent
                InterpolateRemoteCar();
            }
        }

        private void BroadcastLocalSnapshot()
        {
            CarNetworkSnapshot snapshot = new CarNetworkSnapshot
            {
                Timestamp = Time.timeAsDouble,
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = rb != null ? rb.linearVelocity : Vector3.zero,
                AngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero,
                SteerAngle = car.CurrentSteerAngle,
                WheelRPM = car.AverageWheelRPM,
                SpeedKmH = car.CurrentSpeedKmH,
                Handbrake = car.CurrentInput.Handbrake
            };

            netState.Value = snapshot;
        }

        private void HandleRemoteSnapshotReceived(CarNetworkSnapshot prev, CarNetworkSnapshot current)
        {
            targetPosition = current.Position;
            targetRotation = current.Rotation;
            targetVelocity = current.Velocity;
            targetSteerAngle = current.SteerAngle;
            targetWheelRPM = current.WheelRPM;
            hasReceivedSnapshot = true;
        }

        private void InterpolateRemoteCar()
        {
            if (!hasReceivedSnapshot) return;

            float dist = Vector3.Distance(transform.position, targetPosition);
            if (dist > snapDistanceThreshold)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
            else
            {
                // Smooth interpolation with dead reckoning prediction
                // Predict horizontal velocity to prevent vertical bounce spikes from erratic suspension velocity
                Vector3 horizontalVel = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
                Vector3 predictedPos = targetPosition + horizontalVel * Time.deltaTime;
                predictedPos.y = targetPosition.y;

                transform.position = Vector3.Lerp(transform.position, predictedPos, Time.deltaTime * 20f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 20f);
            }

            ApplyWheelVisuals(targetSteerAngle, targetWheelRPM);
        }

        private void ApplyWheelVisuals(float steerAngle, float wheelRPM)
        {
            // Update steering angle and preserve visual mesh orientation offset (e.g. Z = -90)
            Vector3 offset = (car != null && car.FrontAxle != null) ? car.FrontAxle.VisualRotationOffset : new Vector3(0f, 0f, -90f);
            Quaternion steerRot = Quaternion.Euler(offset.x, steerAngle + offset.y, offset.z);

            if (car != null)
            {
                if (car.FrontAxle != null)
                {
                    if (car.FrontAxle.LeftWheelVisual != null) car.FrontAxle.LeftWheelVisual.localRotation = steerRot;
                    if (car.FrontAxle.RightWheelVisual != null) car.FrontAxle.RightWheelVisual.localRotation = steerRot;
                }
                if (car.RearAxle != null)
                {
                    if (car.RearAxle.LeftWheelVisual != null) car.RearAxle.LeftWheelVisual.localRotation = Quaternion.Euler(offset);
                    if (car.RearAxle.RightWheelVisual != null) car.RearAxle.RightWheelVisual.localRotation = Quaternion.Euler(offset);
                }
            }
        }
    }
}
