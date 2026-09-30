using System;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Skills;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public sealed class MatchOutcomeController : MonoBehaviour
    {
        [SerializeField] private CombatUnit blueTower;
        [SerializeField] private CombatUnit redTower;
        [SerializeField] private MinionWaveSpawner spawner;
        private bool blueTowerDestroyed;
        private bool redTowerDestroyed;
        private bool settlementPending;

        public bool IsMatchOver { get; private set; }

        public bool IsEnding => settlementPending || IsMatchOver;

        public event Action MatchEnding;

        public MatchOutcome Outcome { get; private set; }

        private void Awake()
        {
            if (blueTower != null && redTower != null && spawner != null)
            {
                Configure(blueTower, redTower, spawner);
            }
        }

        private void LateUpdate()
        {
            Tick();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTowers();
        }

        public void Configure(CombatUnit blueTower, CombatUnit redTower, MinionWaveSpawner spawner)
        {
            if (settlementPending || IsMatchOver)
            {
                return;
            }

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
            settlementPending = false;

            this.blueTower.Died += OnBlueTowerDied;
            this.redTower.Died += OnRedTowerDied;

            if (blueTowerDestroyed || redTowerDestroyed)
            {
                BeginSettlement();
            }
        }

        public void Tick()
        {
            if (IsMatchOver || !settlementPending)
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

            IsMatchOver = true;
            settlementPending = false;
            CancelInFlightProjectiles();
        }

        private void BeginSettlement()
        {
            if (settlementPending || IsMatchOver)
            {
                return;
            }

            settlementPending = true;
            MatchEnding?.Invoke();
            StopCombatProducers();
        }

        private void StopCombatProducers()
        {
            spawner.StopForMatch();

            foreach (LaneMinionController controller in
                     UnityEngine.Object.FindObjectsByType<LaneMinionController>(FindObjectsSortMode.None))
            {
                controller.StopForMatch();
            }

            foreach (TowerCombatController controller in
                     UnityEngine.Object.FindObjectsByType<TowerCombatController>(FindObjectsSortMode.None))
            {
                controller.StopForMatch();
            }

            foreach (BasicAttackController attack in
                     UnityEngine.Object.FindObjectsByType<BasicAttackController>(FindObjectsSortMode.None))
            {
                attack.ClearTarget();
                attack.enabled = false;
            }

            foreach (ExusiaiSkillController skills in
                     UnityEngine.Object.FindObjectsByType<ExusiaiSkillController>(FindObjectsSortMode.None))
            {
                skills.StopForMatch();
            }

            foreach (AttackSequenceExecutor sequence in
                     UnityEngine.Object.FindObjectsByType<AttackSequenceExecutor>(FindObjectsSortMode.None))
            {
                sequence.Cancel();
            }

            foreach (SkillDashController dash in
                     UnityEngine.Object.FindObjectsByType<SkillDashController>(FindObjectsSortMode.None))
            {
                dash.Cancel();
            }

            foreach (TimedStatModifierController effects in
                     UnityEngine.Object.FindObjectsByType<TimedStatModifierController>(FindObjectsSortMode.None))
            {
                effects.StopForMatch();
            }
        }

        private static void CancelInFlightProjectiles()
        {
            foreach (Projectile projectile in
                     UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
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
            BeginSettlement();
        }

        private void OnRedTowerDied(CombatUnit _)
        {
            redTowerDestroyed = true;
            BeginSettlement();
        }
    }
}
