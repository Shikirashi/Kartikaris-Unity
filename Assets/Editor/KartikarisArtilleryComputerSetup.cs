using System;
using System.Linq;
using Kartikaris.Cockpit;
using Kartikaris.Combat;
using Kartikaris.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kartikaris.Editor
{
    public static class KartikarisArtilleryComputerSetup
    {
        private const string ScenePath = "Assets/Scenes/Kartikaris_MapTest.unity";
        private const string MaterialFolder = "Assets/Materials";
        private const string OutlineMaterialPath =
            MaterialFolder + "/KartikarisCockpitOutline.mat";
        private const int CockpitLayer = 7;
        private const int InteractableLayer = 8;
        private const uint CockpitRenderingLayer = 1u << 8;

        [MenuItem("Kartikaris/Cockpit/Configure Artillery Computer")]
        public static void ConfigureScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = RequireRoot(scene, "Player");
            GameObject enemy = RequireRoot(scene, "Enemy");
            GameObject cockpit = RequireRoot(scene, "cockpit");

            KartikarisShellLoadout playerLoadout =
                RequireComponent<KartikarisShellLoadout>(player);
            KartikarisShellLoadout enemyLoadout =
                RequireComponent<KartikarisShellLoadout>(enemy);
            KartikarisCannonWeapon weapon = RequireComponent<KartikarisCannonWeapon>(player);
            KartikarisFireControlPredictor predictor =
                RequireComponent<KartikarisFireControlPredictor>(player);
            MechFirstPersonController controller =
                RequireComponent<MechFirstPersonController>(player);

            ConfigurePlayerLoadout(playerLoadout);
            EnsureEnemyRemainsFixed(enemyLoadout);

            Transform panelRoot = FindPanelRoot(cockpit.transform);
            Camera cockpitCamera = FindExact(cockpit.transform, "CockpitCamera")
                ?.GetComponent<Camera>();
            if (cockpitCamera == null)
                throw new InvalidOperationException("CockpitCamera was not found.");

            Material outlineMaterial = GetOrCreateOutlineMaterial();
            TextMeshPro monitor = GetOrCreateMonitor(panelRoot, cockpitCamera);
            KartikarisArtilleryComputer computer =
                GetOrAdd<KartikarisArtilleryComputer>(panelRoot.gameObject);
            ConfigureComputer(computer, playerLoadout, weapon, predictor, controller,
                monitor, panelRoot);

            ConfigureButton(panelRoot, "shell prev", "shell.prev",
                KartikarisArtilleryComputerButton.ButtonAction.PreviousShell,
                computer, outlineMaterial);
            ConfigureButton(panelRoot, "shell next", "shell.next",
                KartikarisArtilleryComputerButton.ButtonAction.NextShell,
                computer, outlineMaterial);
            ConfigureButton(panelRoot, "charge less", "charge.less",
                KartikarisArtilleryComputerButton.ButtonAction.ChargeLess,
                computer, outlineMaterial);
            ConfigureButton(panelRoot, "charge more", "charge.more",
                KartikarisArtilleryComputerButton.ButtonAction.ChargeMore,
                computer, outlineMaterial);

            KartikarisCockpitInteractionController interaction =
                GetOrAdd<KartikarisCockpitInteractionController>(cockpitCamera.gameObject);
            SerializedObject interactionSo = new SerializedObject(interaction);
            interactionSo.FindProperty("interactionCamera").objectReferenceValue = cockpitCamera;
            // This imported cockpit is staged at 100x renderer scale; its button
            // faces sit roughly 7.5 m from the origin camera. The reusable
            // component retains a 2 m code default, while this scene needs 10 m.
            interactionSo.FindProperty("interactionDistance").floatValue = 10f;
            interactionSo.FindProperty("interactionLayers").intValue = 1 << InteractableLayer;
            interactionSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("ARTILLERY_COMPUTER_SETUP_COMPLETE|scene=" + ScenePath +
                "|panel=" + GetPath(panelRoot) + "|monitor=" + GetPath(monitor.transform));
            ValidateScene();
        }

        [MenuItem("Kartikaris/Cockpit/Validate Artillery Computer")]
        public static void ValidateScene()
        {
            Scene scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int failures = 0;

            GameObject player = FindRoot(scene, "Player");
            GameObject enemy = FindRoot(scene, "Enemy");
            GameObject cockpit = FindRoot(scene, "cockpit");
            failures += Check(player != null, "Player root");
            failures += Check(enemy != null, "Enemy root");
            failures += Check(cockpit != null, "cockpit root");
            if (failures > 0)
                throw new InvalidOperationException("Core scene roots are missing.");

            KartikarisShellLoadout playerLoadout = player.GetComponent<KartikarisShellLoadout>();
            KartikarisShellLoadout enemyLoadout = enemy.GetComponent<KartikarisShellLoadout>();
            SerializedObject playerLoadoutSo = new SerializedObject(playerLoadout);
            SerializedObject enemyLoadoutSo = new SerializedObject(enemyLoadout);
            failures += Check(playerLoadoutSo.FindProperty("runtimeSelectionEnabled").boolValue,
                "Player runtime selection enabled");
            failures += Check(playerLoadoutSo.FindProperty("runtimeSlots").arraySize == 6,
                "six Player shell slots");
            failures += Check(playerLoadoutSo.FindProperty("chargeCount").intValue == 1,
                "initial charge count 1");
            failures += Check(!enemyLoadoutSo.FindProperty("runtimeSelectionEnabled").boolValue,
                "Enemy fixed loadout preserved");

            Transform panel = FindPanelRoot(cockpit.transform);
            KartikarisArtilleryComputer computer =
                panel.GetComponent<KartikarisArtilleryComputer>();
            failures += Check(computer != null, "Artillery Computer component");
            TextMeshPro monitor = FindExact(panel, "ArtilleryComputerMonitor")
                ?.GetComponent<TextMeshPro>();
            failures += Check(monitor != null, "world-space TMP monitor");

            string[] buttonNames = { "shell prev", "shell next", "charge less", "charge more" };
            foreach (string buttonName in buttonNames)
            {
                Transform button = FindExact(panel, buttonName);
                bool valid = button != null && button.gameObject.layer == InteractableLayer &&
                    button.GetComponent<BoxCollider>() != null &&
                    button.GetComponent<KartikarisCockpitInteractable>() != null &&
                    button.GetComponent<KartikarisArtilleryComputerButton>() != null &&
                    button.GetComponent<KartikarisCockpitOutline>() != null;
                failures += Check(valid, "button wiring: " + buttonName);
            }

            Camera camera = FindExact(cockpit.transform, "CockpitCamera")?.GetComponent<Camera>();
            KartikarisCockpitInteractionController interaction = camera != null
                ? camera.GetComponent<KartikarisCockpitInteractionController>()
                : null;
            failures += Check(interaction != null, "CockpitCamera gaze interaction");
            failures += Check(interaction != null && interaction.InteractionDistance >= 7.5f,
                "scene interaction reach covers staged panel");
            failures += Check(interaction != null && interaction.InteractionLayers.value ==
                (1 << InteractableLayer), "interaction mask is CockpitInteractable only");
            Physics.SyncTransforms();
            foreach (string buttonName in buttonNames)
            {
                Transform button = FindExact(panel, buttonName);
                Collider targetCollider = button != null ? button.GetComponent<Collider>() : null;
                Vector3 delta = targetCollider != null
                    ? targetCollider.bounds.center - camera.transform.position
                    : Vector3.zero;
                bool gazeHit = targetCollider != null && delta.sqrMagnitude > 0f &&
                    Physics.Raycast(camera.transform.position, delta.normalized,
                        out RaycastHit hit, interaction.InteractionDistance,
                        interaction.InteractionLayers, QueryTriggerInteraction.Collide) &&
                    hit.collider == targetCollider;
                failures += Check(gazeHit, "camera ray can focus: " + buttonName);
            }

            failures += ValidateRuntimeStateModel();

            int missingScripts = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject);
            failures += Check(missingScripts == 0, "no missing MonoBehaviours");

            Debug.Log("ARTILLERY_COMPUTER_VALIDATION|failures=" + failures +
                "|roots=" + scene.rootCount + "|missingScripts=" + missingScripts +
                "|monitorFont=" + (monitor != null && monitor.font != null ? monitor.font.name : "none"));
            if (failures > 0)
                throw new InvalidOperationException(
                    "Artillery Computer validation failed with " + failures + " issue(s).");
        }

        private static void ConfigurePlayerLoadout(KartikarisShellLoadout loadout)
        {
            SerializedObject so = new SerializedObject(loadout);
            so.FindProperty("runtimeSelectionEnabled").boolValue = true;
            so.FindProperty("selectedRuntimeSlot").intValue = 0;
            so.FindProperty("chargeCount").intValue = 1;
            SerializedProperty fixedShell = so.FindProperty("selectedShell");
            SerializedProperty slots = so.FindProperty("runtimeSlots");
            slots.arraySize = KartikarisShellLoadout.RuntimeSlotCount;
            string[] codes = { "SPEX", "ABST", "HOMG", "GUID", "----", "----" };
            for (int index = 0; index < slots.arraySize; index++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(index);
                slot.FindPropertyRelative("definition").objectReferenceValue =
                    index == 0 ? fixedShell.objectReferenceValue : null;
                slot.FindPropertyRelative("displayCode").stringValue = codes[index];
                slot.FindPropertyRelative("physicalSlotIndex").intValue = index + 1;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(loadout);
        }

        private static void EnsureEnemyRemainsFixed(KartikarisShellLoadout loadout)
        {
            SerializedObject so = new SerializedObject(loadout);
            so.FindProperty("runtimeSelectionEnabled").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(loadout);
        }

        private static void ConfigureComputer(KartikarisArtilleryComputer computer,
            KartikarisShellLoadout loadout, KartikarisCannonWeapon weapon,
            KartikarisFireControlPredictor predictor, MechFirstPersonController controller,
            TMP_Text monitor, Transform panelRoot)
        {
            SerializedObject so = new SerializedObject(computer);
            so.FindProperty("shellLoadout").objectReferenceValue = loadout;
            so.FindProperty("weapon").objectReferenceValue = weapon;
            so.FindProperty("predictor").objectReferenceValue = predictor;
            so.FindProperty("playerController").objectReferenceValue = controller;
            so.FindProperty("monitorText").objectReferenceValue = monitor;
            so.FindProperty("filledChargeGlyph").stringValue = "#";
            so.FindProperty("emptyChargeGlyph").stringValue = "   ";
            so.FindProperty("filledModeGlyph").stringValue = "#";
            so.FindProperty("emptyModeGlyph").stringValue = ".";

            SerializedProperty chargeRenderers = so.FindProperty("chargeRenderers");
            chargeRenderers.arraySize = 3;
            for (int index = 0; index < 3; index++)
            {
                Renderer renderer = FindExact(panelRoot, "charge." + (index + 1))
                    ?.GetComponent<Renderer>();
                if (renderer == null)
                    throw new InvalidOperationException("Visible charge renderer was not found: charge." +
                        (index + 1));
                chargeRenderers.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            }

            Transform armature = FindExact(panelRoot, "Armature");
            if (armature == null)
                throw new InvalidOperationException("Artillery Computer Armature was not found.");
            AssignTransform(so, "ramrodBone", FindExact(armature, "ramrod"));
            AssignTransform(so, "chargesBone", FindExact(armature, "charges"));
            AssignTransform(so, "coverpanelBone", FindExact(armature, "coverpanel"));
            AssignTransform(so, "revolverBone", FindExact(armature, "revolver"));
            SerializedProperty shellBones = so.FindProperty("shellBones");
            shellBones.arraySize = 6;
            for (int index = 0; index < 6; index++)
                shellBones.GetArrayElementAtIndex(index).objectReferenceValue =
                    FindExact(armature, "shell." + (index + 1));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(computer);
        }

        private static void ConfigureButton(Transform panelRoot, string visibleName,
            string boneName, KartikarisArtilleryComputerButton.ButtonAction action,
            KartikarisArtilleryComputer computer, Material outlineMaterial)
        {
            Transform visible = FindExact(panelRoot, visibleName);
            Transform armature = FindExact(panelRoot, "Armature");
            Transform bone = FindExact(armature, boneName);
            if (visible == null || bone == null)
                throw new InvalidOperationException("Button mesh/bone pair not found: " + visibleName +
                    " / " + boneName);

            visible.gameObject.layer = InteractableLayer;
            Renderer renderer = visible.GetComponent<Renderer>();
            if (renderer == null)
                throw new InvalidOperationException("Button renderer not found: " + visibleName);

            BoxCollider collider = GetOrAdd<BoxCollider>(visible.gameObject);
            collider.isTrigger = true;
            Bounds localBounds = renderer is SkinnedMeshRenderer skinned
                ? skinned.localBounds
                : CalculateAccurateLocalBounds(renderer);
            collider.center = localBounds.center;
            collider.size = Vector3.Max(localBounds.size * 1.08f,
                new Vector3(0.002f, 0.002f, 0.002f));

            KartikarisCockpitOutline outline = GetOrAdd<KartikarisCockpitOutline>(visible.gameObject);
            SerializedObject outlineSo = new SerializedObject(outline);
            outlineSo.FindProperty("targetRenderer").objectReferenceValue = renderer;
            outlineSo.FindProperty("outlineMaterial").objectReferenceValue = outlineMaterial;
            outlineSo.FindProperty("outlineColor").colorValue = Color.white;
            outlineSo.FindProperty("outlineThickness").floatValue = 0.0003f;
            outlineSo.ApplyModifiedPropertiesWithoutUndo();

            KartikarisCockpitInteractable interactable =
                GetOrAdd<KartikarisCockpitInteractable>(visible.gameObject);
            SerializedObject interactableSo = new SerializedObject(interactable);
            interactableSo.FindProperty("interactionEnabled").boolValue = true;
            interactableSo.FindProperty("outlineTarget").objectReferenceValue = outline;
            interactableSo.ApplyModifiedPropertiesWithoutUndo();

            KartikarisArtilleryComputerButton button =
                GetOrAdd<KartikarisArtilleryComputerButton>(visible.gameObject);
            SerializedObject buttonSo = new SerializedObject(button);
            buttonSo.FindProperty("action").enumValueIndex = (int)action;
            buttonSo.FindProperty("artilleryComputer").objectReferenceValue = computer;
            buttonSo.FindProperty("buttonBone").objectReferenceValue = bone;
            buttonSo.FindProperty("animator").objectReferenceValue = null;
            buttonSo.FindProperty("successfulPressTrigger").stringValue = string.Empty;
            buttonSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(visible.gameObject);
            Debug.Log("ARTILLERY_BUTTON_WIRED|mesh=" + GetPath(visible) +
                "|bone=" + GetPath(bone) + "|bounds=" + localBounds);
        }

        private static TextMeshPro GetOrCreateMonitor(Transform panelRoot, Camera camera)
        {
            Transform existing = FindExact(panelRoot, "ArtilleryComputerMonitor");
            TextMeshPro text = existing != null ? existing.GetComponent<TextMeshPro>() : null;
            if (text == null)
            {
                GameObject monitorObject = new GameObject("ArtilleryComputerMonitor",
                    typeof(RectTransform));
                monitorObject.transform.SetParent(panelRoot, false);
                text = monitorObject.AddComponent<TextMeshPro>();
            }

            text.gameObject.layer = CockpitLayer;
            text.richText = false;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 0.4f;
            text.color = new Color(0.68f, 1f, 0.63f, 1f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.overflowMode = TextOverflowModes.Overflow;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = "SHEL: [SPEX]\nCHRG: [#] [   ] [   ]\nDIST: ----m\nFLGT: --:---\n. . . . . . . . . .";
            RectTransform rect = text.rectTransform;
            rect.sizeDelta = new Vector2(4.65f, 4.25f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Transform screen = panelRoot.Cast<Transform>()
                .FirstOrDefault(item => item.name == "right panel" &&
                    item.GetComponent<Renderer>() != null);
            Renderer screenRenderer = screen != null ? screen.GetComponent<Renderer>() : null;
            if (screenRenderer is SkinnedMeshRenderer skinnedScreen &&
                TryGetMaterialSubmeshIndex(skinnedScreen, "monitor", out int monitorSubmesh) &&
                TryCalculateSkinnedSubmeshWorldBounds(skinnedScreen, monitorSubmesh,
                    out Bounds monitorBounds))
            {
                Vector3 center = monitorBounds.center;
                Vector3 towardCamera = (camera.transform.position - center).normalized;
                rect.position = center + towardCamera * 0.006f;
                rect.rotation = Quaternion.LookRotation(-towardCamera, Vector3.up);
                float horizontalSize = Mathf.Max(monitorBounds.size.x, monitorBounds.size.z);
                float verticalSize = monitorBounds.size.y;
                rect.sizeDelta = new Vector2(horizontalSize * 0.88f, verticalSize * 0.8f);
                text.fontSize = verticalSize * 0.075f;
                Debug.Log("ARTILLERY_MONITOR_PLACED|screen=" + GetPath(screen) +
                    "|material=monitor|center=" + FormatVector(monitorBounds.center) +
                    "|size=" + FormatVector(monitorBounds.size) +
                    "|position=" + FormatVector(rect.position));
            }
            else
            {
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                Debug.LogWarning("ARTILLERY_MONITOR_MANUAL_TUNING_REQUIRED|screen renderer not found");
            }

            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.renderingLayerMask = CockpitRenderingLayer;
            EditorUtility.SetDirty(text.gameObject);
            return text;
        }

        private static Material GetOrCreateOutlineMaterial()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets"))
                    throw new InvalidOperationException("Assets root is unavailable.");
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            Shader shader = Shader.Find("Kartikaris/Cockpit/Object Local Outline");
            if (shader == null)
                throw new InvalidOperationException("Kartikaris cockpit outline shader was not imported.");
            if (material == null)
            {
                material = new Material(shader) { name = "KartikarisCockpitOutline" };
                AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetColor("_OutlineColor", Color.white);
            material.SetFloat("_OutlineThickness", 0.0003f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform FindPanelRoot(Transform cockpit)
        {
            foreach (Transform candidate in cockpit.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "right panel" &&
                    FindExact(candidate, "Armature") != null &&
                    FindExact(candidate, "shell prev") != null &&
                    FindExact(candidate, "charge more") != null)
                    return candidate;
            }
            throw new InvalidOperationException("Right Artillery Computer panel root was not found.");
        }

        private static Transform FindExact(Transform root, string objectName)
        {
            if (root == null)
                return null;
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == objectName);
        }

        private static Bounds CalculateAccurateLocalBounds(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                Mesh baked = new Mesh();
                skinned.BakeMesh(baked);
                Bounds bakedBounds = baked.bounds;
                UnityEngine.Object.DestroyImmediate(baked);
                return bakedBounds;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                return filter.sharedMesh.bounds;

            Bounds world = renderer.bounds;
            Vector3 center = renderer.transform.InverseTransformPoint(world.center);
            Vector3 size = world.size;
            Vector3 scale = renderer.transform.lossyScale;
            size.x /= Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            size.y /= Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            size.z /= Mathf.Max(0.0001f, Mathf.Abs(scale.z));
            return new Bounds(center, size);
        }

        private static bool TryGetMaterialSubmeshIndex(SkinnedMeshRenderer renderer,
            string materialName, out int submeshIndex)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                if (materials[index] != null &&
                    string.Equals(materials[index].name, materialName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    submeshIndex = index;
                    return true;
                }
            }
            submeshIndex = -1;
            return false;
        }

        private static bool TryCalculateSkinnedSubmeshWorldBounds(
            SkinnedMeshRenderer renderer, int submeshIndex, out Bounds bounds)
        {
            bounds = default;
            if (renderer.sharedMesh == null || submeshIndex < 0 ||
                submeshIndex >= renderer.sharedMesh.subMeshCount)
                return false;

            Mesh baked = new Mesh();
            renderer.BakeMesh(baked);
            int[] indices = baked.GetTriangles(submeshIndex);
            Vector3[] vertices = baked.vertices;
            if (indices.Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(baked);
                return false;
            }

            // BakeMesh already includes the SkinnedMeshRenderer transform scale.
            // Applying localToWorld would scale it twice on this 100x FBX stage.
            Matrix4x4 pose = Matrix4x4.TRS(renderer.transform.position,
                renderer.transform.rotation, Vector3.one);
            Vector3 first = pose.MultiplyPoint3x4(vertices[indices[0]]);
            bounds = new Bounds(first, Vector3.zero);
            for (int index = 1; index < indices.Length; index++)
                bounds.Encapsulate(pose.MultiplyPoint3x4(vertices[indices[index]]));
            UnityEngine.Object.DestroyImmediate(baked);
            return true;
        }

        private static string FormatVector(Vector3 value)
        {
            return value.x.ToString("F4") + "," + value.y.ToString("F4") + "," +
                value.z.ToString("F4");
        }

        private static void AssignTransform(SerializedObject so, string field, Transform value)
        {
            if (value == null)
                throw new InvalidOperationException("Required bone not found: " + field);
            so.FindProperty(field).objectReferenceValue = value;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static T RequireComponent<T>(GameObject target) where T : Component
        {
            T component = target != null ? target.GetComponent<T>() : null;
            if (component == null)
                throw new InvalidOperationException(typeof(T).Name + " missing on " +
                    (target != null ? target.name : "null"));
            return component;
        }

        private static GameObject RequireRoot(Scene scene, string rootName)
        {
            GameObject root = FindRoot(scene, rootName);
            if (root == null)
                throw new InvalidOperationException("Scene root not found: " + rootName);
            return root;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName);
        }

        private static int Check(bool condition, string description)
        {
            if (condition)
            {
                Debug.Log("ARTILLERY_CHECK_OK|" + description);
                return 0;
            }
            Debug.LogError("ARTILLERY_CHECK_FAILED|" + description);
            return 1;
        }

        private static int ValidateRuntimeStateModel()
        {
            int failures = 0;
            GameObject testObject = new GameObject("ArtilleryComputerStateValidation")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            KartikarisShellLoadout loadout = testObject.AddComponent<KartikarisShellLoadout>();
            KartikarisShellDefinition first =
                ScriptableObject.CreateInstance<KartikarisShellDefinition>();
            KartikarisShellDefinition third =
                ScriptableObject.CreateInstance<KartikarisShellDefinition>();
            KartikarisShellDefinition sixth =
                ScriptableObject.CreateInstance<KartikarisShellDefinition>();
            try
            {
                SerializedObject so = new SerializedObject(loadout);
                so.FindProperty("runtimeSelectionEnabled").boolValue = true;
                so.FindProperty("selectedRuntimeSlot").intValue = 0;
                SerializedProperty slots = so.FindProperty("runtimeSlots");
                slots.arraySize = 6;
                for (int index = 0; index < 6; index++)
                {
                    SerializedProperty slot = slots.GetArrayElementAtIndex(index);
                    slot.FindPropertyRelative("definition").objectReferenceValue =
                        index == 0 ? first : index == 2 ? third : index == 5 ? sixth : null;
                    slot.FindPropertyRelative("displayCode").stringValue = "T" + index;
                    slot.FindPropertyRelative("physicalSlotIndex").intValue = index + 1;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                int eventCount = 0;
                KartikarisShellLoadout.ShellSelectionChange lastChange = default;
                loadout.ShellSelectionChanged += change =>
                {
                    eventCount++;
                    lastChange = change;
                };
                failures += Check(loadout.SelectNextAvailable() &&
                    loadout.SelectedPhysicalSlotIndex == 3, "selection skips null slots");
                failures += Check(lastChange.PreviousPhysicalSlotIndex == 1 &&
                    lastChange.NewPhysicalSlotIndex == 3 &&
                    lastChange.Direction == KartikarisShellLoadout.SelectionDirection.Next,
                    "selection event payload");
                failures += Check(loadout.SelectNextAvailable() &&
                    loadout.SelectedPhysicalSlotIndex == 6 && loadout.SelectNextAvailable() &&
                    loadout.SelectedPhysicalSlotIndex == 1, "next selection wraps");
                failures += Check(loadout.SelectPreviousAvailable() &&
                    loadout.SelectedPhysicalSlotIndex == 6 && eventCount == 4,
                    "previous selection wraps");

                int rejectionCount = 0;
                loadout.ZeroChargeFireRejected += () => rejectionCount++;
                failures += Check(loadout.SetChargeCount(0) &&
                    !loadout.TryAuthorizeFire() && rejectionCount == 1,
                    "zero charge rejects once per attempt");
                failures += Check(!loadout.TryAuthorizeFire() && rejectionCount == 2,
                    "repeated zero-charge attempts each signal once");
                failures += Check(loadout.SetChargeCount(3) && loadout.TryAuthorizeFire() &&
                    Mathf.Approximately(loadout.ApplyChargeMuzzleVelocity(80f), 80f),
                    "charges 1-3 preserve first-pass velocity");

                so.Update();
                so.FindProperty("runtimeSelectionEnabled").boolValue = false;
                so.FindProperty("chargeCount").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                failures += Check(!loadout.SelectNextAvailable() && loadout.TryAuthorizeFire(),
                    "fixed Enemy-style loadout ignores Player controls");
                failures += Check(KartikarisShellLoadout.FormatDisplayCode("ab") == "AB  " &&
                    KartikarisShellLoadout.FormatDisplayCode("guided") == "GUID",
                    "four-character display code normalization");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(third);
                UnityEngine.Object.DestroyImmediate(sixth);
            }
            return failures;
        }

        private static string GetPath(Transform item)
        {
            if (item == null)
                return "null";
            string path = item.name;
            for (Transform parent = item.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }
}
