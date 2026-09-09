using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalHaul
{
    public sealed class RandomMapGenerator : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField, Range(8, 16)] private int minLevels = 10;
        [SerializeField, Range(8, 16)] private int maxLevels = 13;
        [SerializeField] private float levelHeight = 3.1f;
        [SerializeField] private float firstLevelY = 2.2f;
        [SerializeField] private float horizontalStep = 2.2f;
        [SerializeField] private float depthStep = 1.8f;

        private Transform generatedRoot;
        private GameObject staticTower;
        private Material deckMaterial;
        private Material steelMaterial;
        private Material hazardMaterial;
        private readonly List<Vector3> levelCenters = new();

        public int CurrentSeed { get; private set; }
        public int GeneratedLevelCount => levelCenters.Count;

        public void Generate(int seed, bool placeNetworkObjects)
        {
            if (seed == 0)
                return;

            ResolveSceneReferences();
            ClearGeneratedChildren();
            levelCenters.Clear();
            CurrentSeed = seed;

            var random = new System.Random(seed);
            int low = Mathf.Min(minLevels, maxLevels);
            int high = Mathf.Max(minLevels, maxLevels);
            int levelCount = random.Next(low, high + 1);

            Vector3 previous = new Vector3(0f, .35f, 0f);
            float x = 0f;
            float z = 0f;

            for (int i = 0; i < levelCount; i++)
            {
                if (i > 0)
                {
                    bool moveX = random.NextDouble() < .55;
                    float direction = random.Next(0, 2) == 0 ? -1f : 1f;

                    if (moveX)
                        x = Mathf.Clamp(x + direction * horizontalStep, -3.6f, 3.6f);
                    else
                        z = Mathf.Clamp(z + direction * depthStep, -2.8f, 2.8f);
                }

                Vector3 center = new Vector3(x, firstLevelY + i * levelHeight, z);
                levelCenters.Add(center);
                BuildLevel(i, previous, center, random);
                previous = center;
            }

            if (staticTower != null)
                staticTower.SetActive(false);

            if (placeNetworkObjects)
                PositionNetworkGameplayObjects();
        }

        private void ResolveSceneReferences()
        {
            if (generatedRoot != null)
                return;

            SignalHaulSceneMarker marker = FindFirstObjectByType<SignalHaulSceneMarker>();
            Transform environment = null;
            if (marker != null)
                environment = marker.transform.Find("Environment");

            if (environment == null)
            {
                GameObject environmentObject = GameObject.Find("Environment");
                if (environmentObject != null)
                    environment = environmentObject.transform;
            }

            if (environment == null)
            {
                environment = new GameObject("Environment").transform;
                if (marker != null)
                    environment.SetParent(marker.transform, false);
            }

            Transform towerTransform = environment.Find("Tower");
            if (towerTransform != null)
            {
                staticTower = towerTransform.gameObject;
                deckMaterial = FindMaterialByObjectName(towerTransform, "Deck");
                steelMaterial = FindMaterialByObjectName(towerTransform, "Climb Wall");
                hazardMaterial = FindMaterialByObjectName(towerTransform, "Hazard Bridge");
            }

            Transform existingGenerated = environment.Find("Generated Random Tower");
            if (existingGenerated != null)
                generatedRoot = existingGenerated;
            else
            {
                var rootObject = new GameObject("Generated Random Tower");
                rootObject.transform.SetParent(environment, false);
                generatedRoot = rootObject.transform;
            }

            deckMaterial ??= CreateRuntimeMaterial("Random Deck", new Color(.25f, .29f, .3f), .45f, .3f);
            steelMaterial ??= CreateRuntimeMaterial("Random Steel", new Color(.16f, .2f, .23f), .65f, .35f);
            hazardMaterial ??= CreateRuntimeMaterial("Random Hazard", new Color(.95f, .55f, .08f), .15f, .3f);
        }

        private void BuildLevel(int index, Vector3 previous, Vector3 center, System.Random random)
        {
            int style = random.Next(0, 4);
            var level = new GameObject($"Level_{index:00}_{StyleName(style)}");
            level.transform.SetParent(generatedRoot, false);

            float deckWidth = style == 1 ? 4.8f : style == 2 ? 7.2f : 6.1f;
            float deckDepth = style == 2 ? 4.2f : style == 3 ? 6.1f : 5.2f;
            CreateCube("Deck", level.transform, center, new Vector3(deckWidth, .35f, deckDepth), deckMaterial);

            Vector3 delta = center - previous;
            Vector3 midpoint = Vector3.Lerp(previous, center, .55f);
            midpoint.y = (previous.y + center.y) * .5f + .25f;

            bool mostlyX = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);
            Vector3 wallScale = mostlyX
                ? new Vector3(.45f, Mathf.Max(2.4f, center.y - previous.y), 4.4f)
                : new Vector3(4.4f, Mathf.Max(2.4f, center.y - previous.y), .45f);
            CreateCube("Climb Wall", level.transform, midpoint, wallScale, steelMaterial);

            Vector3 bridgeCenter = Vector3.Lerp(previous, center, .35f);
            bridgeCenter.y = previous.y + .55f;
            Vector3 bridgeScale = mostlyX
                ? new Vector3(Mathf.Max(2.4f, Mathf.Abs(delta.x) + 2.2f), .28f, style == 1 ? 1.4f : 2.3f)
                : new Vector3(style == 1 ? 1.4f : 2.3f, .28f, Mathf.Max(2.4f, Mathf.Abs(delta.z) + 2.2f));
            CreateCube(style == 3 ? "Hazard Bridge" : "Bridge", level.transform, bridgeCenter, bridgeScale, style == 3 ? hazardMaterial : deckMaterial);

            if (style == 0 || style == 2)
            {
                float railOffset = deckWidth * .5f - .18f;
                CreateCube("Rail Left", level.transform, center + new Vector3(-railOffset, .65f, 0f), new Vector3(.14f, 1.3f, deckDepth), steelMaterial);
                CreateCube("Rail Right", level.transform, center + new Vector3(railOffset, .65f, 0f), new Vector3(.14f, 1.3f, deckDepth), steelMaterial);
            }

            if (style == 1)
            {
                float side = random.Next(0, 2) == 0 ? -1f : 1f;
                Vector3 ledgePosition = center + new Vector3(side * (deckWidth * .55f), .75f, 0f);
                CreateCube("Side Ledge", level.transform, ledgePosition, new Vector3(1.4f, .22f, 2.5f), hazardMaterial);
            }

            if (style == 2)
            {
                Vector3 obstacle = center + new Vector3(0f, .85f, random.Next(0, 2) == 0 ? -1.6f : 1.6f);
                CreateCube("Cargo Obstacle", level.transform, obstacle, new Vector3(1.6f, 1.5f, 1.2f), steelMaterial);
            }
        }

        private void PositionNetworkGameplayObjects()
        {
            if (levelCenters.Count == 0)
                return;

            SignalCore[] cores = FindObjectsByType<SignalCore>(FindObjectsSortMode.None);
            Array.Sort(cores, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < cores.Length; i++)
            {
                int levelIndex = Mathf.Clamp(
                    Mathf.RoundToInt((i + 1f) / (cores.Length + 1f) * (levelCenters.Count - 1)),
                    0,
                    levelCenters.Count - 1);
                cores[i].SetMapSpawnServer(levelCenters[levelIndex] + Vector3.up * 1.15f);
            }

            PhysicsLoot[] allLoot = FindObjectsByType<PhysicsLoot>(FindObjectsSortMode.None);
            var bonusLoot = new List<PhysicsLoot>();
            foreach (PhysicsLoot item in allLoot)
            {
                if (item != null && !item.IsRequiredObjective)
                    bonusLoot.Add(item);
            }

            bonusLoot.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < bonusLoot.Count; i++)
            {
                int levelIndex = Mathf.Clamp(
                    Mathf.RoundToInt((i + 1f) / (bonusLoot.Count + 1f) * (levelCenters.Count - 2)),
                    1,
                    levelCenters.Count - 2);
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 offset = new Vector3(side * 1.25f, 1.05f, i % 3 == 0 ? .8f : -.8f);
                bonusLoot[i].SetMapSpawnServer(levelCenters[levelIndex] + offset);
            }

            DroneHazard[] drones = FindObjectsByType<DroneHazard>(FindObjectsSortMode.None);
            Array.Sort(drones, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < drones.Length; i++)
            {
                int levelIndex = Mathf.Clamp(
                    Mathf.RoundToInt((i + .7f) / Mathf.Max(1f, drones.Length) * (levelCenters.Count - 1)),
                    0,
                    levelCenters.Count - 1);
                drones[i].SetMapCenterServer(levelCenters[levelIndex] + Vector3.up * 2.3f);
            }

            MonsterController[] monsters = FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
            Array.Sort(monsters, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < monsters.Length; i++)
            {
                int levelIndex = Mathf.Clamp(
                    Mathf.RoundToInt((i + 1f) / (monsters.Length + 1f) * (levelCenters.Count - 1)),
                    1,
                    levelCenters.Count - 1);
                float side = i % 2 == 0 ? 2.4f : -2.4f;
                Vector3 position = levelCenters[levelIndex] + new Vector3(side, 2.2f, i % 2 == 0 ? -1.6f : 1.6f);
                monsters[i].SetMapHomeServer(position);
            }
        }

        private void ClearGeneratedChildren()
        {
            if (generatedRoot == null)
                return;

            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                Destroy(generatedRoot.GetChild(i).gameObject);
        }

        private static Material FindMaterialByObjectName(Transform root, string objectName)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name == objectName && renderer.sharedMaterial != null)
                    return renderer.sharedMaterial;
            }

            return null;
        }

        private static Material CreateRuntimeMaterial(string name, Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            return gameObject;
        }

        private static string StyleName(int style)
        {
            return style switch
            {
                1 => "Narrow",
                2 => "Cargo",
                3 => "Hazard",
                _ => "Standard"
            };
        }
    }
}
