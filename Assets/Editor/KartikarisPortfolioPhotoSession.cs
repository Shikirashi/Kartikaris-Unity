using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kartikaris.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Kartikaris.Editor
{
    /// <summary>
    /// Creates and captures an isolated, static copy of the production scene for
    /// portfolio photography. The source scene is never opened for writing.
    /// </summary>
    public static class KartikarisPortfolioPhotoSession
    {
        private const string SourceScenePath = "Assets/Scenes/Kartikaris_MapTest.unity";
        private const string PhotoScenePath =
            "Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity";
        private const string PhotoAssetFolder = "Assets/PortfolioPhotoSession";
        private const string PhotoTerrainDataPath =
            PhotoAssetFolder + "/Kartikaris_PhotoTerrainData.asset";
        private const string PhotoRootName = "PhotoSession_STATIC_NON_HOSTILE";
        private const string CaptureDirectory = "PortfolioCaptures/PhotoSession";
        private const int CaptureWidth = 1600;
        private const int CaptureHeight = 900;

        private static readonly Shot[] Shots =
        {
            new Shot("PhotoCamera_A_Hero", "kartikaris_hero_01.png", 38f,
                1.08f, 0.90f, 0.35f, 0.10f, 0.04f),
            new Shot("PhotoCamera_B_Environment", "kartikaris_alt_01.png", 47f,
                1.75f, -0.72f, 0.70f, 0.28f, 0.10f),
            new Shot("PhotoCamera_C_Cannon", "kartikaris_alt_02.png", 34f,
                1.22f, -0.92f, -0.48f, 0.14f, 0.10f)
        };

        [MenuItem("Kartikaris/Portfolio Photo Session/Create or Refresh Static Scene")]
        public static void CreateOrRefreshPhotoScene()
        {
            bool replace = !AssetDatabase.LoadAssetAtPath<SceneAsset>(PhotoScenePath) ||
                EditorUtility.DisplayDialog(
                    "Refresh portfolio photo scene?",
                    "This recreates only the isolated photo-session scene. The production " +
                    "Kartikaris_MapTest scene is not modified.",
                    "Recreate Photo Scene",
                    "Cancel");
            if (!replace)
                return;

            BuildPhotoScene(true);
        }

        [MenuItem("Kartikaris/Portfolio Photo Session/Capture 1600x900 Drafts")]
        public static void CaptureDrafts()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(PhotoScenePath);
            if (sceneAsset == null)
                throw new InvalidOperationException(
                    "Create the isolated portfolio photo scene before capturing drafts.");

            Scene scene = EditorSceneManager.OpenScene(PhotoScenePath, OpenSceneMode.Single);
            GameObject photoRoot = RequireRoot(scene, PhotoRootName);
            string absoluteDirectory = Path.GetFullPath(CaptureDirectory);
            Directory.CreateDirectory(absoluteDirectory);

            foreach (Shot shot in Shots)
            {
                Transform cameraTransform = FindExact(photoRoot.transform, shot.CameraName);
                Camera camera = cameraTransform != null
                    ? cameraTransform.GetComponent<Camera>()
                    : null;
                if (camera == null)
                    throw new InvalidOperationException(
                        "Photo camera is missing: " + shot.CameraName);

                string absolutePath = Path.Combine(absoluteDirectory, shot.FileName);
                Capture(camera, absolutePath, CaptureWidth, CaptureHeight);
                Debug.Log("PORTFOLIO_PHOTO_CAPTURED|shot=" + shot.CameraName +
                    "|path=" + absolutePath + "|size=" + CaptureWidth + "x" +
                    CaptureHeight + "|position=" + Format(camera.transform.position) +
                    "|rotation=" + Format(camera.transform.eulerAngles) +
                    "|fov=" + camera.fieldOfView.ToString("F1"));
            }

            Debug.Log("PORTFOLIO_PHOTO_CAPTURE_COMPLETE|scene=" + PhotoScenePath +
                "|directory=" + absoluteDirectory + "|count=" + Shots.Length);
        }

        [MenuItem("Kartikaris/Portfolio Photo Session/Capture Alt 2 Cannon Pose Variants")]
        public static void CaptureAlt2CannonPoseVariants()
        {
            Scene scene = EditorSceneManager.OpenScene(PhotoScenePath, OpenSceneMode.Single);
            GameObject player = RequireRoot(scene, "Player");
            GameObject mechVisual = FindExact(player.transform, "Kartikaris Mech")?.gameObject;
            GameObject photoRoot = RequireRoot(scene, PhotoRootName);
            Camera anchorCamera = FindExact(photoRoot.transform, "PhotoCamera_C_Cannon")
                ?.GetComponent<Camera>();
            KartikarisCannonWeapon weapon = player.GetComponent<KartikarisCannonWeapon>();
            if (mechVisual == null || anchorCamera == null || weapon == null)
                throw new InvalidOperationException(
                    "Alt-2 capture requires the staged mech, Cannon camera and Player weapon.");

            string absoluteDirectory = Path.GetFullPath(CaptureDirectory);
            Directory.CreateDirectory(absoluteDirectory);
            Vector3 anchorPosition = anchorCamera.transform.position;
            Quaternion anchorRotation = anchorCamera.transform.rotation;
            float anchorFov = anchorCamera.fieldOfView;
            Quaternion unfoldedHeading = weapon.HeadingPivot.localRotation;
            Quaternion unfoldedElevation = weapon.ElevationPivot.localRotation;

            try
            {
                CaptureVariant(anchorCamera, absoluteDirectory,
                    "kartikaris_alt2_unfolded_01.png", "unfolded", 0f, 0f,
                    anchorPosition, anchorRotation, anchorFov);
                Bounds unfoldedBounds = CalculateBounds(mechVisual);
                Vector3 unfoldedAltPosition = anchorPosition -
                    (anchorRotation * Vector3.right) * 2.2f + Vector3.up * 0.5f;
                Quaternion unfoldedAltRotation = Quaternion.LookRotation(
                    unfoldedBounds.center - unfoldedAltPosition, Vector3.up);
                CaptureVariant(anchorCamera, absoluteDirectory,
                    "kartikaris_alt2_unfolded_02.png", "unfolded-alt-camera", 0f, 0f,
                    unfoldedAltPosition, unfoldedAltRotation, 35f);

                weapon.HeadingPivot.localRotation = unfoldedHeading;
                weapon.ElevationPivot.localRotation = unfoldedElevation;
                ApplyStaticCannonOffset(weapon, 12f, 12f);
                CaptureVariant(anchorCamera, absoluteDirectory,
                    "kartikaris_alt2_unfolded_elev78_az12.png",
                    "unfolded-elevation-azimuth", 12f, 12f,
                    anchorPosition, anchorRotation, anchorFov);

                weapon.HeadingPivot.localRotation = unfoldedHeading;
                weapon.ElevationPivot.localRotation = unfoldedElevation;
                ApplyStaticCannonOffset(weapon, -12f, 6f);
                CaptureVariant(anchorCamera, absoluteDirectory,
                    "kartikaris_alt2_unfolded_elev84_az-12.png",
                    "unfolded-elevation-azimuth", -12f, 6f,
                    anchorPosition, anchorRotation, anchorFov);

            }
            finally
            {
                weapon.HeadingPivot.localRotation = unfoldedHeading;
                weapon.ElevationPivot.localRotation = unfoldedElevation;
                anchorCamera.transform.SetPositionAndRotation(anchorPosition, anchorRotation);
                anchorCamera.fieldOfView = anchorFov;
            }

            CaptureFoldedAlt2Variants(absoluteDirectory);

            Debug.Log("PORTFOLIO_ALT2_VARIANTS_COMPLETE|scene=" + PhotoScenePath +
                "|directory=" + absoluteDirectory + "|count=6|sceneSaved=false");
        }

        public static void CaptureAlt2CannonPoseVariantsAndExit()
        {
            try
            {
                CaptureAlt2CannonPoseVariants();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void CaptureVariant(Camera camera, string directory, string fileName,
            string pose, float headingOffset, float elevationOffset, Vector3 position,
            Quaternion rotation, float fieldOfView)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fieldOfView;
            string path = Path.Combine(directory, fileName);
            Capture(camera, path, CaptureWidth, CaptureHeight);
            Debug.Log("PORTFOLIO_ALT2_VARIANT_CAPTURED|file=" + fileName +
                "|pose=" + pose + "|position=" + Format(position) +
                "|rotation=" + Format(rotation.eulerAngles) +
                "|fov=" + fieldOfView.ToString("F1") +
                "|headingOffset=" + headingOffset.ToString("F1") +
                "|elevationOffset=" + elevationOffset.ToString("F1") +
                "|size=" + CaptureWidth + "x" + CaptureHeight);
        }

        private static void CaptureFoldedAlt2Variants(string absoluteDirectory)
        {
            Scene scene = EditorSceneManager.OpenScene(PhotoScenePath, OpenSceneMode.Single);
            GameObject player = RequireRoot(scene, "Player");
            GameObject mechVisual = FindExact(player.transform, "Kartikaris Mech")?.gameObject;
            GameObject photoRoot = RequireRoot(scene, PhotoRootName);
            Camera camera = FindExact(photoRoot.transform, "PhotoCamera_C_Cannon")
                ?.GetComponent<Camera>();
            if (mechVisual == null || camera == null)
                throw new InvalidOperationException("The folded Alt-2 setup is incomplete.");

            Vector3 anchorPosition = camera.transform.position;
            Quaternion anchorRotation = camera.transform.rotation;
            float anchorFov = camera.fieldOfView;
            PoseCannon(mechVisual, false);
            CaptureVariant(camera, absoluteDirectory,
                "kartikaris_alt2_folded_01.png", "folded", 0f, 0f,
                anchorPosition, anchorRotation, anchorFov);
            Bounds foldedBounds = CalculateBounds(mechVisual);
            Vector3 altPosition = anchorPosition +
                (anchorRotation * Vector3.right) * 2.4f + Vector3.up * 0.35f;
            Quaternion altRotation = Quaternion.LookRotation(
                foldedBounds.center - altPosition, Vector3.up);
            CaptureVariant(camera, absoluteDirectory,
                "kartikaris_alt2_folded_02.png", "folded-alt-camera", 0f, 0f,
                altPosition, altRotation, 35f);
        }

        private static void ApplyStaticCannonOffset(KartikarisCannonWeapon weapon,
            float headingDegrees, float elevationOffsetDegrees)
        {
            if (weapon.HeadingPivot == null || weapon.ElevationPivot == null)
                throw new InvalidOperationException("The staged weapon pivots are unavailable.");
            weapon.HeadingPivot.localRotation *= Quaternion.AngleAxis(
                headingDegrees, AxisVector(weapon.HeadingAxis));
            weapon.ElevationPivot.localRotation *= Quaternion.AngleAxis(
                elevationOffsetDegrees, AxisVector(weapon.ElevationAxis));
        }

        private static Vector3 AxisVector(KartikarisCannonWeapon.LocalRotationAxis axis)
        {
            switch (axis)
            {
                case KartikarisCannonWeapon.LocalRotationAxis.X:
                    return Vector3.right;
                case KartikarisCannonWeapon.LocalRotationAxis.Y:
                    return Vector3.up;
                default:
                    return Vector3.forward;
            }
        }

        // Entry point used by an exact-version Unity batch invocation.
        public static void BuildAndCaptureDrafts()
        {
            try
            {
                BuildPhotoScene(true);
                CaptureDrafts();
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildPhotoScene(bool replaceExisting)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                throw new FileNotFoundException("Production scene was not found.", SourceScenePath);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PhotoScenePath) != null)
            {
                if (!replaceExisting)
                    throw new InvalidOperationException("Photo scene already exists.");
                if (!AssetDatabase.DeleteAsset(PhotoScenePath))
                    throw new IOException("Could not replace the existing photo scene.");
            }

            EnsurePhotoAssetFolder();
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(PhotoTerrainDataPath) != null &&
                !AssetDatabase.DeleteAsset(PhotoTerrainDataPath))
                throw new IOException("Could not replace the isolated photo TerrainData.");

            if (!AssetDatabase.CopyAsset(SourceScenePath, PhotoScenePath))
                throw new IOException("Could not copy the production scene for photography.");
            AssetDatabase.ImportAsset(PhotoScenePath, ImportAssetOptions.ForceSynchronousImport);

            Scene scene = EditorSceneManager.OpenScene(PhotoScenePath, OpenSceneMode.Single);
            GameObject player = RequireRoot(scene, "Player");
            GameObject enemy = RequireRoot(scene, "Enemy");
            GameObject mapGenerator = RequireRoot(scene, "MapGenerator");
            GameObject terrainObject = RequireRoot(scene, "Terrain");
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("The scene Terrain/TerrainData is unavailable.");

            GameObject mechVisual = FindExact(player.transform, "Kartikaris Mech")?.gameObject;
            if (mechVisual == null)
                throw new InvalidOperationException("The real Player Kartikaris Mech was not found.");

            DisableAllSceneCameras(scene);
            DisableGameplay(player);
            DisableGameplay(mapGenerator);
            DisableGameplay(enemy);
            enemy.SetActive(false);
            SetRootInactiveIfPresent(scene, "cockpit");
            SetRootInactiveIfPresent(scene, "Canvas");
            SetRootInactiveIfPresent(scene, "EventSystem");
            FreezeScenePhysicsAndEffects(scene);

            Vector3 stagePoint = FindClearing(terrain, player.transform.position);
            player.transform.SetPositionAndRotation(stagePoint, Quaternion.Euler(0f, 28f, 0f));
            player.SetActive(true);
            mechVisual.SetActive(true);

            PoseCannon(mechVisual, true);
            Bounds mechBounds = CalculateBounds(mechVisual);
            if (mechBounds.size.sqrMagnitude < 0.01f)
                throw new InvalidOperationException("The staged mech has no renderable bounds.");

            ConfigureLighting(scene);
            GameObject photoRoot = new GameObject(PhotoRootName);
            SceneManager.MoveGameObjectToScene(photoRoot, scene);

            foreach (Shot shot in Shots)
                CreateCamera(photoRoot.transform, player.transform, mechBounds, terrain, shot);

            CreateIsolatedPhotoTerrain(terrain, mechBounds.center, photoRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PhotoScenePath))
                throw new IOException("Unity could not save the isolated photo scene.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("PORTFOLIO_PHOTO_SCENE_READY|scene=" + PhotoScenePath +
                "|source=" + SourceScenePath + "|mechPosition=" + Format(player.transform.position) +
                "|mechRotation=" + Format(player.transform.eulerAngles) +
                "|boundsCenter=" + Format(mechBounds.center) +
                "|boundsSize=" + Format(mechBounds.size) +
                "|terrainTrees=" + terrain.terrainData.treeInstanceCount +
                "|staticNonHostile=true");
        }

        // Validation entry point for CI/CLI use. It does not modify or save the scene.
        public static void ValidatePhotoSceneAndExit()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(PhotoScenePath, OpenSceneMode.Single);
                GameObject player = RequireRoot(scene, "Player");
                GameObject enemy = RequireRoot(scene, "Enemy");
                GameObject mapGenerator = RequireRoot(scene, "MapGenerator");
                GameObject cockpit = RequireRoot(scene, "cockpit");
                GameObject canvas = RequireRoot(scene, "Canvas");
                GameObject photoRoot = RequireRoot(scene, PhotoRootName);
                Terrain terrain = RequireRoot(scene, "Terrain").GetComponent<Terrain>();
                List<string> failures = new List<string>();

                Check(!enemy.activeSelf, "Enemy root inactive", failures);
                Check(!cockpit.activeSelf, "cockpit root inactive", failures);
                Check(!canvas.activeSelf, "Canvas root inactive", failures);
                Check(player.GetComponentsInChildren<MonoBehaviour>(true)
                    .All(behaviour => !behaviour.enabled),
                    "Player gameplay MonoBehaviours disabled", failures);
                Check(enemy.GetComponentsInChildren<MonoBehaviour>(true)
                    .All(behaviour => !behaviour.enabled),
                    "Enemy gameplay MonoBehaviours disabled", failures);
                Check(mapGenerator.GetComponentsInChildren<MonoBehaviour>(true)
                    .All(behaviour => !behaviour.enabled),
                    "MapGenerator gameplay MonoBehaviours disabled", failures);
                Check(scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CharacterController>(true))
                    .All(controller => !controller.enabled),
                    "CharacterControllers disabled", failures);

                Camera[] cameras = photoRoot.GetComponentsInChildren<Camera>(true);
                Check(cameras.Length == Shots.Length, "three photo cameras saved", failures);
                Check(cameras.All(camera => !camera.enabled),
                    "photo cameras disabled by default", failures);
                Check(cameras.All(camera => (camera.cullingMask &
                    ((1 << 7) | (1 << 8) | (1 << 9))) == 0),
                    "cockpit and UI layers excluded", failures);
                Check(terrain != null && terrain.terrainData != null &&
                    AssetDatabase.GetAssetPath(terrain.terrainData) == PhotoTerrainDataPath,
                    "private photo TerrainData assigned", failures);
                Check(terrain != null && terrain.terrainData != null &&
                    terrain.terrainData.treeInstanceCount == 24968,
                    "expected copied forest tree count", failures);

                int missingScripts = scene.GetRootGameObjects().Sum(root =>
                    root.GetComponentsInChildren<Transform>(true).Sum(item =>
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject)));
                Check(missingScripts == 0, "no missing scripts", failures);

                foreach (Shot shot in Shots)
                {
                    string path = Path.GetFullPath(Path.Combine(CaptureDirectory, shot.FileName));
                    Check(File.Exists(path), "draft exists: " + shot.FileName, failures);
                    if (!File.Exists(path))
                        continue;
                    Texture2D image = new Texture2D(2, 2);
                    try
                    {
                        Check(image.LoadImage(File.ReadAllBytes(path), true) &&
                            image.width == CaptureWidth && image.height == CaptureHeight,
                            "draft dimensions: " + shot.FileName, failures);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(image);
                    }
                }

                Debug.Log("PORTFOLIO_PHOTO_VALIDATION|failures=" + failures.Count +
                    "|trees=" + (terrain != null && terrain.terrainData != null
                        ? terrain.terrainData.treeInstanceCount : -1) +
                    "|cameras=" + cameras.Length + "|missingScripts=" + missingScripts);
                if (failures.Count > 0)
                    throw new InvalidOperationException(string.Join("; ", failures));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Check(bool condition, string label, List<string> failures)
        {
            if (!condition)
                failures.Add(label);
        }

        private static void EnsurePhotoAssetFolder()
        {
            if (AssetDatabase.IsValidFolder(PhotoAssetFolder))
                return;
            string guid = AssetDatabase.CreateFolder("Assets", "PortfolioPhotoSession");
            if (string.IsNullOrEmpty(guid))
                throw new IOException("Could not create the isolated photo asset folder.");
        }

        private static void CreateIsolatedPhotoTerrain(Terrain terrain, Vector3 target,
            Transform photoRoot)
        {
            TerrainData sourceData = terrain.terrainData;
            TerrainData photoData = UnityEngine.Object.Instantiate(sourceData);
            photoData.name = "Kartikaris Photo TerrainData";
            AssetDatabase.CreateAsset(photoData, PhotoTerrainDataPath);

            Vector3 origin = terrain.transform.position;
            Vector3 size = photoData.size;
            Camera[] cameras = photoRoot.GetComponentsInChildren<Camera>(true);
            List<TreeInstance> retained = new List<TreeInstance>(photoData.treeInstanceCount);
            int removed = 0;

            foreach (TreeInstance tree in photoData.treeInstances)
            {
                Vector2 treePoint = new Vector2(
                    origin.x + tree.position.x * size.x,
                    origin.z + tree.position.z * size.z);
                bool obstructs = Vector2.Distance(treePoint,
                    new Vector2(target.x, target.z)) < 13f;

                foreach (Camera camera in cameras)
                {
                    if (obstructs)
                        break;
                    Vector2 cameraPoint = new Vector2(
                        camera.transform.position.x, camera.transform.position.z);
                    Vector2 targetPoint = new Vector2(target.x, target.z);
                    obstructs = Vector2.Distance(treePoint, cameraPoint) < 8f ||
                        DistanceToSegment(treePoint, cameraPoint, targetPoint) < 10f;
                }

                if (obstructs)
                    removed++;
                else
                    retained.Add(tree);
            }

            photoData.SetTreeInstances(retained.ToArray(), true);
            terrain.terrainData = photoData;
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
                terrainCollider.terrainData = photoData;
            EditorUtility.SetDirty(photoData);
            EditorUtility.SetDirty(terrain);
            if (terrainCollider != null)
                EditorUtility.SetDirty(terrainCollider);

            Debug.Log("PORTFOLIO_PHOTO_FOREST_ISOLATED|sourceTrees=" +
                sourceData.treeInstanceCount + "|retainedTrees=" + retained.Count +
                "|removedSightlineTrees=" + removed + "|asset=" + PhotoTerrainDataPath);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.0001f)
                return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void DisableAllSceneCameras(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = false;
                camera.targetTexture = null;
            }
        }

        private static void DisableGameplay(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (CharacterController controller in
                root.GetComponentsInChildren<CharacterController>(true))
                controller.enabled = false;
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
            {
                source.Stop();
                source.mute = true;
                source.playOnAwake = false;
            }
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;
        }

        private static void FreezeScenePhysicsAndEffects(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }

                foreach (ParticleSystem particles in
                    root.GetComponentsInChildren<ParticleSystem>(true))
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void PoseCannon(GameObject mechVisual, bool unfolded)
        {
            Animator animator = mechVisual.GetComponentInChildren<Animator>(true);
            if (animator == null)
                return;

            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
            bool hasCannon = animator.parameters.Any(parameter =>
                parameter.name == "cannon" && parameter.type == AnimatorControllerParameterType.Bool);
            if (hasCannon)
            {
                animator.SetBool("cannon", unfolded);
                for (int index = 0; index < 240; index++)
                    animator.Update(1f / 30f);
            }
            animator.enabled = false;
        }

        private static Vector3 FindClearing(Terrain terrain, Vector3 fallback)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            TreeInstance[] trees = data.treeInstances;
            Vector3 best = new Vector3(fallback.x, 0f, fallback.z);
            float bestScore = float.NegativeInfinity;
            const int gridRadius = 10;
            const float spacing = 18f;

            for (int zIndex = -gridRadius; zIndex <= gridRadius; zIndex++)
            for (int xIndex = -gridRadius; xIndex <= gridRadius; xIndex++)
            {
                float x = Mathf.Clamp(fallback.x + xIndex * spacing,
                    origin.x + 45f, origin.x + size.x - 45f);
                float z = Mathf.Clamp(fallback.z + zIndex * spacing,
                    origin.z + 45f, origin.z + size.z - 45f);
                float normalizedX = (x - origin.x) / size.x;
                float normalizedZ = (z - origin.z) / size.z;
                float slope = Vector3.Angle(
                    data.GetInterpolatedNormal(normalizedX, normalizedZ), Vector3.up);
                if (slope > 12f)
                    continue;

                float nearestTreeSquared = float.PositiveInfinity;
                foreach (TreeInstance tree in trees)
                {
                    float treeX = origin.x + tree.position.x * size.x;
                    float treeZ = origin.z + tree.position.z * size.z;
                    float deltaX = treeX - x;
                    float deltaZ = treeZ - z;
                    float distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
                    if (distanceSquared < nearestTreeSquared)
                        nearestTreeSquared = distanceSquared;
                }

                float clearance = Mathf.Sqrt(nearestTreeSquared);
                float height = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
                float score = clearance * 5f + height * 0.025f - slope * 0.75f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = new Vector3(x, height + 0.04f, z);
            }

            if (float.IsNegativeInfinity(bestScore))
            {
                best.y = terrain.SampleHeight(best) + origin.y + 0.04f;
            }
            return best;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void ConfigureLighting(Scene scene)
        {
            GameObject lightObject = FindRoot(scene, "Directional Light");
            Light sun = lightObject != null ? lightObject.GetComponent<Light>() : null;
            if (sun == null)
                throw new InvalidOperationException("Directional Light was not found.");

            sun.enabled = true;
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientIntensity = 1.05f;
        }

        private static void CreateCamera(Transform parent, Transform mechRoot,
            Bounds mechBounds, Terrain terrain, Shot shot)
        {
            GameObject cameraObject = new GameObject(shot.CameraName);
            cameraObject.transform.SetParent(parent, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            UniversalAdditionalCameraData cameraData =
                cameraObject.AddComponent<UniversalAdditionalCameraData>();

            float mechHeight = Mathf.Max(mechBounds.size.y, 4f);
            Vector3 forward = Vector3.ProjectOnPlane(mechRoot.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 horizontalDirection =
                (-forward * shot.ForwardWeight + right * shot.RightWeight).normalized;
            float verticalFovRadians = shot.FieldOfView * Mathf.Deg2Rad;
            float fitDistance = mechHeight * 0.58f /
                Mathf.Tan(verticalFovRadians * 0.5f);
            float distance = fitDistance * shot.DistanceMultiplier;
            Vector3 target = mechBounds.center + Vector3.up * (mechHeight * shot.TargetLift);
            Vector3 position = target + horizontalDirection * distance +
                Vector3.up * (mechHeight * shot.CameraLift);
            float terrainHeight = terrain.SampleHeight(position) + terrain.transform.position.y;
            position.y = Mathf.Max(position.y, terrainHeight + 1.2f);

            cameraObject.transform.position = position;
            cameraObject.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = shot.FieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.useOcclusionCulling = true;
            camera.cullingMask = ~((1 << 7) | (1 << 8) | (1 << 9));
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
        }

        private static void Capture(Camera camera, string path, int width, int height)
        {
            RenderTexture renderTexture = new RenderTexture(width, height, 24,
                RenderTextureFormat.ARGB32)
            {
                name = "KartikarisPhotoCapture",
                antiAliasing = 4,
                useMipMap = false,
                autoGenerateMips = false
            };
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void SetRootInactiveIfPresent(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root != null)
                root.SetActive(false);
        }

        private static GameObject RequireRoot(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root == null)
                throw new InvalidOperationException("Required scene root is missing: " + name);
            return root;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        }

        private static Transform FindExact(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                Transform found = FindExact(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static string Format(Vector3 value)
        {
            return value.x.ToString("F3") + "," + value.y.ToString("F3") + "," +
                value.z.ToString("F3");
        }

        private readonly struct Shot
        {
            public string CameraName { get; }
            public string FileName { get; }
            public float FieldOfView { get; }
            public float DistanceMultiplier { get; }
            public float ForwardWeight { get; }
            public float RightWeight { get; }
            public float CameraLift { get; }
            public float TargetLift { get; }

            public Shot(string cameraName, string fileName, float fieldOfView,
                float distanceMultiplier, float forwardWeight, float rightWeight,
                float cameraLift, float targetLift)
            {
                CameraName = cameraName;
                FileName = fileName;
                FieldOfView = fieldOfView;
                DistanceMultiplier = distanceMultiplier;
                ForwardWeight = forwardWeight;
                RightWeight = rightWeight;
                CameraLift = cameraLift;
                TargetLift = targetLift;
            }
        }
    }
}
