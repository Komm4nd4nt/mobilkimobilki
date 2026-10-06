using UnityEngine;
using RacingMobile.Vehicle;

namespace RacingMobile.Camera
{
    /// <summary>
    /// Smooth chase camera tailored for mobile racing games.
    /// Features velocity look-ahead, drift smoothing, and speed-responsive dynamic FOV.
    /// </summary>
    public class SmoothFollowCamera : MonoBehaviour
    {
        public static SmoothFollowCamera Instance { get; private set; }

        [Header("Target Tracking")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.2f, -5.2f);

        [Header("Smoothing Speeds")]
        [Tooltip("Position tracking smoothness")]
        [SerializeField] private float positionDamping = 10f;
        [Tooltip("Rotation tracking smoothness")]
        [SerializeField] private float rotationDamping = 8f;

        [Header("Dynamic Field of View (Speed Sensation)")]
        [SerializeField] private bool enableDynamicFOV = true;
        [SerializeField] private float baseFOV = 60f;
        [SerializeField] private float maxFOV = 76f;
        [SerializeField] private float maxSpeedForFOV = 200f;
        [SerializeField] private float fovDamping = 4f;

        private UnityEngine.Camera cam;
        private CarPhysicsController targetCar;
        private Rigidbody targetRb;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            cam = GetComponent<UnityEngine.Camera>();
        }

        public Transform Target => target;

        private void Start()
        {
            if (target != null)
            {
                SetTarget(target);
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                targetCar = target.GetComponent<CarPhysicsController>();
                targetRb = target.GetComponent<Rigidbody>();
            }
            else
            {
                targetCar = null;
                targetRb = null;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 1. Calculate Target Position behind the vehicle
            Vector3 desiredPosition = target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionDamping * Time.deltaTime);

            // 2. Calculate Smooth Look Rotation towards the vehicle
            Vector3 lookTarget = target.position + target.up * (offset.y * 0.4f);
            Vector3 forwardDirection = (lookTarget - transform.position).normalized;

            if (forwardDirection != Vector3.zero)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(forwardDirection, target.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationDamping * Time.deltaTime);
            }

            // 3. Dynamic FOV based on current speed
            if (enableDynamicFOV && cam != null && targetCar != null)
            {
                float speed = Mathf.Abs(targetCar.CurrentSpeedKmH);
                float speedRatio = Mathf.Clamp01(speed / maxSpeedForFOV);
                float desiredFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, desiredFOV, fovDamping * Time.deltaTime);
            }
        }
    }
}
