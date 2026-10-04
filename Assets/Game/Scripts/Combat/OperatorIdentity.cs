using System;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class OperatorIdentity : MonoBehaviour
    {
        [SerializeField] private string stableKey;
        [SerializeField] private TeamId team;
        [SerializeField] private OperatorType operatorType;

        public string StableKey => stableKey;

        public TeamId Team => team;

        public OperatorType OperatorType => operatorType;

        public void Configure(string stableKey, TeamId team, OperatorType operatorType)
        {
            if (string.IsNullOrWhiteSpace(stableKey))
            {
                throw new ArgumentException("Operator identities require a stable key.", nameof(stableKey));
            }

            this.stableKey = stableKey;
            this.team = team;
            this.operatorType = operatorType;
        }
    }
}
