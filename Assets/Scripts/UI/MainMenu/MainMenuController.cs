using System.Collections;
using System.Collections.Generic;
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
    [System.Serializable]
    public class LobbyPlayerSlotUI
    {
        public GameObject slotRoot;
        public Text avatarText;
        public Text nicknameText;
        public GameObject hostBadge;
        public GameObject clientBadge;
        public Button kickButton;
        public GameObject emptyIndicator;
        public ulong boundClientId;
    }

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
        [SerializeField] private GameObject testDriveModalPanel;
        [SerializeField] private GameObject lobbyRoomPanel;

        [Header("Lobby Room Panel Elements")]
        [SerializeField] private Text lobbyRoomTitleText;
        [SerializeField] private Text lobbyRoomCodeText;
        [SerializeField] private Text lobbyPlayerCountText;
        [SerializeField] private Text lobbyTrackNameText;
        [SerializeField] private Text lobbyTrackDetailsText;
        [SerializeField] private Button lobbyTrackPrevBtn;
        [SerializeField] private Button lobbyTrackNextBtn;
        [SerializeField] private Button lobbyStartRaceButton;
        [SerializeField] private GameObject lobbyWaitingForHostBanner;
        [SerializeField] private Button lobbyLeaveButton;
        [SerializeField] private Transform lobbyPlayerListContent;
        [SerializeField] private List<LobbyPlayerSlotUI> lobbyPlayerSlots = new List<LobbyPlayerSlotUI>();

        [Header("Test Drive Panel Elements")]
        [SerializeField] private Button testDrivePrevTrackBtn;
        [SerializeField] private Button testDriveNextTrackBtn;
        [SerializeField] private Text testDriveTrackNameText;
        [SerializeField] private Text testDriveTrackDetailsText;
        [SerializeField] private Text testDriveTrackDescText;
        [SerializeField] private Button testDriveStartBtn;
        [SerializeField] private Button testDriveBackBtn;

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

            // Register NetworkLobbyManager listeners
            SubscribeLobbyEvents();
        }

        private void OnDestroy()
        {
            PlayerProfileManager.OnNicknameChanged -= UpdateProfileNicknameDisplay;
            UnsubscribeLobbyEvents();
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
            if (testDriveModalPanel == null) testDriveModalPanel = FindChildRecursively(transform, "Modal_TestDrive")?.gameObject;

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

            // Test Drive Modal Elements
            if (testDrivePrevTrackBtn == null) testDrivePrevTrackBtn = FindComponentByName<Button>("Btn_TDPrevTrack");
            if (testDriveNextTrackBtn == null) testDriveNextTrackBtn = FindComponentByName<Button>("Btn_TDNextTrack");
            if (testDriveTrackNameText == null) testDriveTrackNameText = FindComponentByName<Text>("Txt_TDTrackName");
            if (testDriveTrackDetailsText == null) testDriveTrackDetailsText = FindComponentByName<Text>("Txt_TDTrackDetails");
            if (testDriveTrackDescText == null) testDriveTrackDescText = FindComponentByName<Text>("Txt_TDTrackDesc");
            if (testDriveStartBtn == null) testDriveStartBtn = FindComponentByName<Button>("Btn_TDStartDrive");
            if (testDriveBackBtn == null && testDriveModalPanel != null)
            {
                var card = FindChildRecursively(testDriveModalPanel.transform, "Card_TestDrive");
                if (card != null) testDriveBackBtn = FindChildRecursively(card, "Btn_Back")?.GetComponent<Button>();
            }

            // Lobby Room Elements
            if (lobbyRoomPanel == null) lobbyRoomPanel = FindChildRecursively(transform, "Panel_LobbyRoom")?.gameObject;
            if (lobbyRoomPanel == null)
            {
                BuildRuntimeLobbyRoom();
            }
            if (lobbyRoomTitleText == null) lobbyRoomTitleText = FindComponentByName<Text>("Txt_LobbyTitle");
            if (lobbyRoomCodeText == null) lobbyRoomCodeText = FindComponentByName<Text>("Txt_LobbyCode");
            if (lobbyPlayerCountText == null) lobbyPlayerCountText = FindComponentByName<Text>("Txt_LobbyCount");
            if (lobbyTrackNameText == null) lobbyTrackNameText = FindComponentByName<Text>("Txt_LobbyTrackName");
            if (lobbyTrackDetailsText == null) lobbyTrackDetailsText = FindComponentByName<Text>("Txt_LobbyTrackDetails");
            if (lobbyTrackPrevBtn == null) lobbyTrackPrevBtn = FindComponentByName<Button>("Btn_LobbyTrackPrev");
            if (lobbyTrackNextBtn == null) lobbyTrackNextBtn = FindComponentByName<Button>("Btn_LobbyTrackNext");
            if (lobbyStartRaceButton == null) lobbyStartRaceButton = FindComponentByName<Button>("Btn_LobbyStartRace");
            if (lobbyWaitingForHostBanner == null) lobbyWaitingForHostBanner = FindChildRecursively(transform, "Banner_WaitingForHost")?.gameObject;
            if (lobbyLeaveButton == null) lobbyLeaveButton = FindComponentByName<Button>("Btn_LobbyLeave");
            if (lobbyPlayerListContent == null) lobbyPlayerListContent = FindChildRecursively(transform, "LobbyPlayerListContent");

            // Auto-discover Player Slots
            if (lobbyPlayerSlots == null || lobbyPlayerSlots.Count == 0)
            {
                lobbyPlayerSlots = new List<LobbyPlayerSlotUI>();
                for (int i = 1; i <= 8; i++)
                {
                    var slotObj = FindChildRecursively(transform, $"Slot_{i}");
                    if (slotObj != null)
                    {
                        var slotUI = new LobbyPlayerSlotUI();
                        slotUI.slotRoot = slotObj.gameObject;
                        slotUI.avatarText = FindChildRecursively(slotObj, "Txt_Avatar")?.GetComponent<Text>();
                        slotUI.nicknameText = FindChildRecursively(slotObj, "Txt_Nick")?.GetComponent<Text>();
                        slotUI.hostBadge = FindChildRecursively(slotObj, "Badge_Host")?.gameObject;
                        slotUI.clientBadge = FindChildRecursively(slotObj, "Badge_Client")?.gameObject;
                        slotUI.kickButton = FindChildRecursively(slotObj, "Btn_Kick")?.GetComponent<Button>();
                        slotUI.emptyIndicator = FindChildRecursively(slotObj, "EmptyIndicator")?.gameObject;
                        lobbyPlayerSlots.Add(slotUI);
                    }
                }
            }

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
                testDriveButton.onClick.AddListener(OpenTestDriveModal);
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

            // Test Drive Modal Buttons
            if (testDrivePrevTrackBtn != null)
            {
                testDrivePrevTrackBtn.onClick.RemoveAllListeners();
                testDrivePrevTrackBtn.onClick.AddListener(OnClickTestDrivePrevTrack);
            }
            if (testDriveNextTrackBtn != null)
            {
                testDriveNextTrackBtn.onClick.RemoveAllListeners();
                testDriveNextTrackBtn.onClick.AddListener(OnClickTestDriveNextTrack);
            }
            if (testDriveStartBtn != null)
            {
                testDriveStartBtn.onClick.RemoveAllListeners();
                testDriveStartBtn.onClick.AddListener(OnClickConfirmStartTestDrive);
            }
            if (testDriveBackBtn != null)
            {
                testDriveBackBtn.onClick.RemoveAllListeners();
                testDriveBackBtn.onClick.AddListener(OpenMainPanel);
            }

            // Lobby Room Buttons
            if (lobbyStartRaceButton != null)
            {
                lobbyStartRaceButton.onClick.RemoveAllListeners();
                lobbyStartRaceButton.onClick.AddListener(OnClickLobbyStartRace);
            }
            if (lobbyLeaveButton != null)
            {
                lobbyLeaveButton.onClick.RemoveAllListeners();
                lobbyLeaveButton.onClick.AddListener(OnClickLobbyLeave);
            }
            if (lobbyTrackPrevBtn != null)
            {
                lobbyTrackPrevBtn.onClick.RemoveAllListeners();
                lobbyTrackPrevBtn.onClick.AddListener(OnClickLobbyPrevTrack);
            }
            if (lobbyTrackNextBtn != null)
            {
                lobbyTrackNextBtn.onClick.RemoveAllListeners();
                lobbyTrackNextBtn.onClick.AddListener(OnClickLobbyNextTrack);
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
            if (testDriveModalPanel != null) testDriveModalPanel.SetActive(targetPanel == testDriveModalPanel);
            if (lobbyRoomPanel != null) lobbyRoomPanel.SetActive(targetPanel == lobbyRoomPanel);
        }

        public void OpenMainPanel()
        {
            if (NetworkLobbyManager.Instance != null && !NetworkLobbyManager.Instance.IsInLobbyRoom)
            {
                NetworkLobbyManager.Instance.LeaveLobby();
                NetworkLobbyManager.Instance.StopLanDiscovery();
            }
            SetJoinButtonsInteractable(true);
            ShowPanel(mainPanel);
        }

        public void OpenLobbyRoomPanel()
        {
            RefreshLobbyRoomUI();
            ShowPanel(lobbyRoomPanel);
        }

        public void OpenCreateLobbyPanel()
        {
            if (createLobbyNameInput != null)
            {
                createLobbyNameInput.text = $"{PlayerProfileManager.Nickname}'s Room";
            }
            UpdateTrackSelectionDisplay();
            ShowPanel(createLobbyPanel);
        }

        public void OpenJoinLobbyPanel()
        {
            EnsureLobbyManagerInstance();
            SubscribeLobbyEvents();
            SetJoinButtonsInteractable(true);
            if (joinFeedbackText != null) joinFeedbackText.text = "";
            NetworkLobbyManager.Instance?.StartLanDiscovery();
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
            else if (testDriveModalPanel != null && testDriveModalPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            else if (createLobbyPanel != null && createLobbyPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            else if (joinLobbyPanel != null && joinLobbyPanel.activeSelf)
            {
                OpenMainPanel();
            }
            else if (settingsPanel != null && settingsPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            else if (lobbyRoomPanel != null && lobbyRoomPanel.activeSelf)
            {
                OnClickLobbyLeave();
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
            int trackIndex = LobbySessionData.SelectedTrackIndex;
            bool isPrivate = privateLobbyToggle != null ? privateLobbyToggle.isOn : true;

            // Ensure NetworkLobbyManager is active
            EnsureLobbyManagerInstance();

            bool success = NetworkLobbyManager.Instance.StartLobbyHost(roomName, maxPlayers, trackIndex, isPrivate: isPrivate);
            if (success)
            {
                SubscribeLobbyEvents();
                string privStr = isPrivate ? " (Prywatny — tylko na kod)" : " (Publiczny)";
                ShowToast($"Lobby '{roomName}'{privStr} utworzone!");
                OpenLobbyRoomPanel();
                Debug.Log($"[MainMenu] Stworzono lobby: {roomName}, Prywatne: {isPrivate}, Tor: {LobbySessionData.SelectedTrack.TrackName}, Kod: {LobbySessionData.CurrentRoomCode}, Max: {maxPlayers}");
            }
            else
            {
                ShowToast("Błąd przy uruchamianiu serwera lobby!");
            }
        }

        #endregion

        #region Join Lobby Logic

        public void OnClickConfirmJoinByCode()
        {
            PlayClickSound();

            string input = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpperInvariant() : "";
            if (string.IsNullOrWhiteSpace(input))
            {
                if (joinFeedbackText != null) joinFeedbackText.text = "Wpisz 6-znakowy kod pokoju lub adres IP hosta!";
                ShowToast("Wpisz kod lobby lub IP!");
                return;
            }

            EnsureLobbyManagerInstance();
            SubscribeLobbyEvents();

            SetJoinButtonsInteractable(false);
            if (joinFeedbackText != null) joinFeedbackText.text = $"Szukanie i łączenie z lobby '{input}'...";
            ShowToast($"Szukanie lobby: {input}...");

            bool success = NetworkLobbyManager.Instance.JoinLobbyClient(input, 7777, enteredCode: input, isQuickJoin: false);
            if (success)
            {
                Debug.Log($"[MainMenu] Rozpoczęto łączenie z lobby (kod/IP: {input}). Oczekiwanie na akceptację przez hosta...");
                // Panel pokoju otworzy się dopiero po otrzymaniu MSG_LOBBY_SYNC z serwera (OnLobbyJoinedSuccess).
            }
            else
            {
                SetJoinButtonsInteractable(true);
                ShowToast("Błąd przy próbie połączenia!");
                if (joinFeedbackText != null) joinFeedbackText.text = "Nie udało się rozpocząć połączenia.";
            }
        }

        public void OnClickQuickJoin()
        {
            PlayClickSound();

            EnsureLobbyManagerInstance();
            SubscribeLobbyEvents();

            SetJoinButtonsInteractable(false);
            if (joinFeedbackText != null) joinFeedbackText.text = "Szukanie otwartego publicznego lobby w sieci...";
            ShowToast("Szukanie publicznego pokoju...");

            string targetIp = "127.0.0.1";
            if (NetworkLobbyManager.Instance.TryGetFirstPublicDiscoveredRoom(out var room))
            {
                targetIp = room.ipAddress;
                Debug.Log($"[MainMenu] Wykryto publiczny pokój '{room.roomName}' na IP {targetIp}");
            }

            bool success = NetworkLobbyManager.Instance.JoinLobbyClient(targetIp, 7777, enteredCode: "", isQuickJoin: true);
            if (success)
            {
                Debug.Log($"[MainMenu] Rozpoczęto szybkie łączenie z {targetIp}. Oczekiwanie na odpowiedź serwera...");
                // Panel pokoju otworzy się dopiero po otrzymaniu MSG_LOBBY_SYNC (OnLobbyJoinedSuccess).
            }
            else
            {
                SetJoinButtonsInteractable(true);
                ShowToast("Nie można rozpocząć szukania!");
                if (joinFeedbackText != null) joinFeedbackText.text = "Błąd przy próbie połączenia.";
            }
        }

        private void SetJoinButtonsInteractable(bool state)
        {
            if (confirmJoinLobbyButton != null) confirmJoinLobbyButton.interactable = state;
            if (quickJoinButton != null) quickJoinButton.interactable = state;
        }

        #endregion

        #region Lobby Room Logic & Host Controls

        private void EnsureLobbyManagerInstance()
        {
            if (NetworkLobbyManager.Instance == null)
            {
                var existing = FindFirstObjectByType<NetworkLobbyManager>();
                if (existing == null)
                {
                    GameObject obj = new GameObject("[NetworkLobbyManager]", typeof(NetworkLobbyManager));
                }
            }
        }

        private void SubscribeLobbyEvents()
        {
            if (NetworkLobbyManager.Instance == null) return;
            NetworkLobbyManager.Instance.OnLobbyStateUpdated -= RefreshLobbyRoomUI;
            NetworkLobbyManager.Instance.OnPlayerKickedNotice -= HandlePlayerKickedNotice;
            NetworkLobbyManager.Instance.OnLobbyClosedNotice -= HandleLobbyClosedNotice;
            NetworkLobbyManager.Instance.OnConnectionFailedNotice -= HandleConnectionFailedNotice;
            NetworkLobbyManager.Instance.OnLobbyJoinedSuccess -= HandleLobbyJoinedSuccess;

            NetworkLobbyManager.Instance.OnLobbyStateUpdated += RefreshLobbyRoomUI;
            NetworkLobbyManager.Instance.OnPlayerKickedNotice += HandlePlayerKickedNotice;
            NetworkLobbyManager.Instance.OnLobbyClosedNotice += HandleLobbyClosedNotice;
            NetworkLobbyManager.Instance.OnConnectionFailedNotice += HandleConnectionFailedNotice;
            NetworkLobbyManager.Instance.OnLobbyJoinedSuccess += HandleLobbyJoinedSuccess;
        }

        private void UnsubscribeLobbyEvents()
        {
            if (NetworkLobbyManager.Instance == null) return;
            NetworkLobbyManager.Instance.OnLobbyStateUpdated -= RefreshLobbyRoomUI;
            NetworkLobbyManager.Instance.OnPlayerKickedNotice -= HandlePlayerKickedNotice;
            NetworkLobbyManager.Instance.OnLobbyClosedNotice -= HandleLobbyClosedNotice;
            NetworkLobbyManager.Instance.OnConnectionFailedNotice -= HandleConnectionFailedNotice;
            NetworkLobbyManager.Instance.OnLobbyJoinedSuccess -= HandleLobbyJoinedSuccess;
        }

        public void RefreshLobbyRoomUI()
        {
            if (lobbyRoomPanel == null || !lobbyRoomPanel.activeInHierarchy) return;
            if (lobbyPlayerSlots == null || lobbyPlayerSlots.Count == 0) AutoFindMissingReferences();

            bool isHost = NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.IsLocalHost;
            var players = NetworkLobbyManager.Instance != null ? NetworkLobbyManager.Instance.CurrentPlayers : new List<LobbyPlayerData>();
            int maxPlayers = LobbySessionData.MaxPlayers;
            var track = LobbySessionData.SelectedTrack;

            if (lobbyRoomTitleText != null)
            {
                lobbyRoomTitleText.text = $"POKÓJ: {LobbySessionData.CurrentRoomName.ToUpperInvariant()}";
            }

            if (lobbyRoomCodeText != null)
            {
                string hostIp = LobbySessionData.ServerAddress;
                string privacyTag = LobbySessionData.IsPrivate ? "🔒 PRYWATNY (TYLKO KOD)" : "🌐 PUBLICZNY";
                lobbyRoomCodeText.text = $"KOD: {LobbySessionData.CurrentRoomCode}   •   IP: {hostIp}:{LobbySessionData.ServerPort}   •   {privacyTag}";
            }

            if (lobbyPlayerCountText != null)
            {
                lobbyPlayerCountText.text = $"{players.Count} / {maxPlayers} GRACZY";
            }

            if (lobbyTrackNameText != null)
            {
                lobbyTrackNameText.text = track.TrackName;
            }

            if (lobbyTrackDetailsText != null)
            {
                lobbyTrackDetailsText.text = $"{track.LengthKm} • {track.SurfaceType} • {track.Difficulty}";
            }

            // Host controls vs Client indicator
            if (lobbyStartRaceButton != null)
            {
                lobbyStartRaceButton.gameObject.SetActive(isHost);
                lobbyStartRaceButton.interactable = players.Count >= 1;
            }

            if (lobbyWaitingForHostBanner != null)
            {
                lobbyWaitingForHostBanner.SetActive(!isHost);
            }

            if (lobbyTrackPrevBtn != null) lobbyTrackPrevBtn.gameObject.SetActive(isHost);
            if (lobbyTrackNextBtn != null) lobbyTrackNextBtn.gameObject.SetActive(isHost);

            // Synchronize each player slot
            for (int i = 0; i < lobbyPlayerSlots.Count; i++)
            {
                var slot = lobbyPlayerSlots[i];
                if (slot == null || slot.slotRoot == null) continue;

                if (i < players.Count)
                {
                    var player = players[i];
                    slot.boundClientId = player.clientId;
                    slot.slotRoot.SetActive(true);

                    if (slot.emptyIndicator != null) slot.emptyIndicator.SetActive(false);
                    if (slot.avatarText != null) slot.avatarText.text = player.isHost ? "👑" : "🏎️";
                    if (slot.nicknameText != null) slot.nicknameText.text = player.nickname;
                    if (slot.hostBadge != null) slot.hostBadge.SetActive(player.isHost);
                    if (slot.clientBadge != null) slot.clientBadge.SetActive(!player.isHost);

                    // Host Kick Button: Visible ONLY for Host, and NOT on the Host's own row!
                    if (slot.kickButton != null)
                    {
                        bool canKick = isHost && !player.isHost;
                        slot.kickButton.gameObject.SetActive(canKick);
                        if (canKick)
                        {
                            ulong targetId = player.clientId;
                            slot.kickButton.onClick.RemoveAllListeners();
                            slot.kickButton.onClick.AddListener(() => OnClickLobbyKickPlayer(targetId));
                        }
                    }
                }
                else if (i < maxPlayers)
                {
                    // Empty available slot
                    slot.boundClientId = ulong.MaxValue;
                    slot.slotRoot.SetActive(true);

                    if (slot.emptyIndicator != null) slot.emptyIndicator.SetActive(true);
                    if (slot.avatarText != null) slot.avatarText.text = "—";
                    if (slot.nicknameText != null) slot.nicknameText.text = "Wolne miejsce...";
                    if (slot.hostBadge != null) slot.hostBadge.SetActive(false);
                    if (slot.clientBadge != null) slot.clientBadge.SetActive(false);
                    if (slot.kickButton != null) slot.kickButton.gameObject.SetActive(false);
                }
                else
                {
                    // Slot beyond current room capacity
                    slot.slotRoot.SetActive(false);
                }
            }
        }

        public void OnClickLobbyKickPlayer(ulong targetClientId)
        {
            PlayClickSound();
            if (NetworkLobbyManager.Instance == null || !NetworkLobbyManager.Instance.IsLocalHost) return;

            Debug.Log($"<color=red>[MainMenu]</color> Host kicked Client {targetClientId}");
            NetworkLobbyManager.Instance.KickPlayer(targetClientId, "Zostałeś usunięty z lobby przez gospodarza.");
            ShowToast("Wyrzucono gracza z lobby.");
        }

        public void OnClickLobbyStartRace()
        {
            PlayClickSound();
            if (NetworkLobbyManager.Instance == null || !NetworkLobbyManager.Instance.IsLocalHost) return;

            ShowToast("Rozpoczynanie wyścigu! Ładowanie toru...");
            NetworkLobbyManager.Instance.StartRace();
        }

        public void OnClickLobbyLeave()
        {
            PlayClickSound();
            if (NetworkLobbyManager.Instance != null)
            {
                NetworkLobbyManager.Instance.LeaveLobby();
            }
            SetJoinButtonsInteractable(true);
            ShowPanel(mainPanel);
            ShowToast("Opuszczono lobby.");
        }

        public void OnClickLobbyPrevTrack()
        {
            PlayClickSound();
            if (NetworkLobbyManager.Instance != null)
            {
                NetworkLobbyManager.Instance.PrevTrack();
            }
        }

        public void OnClickLobbyNextTrack()
        {
            PlayClickSound();
            if (NetworkLobbyManager.Instance != null)
            {
                NetworkLobbyManager.Instance.NextTrack();
            }
        }

        private void HandleLobbyJoinedSuccess()
        {
            SetJoinButtonsInteractable(true);
            OpenLobbyRoomPanel();
            ShowToast("Pomyślnie połączono z lobby!");
        }

        private void HandlePlayerKickedNotice(string reason)
        {
            SetJoinButtonsInteractable(true);
            if (lobbyRoomPanel != null && lobbyRoomPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            if (joinFeedbackText != null)
            {
                joinFeedbackText.text = reason;
            }
            ShowToast(reason, 5f);
        }

        private void HandleLobbyClosedNotice(string reason)
        {
            SetJoinButtonsInteractable(true);
            ShowPanel(mainPanel);
            if (joinFeedbackText != null)
            {
                joinFeedbackText.text = reason;
            }
            ShowToast(reason, 3.5f);
        }

        private void HandleConnectionFailedNotice(string error)
        {
            SetJoinButtonsInteractable(true);
            if (lobbyRoomPanel != null && lobbyRoomPanel.activeSelf)
            {
                ShowPanel(mainPanel);
            }
            if (joinFeedbackText != null)
            {
                joinFeedbackText.text = error;
            }
            ShowToast(error, 4.5f);
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

        public void OpenTestDriveModal()
        {
            UpdateTestDriveTrackUI();
            ShowPanel(testDriveModalPanel);
        }

        public void OnClickTestDrive() => OpenTestDriveModal();

        public void OnClickTestDrivePrevTrack()
        {
            PlayClickSound();
            LobbySessionData.PrevTrack();
            UpdateTestDriveTrackUI();
        }

        public void OnClickTestDriveNextTrack()
        {
            PlayClickSound();
            LobbySessionData.NextTrack();
            UpdateTestDriveTrackUI();
        }

        private void UpdateTestDriveTrackUI()
        {
            var track = LobbySessionData.SelectedTrack;
            if (testDriveTrackNameText != null)
            {
                testDriveTrackNameText.text = track.TrackName;
            }
            if (testDriveTrackDetailsText != null)
            {
                testDriveTrackDetailsText.text = $"{track.LengthKm} • {track.SurfaceType} • Trudność: {track.Difficulty}";
            }
            if (testDriveTrackDescText != null)
            {
                testDriveTrackDescText.text = track.Description;
            }
        }

        public void OnClickConfirmStartTestDrive()
        {
            PlayClickSound();
            var track = LobbySessionData.SelectedTrack;

            // Prepare session data for solo test drive
            LobbySessionData.IsHost = true;
            LobbySessionData.CurrentRoomName = "Trening Solo";

            ShowToast($"Ładowanie toru: {track.TrackName}...");
            Debug.Log($"[MainMenu] Uruchamianie jazdy testowej na torze: {track.TrackName} (Scena: {track.SceneName})");

            string sceneName = track.SceneName;
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogWarning($"[MainMenu] Scena '{sceneName}' nie jest dodana do Build Settings! Ładowanie domyślnej SampleScene.");
                SceneManager.LoadScene("SampleScene");
            }
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

        #region Runtime Lobby Room UI Fallback Generator

        private void BuildRuntimeLobbyRoom()
        {
            Transform parentTransform = transform.Find("SafeAreaContainer") ?? transform;

            // Overlay backdrop
            lobbyRoomPanel = CreateUIRect("Panel_LobbyRoom", parentTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            lobbyRoomPanel.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            lobbyRoomPanel.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            var overlayImg = lobbyRoomPanel.AddComponent<Image>();
            overlayImg.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);

            // Card
            GameObject card = CreateUIRect("Card_LobbyRoom", lobbyRoomPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 720f));
            card.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.1f, 0.13f, 0.18f, 0.98f);

            Font font = GetActiveFont();

            // Header
            lobbyRoomTitleText = CreateRuntimeText(card.transform, "Txt_LobbyTitle", new Vector2(0f, 310f), new Vector2(980f, 42f), "POKÓJ: POKÓJ MISTRZÓW", 30, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), FontStyle.Bold, font);
            lobbyRoomCodeText = CreateRuntimeText(card.transform, "Txt_LobbyCode", new Vector2(0f, 275f), new Vector2(980f, 28f), "KOD: RACE01   •   IP HOSTA: 127.0.0.1:7777", 17, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f), FontStyle.Normal, font);
            lobbyPlayerCountText = CreateRuntimeText(card.transform, "Txt_LobbyCount", new Vector2(0f, 245f), new Vector2(980f, 30f), "1 / 4 GRACZY W POKOJU", 19, TextAnchor.MiddleCenter, new Color(0.04f, 0.76f, 0.55f), FontStyle.Bold, font);

            // Left: Track Info
            GameObject trackCard = CreateUIRect("Card_LobbyTrackInfo", card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, 15f), new Vector2(340f, 420f));
            trackCard.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            var tcImg = trackCard.AddComponent<Image>();
            tcImg.color = new Color(0.06f, 0.08f, 0.13f, 0.95f);

            CreateRuntimeText(trackCard.transform, "Lbl_TrackHeader", new Vector2(0f, 170f), new Vector2(300f, 30f), "WYBRANY TOR", 20, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.1f), FontStyle.Bold, font);

            GameObject trackBox = CreateUIRect("TrackBox", trackCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 85f), new Vector2(310f, 110f));
            trackBox.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            var tbImg = trackBox.AddComponent<Image>();
            tbImg.color = new Color(0.1f, 0.13f, 0.19f, 0.9f);

            lobbyTrackPrevBtn = CreateRuntimeButton(trackBox.transform, "Btn_LobbyTrackPrev", new Vector2(-120f, 0f), new Vector2(46f, 70f), "◀", new Color(0.18f, 0.24f, 0.34f), new Color(0f, 0.88f, 1f), 24, font);
            lobbyTrackNextBtn = CreateRuntimeButton(trackBox.transform, "Btn_LobbyTrackNext", new Vector2(120f, 0f), new Vector2(46f, 70f), "▶", new Color(0.18f, 0.24f, 0.34f), new Color(0f, 0.88f, 1f), 24, font);
            lobbyTrackNameText = CreateRuntimeText(trackBox.transform, "Txt_LobbyTrackName", new Vector2(0f, 15f), new Vector2(190f, 44f), "TOR GŁÓWNY GP (ASFALT)", 16, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), FontStyle.Bold, font);
            lobbyTrackDetailsText = CreateRuntimeText(trackBox.transform, "Txt_LobbyTrackDetails", new Vector2(0f, -22f), new Vector2(210f, 30f), "2.4 km • Asfalt • Średnia", 13, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f), FontStyle.Bold, font);

            GameObject noteBox = CreateUIRect("MatchNoteBox", trackCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(310f, 150f));
            noteBox.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            var nbImg = noteBox.AddComponent<Image>();
            nbImg.color = new Color(0.08f, 0.1f, 0.15f, 0.8f);
            CreateRuntimeText(noteBox.transform, "Txt_Notes", Vector2.zero, new Vector2(290f, 130f),
                "★ Multiplayer: Netcode for GameObjects\n★ Synchronizacja pozycji i kolizji fizyki\n★ Gospodarz może usuwać graczy z pokoju\n★ Start: Równy start z pól startowych",
                14, TextAnchor.MiddleLeft, new Color(0.7f, 0.78f, 0.9f), FontStyle.Normal, font);

            // Right: Player List
            GameObject playerCol = CreateUIRect("Col_LobbyPlayers", card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(175f, 15f), new Vector2(650f, 420f));
            playerCol.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            var pcImg = playerCol.AddComponent<Image>();
            pcImg.color = new Color(0.06f, 0.08f, 0.13f, 0.95f);

            CreateRuntimeText(playerCol.transform, "Lbl_PlayersHeader", new Vector2(-180f, 185f), new Vector2(250f, 30f), "LISTA GRACZY W LOBBY", 19, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold, font);
            CreateRuntimeText(playerCol.transform, "Lbl_HostHint", new Vector2(120f, 185f), new Vector2(350f, 30f), "(Tylko gospodarz może usuwać)", 14, TextAnchor.MiddleRight, new Color(0.6f, 0.65f, 0.75f), FontStyle.Normal, font);

            GameObject listContent = CreateUIRect("LobbyPlayerListContent", playerCol.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(630f, 360f));
            listContent.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            lobbyPlayerListContent = listContent.transform;

            lobbyPlayerSlots = new List<LobbyPlayerSlotUI>();
            for (int i = 1; i <= 8; i++)
            {
                float yPos = 145f - ((i - 1) * 44f);
                GameObject slotObj = CreateUIRect($"Slot_{i}", listContent.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), new Vector2(620f, 40f));
                slotObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                var slotBg = slotObj.AddComponent<Image>();
                slotBg.color = (i % 2 == 0) ? new Color(0.09f, 0.12f, 0.18f, 0.95f) : new Color(0.12f, 0.15f, 0.22f, 0.95f);

                GameObject avBox = CreateUIRect("AvatarBox", slotObj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(30f, 30f));
                avBox.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                var avBg = avBox.AddComponent<Image>();
                avBg.color = new Color(0.18f, 0.24f, 0.35f);
                Text avTxt = CreateRuntimeText(avBox.transform, "Txt_Avatar", Vector2.zero, new Vector2(30f, 30f), "★", 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold, font);

                Text nickTxt = CreateRuntimeText(slotObj.transform, "Txt_Nick", new Vector2(-90f, 0f), new Vector2(210f, 32f), $"Driver_0{i}", 18, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold, font);

                GameObject hBadge = CreateUIRect("Badge_Host", slotObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(75f, 0f), new Vector2(115f, 26f));
                hBadge.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                var hbImg = hBadge.AddComponent<Image>();
                hbImg.color = new Color(0.85f, 0.65f, 0.1f, 0.9f);
                CreateRuntimeText(hBadge.transform, "Txt", Vector2.zero, new Vector2(115f, 26f), "👑 GOSPODARZ", 12, TextAnchor.MiddleCenter, Color.black, FontStyle.Bold, font);
                hBadge.SetActive(i == 1);

                GameObject cBadge = CreateUIRect("Badge_Client", slotObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(75f, 0f), new Vector2(115f, 26f));
                cBadge.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                var cbImg = cBadge.AddComponent<Image>();
                cbImg.color = new Color(0.08f, 0.45f, 0.8f, 0.85f);
                CreateRuntimeText(cBadge.transform, "Txt", Vector2.zero, new Vector2(115f, 26f), "🏎️ GRACZ", 12, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold, font);
                cBadge.SetActive(false);

                Button kickBtn = CreateRuntimeButton(slotObj.transform, "Btn_Kick", new Vector2(245f, 0f), new Vector2(95f, 30f), "WYRZUĆ", new Color(0.85f, 0.2f, 0.2f), Color.white, 14, font);
                kickBtn.gameObject.SetActive(false);

                GameObject emptyInd = CreateUIRect("EmptyIndicator", slotObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(400f, 30f));
                emptyInd.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                CreateRuntimeText(emptyInd.transform, "Txt", Vector2.zero, new Vector2(400f, 30f), "— Wolne miejsce (Oczekiwanie na dołączenie) —", 14, TextAnchor.MiddleCenter, new Color(0.45f, 0.5f, 0.6f), FontStyle.Normal, font);
                emptyInd.SetActive(i > 1);

                lobbyPlayerSlots.Add(new LobbyPlayerSlotUI
                {
                    slotRoot = slotObj,
                    avatarText = avTxt,
                    nicknameText = nickTxt,
                    hostBadge = hBadge,
                    clientBadge = cBadge,
                    kickButton = kickBtn,
                    emptyIndicator = emptyInd,
                    boundClientId = ulong.MaxValue
                });
            }

            // Bottom Buttons
            lobbyStartRaceButton = CreateRuntimeButton(card.transform, "Btn_LobbyStartRace", new Vector2(180f, -270f), new Vector2(340f, 66f), "ROZPOCZNIJ WYŚCIG", new Color(0.04f, 0.78f, 0.55f), Color.white, 22, font);

            lobbyWaitingForHostBanner = CreateUIRect("Banner_WaitingForHost", card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, -270f), new Vector2(340f, 66f));
            lobbyWaitingForHostBanner.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            var wbBg = lobbyWaitingForHostBanner.AddComponent<Image>();
            wbBg.color = new Color(0.14f, 0.18f, 0.26f, 0.9f);
            CreateRuntimeText(lobbyWaitingForHostBanner.transform, "Txt", Vector2.zero, new Vector2(320f, 60f), "⏳ Oczekiwanie na start\nprzez gospodarza...", 17, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.2f), FontStyle.Bold, font);
            lobbyWaitingForHostBanner.SetActive(false);

            lobbyLeaveButton = CreateRuntimeButton(card.transform, "Btn_LobbyLeave", new Vector2(-280f, -270f), new Vector2(250f, 66f), "OPUŚĆ LOBBY", new Color(0.5f, 0.2f, 0.25f), Color.white, 20, font);

            lobbyRoomPanel.SetActive(false);
        }

        private GameObject CreateUIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return obj;
        }

        private Text CreateRuntimeText(Transform parent, string name, Vector2 pos, Vector2 size, string content, int fontSize, TextAnchor alignment, Color color, FontStyle style, Font font)
        {
            GameObject obj = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            obj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            Text txt = obj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.color = color;
            txt.text = content;
            return txt;
        }

        private Button CreateRuntimeButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, Color bgColor, Color textColor, int fontSize, Font font)
        {
            GameObject obj = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            obj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            Image img = obj.AddComponent<Image>();
            img.color = bgColor;
            Button btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            CreateRuntimeText(obj.transform, "Label", Vector2.zero, size, label, fontSize, TextAnchor.MiddleCenter, textColor, FontStyle.Bold, font);
            return btn;
        }

        private Font GetActiveFont()
        {
            if (profileNicknameText != null && profileNicknameText.font != null)
                return profileNicknameText.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        #endregion
    }
}