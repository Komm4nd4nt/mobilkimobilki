using System;
using System.Collections.Generic;
using UnityEngine;
using RacingMobile.Core;

namespace RacingMobile.Vehicle
{
    public enum DriveTrainType
    {
        AllWheelDrive,
        RearWheelDrive,
        FrontWheelDrive
    }

    /// <summary>
    /// Core vehicle physics controller handling motor torque, braking, steering,
    /// aerodynamics, anti-roll bars, and drift mechanics.
    /// Fully decoupled from input and network simulation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarPhysicsController : MonoBehaviour
    {
        [Header("Wheel Axles")]
        [SerializeField] private WheelAxle frontAxle = new WheelAxle { AxleName = "Front Axle", IsMotor = true, IsSteering = true, IsHandbrake = false, AntiRollForce = 6000f, VisualRotationOffset = new Vector3(0f, 0f, -90f) };
        [SerializeField] private WheelAxle rearAxle = new WheelAxle { AxleName = "Rear Axle", IsMotor = true, IsSteering = false, IsHandbrake = true, AntiRollForce = 6000f, VisualRotationOffset = new Vector3(0f, 0f, -90f) };

        [Header("Engine & Performance")]
        [SerializeField] private DriveTrainType driveType = DriveTrainType.AllWheelDrive;
        [Tooltip("Peak engine torque in Nm")]
        [SerializeField] private float maxMotorTorque = 2200f;
        [Tooltip("Max forward speed in km/h")]
        [SerializeField] private float maxSpeedKmH = 210f;
        [Tooltip("Max reverse speed in km/h")]
        [SerializeField] private float maxReverseSpeedKmH = 45f;
        [Tooltip("Reverse torque in Nm")]
        [SerializeField] private float reverseTorque = 1200f;
        [Tooltip("Nitrous / Boost acceleration multiplier")]
        [SerializeField] private float boostMultiplier = 1.6f;

        [Header("Braking & Drifting")]
        [Tooltip("Service brake torque in Nm")]
        [SerializeField] private float maxBrakeTorque = 4000f;
        [Tooltip("Handbrake torque in Nm applied to rear wheels")]
        [SerializeField] private float maxHandbrakeTorque = 6000f;
        [Tooltip("Normal sideways grip stiffness of rear tires")]
        [SerializeField] private float normalRearGrip = 1.0f;
        [Tooltip("Sideways grip stiffness during handbrake drift")]
        [SerializeField] private float driftRearGrip = 0.42f;

        [Header("Steering Dynamics")]
        [Tooltip("Steering angle at low speeds (degrees)")]
        [SerializeField] private float maxSteerAngle = 35f;
        [Tooltip("Steering angle at high speeds to prevent violent spinouts on mobile (degrees)")]
        [SerializeField] private float highSpeedSteerAngle = 14f;
        [Tooltip("Speed in km/h at which steering reaches high speed limit")]
        [SerializeField] private float highSpeedSteerThreshold = 140f;
        [Tooltip("Rate of steering angle change in degrees per second")]
        [SerializeField] private float steerResponseSpeed = 120f;

        [Header("Aerodynamics & Stability")]
        [Tooltip("Downforce coefficient pushing the car into the track at high speeds")]
        [SerializeField] private float downforceCoefficient = 60f;
        [Tooltip("Center of mass offset. Lowering Y improves roll stability dramatically")]
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0.05f);

        [Header("Multiplayer & Control Mode")]
        [Tooltip("When true, car is driven by local player inputs. When false, state is driven by network sync")]
        [SerializeField] private bool isLocallyControlled = true;

        // Runtime State
        private Rigidbody rb;
        private CarInputState currentInput;
        private float currentSteerAngle;
        private float targetSteerAngle;
        private float currentSpeedKmH;
        private bool isReversing;
        private bool isDrifting;
        private float averageWheelRPM;

        // Public Properties for HUD, Sound, and Multiplayer Sync
        public bool IsLocallyControlled
        {
            get => isLocallyControlled;
            set
            {
                isLocallyControlled = value;
                if (!isLocallyControlled)
                {
                    // Clear inputs and wheel forces on remote opponent cars
                    currentInput = CarInputState.Empty;
                    currentSteerAngle = 0f;
                    targetSteerAngle = 0f;

                    if (frontAxle.LeftWheelCollider != null) { frontAxle.LeftWheelCollider.motorTorque = 0f; frontAxle.LeftWheelCollider.brakeTorque = 0f; frontAxle.LeftWheelCollider.steerAngle = 0f; }
                    if (frontAxle.RightWheelCollider != null) { frontAxle.RightWheelCollider.motorTorque = 0f; frontAxle.RightWheelCollider.brakeTorque = 0f; frontAxle.RightWheelCollider.steerAngle = 0f; }
                    if (rearAxle.LeftWheelCollider != null) { rearAxle.LeftWheelCollider.motorTorque = 0f; rearAxle.LeftWheelCollider.brakeTorque = 0f; }
                    if (rearAxle.RightWheelCollider != null) { rearAxle.RightWheelCollider.motorTorque = 0f; rearAxle.RightWheelCollider.brakeTorque = 0f; }
                }
            }
        }

        public float CurrentSpeedKmH => currentSpeedKmH;
        public float TargetSteerAngle => targetSteerAngle;
        public float CurrentSteerAngle => currentSteerAngle;
        public bool IsDrifting => isDrifting;
        public bool IsReversing => isReversing;
        public float AverageWheelRPM => averageWheelRPM;
        public Rigidbody Rigidbody => rb;
        public CarInputState CurrentInput => currentInput;
        public WheelAxle FrontAxle => frontAxle;
        public WheelAxle RearAxle => rearAxle;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ApplyCenterOfMass();
        }

        private void Start()
        {
            ApplyCenterOfMass();
        }

        public void ApplyCenterOfMass()
        {
            if (rb != null)
            {
                rb.centerOfMass = centerOfMassOffset;
            }
        }

        /// <summary>
        /// External entry point for setting vehicle inputs (Mobile UI, AI, Keyboard, Network).
        /// </summary>
        public void SetInput(CarInputState input)
        {
            currentInput = input;
        }

        private void Update()
        {
            // Visual wheel transforms are updated here for local player only.
            // Remote opponent visual wheels are synchronized by NetworkCarController.
            if (!isLocallyControlled) return;

            frontAxle.UpdateVisuals();
            rearAxle.UpdateVisuals();
        }

        private void FixedUpdate()
        {
            CalculateSpeedAndRPM();

            if (!isLocallyControlled)
            {
                // In remote multiplayer mode, physics/transforms are handled by CarNetworkSync
                return;
            }

            ApplySteering();
            ApplyMotorAndBrakes();
            ApplyAntiRollBars();
            ApplyAerodynamics();
        }

        private void CalculateSpeedAndRPM()
        {
            if (rb == null) return;

            // Forward speed in km/h (positive = forward, negative = reverse)
            float forwardVelocity = Vector3.Dot(rb.linearVelocity, transform.forward);
            currentSpeedKmH = forwardVelocity * 3.6f;

            // Calculate average wheel RPM for audio / network sync
            float totalRPM = 0f;
            int wheelCount = 0;

            if (frontAxle.LeftWheelCollider != null) { totalRPM += frontAxle.LeftWheelCollider.rpm; wheelCount++; }
            if (frontAxle.RightWheelCollider != null) { totalRPM += frontAxle.RightWheelCollider.rpm; wheelCount++; }
            if (rearAxle.LeftWheelCollider != null) { totalRPM += rearAxle.LeftWheelCollider.rpm; wheelCount++; }
            if (rearAxle.RightWheelCollider != null) { totalRPM += rearAxle.RightWheelCollider.rpm; wheelCount++; }

            averageWheelRPM = wheelCount > 0 ? totalRPM / wheelCount : 0f;
        }

        private void ApplySteering()
        {
            // Speed-sensitive steering reduction for crisp mobile controls
            float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeedKmH) / Mathf.Max(1f, highSpeedSteerThreshold));
            float dynamicMaxSteer = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedFactor);

            targetSteerAngle = currentInput.Steer * dynamicMaxSteer;
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, steerResponseSpeed * Time.fixedDeltaTime);

            if (frontAxle.LeftWheelCollider != null && frontAxle.IsSteering)
            {
                frontAxle.LeftWheelCollider.steerAngle = currentSteerAngle;
            }
            if (frontAxle.RightWheelCollider != null && frontAxle.IsSteering)
            {
                frontAxle.RightWheelCollider.steerAngle = currentSteerAngle;
            }
        }

        private void ApplyMotorAndBrakes()
        {
            float throttleInput = Mathf.Clamp01(currentInput.Throttle);
            float brakeInput = Mathf.Clamp01(currentInput.Brake);
            bool handbrake = currentInput.Handbrake;

            // Boost multiplier
            if (currentInput.Boost)
            {
                throttleInput *= boostMultiplier;
            }

            // Automatic Arcade Reverse Logic for Mobile:
            // If car is almost stopped (< 3 km/h) and brake pedal is held, engage reverse gear
            if (brakeInput > 0.1f && currentSpeedKmH < 3f && throttleInput < 0.1f)
            {
                isReversing = true;
            }
            else if (throttleInput > 0.1f && currentSpeedKmH > -3f)
            {
                isReversing = false;
            }

            float appliedMotorTorque = 0f;
            float appliedBrakeTorque = 0f;

            if (isReversing)
            {
                // Brake pedal acts as reverse throttle
                if (Mathf.Abs(currentSpeedKmH) < maxReverseSpeedKmH)
                {
                    appliedMotorTorque = -reverseTorque * brakeInput;
                }
                // Gas pedal acts as brake when moving backward
                appliedBrakeTorque = throttleInput * maxBrakeTorque;
            }
            else
            {
                // Forward drive: scale torque down as speed approaches max speed
                float speedLimitRatio = Mathf.Clamp01((maxSpeedKmH - currentSpeedKmH) / Mathf.Max(10f, maxSpeedKmH * 0.2f));
                appliedMotorTorque = maxMotorTorque * throttleInput * speedLimitRatio;
                appliedBrakeTorque = maxBrakeTorque * brakeInput;
            }

            // Distribute motor torque to driven axles
            int drivenWheelCount = 0;
            switch (driveType)
            {
                case DriveTrainType.AllWheelDrive: drivenWheelCount = 4; break;
                case DriveTrainType.RearWheelDrive: drivenWheelCount = 2; break;
                case DriveTrainType.FrontWheelDrive: drivenWheelCount = 2; break;
            }

            float torquePerWheel = appliedMotorTorque / Mathf.Max(1, drivenWheelCount);

            // Apply to Front Wheels
            if (driveType == DriveTrainType.AllWheelDrive || driveType == DriveTrainType.FrontWheelDrive)
            {
                SetMotorTorque(frontAxle.LeftWheelCollider, torquePerWheel);
                SetMotorTorque(frontAxle.RightWheelCollider, torquePerWheel);
            }
            else
            {
                SetMotorTorque(frontAxle.LeftWheelCollider, 0f);
                SetMotorTorque(frontAxle.RightWheelCollider, 0f);
            }

            // Apply to Rear Wheels
            if (driveType == DriveTrainType.AllWheelDrive || driveType == DriveTrainType.RearWheelDrive)
            {
                SetMotorTorque(rearAxle.LeftWheelCollider, torquePerWheel);
                SetMotorTorque(rearAxle.RightWheelCollider, torquePerWheel);
            }
            else
            {
                SetMotorTorque(rearAxle.LeftWheelCollider, 0f);
                SetMotorTorque(rearAxle.RightWheelCollider, 0f);
            }

            // Service braking (front biased 60% front, 40% rear for stable deceleration)
            float frontBrake = appliedBrakeTorque * 0.6f;
            float rearBrake = appliedBrakeTorque * 0.4f;

            SetBrakeTorque(frontAxle.LeftWheelCollider, frontBrake);
            SetBrakeTorque(frontAxle.RightWheelCollider, frontBrake);

            // Handbrake & Drift Handling on rear axle
            if (handbrake)
            {
                rearBrake = Mathf.Max(rearBrake, maxHandbrakeTorque);
                isDrifting = Mathf.Abs(currentSpeedKmH) > 15f;
                rearAxle.SetSidewaysStiffness(driftRearGrip);
            }
            else
            {
                isDrifting = false;
                rearAxle.SetSidewaysStiffness(normalRearGrip);
            }

            SetBrakeTorque(rearAxle.LeftWheelCollider, rearBrake);
            SetBrakeTorque(rearAxle.RightWheelCollider, rearBrake);
        }

        private void SetMotorTorque(WheelCollider col, float torque)
        {
            if (col != null) col.motorTorque = torque;
        }

        private void SetBrakeTorque(WheelCollider col, float torque)
        {
            if (col != null) col.brakeTorque = torque;
        }

        private void ApplyAntiRollBars()
        {
            frontAxle.ApplyAntiRoll(rb);
            rearAxle.ApplyAntiRoll(rb);
        }

        private void ApplyAerodynamics()
        {
            if (rb == null) return;

            // Downforce = 0.5 * airDensity * downforceCoeff * velocity^2
            // Pushes car downwards into the track proportionally to speed for high-speed cornering grip
            float speedMetersPerSec = rb.linearVelocity.magnitude;
            float downforce = downforceCoefficient * (speedMetersPerSec * speedMetersPerSec * 0.05f);
            rb.AddForce(-transform.up * downforce, ForceMode.Force);
        }

        /// <summary>
        /// Recovers the car if it gets flipped upside down.
        /// </summary>
        public void ResetOrientation()
        {
            if (rb == null) return;

            transform.position += Vector3.up * 1.5f;
            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize Center of Mass in Editor
            Gizmos.color = Color.yellow;
            Vector3 com = transform.TransformPoint(centerOfMassOffset);
            Gizmos.DrawSphere(com, 0.15f);
            Gizmos.DrawLine(com, com + transform.up * 0.5f);
        }
    }
}
