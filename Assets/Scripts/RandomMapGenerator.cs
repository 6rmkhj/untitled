using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class RandomMapGenerator : NetworkBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform generatedRoot;
        [SerializeField] private Material deckMaterial;
        [SerializeField] private Material steelMaterial;
        [SerializeField] private Material hazardMaterial;
        [SerializeField] private SignalCore[] cores;
        [SerializeField] private DroneHazard[] drones;

        [Header("Generation")]
        [SerializeField, Range(8, 16)] private int minLevels = 10;
        [SerializeField, Range(8, 16)] private int maxLevels = 13;
        [SerializeField] private float levelHeight = 3.1f;
        [SerializeField] private float firstLevelY = 2.2f;
        [SerializeField] private float horizontalStep = 2.2f;
        [SerializeField] private float depthStep = 1.8f;

        private readonly NetworkVariable<int> mapSeed = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<Vector3> levelCenters = new();

        public int CurrentSeed => mapSeed.Value;
        public int GeneratedLevelCount => levelCenters.Count;

        public void Configure(
            Transform root,
            Material deck,
            Material steel,
            Material hazard,
            SignalCore[] signalCores,
            DroneHazard[] mapDrones)
        {
            generatedRoot = root;
            deckMaterial = deck;
            steelMaterial = steel;
            hazardMaterial = hazard;
            cores = signalCores;
            drones = mapDrones;
        }

        public override void OnNetworkSpawn()
        {
            mapSeed.OnValueChanged += OnSeedChanged;

            if (IsServer && mapSeed.Value == 0)
            {
                int seed;
                do
                {
                    seed = UnityEngine.Random.Range(1, int.MaxValue);
                }
                while (seed == 0);

                mapSeed.Value = seed;
            }

            if (mapSeed.Value != 0)
                Generate(mapSeed.Value);
        }

        public override void OnNetworkDespawn()
        {
            mapSeed.OnValueChanged -= OnSeedChanged;
        }

        [ContextMenu("Regenerate On Server")]
        public void RegenerateOnServer()
        {
            if (!IsServer || !IsSpawned)
                return;

            int nextSeed;
            do
            {
                nextSeed = UnityEngine.Random.Range(1, int.MaxValue);
            }
            while (nextSeed == mapSeed.Value);

            mapSeed.Value = nextSeed;
        }

        private void OnSeedChanged(int previousValue, int newValue)
        {
            if (newValue != 0)
                Generate(newValue);
        }

        private void Generate(int seed)
        {
            EnsureReferences();
            if (generatedRoot == null)
            {
                Debug.LogError("SIGNAL HAUL: RandomMapGenerator has no Generated Root.");
                return;
            }

            ClearGeneratedChildren();
            levelCenters.Clear();

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

                var center = new Vector3(x, firstLevelY + i * levelHeight, z);
                levelCenters.Add(center);
                BuildLevel(i, previous, center, random);
                previous = center;
            }

            if (IsServer)
                PositionNetworkGameplayObjects();
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

            if (cores != null)
            {
                for (int i = 0; i < cores.Length; i++)
                {
                    if (cores[i] == null)
                        continue;

                    int levelIndex = Mathf.Clamp(
                        Mathf.RoundToInt((i + 1f) / (cores.Length + 1f) * (levelCenters.Count - 1)),
                        0,
                        levelCenters.Count - 1);
                    Vector3 position = levelCenters[levelIndex] + Vector3.up * 1.15f;
                    cores[i].SetMapSpawnServer(position);
                }
            }

            if (drones != null)
            {
                for (int i = 0; i < drones.Length; i++)
                {
                    if (drones[i] == null)
                        continue;

                    int levelIndex = Mathf.Clamp(
                        Mathf.RoundToInt((i + .7f) / Mathf.Max(1f, drones.Length) * (levelCenters.Count - 1)),
                        0,
                        levelCenters.Count - 1);
                    Vector3 position = levelCenters[levelIndex] + Vector3.up * 2.3f;
                    drones[i].SetMapCenterServer(position);
                }
            }
        }

        private void EnsureReferences()
        {
            if (cores == null || cores.Length == 0)
                cores = FindObjectsByType<SignalCore>(FindObjectsSortMode.None);
            if (drones == null || drones.Length == 0)
                drones = FindObjectsByType<DroneHazard>(FindObjectsSortMode.None);
        }

        private void ClearGeneratedChildren()
        {
            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                Destroy(generatedRoot.GetChild(i).gameObject);
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;

            var renderer = gameObject.GetComponent<Renderer>();
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
