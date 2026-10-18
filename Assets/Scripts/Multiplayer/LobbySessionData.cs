using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RacingMobile.Multiplayer
{
    [Serializable]
    public class TrackInfo
    {
        public string TrackId;
        public string TrackName;
        public string SceneName;
        public string SurfaceType;
        public string Difficulty;
        public string LengthKm;
        public string Description;

        public TrackInfo(string id, string name, string scene, string surface, string difficulty, string length, string description)
        {
            TrackId = id;
            TrackName = name;
            SceneName = scene;
            SurfaceType = surface;
            Difficulty = difficulty;
            LengthKm = length;
            Description = description;
        }
    }

    /// <summary>
    /// Static session container bridging Main Menu lobby creation/joining
    /// with the upcoming Lobby Room and Netcode gameplay.
    /// Includes track configuration and player limits.
    /// </summary>
    public static class LobbySessionData
    {
        public const int DefaultMaxPlayers = 4;
        public const int MinPlayers = 2;
        public const int MaxAllowedPlayers = 8;

        public static string CurrentRoomName { get; set; } = "Szybcy_i_Wsciekli";
        public static string CurrentRoomCode { get; set; } = "RACE01";
        public static int MaxPlayers { get; set; } = DefaultMaxPlayers;
        public static bool IsHost { get; set; } = false;
        public static bool IsPrivate { get; set; } = true;
        public static string ServerAddress { get; set; } = "127.0.0.1";
        public static ushort ServerPort { get; set; } = 7777;

        // Tracks available in the game
        public static readonly List<TrackInfo> AvailableTracks = new List<TrackInfo>
        {
            new TrackInfo("asphalt_gp", "TOR GŁÓWNY GP (ASFALT)", "SampleScene", "Gładki asfalt", "Średnia", "2.4 km", "Szybki asfaltowy tor z rampami skokowymi i łukami driftowymi."),
            new TrackInfo("city_sprint", "MIEJSKI SPRINT NOCNY", "SampleScene", "Ulica / Asfalt", "Trudna", "3.1 km", "Wąski miejski tor z ostrymi zakrętami 90° i długą prostą."),
            new TrackInfo("drift_arena", "ARENA TESTOWA & DRIFT", "SampleScene", "Mieszana (Asfalt + Rampy)", "Łatwa", "1.5 km", "Otwarta arena idealna do doskonalenia poślizgów i testowania fizyki.")
        };

        public static int SelectedTrackIndex { get; set; } = 0;

        public static TrackInfo SelectedTrack
        {
            get
            {
                if (SelectedTrackIndex < 0 || SelectedTrackIndex >= AvailableTracks.Count)
                {
                    SelectedTrackIndex = 0;
                }
                return AvailableTracks[SelectedTrackIndex];
            }
        }

        public static string TargetTrackScene
        {
            get => SelectedTrack.SceneName;
            set { } // For backwards compatibility
        }

        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string GenerateRoomCode(int length = 6)
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                int index = UnityEngine.Random.Range(0, CodeChars.Length);
                sb.Append(CodeChars[index]);
            }
            return sb.ToString();
        }

        public static void SetupNewHostLobby(string roomName, int maxPlayers, bool isPrivate, int trackIndex = 0)
        {
            IsHost = true;
            CurrentRoomName = string.IsNullOrWhiteSpace(roomName) ? "Pokoj_Wyscigowy" : roomName.Trim();
            MaxPlayers = Mathf.Clamp(maxPlayers, MinPlayers, MaxAllowedPlayers);
            IsPrivate = isPrivate;
            SelectedTrackIndex = Mathf.Clamp(trackIndex, 0, AvailableTracks.Count - 1);
            CurrentRoomCode = GenerateRoomCode(6);
            Debug.Log($"[LobbySessionData] Host lobby prepared: '{CurrentRoomName}' (Code: {CurrentRoomCode}, Max: {MaxPlayers}, Tor: {SelectedTrack.TrackName})");
        }

        public static void SetupClientJoin(string roomCodeOrIp)
        {
            IsHost = false;
            string cleanInput = string.IsNullOrWhiteSpace(roomCodeOrIp) ? "RACE01" : roomCodeOrIp.Trim().ToUpperInvariant();
            
            // Check if it's an IP address or a room code
            if (cleanInput.Contains(".") || cleanInput.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                ServerAddress = cleanInput;
                CurrentRoomCode = "DIRECT_IP";
            }
            else
            {
                CurrentRoomCode = cleanInput;
            }

            Debug.Log($"[LobbySessionData] Client join prepared: Code/Target '{CurrentRoomCode}'");
        }

        public static void NextTrack()
        {
            SelectedTrackIndex = (SelectedTrackIndex + 1) % AvailableTracks.Count;
        }

        public static void PrevTrack()
        {
            SelectedTrackIndex = (SelectedTrackIndex - 1 + AvailableTracks.Count) % AvailableTracks.Count;
        }

        public static void ResetSession()
        {
            IsHost = false;
            CurrentRoomName = "";
            CurrentRoomCode = "";
            MaxPlayers = DefaultMaxPlayers;
            IsPrivate = false;
            SelectedTrackIndex = 0;
        }
    }
}
