#if UNITY_EDITOR
using System.IO;
using SignalHaul;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalHaul.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string MaterialsFolder = "Assets/Materials";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string PlayerPrefabPath = PrefabsFolder + "/NetworkPlayer.prefab";

        static ProjectSetup()
        {
            EditorApplication.delayCall += EnsureMainScene;
        }

        [MenuItem("Tools/Signal Haul/Rebuild Main Scene")]
        public static void RebuildMainScene()
        {
            if (Application.isPlaying)
                return;

            var scene = OpenMainSceneSingle();
            BuildScene(scene);
        }

        private static void EnsureMainScene()
        {
            if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            Directory.CreateDirectory("Assets/Scenes");

            bool wasLoaded = false;
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                wasLoaded = true;
            }
            else if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var marker = FindMarker(scene);
            bool needsBuild = marker == null || marker.SceneVersion != SignalHaulSceneMarker.CurrentVersion;

            if (needsBuild)
            {
                SceneManager.SetActiveScene(scene);
                BuildScene(scene);
                return;
            }

            EnsureBuildSettings();

            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static Scene OpenMainSceneSingle()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (File.Exists(ScenePath))
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        private static SignalHaulSceneMarker FindMarker(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                var marker = root.GetComponentInChildren<SignalHaulSceneMarker>(true);
                if (marker != null)
                    return marker;
            }

            return null;
        }

        private static void BuildScene(Scene scene)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
                Object.DestroyImmediate(rootObject);

            var dark = GetOrCreateMaterial("Dark", new Color(.035f, .045f, .06f), .1f, .15f);
            var steel = GetOrCreateMaterial("Steel", new Color(.16f, .2f, .23f), .65f, .35f);
            var deck = GetOrCreateMaterial("Deck", new Color(.25f, .29f, .3f), .45f, .3f);
            var hazard = GetOrCreateMaterial("Hazard", new Color(.95f, .55f, .08f), .15f, .3f);
            var cyan = GetOrCreateMaterial("SignalCyan", new Color(.05f, .8f, 1f), .25f, .65f);
            var red = GetOrCreateMaterial("DroneRed", new Color(.85f, .08f, .08f), .25f, .4f);
            var playerMaterial = GetOrCreateMaterial("Player", new Color(.16f, .52f, .95f), .1f, .35f);
            var cacheMaterial = GetOrCreateMaterial("LootDataCache", new Color(.12f, .75f, .42f), .15f, .3f);
            var batteryMaterial = GetOrCreateMaterial("LootBattery", new Color(.95f, .72f, .08f), .4f, .25f);
            var glassMaterial = GetOrCreateMaterial("LootGlassRelic", new Color(.72f, .18f, .95f), .05f, .8f);
            var reactorMaterial = GetOrCreateMaterial("LootReactor", new Color(.45f, .18f, .75f), .75f, .4f);
            var stalkerMaterial = GetOrCreateMaterial("MonsterStalker", new Color(.16f, .04f, .24f), .1f, .2f);
            var bruteMaterial = GetOrCreateMaterial("MonsterBrute", new Color(.72f, .12f, .05f), .2f, .16f);
            var scavengerMaterial = GetOrCreateMaterial("MonsterScavenger", new Color(.08f, .52f, .25f), .15f, .28f);
            GameObject playerPrefab = CreatePlayerPrefab(playerMaterial);

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.06f, .09f, .12f);
            RenderSettings.fogDensity = .018f;
            RenderSettings.ambientLight = new Color(.18f, .22f, .28f);

            var root = CreateEmpty("SIGNAL_HAUL", null);
            var marker = root.AddComponent<SignalHaulSceneMarker>();
            marker.InitializeVersion();

            var managers = CreateEmpty("Managers", root.transform);
            var lighting = CreateEmpty("Lighting", root.transform);
            var environment = CreateEmpty("Environment", root.transform);
            var gameplay = CreateEmpty("Gameplay", root.transform);
            var hazards = CreateEmpty("Hazards", root.transform);

            CreateLighting(lighting.transform);
            CreateEnvironment(environment.transform, dark, steel, deck, hazard);

            Transform[] spawnPoints = CreateSpawnPoints(gameplay.transform);
            CreateExtraction(gameplay.transform, new Vector3(4.5f, .55f, -4.5f), cyan);
            CreateCores(gameplay.transform, cyan);
            CreateSalvageLoot(gameplay.transform, cacheMaterial, batteryMaterial, glassMaterial, reactorMaterial);
            CreateDrones(hazards.transform, red);
            CreateMonsters(hazards.transform, stalkerMaterial, bruteMaterial, scavengerMaterial);

            var gameManagerObject = CreateEmpty("GameManager", managers.transform);
            gameManagerObject.AddComponent<NetworkObject>();
            var gameManager = gameManagerObject.AddComponent<GameManager>();
            gameManager.Configure(3, 240f);

            // NetworkManager itself must be a scene-root GameObject in NGO.
            CreateNetworkManager(null, playerPrefab, spawnPoints);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            Debug.Log("SIGNAL HAUL: Main scene rebuilt with multiplayer, random maps, voice, breakable salvage, and server-authoritative monsters.");
        }

        private static void CreateNetworkManager(Transform parent, GameObject playerPrefab, Transform[] spawnPoints)
        {
            var networkObject = CreateEmpty("Network Manager", parent);
            var transport = networkObject.AddComponent<UnityTransport>();
            var networkManager = networkObject.AddComponent<NetworkManager>();

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.EnableSceneManagement = true;
            networkManager.NetworkConfig.TickRate = 30;

            var session = networkObject.AddComponent<NetworkSessionManager>();
            session.Configure(networkManager, transport, spawnPoints, 4, 7777);

            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(session);
        }

        private static Transform[] CreateSpawnPoints(Transform parent)
        {
            var container = CreateEmpty("Player Spawn Points", parent);
            Vector3[] positions =
            {
                new Vector3(-4.5f, 1.15f, -4.5f),
                new Vector3(-4.5f, 1.15f, 4.5f),
                new Vector3(4.5f, 1.15f, 4.5f),
                new Vector3(0f, 1.15f, -4.5f)
            };

            var result = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var spawn = CreateEmpty($"Spawn {i + 1}", container.transform);
                spawn.transform.position = positions[i];
                result[i] = spawn.transform;
            }

            return result;
        }

        private static GameObject CreatePlayerPrefab(Material playerMaterial)
        {
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
                AssetDatabase.DeleteAsset(PlayerPrefabPath);

            var playerObject = new GameObject("NetworkPlayer");
            playerObject.AddComponent<NetworkObject>();

            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = .35f;
            characterController.center = new Vector3(0f, .9f, 0f);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Body";
            visual.transform.SetParent(playerObject.transform, false);
            visual.transform.localPosition = new Vector3(0f, .9f, 0f);
            visual.transform.localScale = new Vector3(.65f, .9f, .65f);
            visual.GetComponent<Renderer>().sharedMaterial = playerMaterial;
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var cameraObject = CreateEmpty("Camera", playerObject.transform);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 78f;
            camera.enabled = false;
            var listener = cameraObject.AddComponent<AudioListener>();
            listener.enabled = false;

            var holdPoint = CreateEmpty("HoldPoint", cameraObject.transform);
            holdPoint.transform.localPosition = new Vector3(0f, -.08f, 2.35f);

            var controller = playerObject.AddComponent<PlayerController>();
            controller.Configure(camera, holdPoint.transform);
            playerObject.AddComponent<ProximityVoiceChat>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(playerObject, PlayerPrefabPath);
            Object.DestroyImmediate(playerObject);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static void CreateLighting(Transform parent)
        {
            var lightObject = CreateEmpty("Storm Light", parent);
            lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(.65f, .75f, .95f);
        }

        private static void CreateEnvironment(Transform parent, Material dark, Material steel, Material deck, Material hazard)
        {
            CreatePrimitive(PrimitiveType.Cube, "Ground", parent, new Vector3(0f, -1f, 0f), new Vector3(42f, 2f, 42f), dark);
            CreatePrimitive(PrimitiveType.Cube, "Base Deck", parent, new Vector3(0f, .2f, 0f), new Vector3(13f, .4f, 13f), deck);

            var tower = CreateEmpty("Tower", parent);
            const int levels = 10;

            for (int i = 0; i < levels; i++)
            {
                float y = 2.2f + i * 3.1f;
                float x = i % 2 == 0 ? -2.8f : 2.8f;
                float z = (i / 2) % 2 == 0 ? 1.8f : -1.8f;
                float bridgeX = i % 2 == 0 ? 1.1f : -1.1f;

                var level = CreateEmpty($"Level_{i:00}", tower.transform);
                CreatePrimitive(PrimitiveType.Cube, "Deck", level.transform, new Vector3(x, y, z), new Vector3(6.2f, .35f, 5.3f), deck);
                CreatePrimitive(PrimitiveType.Cube, "Hazard Bridge", level.transform, new Vector3(bridgeX, y + 1.45f, z * .45f), new Vector3(2.1f, .28f, 2.8f), hazard);
                CreatePrimitive(PrimitiveType.Cube, "Climb Wall", level.transform, new Vector3(0f, y + 1.5f, z), new Vector3(.45f, 3f, 4.6f), steel);

                if (i % 2 == 0)
                {
                    var rails = CreateEmpty("Rails", level.transform);
                    CreatePrimitive(PrimitiveType.Cube, "Rail Left", rails.transform, new Vector3(x - 3f, y + .65f, z), new Vector3(.15f, 1.3f, 5.4f), steel);
                    CreatePrimitive(PrimitiveType.Cube, "Rail Right", rails.transform, new Vector3(x + 3f, y + .65f, z), new Vector3(.15f, 1.3f, 5.4f), steel);
                }
            }
        }

        private static void CreateExtraction(Transform gameplayParent, Vector3 position, Material material)
        {
            var objectRoot = CreateEmpty("Extraction", gameplayParent);
            var pad = CreatePrimitive(PrimitiveType.Cylinder, "Extraction Pad", objectRoot.transform, position, new Vector3(2.8f, .15f, 2.8f), material);

            var collider = pad.GetComponent<Collider>();
            collider.isTrigger = true;

            var rigidbody = pad.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            pad.AddComponent<ExtractionPad>();
        }

        private static void CreateCores(Transform gameplayParent, Material material)
        {
            var cores = CreateEmpty("Signal Cores", gameplayParent);
            CreateCore(cores.transform, "CORE-ALPHA", new Vector3(-2.8f, 15.2f, 1.8f), material);
            CreateCore(cores.transform, "CORE-BETA", new Vector3(2.8f, 24.5f, -1.8f), material);
            CreateCore(cores.transform, "CORE-GAMMA", new Vector3(-2.8f, 33.8f, 1.8f), material);
        }

        private static void CreateCore(Transform parent, string name, Vector3 position, Material material)
        {
            var core = CreatePrimitive(PrimitiveType.Sphere, name, parent, position, Vector3.one * .75f, material);
            core.AddComponent<NetworkObject>();

            var rigidbody = core.AddComponent<Rigidbody>();
            rigidbody.mass = 5f;
            rigidbody.linearDamping = .35f;
            rigidbody.angularDamping = .2f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            core.AddComponent<SignalCore>();
            PhysicsLoot loot = core.GetComponent<PhysicsLoot>();
            loot.Configure("SIGNAL CORE", 1000, 5f, 120f, 7.5f, 1.1f, 1, 1, true);
        }

        private static void CreateSalvageLoot(Transform gameplayParent, Material cache, Material battery, Material glass, Material reactor)
        {
            var lootRoot = CreateEmpty("Salvage Loot", gameplayParent);

            CreateLoot(
                PrimitiveType.Cube,
                lootRoot.transform,
                "DATA CACHE",
                new Vector3(-2.2f, 1.15f, -1.8f),
                new Vector3(.9f, .65f, .65f),
                cache,
                220,
                4f,
                65f,
                6.5f,
                1.4f,
                1,
                1);

            CreateLoot(
                PrimitiveType.Cube,
                lootRoot.transform,
                "INDUSTRIAL BATTERY",
                new Vector3(2.1f, 1.25f, 1.7f),
                new Vector3(1.15f, .72f, .72f),
                battery,
                420,
                14f,
                120f,
                8.5f,
                1f,
                1,
                1);

            CreateLoot(
                PrimitiveType.Sphere,
                lootRoot.transform,
                "GLASS RELIC",
                new Vector3(-1.8f, 1.25f, 2.3f),
                Vector3.one * .72f,
                glass,
                700,
                3.5f,
                35f,
                3.5f,
                2.4f,
                1,
                1);

            CreateLoot(
                PrimitiveType.Cylinder,
                lootRoot.transform,
                "REACTOR ASSEMBLY",
                new Vector3(2.5f, 1.3f, -1.8f),
                new Vector3(1.15f, .75f, 1.15f),
                reactor,
                1100,
                30f,
                180f,
                9.5f,
                .9f,
                2,
                2);
        }

        private static void CreateLoot(
            PrimitiveType primitive,
            Transform parent,
            string displayName,
            Vector3 position,
            Vector3 scale,
            Material material,
            int value,
            float weight,
            float durability,
            float impactThreshold,
            float impactDamageMultiplier,
            int requiredCarriers,
            int maxCarriers)
        {
            GameObject item = CreatePrimitive(primitive, displayName, parent, position, scale, material);
            item.AddComponent<NetworkObject>();

            Rigidbody rigidbody = item.AddComponent<Rigidbody>();
            rigidbody.mass = weight;
            rigidbody.linearDamping = .35f;
            rigidbody.angularDamping = .2f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            PhysicsLoot loot = item.AddComponent<PhysicsLoot>();
            loot.Configure(
                displayName,
                value,
                weight,
                durability,
                impactThreshold,
                impactDamageMultiplier,
                requiredCarriers,
                maxCarriers,
                false);
        }

        private static void CreateDrones(Transform parent, Material material)
        {
            CreateDrone(parent, "Storm Drone 01", new Vector3(0f, 11f, 0f), 4.5f, material);
            CreateDrone(parent, "Storm Drone 02", new Vector3(0f, 21f, 0f), 5.8f, material);
            CreateDrone(parent, "Storm Drone 03", new Vector3(0f, 31f, 0f), 6.5f, material);
        }

        private static void CreateDrone(Transform parent, string name, Vector3 position, float orbitRadius, Material material)
        {
            var drone = CreatePrimitive(PrimitiveType.Sphere, name, parent, position, new Vector3(1.25f, .55f, 1.25f), material);
            drone.AddComponent<NetworkObject>();
            drone.GetComponent<Collider>().isTrigger = true;

            var rigidbody = drone.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var droneHazard = drone.AddComponent<DroneHazard>();
            droneHazard.Configure(orbitRadius);
        }

        private static void CreateMonsters(Transform hazardsParent, Material stalkerMaterial, Material bruteMaterial, Material scavengerMaterial)
        {
            Transform monsters = CreateEmpty("Monsters", hazardsParent).transform;

            CreateMonster(
                monsters,
                "STALKER",
                PrimitiveType.Capsule,
                new Vector3(-2.2f, 12f, 2.2f),
                new Vector3(.82f, 1.15f, .82f),
                stalkerMaterial,
                MonsterArchetype.Stalker);

            CreateMonster(
                monsters,
                "BRUTE",
                PrimitiveType.Cube,
                new Vector3(2.4f, 22f, -1.8f),
                new Vector3(1.45f, 1.5f, 1.45f),
                bruteMaterial,
                MonsterArchetype.Brute);

            CreateMonster(
                monsters,
                "SCAVENGER",
                PrimitiveType.Sphere,
                new Vector3(-2.4f, 31f, -1.6f),
                new Vector3(1.15f, .72f, 1.45f),
                scavengerMaterial,
                MonsterArchetype.Scavenger);
        }

        private static void CreateMonster(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 position,
            Vector3 scale,
            Material material,
            MonsterArchetype archetype)
        {
            GameObject monster = CreatePrimitive(primitive, name, parent, position, scale, material);
            monster.AddComponent<NetworkObject>();

            Collider collider = monster.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

            Rigidbody rigidbody = monster.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            MonsterController controller = monster.AddComponent<MonsterController>();
            controller.Configure(archetype);
        }

        private static GameObject CreateEmpty(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            if (parent != null)
                gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");

            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
#endif