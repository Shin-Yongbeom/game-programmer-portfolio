using System;
using System.Collections.Generic;
using Greyline.CameraSystem;
using Greyline.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Greyline.World.EditorTools
{
    /// <summary>Original procedural city kit; explicit authoring only, with a traversable loop validator.</summary>
    public static class ProductionDistrictBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/ProductionDistrict.unity";
        private const string ArtFolder = "Assets/_Project/Art/Environment/ProductionDistrict";
        private const string DevScene = "Assets/_Project/Scenes/DevCombat.unity";
        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly Dictionary<Material, List<CombineInstance>> Decoration = new();
        private static Mesh cubeMesh;
        private static Texture2D surfaceTexture;
        public static readonly Vector3 Spawn = new(0, .2f, -66);
        public static readonly Vector3[] WalkRoute =
        {
            Spawn, new(0,.2f,-29), new(0,.2f,18), new(-43,.2f,18), new(-43,.2f,35),
            new(-17,.2f,35), new(0,.2f,35), new(0,.2f,4), new(38,.2f,4),
            new(63,.2f,4), new(63,.2f,24), new(63,.2f,42), new(63,.2f,62),
            new(26,.2f,62), new(0,.2f,53), new(0,.2f,30), new(0,.2f,-29), Spawn
        };

        [MenuItem("Greyline/World/Build Playable Production District")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rebuilding the district.");
            EnsureFolder(ArtFolder);
            PrepareMaterials();
            Decoration.Clear();
            Scene source = EditorSceneManager.OpenScene(DevScene, OpenSceneMode.Single);
            GameObject sourcePlayer = FindRoot(source, "Player");
            if (sourcePlayer == null) throw new InvalidOperationException("DevCombat player is required.");
            GameObject player = Object.Instantiate(sourcePlayer);
            GameObject sourceCamera = FindRoot(source, "Follow Camera");
            if (sourceCamera == null) throw new InvalidOperationException("DevCombat follow camera is required.");
            GameObject cameraObject = Object.Instantiate(sourceCamera);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(player, scene);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(scene);
            player.name = "Player";
            player.transform.SetPositionAndRotation(Spawn, Quaternion.identity);
            cameraObject.name = "Follow Camera";
            cameraObject.transform.SetPositionAndRotation(Spawn + new Vector3(0, 2.6f, -5.2f), Quaternion.Euler(12, 0, 0));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 450;
            camera.fieldOfView = 64;
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            cameraObject.GetComponent<ThirdPersonOrbitCamera>().Configure(player.transform, actions);
            player.GetComponent<ThirdPersonPlayerMotor>().Configure(camera, actions);
            Transform environment = new GameObject("District Environment").transform;
            GroundAndRoutes(environment);
            Boulevard(environment);
            University(environment);
            Market(environment);
            Plaza(environment);
            Interior(environment);
            Lighting();
            BuildAnchors(player.transform);
            FlushDecoration(environment);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Production district could not be saved.");
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PRODUCTION_DISTRICT_BUILD_OK " + ScenePath);
        }

        public static void BuildFromCommandLine() => Build();
        public static void ValidateFromCommandLine() => Validate();

        [MenuItem("Greyline/World/Validate Playable Production District")]
        public static void Validate()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            List<string> errors = new();
            foreach (string root in new[] { "Player", "Follow Camera", "District Environment", "District Navigation", "EncounterAnchors", "QA Vantage Points", "Key Light", "District Grade" })
                if (FindRoot(scene, root) == null) errors.Add("Missing root " + root);
            GameObject player = FindRoot(scene, "Player");
            if (player != null && (player.GetComponent<CharacterController>() == null || player.GetComponent<PlayerCombat>() == null)) errors.Add("Player lacks combat/controller.");
            GameObject navigation = FindRoot(scene, "District Navigation");
            if (navigation != null && navigation.GetComponent<ProductionDistrictNavigation>().Places.Length < 6) errors.Add("District locations missing.");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentsInChildren<TextMesh>(true).Length > 0)
                    errors.Add("World labels must use opaque HDRP sign surfaces, never depth-ignoring default TextMesh materials.");
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0) errors.Add("Missing script on " + child.name);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        if (material == null || material.shader == null) errors.Add("Missing material/shader on " + renderer.name);
            }
            Physics.SyncTransforms();
            // Sample the real floor and full standing capsule along every connection, including the hall's two doors.
            int samples = 0;
            for (int i = 1; i < WalkRoute.Length; i++)
            {
                Vector3 a = WalkRoute[i - 1], b = WalkRoute[i];
                int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / .65f);
                for (int s = 0; s <= steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)Mathf.Max(1, steps));
                    if (!Physics.Raycast(p + Vector3.up * 2, Vector3.down, out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore))
                    { errors.Add("Route has no floor at " + p); break; }
                    foreach (Collider c in Physics.OverlapCapsule(p + Vector3.up * .51f, p + Vector3.up * 1.4f, .46f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (player != null && c.transform.IsChildOf(player.transform)) continue;
                        // Encounter actors are dynamic and are tested by the encounter validator.
                        if (c.GetComponentInParent<Greyline.Enemies.CombatHealth>() != null) continue;
                        errors.Add("Route obstructed by " + c.name + " at " + p);
                        break;
                    }
                    samples++;
                }
            }
            if (errors.Count > 0)
            {
                foreach (string error in errors) Debug.LogError("[ProductionDistrict] " + error);
                throw new InvalidOperationException("Production district validation failed with " + errors.Count + " errors.");
            }
            Debug.Log($"PRODUCTION_DISTRICT_VALIDATION_OK locations=6 connected_capsule_samples={samples} scene={ScenePath}");
        }

        private static void GroundAndRoutes(Transform root)
        {
            Transform streets = Group(root, "Streets and Connections");
            Block(streets, "District Ground", new(0,-.25f,3), new(166,.5f,166), "paving");
            Block(streets, "Boulevard Asphalt", new(0,.025f,-13), new(24,.05f,130), "asphalt");
            Block(streets, "Cross Street South", new(0,.03f,-29), new(150,.06f,11), "asphalt");
            Block(streets, "Market University Connection", new(35,.03f,25), new(80,.06f,10), "asphalt");
            Block(streets, "Eastern Service Lane", new(63,.035f,15), new(10,.07f,102), "asphalt");
            Block(streets, "Northern Escape Lane", new(40,.04f,62), new(70,.08f,10), "asphalt");
            for (int side = -1; side <= 1; side += 2)
                Block(streets, "Boulevard Promenade " + side, new(side*15.5f,.06f,-13), new(7,.12f,130), "stone");
            for (int i = 0; i < 18; i++)
                Detail(new(0,.067f,-70+i*6), new(.16f,.012f,2.7f), "paint");
            for (int i = 0; i < 7; i++)
            {
                Detail(new(-9+i*3,.073f,-21), new(1.3f,.012f,3.5f), "paint");
                Detail(new(-9+i*3,.14f,33), new(1.3f,.012f,3.5f), "paint");
            }
            // Architectural edges give the playable field an authored perimeter without open ground beyond it.
            Block(streets, "West Boundary", new(-82,1.7f,3), new(1,3.4f,166), "concrete");
            Block(streets, "East Boundary", new(82,1.7f,3), new(1,3.4f,166), "concrete");
            Block(streets, "South Boundary", new(0,1.7f,-79), new(164,3.4f,1), "concrete");
            Block(streets, "North Boundary", new(0,1.7f,85), new(164,3.4f,1), "concrete");
        }

        private static void Boulevard(Transform root)
        {
            Transform boulevard = Group(root, "01 Boulevard Arrival");
            Building(boulevard, "West Housing 01", new(-51,0,-57), new(40,23,29), "concrete", 7);
            Building(boulevard, "West Housing 02", new(-60,0,-7), new(30,29,27), "civic", 9);
            Building(boulevard, "East Housing 01", new(37,0,-54), new(25,19,27), "civic", 6);
            Building(boulevard, "East Housing 02", new(66,0,-53), new(24,26,29), "concrete", 8);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 6; i++)
                {
                    float z = -64+i*18;
                    Lamp(boulevard, new(side*17.6f,0,z));
                    if (i != 4) Planter(boulevard, new(side*20.5f,0,z+4), new(2.3f,.6f,5));
                }
                Sign(boulevard, new(side*23f,4.6f,-40), new(7,1.3f,.14f), side < 0 ? "RYUMYONG  /  UNIVERSITY" : "MARKET  /  REPAIR HALL", 0);
            }
            Transform bus = Group(boulevard, "Bus Shelter");
            Block(bus,"Shelter Roof",new(-17,3,-49),new(4,.25f,9),"red");
            Block(bus,"Shelter Back",new(-18.9f,1.4f,-49),new(.15f,2.8f,9),"glass");
            Block(bus,"Shelter Bench",new(-18, .48f,-49),new(.7f,.12f,6),"wood");
            foreach (float z in new[]{-53f,-45f}) Block(bus,"Shelter Post",new(-15.1f,1.5f,z),new(.1f,3,.1f),"metal");
            Sign(boulevard,new(-17,2.7f,-44.9f),new(3.6f,.55f,.12f),"07   CENTRAL DISTRICT",0);
            Vantage("01_BoulevardArrival", new(0,3,-68), new(0,12,65));
        }

        private static void University(Transform root)
        {
            Transform university = Group(root, "02 University Forecourt");
            Block(university,"University Forecourt",new(-43,.075f,23),new(46,.15f,31),"stone");
            Building(university,"University Main Hall",new(-45,0,48),new(50,17,16),"stone",4);
            Building(university,"University West Wing",new(-73,0,32),new(10,12,33),"civic",3);
            for (int i=0;i<7;i++) Block(university,"Main Hall Colonnade "+i,new(-65+i*6.5f,5,38.3f),new(1,10,1),"stone");
            Block(university,"Main Hall Pediment",new(-45,10.5f,38.3f),new(47,1,3),"stone");
            Sign(university,new(-45,12.8f,39.75f),new(29,1.7f,.15f),"RYUMYONG  TECHNICAL  UNIVERSITY",0);
            Block(university,"University Red Emblem",new(-45,15.7f,39.7f),new(2.1f,2.1f,.18f),"red").transform.rotation=Quaternion.Euler(0,0,45);
            // Gate remains wide enough for simultaneous approach and a readable many-versus-one pocket.
            foreach(float x in new[]{-62f,-25f})
            {
                Block(university,"University Gate Pier",new(x,2.8f,9),new(1.4f,5.6f,1.4f),"stone");
                Planter(university,new(x,0,27),new(3,.6f,6));
                Bench(university,new(x,0,17));
            }
            Sign(university,new(-43,5.8f,9),new(20,.7f,.18f),"LEARNING  /  PRACTICE  /  PROGRESS",0);
            Vantage("02_UniversityForecourt",new(-38,2.5f,13),new(-45,8,46));
        }

        private static void Market(Transform root)
        {
            Transform market=Group(root,"03 Market and Alley Network");
            Block(market,"Market Court",new(38,.045f,3),new(30,.09f,25),"paving");
            Building(market,"Market West Row",new(24,0,-8),new(8,8,21),"concrete",2);
            Building(market,"Market East Row",new(53,0,-9),new(8,9,22),"civic",2);
            Building(market,"Market North Row",new(38,0,18),new(24,9,6),"concrete",2);
            for(int i=0;i<3;i++)
            {
                Stall(market,new(32+i*6,0,-7),i%2==0?"red":"green");
                Stall(market,new(31+i*7,0,12),i%2==0?"green":"red");
            }
            // Southern lane and both side alleys remain connected around the court's stalls.
            for(int i=0;i<4;i++)
            {
                Crate(market,new(29+i*4,0,-18),i%2==0?2:1);
                Detail(new(28+i*7,3.8f,6),new(4.5f,.06f,.6f),i%2==0?"red":"paint");
            }
            Sign(market,new(38,4.7f,-21),new(18,1.2f,.16f),"RYUMYONG  MARKET",0);
            foreach(float x in new[]{28f,48f}) Block(market,"Market Entry Pole",new(x,2.5f,-21),new(.14f,5,.14f),"metal");
            Sign(market,new(38,5.6f,14.9f),new(18,.8f,.13f),"DAILY GOODS    /    FRESH PRODUCE",0);
            Sign(market,new(57.1f,3.3f,-10),new(9,.85f,.13f),"REPAIRS   →   NORTH LANE",90);
            Vantage("03_MarketPocket",new(38,2.7f,-2),new(38,3,16));
        }

        private static void Plaza(Transform root)
        {
            Transform plaza=Group(root,"04 Civic Plaza and Landmark");
            Block(plaza,"Civic Plaza",new(0,.08f,63),new(44,.16f,42),"stone");
            for(int i=0;i<8;i++)
            {
                Detail(new(-19+i*5.4f,.17f,63),new(.07f,.01f,38),"concrete");
                Detail(new(0,.17f,46+i*5.1f),new(40,.01f,.07f),"concrete");
            }
            Block(plaza,"Monument Lower Plinth",new(0,.6f,78),new(10,1.2f,10),"concrete");
            Block(plaza,"Monument Upper Plinth",new(0,1.5f,78),new(7,1,7),"stone");
            Block(plaza,"Monument Shaft",new(0,15,78),new(2.8f,26,2.8f),"stone");
            GameObject crown=Block(plaza,"Monument Crown",new(0,29,78),new(3.4f,3.4f,3.4f),"red");
            crown.transform.rotation=Quaternion.Euler(0,45,45);
            foreach(float x in new[]{-19f,19f})
                foreach(float z in new[]{48f,72f}) { Planter(plaza,new(x,0,z),new(2,.7f,5)); Lamp(plaza,new(x,0,z+4)); }
            Building(plaza,"Northwest Civic Tower",new(-50,0,73),new(41,25,18),"civic",7);
            Building(plaza,"Northeast Utility Block",new(50,0,78),new(48,15,11),"concrete",4);
            Sign(plaza,new(0,3,72.85f),new(6,.8f,.15f),"CIVIC  SQUARE",0);
            Vantage("04_PlazaLandmark",new(10,3,48),new(0,14,78));
        }

        private static void Interior(Transform root)
        {
            Transform hall=Group(root,"05 Repair Hall Interior");
            Block(hall,"Repair Hall Floor",new(63,.065f,42),new(20,.13f,24),"paving");
            Block(hall,"West Interior Wall",new(53,2.6f,42),new(.35f,5.2f,24),"concrete");
            Block(hall,"East Interior Wall",new(73,2.6f,42),new(.35f,5.2f,24),"concrete");
            foreach(float z in new[]{30f,54f})
            {
                Block(hall,"Doorway Left",new(56.5f,2.6f,z),new(7,5.2f,.35f),"concrete");
                Block(hall,"Doorway Right",new(69.5f,2.6f,z),new(7,5.2f,.35f),"concrete");
                Block(hall,"Doorway Header",new(63,4.25f,z),new(6,1.9f,.35f),"red");
            }
            Block(hall,"Repair Hall Roof West",new(57.5f,5.25f,42),new(9,.3f,24),"metal");
            Block(hall,"Repair Hall Roof East",new(68.5f,5.25f,42),new(9,.3f,24),"metal");
            // Open clerestory admits HDRP sky light while two opposing doors support escape play.
            foreach(float z in new[]{35f,42f,49f})
            {
                Bench(hall,new(55.5f,0,z));
                Crate(hall,new(70,0,z),2);
                Detail(new(63,5.1f,z),new(20,.15f,.16f),"red");
            }
            Sign(hall,new(63,3.8f,29.75f),new(14,.9f,.16f),"REPAIR  HALL   /   THROUGH  ROUTE",0);
            Sign(hall,new(63,3.2f,53.78f),new(5,.5f,.12f),"NORTH LANE  ↑",0);
            Lamp(hall,new(70,0,27));
            Lamp(hall,new(69,0,58));
            GameObject fill=new("Repair Hall Practical Light");fill.transform.SetParent(hall,false);fill.transform.position=new(63,4.2f,42);
            Light practical=fill.AddComponent<Light>();practical.type=LightType.Point;practical.color=new Color(1,.8f,.56f);practical.intensity=700;practical.range=17;
            fill.AddComponent<HDAdditionalLightData>();
            Vantage("05_RepairHall",new(63,2.4f,33),new(63,2.3f,52));
            Vantage("06_PursuitEscape",new(63,2.6f,62),new(26,2,62));
        }

        private static void BuildAnchors(Transform player)
        {
            Transform anchors=new GameObject("EncounterAnchors").transform;
            Anchor(anchors,"University",new(-43,.2f,18));
            Anchor(anchors,"Market",new(38,.2f,4));
            Anchor(anchors,"Plaza",new(0,.2f,53));
            Anchor(anchors,"Escape",new(63,.2f,62));
            Transform route=Group(anchors,"PursuitRoute");
            Anchor(route,"01_Market",new(38,.2f,4));
            Anchor(route,"02_EastAlley",new(63,.2f,4));
            Anchor(route,"03_HallEntry",new(63,.2f,24));
            Anchor(route,"04_HallInterior",new(63,.2f,42));
            Anchor(route,"05_Escape",new(63,.2f,62));
            var navigation=new GameObject("District Navigation").AddComponent<ProductionDistrictNavigation>();
            navigation.Configure(player,new[]
            {
                Place("REPAIR HALL",new(63,0,42),new(20,24),new(.42f,.48f,.36f)),
                Place("MARKET",new(38,0,0),new(32,30),new(.55f,.27f,.20f)),
                Place("UNIVERSITY",new(-43,0,25),new(48,33),new(.32f,.40f,.48f)),
                Place("CIVIC SQUARE",new(0,0,63),new(44,42),new(.55f,.47f,.31f)),
                Place("NORTH SERVICE LANE",new(54,0,62),new(45,10),new(.26f,.38f,.34f)),
                Place("RYUMYONG BOULEVARD",new(0,0,-15),new(36,126),new(.29f,.31f,.34f))
            });
        }

        private static ProductionDistrictNavigation.Place Place(string label,Vector3 center,Vector2 size,Color color) => new(){label=label,center=center,size=size,color=color};

        private static void Lighting()
        {
            GameObject sun=new("Key Light");
            Light light=sun.AddComponent<Light>();
            light.type=LightType.Directional;
            light.color=new Color(1,.9f,.76f);
            light.intensity=28000;
            light.shadows=LightShadows.Soft;
            sun.transform.rotation=Quaternion.Euler(42,-28,0);
            sun.AddComponent<HDAdditionalLightData>();
            var sky=new GameObject("Sky and Fog").AddComponent<Volume>();
            sky.isGlobal=true;
            sky.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SkyandFogSettingsProfile.asset");
            string path=ArtFolder+"/DistrictGrade.asset";
            VolumeProfile profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
            Exposure exposure=Component<Exposure>(profile); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(11.1f);
            // A district-owned HDRP sky supplies both the backdrop and dynamic ambient fill.
            // Its physical brightness is matched to the fixed exposure, keeping shaded facades readable.
            VisualEnvironment environment=Component<VisualEnvironment>(profile);
            environment.skyType.Override((int)SkyType.Gradient);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            GradientSky gradient=Component<GradientSky>(profile);
            gradient.top.Override(new Color(.18f,.34f,.52f));
            gradient.middle.Override(new Color(.56f,.66f,.69f));
            gradient.bottom.Override(new Color(.22f,.25f,.25f));
            gradient.gradientDiffusion.Override(1.1f);
            gradient.skyIntensityMode.Override(SkyIntensityMode.Exposure);
            gradient.exposure.Override(11.5f);
            gradient.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            Tonemapping tone=Component<Tonemapping>(profile); tone.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments color=Component<ColorAdjustments>(profile); color.saturation.Override(-3); color.contrast.Override(5);
            WhiteBalance white=Component<WhiteBalance>(profile); white.temperature.Override(-5);
            Bloom bloom=Component<Bloom>(profile); bloom.intensity.Override(.09f);
            HDShadowSettings shadows=Component<HDShadowSettings>(profile); shadows.maxShadowDistance.Override(130);
            var grade=new GameObject("District Grade").AddComponent<Volume>();grade.isGlobal=true;grade.priority=5;grade.sharedProfile=profile;
            EditorUtility.SetDirty(profile);
        }

        private static T Component<T>(VolumeProfile profile) where T:VolumeComponent
        {
            if(profile.TryGet(out T c)) return c;
            c=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(c,profile);return c;
        }

        private static void Building(Transform root,string name,Vector3 p,Vector3 size,string material,int floors)
        {
            Transform building=Group(root,name);
            Block(building,"Structure",p+Vector3.up*size.y*.5f,size,material);
            Block(building,"Cornice",p+Vector3.up*(size.y+.25f),new(size.x+.5f,.5f,size.z+.5f),"stone");
            Detail(p+new Vector3(0,.5f,-size.z*.5f-.06f),new(size.x,1,.16f),"stone");
            int windows=Mathf.FloorToInt(size.x/3.6f);
            for(int f=0;f<floors;f++)
            {
                float y=2.4f+f*(size.y-3)/Mathf.Max(1,floors-1);
                for(int w=0;w<windows;w++)
                {
                    float x=-size.x*.5f+(w+.5f)*size.x/windows;
                    foreach(int side in new[]{-1,1})
                    {
                        Vector3 wp=p+new Vector3(x,y,side*(size.z*.5f+.045f));
                        Detail(wp,new(1.55f,1.8f,.09f),"glass");
                        Detail(wp+new Vector3(0,-1,side*.07f),new(1.85f,.16f,.24f),"stone");
                        Detail(wp+new Vector3(0,0,side*.06f),new(.07f,1.8f,.12f),"metal");
                    }
                }
            }
            for(int w=0;w<Mathf.FloorToInt(size.z/4);w++)
                for(int f=0;f<floors;f++)
                    foreach(int side in new[]{-1,1})
                        Detail(p+new Vector3(side*(size.x*.5f+.045f),2.4f+f*(size.y-3)/Mathf.Max(1,floors-1),-size.z*.5f+2+w*4),new(.09f,1.8f,1.55f),"glass");
        }

        private static void Stall(Transform root,Vector3 p,string material)
        {
            Block(root,"Market Counter",p+new Vector3(0,.65f,0),new(4,1.3f,1.4f),"wood");
            Block(root,"Market Awning",p+new Vector3(0,2.9f,0),new(4.8f,.12f,2.8f),material);
            foreach(float x in new[]{-2.1f,2.1f}) Detail(p+new Vector3(x,1.4f,0),new(.09f,2.8f,.09f),"metal");
            for(int i=0;i<4;i++) Detail(p+new Vector3(-1.5f+i,1.45f,0),new(.65f,.3f,.7f),i%2==0?"green":"red");
        }

        private static void Planter(Transform root,Vector3 p,Vector3 size)
        {
            Block(root,"Stone Planter",p+Vector3.up*size.y*.5f,size,"concrete");
            Detail(p+Vector3.up*(size.y+.45f),new(size.x*.9f,.9f,size.z*.92f),"green");
            Detail(p+Vector3.up*2.1f,new(.2f,2.5f,.2f),"wood");
            Detail(p+Vector3.up*3.4f,new(2.8f,2.3f,2.8f),"green");
        }
        private static void Bench(Transform root,Vector3 p)
        {
            Block(root,"Bench Seat",p+Vector3.up*.55f,new(3,.17f,.8f),"wood");
            Detail(p+new Vector3(0,.95f,.32f),new(3,.7f,.15f),"wood");
            foreach(float x in new[]{-1.2f,1.2f})Detail(p+new Vector3(x,.27f,0),new(.18f,.54f,.65f),"metal");
        }
        private static void Crate(Transform root,Vector3 p,int count)
        {
            for(int i=0;i<count;i++)
            {
                Block(root,"Stacked Crate",p+Vector3.up*(.45f+i*.92f),new(1.1f,.9f,.9f),"wood");
                Detail(p+new Vector3(0,.45f+i*.92f,-.46f),new(.1f,.9f,.03f),"metal");
            }
        }
        private static void Lamp(Transform root,Vector3 p)
        {
            Block(root,"Street Lamp Post",p+Vector3.up*3.4f,new(.13f,6.8f,.13f),"metal");
            Detail(p+new Vector3(-.7f,6.7f,0),new(1.6f,.11f,.12f),"metal");
            Detail(p+new Vector3(-1.35f,6.58f,0),new(.9f,.17f,.45f),"paint");
        }

        private static void Sign(Transform root,Vector3 p,Vector3 size,string text,float yaw)
        {
            GameObject panel=Block(root,"Sign "+text,p,size,"red",false);panel.transform.rotation=Quaternion.Euler(0,yaw,0);
            // GUI/Text shaders ignore ordinary scene depth. Sign lettering is instead a bounded
            // opaque HDRP surface, authored from original glyph pixels and occluded by buildings.
            GameObject face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Opaque Lettered Sign Face";
            face.transform.SetParent(root,false);
            face.transform.SetPositionAndRotation(p+Quaternion.Euler(0,yaw,0)*new Vector3(0,0,-size.z*.5f-.012f),Quaternion.Euler(0,yaw,0));
            face.transform.localScale=new Vector3(size.x*.97f,size.y*.94f,1);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.GetComponent<MeshRenderer>().sharedMaterial=SignMaterial(text,size.x/size.y);
            face.isStatic=true;
        }

        private static Material SignMaterial(string text,float aspect)
        {
            uint hash=2166136261;
            unchecked { foreach(char c in text) { hash^=c;hash*=16777619; } }
            string key="Sign_"+hash.ToString("X8");
            string texturePath=ArtFolder+"/"+key+".asset";
            const int width=1024;
            int height=Mathf.Clamp(Mathf.RoundToInt(width/aspect),64,384);
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null)
            {
                texture=new Texture2D(width,height,TextureFormat.RGBA32,true){name=key};
                AssetDatabase.CreateAsset(texture,texturePath);
            }
            else if(texture.width!=width||texture.height!=height)texture.Reinitialize(width,height,TextureFormat.RGBA32,true);
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;texture.anisoLevel=4;
            Color32 background=new(111,29,24,255),foreground=new(233,221,186,255);
            Color32[] pixels=new Color32[width*height];
            for(int i=0;i<pixels.Length;i++)pixels[i]=background;
            int scale=Mathf.Max(1,Mathf.Min((width-48)/Mathf.Max(1,text.Length*6-1),(height-20)/7));
            int startX=(width-(text.Length*6-1)*scale)/2,startY=(height-7*scale)/2;
            for(int letter=0;letter<text.Length;letter++)
            {
                string[] rows=Glyph(text[letter]).Split('/');
                for(int y=0;y<7;y++)for(int x=0;x<5;x++)
                    if(rows[y][x]=='1')
                        for(int py=0;py<scale;py++)for(int px=0;px<scale;px++)
                            pixels[(startY+(6-y)*scale+py)*width+startX+(letter*6+x)*scale+px]=foreground;
            }
            for(int x=8;x<width-8;x++)for(int thickness=0;thickness<2;thickness++)
            {pixels[(5+thickness)*width+x]=foreground;pixels[(height-6-thickness)*width+x]=foreground;}
            texture.SetPixels32(pixels);texture.Apply();EditorUtility.SetDirty(texture);
            string materialPath=ArtFolder+"/"+key+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader=Shader.Find("HDRP/Unlit");
            if(shader==null)throw new InvalidOperationException("HDRP/Unlit is required for depth-tested district signs.");
            if(material==null){material=new Material(shader){name=key};AssetDatabase.CreateAsset(material,materialPath);}
            material.shader=shader;material.SetColor("_UnlitColor",Color.white);material.SetTexture("_UnlitColorMap",texture);
            material.SetFloat("_SurfaceType",0);material.SetFloat("_ZWrite",1);
            material.SetFloat("_DoubleSidedEnable",1);material.SetFloat("_CullMode",0);material.SetFloat("_OpaqueCullMode",0);
            material.SetFloat("_ZTestDepthEqualForOpaque",(float)CompareFunction.LessEqual);
            material.renderQueue=(int)RenderQueue.Geometry;
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);return material;
        }

        // Original five-by-seven monospaced sign glyphs. No OS font, atlas or external asset dependency.
        private static string Glyph(char c) => c switch
        {
            'A'=>"01110/10001/10001/11111/10001/10001/10001", 'B'=>"11110/10001/10001/11110/10001/10001/11110",
            'C'=>"01111/10000/10000/10000/10000/10000/01111", 'D'=>"11110/10001/10001/10001/10001/10001/11110",
            'E'=>"11111/10000/10000/11110/10000/10000/11111", 'F'=>"11111/10000/10000/11110/10000/10000/10000",
            'G'=>"01111/10000/10000/10111/10001/10001/01111", 'H'=>"10001/10001/10001/11111/10001/10001/10001",
            'I'=>"11111/00100/00100/00100/00100/00100/11111", 'J'=>"00111/00010/00010/00010/00010/10010/01100",
            'K'=>"10001/10010/10100/11000/10100/10010/10001", 'L'=>"10000/10000/10000/10000/10000/10000/11111",
            'M'=>"10001/11011/10101/10101/10001/10001/10001", 'N'=>"10001/11001/10101/10011/10001/10001/10001",
            'O'=>"01110/10001/10001/10001/10001/10001/01110", 'P'=>"11110/10001/10001/11110/10000/10000/10000",
            'Q'=>"01110/10001/10001/10001/10101/10010/01101", 'R'=>"11110/10001/10001/11110/10100/10010/10001",
            'S'=>"01111/10000/10000/01110/00001/00001/11110", 'T'=>"11111/00100/00100/00100/00100/00100/00100",
            'U'=>"10001/10001/10001/10001/10001/10001/01110", 'V'=>"10001/10001/10001/10001/10001/01010/00100",
            'W'=>"10001/10001/10001/10101/10101/11011/10001", 'X'=>"10001/10001/01010/00100/01010/10001/10001",
            'Y'=>"10001/10001/01010/00100/00100/00100/00100", 'Z'=>"11111/00001/00010/00100/01000/10000/11111",
            '0'=>"01110/10001/10011/10101/11001/10001/01110", '1'=>"00100/01100/00100/00100/00100/00100/01110",
            '2'=>"01110/10001/00001/00010/00100/01000/11111", '3'=>"11110/00001/00001/01110/00001/00001/11110",
            '4'=>"00010/00110/01010/10010/11111/00010/00010", '5'=>"11111/10000/10000/11110/00001/00001/11110",
            '6'=>"01110/10000/10000/11110/10001/10001/01110", '7'=>"11111/00001/00010/00100/01000/01000/01000",
            '8'=>"01110/10001/10001/01110/10001/10001/01110", '9'=>"01110/10001/10001/01111/00001/00001/01110",
            '/'=>"00001/00001/00010/00100/01000/10000/10000", '→'=>"00000/00100/00010/11111/00010/00100/00000",
            '↑'=>"00100/01110/10101/00100/00100/00100/00100", '-'=>"00000/00000/00000/11111/00000/00000/00000",
            _=>"00000/00000/00000/00000/00000/00000/00000"
        };

        private static Transform Group(Transform root,string name){GameObject g=new(name);g.transform.SetParent(root,false);return g.transform;}
        private static void Anchor(Transform root,string name,Vector3 p){Transform t=Group(root,name);t.position=p;}
        private static void Vantage(string name,Vector3 p,Vector3 target)
        {
            GameObject root=GameObject.Find("QA Vantage Points")??new GameObject("QA Vantage Points");
            Transform t=Group(root.transform,name);t.position=p;t.rotation=Quaternion.LookRotation(target-p);
        }
        private static GameObject Block(Transform root,string name,Vector3 p,Vector3 size,string material,bool collision=true)
        {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.position=p;g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=Materials[material];
            if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic=true;return g;
        }

        // Repeated windows, trims, foliage and small props share one renderer per material.
        private static void Detail(Vector3 p,Vector3 size,string key)
        {
            if(cubeMesh==null)
            {
                GameObject cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cubeMesh=cube.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(cube);
            }
            Material material=Materials[key];
            if(!Decoration.TryGetValue(material,out var list)){list=new();Decoration.Add(material,list);}
            list.Add(new CombineInstance{mesh=cubeMesh,transform=Matrix4x4.TRS(p,Quaternion.identity,size)});
        }
        private static void FlushDecoration(Transform root)
        {
            Transform details=Group(root,"Batched Original Details");
            foreach(var pair in Decoration)
            {
                string path=ArtFolder+"/Details_"+pair.Key.name+".asset";
                Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
                mesh.name="District "+pair.Key.name+" Details";mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(pair.Value.ToArray(),true,true);
                EditorUtility.SetDirty(mesh);
                GameObject g=new(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(details,false);
                g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;g.isStatic=true;
            }
        }

        private static void PrepareMaterials()
        {
            Materials.Clear();
            string texturePath=ArtFolder+"/OriginalConcreteMottle.asset";
            surfaceTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(surfaceTexture==null)
            {
                surfaceTexture=new Texture2D(128,128,TextureFormat.RGB24,true){name="Original Concrete Mottle",wrapMode=TextureWrapMode.Repeat};
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {
                    float m=.84f+Mathf.PerlinNoise(x*.073f,y*.073f)*.13f+Mathf.PerlinNoise(x*.71f+23,y*.71f+47)*.03f;
                    surfaceTexture.SetPixel(x,y,new Color(m,m,m));
                }
                surfaceTexture.Apply();AssetDatabase.CreateAsset(surfaceTexture,texturePath);
            }
            MakeMaterial("concrete",new(.40f,.43f,.43f),.26f);
            MakeMaterial("stone",new(.64f,.62f,.53f),.3f);
            MakeMaterial("paving",new(.38f,.39f,.36f),.22f);
            MakeMaterial("asphalt",new(.095f,.105f,.11f),.17f);
            MakeMaterial("civic",new(.28f,.38f,.42f),.28f);
            MakeMaterial("glass",new(.075f,.18f,.23f),.65f);
            MakeMaterial("metal",new(.08f,.1f,.11f),.48f);
            MakeMaterial("paint",new(.8f,.74f,.58f),.25f);
            MakeMaterial("red",new(.51f,.1f,.075f),.24f);
            MakeMaterial("green",new(.22f,.31f,.17f),.12f);
            MakeMaterial("wood",new(.36f,.25f,.15f),.22f);
        }
        private static void MakeMaterial(string name,Color color,float smoothness)
        {
            string path=ArtFolder+"/District_"+name+".mat";
            Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("HDRP/Lit")){name="District_"+name};AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);
            if(name!="glass"&&name!="metal")m.SetTexture("_BaseColorMap",surfaceTexture);
            EditorUtility.SetDirty(m);Materials[name]=m;
        }
        private static void EnsureFolder(string path)
        {
            string[] parts=path.Split('/');string current=parts[0];
            for(int i=1;i<parts.Length;i++){if(!AssetDatabase.IsValidFolder(current+"/"+parts[i]))AssetDatabase.CreateFolder(current,parts[i]);current+="/"+parts[i];}
        }
        private static GameObject FindRoot(Scene scene,string name)
        {foreach(GameObject root in scene.GetRootGameObjects())if(root.name==name)return root;return null;}
    }
}
