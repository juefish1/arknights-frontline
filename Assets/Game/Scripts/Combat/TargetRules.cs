using ArknightsFrontline.Common;

namespace ArknightsFrontline.Combat
{
    public static class TargetRules
    {
        public static bool IsLegal(CombatUnit attacker, CombatUnit candidate)
        {
            if (attacker == null || candidate == null || attacker == candidate)
            {
                return false;
            }

            if (attacker.Team == candidate.Team || attacker.IsDead || candidate.IsDead)
            {
                return false;
            }

            return candidate.Altitude == Altitude.Ground
                ? attacker.CanAttackGround
                : candidate.Altitude == Altitude.Air && attacker.CanAttackAir;
        }
    }
}
