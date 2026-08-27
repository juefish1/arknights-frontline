using System;
using ArknightsFrontline.Combat;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public sealed class MatchOutcomeController : MonoBehaviour
    {
        private CombatUnit blueTower;
        private CombatUnit redTower;
        private MinionWaveSpawner spawner;
        private bool blueTowerDestroyed;
        private bool redTowerDestroyed;

        public bool IsMatchOver { get; private set; }

        public MatchOutcome Outcome { get; private set; }

        private void Update()
        {
            Tick();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTowers();
        }

        public void Configure(CombatUnit blueTower, CombatUnit redTower, MinionWaveSpawner spawner)
        {
            if (blueTower == null)
            {
                throw new ArgumentNullException(nameof(blueTower));
            }

            if (redTower == null)
            {
                throw new ArgumentNullException(nameof(redTower));
            }

            if (spawner == null)
            {
                throw new ArgumentNullException(nameof(spawner));
            }

            UnsubscribeFromTowers();

            this.blueTower = blueTower;
            this.redTower = redTower;
            this.spawner = spawner;
            blueTowerDestroyed = blueTower.IsDead;
            redTowerDestroyed = redTower.IsDead;
            Outcome = MatchOutcome.None;
            IsMatchOver = false;

            this.blueTower.Died += OnBlueTowerDied;
            this.redTower.Died += OnRedTowerDied;
        }

        public void Tick()
        {
            if (IsMatchOver || (!blueTowerDestroyed && !redTowerDestroyed))
            {
                return;
            }

            if (blueTowerDestroyed && redTowerDestroyed)
            {
                Outcome = MatchOutcome.Draw;
            }
            else if (blueTowerDestroyed)
            {
                Outcome = MatchOutcome.RedVictory;
            }
            else
            {
                Outcome = MatchOutcome.BlueVictory;
            }

            FreezeCombat();
            IsMatchOver = true;
        }

        private void FreezeCombat()
        {
            spawner.StopForMatch();

            foreach (LaneMinionController controller in
                     Object.FindObjectsByType<LaneMinionController>(FindObjectsSortMode.None))
            {
                controller.StopForMatch();
            }

            foreach (TowerCombatController controller in
                     Object.FindObjectsByType<TowerCombatController>(FindObjectsSortMode.None))
            {
                controller.StopForMatch();
            }

            foreach (BasicAttackController attack in
                     Object.FindObjectsByType<BasicAttackController>(FindObjectsSortMode.None))
            {
                attack.ClearTarget();
                attack.enabled = false;
            }

            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                projectile.Cancel();
            }
        }

        private void UnsubscribeFromTowers()
        {
            if (blueTower != null)
            {
                blueTower.Died -= OnBlueTowerDied;
            }

            if (redTower != null)
            {
                redTower.Died -= OnRedTowerDied;
            }
        }

        private void OnBlueTowerDied(CombatUnit _)
        {
            blueTowerDestroyed = true;
        }

        private void OnRedTowerDied(CombatUnit _)
        {
            redTowerDestroyed = true;
        }
    }
}
