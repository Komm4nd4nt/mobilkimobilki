using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using RacingMobile.Vehicle;
using RacingMobile.MobileUI;
using RacingMobile.Multiplayer;
using RacingMobile.Camera;

namespace RacingMobile.Editor
{
    /// <summary>
    /// Automated builder for racing tracks, car prefabs, mobile touch canvas,
    /// and Netcode multiplayer architecture in Unity.
    /// </summary>
    public static class RacingSceneBuilder
    {
        [MenuItem("Racing Mobile/Build Playable Racing Scene", false, 1)]
        public static void BuildScene()
        {
            // 1. Build Environment / Race Track
            GameObject trackRoot = BuildTrack();

            // 2. Build Mobile Touch Canvas & HUD
            GameObject canvasObj = BuildMobileCanvas();

            // 3. Build Player Racing Car Template
            GameObject carObj = BuildCar(new Vector3(-3.5f, 0.6f, 0f), "Player_RacingCar", true);

            // 4. Build Camera
            BuildCamera(carObj.transform);

            // 5. Build Netcode NetworkManager & Save Car Prefab
            BuildNetworkManager(carObj);

            // Remove temporary in-scene template car so Netcode spawns dedicated cars per connected player
            Object.DestroyImmediate(carObj);

            // Save Scene
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=green>[RacingSceneBuilder]</color> Playable Mobile Racing Scene with Netcode successfully created!");
        }

        private static GameObject BuildTrack()
        {
            GameObject track = new GameObject("Environment_Track");

            // Main asphalt track arena (600m x 600m)
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Track_AsphaltGround";
            ground.transform.SetParent(track.transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(60f, 1f, 60f); // 600m x 600m

            // Material for Asphalt
            Material asphaltMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            asphaltMat.color = new Color(0.18f, 0.19f, 0.22f); // Dark asphalt
            ground.GetComponent<MeshRenderer>().sharedMaterial = asphaltMat;

            // Add track curbs / ramps to test suspension and jumping
            CreateRamp(track.transform, new Vector3(0f, 0f, 40f), new Vector3(12f, 1f, 15f), 12f);
            CreateRamp(track.transform, new Vector3(0f, 0f, 90f), new Vector3(12f, 1.8f, 20f), -12f);

            // Track borders / obstacles
            CreateBorder(track.transform, new Vector3(-25f, 1f, 50f), new Vector3(1f, 2f, 120f));
            CreateBorder(track.transform, new Vector3(25f, 1f, 50f), new Vector3(1f, 2f, 120f));

            // Starting grid marks
            for (int i = 0; i < 4; i++)
            {
                GameObject gridMark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gridMark.name = $"GridSlot_{i + 1}";
                gridMark.transform.SetParent(track.transform);
                float xOffset = (i % 2 == 0) ? -3.5f : 3.5f;
                float zOffset = -(i * 10f);
                gridMark.transform.position = new Vector3(xOffset, 0.02f, zOffset);
                gridMark.transform.localScale = new Vector3(2.5f, 0.02f, 5f);

                Material gridMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                gridMat.color = new Color(0.9f, 0.75f, 0.1f); // Yellow starting grid
                gridMark.GetComponent<MeshRenderer>().sharedMaterial = gridMat;
                Object.DestroyImmediate(gridMark.GetComponent<Collider>());
            }

            return track;
        }

        private static void CreateRamp(Transform parent, Vector3 pos, Vector3 scale, float angle)
        {
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Test_Ramp";
            ramp.transform.SetParent(parent);
            ramp.transform.position = pos;
            ramp.transform.localScale = scale;
            ramp.transform.rotation = Quaternion.Euler(angle, 0f, 0f);

            Material rampMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            rampMat.color = new Color(0.85f, 0.35f, 0.15f); // Orange ramp
            ramp.GetComponent<MeshRenderer>().sharedMaterial = rampMat;
        }

        private static void CreateBorder(Transform parent, Vector3 pos, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Track_Barrier";
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;

            Material wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            wallMat.color = new Color(0.8f, 0.15f, 0.15f); // Red racing barrier
            wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        }

        private static GameObject BuildCar(Vector3 spawnPos, string carName, bool isLocal)
        {
            GameObject car = new GameObject(carName);
            car.transform.position = spawnPos;

            // Rigidbody
            Rigidbody rb = car.AddComponent<Rigidbody>();
            rb.mass = 1350f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.8f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Chassis Collider
            BoxCollider chassisCol = car.AddComponent<BoxCollider>();
            chassisCol.size = new Vector3(1.9f, 0.75f, 4.3f);
            chassisCol.center = new Vector3(0f, 0.65f, 0f);

            // Car Visual Body
            GameObject bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyVisual.name = "Chassis_Visual";
            bodyVisual.transform.SetParent(car.transform, false);
            bodyVisual.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            bodyVisual.transform.localScale = new Vector3(1.85f, 0.65f, 4.2f);
            Object.DestroyImmediate(bodyVisual.GetComponent<Collider>());

            Material bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            bodyMat.color = new Color(0.08f, 0.45f, 0.95f); // Metallic blue
            bodyVisual.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Car Cabin / Roof Visual
            GameObject cabinVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabinVisual.name = "Cabin_Visual";
            cabinVisual.transform.SetParent(car.transform, false);
            cabinVisual.transform.localPosition = new Vector3(0f, 1.05f, -0.2f);
            cabinVisual.transform.localScale = new Vector3(1.4f, 0.55f, 2.1f);
            Object.DestroyImmediate(cabinVisual.GetComponent<Collider>());

            Material glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            glassMat.color = new Color(0.1f, 0.12f, 0.15f); // Tinted glass
            cabinVisual.GetComponent<MeshRenderer>().sharedMaterial = glassMat;

            // Rear Spoiler
            GameObject spoiler = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spoiler.name = "Spoiler_Visual";
            spoiler.transform.SetParent(car.transform, false);
            spoiler.transform.localPosition = new Vector3(0f, 1.05f, -1.95f);
            spoiler.transform.localScale = new Vector3(1.7f, 0.08f, 0.45f);
            Object.DestroyImmediate(spoiler.GetComponent<Collider>());
            spoiler.GetComponent<MeshRenderer>().sharedMaterial = glassMat;

            // Build Axles & Wheels
            GameObject wheelsRoot = new GameObject("Wheels");
            wheelsRoot.transform.SetParent(car.transform, false);

            WheelCollider flCol = CreateWheelCollider(wheelsRoot.transform, "Wheel_FL", new Vector3(-0.95f, 0.38f, 1.35f));
            WheelCollider frCol = CreateWheelCollider(wheelsRoot.transform, "Wheel_FR", new Vector3(0.95f, 0.38f, 1.35f));
            WheelCollider rlCol = CreateWheelCollider(wheelsRoot.transform, "Wheel_RL", new Vector3(-0.95f, 0.38f, -1.35f));
            WheelCollider rrCol = CreateWheelCollider(wheelsRoot.transform, "Wheel_RR", new Vector3(0.95f, 0.38f, -1.35f));

            Transform flVis = CreateWheelVisual(car.transform, "WheelMesh_FL", new Vector3(-0.95f, 0.38f, 1.35f));
            Transform frVis = CreateWheelVisual(car.transform, "WheelMesh_FR", new Vector3(0.95f, 0.38f, 1.35f));
            Transform rlVis = CreateWheelVisual(car.transform, "WheelMesh_RL", new Vector3(-0.95f, 0.38f, -1.35f));
            Transform rrVis = CreateWheelVisual(car.transform, "WheelMesh_RR", new Vector3(0.95f, 0.38f, -1.35f));

            // Physics Controller
            CarPhysicsController carPhys = car.AddComponent<CarPhysicsController>();
            carPhys.IsLocallyControlled = isLocal;

            // Configure Axles via Reflection/Fields
            ConfigureAxle(carPhys.FrontAxle, flCol, frCol, flVis, frVis, isMotor: true, isSteer: true, isHandbrake: false);
            ConfigureAxle(carPhys.RearAxle, rlCol, rrCol, rlVis, rrVis, isMotor: true, isSteer: false, isHandbrake: true);

            // Audio Visuals
            car.AddComponent<CarAudioVisuals>();

            // Netcode Components
            car.AddComponent<NetworkObject>();
            car.AddComponent<NetworkCarController>();
            car.AddComponent<CarNetworkSync>();

            return car;
        }

        private static WheelCollider CreateWheelCollider(Transform parent, string name, Vector3 localPos)
        {
            GameObject wheelObj = new GameObject(name);
            wheelObj.transform.SetParent(parent, false);
            wheelObj.transform.localPosition = localPos;

            WheelCollider col = wheelObj.AddComponent<WheelCollider>();
            col.radius = 0.38f;
            col.suspensionDistance = 0.22f;
            col.mass = 35f;
            col.forceAppPointDistance = 0.15f;

            JointSpring spring = col.suspensionSpring;
            spring.spring = 32000f;
            spring.damper = 4500f;
            spring.targetPosition = 0.5f;
            col.suspensionSpring = spring;

            // Forward Friction
            WheelFrictionCurve forward = col.forwardFriction;
            forward.extremumSlip = 0.2f;
            forward.extremumValue = 1.0f;
            forward.asymptoteSlip = 0.5f;
            forward.asymptoteValue = 0.8f;
            forward.stiffness = 1.0f;
            col.forwardFriction = forward;

            // Sideways Friction
            WheelFrictionCurve sideways = col.sidewaysFriction;
            sideways.extremumSlip = 0.22f;
            sideways.extremumValue = 1.0f;
            sideways.asymptoteSlip = 0.55f;
            sideways.asymptoteValue = 0.75f;
            sideways.stiffness = 1.0f;
            col.sidewaysFriction = sideways;

            return col;
        }

        private static Transform CreateWheelVisual(Transform parent, string name, Vector3 localPos)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPos;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            wheel.transform.localScale = new Vector3(0.76f, 0.18f, 0.76f);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());

            Material tireMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            tireMat.color = new Color(0.12f, 0.12f, 0.12f); // Black tire
            wheel.GetComponent<MeshRenderer>().sharedMaterial = tireMat;

            return wheel.transform;
        }

        private static void ConfigureAxle(WheelAxle axle, WheelCollider leftCol, WheelCollider rightCol, Transform leftVis, Transform rightVis, bool isMotor, bool isSteer, bool isHandbrake)
        {
            axle.LeftWheelCollider = leftCol;
            axle.RightWheelCollider = rightCol;
            axle.LeftWheelVisual = leftVis;
            axle.RightWheelVisual = rightVis;
            axle.IsMotor = isMotor;
            axle.IsSteering = isSteer;
            axle.IsHandbrake = isHandbrake;
            axle.VisualRotationOffset = new Vector3(0f, 0f, -90f);
            axle.AntiRollForce = 3500f;
        }

        private static GameObject BuildMobileCanvas()
        {
            // EventSystem (InputSystemUIInputModule for New Input System)
            EventSystem existingEs = Object.FindFirstObjectByType<EventSystem>();
            if (existingEs == null)
            {
                GameObject es = new GameObject("EventSystem");
                existingEs = es.AddComponent<EventSystem>();
            }

            StandaloneInputModule legacyModule = existingEs.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }

            if (existingEs.GetComponent<InputSystemUIInputModule>() == null)
            {
                existingEs.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            // Canvas
            GameObject canvasObj = new GameObject("MobileRacingCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Input Manager
            MobileInputManager inputMgr = canvasObj.AddComponent<MobileInputManager>();
            SpeedometerUI speedoUI = canvasObj.AddComponent<SpeedometerUI>();

            // 1. Steering Container (Left side)
            GameObject steerContainer = CreateUIRect("SteeringContainer_Buttons", canvasObj.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(400f, 200f));
            TouchButton leftBtn = CreateTouchButton(steerContainer.transform, "Btn_SteerLeft", new Vector2(0f, 0f), new Vector2(140f, 140f), "<", new Color(0.2f, 0.4f, 0.8f, 0.75f));
            TouchButton rightBtn = CreateTouchButton(steerContainer.transform, "Btn_SteerRight", new Vector2(160f, 0f), new Vector2(140f, 140f), ">", new Color(0.2f, 0.4f, 0.8f, 0.75f));

            // Virtual Joystick Container
            GameObject joyContainer = CreateUIRect("SteeringContainer_Joystick", canvasObj.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(250f, 250f));
            VirtualJoystick vJoy = CreateVirtualJoystick(joyContainer.transform);
            joyContainer.SetActive(false);

            // 2. Pedals Container (Right side)
            GameObject pedalsContainer = CreateUIRect("PedalsContainer", canvasObj.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(450f, 220f));
            TouchButton brakeBtn = CreateTouchButton(pedalsContainer.transform, "Btn_Brake", new Vector2(-280f, 0f), new Vector2(130f, 150f), "BRAKE\nREV", new Color(0.85f, 0.15f, 0.15f, 0.75f));
            TouchButton gasBtn = CreateTouchButton(pedalsContainer.transform, "Btn_Gas", new Vector2(-120f, 0f), new Vector2(130f, 180f), "GAS", new Color(0.15f, 0.8f, 0.25f, 0.75f));
            TouchButton handbrakeBtn = CreateTouchButton(pedalsContainer.transform, "Btn_Handbrake", new Vector2(-280f, 170f), new Vector2(130f, 90f), "DRIFT", new Color(0.95f, 0.55f, 0.1f, 0.75f));
            TouchButton boostBtn = CreateTouchButton(pedalsContainer.transform, "Btn_Boost", new Vector2(-120f, 200f), new Vector2(130f, 80f), "NITRO", new Color(0.1f, 0.75f, 0.95f, 0.75f));

            // 3. HUD Speedometer (Top Center)
            GameObject hudPanel = CreateUIRect("HUD_Speedometer", canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(360f, 130f));
            Text speedText = CreateUIText(hudPanel.transform, "SpeedText", new Vector2(0f, 10f), 64, "0", TextAnchor.MiddleCenter, Color.white);
            CreateUIText(hudPanel.transform, "KmHText", new Vector2(0f, -38f), 22, "KM/H", TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));
            Text gearText = CreateUIText(hudPanel.transform, "GearText", new Vector2(120f, 15f), 48, "1", TextAnchor.MiddleCenter, new Color(0.95f, 0.8f, 0.1f));
            CreateUIText(hudPanel.transform, "GearLabel", new Vector2(120f, -25f), 18, "GEAR", TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.6f));

            // Mode Button & Reset Button (Top Left)
            GameObject topTools = CreateUIRect("TopTools", canvasObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -50f), new Vector2(300f, 120f));
            Button modeBtn = CreateUIButton(topTools.transform, "Btn_Mode", new Vector2(0f, 0f), new Vector2(180f, 50f), "MODE: BUTTONS");
            Button resetBtn = CreateUIButton(topTools.transform, "Btn_Reset", new Vector2(200f, 0f), new Vector2(90f, 50f), "RESET");

            // Wire UI elements to scripts
            SetSerializedField(inputMgr, "steerLeftButton", leftBtn);
            SetSerializedField(inputMgr, "steerRightButton", rightBtn);
            SetSerializedField(inputMgr, "gasPedalButton", gasBtn);
            SetSerializedField(inputMgr, "brakePedalButton", brakeBtn);
            SetSerializedField(inputMgr, "handbrakeButton", handbrakeBtn);
            SetSerializedField(inputMgr, "boostButton", boostBtn);
            SetSerializedField(inputMgr, "virtualJoystick", vJoy);
            SetSerializedField(inputMgr, "buttonSteeringContainer", steerContainer);
            SetSerializedField(inputMgr, "joystickSteeringContainer", joyContainer);

            SetSerializedField(speedoUI, "speedText", speedText);
            SetSerializedField(speedoUI, "gearText", gearText);
            SetSerializedField(speedoUI, "modeText", modeBtn.GetComponentInChildren<Text>());

            modeBtn.onClick.AddListener(speedoUI.OnClickToggleControlMode);
            resetBtn.onClick.AddListener(speedoUI.OnClickResetCar);

            return canvasObj;
        }

        private static VirtualJoystick CreateVirtualJoystick(Transform parent)
        {
            GameObject bgObj = new GameObject("Joystick_Background", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            bgObj.transform.SetParent(parent, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(200f, 200f);
            bgRect.anchoredPosition = new Vector2(100f, 100f);

            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            GameObject handleObj = new GameObject("Joystick_Handle", typeof(RectTransform), typeof(Image));
            handleObj.transform.SetParent(bgObj.transform, false);
            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(80f, 80f);
            handleRect.anchoredPosition = Vector2.zero;

            Image handleImg = handleObj.GetComponent<Image>();
            handleImg.color = new Color(0.2f, 0.6f, 1f, 0.8f);

            return bgObj.GetComponent<VirtualJoystick>();
        }

        private static GameObject CreateUIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return obj;
        }

        private static TouchButton CreateTouchButton(Transform parent, string name, Vector2 pos, Vector2 size, string text, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TouchButton));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.color = color;

            CreateUIText(obj.transform, "Label", Vector2.zero, 26, text, TextAnchor.MiddleCenter, Color.white);
            return obj.GetComponent<TouchButton>();
        }

        private static Button CreateUIButton(Transform parent, string name, Vector2 pos, Vector2 size, string text)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);

            CreateUIText(obj.transform, "Label", Vector2.zero, 18, text, TextAnchor.MiddleCenter, Color.white);
            return obj.GetComponent<Button>();
        }

        private static Text CreateUIText(Transform parent, string name, Vector2 pos, int fontSize, string text, TextAnchor align, Color col)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = pos;

            Text txt = obj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.text = text;
            txt.alignment = align;
            txt.color = col;
            return txt;
        }

        private static void BuildCamera(Transform carTarget)
        {
            UnityEngine.Camera existingCam = Object.FindFirstObjectByType<UnityEngine.Camera>();
            GameObject camObj = existingCam != null ? existingCam.gameObject : new GameObject("RacingCamera", typeof(UnityEngine.Camera));

            SmoothFollowCamera follow = camObj.GetComponent<SmoothFollowCamera>() ?? camObj.AddComponent<SmoothFollowCamera>();
            follow.SetTarget(carTarget);
        }

        private static void BuildNetworkManager(GameObject carObj)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            string prefabPath = "Assets/Prefabs/RacingCar_Netcode.prefab";
            GameObject carPrefab = PrefabUtility.SaveAsPrefabAsset(carObj, prefabPath);

            GameObject netObj = Object.FindFirstObjectByType<NetworkManager>()?.gameObject;
            if (netObj == null)
            {
                netObj = new GameObject("[NetworkManager]");
                NetworkManager netMgr = netObj.AddComponent<NetworkManager>();
                UnityTransport transport = netObj.AddComponent<UnityTransport>();
                NetworkRacingManager racingNet = netObj.AddComponent<NetworkRacingManager>();

                if (carPrefab != null)
                {
                    netMgr.AddNetworkPrefab(carPrefab);
                    netMgr.NetworkConfig.PlayerPrefab = null; // Controlled dynamically by NetworkRacingManager

                    SerializedObject soNet = new SerializedObject(racingNet);
                    soNet.FindProperty("networkCarPrefab").objectReferenceValue = carPrefab;

                    // Collect Grid Spawn Points from track
                    List<Transform> gridPoints = new List<Transform>();
                    for (int i = 1; i <= 4; i++)
                    {
                        GameObject slot = GameObject.Find($"GridSlot_{i}");
                        if (slot != null) gridPoints.Add(slot.transform);
                    }

                    SerializedProperty spawnsProp = soNet.FindProperty("spawnGridPoints");
                    spawnsProp.ClearArray();
                    for (int i = 0; i < gridPoints.Count; i++)
                    {
                        spawnsProp.InsertArrayElementAtIndex(i);
                        spawnsProp.GetArrayElementAtIndex(i).objectReferenceValue = gridPoints[i];
                    }
                    soNet.ApplyModifiedProperties();
                }
            }
            else
            {
                NetworkManager netMgr = netObj.GetComponent<NetworkManager>();
                NetworkRacingManager racingNet = netObj.GetComponent<NetworkRacingManager>();

                if (carPrefab != null)
                {
                    if (netMgr != null) netMgr.AddNetworkPrefab(carPrefab);
                    if (racingNet != null)
                    {
                        SerializedObject soNet = new SerializedObject(racingNet);
                        soNet.FindProperty("networkCarPrefab").objectReferenceValue = carPrefab;
                        soNet.ApplyModifiedProperties();
                    }
                }
            }
        }

        private static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
