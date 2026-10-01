using System;
using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public sealed class TeamExitVoteController : MonoBehaviour
    {
        private const int RequiredTeamSize = 3;

        private readonly Dictionary<string, OperatorRosterSlot> slotsByKey =
            new Dictionary<string, OperatorRosterSlot>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> votesByKey =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly List<OperatorRosterSlot> configuredSlots = new List<OperatorRosterSlot>();
        private readonly List<OperatorRosterSlot> currentTeamSlots = new List<OperatorRosterSlot>(RequiredTeamSize);

        private MatchOutcomeController matchOutcomeController;
        private bool autoApproveComputerVotes;
        private int agreeCount;
        private TeamId votingTeam;

        public bool IsConfigured { get; private set; }

        public bool HasRequestedExit { get; private set; }

        public bool IsExitApproved { get; private set; }

        public int AgreeCount => agreeCount;

        public int RequiredCount => RequiredTeamSize;

        public event Action ExitApproved;

        public void Configure(
            MatchOutcomeController matchOutcomeController,
            OperatorRosterController roster,
            bool autoApproveComputerVotes = true)
        {
            if (matchOutcomeController == null)
            {
                throw new ArgumentNullException(nameof(matchOutcomeController));
            }

            if (roster == null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            if (HasRequestedExit)
            {
                throw new InvalidOperationException("An exit vote cannot be reconfigured after it has started.");
            }

            Dictionary<string, OperatorRosterSlot> newSlotsByKey =
                new Dictionary<string, OperatorRosterSlot>(StringComparer.Ordinal);
            List<OperatorRosterSlot> newConfiguredSlots = new List<OperatorRosterSlot>();
            int blueCount = 0;
            int redCount = 0;

            foreach (OperatorRosterSlot slot in roster.Slots)
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.StableKey))
                {
                    throw new InvalidOperationException("Every exit vote seat requires a stable roster key.");
                }

                if (!newSlotsByKey.TryAdd(slot.StableKey, slot))
                {
                    throw new InvalidOperationException(
                        $"The roster contains duplicate exit vote key '{slot.StableKey}'.");
                }

                switch (slot.Team)
                {
                    case TeamId.Blue:
                        blueCount++;
                        break;
                    case TeamId.Red:
                        redCount++;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Exit vote seat '{slot.StableKey}' has an unsupported team.");
                }

                newConfiguredSlots.Add(slot);
            }

            if (blueCount != RequiredTeamSize || redCount != RequiredTeamSize)
            {
                throw new InvalidOperationException(
                    "An exit vote requires exactly three configured roster seats on each team.");
            }

            this.matchOutcomeController = matchOutcomeController;
            this.autoApproveComputerVotes = autoApproveComputerVotes;
            slotsByKey.Clear();
            configuredSlots.Clear();
            foreach (KeyValuePair<string, OperatorRosterSlot> pair in newSlotsByKey)
            {
                slotsByKey.Add(pair.Key, pair.Value);
            }

            configuredSlots.AddRange(newConfiguredSlots);
            currentTeamSlots.Clear();
            votesByKey.Clear();
            agreeCount = 0;
            votingTeam = default;
            HasRequestedExit = false;
            IsExitApproved = false;
            IsConfigured = true;
        }

        public bool RequestExit(string requesterStableKey)
        {
            if (!IsConfigured
                || HasRequestedExit
                || string.IsNullOrWhiteSpace(requesterStableKey)
                || matchOutcomeController == null
                || !matchOutcomeController.IsMatchOver
                || matchOutcomeController.Outcome == MatchOutcome.None
                || !slotsByKey.TryGetValue(requesterStableKey, out OperatorRosterSlot requester)
                || !requester.IsPlayerControlled)
            {
                return false;
            }

            votingTeam = requester.Team;
            currentTeamSlots.Clear();
            foreach (OperatorRosterSlot slot in configuredSlots)
            {
                if (slot.Team == votingTeam)
                {
                    currentTeamSlots.Add(slot);
                }
            }

            if (currentTeamSlots.Count != RequiredTeamSize)
            {
                currentTeamSlots.Clear();
                return false;
            }

            HasRequestedExit = true;
            RecordVote(requesterStableKey, true);

            if (autoApproveComputerVotes)
            {
                foreach (OperatorRosterSlot slot in currentTeamSlots)
                {
                    if (!slot.IsPlayerControlled)
                    {
                        CastVote(slot.StableKey, true);
                    }
                }
            }

            return true;
        }

        public bool CastVote(string stableKey, bool agrees)
        {
            if (!IsConfigured
                || !HasRequestedExit
                || IsExitApproved
                || matchOutcomeController == null
                || !matchOutcomeController.IsMatchOver
                || string.IsNullOrWhiteSpace(stableKey)
                || !slotsByKey.TryGetValue(stableKey, out OperatorRosterSlot voter)
                || voter.Team != votingTeam
                || !currentTeamSlots.Contains(voter)
                || votesByKey.ContainsKey(stableKey))
            {
                return false;
            }

            RecordVote(stableKey, agrees);
            return true;
        }

        private void RecordVote(string stableKey, bool agrees)
        {
            votesByKey.Add(stableKey, agrees);
            if (!agrees)
            {
                return;
            }

            agreeCount++;
            if (agreeCount == RequiredTeamSize && votesByKey.Count == RequiredTeamSize)
            {
                IsExitApproved = true;
                ExitApproved?.Invoke();
            }
        }
    }
}
