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

        // Snapshot buffer for remote car smoothing
        private readonly List<CarNetworkSnapshot> snapshotBuffer = new List<CarNetworkSnapshot>();
        private Rigidbody rb;
        private float lastSendTime;

        private void Awake()
        {
            if (car == null) car = GetComponent<CarPhysicsController>();
            rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                SetupLocalPlayer();
            }
            else
            {
                SetupRemoteOpponent();
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsOwner)
            {
                netState.OnValueChanged -= HandleRemoteSnapshotReceived;
            }
        }

        private void SetupLocalPlayer()
        {
            // 1. Enable local physical control
            car.IsLocallyControlled = true;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

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

            // 4. Connect Camera to follow this local vehicle
            SmoothFollowCamera cam = FindFirstObjectByType<SmoothFollowCamera>();
            if (cam != null)
            {
                cam.SetTarget(transform);
            }
        }

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 targetVelocity;
        private float targetSteerAngle;
        private float targetWheelRPM;

        private void SetupRemoteOpponent()
        {
            // 1. Disable local control and set Rigidbody to kinematic
            car.IsLocallyControlled = false;
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }

            // 2. Ensure local input manager and camera NEVER target this remote car!
            MobileInputManager inputMgr = FindFirstObjectByType<MobileInputManager>();
            if (inputMgr != null && inputMgr.TargetCar == car)
            {
                inputMgr.SetTargetCar(null);
            }

            SmoothFollowCamera cam = FindFirstObjectByType<SmoothFollowCamera>();
            if (cam != null && cam.Target == transform)
            {
                cam.SetTarget(null);
            }

            targetPosition = transform.position;
            targetRotation = transform.rotation;

            // 3. Listen for incoming Netcode state changes
            netState.OnValueChanged += HandleRemoteSnapshotReceived;

            if (netState.Value.Timestamp > 0)
            {
                HandleRemoteSnapshotReceived(netState.Value, netState.Value);
            }
        }

        private void Update()
        {
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
                // Interpolate remote opponent with snapshot buffer
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
        }

        private void InterpolateRemoteCar()
        {
            float dist = Vector3.Distance(transform.position, targetPosition);
            if (dist > snapDistanceThreshold)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
            else
            {
                // Smooth interpolation with dead reckoning prediction
                Vector3 predictedPos = targetPosition + targetVelocity * Time.deltaTime;
                transform.position = Vector3.Lerp(transform.position, predictedPos, Time.deltaTime * 22f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 22f);
            }

            ApplyWheelVisuals(targetSteerAngle, targetWheelRPM);
        }

        private void ApplyWheelVisuals(float steerAngle, float wheelRPM)
        {
            // Update steering angle and preserve visual mesh orientation offset (e.g. Z = -90)
            Vector3 offset = (car != null && car.FrontAxle != null) ? car.FrontAxle.VisualRotationOffset : new Vector3(0f, 0f, -90f);
            Quaternion steerRot = Quaternion.Euler(offset.x, steerAngle + offset.y, offset.z);

            if (car != null && car.FrontAxle.LeftWheelVisual != null)
            {
                car.FrontAxle.LeftWheelVisual.localRotation = steerRot;
            }
            if (car != null && car.FrontAxle.RightWheelVisual != null)
            {
                car.FrontAxle.RightWheelVisual.localRotation = steerRot;
            }
        }
    }
}
