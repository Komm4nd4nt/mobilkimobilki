using System;
using UnityEngine;

namespace RacingMobile.Vehicle
{
    /// <summary>
    /// Represents a vehicle axle consisting of a left and right wheel pair.
    /// Handles WheelCollider to visual mesh transformation and axle-specific behavior.
    /// </summary>
    [Serializable]
    public class WheelAxle
    {
        [Header("Axle Name")]
        public string AxleName = "Axle";

        [Header("Wheel Colliders")]
        public WheelCollider LeftWheelCollider;
        public WheelCollider RightWheelCollider;

        [Header("Visual Meshes")]
        public Transform LeftWheelVisual;
        public Transform RightWheelVisual;

        [Header("Axle Properties")]
        public bool IsMotor = true;         // Receives engine torque
        public bool IsSteering = false;     // Turns when steering
        public bool IsHandbrake = false;    // Affected by handbrake (rear axle)

        [Header("Anti-Roll Bar")]
        public float AntiRollForce = 5000f; // Stabilizes against roll-over in sharp turns

        [Header("Visual Orientation")]
        [Tooltip("Euler angle offset applied to the visual wheel mesh. Default is (0, 0, -90) so cylinder wheels lie flat on the axle.")]
        public Vector3 VisualRotationOffset = new Vector3(0f, 0f, -90f);

        /// <summary>
        /// Synchronizes the visual wheel mesh with the WheelCollider's physics pose.
        /// </summary>
        public void UpdateVisuals()
        {
            UpdateSingleWheelVisual(LeftWheelCollider, LeftWheelVisual);
            UpdateSingleWheelVisual(RightWheelCollider, RightWheelVisual);
        }

        private void UpdateSingleWheelVisual(WheelCollider col, Transform visual)
        {
            if (col == null || visual == null) return;

            col.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visual.position = position;
            visual.rotation = rotation * Quaternion.Euler(VisualRotationOffset);
        }

        /// <summary>
        /// Applies anti-roll bar physics to keep both wheels on the ground and prevent rollovers.
        /// </summary>
        public void ApplyAntiRoll(Rigidbody rb)
        {
            if (LeftWheelCollider == null || RightWheelCollider == null || rb == null) return;

            float travelL = 1.0f;
            float travelR = 1.0f;

            bool groundedL = LeftWheelCollider.GetGroundHit(out WheelHit hitL);
            if (groundedL)
            {
                travelL = (-LeftWheelCollider.transform.InverseTransformPoint(hitL.point).y - LeftWheelCollider.radius) / LeftWheelCollider.suspensionDistance;
            }

            bool groundedR = RightWheelCollider.GetGroundHit(out WheelHit hitR);
            if (groundedR)
            {
                travelR = (-RightWheelCollider.transform.InverseTransformPoint(hitR.point).y - RightWheelCollider.radius) / RightWheelCollider.suspensionDistance;
            }

            float antiRollForce = (travelL - travelR) * AntiRollForce;

            if (groundedL)
            {
                rb.AddForceAtPosition(LeftWheelCollider.transform.up * -antiRollForce, LeftWheelCollider.transform.position);
            }

            if (groundedR)
            {
                rb.AddForceAtPosition(RightWheelCollider.transform.up * antiRollForce, RightWheelCollider.transform.position);
            }
        }

        /// <summary>
        /// Modifies sideways friction stiffness (used for drift/handbrake).
        /// </summary>
        public void SetSidewaysStiffness(float stiffness)
        {
            if (LeftWheelCollider != null)
            {
                WheelFrictionCurve friction = LeftWheelCollider.sidewaysFriction;
                friction.stiffness = stiffness;
                LeftWheelCollider.sidewaysFriction = friction;
            }

            if (RightWheelCollider != null)
            {
                WheelFrictionCurve friction = RightWheelCollider.sidewaysFriction;
                friction.stiffness = stiffness;
                RightWheelCollider.sidewaysFriction = friction;
            }
        }
    }
}
