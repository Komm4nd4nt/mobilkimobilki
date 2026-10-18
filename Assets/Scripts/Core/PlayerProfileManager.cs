using System;
using UnityEngine;

namespace RacingMobile.Core
{
    /// <summary>
    /// Manages local player profile data, nickname persistence via PlayerPrefs,
    /// and provides events when the nickname changes.
    /// </summary>
    public static class PlayerProfileManager
    {
        private const string NicknamePrefKey = "RacingMobile_PlayerNickname";
        private const string PlayerLevelPrefKey = "RacingMobile_PlayerLevel";
        private const string PlayerScorePrefKey = "RacingMobile_PlayerScore";

        public const int MinNicknameLength = 2;
        public const int MaxNicknameLength = 16;

        public static event Action<string> OnNicknameChanged;

        private static string cachedNickname;

        private static readonly string[] RandomNamePrefixes = new string[]
        {
            "Turbo", "Apex", "Nitro", "Drift", "Ghost", "Speed", "Bolid", "Veloce", "Hyper", "Viper", "Storm", "Krol"
        };

        private static readonly string[] RandomNameSuffixes = new string[]
        {
            "Racer", "Driver", "Mistrz", "Pilot", "Beast", "Kierowca", "Hawk", "Striker", "Pro", "Rider"
        };

        static PlayerProfileManager()
        {
            LoadProfile();
        }

        public static string Nickname
        {
            get
            {
                if (string.IsNullOrEmpty(cachedNickname))
                {
                    LoadProfile();
                }
                return cachedNickname;
            }
            set
            {
                SetNickname(value);
            }
        }

        public static int PlayerLevel
        {
            get => PlayerPrefs.GetInt(PlayerLevelPrefKey, 1);
            set
            {
                PlayerPrefs.SetInt(PlayerLevelPrefKey, Mathf.Max(1, value));
                PlayerPrefs.Save();
            }
        }

        public static int PlayerScore
        {
            get => PlayerPrefs.GetInt(PlayerScorePrefKey, 0);
            set
            {
                PlayerPrefs.SetInt(PlayerScorePrefKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static void LoadProfile()
        {
            if (PlayerPrefs.HasKey(NicknamePrefKey))
            {
                cachedNickname = PlayerPrefs.GetString(NicknamePrefKey, "").Trim();
            }

            if (string.IsNullOrEmpty(cachedNickname))
            {
                cachedNickname = GenerateRandomNickname();
                PlayerPrefs.SetString(NicknamePrefKey, cachedNickname);
                PlayerPrefs.Save();
            }
        }

        public static bool SetNickname(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return false;
            }

            string sanitized = newName.Trim();
            if (sanitized.Length < MinNicknameLength)
            {
                return false;
            }

            if (sanitized.Length > MaxNicknameLength)
            {
                sanitized = sanitized.Substring(0, MaxNicknameLength);
            }

            cachedNickname = sanitized;
            PlayerPrefs.SetString(NicknamePrefKey, cachedNickname);
            PlayerPrefs.Save();

            OnNicknameChanged?.Invoke(cachedNickname);
            return true;
        }

        public static string GenerateRandomNickname()
        {
            string prefix = RandomNamePrefixes[UnityEngine.Random.Range(0, RandomNamePrefixes.Length)];
            string suffix = RandomNameSuffixes[UnityEngine.Random.Range(0, RandomNameSuffixes.Length)];
            int number = UnityEngine.Random.Range(10, 99);
            return $"{prefix}_{suffix}{number}";
        }
    }
}
