using System;
using UnityEngine;
using RacingMobile.MobileUI;

namespace RacingMobile.Core
{
    /// <summary>
    /// Global game settings manager handling Audio, Graphics quality,
    /// Mobile Frame Rate caps (30/60 FPS), and Mobile Steering modes.
    /// Settings are automatically persisted to PlayerPrefs and applied on launch.
    /// </summary>
    public static class SettingsManager
    {
        private const string KeyMasterVolume = "Settings_MasterVolume";
        private const string KeySfxVolume = "Settings_SfxVolume";
        private const string KeySteerMode = "Settings_SteeringMode";
        private const string KeyGraphicsQuality = "Settings_GraphicsQuality";
        private const string KeyTargetFps = "Settings_TargetFps";
        private const string KeyTiltSensitivity = "Settings_TiltSensitivity";

        public static float MasterVolume { get; private set; } = 1.0f;
        public static float SfxVolume { get; private set; } = 0.85f;
        public static MobileSteerMode CurrentSteerMode { get; private set; } = MobileSteerMode.Buttons;
        public static int GraphicsQualityLevel { get; private set; } = 1; // 0=Low, 1=Med, 2=High
        public static int TargetFps { get; private set; } = 60; // 30 or 60
        public static float TiltSensitivity { get; private set; } = 2.0f;

        public static event Action OnSettingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeOnStartup()
        {
            LoadAllSettings();
            ApplyAllSettings();
        }

        public static void LoadAllSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat(KeyMasterVolume, 1.0f);
            SfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, 0.85f);
            CurrentSteerMode = (MobileSteerMode)PlayerPrefs.GetInt(KeySteerMode, (int)MobileSteerMode.Buttons);
            
            // Default graphics based on quality level count
            int maxQuality = QualitySettings.names.Length - 1;
            int defaultQuality = Mathf.Clamp(1, 0, maxQuality);
            GraphicsQualityLevel = PlayerPrefs.GetInt(KeyGraphicsQuality, defaultQuality);

            TargetFps = PlayerPrefs.GetInt(KeyTargetFps, 60);
            TiltSensitivity = PlayerPrefs.GetFloat(KeyTiltSensitivity, 2.0f);
        }

        public static void ApplyAllSettings()
        {
            // Audio
            AudioListener.volume = MasterVolume;

            // Frame Rate
            Application.targetFrameRate = TargetFps;

            // Graphics
            if (GraphicsQualityLevel >= 0 && GraphicsQualityLevel < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(GraphicsQualityLevel, true);
            }

            OnSettingsChanged?.Invoke();
        }

        public static void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat(KeyMasterVolume, MasterVolume);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static void SetSteerMode(MobileSteerMode mode)
        {
            CurrentSteerMode = mode;
            PlayerPrefs.SetInt(KeySteerMode, (int)CurrentSteerMode);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static void SetGraphicsQuality(int level)
        {
            int maxQuality = QualitySettings.names.Length - 1;
            GraphicsQualityLevel = Mathf.Clamp(level, 0, maxQuality);
            QualitySettings.SetQualityLevel(GraphicsQualityLevel, true);
            PlayerPrefs.SetInt(KeyGraphicsQuality, GraphicsQualityLevel);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static void SetTargetFps(int fps)
        {
            TargetFps = (fps >= 60) ? 60 : 30;
            Application.targetFrameRate = TargetFps;
            PlayerPrefs.SetInt(KeyTargetFps, TargetFps);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static void SetTiltSensitivity(float sensitivity)
        {
            TiltSensitivity = Mathf.Clamp(sensitivity, 0.5f, 5.0f);
            PlayerPrefs.SetFloat(KeyTiltSensitivity, TiltSensitivity);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }
    }
}
