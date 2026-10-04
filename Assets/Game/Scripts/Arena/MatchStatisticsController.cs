using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public readonly struct MatchResultRow
    {
        internal MatchResultRow(
            string stableKey,
            TeamId team,
            OperatorType operatorType,
            int kills,
            int deaths,
            float towerDamage)
        {
            StableKey = stableKey;
            Team = team;
            OperatorType = operatorType;
            Kills = kills;
            Deaths = deaths;
            TowerDamage = towerDamage;
        }

        public string StableKey { get; }

        public TeamId Team { get; }

        public OperatorType OperatorType { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public float TowerDamage { get; }
    }

    public sealed class MatchResultSnapshot
    {
        private readonly ReadOnlyCollection<MatchResultRow> rows;

        internal MatchResultSnapshot(
            MatchOutcome outcome,
            float elapsedSeconds,
            float blueTowerCurrentHealth,
            float blueTowerMaxHealth,
            float redTowerCurrentHealth,
            float redTowerMaxHealth,
            MatchResultRow[] rows)
        {
            Outcome = outcome;
            ElapsedSeconds = elapsedSeconds;
            BlueTowerCurrentHealth = blueTowerCurrentHealth;
            BlueTowerMaxHealth = blueTowerMaxHealth;
            RedTowerCurrentHealth = redTowerCurrentHealth;
            RedTowerMaxHealth = redTowerMaxHealth;
            MatchResultRow[] copy = rows == null
                ? Array.Empty<MatchResultRow>()
                : (MatchResultRow[])rows.Clone();
            this.rows = Array.AsReadOnly(copy);
        }

        public MatchOutcome Outcome { get; }

        public float ElapsedSeconds { get; }

        public float BlueTowerCurrentHealth { get; }

        public float BlueTowerMaxHealth { get; }

        public float RedTowerCurrentHealth { get; }

        public float RedTowerMaxHealth { get; }

        public IReadOnlyList<MatchResultRow> Rows => rows;
    }

    public sealed class MatchStatisticsController : MonoBehaviour
    {
        private readonly List<RowAccumulator> rows = new List<RowAccumulator>();
        private readonly Dictionary<string, RowAccumulator> rowsByKey =
            new Dictionary<string, RowAccumulator>(StringComparer.Ordinal);
        private readonly Dictionary<string, OperatorBinding> operatorBindings =
            new Dictionary<string, OperatorBinding>(StringComparer.Ordinal);

        private MatchOutcomeController match;
        private OperatorRosterController roster;
        private CombatUnit blueTower;
        private CombatUnit redTower;
        private TowerBinding blueTowerBinding;
        private TowerBinding redTowerBinding;
        private float blueTowerCurrentHealth;
        private float blueTowerMaxHealth;
        private float redTowerCurrentHealth;
        private float redTowerMaxHealth;
        private bool isConfigured;

        public bool IsFrozen { get; private set; }

        public MatchResultSnapshot Snapshot { get; private set; }

        public event Action<MatchResultSnapshot> ResultReady;

        public void Configure(
            MatchOutcomeController match,
            OperatorRosterController roster,
            CombatUnit blueTower,
            CombatUnit redTower)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (roster == null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            if (blueTower == null)
            {
                throw new ArgumentNullException(nameof(blueTower));
            }

            if (redTower == null)
            {
                throw new ArgumentNullException(nameof(redTower));
            }

            if (blueTower.Team != TeamId.Blue)
            {
                throw new ArgumentException("The blue tower reference must belong to the blue team.", nameof(blueTower));
            }

            if (redTower.Team != TeamId.Red)
            {
                throw new ArgumentException("The red tower reference must belong to the red team.", nameof(redTower));
            }

            ValidateRoster(roster);

            if (isConfigured
                && ReferenceEquals(this.match, match)
                && ReferenceEquals(this.roster, roster)
                && ReferenceEquals(this.blueTower, blueTower)
                && ReferenceEquals(this.redTower, redTower))
            {
                return;
            }

            UnbindSources();
            ResetStatistics();

            this.match = match;
            this.roster = roster;
            this.blueTower = blueTower;
            this.redTower = redTower;
            isConfigured = true;

            blueTowerMaxHealth = blueTower.MaxHealth;
            blueTowerCurrentHealth = Mathf.Clamp(blueTower.CurrentHealth, 0f, blueTowerMaxHealth);
            redTowerMaxHealth = redTower.MaxHealth;
            redTowerCurrentHealth = Mathf.Clamp(redTower.CurrentHealth, 0f, redTowerMaxHealth);

            foreach (OperatorRosterSlot slot in roster.Slots)
            {
                RowAccumulator row = new RowAccumulator(slot.StableKey, slot.Team, slot.OperatorType);
                rows.Add(row);
                rowsByKey.Add(row.StableKey, row);
            }

            blueTowerBinding = CreateTowerBinding(blueTower, TeamId.Blue);
            redTowerBinding = CreateTowerBinding(redTower, TeamId.Red);

            blueTower.DamageTaken += blueTowerBinding.Handler;
            redTower.DamageTaken += redTowerBinding.Handler;

            // Seed already-deployed lives before listening for future spawns. ArenaRosterBootstrap
            // starts the roster in Awake, while this presentation component configures from Start.
            foreach (OperatorRosterSlot slot in roster.Slots)
            {
                if (slot.CurrentOperator != null)
                {
                    BindOperator(slot, slot.CurrentOperator);
                }
            }

            roster.OperatorSpawned += OnOperatorSpawned;
            roster.OperatorDeparted += OnOperatorDeparted;
            match.MatchResolved += OnMatchResolved;

            if (match.IsMatchOver)
            {
                Freeze();
            }
        }

        public IReadOnlyList<MatchResultRow> GetRows()
        {
            MatchResultRow[] snapshot = new MatchResultRow[rows.Count];
            for (int index = 0; index < rows.Count; index++)
            {
                snapshot[index] = rows[index].ToRow();
            }

            return Array.AsReadOnly(snapshot);
        }

        public void Unbind()
        {
            UnbindSources();
            isConfigured = false;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnOperatorSpawned(OperatorRosterSlot slot, CombatUnit liveOperator)
        {
            if (!IsFrozen && slot != null && liveOperator != null)
            {
                BindOperator(slot, liveOperator);
            }
        }

        private void OnOperatorDeparted(OperatorRosterSlot slot, CombatUnit departingOperator)
        {
            if (slot == null || departingOperator == null)
            {
                return;
            }

            if (operatorBindings.TryGetValue(slot.StableKey, out OperatorBinding binding)
                && ReferenceEquals(binding.Operator, departingOperator))
            {
                UnbindOperator(binding);
            }
        }

        private void BindOperator(OperatorRosterSlot slot, CombatUnit liveOperator)
        {
            if (IsFrozen || !IsRegisteredLife(slot, liveOperator))
            {
                return;
            }

            if (operatorBindings.TryGetValue(slot.StableKey, out OperatorBinding existing))
            {
                if (ReferenceEquals(existing.Operator, liveOperator) && existing.IsActive)
                {
                    return;
                }

                UnbindOperator(existing);
            }

            OperatorBinding binding = new OperatorBinding(slot, liveOperator, rowsByKey[slot.StableKey]);
            binding.Handler = (attacker, actualDamage) => OnOperatorDamageTaken(binding, attacker, actualDamage);
            operatorBindings[slot.StableKey] = binding;
            liveOperator.DamageTaken += binding.Handler;
        }

        private void UnbindOperator(OperatorBinding binding)
        {
            if (binding == null || !binding.IsActive)
            {
                return;
            }

            // Mark inactive before touching event subscriptions: a re-entrant DamageTaken
            // invocation may still hold a copied multicast delegate after departure.
            binding.IsActive = false;
            if (binding.Operator != null)
            {
                binding.Operator.DamageTaken -= binding.Handler;
            }

            if (operatorBindings.TryGetValue(binding.Slot.StableKey, out OperatorBinding current)
                && ReferenceEquals(current, binding))
            {
                operatorBindings.Remove(binding.Slot.StableKey);
            }
        }

        private void OnOperatorDamageTaken(OperatorBinding binding, CombatUnit attacker, float actualDamage)
        {
            if (!IsCurrentBinding(binding) || actualDamage <= 0f || float.IsNaN(actualDamage))
            {
                return;
            }

            if (!binding.DeathRecorded && binding.Operator.IsDead)
            {
                // Mark first so nested DamageTaken calls cannot double-count this life.
                binding.DeathRecorded = true;
                binding.Row.Deaths++;

                if (TryGetRegisteredAttacker(attacker, out RowAccumulator killer)
                    && killer.Team != binding.Row.Team)
                {
                    killer.Kills++;
                }
            }
        }

        private void OnTowerDamageTaken(TowerBinding binding, CombatUnit attacker, float actualDamage)
        {
            if (IsFrozen || actualDamage <= 0f || float.IsNaN(actualDamage)
                || !ReferenceEquals(binding, blueTowerBinding) && !ReferenceEquals(binding, redTowerBinding))
            {
                return;
            }

            if (binding.Team == TeamId.Blue)
            {
                blueTowerCurrentHealth = Mathf.Max(0f, blueTowerCurrentHealth - actualDamage);
            }
            else
            {
                redTowerCurrentHealth = Mathf.Max(0f, redTowerCurrentHealth - actualDamage);
            }

            if (TryGetRegisteredAttacker(attacker, out RowAccumulator source)
                && source.Team != binding.Team)
            {
                source.TowerDamage += actualDamage;
            }
        }

        private void OnMatchResolved()
        {
            Freeze();
        }

        private void Freeze()
        {
            if (IsFrozen || match == null)
            {
                return;
            }

            MatchResultRow[] finalRows = new MatchResultRow[rows.Count];
            for (int index = 0; index < rows.Count; index++)
            {
                finalRows[index] = rows[index].ToRow();
            }

            MatchResultSnapshot snapshot = new MatchResultSnapshot(
                match.Outcome,
                match.ElapsedSeconds,
                blueTowerCurrentHealth,
                blueTowerMaxHealth,
                redTowerCurrentHealth,
                redTowerMaxHealth,
                finalRows);

            IsFrozen = true;
            Snapshot = snapshot;
            ResultReady?.Invoke(snapshot);
        }

        private TowerBinding CreateTowerBinding(CombatUnit tower, TeamId team)
        {
            TowerBinding binding = new TowerBinding(tower, team);
            binding.Handler = (attacker, actualDamage) => OnTowerDamageTaken(binding, attacker, actualDamage);
            return binding;
        }

        private bool IsCurrentBinding(OperatorBinding binding)
        {
            return !IsFrozen
                && binding != null
                && binding.IsActive
                && binding.Operator != null
                && ReferenceEquals(binding.Slot.CurrentOperator, binding.Operator)
                && operatorBindings.TryGetValue(binding.Slot.StableKey, out OperatorBinding current)
                && ReferenceEquals(current, binding);
        }

        private bool IsRegisteredLife(OperatorRosterSlot slot, CombatUnit liveOperator)
        {
            if (slot == null || liveOperator == null || liveOperator.IsDead
                || !ReferenceEquals(slot.CurrentOperator, liveOperator)
                || !rowsByKey.TryGetValue(slot.StableKey, out RowAccumulator row))
            {
                return false;
            }

            OperatorIdentity identity = liveOperator.GetComponent<OperatorIdentity>();
            return row.Team == slot.Team
                && row.OperatorType == slot.OperatorType
                && identity != null
                && string.Equals(identity.StableKey, slot.StableKey, StringComparison.Ordinal)
                && identity.Team == slot.Team
                && identity.OperatorType == slot.OperatorType;
        }

        private bool TryGetRegisteredAttacker(CombatUnit attacker, out RowAccumulator row)
        {
            row = null;
            if (IsFrozen || roster == null || attacker == null || attacker.IsDead)
            {
                return false;
            }

            OperatorIdentity identity = attacker.GetComponent<OperatorIdentity>();
            if (identity == null || string.IsNullOrWhiteSpace(identity.StableKey)
                || !rowsByKey.TryGetValue(identity.StableKey, out row)
                || row.Team != identity.Team
                || row.OperatorType != identity.OperatorType)
            {
                row = null;
                return false;
            }

            OperatorRosterSlot slot = null;
            foreach (OperatorRosterSlot candidate in roster.Slots)
            {
                if (string.Equals(candidate.StableKey, identity.StableKey, StringComparison.Ordinal))
                {
                    slot = candidate;
                    break;
                }
            }

            if (slot == null || slot.Team != identity.Team || slot.OperatorType != identity.OperatorType
                || !ReferenceEquals(slot.CurrentOperator, attacker))
            {
                row = null;
                return false;
            }

            return true;
        }

        private void UnbindSources()
        {
            if (roster != null)
            {
                roster.OperatorSpawned -= OnOperatorSpawned;
                roster.OperatorDeparted -= OnOperatorDeparted;
            }

            if (match != null)
            {
                match.MatchResolved -= OnMatchResolved;
            }

            foreach (OperatorBinding binding in new List<OperatorBinding>(operatorBindings.Values))
            {
                UnbindOperator(binding);
            }

            if (blueTowerBinding != null && blueTowerBinding.Tower != null)
            {
                blueTowerBinding.Tower.DamageTaken -= blueTowerBinding.Handler;
            }

            if (redTowerBinding != null && redTowerBinding.Tower != null)
            {
                redTowerBinding.Tower.DamageTaken -= redTowerBinding.Handler;
            }

            blueTowerBinding = null;
            redTowerBinding = null;
            match = null;
            roster = null;
            blueTower = null;
            redTower = null;
        }

        private void ResetStatistics()
        {
            rows.Clear();
            rowsByKey.Clear();
            operatorBindings.Clear();
            blueTowerCurrentHealth = 0f;
            blueTowerMaxHealth = 0f;
            redTowerCurrentHealth = 0f;
            redTowerMaxHealth = 0f;
            IsFrozen = false;
            Snapshot = null;
        }

        private static void ValidateRoster(OperatorRosterController configuredRoster)
        {
            const int expectedSlotCount = 6;
            const int expectedSlotsPerTeam = 3;
            if (configuredRoster.Slots.Count != expectedSlotCount)
            {
                throw new InvalidOperationException("Match statistics require exactly six roster slots.");
            }

            HashSet<string> stableKeys = new HashSet<string>(StringComparer.Ordinal);
            int blueSlots = 0;
            int redSlots = 0;
            foreach (OperatorRosterSlot slot in configuredRoster.Slots)
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.StableKey)
                    || !stableKeys.Add(slot.StableKey))
                {
                    throw new InvalidOperationException("Match statistics require six slots with unique stable keys.");
                }

                if (slot.Team == TeamId.Blue)
                {
                    blueSlots++;
                }
                else if (slot.Team == TeamId.Red)
                {
                    redSlots++;
                }
                else
                {
                    throw new InvalidOperationException("Every match statistics slot must belong to a valid team.");
                }
            }

            if (blueSlots != expectedSlotsPerTeam || redSlots != expectedSlotsPerTeam)
            {
                throw new InvalidOperationException("Match statistics require exactly three slots per team.");
            }
        }

        private sealed class RowAccumulator
        {
            public RowAccumulator(string stableKey, TeamId team, OperatorType operatorType)
            {
                StableKey = stableKey;
                Team = team;
                OperatorType = operatorType;
            }

            public string StableKey { get; }

            public TeamId Team { get; }

            public OperatorType OperatorType { get; }

            public int Kills { get; set; }

            public int Deaths { get; set; }

            public float TowerDamage { get; set; }

            public MatchResultRow ToRow()
            {
                return new MatchResultRow(StableKey, Team, OperatorType, Kills, Deaths, TowerDamage);
            }
        }

        private sealed class OperatorBinding
        {
            public OperatorBinding(OperatorRosterSlot slot, CombatUnit liveOperator, RowAccumulator row)
            {
                Slot = slot;
                Operator = liveOperator;
                Row = row;
                IsActive = true;
            }

            public OperatorRosterSlot Slot { get; }

            public CombatUnit Operator { get; }

            public RowAccumulator Row { get; }

            public Action<CombatUnit, float> Handler { get; set; }

            public bool IsActive { get; set; }

            public bool DeathRecorded { get; set; }
        }

        private sealed class TowerBinding
        {
            public TowerBinding(CombatUnit tower, TeamId team)
            {
                Tower = tower;
                Team = team;
            }

            public CombatUnit Tower { get; }

            public TeamId Team { get; }

            public Action<CombatUnit, float> Handler { get; set; }
        }
    }
}
