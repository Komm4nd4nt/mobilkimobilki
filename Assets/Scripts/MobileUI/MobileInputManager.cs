using System;
using UnityEngine;
using UnityEngine.EventSystems;
using RacingMobile.Core;
using RacingMobile.Vehicle;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace RacingMobile.MobileUI
{
    public enum MobileSteerMode
    {
        Buttons,    // On-screen Left & Right buttons
        Tilt,       // Device Gyroscope / Accelerometer
        Joystick    // On-screen Virtual Joystick
    }

    /// <summary>
    /// Central manager for mobile touch inputs, accelerometer tilt, and editor keyboard fallback.
    /// Compatible with both the New Input System and Legacy Input Manager.
    /// Implements ICarInputProvider and feeds CarInputState into CarPhysicsController.
    /// </summary>
    public class MobileInputManager : MonoBehaviour, ICarInputProvider
    {
        [Header("Target Vehicle")]
        [SerializeField] private CarPhysicsController targetCar;

        [Header("Control Scheme")]
        [SerializeField] private MobileSteerMode steerMode = MobileSteerMode.Buttons;

        [Header("Touch UI References")]
        [SerializeField] private TouchButton steerLeftButton;
        [SerializeField] private TouchButton steerRightButton;
        [SerializeField] private TouchButton gasPedalButton;
        [SerializeField] private TouchButton brakePedalButton;
        [SerializeField] private TouchButton handbrakeButton;
        [SerializeField] private TouchButton boostButton;
        [SerializeField] private VirtualJoystick virtualJoystick;

        [Header("UI Containers (for visibility toggling)")]
        [SerializeField] private GameObject buttonSteeringContainer;
        [SerializeField] private GameObject joystickSteeringContainer;

        [Header("Steering Smoothing (Buttons Mode)")]
        [Tooltip("How quickly steering reaches full lock when pressing buttons")]
        [SerializeField] private float buttonSteerSpeed = 4.5f;
        [Tooltip("How quickly steering snaps back to zero when releasing buttons")]
        [SerializeField] private float buttonSteerReturnSpeed = 6.0f;

        [Header("Tilt / Accelerometer Settings")]
        [Tooltip("Sensitivity multiplier for phone tilting")]
        [SerializeField] private float tiltSensitivity = 2.0f;
        [Tooltip("Deadzone where tilt produces zero steering")]
        [SerializeField] private float tiltDeadzone = 0.04f;

        [Header("Desktop / Editor Testing")]
        [Tooltip("Enable keyboard (WASD / Arrows / Space) for fast editor testing")]
        [SerializeField] private bool enableKeyboardFallback = true;

        // Current resolved input state
        private CarInputState currentState;
        private float smoothedSteerValue;

        public MobileSteerMode SteerMode => steerMode;
        public CarInputState CurrentInputState => currentState;
        public CarPhysicsController TargetCar => targetCar;

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            // Automatically upgrade EventSystem if it still has legacy StandaloneInputModule
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var es in eventSystems)
            {
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    Destroy(standalone);
                    if (es.GetComponent<InputSystemUIInputModule>() == null)
                    {
                        es.gameObject.AddComponent<InputSystemUIInputModule>();
                    }
                }
            }
#endif
        }

        private void Start()
        {
            // Only set targetCar if already assigned, active, and locally controlled
            if (targetCar != null && (!targetCar.gameObject.activeInHierarchy || !targetCar.IsLocallyControlled))
            {
                targetCar = null;
            }

            if (targetCar == null)
            {
                TryAcquireLocalCar();
            }

            if (PlayerPrefs.HasKey("Settings_SteeringMode"))
            {
                steerMode = (MobileSteerMode)PlayerPrefs.GetInt("Settings_SteeringMode", (int)steerMode);
            }

            UpdateControlContainers();
        }

        /// <summary>
        /// Automatically discovers and links to the active local player vehicle.
        /// </summary>
        public bool TryAcquireLocalCar()
        {
            // 1. From Netcode LocalClient PlayerObject
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                var localClient = Unity.Netcode.NetworkManager.Singleton.LocalClient;
                if (localClient != null && localClient.PlayerObject != null && localClient.PlayerObject.gameObject.activeInHierarchy)
                {
                    var pc = localClient.PlayerObject.GetComponent<CarPhysicsController>();
                    if (pc != null && pc.IsLocallyControlled)
                    {
                        targetCar = pc;
                        return true;
                    }
                }
            }

            // 2. Search for any active CarPhysicsController marked IsLocallyControlled
            var allCars = FindObjectsByType<CarPhysicsController>(FindObjectsSortMode.None);
            foreach (var c in allCars)
            {
                if (c.gameObject.activeInHierarchy && c.IsLocallyControlled)
                {
                    targetCar = c;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Switch steering mode dynamically (e.g. from UI settings or toggle button).
        /// </summary>
        public void SetSteerMode(MobileSteerMode newMode)
        {
            steerMode = newMode;
            smoothedSteerValue = 0f;
            UpdateControlContainers();
        }

        public void CycleNextSteerMode()
        {
            int nextMode = ((int)steerMode + 1) % 3;
            SetSteerMode((MobileSteerMode)nextMode);
        }

        private void UpdateControlContainers()
        {
            if (buttonSteeringContainer != null)
            {
                buttonSteeringContainer.SetActive(steerMode == MobileSteerMode.Buttons);
            }

            if (joystickSteeringContainer != null)
            {
                joystickSteeringContainer.SetActive(steerMode == MobileSteerMode.Joystick);
            }
        }

        private void Update()
        {
            PollInputs();

            // Auto-acquire local vehicle if current targetCar is missing, inactive, or not locally controlled
            if (targetCar == null || !targetCar.gameObject.activeInHierarchy || !targetCar.IsLocallyControlled)
            {
                TryAcquireLocalCar();
            }

            // Feed input strictly to locally controlled vehicle
            if (targetCar != null)
            {
                if (targetCar.IsLocallyControlled && targetCar.gameObject.activeInHierarchy)
                {
                    targetCar.SetInput(currentState);
                }
                else
                {
                    // Target car is a remote opponent or deactivated! Detach immediately
                    targetCar = null;
                }
            }
        }

        private void PollInputs()
        {
            float rawSteer = 0f;
            float rawThrottle = 0f;
            float rawBrake = 0f;
            bool rawHandbrake = false;
            bool rawBoost = false;

            // 1. Process Mobile Steering based on selected mode
            switch (steerMode)
            {
                case MobileSteerMode.Buttons:
                    bool leftHeld = steerLeftButton != null && steerLeftButton.IsPressed;
                    bool rightHeld = steerRightButton != null && steerRightButton.IsPressed;

                    if (leftHeld && !rightHeld)
                    {
                        smoothedSteerValue = Mathf.MoveTowards(smoothedSteerValue, -1f, buttonSteerSpeed * Time.deltaTime);
                    }
                    else if (rightHeld && !leftHeld)
                    {
                        smoothedSteerValue = Mathf.MoveTowards(smoothedSteerValue, 1f, buttonSteerSpeed * Time.deltaTime);
                    }
                    else
                    {
                        smoothedSteerValue = Mathf.MoveTowards(smoothedSteerValue, 0f, buttonSteerReturnSpeed * Time.deltaTime);
                    }
                    rawSteer = smoothedSteerValue;
                    break;

                case MobileSteerMode.Joystick:
                    if (virtualJoystick != null)
                    {
                        rawSteer = Mathf.Clamp(virtualJoystick.Horizontal, -1f, 1f);
                    }
                    break;

                case MobileSteerMode.Tilt:
#if ENABLE_INPUT_SYSTEM
                    Accelerometer accel = Accelerometer.current;
                    if (accel != null)
                    {
                        if (!accel.enabled) InputSystem.EnableDevice(accel);
                        float accelX = accel.acceleration.ReadValue().x;
                        if (Mathf.Abs(accelX) > tiltDeadzone)
                        {
                            float tiltValue = (accelX - Mathf.Sign(accelX) * tiltDeadzone) / (1f - tiltDeadzone);
                            rawSteer = Mathf.Clamp(tiltValue * tiltSensitivity, -1f, 1f);
                        }
                    }
#elif ENABLE_LEGACY_INPUT_MANAGER
                    float accelX = Input.acceleration.x;
                    if (Mathf.Abs(accelX) > tiltDeadzone)
                    {
                        float tiltValue = (accelX - Mathf.Sign(accelX) * tiltDeadzone) / (1f - tiltDeadzone);
                        rawSteer = Mathf.Clamp(tiltValue * tiltSensitivity, -1f, 1f);
                    }
#endif
                    break;
            }

            // 2. Process Mobile Pedals
            if (gasPedalButton != null && gasPedalButton.IsPressed)
            {
                rawThrottle = 1f;
            }

            if (brakePedalButton != null && brakePedalButton.IsPressed)
            {
                rawBrake = 1f;
            }

            if (handbrakeButton != null && handbrakeButton.IsPressed)
            {
                rawHandbrake = true;
            }

            if (boostButton != null && boostButton.IsPressed)
            {
                rawBoost = true;
            }

            // 3. Process Desktop / Editor Keyboard & Gamepad Fallback
            if (enableKeyboardFallback)
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    float keyboardSteer = 0f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyboardSteer -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyboardSteer += 1f;

                    if (Mathf.Abs(keyboardSteer) > 0.01f)
                    {
                        rawSteer = keyboardSteer;
                    }

                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
                    {
                        rawThrottle = Mathf.Max(rawThrottle, 1f);
                    }

                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
                    {
                        rawBrake = Mathf.Max(rawBrake, 1f);
                    }

                    if (kb.spaceKey.isPressed)
                    {
                        rawHandbrake = true;
                    }

                    if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
                    {
                        rawBoost = true;
                    }

                    if (kb.rKey.wasPressedThisFrame && targetCar != null)
                    {
                        targetCar.ResetOrientation();
                    }
                }

                Gamepad pad = Gamepad.current;
                if (pad != null)
                {
                    float stickX = pad.leftStick.x.ReadValue();
                    if (Mathf.Abs(stickX) > 0.15f) rawSteer = stickX;

                    float rTrigger = pad.rightTrigger.ReadValue();
                    if (rTrigger > 0.05f) rawThrottle = Mathf.Max(rawThrottle, rTrigger);

                    float lTrigger = pad.leftTrigger.ReadValue();
                    if (lTrigger > 0.05f) rawBrake = Mathf.Max(rawBrake, lTrigger);

                    if (pad.buttonSouth.isPressed) rawHandbrake = true;
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                float keyboardSteer = 0f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyboardSteer -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyboardSteer += 1f;

                if (Mathf.Abs(keyboardSteer) > 0.01f)
                {
                    rawSteer = keyboardSteer;
                }

                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                {
                    rawThrottle = Mathf.Max(rawThrottle, 1f);
                }

                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                {
                    rawBrake = Mathf.Max(rawBrake, 1f);
                }

                if (Input.GetKey(KeyCode.Space))
                {
                    rawHandbrake = true;
                }

                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    rawBoost = true;
                }

                if (Input.GetKeyDown(KeyCode.R) && targetCar != null)
                {
                    targetCar.ResetOrientation();
                }
#endif
            }

            currentState = new CarInputState
            {
                Steer = Mathf.Clamp(rawSteer, -1f, 1f),
                Throttle = Mathf.Clamp01(rawThrottle),
                Brake = Mathf.Clamp01(rawBrake),
                Handbrake = rawHandbrake,
                Boost = rawBoost
            };
        }

        public CarInputState GetInput()
        {
            return currentState;
        }

        public void SetTargetCar(CarPhysicsController car)
        {
            targetCar = car;
        }
    }
}
