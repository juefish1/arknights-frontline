using System;
using System.Globalization;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using UnityEngine;

namespace ArknightsFrontline.Skills
{
    [DisallowMultipleComponent]
    public sealed class PlayerDeploymentPresenter : MonoBehaviour
    {
        [SerializeField] private OperatorRosterController roster;
        [SerializeField] private string playerSlotKey;
        [SerializeField] private SkillHudPresenter skillHud;
        [SerializeField] private MobaCameraController cameraController;
        [SerializeField] private MatchOutcomeController match;

        private OperatorRosterSlot playerSlot;
        private CombatUnit currentOperator;
        private OperatorRetreatController currentRetreat;
        private bool isConfigured;

        public CombatUnit CurrentOperator => currentOperator;

        public string FeedbackText { get; private set; } = string.Empty;

        private void Awake()
        {
            if (roster != null && skillHud != null && cameraController != null)
            {
                Configure(roster, playerSlotKey, skillHud, cameraController);
            }
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            Transform centeringTarget = cameraController == null ? null : cameraController.CenteringTarget;
            Unsubscribe();
            if (skillHud != null)
            {
                skillHud.BindOperator(null);
                skillHud.SetDeploymentStatus(string.Empty);
            }

            if (cameraController != null
                && (centeringTarget == transform || currentOperator != null && centeringTarget == currentOperator.transform))
            {
                cameraController.SetCenteringTarget(cameraController.transform);
            }
        }

        public void Configure(
            OperatorRosterController operatorRoster,
            string stableSlotKey,
            SkillHudPresenter hudPresenter,
            MobaCameraController mobaCamera)
        {
            if (operatorRoster == null)
            {
                throw new ArgumentNullException(nameof(operatorRoster));
            }

            if (string.IsNullOrWhiteSpace(stableSlotKey))
            {
                throw new ArgumentException("A player slot requires its stable key.", nameof(stableSlotKey));
            }

            if (hudPresenter == null)
            {
                throw new ArgumentNullException(nameof(hudPresenter));
            }

            if (mobaCamera == null)
            {
                throw new ArgumentNullException(nameof(mobaCamera));
            }

            if (isConfigured
                && roster == operatorRoster
                && string.Equals(playerSlotKey, stableSlotKey, StringComparison.Ordinal)
                && skillHud == hudPresenter
                && cameraController == mobaCamera)
            {
                ResolvePlayerSlot(throwIfInvalid: true);
                SynchronizeWithPlayerSlot();
                Refresh();
                return;
            }

            Unsubscribe();
            ClearCurrentBinding();
            if (skillHud != null)
            {
                skillHud.BindOperator(null);
                skillHud.SetDeploymentStatus(string.Empty);
            }

            if (cameraController != null)
            {
                cameraController.SetCenteringTarget(transform);
            }

            roster = operatorRoster;
            playerSlotKey = stableSlotKey;
            skillHud = hudPresenter;
            cameraController = mobaCamera;
            skillHud.BindOperator(null);
            skillHud.SetDeploymentStatus(string.Empty);
            cameraController.SetCenteringTarget(transform);
            if (match == null)
            {
                match = FindFirstObjectByType<MatchOutcomeController>();
            }

            playerSlot = null;
            ResolvePlayerSlot(throwIfInvalid: true);
            roster.PlayerOperatorSpawned += OnPlayerOperatorSpawned;
            roster.PlayerOperatorDeparted += OnPlayerOperatorDeparted;
            if (match != null)
            {
                match.MatchEnding += OnMatchEnding;
            }

            isConfigured = true;
            SynchronizeWithPlayerSlot();
            Refresh();
        }

        public void Refresh()
        {
            if (skillHud == null)
            {
                return;
            }

            ResolvePlayerSlot(throwIfInvalid: false);
            SynchronizeWithPlayerSlot();
            if (playerSlot == null)
            {
                FeedbackText = string.Empty;
                skillHud.SetDeploymentStatus(FeedbackText);
                return;
            }

            if (currentOperator == null)
            {
                FeedbackText = playerSlot.IsStopped || match != null && match.IsEnding
                    ? string.Empty
                    : $"REDEPLOY {FormatSeconds(playerSlot.RedeployRemaining)}";
            }
            else if (currentRetreat != null && currentRetreat.IsGuiding)
            {
                FeedbackText = $"B RETREAT {FormatSeconds(currentRetreat.RemainingSeconds)}";
            }
            else
            {
                FeedbackText = string.Empty;
            }

            skillHud.SetDeploymentStatus(FeedbackText);
        }

        private void OnPlayerOperatorSpawned(OperatorRosterSlot slot, CombatUnit liveOperator)
        {
            if (slot == null || liveOperator == null)
            {
                return;
            }

            if (playerSlot == null && IsConfiguredPlayerSlot(slot))
            {
                playerSlot = slot;
            }

            if (slot != playerSlot || !slot.IsPlayerControlled)
            {
                return;
            }

            BindPlayerOperator(liveOperator);
        }

        private void OnPlayerOperatorDeparted(OperatorRosterSlot slot, CombatUnit departingOperator)
        {
            if (slot == null)
            {
                return;
            }

            if (playerSlot == null && IsConfiguredPlayerSlot(slot))
            {
                playerSlot = slot;
            }

            if (slot != playerSlot || !slot.IsPlayerControlled || currentOperator != departingOperator)
            {
                return;
            }

            UnsubscribeFromRetreat();
            currentOperator = null;
            skillHud.BindOperator(null);
            cameraController.SetCenteringTarget(transform);
            Refresh();
        }

        private void BindPlayerOperator(CombatUnit liveOperator)
        {
            if (currentOperator == liveOperator)
            {
                return;
            }

            UnsubscribeFromRetreat();
            currentOperator = liveOperator;
            currentRetreat = liveOperator.GetComponent<OperatorRetreatController>();
            if (currentRetreat != null)
            {
                currentRetreat.GuidanceStarted += OnRetreatStateChanged;
                currentRetreat.GuidanceInterrupted += OnRetreatStateChanged;
                currentRetreat.GuidanceCompleted += OnRetreatStateChanged;
            }

            skillHud.BindOperator(liveOperator.GetComponent<ExusiaiSkillController>());
            cameraController.SetCenteringTarget(liveOperator.transform);
            cameraController.CenterOn(liveOperator.transform);
            Refresh();
        }

        private void SynchronizeWithPlayerSlot()
        {
            if (playerSlot == null || skillHud == null || cameraController == null)
            {
                return;
            }

            if (playerSlot.CurrentOperator != null)
            {
                if (currentOperator != playerSlot.CurrentOperator)
                {
                    BindPlayerOperator(playerSlot.CurrentOperator);
                }

                return;
            }

            if (currentOperator != null)
            {
                ClearCurrentBinding();
                skillHud.BindOperator(null);
                cameraController.SetCenteringTarget(transform);
            }
        }

        private bool ResolvePlayerSlot(bool throwIfInvalid)
        {
            if (playerSlot != null)
            {
                return true;
            }

            if (roster == null)
            {
                return false;
            }

            foreach (OperatorRosterSlot candidate in roster.Slots)
            {
                if (!IsConfiguredPlayerSlot(candidate))
                {
                    continue;
                }

                if (!candidate.IsPlayerControlled)
                {
                    if (throwIfInvalid)
                    {
                        throw new ArgumentException(
                            "The stable key must identify a registered player slot.",
                            nameof(playerSlotKey));
                    }

                    return false;
                }

                playerSlot = candidate;
                return true;
            }

            return false;
        }

        private bool IsConfiguredPlayerSlot(OperatorRosterSlot candidate)
        {
            return candidate != null
                && string.Equals(candidate.StableKey, playerSlotKey, StringComparison.Ordinal);
        }

        private void OnRetreatStateChanged(CombatUnit _)
        {
            Refresh();
        }

        private void OnMatchEnding()
        {
            Refresh();
        }

        private void Unsubscribe()
        {
            if (roster != null)
            {
                roster.PlayerOperatorSpawned -= OnPlayerOperatorSpawned;
                roster.PlayerOperatorDeparted -= OnPlayerOperatorDeparted;
            }

            if (match != null)
            {
                match.MatchEnding -= OnMatchEnding;
            }

            UnsubscribeFromRetreat();
        }

        private void ClearCurrentBinding()
        {
            UnsubscribeFromRetreat();
            currentOperator = null;
        }

        private void UnsubscribeFromRetreat()
        {
            if (currentRetreat != null)
            {
                currentRetreat.GuidanceStarted -= OnRetreatStateChanged;
                currentRetreat.GuidanceInterrupted -= OnRetreatStateChanged;
                currentRetreat.GuidanceCompleted -= OnRetreatStateChanged;
            }

            currentRetreat = null;
        }

        private static string FormatSeconds(float seconds)
        {
            return Mathf.Max(0f, seconds).ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
