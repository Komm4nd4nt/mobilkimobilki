using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using RacingMobile.Core;
using RacingMobile.MobileUI;
using RacingMobile.Multiplayer;
using RacingMobile.UI.MainMenu;
using RacingMobile.Vehicle;

namespace RacingMobile.Editor
{
    /// <summary>
    /// Editor automation tool that builds a complete, high-polish Mobile Main Menu scene,
    /// sets up responsive UI panels, player nickname editor, lobby creation & joining modals,
    /// settings menu, and registers scenes in EditorBuildSettings.
    /// </summary>
    [InitializeOnLoad]
    public static class MainMenuSceneBuilder
    {
        private static Font defaultFont;

        static MainMenuSceneBuilder()
        {
            EditorApplication.delayCall += CheckAndBuildMenuScene;
        }

        private static void CheckAndBuildMenuScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name == "MainMenu" || activeScene.name == "MainMenuScene")
            {
                if (UnityEngine.Object.FindFirstObjectByType<MainMenuController>() == null)
                {
                    BuildMenuScene();
                }
            }
        }

        private static Font GetFont()
        {
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return defaultFont;
        }

        [MenuItem("Racing Mobile/Build Main Menu Scene", false, 2)]
        public static void BuildMenuScene()
        {
            string scenePath = System.IO.File.Exists("Assets/Scenes/MainMenu.unity")
                ? "Assets/Scenes/MainMenu.unity"
                : "Assets/Scenes/MainMenuScene.unity";

            var activeScene = EditorSceneManager.GetActiveScene();
            bool isCurrentMenu = activeScene.name == "MainMenu" || activeScene.name == "MainMenuScene";

            UnityEngine.SceneManagement.Scene scene;
            if (isCurrentMenu)
            {
                scene = activeScene;
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            // Clean up previous elements if rebuilding
            var oldCanvas = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            if (oldCanvas != null) UnityEngine.Object.DestroyImmediate(oldCanvas.gameObject);

            var oldShowroom = GameObject.Find("Showroom_Pedestal");
            if (oldShowroom != null) UnityEngine.Object.DestroyImmediate(oldShowroom);

            var oldCar = GameObject.Find("Showroom_ShowcaseCar");
            if (oldCar != null) UnityEngine.Object.DestroyImmediate(oldCar);

            var oldLight = GameObject.Find("Showroom_Light");
            if (oldLight != null) UnityEngine.Object.DestroyImmediate(oldLight);

            var oldRim = GameObject.Find("Showroom_RimLight");
            if (oldRim != null) UnityEngine.Object.DestroyImmediate(oldRim);

            // 2. Setup Camera & 3D Background Showroom
            SetupEnvironment();

            // 3. Setup Event System
            SetupEventSystem();

            // 4. Setup Canvas and all UI
            GameObject canvasObj = BuildCanvas();

            // 5. Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);

            // 6. Update Build Settings so MainMenu is Scene 0 and SampleScene is Scene 1
            ConfigureBuildSettings();

            Debug.Log("<color=cyan>[MainMenuSceneBuilder]</color> Main Menu scene successfully built and saved to " + scenePath);
        }

        [MenuItem("Racing Mobile/Configure Build Settings (Add Scenes)", false, 3)]
        public static void ConfigureBuildSettings()
        {
            var editorScenes = new List<EditorBuildSettingsScene>();

            string menuPath = System.IO.File.Exists("Assets/Scenes/MainMenu.unity")
                ? "Assets/Scenes/MainMenu.unity"
                : "Assets/Scenes/MainMenuScene.unity";
            string gamePath = "Assets/Scenes/SampleScene.unity";

            if (System.IO.File.Exists(menuPath))
            {
                editorScenes.Add(new EditorBuildSettingsScene(menuPath, true));
            }
            if (System.IO.File.Exists(gamePath))
            {
                editorScenes.Add(new EditorBuildSettingsScene(gamePath, true));
            }

            EditorBuildSettings.scenes = editorScenes.ToArray();
            Debug.Log($"<color=green>[MainMenuSceneBuilder]</color> Build Settings updated! Scene 0: {menuPath}, Scene 1: {gamePath}");
        }

        private static void SetupEnvironment()
        {
            // Main Camera
            UnityEngine.Camera existingCam = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Camera>();
            GameObject camObj = existingCam != null ? existingCam.gameObject : new GameObject("Main Camera", typeof(UnityEngine.Camera), typeof(AudioListener));
            var cam = camObj.GetComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f); // Sleek dark slate
            camObj.transform.position = new Vector3(0f, 1.3f, -4.5f);
            camObj.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            // Directional Light
            GameObject lightObj = new GameObject("Showroom_Light", typeof(Light));
            var light = lightObj.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.95f, 0.98f, 1f);
            light.intensity = 1.3f;
            lightObj.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            // Rim / Accent Light (Neon Cyan)
            GameObject rimLightObj = new GameObject("Showroom_RimLight", typeof(Light));
            var rimLight = rimLightObj.GetComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0f, 0.85f, 1f);
            rimLight.intensity = 0.8f;
            rimLightObj.transform.rotation = Quaternion.Euler(-20f, 150f, 0f);

            // Showroom Pedestal & Showcase Car
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Showroom_Pedestal";
            pedestal.transform.position = new Vector3(2.6f, -0.6f, 2.5f);
            pedestal.transform.localScale = new Vector3(5.5f, 0.15f, 5.5f);

            Material pedMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            pedMat.color = new Color(0.1f, 0.12f, 0.16f);
            pedestal.GetComponent<MeshRenderer>().sharedMaterial = pedMat;
            UnityEngine.Object.DestroyImmediate(pedestal.GetComponent<Collider>());

            // Spawn car prefab if available
            GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RacingCar_Netcode.prefab");
            if (carPrefab != null)
            {
                GameObject carInstance = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
                carInstance.name = "Showroom_ShowcaseCar";
                carInstance.transform.position = new Vector3(2.6f, -0.45f, 2.5f);
                carInstance.transform.rotation = Quaternion.Euler(0f, -40f, 0f);

                // Add slow showroom rotation
                carInstance.AddComponent<ShowroomRotator>();

                // Remove unneeded components for menu display
                var rb = carInstance.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                var netObj = carInstance.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null) UnityEngine.Object.DestroyImmediate(netObj);
                var netSync = carInstance.GetComponent<CarNetworkSync>();
                if (netSync != null) UnityEngine.Object.DestroyImmediate(netSync);
                var netCtrl = carInstance.GetComponent<NetworkCarController>();
                if (netCtrl != null) UnityEngine.Object.DestroyImmediate(netCtrl);
                var audioVis = carInstance.GetComponent<CarAudioVisuals>();
                if (audioVis != null) UnityEngine.Object.DestroyImmediate(audioVis);
            }
        }

        private static void SetupEventSystem()
        {
            var existing = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0)
            {
                for (int i = 1; i < existing.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
                }

                var mainEs = existing[0];
                var legacyModule = mainEs.GetComponent<StandaloneInputModule>();
                if (legacyModule != null) UnityEngine.Object.DestroyImmediate(legacyModule);

                if (mainEs.GetComponent<InputSystemUIInputModule>() == null)
                {
                    mainEs.gameObject.AddComponent<InputSystemUIInputModule>();
                }
                return;
            }

            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static GameObject BuildCanvas()
        {
            // Canvas Root
            GameObject canvasObj = new GameObject("MobileMainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Safe Area Wrapper
            GameObject safeAreaObj = new GameObject("SafeAreaContainer", typeof(RectTransform), typeof(MobileSafeArea));
            safeAreaObj.transform.SetParent(canvasObj.transform, false);
            RectTransform safeRect = safeAreaObj.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;

            // Main Menu Controller Component
            MainMenuController controller = canvasObj.AddComponent<MainMenuController>();

            // UI AudioSource
            AudioSource uiAudio = canvasObj.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            SetPrivateField(controller, "uiAudioSource", uiAudio);

            // ================= 1. TOP HEADER & PROFILE BAR =================
            BuildHeaderAndProfile(safeAreaObj.transform, controller);

            // ================= 2. MAIN NAVIGATION PANEL =================
            GameObject mainPanel = BuildMainNavigationPanel(safeAreaObj.transform, controller);
            SetPrivateField(controller, "mainPanel", mainPanel);

            // ================= 3. CREATE LOBBY MODAL =================
            GameObject createPanel = BuildCreateLobbyModal(safeAreaObj.transform, controller);
            SetPrivateField(controller, "createLobbyPanel", createPanel);
            createPanel.SetActive(false);

            // ================= 4. JOIN LOBBY MODAL =================
            GameObject joinPanel = BuildJoinLobbyModal(safeAreaObj.transform, controller);
            SetPrivateField(controller, "joinLobbyPanel", joinPanel);
            joinPanel.SetActive(false);

            // ================= 5. SETTINGS MODAL =================
            GameObject settingsPanel = BuildSettingsModal(safeAreaObj.transform, controller);
            SetPrivateField(controller, "settingsPanel", settingsPanel);
            settingsPanel.SetActive(false);

            // ================= 6. PROFILE EDIT MODAL =================
            GameObject profileModal = BuildProfileEditModal(safeAreaObj.transform, controller);
            SetPrivateField(controller, "profileModalPanel", profileModal);
            profileModal.SetActive(false);

            // ================= 7. TOAST NOTIFICATION =================
            BuildToastNotification(safeAreaObj.transform, controller);

            return canvasObj;
        }

        private static void BuildHeaderAndProfile(Transform parent, MainMenuController controller)
        {
            // Top Bar Container
            GameObject topBar = CreateUIRect("TopBar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(-80f, 100f));
            RectTransform tbRect = topBar.GetComponent<RectTransform>();
            tbRect.pivot = new Vector2(0.5f, 1f);

            // Game Logo / Title (Left)
            GameObject titleObj = CreateUIRect("TitleContainer", topBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(500f, 90f));
            titleObj.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);

            Text titleText = CreateText(titleObj.transform, "TitleText", new Vector2(0f, 16f), new Vector2(500f, 50f), "MOBILKI RACING", 44, TextAnchor.MiddleLeft, new Color(0f, 0.88f, 1f, 1f), FontStyle.Bold);
            Text subText = CreateText(titleObj.transform, "SubtitleText", new Vector2(2f, -22f), new Vector2(500f, 30f), "MULTIPLAYER MOBILE RACING • NETCODE NGO", 17, TextAnchor.MiddleLeft, new Color(0.7f, 0.75f, 0.85f, 0.85f));

            // Player Profile Badge (Right)
            GameObject profileObj = CreateUIRect("ProfileBadge", topBar.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(360f, 75f));
            profileObj.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);

            Image profileBg = profileObj.AddComponent<Image>();
            profileBg.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

            // Avatar Icon circle/box
            GameObject avatarBox = CreateUIRect("AvatarBox", profileObj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(35f, 0f), new Vector2(52f, 52f));
            avatarBox.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            Image avImg = avatarBox.AddComponent<Image>();
            avImg.color = new Color(0f, 0.82f, 1f, 1f);
            CreateText(avatarBox.transform, "AvText", Vector2.zero, new Vector2(52f, 52f), "★", 30, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

            // Nickname Label
            CreateText(profileObj.transform, "NickHeader", new Vector2(75f, 15f), new Vector2(160f, 22f), "KIEROWCA", 13, TextAnchor.MiddleLeft, new Color(0.6f, 0.7f, 0.8f));
            Text nickText = CreateText(profileObj.transform, "PlayerNickText", new Vector2(75f, -10f), new Vector2(180f, 32f), "Driver_01", 23, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            SetPrivateField(controller, "profileNicknameText", nickText);

            // Edit Nick Button
            Button editBtn = CreateButton(profileObj.transform, "Btn_EditNick", new Vector2(135f, 0f), new Vector2(70f, 48f), "ZMIEŃ", new Color(0.2f, 0.28f, 0.38f), Color.white, 16);
            editBtn.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            SetPrivateField(controller, "editProfileButton", editBtn);
        }

        private static GameObject BuildMainNavigationPanel(Transform parent, MainMenuController controller)
        {
            GameObject panel = CreateUIRect("Panel_MainMenu", parent, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            RectTransform pRect = panel.GetComponent<RectTransform>();
            pRect.offsetMin = Vector2.zero;
            pRect.offsetMax = Vector2.zero;

            // Menu Buttons Container on Left
            GameObject menuCol = CreateUIRect("MenuButtonsColumn", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, -40f), new Vector2(440f, 540f));
            menuCol.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);

            // 1. STWÓRZ LOBBY Button (Primary Cyan/Emerald Accent)
            Button createLobbyBtn = CreateMenuActionButton(menuCol.transform, "Btn_CreateLobby", new Vector2(0f, 180f), new Vector2(420f, 82f),
                "STWÓRZ LOBBY", "Graj ze znajomymi w prywatnym pokoju", new Color(0.04f, 0.76f, 0.55f, 0.95f), 26);
            SetPrivateField(controller, "openCreateLobbyButton", createLobbyBtn);

            // 2. DOŁĄCZ DO LOBBY Button (Electric Blue Accent)
            Button joinLobbyBtn = CreateMenuActionButton(menuCol.transform, "Btn_JoinLobby", new Vector2(0f, 85f), new Vector2(420f, 82f),
                "DOŁĄCZ DO LOBBY", "Wpisz kod pokoju lub znajdź mecz", new Color(0.08f, 0.55f, 0.95f, 0.95f), 26);
            SetPrivateField(controller, "openJoinLobbyButton", joinLobbyBtn);

            // 3. JAZDA TESTOWA (SOLO) Button (Amber Accent)
            Button testDriveBtn = CreateMenuActionButton(menuCol.transform, "Btn_TestDrive", new Vector2(0f, -10f), new Vector2(420f, 76f),
                "TRENING SOLO", "Wejdź od razu na tor i testuj fizykę auta", new Color(0.95f, 0.65f, 0.1f, 0.9f), 23);
            SetPrivateField(controller, "testDriveButton", testDriveBtn);

            // 4. USTAWIENIA Button (Slate Metal Accent)
            Button settingsBtn = CreateMenuActionButton(menuCol.transform, "Btn_Settings", new Vector2(0f, -100f), new Vector2(420f, 72f),
                "USTAWIENIA", "Dźwięk, sterowanie żyroskopem, 60 FPS", new Color(0.24f, 0.29f, 0.38f, 0.9f), 22);
            SetPrivateField(controller, "openSettingsButton", settingsBtn);

            // 5. WYJŚCIE Button (Crimson)
            Button quitBtn = CreateButton(menuCol.transform, "Btn_Quit", new Vector2(210f, -185f), new Vector2(420f, 56f),
                "WYJDŹ Z GRY", new Color(0.6f, 0.15f, 0.15f, 0.85f), Color.white, 20);
            quitBtn.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            SetPrivateField(controller, "quitGameButton", quitBtn);

            // Footer version & hints
            CreateText(panel.transform, "VersionInfo", new Vector2(100f, 30f), new Vector2(400f, 30f),
                "v0.2.0 Alpha • Unity Netcode NGO • Mobile Ready", 16, TextAnchor.MiddleLeft, new Color(0.5f, 0.55f, 0.65f, 0.7f));

            return panel;
        }

        private static GameObject BuildCreateLobbyModal(Transform parent, MainMenuController controller)
        {
            GameObject overlay = CreateModalBackdrop("Modal_CreateLobby", parent);
            GameObject card = CreateModalCard(overlay.transform, "Card_CreateLobby", new Vector2(780f, 660f));

            // Title
            CreateText(card.transform, "Title", new Vector2(0f, 275f), new Vector2(700f, 45f), "STWÓRZ NOWE LOBBY", 32, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), FontStyle.Bold);
            CreateText(card.transform, "Sub", new Vector2(0f, 240f), new Vector2(700f, 30f), "Skonfiguruj pokój wyścigowy dla znajomych", 18, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));

            // Section 1: Room Name Input
            CreateText(card.transform, "Lbl_RoomName", new Vector2(-220f, 180f), new Vector2(240f, 30f), "Nazwa pokoju:", 20, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            InputField nameInput = CreateInputField(card.transform, "Input_RoomName", new Vector2(90f, 180f), new Vector2(380f, 52f), "Wpisz nazwę lobby...", "Pokój Mistrzów");
            SetPrivateField(controller, "createLobbyNameInput", nameInput);

            // Section 2: Track Selector
            CreateText(card.transform, "Lbl_Track", new Vector2(-220f, 100f), new Vector2(240f, 30f), "Wybór toru:", 20, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);

            GameObject trackBox = CreateUIRect("TrackSelectorBox", card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, 100f), new Vector2(380f, 68f));
            trackBox.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            Image trackBoxBg = trackBox.AddComponent<Image>();
            trackBoxBg.color = new Color(0.06f, 0.08f, 0.13f, 0.95f);

            Button prevTrackBtn = CreateButton(trackBox.transform, "Btn_PrevTrack", new Vector2(-155f, 0f), new Vector2(46f, 52f), "<", new Color(0.2f, 0.26f, 0.36f), Color.white, 24, FontStyle.Bold);
            Button nextTrackBtn = CreateButton(trackBox.transform, "Btn_NextTrack", new Vector2(155f, 0f), new Vector2(46f, 52f), ">", new Color(0.2f, 0.26f, 0.36f), Color.white, 24, FontStyle.Bold);

            Text trackNameTxt = CreateText(trackBox.transform, "Txt_TrackName", new Vector2(0f, 12f), new Vector2(250f, 28f), "TOR GŁÓWNY GP (ASFALT)", 18, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), FontStyle.Bold);
            Text trackDetailsTxt = CreateText(trackBox.transform, "Txt_TrackDetails", new Vector2(0f, -14f), new Vector2(250f, 22f), "2.4 km • Gładki asfalt • Trudność: Średnia", 13, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));

            SetPrivateField(controller, "trackPrevButton", prevTrackBtn);
            SetPrivateField(controller, "trackNextButton", nextTrackBtn);
            SetPrivateField(controller, "trackNameText", trackNameTxt);
            SetPrivateField(controller, "trackDetailsText", trackDetailsTxt);

            // Section 3: Max Players Slider
            CreateText(card.transform, "Lbl_MaxPlayers", new Vector2(-220f, 20f), new Vector2(240f, 30f), "Maks. graczy:", 20, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            Slider playersSlider = CreateSlider(card.transform, "Slider_MaxPlayers", new Vector2(20f, 20f), new Vector2(260f, 32f), 2f, 8f, 4f);
            SetPrivateField(controller, "maxPlayersSlider", playersSlider);

            Text playersValText = CreateText(card.transform, "Val_MaxPlayers", new Vector2(215f, 20f), new Vector2(120f, 35f), "4 graczy", 20, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), FontStyle.Bold);
            SetPrivateField(controller, "maxPlayersValueText", playersValText);

            // Section 4: Privacy Toggle
            Toggle privToggle = CreateToggle(card.transform, "Toggle_Private", new Vector2(0f, -55f), new Vector2(480f, 45f), "Tylko na kod / Ze znajomymi (Prywatny)", true);
            SetPrivateField(controller, "privateLobbyToggle", privToggle);

            // Bottom Buttons
            Button createBtn = CreateButton(card.transform, "Btn_ConfirmCreate", new Vector2(130f, -220f), new Vector2(270f, 65f),
                "UTWÓRZ POKÓJ", new Color(0.04f, 0.76f, 0.55f), Color.white, 22, FontStyle.Bold);
            SetPrivateField(controller, "confirmCreateLobbyButton", createBtn);

            Button backBtn = CreateButton(card.transform, "Btn_Back", new Vector2(-155f, -220f), new Vector2(230f, 65f),
                "WRÓĆ", new Color(0.35f, 0.38f, 0.45f), Color.white, 20);
            SetPrivateField(controller, "createLobbyBackButton", backBtn);

            return overlay;
        }

        private static GameObject BuildJoinLobbyModal(Transform parent, MainMenuController controller)
        {
            GameObject overlay = CreateModalBackdrop("Modal_JoinLobby", parent);
            GameObject card = CreateModalCard(overlay.transform, "Card_JoinLobby", new Vector2(760f, 540f));

            // Title
            CreateText(card.transform, "Title", new Vector2(0f, 205f), new Vector2(700f, 45f), "DOŁĄCZ DO LOBBY", 32, TextAnchor.MiddleCenter, new Color(0.1f, 0.65f, 1f), FontStyle.Bold);
            CreateText(card.transform, "Sub", new Vector2(0f, 170f), new Vector2(700f, 30f), "Wpisz 6-znakowy kod pokoju od znajomego lub adres IP", 18, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));

            // Room Code Input
            CreateText(card.transform, "Lbl_Code", new Vector2(0f, 105f), new Vector2(500f, 30f), "Kod pokoju lub IP hosta:", 20, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            InputField codeInput = CreateInputField(card.transform, "Input_JoinCode", new Vector2(0f, 55f), new Vector2(420f, 60f), "np. RACE01 lub 192.168.1.5", "");
            SetPrivateField(controller, "joinCodeInput", codeInput);

            // Join by Code Button
            Button confirmJoinBtn = CreateButton(card.transform, "Btn_ConfirmJoinCode", new Vector2(0f, -25f), new Vector2(420f, 65f),
                "DOŁĄCZ PRZEZ KOD", new Color(0.08f, 0.55f, 0.95f), Color.white, 22, FontStyle.Bold);
            SetPrivateField(controller, "confirmJoinLobbyButton", confirmJoinBtn);

            // Quick Join Button
            Button quickJoinBtn = CreateButton(card.transform, "Btn_QuickJoin", new Vector2(0f, -95f), new Vector2(420f, 52f),
                "SZYBKIE DOŁĄCZENIE (DOWOLNY POKÓJ)", new Color(0.2f, 0.26f, 0.36f), new Color(0.85f, 0.9f, 1f), 18);
            SetPrivateField(controller, "quickJoinButton", quickJoinBtn);

            // Feedback Text
            Text feedbackText = CreateText(card.transform, "Txt_JoinFeedback", new Vector2(0f, -145f), new Vector2(600f, 30f), "", 17, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
            SetPrivateField(controller, "joinFeedbackText", feedbackText);

            // Back Button
            Button backBtn = CreateButton(card.transform, "Btn_Back", new Vector2(0f, -200f), new Vector2(250f, 52f),
                "WRÓĆ", new Color(0.35f, 0.38f, 0.45f), Color.white, 19);
            SetPrivateField(controller, "joinLobbyBackButton", backBtn);

            return overlay;
        }

        private static GameObject BuildSettingsModal(Transform parent, MainMenuController controller)
        {
            GameObject overlay = CreateModalBackdrop("Modal_Settings", parent);
            GameObject card = CreateModalCard(overlay.transform, "Card_Settings", new Vector2(880f, 640f));

            // Title
            CreateText(card.transform, "Title", new Vector2(0f, 260f), new Vector2(700f, 45f), "USTAWIENIA GRY", 32, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), FontStyle.Bold);

            // --- 1. DŹWIĘK ---
            CreateText(card.transform, "Header_Audio", new Vector2(-280f, 195f), new Vector2(240f, 30f), "DŹWIĘK (AUDIO)", 22, TextAnchor.MiddleLeft, new Color(1f, 0.78f, 0.1f), FontStyle.Bold);

            CreateText(card.transform, "Lbl_MasterVol", new Vector2(-240f, 150f), new Vector2(180f, 25f), "Głośność ogólna:", 18, TextAnchor.MiddleLeft, Color.white);
            Slider masterSlider = CreateSlider(card.transform, "Slider_MasterVol", new Vector2(40f, 150f), new Vector2(280f, 26f), 0f, 1f, 1f);
            SetPrivateField(controller, "masterVolumeSlider", masterSlider);
            Text masterValText = CreateText(card.transform, "Val_MasterVol", new Vector2(230f, 150f), new Vector2(70f, 25f), "100%", 18, TextAnchor.MiddleLeft, Color.white);
            SetPrivateField(controller, "masterVolumeValueText", masterValText);

            CreateText(card.transform, "Lbl_SfxVol", new Vector2(-240f, 105f), new Vector2(180f, 25f), "Efekty & Silnik:", 18, TextAnchor.MiddleLeft, Color.white);
            Slider sfxSlider = CreateSlider(card.transform, "Slider_SfxVol", new Vector2(40f, 105f), new Vector2(280f, 26f), 0f, 1f, 0.85f);
            SetPrivateField(controller, "sfxVolumeSlider", sfxSlider);
            Text sfxValText = CreateText(card.transform, "Val_SfxVol", new Vector2(230f, 105f), new Vector2(70f, 25f), "85%", 18, TextAnchor.MiddleLeft, Color.white);
            SetPrivateField(controller, "sfxVolumeValueText", sfxValText);

            // --- 2. STEROWANIE MOBILNE ---
            CreateText(card.transform, "Header_Controls", new Vector2(-280f, 45f), new Vector2(300f, 30f), "STEROWANIE MOBILNE", 22, TextAnchor.MiddleLeft, new Color(1f, 0.78f, 0.1f), FontStyle.Bold);

            Button btnScheme = CreateButton(card.transform, "Btn_SteerButtons", new Vector2(-190f, -5f), new Vector2(180f, 50f), "PRZYCISKI", new Color(0.2f, 0.25f, 0.32f), Color.white, 17);
            Button tiltScheme = CreateButton(card.transform, "Btn_SteerTilt", new Vector2(0f, -5f), new Vector2(180f, 50f), "ŻYROSKOP", new Color(0.2f, 0.25f, 0.32f), Color.white, 17);
            Button joyScheme = CreateButton(card.transform, "Btn_SteerJoy", new Vector2(190f, -5f), new Vector2(180f, 50f), "JOYSTICK", new Color(0.2f, 0.25f, 0.32f), Color.white, 17);
            SetPrivateField(controller, "steerButtonsModeBtn", btnScheme);
            SetPrivateField(controller, "steerTiltModeBtn", tiltScheme);
            SetPrivateField(controller, "steerJoyModeBtn", joyScheme);

            // --- 3. GRAFIKA & PŁYNNOŚĆ ---
            CreateText(card.transform, "Header_Graphics", new Vector2(-280f, -65f), new Vector2(320f, 30f), "GRAFIKA & PŁYNNOŚĆ", 22, TextAnchor.MiddleLeft, new Color(1f, 0.78f, 0.1f), FontStyle.Bold);

            Button qualLow = CreateButton(card.transform, "Btn_QualLow", new Vector2(-190f, -115f), new Vector2(180f, 48f), "NISKA", new Color(0.2f, 0.25f, 0.32f), Color.white, 17);
            Button qualMed = CreateButton(card.transform, "Btn_QualMed", new Vector2(0f, -115f), new Vector2(180f, 48f), "ŚREDNIA", new Color(0.2f, 0.25f, 0.32f), Color.white, 17);
            Button qualHigh = CreateButton(card.transform, "Btn_QualHigh", new Vector2(190f, -115f), new Vector2(180f, 48f), "WYSOKA", new Color(0.2f, 0.25f, 0.32f), Color.white, 17);
            SetPrivateField(controller, "qualityLowBtn", qualLow);
            SetPrivateField(controller, "qualityMedBtn", qualMed);
            SetPrivateField(controller, "qualityHighBtn", qualHigh);

            // FPS
            CreateText(card.transform, "Lbl_Fps", new Vector2(-240f, -180f), new Vector2(180f, 25f), "Limit FPS:", 18, TextAnchor.MiddleLeft, Color.white);
            Button fps30 = CreateButton(card.transform, "Btn_Fps30", new Vector2(-40f, -180f), new Vector2(170f, 44f), "30 FPS (Bateria)", new Color(0.2f, 0.25f, 0.32f), Color.white, 15);
            Button fps60 = CreateButton(card.transform, "Btn_Fps60", new Vector2(150f, -180f), new Vector2(170f, 44f), "60 FPS (Płynnie)", new Color(0.2f, 0.25f, 0.32f), Color.white, 15);
            SetPrivateField(controller, "fps30Btn", fps30);
            SetPrivateField(controller, "fps60Btn", fps60);

            // Back Button
            Button backBtn = CreateButton(card.transform, "Btn_CloseSettings", new Vector2(0f, -250f), new Vector2(300f, 56f),
                "ZAPISZ I ZAMKNIJ", new Color(0.04f, 0.76f, 0.55f), Color.white, 20, FontStyle.Bold);
            SetPrivateField(controller, "settingsCloseButton", backBtn);

            return overlay;
        }

        private static GameObject BuildProfileEditModal(Transform parent, MainMenuController controller)
        {
            GameObject overlay = CreateModalBackdrop("Modal_EditProfile", parent);
            GameObject card = CreateModalCard(overlay.transform, "Card_EditProfile", new Vector2(680f, 400f));

            // Title
            CreateText(card.transform, "Title", new Vector2(0f, 135f), new Vector2(600f, 40f), "EDYTUJ PROFIL KIEROWCY", 28, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), FontStyle.Bold);
            CreateText(card.transform, "Sub", new Vector2(0f, 95f), new Vector2(600f, 30f), "Wpisz swój nick, który będzie widoczny dla znajomych w lobby", 17, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));

            // Nick Input
            InputField nickInput = CreateInputField(card.transform, "Input_ProfileNick", new Vector2(-60f, 25f), new Vector2(340f, 56f), "Wpisz swój nick...", "Driver_01");
            SetPrivateField(controller, "profileNicknameInput", nickInput);

            // Random Button
            Button randBtn = CreateButton(card.transform, "Btn_RandomNick", new Vector2(180f, 25f), new Vector2(110f, 56f), "LOSUJ", new Color(0.24f, 0.32f, 0.44f), Color.white, 17);
            SetPrivateField(controller, "profileRandomButton", randBtn);

            // Save and Cancel Buttons
            Button saveBtn = CreateButton(card.transform, "Btn_SaveNick", new Vector2(110f, -85f), new Vector2(210f, 58f), "ZAPISZ", new Color(0.04f, 0.76f, 0.55f), Color.white, 20, FontStyle.Bold);
            SetPrivateField(controller, "profileSaveButton", saveBtn);

            Button cancelBtn = CreateButton(card.transform, "Btn_CancelNick", new Vector2(-110f, -85f), new Vector2(190f, 58f), "ANULUJ", new Color(0.35f, 0.38f, 0.45f), Color.white, 19);
            SetPrivateField(controller, "profileCancelButton", cancelBtn);

            return overlay;
        }

        private static void BuildToastNotification(Transform parent, MainMenuController controller)
        {
            GameObject toastRoot = CreateUIRect("ToastNotification", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 65f), new Vector2(640f, 60f));
            toastRoot.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            Image toastBg = toastRoot.AddComponent<Image>();
            toastBg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

            Text toastText = CreateText(toastRoot.transform, "ToastMessage", Vector2.zero, new Vector2(600f, 50f), "Komunikat", 20, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), FontStyle.Bold);

            SetPrivateField(controller, "toastRoot", toastRoot);
            SetPrivateField(controller, "toastText", toastText);

            toastRoot.SetActive(false);
        }

        #region UI Creation Helpers

        private static GameObject CreateUIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
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

        private static GameObject CreateModalBackdrop(string name, Transform parent)
        {
            GameObject backdrop = CreateUIRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform rt = backdrop.GetComponent<RectTransform>();
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = backdrop.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.05f, 0.85f); // Dark translucent backdrop
            return backdrop;
        }

        private static GameObject CreateModalCard(Transform parent, string name, Vector2 size)
        {
            GameObject card = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            card.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            Image cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.1f, 0.13f, 0.18f, 0.98f); // High-tech card background
            return card;
        }

        private static Button CreateMenuActionButton(Transform parent, string name, Vector2 pos, Vector2 size, string mainText, string subText, Color accentColor, int mainFontSize)
        {
            GameObject btnObj = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            btnObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            Image img = btnObj.AddComponent<Image>();
            img.color = accentColor;

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = accentColor;
            colors.highlightedColor = accentColor * 1.15f;
            colors.pressedColor = accentColor * 0.85f;
            btn.colors = colors;

            // Main Label
            CreateText(btnObj.transform, "MainLabel", new Vector2(25f, 12f), new Vector2(size.x - 50f, 32f), mainText, mainFontSize, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);

            // Subtitle
            CreateText(btnObj.transform, "SubLabel", new Vector2(25f, -18f), new Vector2(size.x - 50f, 24f), subText, 14, TextAnchor.MiddleLeft, new Color(0.9f, 0.95f, 1f, 0.85f));

            return btn;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, Color bgColor, Color textColor, int fontSize = 22, FontStyle style = FontStyle.Normal)
        {
            GameObject btnObj = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            btnObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            CreateText(btnObj.transform, "Label", Vector2.zero, size, label, fontSize, TextAnchor.MiddleCenter, textColor, style);
            return btn;
        }

        private static Text CreateText(Transform parent, string name, Vector2 pos, Vector2 size, string content, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            GameObject textObj = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            textObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            Text txt = textObj.AddComponent<Text>();
            txt.font = GetFont();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.color = color;
            txt.text = content;
            return txt;
        }

        private static InputField CreateInputField(Transform parent, string name, Vector2 pos, Vector2 size, string placeholderText, string defaultText)
        {
            GameObject inputObj = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            inputObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            Image bg = inputObj.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

            InputField inputField = inputObj.AddComponent<InputField>();

            // Placeholder Text
            GameObject phObj = CreateUIRect("Placeholder", inputObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform phRt = phObj.GetComponent<RectTransform>();
            phRt.offsetMin = new Vector2(16, 4);
            phRt.offsetMax = new Vector2(-16, -4);
            Text ph = phObj.AddComponent<Text>();
            ph.font = GetFont();
            ph.fontSize = 20;
            ph.text = placeholderText;
            ph.color = new Color(0.5f, 0.55f, 0.65f, 0.6f);
            ph.alignment = TextAnchor.MiddleLeft;

            // Content Text
            GameObject textObj = CreateUIRect("Text", inputObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform tRt = textObj.GetComponent<RectTransform>();
            tRt.offsetMin = new Vector2(16, 4);
            tRt.offsetMax = new Vector2(-16, -4);
            Text t = textObj.AddComponent<Text>();
            t.font = GetFont();
            t.fontSize = 20;
            t.text = defaultText;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;

            inputField.placeholder = ph;
            inputField.textComponent = t;
            inputField.text = defaultText;

            return inputField;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, float defVal)
        {
            GameObject sliderObj = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            sliderObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defVal;

            // Background Bar
            GameObject bgObj = CreateUIRect("Background", sliderObj.transform, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.18f, 0.22f, 0.3f, 1f);

            // Fill Area
            GameObject fillArea = CreateUIRect("Fill Area", sliderObj.transform, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.offsetMin = new Vector2(5, 0);
            faRt.offsetMax = new Vector2(-5, 0);

            GameObject fillObj = CreateUIRect("Fill", fillArea.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform fRt = fillObj.GetComponent<RectTransform>();
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0f, 0.85f, 1f, 1f);
            slider.fillRect = fRt;

            // Handle Area
            GameObject handleArea = CreateUIRect("Handle Slide Area", sliderObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform haRt = handleArea.GetComponent<RectTransform>();
            haRt.offsetMin = new Vector2(10, 0);
            haRt.offsetMax = new Vector2(-10, 0);

            GameObject handleObj = CreateUIRect("Handle", handleArea.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24f, 32f));
            handleObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = Color.white;
            slider.handleRect = handleObj.GetComponent<RectTransform>();
            slider.targetGraphic = handleImg;

            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string name, Vector2 pos, Vector2 size, string labelText, bool defaultVal)
        {
            GameObject toggleObj = CreateUIRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            toggleObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = defaultVal;

            // Checkbox Box
            GameObject bgObj = CreateUIRect("Background", toggleObj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(25f, 0f), new Vector2(36f, 36f));
            bgObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.18f, 0.25f, 1f);

            // Checkmark
            GameObject checkObj = CreateUIRect("Checkmark", bgObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            checkObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            Image checkImg = checkObj.AddComponent<Image>();
            checkImg.color = new Color(0f, 0.88f, 1f, 1f);
            toggle.graphic = checkImg;
            toggle.targetGraphic = bgImg;

            // Label
            CreateText(toggleObj.transform, "Label", new Vector2(65f, 0f), new Vector2(size.x - 70f, size.y), labelText, 19, TextAnchor.MiddleLeft, Color.white);

            return toggle;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        #endregion
    }
}
