using UnityEngine;

namespace ArmOrb
{
    public enum UpgradeKind { Heal, Armor, Weapon, Vulnerability, Poison }

    public sealed partial class ArenaSimulation
    {
        bool hasUpgrades;
        public bool CanUpgrade(int actor, UpgradeKind kind, int target)
        {
            if (actor < 0 || actor >= Orbs.Length || Orbs[actor].Health.IsDead) return false;
            var orb = Orbs[actor];
            return kind switch
            {
                UpgradeKind.Heal => orb.Health.Current < orb.Health.Maximum,
                UpgradeKind.Armor => orb.ArmorRanks < 3,
                UpgradeKind.Weapon => orb.WeaponRanks < 3,
                UpgradeKind.Vulnerability or UpgradeKind.Poison => target >= 0 && target < Orbs.Length && target != actor && !Orbs[target].Health.IsDead,
                _ => false
            };
        }
        public bool ApplyUpgrade(int actor, UpgradeKind kind, int target = -1)
        {
            if (!CanUpgrade(actor, kind, target)) return false;
            hasUpgrades = true;
            var orb = Orbs[actor];
            switch (kind)
            {
                case UpgradeKind.Heal: orb.Health.Heal(60); break;
                case UpgradeKind.Armor: orb.ArmorRanks++; break;
                case UpgradeKind.Weapon:
                    float oldLimit = orb.Weapon.UnarmedWorldLimit;
                    orb.WeaponRanks++; orb.Weapon.Upgrade();
                    if (orb.Weapon.Kind == WeaponKind.Unarmed && orb.Velocity.magnitude < orb.Weapon.UnarmedWorldLimit)
                        orb.Velocity = OrbMotion.Direction(orb.Velocity, Vector2.up) * Mathf.Min(orb.Weapon.UnarmedWorldLimit, orb.Velocity.magnitude + orb.Weapon.UnarmedWorldLimit - oldLimit);
                    break;
                case UpgradeKind.Vulnerability: Orbs[target].VulnerableRemaining = 20; break;
                case UpgradeKind.Poison: Orbs[target].ToxicRemaining = 20; break;
            }
            RefreshMultipliers();
            return true;
        }
        void RefreshMultipliers()
        {
            foreach (var orb in Orbs) orb.Health.DamageMultiplier = Mathf.Pow(0.9f, orb.ArmorRanks) * (orb.VulnerableRemaining > 0 ? 1.2f : 1);
        }
        void TickUpgrades(float dt)
        {
            if (!hasUpgrades) return;
            foreach (var orb in Orbs)
            {
                if (!orb.Health.IsDead && orb.ToxicRemaining > 0)
                { orb.Health.Damage(2 * Mathf.Min(dt, orb.ToxicRemaining)); lastDamage = Elapsed; }
                orb.ToxicRemaining = Mathf.Max(0, orb.ToxicRemaining - dt);
                orb.VulnerableRemaining = Mathf.Max(0, orb.VulnerableRemaining - dt);
            }
            RefreshMultipliers();
        }
    }
}
