using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Greyline.World.EditorTools
{
    /// <summary>
    /// World / Visual track tooling. Owns an isolated grey-box sandbox scene and a post-processing
    /// grade profile so HDRP visual-identity work never touches the Combat track's DevCombat scene,
    /// DevCombat volume profile, or shared HDRP default resources.
    ///
    /// Execution policy: explicit menu / CLI only. No InitializeOnLoad, no reliance on auto refresh.
    /// Record the Console result after every run.
    /// </summary>
    public static class WorldSandbox
    {
        public const string ScenePath = "Assets/_Project/Scenes/WorldSandbox.unity";
        public const string GradeProfilePath = "Assets/_Project/Settings/WorldSandboxGradeProfile.asset";
        private const string EnvironmentMaterialFolder = "Assets/_Project/Art/Environment/Materials";
        private const string BannerTexturePath = "Assets/_Project/Art/Environment/Textures/CampusA_AbstractBanner.asset";

        // Reused, unmodified project render settings profile.
        private const string SkyAndFogProfilePath = "Assets/Settings/SkyandFogSettingsProfile.asset";

        private const float HumanReferenceHeight = 1.8f;

        [MenuItem("Greyline/World/Build or Refresh World Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before building the World Sandbox.");
                return;
            }

            EnsureFolders();
            BuildGradeProfile();
            BuildScene();
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Greyline/World/Validate World Visual Baseline")]
        public static void Validate()
        {
            int errors = 0;
            int warnings = 0;

            VolumeProfile grade = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradeProfilePath);
            if (grade == null)
            {
                errors += Error("World grade profile missing: " + GradeProfilePath);
            }
            else
            {
                if (grade.components.Count == 0)
                {
                    errors += Error("World grade profile has no stored components; run Build or Refresh World Sandbox.");
                }

                foreach (VolumeComponent component in grade.components)
                {
                    if (component == null)
                    {
                        errors += Error("World grade profile has an unresolved (null) sub-asset; run Build or Refresh World Sandbox.");
                    }
                }

                errors += Require<Tonemapping>(grade, ref warnings);
                errors += Require<ColorAdjustments>(grade, ref warnings);
                errors += Require<WhiteBalance>(grade, ref warnings);
                errors += Require<Bloom>(grade, ref warnings);
                errors += Require<Vignette>(grade, ref warnings);
                errors += Require<ShadowsMidtonesHighlights>(grade, ref warnings);
                errors += Require<HDShadowSettings>(grade, ref warnings);
            }

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(SkyAndFogProfilePath) == null)
            {
                errors += Error("Shared Sky and Fog profile missing: " + SkyAndFogProfilePath);
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                errors += Error("World Sandbox scene missing: " + ScenePath);
            }
            else
            {
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                bool openedHere = !scene.isLoaded;
                if (openedHere)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                }

                errors += RequireRoot(scene, "Ground");
                errors += RequireRoot(scene, "Key Light");
                errors += RequireRoot(scene, "Sky and Fog Volume");
                errors += RequireRoot(scene, "World Grade Volume");
                errors += RequireRoot(scene, "Massing");
                errors += RequireRoot(scene, "ScaleRef_1.8m");
                errors += RequireNoMassingPrefix(scene, "Boulevard");
                errors += RequireNoMassingPrefix(scene, "Apartment_");
                errors += RequireNoMassingPrefix(scene, "Monument_");
                errors += RequireNoMassingPrefix(scene, "Courtyard_");

                if (FindRoot(scene, "Player") != null)
                {
                    errors += Error("World Sandbox must not contain a Player; gameplay lives in DevCombat.");
                }

                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            string summary = $"World visual baseline validation complete. Errors: {errors}, Warnings: {warnings}.";
            if (errors == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        // Explicit Unity CLI -executeMethod gates (interactive Editor closed).
        public static void BuildFromCommandLine() => Build();

        public static void ValidateFromCommandLine() => Validate();

        // ---- Grade profile -------------------------------------------------

        private static void BuildGradeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, GradeProfilePath);
            }

            // Drop unresolved sub-asset references left by an earlier partial write.
            profile.components.RemoveAll(component => component == null);

            // Neutral tonemap: keep signage colour readable, no ACES contrast crush on B-movie flats.
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            // Cold, slightly desaturated daylight; concrete-grey identity.
            WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.temperature.Override(-8f);
            whiteBalance.tint.Override(4f);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(8f);
            color.saturation.Override(-6f);
            color.colorFilter.Override(new Color(0.96f, 0.97f, 1f, 1f));

            // Lift shadows a touch, pull highlights so overcast sky does not clip; warm-cold split.
            ShadowsMidtonesHighlights smh = GetOrAdd<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.98f, 1.0f, 1.04f, 0.02f));
            smh.highlights.Override(new Vector4(1.02f, 1.0f, 0.97f, -0.03f));

            // Restrained bloom: signage / sodium lamps glow, no bleed.
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.intensity.Override(0.12f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(1f, 0.97f, 0.9f, 1f));

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.4f);

            // World-scale shadowing for wide boulevards and tall slabs.
            HDShadowSettings shadows = GetOrAdd<HDShadowSettings>(profile);
            shadows.maxShadowDistance.Override(180f);
            shadows.cascadeShadowSplitCount.Override(4);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GradeProfilePath);
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
            {
                return existing;
            }

            // profile.Add creates the component in memory only; it must also be
            // stored as a sub-asset or the overrides never reach disk.
            T component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInHierarchy;
            if (!AssetDatabase.Contains(component))
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            return component;
        }

        // ---- Scene -------------------------------------------------------

        // Managed name prefixes inside "Massing". Children matching one of these but no longer
        // produced by a build are removed; anything else a person parented there is left alone.
        private static readonly string[] ManagedMassingPrefixes =
        {
            "Boulevard", "Apartment_", "Monument_", "Courtyard_", "CampusA_", "WorldB_", "WorldC_"
        };

        /// <summary>
        /// Idempotent: reuses the existing sandbox scene and updates managed objects in place so a
        /// repeated build produces no scene diff. Non-managed objects a person added are preserved.
        /// </summary>
        private static void BuildScene()
        {
            Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            UpdateGround(scene);
            UpdateKeyLight(scene);
            UpdateVolume(scene, "Sky and Fog Volume", SkyAndFogProfilePath, 0f, warnIfProfileMissing: true);
            UpdateVolume(scene, "World Grade Volume", GradeProfilePath, 1f, warnIfProfileMissing: false);
            Dictionary<string, Material> materials = BuildEnvironmentMaterials();
            UpdateMassing(scene, materials);
            UpdateCampusADressing(scene, materials);
            UpdateSurroundingField(scene, materials);
            UpdateMarketAlley(scene, materials);
            UpdateScaleReference(scene);
            UpdateSandboxCamera(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("World Sandbox scene refreshed at " + ScenePath);
        }

        private static GameObject GetOrCreateRoot(Scene scene, string name)
        {
            GameObject go = FindRoot(scene, name);
            if (go == null)
            {
                go = new GameObject(name);
            }

            if (go.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            return go;
        }

        private static GameObject GetOrCreatePrimitiveRoot(Scene scene, string name, PrimitiveType primitive)
        {
            GameObject go = FindRoot(scene, name);
            if (go == null || go.GetComponent<MeshFilter>() == null)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }

                go = GameObject.CreatePrimitive(primitive);
                go.name = name;
            }

            if (go.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            return go;
        }

        private static void UpdateGround(Scene scene)
        {
            GameObject ground = GetOrCreatePrimitiveRoot(scene, "Ground", PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(30f, 1f, 30f); // 300 m x 300 m
            ground.isStatic = true;
        }

        private static void UpdateKeyLight(Scene scene)
        {
            GameObject sun = GetOrCreateRoot(scene, "Key Light");
            Light light = sun.GetComponent<Light>();
            if (light == null)
            {
                light = sun.AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            // Low, raking morning angle for long shadows down the boulevard.
            sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
        }

        private static void UpdateVolume(Scene scene, string name, string profilePath, float priority, bool warnIfProfileMissing)
        {
            GameObject go = GetOrCreateRoot(scene, name);
            Volume volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (warnIfProfileMissing && volume.sharedProfile == null)
            {
                Debug.LogWarning($"Volume profile not found for '{name}': {profilePath}");
            }
        }

        private static void UpdateMassing(Scene scene, Dictionary<string, Material> materials)
        {
            GameObject root = GetOrCreateRoot(scene, "Massing");
            HashSet<string> built = new HashSet<string>();

            // Reversible Vertical Slice candidate A: fictional university arrival axis.
            // The composition extracts scale, rhythm and sightline from references without
            // reproducing a real campus name, footprint or landmark.
            built.Add(Block(root.transform, "CampusA_ArrivalPlaza", new Vector3(0f, 0.08f, -28f), new Vector3(46f, 0.16f, 30f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_Arrival_Occluder_A", new Vector3(-6f, 4f, -86f), new Vector3(20f, 8f, 3f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_Arrival_Occluder_B", new Vector3(6f, 3f, -56f), new Vector3(16f, 6f, 3f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_Gate_LeftPillar", new Vector3(-12f, 4f, -12f), new Vector3(3f, 8f, 3f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_Gate_RightPillar", new Vector3(12f, 4f, -12f), new Vector3(3f, 8f, 3f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_Gate_Lintel", new Vector3(0f, 8f, -12f), new Vector3(27f, 3f, 3f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_Gate_Emblem", new Vector3(0f, 8f, -10.2f), new Vector3(4f, 2f, 0.6f), materials["banner"]));

            // Formal campus axis: a low stair plinth, broad steps and a shallow main hall.
            built.Add(Block(root.transform, "CampusA_MainPlinth", new Vector3(0f, 0.8f, 48f), new Vector3(42f, 1.6f, 18f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_MainHall", new Vector3(0f, 10f, 55f), new Vector3(34f, 18f, 12f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_MainHall_LeftWing", new Vector3(-18f, 7f, 55f), new Vector3(10f, 12f, 10f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_MainHall_RightWing", new Vector3(18f, 7f, 55f), new Vector3(10f, 12f, 10f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_MainSteps", new Vector3(0f, 2.1f, 42f), new Vector3(18f, 2.6f, 8f), materials["stone"]));

            // Small interaction and event pockets keep the axis dense without becoming a district.
            built.Add(Block(root.transform, "CampusA_NoticeBoard", new Vector3(-18f, 2.5f, -27f), new Vector3(2f, 5f, 0.6f)));
            built.Add(Block(root.transform, "CampusA_ServicePocket", new Vector3(-28f, 0.1f, 18f), new Vector3(22f, 0.2f, 26f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_ServiceWall", new Vector3(-38f, 4f, 18f), new Vector3(2f, 8f, 26f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_Signage_Left", new Vector3(-21f, 3f, 12f), new Vector3(4f, 6f, 0.6f), materials["banner"]));
            built.Add(Block(root.transform, "CampusA_Signage_Right", new Vector3(21f, 3f, 12f), new Vector3(4f, 6f, 0.6f), materials["banner"]));

            // Dormitory massing frames the campus edge while preserving the central sightline.
            built.Add(Block(root.transform, "CampusA_Dorm_L", new Vector3(-32f, 16f, 78f), new Vector3(12f, 32f, 20f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_Dorm_R", new Vector3(32f, 9f, 78f), new Vector3(12f, 18f, 20f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_Dorm_L_Back", new Vector3(-32f, 9f, 106f), new Vector3(12f, 18f, 20f), materials["concrete"]));
            built.Add(Block(root.transform, "CampusA_Dorm_R_Back", new Vector3(32f, 9f, 106f), new Vector3(12f, 18f, 20f), materials["concrete"]));

            // Campus-only visual anchor at the end of the arrival axis.
            built.Add(Block(root.transform, "CampusA_SignaturePlinth", new Vector3(22f, 1.5f, 116f), new Vector3(18f, 3f, 18f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_SignaturePylon", new Vector3(22f, 17f, 116f), new Vector3(3f, 28f, 3f), materials["stone"]));

            PruneStaleManaged(root.transform, built,
                "Boulevard", "Apartment_", "Monument_", "Courtyard_",
                "CampusA_Arrival", "CampusA_Gate_", "CampusA_Main", "CampusA_NoticeBoard",
                "CampusA_Service", "CampusA_Signage_", "CampusA_Dorm_", "CampusA_Signature");
        }

        private static void UpdateSurroundingField(Scene scene, Dictionary<string, Material> materials)
        {
            GameObject root = GetOrCreateRoot(scene, "Massing");
            HashSet<string> built = new HashSet<string>();

            // Foreground circulation: the campus remains the focal axis while the camera reads
            // an actual city approach on both sides of the arrival plaza.
            built.Add(Block(root.transform, "WorldB_Road_West", new Vector3(-58f, -0.04f, 0f), new Vector3(18f, 0.08f, 286f), materials["asphalt"]));
            built.Add(Block(root.transform, "WorldB_Road_East", new Vector3(58f, -0.04f, 0f), new Vector3(18f, 0.08f, 286f), materials["asphalt"]));
            built.Add(Block(root.transform, "WorldB_EntryAlley", new Vector3(0f, -0.02f, -92f), new Vector3(10f, 0.08f, 74f), materials["asphalt"]));
            built.Add(Block(root.transform, "WorldB_Sidewalk_West", new Vector3(-45f, 0.05f, 0f), new Vector3(4f, 0.1f, 286f), materials["sidewalk"]));
            built.Add(Block(root.transform, "WorldB_Sidewalk_East", new Vector3(45f, 0.05f, 0f), new Vector3(4f, 0.1f, 286f), materials["sidewalk"]));
            built.Add(Block(root.transform, "WorldB_Curb_West", new Vector3(-47.5f, 0.18f, 0f), new Vector3(1f, 0.36f, 286f), materials["curb"]));
            built.Add(Block(root.transform, "WorldB_Curb_East", new Vector3(47.5f, 0.18f, 0f), new Vector3(1f, 0.36f, 286f), materials["curb"]));

            for (int crossing = 0; crossing < 3; crossing++)
            {
                float z = -92f + crossing * 38f;
                for (int stripe = 0; stripe < 5; stripe++)
                {
                    built.Add(Block(root.transform, $"WorldB_Crossing_{crossing}_{stripe}",
                        new Vector3(-4.2f + stripe * 2.1f, 0.14f, z), new Vector3(1.1f, 0.08f, 8f), materials["stone"]));
                }
            }

            // Midground context: low-rise life and a service route frame the core without closing
            // the gate-to-hall view. The right side is harder and more utilitarian by design.
            built.Add(Block(root.transform, "WorldB_Housing_West", new Vector3(-72f, 4f, -38f), new Vector3(22f, 8f, 24f), materials["facade"]));
            built.Add(Block(root.transform, "WorldB_Shop_West", new Vector3(-72f, 3f, 12f), new Vector3(18f, 6f, 14f), materials["facade"]));
            built.Add(Block(root.transform, "WorldB_Shop_East", new Vector3(72f, 3.5f, -18f), new Vector3(18f, 7f, 16f), materials["facade"]));
            built.Add(Block(root.transform, "WorldB_Housing_East", new Vector3(72f, 5f, 38f), new Vector3(22f, 10f, 22f), materials["facade"]));
            built.Add(Block(root.transform, "WorldB_Wall_West", new Vector3(-44f, 2.2f, 43f), new Vector3(1.2f, 4.4f, 32f), materials["concrete"]));
            built.Add(Block(root.transform, "WorldB_Wall_East", new Vector3(44f, 2.2f, 23f), new Vector3(1.2f, 4.4f, 24f), materials["concrete"]));
            built.Add(Block(root.transform, "WorldB_ServiceAlley", new Vector3(50f, 0.02f, 84f), new Vector3(22f, 0.08f, 52f), materials["asphalt"]));
            built.Add(Block(root.transform, "WorldB_ServiceGate", new Vector3(39f, 3f, 84f), new Vector3(2f, 6f, 4f), materials["metal"]));

            for (int i = 0; i < 4; i++)
            {
                float z = -50f + i * 20f;
                built.Add(Block(root.transform, $"WorldB_HousingWindow_West_{i}", new Vector3(-60.8f, 4.5f, z), new Vector3(0.35f, 1.8f, 3f), materials["glass"]));
                if (i < 3)
                {
                    built.Add(Block(root.transform, $"WorldB_HousingWindow_East_{i}", new Vector3(60.8f, 5.5f, z + 35f), new Vector3(0.35f, 1.8f, 3f), materials["glass"]));
                }
            }

            // Background skyline: restrained silhouettes close the horizon and preserve the
            // offset CampusA pylon as a readable secondary landmark.
            built.Add(Block(root.transform, "WorldB_ApartmentSlab_West", new Vector3(-84f, 15f, 118f), new Vector3(18f, 30f, 24f), materials["facade"]));
            built.Add(Block(root.transform, "WorldB_ApartmentSlab_East", new Vector3(82f, 12f, 132f), new Vector3(22f, 24f, 28f), materials["facade"]));
            built.Add(Block(root.transform, "WorldB_CivicMass", new Vector3(-92f, 10f, 72f), new Vector3(26f, 20f, 18f), materials["civic"]));
            built.Add(Block(root.transform, "WorldB_DistantLandmark_Base", new Vector3(96f, 2f, 142f), new Vector3(12f, 4f, 12f), materials["stone"]));
            built.Add(Block(root.transform, "WorldB_DistantLandmark", new Vector3(96f, 18f, 142f), new Vector3(3f, 28f, 3f), materials["banner"]));

            PruneStaleManaged(root.transform, built, "WorldB_");
        }

        /// <summary>
        /// Reversible Vertical Slice candidate: a market alley occupying the north end of the
        /// existing "WorldB_EntryAlley" corridor (x in [-5,5], z in [-124,-96]), before the first
        /// boulevard crossing at z=-92 and the campus arrival occluders (z=-86 / z=-56). Placed
        /// inside the existing corridor deliberately so it does not move or renumber any
        /// already-reviewed CampusA/WorldB coordinate. See
        /// Assets/Editor/World/WorldMarketAlleyReferenceBrief.md and
        /// Docs/ASTRA_START_HERE.md for the current field target.
        /// </summary>
        private static void UpdateMarketAlley(Scene scene, Dictionary<string, Material> materials)
        {
            GameObject root = GetOrCreateRoot(scene, "Massing");
            HashSet<string> built = new HashSet<string>();

            // Covered aisle: overhead cover is the segment's medium occluder (BOTW-translation
            // rule in WORLD_LEVEL_DESIGN_GUIDE.md Sec.5) and reads as a roofed market hall.
            // Sits just above the existing WorldB_EntryAlley asphalt (top at y=0.02) as a distinct
            // paved floor layer, deliberately avoiding any edit to that already-reviewed object.
            built.Add(Block(root.transform, "WorldC_AlleyFloor", new Vector3(0f, 0.03f, -110f), new Vector3(10f, 0.04f, 28f), materials["sidewalk"]));
            built.Add(Block(root.transform, "WorldC_MarketRoof", new Vector3(0f, 4.6f, -110f), new Vector3(10f, 0.25f, 30f), materials["metal"]));

            // Threshold at the arrival-facing (north) end: legible narrowing cue before the dense aisle.
            built.Add(Block(root.transform, "WorldC_Threshold_West", new Vector3(-5f, 2.3f, -124f), new Vector3(1f, 4.6f, 1.2f), materials["concrete"]));
            built.Add(Block(root.transform, "WorldC_Threshold_East", new Vector3(5f, 2.3f, -124f), new Vector3(1f, 4.6f, 1.2f), materials["concrete"]));
            built.Add(Block(root.transform, "WorldC_Threshold_Lintel", new Vector3(0f, 4.5f, -124f), new Vector3(10f, 0.7f, 1f), materials["concrete"]));

            // Five evenly-spaced bay positions (uniform module rhythm per the reference brief); the
            // centre bay (z=-110) is intentionally skipped here and widened into a vendor/social
            // pocket below instead of a stall pair -- a local/decision-point landmark and NPC
            // pocket, not a mirrored copy of the other four.
            float[] stallZ = { -119f, -114.5f, -105.5f, -101f };
            for (int i = 0; i < stallZ.Length; i++)
            {
                float z = stallZ[i];
                built.Add(Block(root.transform, $"WorldC_Stall_West_Counter_{i}", new Vector3(-3.5f, 1.0f, z), new Vector3(2.2f, 1.0f, 3.2f), materials["stone"]));
                built.Add(Block(root.transform, $"WorldC_Stall_East_Counter_{i}", new Vector3(3.5f, 1.0f, z), new Vector3(2.2f, 1.0f, 3.2f), materials["stone"]));
                built.Add(Block(root.transform, $"WorldC_Stall_West_Goods_{i}", new Vector3(-4.4f, 2.2f, z), new Vector3(1.0f, 2.2f, 3.0f), materials["civic"]));
                built.Add(Block(root.transform, $"WorldC_Stall_East_Goods_{i}", new Vector3(4.4f, 2.2f, z), new Vector3(1.0f, 2.2f, 3.0f), materials["civic"]));
            }

            // Stall signage: every-bay density per the reference brief's "highest visual-information
            // density segment" note; abstract banner material stands in for fictional price/product art.
            for (int i = 0; i < stallZ.Length; i++)
            {
                float z = stallZ[i];
                built.Add(Quad(root.transform, $"WorldC_Stall_West_Sign_{i}", new Vector3(-4.4f, 3.6f, z), Quaternion.Euler(0f, 90f, 0f), new Vector2(2.8f, 1f), materials["banner"]));
                built.Add(Quad(root.transform, $"WorldC_Stall_East_Sign_{i}", new Vector3(4.4f, 3.6f, z), Quaternion.Euler(0f, -90f, 0f), new Vector2(2.8f, 1f), materials["banner"]));
            }

            // Vendor / social pocket at the widened bay: bench + planter, no counter -- a distinct
            // local landmark the player can use to describe "where in the alley" they are.
            built.Add(Block(root.transform, "WorldC_VendorPocket_Bench", new Vector3(-3.2f, 0.8f, -110f), new Vector3(2.6f, 0.8f, 1.0f), materials["stone"]));
            built.Add(Block(root.transform, "WorldC_VendorPocket_Planter", new Vector3(3.4f, 0.7f, -110f), new Vector3(1.8f, 1.4f, 1.8f), materials["stone"]));

            // Warm interior lighting under the roof (sodium-lamp glow the existing Bloom tint already
            // budgets for), since the overhead cover blocks the directional key light here.
            built.Add(Lamp(root.transform, "WorldC_Lamp_0", new Vector3(0f, 3.5f, -119f), materials["metal"]));
            built.Add(Lamp(root.transform, "WorldC_Lamp_1", new Vector3(0f, 3.5f, -101f), materials["metal"]));

            PruneStaleManaged(root.transform, built, "WorldC_");
        }

        private static Dictionary<string, Material> BuildEnvironmentMaterials()
        {
            EnsureFolder("Assets/_Project", "Art");
            EnsureFolder("Assets/_Project/Art", "Environment");
            EnsureFolder("Assets/_Project/Art/Environment", "Materials");
            EnsureFolder("Assets/_Project/Art/Environment", "Textures");

            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            materials["concrete"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_ConcreteGrey.mat", "World_ConcreteGrey", new Color(0.42f, 0.44f, 0.46f, 1f), 0.72f);
            materials["stone"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_StonePale.mat", "World_StonePale", new Color(0.64f, 0.63f, 0.58f, 1f), 0.58f);
            materials["green"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_GreenMuted.mat", "World_GreenMuted", new Color(0.18f, 0.28f, 0.16f, 1f), 0.86f);
            materials["metal"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_MetalDark.mat", "World_MetalDark", new Color(0.12f, 0.14f, 0.15f, 1f), 0.42f);
            materials["asphalt"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_Asphalt.mat", "World_Asphalt", new Color(0.075f, 0.085f, 0.09f, 1f), 0.32f);
            materials["sidewalk"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_Sidewalk.mat", "World_Sidewalk", new Color(0.52f, 0.53f, 0.51f, 1f), 0.48f);
            materials["curb"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_Curb.mat", "World_Curb", new Color(0.36f, 0.37f, 0.36f, 1f), 0.52f);
            materials["facade"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_FacadeMuted.mat", "World_FacadeMuted", new Color(0.30f, 0.32f, 0.33f, 1f), 0.66f);
            materials["civic"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_CivicBlueGrey.mat", "World_CivicBlueGrey", new Color(0.24f, 0.29f, 0.32f, 1f), 0.6f);
            materials["glass"] = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_GlassBlueGrey.mat", "World_GlassBlueGrey", new Color(0.12f, 0.22f, 0.27f, 1f), 0.2f);
            materials["banner"] = GetOrCreateBannerMaterial();
            return materials;
        }

        private static Material GetOrCreateMaterial(string path, string name, Color color, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit");
                if (shader == null)
                {
                    Debug.LogWarning("HDRP/Lit shader not found while preparing World materials.");
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateBannerMaterial()
        {
            Material material = GetOrCreateMaterial(
                EnvironmentMaterialFolder + "/World_BannerAbstract.mat", "World_BannerAbstract", new Color(0.55f, 0.08f, 0.06f, 1f), 0.35f);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BannerTexturePath);
            if (texture == null)
            {
                texture = new Texture2D(8, 2, TextureFormat.RGBA32, false) { name = "CampusA_AbstractBanner" };
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    int stripe = i % 8;
                    pixels[i] = stripe == 0 || stripe == 1 ? new Color(0.92f, 0.76f, 0.24f, 1f) : new Color(0.52f, 0.06f, 0.05f, 1f);
                }

                texture.SetPixels(pixels);
                texture.Apply();
                AssetDatabase.CreateAsset(texture, BannerTexturePath);
            }

            material.SetTexture("_BaseColorMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void UpdateCampusADressing(Scene scene, Dictionary<string, Material> materials)
        {
            GameObject root = GetOrCreateRoot(scene, "Massing");
            HashSet<string> built = new HashSet<string>();

            // Repeated windows make the civic mass read as a place rather than an empty block.
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 7; column++)
                {
                    float x = -13.5f + column * 4.5f;
                    float y = 7f + row * 4.5f;
                    built.Add(Block(root.transform, $"CampusA_Window_Main_{row}_{column}", new Vector3(x, y, 48.75f), new Vector3(2.1f, 1.5f, 0.35f), materials["metal"]));
                }
            }

            // Formal approach dressing: lamps, trees, benches and low planters.
            for (int i = 0; i < 4; i++)
            {
                float z = -72f + i * 28f;
                built.Add(Lamp(root.transform, $"CampusA_Lamp_-1_{i}", new Vector3(-18f, 3.5f, z), materials["metal"]));
                built.Add(Tree(root.transform, $"CampusA_Tree_-1_{i}", new Vector3(-25f, 2.5f, z + 8f), materials["green"]));
            }

            for (int i = 0; i < 2; i++)
            {
                float z = -38f + i * 54f;
                built.Add(Lamp(root.transform, $"CampusA_Lamp_1_{i}", new Vector3(18f, 3.5f, z), materials["metal"]));
                built.Add(Tree(root.transform, $"CampusA_Tree_1_{i}", new Vector3(25f, 2.5f, z + 8f), materials["green"]));
            }

            built.Add(Block(root.transform, "CampusA_Bench_-1", new Vector3(-10f, 0.8f, -8f), new Vector3(5f, 0.8f, 1.2f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_Bench_1", new Vector3(10f, 0.8f, 46f), new Vector3(5f, 0.8f, 1.2f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_Planter_-1", new Vector3(-14f, 0.7f, 22f), new Vector3(5f, 1.4f, 5f), materials["stone"]));
            built.Add(Block(root.transform, "CampusA_Planter_1", new Vector3(14f, 0.7f, 70f), new Vector3(5f, 1.4f, 5f), materials["stone"]));
            built.Add(Quad(root.transform, "CampusA_Banner_Left", new Vector3(-24f, 6f, 10f), Quaternion.Euler(0f, 90f, 0f), new Vector2(4f, 6f), materials["banner"]));
            built.Add(Quad(root.transform, "CampusA_Banner_Right", new Vector3(24f, 6f, 68f), Quaternion.Euler(0f, -90f, 0f), new Vector2(4f, 6f), materials["banner"]));
            PruneStaleManaged(root.transform, built, "CampusA_Window_", "CampusA_Lamp_", "CampusA_Tree_", "CampusA_Bench_", "CampusA_Planter_", "CampusA_Banner_");
        }

        private static string Block(Transform parent, string name, Vector3 position, Vector3 size)
        {
            return Block(parent, name, position, size, null);
        }

        private static string Block(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            Transform existing = parent.Find(name);
            GameObject block = existing != null ? existing.gameObject : null;
            if (block == null || block.GetComponent<MeshFilter>() == null)
            {
                if (block != null)
                {
                    Object.DestroyImmediate(block);
                }

                block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = name;
                block.transform.SetParent(parent, false);
            }

            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.isStatic = true;
            ApplyMaterial(block, material);
            return name;
        }

        private static string Lamp(Transform parent, string name, Vector3 position, Material material)
        {
            GameObject lamp = GetOrCreatePrimitiveChild(parent, name, PrimitiveType.Cylinder);
            lamp.transform.localPosition = position;
            lamp.transform.localScale = new Vector3(0.25f, 3.5f, 0.25f);
            ApplyMaterial(lamp, material);

            // Add the Light before flipping the static flag: a fully-static GameObject rejects the
            // realtime Light component add under -batchmode, which returned a null handle and aborted
            // the whole builder during integration.
            Light light = lamp.GetComponent<Light>();
            if (light == null)
            {
                light = lamp.AddComponent<Light>();
            }

            if (light != null)
            {
                light.type = LightType.Point;
                light.color = new Color(1f, 0.73f, 0.42f);
                light.intensity = 1.2f;
                light.range = 10f;
            }

            lamp.isStatic = true;
            return name;
        }

        private static string Tree(Transform parent, string name, Vector3 position, Material material)
        {
            GameObject tree = GetOrCreatePrimitiveChild(parent, name, PrimitiveType.Cylinder);
            tree.transform.localPosition = position;
            tree.transform.localScale = new Vector3(1.1f, 2.5f, 1.1f);
            tree.isStatic = true;
            ApplyMaterial(tree, material);
            return name;
        }

        private static string Quad(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, Material material)
        {
            GameObject quad = GetOrCreatePrimitiveChild(parent, name, PrimitiveType.Quad);
            quad.transform.localPosition = position;
            quad.transform.localRotation = rotation;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.isStatic = true;
            ApplyMaterial(quad, material);
            return name;
        }

        private static GameObject GetOrCreatePrimitiveChild(Transform parent, string name, PrimitiveType primitive)
        {
            Transform existing = parent.Find(name);
            GameObject child = existing != null ? existing.gameObject : null;
            if (child == null || child.GetComponent<MeshFilter>() == null)
            {
                if (child != null)
                {
                    Object.DestroyImmediate(child);
                }

                child = GameObject.CreatePrimitive(primitive);
                child.name = name;
                child.transform.SetParent(parent, false);
            }

            return child;
        }

        private static void ApplyMaterial(GameObject go, Material material)
        {
            if (material == null)
            {
                return;
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void PruneStaleManaged(Transform parent, HashSet<string> keep)
        {
            PruneStaleManaged(parent, keep, ManagedMassingPrefixes);
        }

        private static void PruneStaleManaged(Transform parent, HashSet<string> keep, params string[] prefixes)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (keep.Contains(child.name))
                {
                    continue;
                }

                foreach (string prefix in prefixes)
                {
                    if (child.name.StartsWith(prefix))
                    {
                        Object.DestroyImmediate(child);
                        break;
                    }
                }
            }
        }

        private static void UpdateScaleReference(Scene scene)
        {
            GameObject capsule = GetOrCreatePrimitiveRoot(scene, "ScaleRef_1.8m", PrimitiveType.Capsule);
            capsule.transform.localScale = new Vector3(0.5f, HumanReferenceHeight / 2f, 0.5f);
            capsule.transform.position = new Vector3(0f, HumanReferenceHeight / 2f, 0f);
            Collider collider = capsule.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void UpdateSandboxCamera(Scene scene)
        {
            GameObject cameraObject = FindRoot(scene, "Sandbox Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Sandbox Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
            }
            else if (cameraObject.GetComponent<Camera>() == null)
            {
                cameraObject.AddComponent<Camera>();
            }

            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3f, -140f);
            cameraObject.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
        }

        // ---- helpers ---------------------------------------------------

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project", "Scenes");
            EnsureFolder("Assets/_Project", "Settings");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static int Require<T>(VolumeProfile profile, ref int warnings) where T : VolumeComponent
        {
            if (!profile.TryGet(out T _))
            {
                return Error($"World grade profile is missing {typeof(T).Name}.");
            }

            return 0;
        }

        private static int RequireRoot(Scene scene, string name)
        {
            return FindRoot(scene, name) == null ? Error($"World Sandbox is missing '{name}'.") : 0;
        }

        private static int RequireNoMassingPrefix(Scene scene, string prefix)
        {
            GameObject massing = FindRoot(scene, "Massing");
            if (massing == null)
            {
                return 0;
            }

            for (int i = 0; i < massing.transform.childCount; i++)
            {
                if (massing.transform.GetChild(i).name.StartsWith(prefix))
                {
                    return Error($"World Sandbox must not contain generic district mass '{massing.transform.GetChild(i).name}'.");
                }
            }

            return 0;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static int Error(string message)
        {
            Debug.LogError(message);
            return 1;
        }
    }
}
