using System;
using System.Collections.Generic;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class OperatorRosterSlot
    {
        internal OperatorRosterSlot(
            string stableKey,
            TeamId team,
            OperatorType operatorType,
            GameObject template,
            Vector3 deploymentPosition,
            bool isPlayerControlled)
        {
            StableKey = stableKey;
            Team = team;
            OperatorType = operatorType;
            Template = template;
            DeploymentPosition = deploymentPosition;
            IsPlayerControlled = isPlayerControlled;
        }

        public string StableKey { get; }

        public TeamId Team { get; }

        public OperatorType OperatorType { get; }

        public GameObject Template { get; }

        public Vector3 DeploymentPosition { get; }

        public bool IsPlayerControlled { get; }

        public int DepartureCount { get; internal set; }

        public CombatUnit CurrentOperator { get; internal set; }

        public float RedeployRemaining { get; internal set; }

        public bool IsStopped { get; internal set; }
    }

    public sealed class OperatorRosterController : MonoBehaviour
    {
        private readonly List<OperatorRosterSlot> slots = new List<OperatorRosterSlot>();
        private bool hasStarted;
        private bool isStopped;

        public IReadOnlyList<OperatorRosterSlot> Slots => slots;

        public event Action<OperatorRosterSlot, CombatUnit> OperatorSpawned;

        public event Action<OperatorRosterSlot, CombatUnit> OperatorDeparted;

        public event Action<OperatorRosterSlot, CombatUnit> PlayerOperatorSpawned;

        public event Action<OperatorRosterSlot, CombatUnit> PlayerOperatorDeparted;

        public OperatorRosterSlot RegisterSlot(
            string stableKey,
            TeamId team,
            OperatorType operatorType,
            GameObject template,
            Vector3 deploymentPosition,
            bool isPlayerControlled = false)
        {
            if (hasStarted || isStopped)
            {
                throw new InvalidOperationException("Operator slots must be registered before match start or stop.");
            }

            if (string.IsNullOrWhiteSpace(stableKey))
            {
                throw new ArgumentException("Operator slots require a stable key.", nameof(stableKey));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (template.activeSelf)
            {
                throw new ArgumentException("Operator templates must be inactive GameObjects.", nameof(template));
            }

            if (template.GetComponent<CombatUnit>() == null)
            {
                throw new ArgumentException("Operator templates require a CombatUnit component.", nameof(template));
            }

            if (template.GetComponent<OperatorIdentity>() == null)
            {
                throw new ArgumentException("Operator templates require an OperatorIdentity component.", nameof(template));
            }

            if (template.GetComponent<DeathCorpsePresenter>() == null)
            {
                throw new ArgumentException("Operator templates require a DeathCorpsePresenter component.", nameof(template));
            }

            if (slots.Exists(slot => string.Equals(slot.StableKey, stableKey, StringComparison.Ordinal)))
            {
                throw new ArgumentException($"The operator key '{stableKey}' is already registered.", nameof(stableKey));
            }

            OperatorRosterSlot newSlot = new OperatorRosterSlot(
                stableKey,
                team,
                operatorType,
                template,
                deploymentPosition,
                isPlayerControlled);
            slots.Add(newSlot);
            return newSlot;
        }

        public void StartMatch()
        {
            if (hasStarted || isStopped)
            {
                return;
            }

            hasStarted = true;
            foreach (OperatorRosterSlot slot in slots)
            {
                Deploy(slot);
            }
        }

        public void Tick(float deltaTime)
        {
            if (isStopped)
            {
                return;
            }

            float elapsed = Mathf.Max(0f, deltaTime);
            foreach (OperatorRosterSlot slot in slots)
            {
                if (slot.IsStopped || slot.CurrentOperator != null || slot.RedeployRemaining <= 0f)
                {
                    continue;
                }

                slot.RedeployRemaining = Mathf.Max(0f, slot.RedeployRemaining - elapsed);
                if (slot.RedeployRemaining <= 0.0001f)
                {
                    slot.RedeployRemaining = 0f;
                    Deploy(slot);
                }
            }
        }

        public bool NotifySuccessfulRetreat(CombatUnit liveOperator)
        {
            if (liveOperator == null)
            {
                return false;
            }

            OperatorRosterSlot slot = slots.Find(candidate => candidate.CurrentOperator == liveOperator);
            if (slot == null || slot.IsStopped || isStopped || !hasStarted || liveOperator.IsDead)
            {
                return false;
            }

            BeginDeparture(slot, liveOperator, true);
            liveOperator.gameObject.SetActive(false);
            DestroyUnityObject(liveOperator.gameObject);
            return true;
        }

        public void StopForMatch()
        {
            if (isStopped)
            {
                return;
            }

            isStopped = true;
            foreach (OperatorRosterSlot slot in slots)
            {
                slot.IsStopped = true;
                slot.RedeployRemaining = 0f;
                UnsubscribeFromDeath(slot);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            foreach (OperatorRosterSlot slot in slots)
            {
                UnsubscribeFromDeath(slot);
            }
        }

        private void Deploy(OperatorRosterSlot slot)
        {
            if (isStopped || slot.IsStopped || slot.CurrentOperator != null)
            {
                return;
            }

            CorpseLifetimeController.ClearOperatorCorpse(slot.StableKey);

            CombatUnit templateUnit = slot.Template.GetComponent<CombatUnit>();
            GameObject instance = Instantiate(
                slot.Template,
                slot.DeploymentPosition,
                slot.Template.transform.rotation);
            instance.name = slot.StableKey;

            CombatUnit liveOperator = instance.GetComponent<CombatUnit>();
            liveOperator.Configure(
                slot.Team,
                templateUnit.Altitude,
                templateUnit.MaxHealth,
                templateUnit.BaseAttackPower,
                templateUnit.Defense,
                templateUnit.AttackRange,
                templateUnit.BaseAttackInterval,
                templateUnit.CanAttackGround,
                templateUnit.CanAttackAir);

            OperatorIdentity identity = instance.GetComponent<OperatorIdentity>();
            identity.Configure(slot.StableKey, slot.Team, slot.OperatorType);

            UnitStatModifiers modifiers = instance.GetComponent<UnitStatModifiers>();
            if (modifiers != null)
            {
                modifiers.Clear();
            }

            slot.CurrentOperator = liveOperator;
            slot.RedeployRemaining = 0f;
            liveOperator.Died -= OnOperatorDied;
            liveOperator.Died += OnOperatorDied;

            instance.SetActive(true);
            DeathCorpsePresenter corpsePresenter = instance.GetComponent<DeathCorpsePresenter>();
            DeathCorpsePresenter templateCorpsePresenter = slot.Template.GetComponent<DeathCorpsePresenter>();
            if (corpsePresenter != null && templateCorpsePresenter != null)
            {
                corpsePresenter.ConfigureFromTemplate(templateCorpsePresenter, liveOperator);
            }

            OperatorSpawned?.Invoke(slot, liveOperator);
            if (slot.IsPlayerControlled)
            {
                PlayerOperatorSpawned?.Invoke(slot, liveOperator);
            }
        }

        private void OnOperatorDied(CombatUnit deadOperator)
        {
            OperatorRosterSlot slot = slots.Find(candidate => candidate.CurrentOperator == deadOperator);
            if (slot == null || slot.IsStopped || isStopped)
            {
                return;
            }

            BeginDeparture(slot, deadOperator, false);
        }

        private void BeginDeparture(OperatorRosterSlot slot, CombatUnit departingOperator, bool wasSuccessfulRetreat)
        {
            if (slot.CurrentOperator != departingOperator || slot.IsStopped || isStopped)
            {
                return;
            }

            departingOperator.Died -= OnOperatorDied;
            slot.CurrentOperator = null;
            slot.DepartureCount++;
            float fullWait = Mathf.Min(8f + 4f * (slot.DepartureCount - 1), 24f);
            slot.RedeployRemaining = wasSuccessfulRetreat ? fullWait * 0.7f : fullWait;

            OperatorDeparted?.Invoke(slot, departingOperator);
            if (slot.IsPlayerControlled)
            {
                PlayerOperatorDeparted?.Invoke(slot, departingOperator);
            }
        }

        private void UnsubscribeFromDeath(OperatorRosterSlot slot)
        {
            if (slot.CurrentOperator != null)
            {
                slot.CurrentOperator.Died -= OnOperatorDied;
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }
    }
}
