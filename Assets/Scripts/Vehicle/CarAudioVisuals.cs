using UnityEngine;

namespace RacingMobile.Vehicle
{
    /// <summary>
    /// Handles engine audio pitch scaling, tire screech/smoke during drift,
    /// and brake light visuals.
    /// </summary>
    [RequireComponent(typeof(CarPhysicsController))]
    public class CarAudioVisuals : MonoBehaviour
    {
        [Header("Vehicle Reference")]
        [SerializeField] private CarPhysicsController car;

        [Header("Engine Audio")]
        [SerializeField] private AudioSource engineAudio;
        [SerializeField] private float minPitch = 0.8f;
        [SerializeField] private float maxPitch = 2.4f;
        [SerializeField] private float topSpeedForAudio = 190f;

        [Header("Tire Skid Audio & Particles")]
        [SerializeField] private AudioSource skidAudio;
        [SerializeField] private ParticleSystem tireSmokeParticles;

        [Header("Brake Lights")]
        [SerializeField] private MeshRenderer brakeLightsRenderer;
        [SerializeField] private Material brakeLightOnMat;
        [SerializeField] private Material brakeLightOffMat;

        private void Awake()
        {
            if (car == null) car = GetComponent<CarPhysicsController>();
        }

        private void Update()
        {
            if (car == null) return;

            UpdateEngineSound();
            UpdateDriftEffects();
            UpdateBrakeLights();
        }

        private void UpdateEngineSound()
        {
            if (engineAudio == null) return;

            float speed = Mathf.Abs(car.CurrentSpeedKmH);
            float speedRatio = Mathf.Clamp01(speed / topSpeedForAudio);

            // Modulate pitch with gear oscillation
            float gearProgress = (speed % 40f) / 40f;
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, Mathf.Lerp(speedRatio, gearProgress, 0.4f));

            engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, targetPitch, Time.deltaTime * 6f);
        }

        private void UpdateDriftEffects()
        {
            bool isSlipping = car.IsDrifting || (car.CurrentInput.Handbrake && Mathf.Abs(car.CurrentSpeedKmH) > 10f);

            // Skid Sound
            if (skidAudio != null)
            {
                if (isSlipping && !skidAudio.isPlaying)
                {
                    skidAudio.Play();
                }
                else if (!isSlipping && skidAudio.isPlaying)
                {
                    skidAudio.Stop();
                }
            }

            // Tire Smoke Particles
            if (tireSmokeParticles != null)
            {
                var emission = tireSmokeParticles.emission;
                emission.enabled = isSlipping;
            }
        }

        private void UpdateBrakeLights()
        {
            if (brakeLightsRenderer == null) return;

            bool isBraking = car.CurrentInput.Brake > 0.1f;
            if (brakeLightOnMat != null && brakeLightOffMat != null)
            {
                brakeLightsRenderer.sharedMaterial = isBraking ? brakeLightOnMat : brakeLightOffMat;
            }
        }
    }
}
