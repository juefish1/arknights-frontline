using System;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public sealed class MinionWaveSpawner : MonoBehaviour
    {
        private const float WaveInterval = 25f;
        private static readonly float[] GroundOffsets = { -2f, 0f, 2f };

        [SerializeField] private Transform minionParent;
        [SerializeField] private CombatUnit blueTower;
        [SerializeField] private CombatUnit redTower;
        [SerializeField] private Material blueMaterial;
        [SerializeField] private Material redMaterial;
        [SerializeField] private int targetableLayer;
        [SerializeField] private int groundLayer;

        private ArenaLayout layout;
        private float elapsed;
        private bool stopped;
        private bool isConfigured;

        public int SpawnedWaveCount { get; private set; }

        private void Awake()
        {
            if (minionParent != null && blueTower != null && redTower != null)
            {
                Configure(
                    minionParent,
                    ArenaLayout.CreateDefault(),
                    blueTower,
                    redTower,
                    blueMaterial,
                    redMaterial,
                    targetableLayer,
                    groundLayer);
            }
        }

        private void Start()
        {
            if (!isConfigured)
            {
                return;
            }

            SpawnWaveNow();
            elapsed = 0f;
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(
            Transform minionParent,
            ArenaLayout arenaLayout,
            CombatUnit blueTower,
            CombatUnit redTower,
            Material blueMaterial,
            Material redMaterial,
            int targetableLayer,
            int groundLayer)
        {
            if (minionParent == null)
            {
                throw new ArgumentNullException(nameof(minionParent));
            }

            if (blueTower == null)
            {
                throw new ArgumentNullException(nameof(blueTower));
            }

            if (redTower == null)
            {
                throw new ArgumentNullException(nameof(redTower));
            }

            if (blueMaterial == null)
            {
                throw new ArgumentNullException(nameof(blueMaterial));
            }

            if (redMaterial == null)
            {
                throw new ArgumentNullException(nameof(redMaterial));
            }

            this.minionParent = minionParent;
            layout = arenaLayout;
            this.blueTower = blueTower;
            this.redTower = redTower;
            this.blueMaterial = blueMaterial;
            this.redMaterial = redMaterial;
            this.targetableLayer = targetableLayer;
            this.groundLayer = groundLayer;
            elapsed = 0f;
            stopped = false;
            isConfigured = true;
        }

        public void Tick(float deltaTime)
        {
            if (!CanSpawn())
            {
                return;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            while (elapsed >= WaveInterval)
            {
                elapsed -= WaveInterval;
                SpawnWaveNow();
            }
        }

        public void StopForMatch()
        {
            stopped = true;
        }

        public void SpawnWaveNow()
        {
            if (!CanSpawn())
            {
                return;
            }

            int waveNumber = SpawnedWaveCount + 1;
            SpawnTeamWave(TeamId.Blue, waveNumber, blueTower, redTower, blueMaterial);
            SpawnTeamWave(TeamId.Red, waveNumber, redTower, blueTower, redMaterial);
            SpawnedWaveCount = waveNumber;
        }

        private bool CanSpawn()
        {
            return isConfigured &&
                   !stopped &&
                   !blueTower.IsDead &&
                   !redTower.IsDead;
        }

        private void SpawnTeamWave(
            TeamId team,
            int waveNumber,
            CombatUnit friendlyTower,
            CombatUnit opposingTower,
            Material material)
        {
            for (int index = 0; index < GroundOffsets.Length; index++)
            {
                SpawnMinion(
                    team,
                    Altitude.Ground,
                    waveNumber,
                    index + 1,
                    GroundOffsets[index],
                    friendlyTower,
                    opposingTower,
                    material);
            }

            SpawnMinion(
                team,
                Altitude.Air,
                waveNumber,
                GroundOffsets.Length + 1,
                4f,
                friendlyTower,
                opposingTower,
                material);
        }

        private void SpawnMinion(
            TeamId team,
            Altitude altitude,
            int waveNumber,
            int sequenceNumber,
            float zOffset,
            CombatUnit friendlyTower,
            CombatUnit opposingTower,
            Material material)
        {
            PrimitiveType primitive = altitude == Altitude.Ground ? PrimitiveType.Capsule : PrimitiveType.Sphere;
            GameObject minionObject = GameObject.CreatePrimitive(primitive);
            minionObject.name = $"{team}{altitude}Minion_{waveNumber}_{sequenceNumber}";
            minionObject.transform.SetParent(minionParent);
            minionObject.transform.position = GetSpawnPosition(team, zOffset);
            minionObject.layer = targetableLayer;

            ApplyTeamAppearance(minionObject, material);

            CombatUnit combatUnit = minionObject.AddComponent<CombatUnit>();
            ConfigureCombatUnit(combatUnit, team, altitude);
            DeathCorpsePresenter presenter = minionObject.AddComponent<DeathCorpsePresenter>();
            presenter.Configure(combatUnit, material, groundLayer);

            UnitMotor motor = minionObject.AddComponent<UnitMotor>();
            motor.Configure(altitude == Altitude.Ground ? 3f : 3.2f, layout);

            BasicAttackController attack = minionObject.AddComponent<BasicAttackController>();
            attack.Configure(combatUnit);

            LaneMinionController controller = minionObject.AddComponent<LaneMinionController>();
            controller.Configure(
                combatUnit,
                motor,
                attack,
                opposingTower,
                opposingTower.transform.position);
        }

        private Vector3 GetSpawnPosition(TeamId team, float zOffset)
        {
            Vector3 towerPosition = team == TeamId.Blue ? layout.BlueTower : layout.RedTower;
            float xOffset = team == TeamId.Blue ? 6f : -6f;
            return towerPosition + new Vector3(xOffset, 1f, zOffset);
        }

        private static void ConfigureCombatUnit(CombatUnit combatUnit, TeamId team, Altitude altitude)
        {
            if (altitude == Altitude.Ground)
            {
                combatUnit.Configure(team, altitude, 400f, 35f, 10f, 1.5f, 1.2f, true, false);
                return;
            }

            combatUnit.Configure(team, altitude, 280f, 28f, 5f, 4.5f, 1f, true, true);
        }

        private static void ApplyTeamAppearance(GameObject minionObject, Material material)
        {
            Renderer renderer = minionObject.GetComponent<Renderer>();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
