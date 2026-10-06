using UnityEngine;
using UnityEngine.UI;
using RacingMobile.Vehicle;

namespace RacingMobile.MobileUI
{
    /// <summary>
    /// HUD script displaying digital speed, gear indicator, drift notifications,
    /// and control mode switcher for mobile racing.
    /// </summary>
    public class SpeedometerUI : MonoBehaviour
    {
        [Header("Vehicle Reference")]
        [SerializeField] private CarPhysicsController car;
        [SerializeField] private MobileInputManager inputManager;

        [Header("UI Text Displays")]
        [SerializeField] private Text speedText;
        [SerializeField] private Text gearText;
        [SerializeField] private Text modeText;

        [Header("UI Indicators")]
        [SerializeField] private Image speedFillBar;
        [SerializeField] private RectTransform speedNeedle;
        [SerializeField] private GameObject driftBadge;
        [SerializeField] private GameObject boostBadge;

        [Header("Needle Gauge Calibration")]
        [SerializeField] private float minNeedleAngle = 120f;
        [SerializeField] private float maxNeedleAngle = -120f;
        [SerializeField] private float maxGaugeSpeed = 220f;

        private void Start()
        {
            if (car == null)
            {
                car = FindFirstObjectByType<CarPhysicsController>();
            }

            if (inputManager == null)
            {
                inputManager = FindFirstObjectByType<MobileInputManager>();
            }

            UpdateModeText();
        }

        private void Update()
        {
            if (car == null) return;

            float speed = Mathf.Abs(car.CurrentSpeedKmH);

            // Digital Speed
            if (speedText != null)
            {
                speedText.text = Mathf.RoundToInt(speed).ToString();
            }

            // Gear Indicator
            if (gearText != null)
            {
                if (car.IsReversing)
                {
                    gearText.text = "R";
                }
                else if (speed < 2f)
                {
                    gearText.text = "N";
                }
                else
                {
                    int gear = Mathf.Clamp(Mathf.FloorToInt(speed / 35f) + 1, 1, 6);
                    gearText.text = gear.ToString();
                }
            }

            // Analog Needle / Gauge
            float normalizedSpeed = Mathf.Clamp01(speed / maxGaugeSpeed);

            if (speedFillBar != null)
            {
                speedFillBar.fillAmount = normalizedSpeed;
            }

            if (speedNeedle != null)
            {
                float angle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, normalizedSpeed);
                speedNeedle.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            // Drift & Boost Indicators
            if (driftBadge != null)
            {
                driftBadge.SetActive(car.IsDrifting);
            }

            if (boostBadge != null)
            {
                boostBadge.SetActive(car.CurrentInput.Boost);
            }
        }

        public void OnClickToggleControlMode()
        {
            if (inputManager != null)
            {
                inputManager.CycleNextSteerMode();
                UpdateModeText();
            }
        }

        public void OnClickResetCar()
        {
            if (car != null)
            {
                car.ResetOrientation();
            }
        }

        public void UpdateModeText()
        {
            if (modeText != null && inputManager != null)
            {
                modeText.text = $"MODE: {inputManager.SteerMode.ToString().ToUpper()}";
            }
        }

        public void SetTargetCar(CarPhysicsController newCar)
        {
            car = newCar;
        }
    }
}
