using System;
using System.Collections.Generic;
using UnityEngine;
using RacingMobile.Core;
using RacingMobile.Vehicle;

namespace RacingMobile.Multiplayer
{
    /// <summary>
    /// Network synchronization module for vehicle physics.
    /// Handles snapshot capture for local vehicle, and smooth snapshot interpolation /
    /// dead reckoning for remote multiplayer opponents.
    /// Compatible with Unity Netcode, Photon Fusion, Mirror, or custom UDP sockets.
    /// </summary>
    [RequireComponent(typeof(CarPhysicsController))]
    public class CarNetworkSync : MonoBehaviour
    {
        [Header("Vehicle Reference")]
        [SerializeField] private CarPhysicsController car;

        [Header("Network Interpolation Settings")]
        [Tooltip("Interpolation delay in seconds (buffer time to smooth out network jitter)")]
        [SerializeField] private float interpolationDelay = 0.1f;
        [Tooltip("Position snap threshold in meters (teleports instead of sliding if discrepancy is huge)")]
        [SerializeField] private float teleportDistanceThreshold = 10f;
        [Tooltip("Extrapolation limit in seconds during packet loss")]
        [SerializeField] private float maxExtrapolationTime = 0.5f;

        [Header("Snapshot Rate")]
        [Tooltip("How many snapshots to send per second when locally controlled")]
        [SerializeField] private int sendRate = 20;

        // Snapshot buffer for remote vehicle smoothing
        private readonly List<CarNetworkSnapshot> snapshotBuffer = new List<CarNetworkSnapshot>();
        private float lastSendTime;
        private Rigidbody rb;

        // Event triggered when a new snapshot is generated (hook for Netcode / Photon RPCs)
        public event Action<CarNetworkSnapshot> OnSnapshotGenerated;

        private void Awake()
        {
            if (car == null) car = GetComponent<CarPhysicsController>();
            rb = car.GetComponent<Rigidbody>();
        }

        private void Start()
        {
            ConfigureForLocalOrRemote();
        }

        /// <summary>
        /// Configures physics and colliders based on whether this car is local or remote.
        /// </summary>
        public void ConfigureForLocalOrRemote()
        {
            if (car == null || rb == null) return;

            if (car.IsLocallyControlled)
            {
                // Local player: full active physics simulation
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            else
            {
                // Remote opponent: kinematic movement driven by snapshot interpolation
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }

        private void Update()
        {
            if (car.IsLocallyControlled)
            {
                // Capture snapshots at fixed network tick rate
                if (Time.time - lastSendTime >= 1f / sendRate)
                {
                    lastSendTime = Time.time;
                    CarNetworkSnapshot snapshot = CreateCurrentSnapshot();
                    OnSnapshotGenerated?.Invoke(snapshot);
                }
            }
            else
            {
                // Remote vehicle: Interpolate position, rotation, and wheel visuals
                InterpolateRemoteVehicle();
            }
        }

        /// <summary>
        /// Creates a network snapshot from the current physical state of the vehicle.
        /// </summary>
        public CarNetworkSnapshot CreateCurrentSnapshot()
        {
            return new CarNetworkSnapshot
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
        }

        /// <summary>
        /// Called when a snapshot arrives from the network for this remote car.
        /// </summary>
        public void ReceiveNetworkSnapshot(CarNetworkSnapshot snapshot)
        {
            if (car.IsLocallyControlled) return;

            // Insert snapshot sorted by timestamp
            snapshotBuffer.Add(snapshot);

            // Keep buffer size reasonable (keep last 30 snapshots)
            if (snapshotBuffer.Count > 30)
            {
                snapshotBuffer.RemoveAt(0);
            }
        }

        private void InterpolateRemoteVehicle()
        {
            if (snapshotBuffer.Count == 0) return;

            double renderTime = Time.timeAsDouble - interpolationDelay;

            // 1. If render time is before our earliest snapshot, use earliest
            if (renderTime <= snapshotBuffer[0].Timestamp)
            {
                ApplySnapshotState(snapshotBuffer[0]);
                return;
            }

            // 2. If render time is after latest snapshot, extrapolate with velocity (Dead Reckoning)
            CarNetworkSnapshot latest = snapshotBuffer[snapshotBuffer.Count - 1];
            if (renderTime > latest.Timestamp)
            {
                float timeSinceLatest = (float)(renderTime - latest.Timestamp);
                if (timeSinceLatest <= maxExtrapolationTime)
                {
                    // Linear extrapolation
                    Vector3 extrapolatedPos = latest.Position + latest.Velocity * timeSinceLatest;
                    Quaternion extrapolatedRot = latest.Rotation * Quaternion.Euler(latest.AngularVelocity * Mathf.Rad2Deg * timeSinceLatest);

                    CheckTeleportOrLerp(extrapolatedPos, extrapolatedRot);
                }
                else
                {
                    ApplySnapshotState(latest);
                }
                return;
            }

            // 3. Find the two surrounding snapshots for interpolation
            for (int i = 0; i < snapshotBuffer.Count - 1; i++)
            {
                CarNetworkSnapshot prev = snapshotBuffer[i];
                CarNetworkSnapshot next = snapshotBuffer[i + 1];

                if (prev.Timestamp <= renderTime && renderTime <= next.Timestamp)
                {
                    float t = (float)((renderTime - prev.Timestamp) / (next.Timestamp - prev.Timestamp));
                    t = Mathf.Clamp01(t);

                    Vector3 targetPos = Vector3.Lerp(prev.Position, next.Position, t);
                    Quaternion targetRot = Quaternion.Slerp(prev.Rotation, next.Rotation, t);
                    float targetSteer = Mathf.Lerp(prev.SteerAngle, next.SteerAngle, t);

                    CheckTeleportOrLerp(targetPos, targetRot);

                    // Update steering visual angles for remote car
                    ApplyRemoteWheelVisuals(targetSteer, next.WheelRPM);
                    return;
                }
            }
        }

        private void CheckTeleportOrLerp(Vector3 targetPos, Quaternion targetRot)
        {
            if (Vector3.Distance(transform.position, targetPos) > teleportDistanceThreshold)
            {
                // Distance is too large (teleport / spawn)
                transform.position = targetPos;
                transform.rotation = targetRot;
            }
            else
            {
                // Smooth movement
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 25f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 25f);
            }
        }

        private void ApplySnapshotState(CarNetworkSnapshot snap)
        {
            CheckTeleportOrLerp(snap.Position, snap.Rotation);
            ApplyRemoteWheelVisuals(snap.SteerAngle, snap.WheelRPM);
        }

        private void ApplyRemoteWheelVisuals(float steerAngle, float wheelRPM)
        {
            // Rotate front wheel visuals along steer axis with Z = -90 orientation offset
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

        /// <summary>
        /// Clears buffered snapshots (e.g. on respawn or round reset).
        /// </summary>
        public void ClearBuffer()
        {
            snapshotBuffer.Clear();
        }
    }
}
