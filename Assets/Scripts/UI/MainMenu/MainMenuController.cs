using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using RacingMobile.Core;
using RacingMobile.MobileUI;
using RacingMobile.Multiplayer;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RacingMobile.UI.MainMenu
{
    /// <summary>
    /// Master UI controller for the mobile racing game main menu.
    /// Manages panel transitions (Main View, Create Lobby, Join Lobby, Settings, Nickname Editor),
    /// profile management, mobile safe layout, and session initialization.
    /// Automatically connects all button listeners at runtime so no button click is lost!
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public static MainMenuController Instance { get; private set; }

        [Header("Main Menu Navigation Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject createLobbyPanel;
        [SerializeField] private GameObject joinLobbyPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject profileModalPanel;

        [Header("Main Menu Action Buttons")]
        [SerializeField] private Button openCreateLobbyButton;
        [SerializeField] private Button openJoinLobbyButton;
        [SerializeField] private Button openSettingsButton;
        [SerializeField] private Button testDriveButton;
        [SerializeField] private Button quitGameButton;

        [Header("Player Profile Elements")]
        [SerializeField] private Text profileNicknameText;
        [SerializeField] private InputField profileNicknameInput;
        [SerializeField] private Button editProfileButton;
        [SerializeField] private Button profileSaveButton;
        [SerializeField] private Button profileRandomButton;
        [SerializeField] private Button profileCancelButton;

        [Header("Create Lobby Panel Elements")]
        [SerializeField] private InputField createLobbyNameInput;
        [SerializeField] private Slider maxPlayersSlider;
        [SerializeField] private Text maxPlayersValueText;
        [SerializeField] private Toggle privateLobbyToggle;
        [SerializeField] private Button trackPrevButton;
        [SerializeField] private Button trackNextButton;
        [SerializeField] private Text trackNameText;
        [SerializeField] private Text trackDetailsText;
        [SerializeField] private Button confirmCreateLobbyButton;
        [SerializeField] private Button createLobbyBackButton;

        [Header("Join Lobby Panel Elements")]
        [SerializeField] private InputField joinCodeInput;
        [SerializeField] private Button confirmJoinLobbyButton;
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Text joinFeedbackText;
        [SerializeField] private Button joinLobbyBackButton;

        [Header("Settings Panel Elements")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Text masterVolumeValueText;
        [SerializeField] private Text sfxVolumeValueText;
        [SerializeField] private Button steerButtonsModeBtn;
        [SerializeField] private Button steerTiltModeBtn;
        [SerializeField] private Button steerJoyModeBtn;
        [SerializeField] private Button qualityLowBtn;
        [SerializeField] private Button qualityMedBtn;
        [SerializeField] private Button qualityHighBtn;
        [SerializeField] private Button fps30Btn;
        [SerializeField] private Button fps60Btn;
        [SerializeField] private Button settingsCloseButton;

        [Header("Notification Toast")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private Text toastText;

        [Header("Audio")]
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioClip buttonClickClip;

        private Coroutine activeToastCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;

            // Ensure exactly one active EventSystem in scene
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (eventSystems != null && eventSystems.Length > 1)
            {
                for (int i = 1; i < eventSystems.Length; i++)
                {
                    Destroy(eventSystems[i].gameObject);
                }
            }

            AutoFindMissingReferences();
        }

        private void Start()
        {
            AutoFindMissingReferences();
            InitializeAllButtons();
            InitializeProfileUI();
            InitializeSettingsUI();
            InitializeLobbyUI();

            ShowPanel(mainPanel);

            // Register nickname change listener
            PlayerProfileManager.OnNicknameChanged += UpdateProfileNicknameDisplay;
        }

        private void OnDestroy()
        {
            PlayerProfileManager.OnNicknameChanged -= UpdateProfileNicknameDisplay;
        }

        private void Update()
        {
            // Android back button / PC Escape key handling
            bool backPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                backPressed = true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                backPressed = true;
            }
#endif

            if (backPressed)
            {
                HandleBackNavigation();
            }
        }

        #region Automatic Component Discovery & Wiring

        /// <summary>
        /// Automatically discovers any UI elements by hierarchy name if not assigned in Inspector.
        /// Guarantees that all buttons work even without manual wiring!
        /// </summary>
        private void AutoFindMissingReferences()
        {
            // Panels
            if (mainPanel == null) mainPanel = FindChildRecursively(transform, "Panel_MainMenu")?.gameObject;
            if (createLobbyPanel == null) createLobbyPanel = FindChildRecursively(transform, "Modal_CreateLobby")?.gameObject;
            if (joinLobbyPanel == null) joinLobbyPanel = FindChildRecursively(transform, "Modal_JoinLobby")?.gameObject;
            if (settingsPanel == null) settingsPanel = FindChildRecursively(transform, "Modal_Settings")?.gameObject;
            if (profileModalPanel == null) profileModalPanel = FindChildRecursively(transform, "Modal_EditProfile")?.gameObject;

            // Main Menu Buttons
            if (openCreateLobbyButton == null) openCreateLobbyButton = FindComponentByName<Button>("Btn_CreateLobby");
            if (openJoinLobbyButton == null) openJoinLobbyButton = FindComponentByName<Button>("Btn_JoinLobby");
            if (openSettingsButton == null) openSettingsButton = FindComponentByName<Button>("Btn_Settings");
            if (testDriveButton == null) testDriveButton = FindComponentByName<Button>("Btn_TestDrive");
            if (quitGameButton == null) quitGameButton = FindComponentByName<Button>("Btn_Quit");

            // Profile
            if (profileNicknameText == null) profileNicknameText = FindComponentByName<Text>("PlayerNickText");
            if (profileNicknameInput == null) profileNicknameInput = FindComponentByName<InputField>("Input_ProfileNick");
            if (editProfileButton == null) editProfileButton = FindComponentByName<Button>("Btn_EditNick");
            if (profileSaveButton == null) profileSaveButton = FindComponentByName<Button>("Btn_SaveNick");
            if (profileRandomButton == null) profileRandomButton = FindComponentByName<Button>("Btn_RandomNick");
            if (profileCancelButton == null) profileCancelButton = FindComponentByName<Button>("Btn_CancelNick");

            // Create Lobby
            if (createLobbyNameInput == null) createLobbyNameInput = FindComponentByName<InputField>("Input_RoomName");
            if (trackPrevButton == null) trackPrevButton = FindComponentByName<Button>("Btn_PrevTrack");
            if (trackNextButton == null) trackNextButton = FindComponentByName<Button>("Btn_NextTrack");
            if (trackNameText == null) trackNameText = FindComponentByName<Text>("Txt_TrackName");
            if (trackDetailsText == null) trackDetailsText = FindComponentByName<Text>("Txt_TrackDetails");
            if (maxPlayersSlider == null) maxPlayersSlider = FindComponentByName<Slider>("Slider_MaxPlayers");
            if (maxPlayersValueText == null) maxPlayersValueText = FindComponentByName<Text>("Val_MaxPlayers");
            if (privateLobbyToggle == null) privateLobbyToggle = FindComponentByName<Toggle>("Toggle_Private");
            if (confirmCreateLobbyButton == null) confirmCreateLobbyButton = FindComponentByName<Button>("Btn_ConfirmCreate");
            if (createLobbyBackButton == null && createLobbyPanel != null)
            {
                var card = FindChildRecursively(createLobbyPanel.transform, "Card_CreateLobby");
                if (card != null) createLobbyBackButton = FindChildRecursively(card, "Btn_Back")?.GetComponent<Button>();
            }

            // Join Lobby
            if (joinCodeInput == null) joinCodeInput = FindComponentByName<InputField>("Input_JoinCode");
            if (confirmJoinLobbyButton == null) confirmJoinLobbyButton = FindComponentByName<Button>("Btn_ConfirmJoinCode");
            if (quickJoinButton == null) quickJoinButton = FindComponentByName<Button>("Btn_QuickJoin");
            if (joinFeedbackText == null) joinFeedbackText = FindComponentByName<Text>("Txt_JoinFeedback");
            if (joinLobbyBackButton == null && joinLobbyPanel != null)
            {
                var card = FindChildRecursively(joinLobbyPanel.transform, "Card_JoinLobby");
                if (card != null) joinLobbyBackButton = FindChildRecursively(card, "Btn_Back")?.GetComponent<Button>();
            }

            // Settings
            if (masterVolumeSlider == null) masterVolumeSlider = FindComponentByName<Slider>("Slider_MasterVol");
            if (sfxVolumeSlider == null) sfxVolumeSlider = FindComponentByName<Slider>("Slider_SfxVol");
            if (masterVolumeValueText == null) masterVolumeValueText = FindComponentByName<Text>("Val_MasterVol");
            if (sfxVolumeValueText == null) sfxVolumeValueText = FindComponentByName<Text>("Val_SfxVol");
            if (steerButtonsModeBtn == null) steerButtonsModeBtn = FindComponentByName<Button>("Btn_SteerButtons");
            if (steerTiltModeBtn == null) steerTiltModeBtn = FindComponentByName<Button>("Btn_SteerTilt");
            if (steerJoyModeBtn == null) steerJoyModeBtn = FindComponentByName<Button>("Btn_SteerJoy");
            if (qualityLowBtn == null) qualityLowBtn = FindComponentByName<Button>("Btn_QualLow");
            if (qualityMedBtn == null) qualityMedBtn = FindComponentByName<Button>("Btn_QualMed");
            if (qualityHighBtn == null) qualityHighBtn = FindComponentByName<Button>("Btn_QualHigh");
            if (fps30Btn == null) fps30Btn = FindComponentByName<Button>("Btn_Fps30");
            if (fps60Btn == null) fps60Btn = FindComponentByName<Button>("Btn_Fps60");
            if (settingsCloseButton == null) settingsCloseButton = FindComponentByName<Button>("Btn_CloseSettings");

            // Toast
            if (toastRoot == null) toastRoot = FindChildRecursively(transform, "ToastNotification")?.gameObject;
            if (toastText == null) toastText = FindComponentByName<Text>("ToastMessage");
        }

        private void InitializeAllButtons()
        {
            // Main Navigation Buttons
            if (openCreateLobbyButton != null)
            {
                openCreateLobbyButton.onClick.RemoveAllListeners();
                openCreateLobbyButton.onClick.AddListener(OpenCreateLobbyPanel);
            }
            if (openJoinLobbyButton != null)
            {
                openJoinLobbyButton.onClick.RemoveAllListeners();
                openJoinLobbyButton.onClick.AddListener(OpenJoinLobbyPanel);
            }
            if (openSettingsButton != null)
            {
                openSettingsButton.onClick.RemoveAllListeners();
                openSettingsButton.onClick.AddListener(OpenSettingsPanel);
            }
            if (testDriveButton != null)
            {
                testDriveButton.onClick.RemoveAllListeners();
                testDriveButton.onClick.AddListener(OnClickTestDrive);
            }
            if (quitGameButton != null)
            {
                quitGameButton.onClick.RemoveAllListeners();
                quitGameButton.onClick.AddListener(OnClickQuitGame);
            }

            // Profile Buttons
            if (editProfileButton != null)
            {
                editProfileButton.onClick.RemoveAllListeners();
                editProfileButton.onClick.AddListener(OpenProfileModal);
            }
            if (profileSaveButton != null)
            {
                profileSaveButton.onClick.RemoveAllListeners();
                profileSaveButton.onClick.AddListener(OnClickSaveNickname);
            }
            if (profileRandomButton != null)
            {
                profileRandomButton.onClick.RemoveAllListeners();
                profileRandomButton.onClick.AddListener(OnClickGenerateRandomNickname);
            }
            if (profileCancelButton != null)
            {
                profileCancelButton.onClick.RemoveAllListeners();
                profileCancelButton.onClick.AddListener(OpenMainPanel);
            }

            // Create Lobby Modal Buttons
            if (trackPrevButton != null)
            {
                trackPrevButton.onClick.RemoveAllListeners();
                trackPrevButton.onClick.AddListener(OnClickPrevTrack);
            }
            if (trackNextButton != null)
            {
                trackNextButton.onClick.RemoveAllListeners();
                trackNextButton.onClick.AddListener(OnClickNextTrack);
            }
            if (confirmCreateLobbyButton != null)
            {
                confirmCreateLobbyButton.onClick.RemoveAllListeners();
                confirmCreateLobbyButton.onClick.AddListener(OnClickConfirmCreateLobby);
            }
            if (createLobbyBackButton != null)
            {
                createLobbyBackButton.onClick.RemoveAllListeners();
                createLobbyBackButton.onClick.AddListener(OpenMainPanel);
            }

            // Join Lobby Modal Buttons
            if (confirmJoinLobbyButton != null)
            {
                confirmJoinLobbyButton.onClick.RemoveAllListeners();
                confirmJoinLobbyButton.onClick.AddListener(OnClickConfirmJoinByCode);
            }
            if (quickJoinButton != null)
            {
                quickJoinButton.onClick.RemoveAllListeners();
                quickJoinButton.onClick.AddListener(OnClickQuickJoin);
            }
            if (joinLobbyBackButton != null)
            {
                joinLobbyBackButton.onClick.RemoveAllListeners();
                joinLobbyBackButton.onClick.AddListener(OpenMainPanel);
            }

            // Settings Modal Buttons
            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.RemoveAllListeners();
                settingsCloseButton.onClick.AddListener(OpenMainPanel);
            }
        }

        private T FindComponentByName<T>(string gameObjectName) where T : Component
        {
            var allComponents = GetComponentsInChildren<T>(true);
            foreach (var comp in allComponents)
            {
                if (comp.gameObject.name == gameObjectName) return comp;
            }
            return null;
        }

        private Transform FindChildRecursively(Transform parent, string childName)
        {
            if (parent.name == childName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindChildRecursively(parent.GetChild(i), childName);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        #region Navigation & Panel Switching

        public void ShowPanel(GameObject targetPanel)
        {
            PlayClickSound();

            if (mainPanel != null) mainPanel.SetActive(targetPanel == mainPanel);
            if (createLobbyPanel != null) createLobbyPanel.SetActive(targetPanel == createLobbyPanel);
            if (joinLobbyPanel != null) joinLobbyPanel.SetActive(targetPanel == joinLobbyPanel);
            if (settingsPanel != null) settingsPanel.SetActive(targetPanel == settingsPanel);
            if (profileModalPanel != null) profileModalPanel.SetActive(targetPanel == profileModalPanel);
        }

        public void OpenMainPanel() => ShowPanel(mainPanel);

        public void OpenCreateLobbyPanel()
        {
            if (createLobbyNameInput != null)
            {
                createLobbyNameInput.text = $"{PlayerProfileManager.Nickname}'s Room";
            }
            ShowPanel(createLobbyPanel);
        }

        public void OpenJoinLobbyPanel()
        {
            if (joinFeedbackText != null) joinFeedbackText.text = "";
            ShowPanel(joinLobbyPanel);
        }

        public void OpenSettingsPanel()
        {
            SyncSettingsUIToCurrentValues();
            ShowPanel(settingsPanel);
        }

        public void OpenProfileModal()
        {
            if (profileNicknameInput != null)
            {
                profileNicknameInput.text = PlayerProfileManager.Nickname;
            }
            ShowPanel(profileModalPanel);
        }

        private void HandleBackNavigation()
        {
            if (profileModalPanel != null && profileModalPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            else if (createLobbyPanel != null && createLobbyPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            else if (joinLobbyPanel != null && joinLobbyPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            else if (settingsPanel != null && settingsPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            else
            {
                ShowToast("Wciśnij ponownie, aby wyjść z gry");
            }
        }

        #endregion

        #region Player Profile & Nickname

        private void InitializeProfileUI()
        {
            UpdateProfileNicknameDisplay(PlayerProfileManager.Nickname);
        }

        private void UpdateProfileNicknameDisplay(string nickname)
        {
            if (profileNicknameText != null)
            {
                profileNicknameText.text = nickname;
            }
        }

        public void OnClickSaveNickname()
        {
            PlayClickSound();
            if (profileNicknameInput == null) return;

            string enteredName = profileNicknameInput.text;
            if (PlayerProfileManager.SetNickname(enteredName))
            {
                UpdateProfileNicknameDisplay(PlayerProfileManager.Nickname);
                ShowToast($"Zapisano nick: {PlayerProfileManager.Nickname}");
                ShowPanel(mainPanel);
            }
            else
            {
                ShowToast("Nick musi zawierać od 2 do 16 znaków!");
            }
        }

        public void OnClickGenerateRandomNickname()
        {
            PlayClickSound();
            string randomName = PlayerProfileManager.GenerateRandomNickname();
            if (profileNicknameInput != null)
            {
                profileNicknameInput.text = randomName;
            }
            Debug.Log($"[MainMenu] Wylosowano nowy nick: {randomName}");
        }

        #endregion

        #region Create Lobby Logic

        private void InitializeLobbyUI()
        {
            UpdateTrackSelectionDisplay();

            if (maxPlayersSlider != null)
            {
                maxPlayersSlider.minValue = LobbySessionData.MinPlayers;
                maxPlayersSlider.maxValue = LobbySessionData.MaxAllowedPlayers;
                maxPlayersSlider.wholeNumbers = true;
                maxPlayersSlider.value = LobbySessionData.DefaultMaxPlayers;
                maxPlayersSlider.onValueChanged.RemoveAllListeners();
                maxPlayersSlider.onValueChanged.AddListener(OnMaxPlayersSliderChanged);
                OnMaxPlayersSliderChanged(maxPlayersSlider.value);
            }
        }

        public void OnClickPrevTrack()
        {
            PlayClickSound();
            LobbySessionData.PrevTrack();
            UpdateTrackSelectionDisplay();
        }

        public void OnClickNextTrack()
        {
            PlayClickSound();
            LobbySessionData.NextTrack();
            UpdateTrackSelectionDisplay();
        }

        private void UpdateTrackSelectionDisplay()
        {
            var track = LobbySessionData.SelectedTrack;
            if (trackNameText != null)
            {
                trackNameText.text = track.TrackName;
            }
            if (trackDetailsText != null)
            {
                trackDetailsText.text = $"{track.LengthKm} • {track.SurfaceType} • Trudność: {track.Difficulty}";
            }
        }

        private void OnMaxPlayersSliderChanged(float val)
        {
            int players = Mathf.RoundToInt(val);
            if (maxPlayersValueText != null)
            {
                maxPlayersValueText.text = $"{players} graczy";
            }
        }

        public void OnClickConfirmCreateLobby()
        {
            PlayClickSound();

            string roomName = (createLobbyNameInput != null && !string.IsNullOrWhiteSpace(createLobbyNameInput.text))
                ? createLobbyNameInput.text.Trim()
                : $"{PlayerProfileManager.Nickname}'s Room";

            int maxPlayers = maxPlayersSlider != null ? Mathf.RoundToInt(maxPlayersSlider.value) : 4;
            bool isPrivate = privateLobbyToggle != null && privateLobbyToggle.isOn;

            // Configure Session Data with chosen Track
            LobbySessionData.SetupNewHostLobby(roomName, maxPlayers, isPrivate, LobbySessionData.SelectedTrackIndex);

            ShowToast($"Lobby '{roomName}' utworzone! Tor: {LobbySessionData.SelectedTrack.TrackName} (Kod: {LobbySessionData.CurrentRoomCode})");
            Debug.Log($"[MainMenu] Stworzono lobby: {roomName}, Tor: {LobbySessionData.SelectedTrack.TrackName}, Kod: {LobbySessionData.CurrentRoomCode}, Max: {maxPlayers}");
        }

        #endregion

        #region Join Lobby Logic

        public void OnClickConfirmJoinByCode()
        {
            PlayClickSound();

            string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpperInvariant() : "";
            if (string.IsNullOrEmpty(code))
            {
                if (joinFeedbackText != null) joinFeedbackText.text = "Podaj kod pokoju lub adres IP!";
                ShowToast("Wpisz kod lobby!");
                return;
            }

            LobbySessionData.SetupClientJoin(code);
            if (joinFeedbackText != null) joinFeedbackText.text = $"Łączenie z lobby {code}...";
            ShowToast($"Dołączanie do lobby: {code}...");
            Debug.Log($"[MainMenu] Dołączanie do lobby za pomocą kodu/IP: {code}");
        }

        public void OnClickQuickJoin()
        {
            PlayClickSound();
            LobbySessionData.SetupClientJoin("QUICK_MATCH");
            ShowToast("Szukanie dostępnego pokoju online...");
            Debug.Log("[MainMenu] Szybkie dołączanie do pierwszego dostępnego lobby");
        }

        #endregion

        #region Settings Panel Logic

        private void InitializeSettingsUI()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.minValue = 0f;
                masterVolumeSlider.maxValue = 1f;
                masterVolumeSlider.onValueChanged.RemoveAllListeners();
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.minValue = 0f;
                sfxVolumeSlider.maxValue = 1f;
                sfxVolumeSlider.onValueChanged.RemoveAllListeners();
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }

            if (steerButtonsModeBtn != null)
            {
                steerButtonsModeBtn.onClick.RemoveAllListeners();
                steerButtonsModeBtn.onClick.AddListener(() => SetSteeringMode(MobileSteerMode.Buttons));
            }
            if (steerTiltModeBtn != null)
            {
                steerTiltModeBtn.onClick.RemoveAllListeners();
                steerTiltModeBtn.onClick.AddListener(() => SetSteeringMode(MobileSteerMode.Tilt));
            }
            if (steerJoyModeBtn != null)
            {
                steerJoyModeBtn.onClick.RemoveAllListeners();
                steerJoyModeBtn.onClick.AddListener(() => SetSteeringMode(MobileSteerMode.Joystick));
            }

            if (qualityLowBtn != null)
            {
                qualityLowBtn.onClick.RemoveAllListeners();
                qualityLowBtn.onClick.AddListener(() => SetGraphicsQuality(0));
            }
            if (qualityMedBtn != null)
            {
                qualityMedBtn.onClick.RemoveAllListeners();
                qualityMedBtn.onClick.AddListener(() => SetGraphicsQuality(1));
            }
            if (qualityHighBtn != null)
            {
                qualityHighBtn.onClick.RemoveAllListeners();
                qualityHighBtn.onClick.AddListener(() => SetGraphicsQuality(2));
            }

            if (fps30Btn != null)
            {
                fps30Btn.onClick.RemoveAllListeners();
                fps30Btn.onClick.AddListener(() => SetFpsTarget(30));
            }
            if (fps60Btn != null)
            {
                fps60Btn.onClick.RemoveAllListeners();
                fps60Btn.onClick.AddListener(() => SetFpsTarget(60));
            }
        }

        private void SyncSettingsUIToCurrentValues()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = SettingsManager.MasterVolume;
                if (masterVolumeValueText != null) masterVolumeValueText.text = $"{Mathf.RoundToInt(SettingsManager.MasterVolume * 100)}%";
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = SettingsManager.SfxVolume;
                if (sfxVolumeValueText != null) sfxVolumeValueText.text = $"{Mathf.RoundToInt(SettingsManager.SfxVolume * 100)}%";
            }

            UpdateSteeringButtonsHighlight();
            UpdateQualityButtonsHighlight();
            UpdateFpsButtonsHighlight();
        }

        private void OnMasterVolumeChanged(float val)
        {
            SettingsManager.SetMasterVolume(val);
            if (masterVolumeValueText != null) masterVolumeValueText.text = $"{Mathf.RoundToInt(val * 100)}%";
        }

        private void OnSfxVolumeChanged(float val)
        {
            SettingsManager.SetSfxVolume(val);
            if (sfxVolumeValueText != null) sfxVolumeValueText.text = $"{Mathf.RoundToInt(val * 100)}%";
        }

        private void SetSteeringMode(MobileSteerMode mode)
        {
            PlayClickSound();
            SettingsManager.SetSteerMode(mode);
            UpdateSteeringButtonsHighlight();
            ShowToast($"Sterowanie: {GetSteerModeName(mode)}");
        }

        private void SetGraphicsQuality(int level)
        {
            PlayClickSound();
            SettingsManager.SetGraphicsQuality(level);
            UpdateQualityButtonsHighlight();
            ShowToast($"Grafika: {GetQualityName(level)}");
        }

        private void SetFpsTarget(int fps)
        {
            PlayClickSound();
            SettingsManager.SetTargetFps(fps);
            UpdateFpsButtonsHighlight();
            ShowToast($"Limit klatek: {fps} FPS");
        }

        private void UpdateSteeringButtonsHighlight()
        {
            HighlightButton(steerButtonsModeBtn, SettingsManager.CurrentSteerMode == MobileSteerMode.Buttons);
            HighlightButton(steerTiltModeBtn, SettingsManager.CurrentSteerMode == MobileSteerMode.Tilt);
            HighlightButton(steerJoyModeBtn, SettingsManager.CurrentSteerMode == MobileSteerMode.Joystick);
        }

        private void UpdateQualityButtonsHighlight()
        {
            HighlightButton(qualityLowBtn, SettingsManager.GraphicsQualityLevel == 0);
            HighlightButton(qualityMedBtn, SettingsManager.GraphicsQualityLevel == 1);
            HighlightButton(qualityHighBtn, SettingsManager.GraphicsQualityLevel >= 2);
        }

        private void UpdateFpsButtonsHighlight()
        {
            HighlightButton(fps30Btn, SettingsManager.TargetFps <= 30);
            HighlightButton(fps60Btn, SettingsManager.TargetFps > 30);
        }

        private void HighlightButton(Button btn, bool isActive)
        {
            if (btn == null) return;
            var colors = btn.colors;
            colors.normalColor = isActive ? new Color(0f, 0.8f, 1f, 1f) : new Color(0.2f, 0.25f, 0.32f, 1f);
            btn.colors = colors;
        }

        private string GetSteerModeName(MobileSteerMode mode)
        {
            switch (mode)
            {
                case MobileSteerMode.Buttons: return "Przyciski Ekranowe";
                case MobileSteerMode.Tilt: return "Żyroskop / Przechylanie";
                case MobileSteerMode.Joystick: return "Wirtualny Joystick";
                default: return "Domyślne";
            }
        }

        private string GetQualityName(int level)
        {
            switch (level)
            {
                case 0: return "Niska (Oszczędność baterii)";
                case 1: return "Średnia (Zbalansowana)";
                case 2: return "Wysoka (Maksymalna grafika)";
                default: return "Średnia";
            }
        }

        #endregion

        #region Quick Play & Game Launch

        public void OnClickTestDrive()
        {
            PlayClickSound();
            ShowToast("Ładowanie toru testowego...");
            SceneManager.LoadScene("SampleScene");
        }

        public void OnClickQuitGame()
        {
            PlayClickSound();
            Debug.Log("[MainMenu] Zamykanie aplikacji...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region Toast Notification Helper

        public void ShowToast(string message, float duration = 2.5f)
        {
            if (toastRoot == null || toastText == null) return;

            toastText.text = message;
            toastRoot.SetActive(true);

            if (activeToastCoroutine != null)
            {
                StopCoroutine(activeToastCoroutine);
            }
            activeToastCoroutine = StartCoroutine(HideToastRoutine(duration));
        }

        private IEnumerator HideToastRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }
            activeToastCoroutine = null;
        }

        private void PlayClickSound()
        {
            if (uiAudioSource != null && buttonClickClip != null)
            {
                uiAudioSource.PlayOneShot(buttonClickClip);
            }
        }

        #endregion
    }
}
