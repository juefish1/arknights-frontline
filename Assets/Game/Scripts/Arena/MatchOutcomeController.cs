using System;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
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

        public event Action MatchResolved;

        public MatchOutcome Outcome { get; private set; }

        public float ElapsedSeconds { get; private set; }

        private void Awake()
        {
            if (blueTower != null && redTower != null && spawner != null)
            {
                Configure(blueTower, redTower, spawner);
            }
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
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
            ElapsedSeconds = 0f;
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
            Tick(0f);
        }

        public void Tick(float deltaTime)
        {
            if (IsMatchOver)
            {
                return;
            }

            if (!settlementPending
                && deltaTime > 0f
                && !float.IsNaN(deltaTime)
                && !float.IsInfinity(deltaTime))
            {
                ElapsedSeconds += deltaTime;
            }

            if (!settlementPending)
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
            MatchResolved?.Invoke();
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

            foreach (PlayerCommandController controller in
                     UnityEngine.Object.FindObjectsByType<PlayerCommandController>(FindObjectsSortMode.None))
            {
                controller.StopForMatch();
            }

            foreach (OperatorRosterController controller in
                     UnityEngine.Object.FindObjectsByType<OperatorRosterController>(FindObjectsSortMode.None))
            {
                controller.StopForMatch();
            }

            foreach (SimpleOperatorAiController controller in
                     UnityEngine.Object.FindObjectsByType<SimpleOperatorAiController>(FindObjectsSortMode.None))
            {
                controller.StopForMatch();
            }

            foreach (OperatorRetreatController controller in
                     UnityEngine.Object.FindObjectsByType<OperatorRetreatController>(FindObjectsSortMode.None))
            {
                controller.StopForMatch();
            }

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

            foreach (UnitMotor motor in
                     UnityEngine.Object.FindObjectsByType<UnitMotor>(FindObjectsSortMode.None))
            {
                motor.Stop();
                motor.enabled = false;
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
