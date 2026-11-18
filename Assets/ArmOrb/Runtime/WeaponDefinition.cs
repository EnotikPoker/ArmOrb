using UnityEngine;

namespace ArmOrb
{
    public enum WeaponKind { Sword = 0, Spear = 1, Blade = 2, Scythe = 3, Shield = 4, Unarmed = 5, Bow = 6, Staff = 7, Shuriken = 8, Crossbow = 9 }

    [CreateAssetMenu(menuName = "ArmOrb/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public WeaponKind kind;
        public string displayName;
        [TextArea] public string description;
        [Min(0)] public float damage = 1;
        [Tooltip("Weapon reach in orb radii, measured from its centre.")]
        [Min(1)] public float reach = 2.6f;
        [Min(1)] public float rotationSpeed = 180;
        [Tooltip("Damage, reach in radii, degrees/second, poison DPS or shield width per successful returned hit.")]
        [Min(0)] public float growth = 1;
        public Sprite sprite;
        public WeaponView prefab;
        public WeaponShapeDefinition shape;

        [Header("Ranged combat")]
        [Min(0.05f)] public float cooldown = 1;
        [Min(0.1f)] public float projectileSpeed = 14;
        [Min(0.01f)] public float blastRadius = 1.1f;
        [Min(0.1f)] public float waveSpeed = 6;
        [Min(0.01f)] public float burstInterval = 0.06f;
        [Min(0.1f)] public float unarmedMaxSpeed = 3;
        [Tooltip("World units per point of Speed/Damage, calibrated to the reference arena.")]
        [Min(0.1f)] public float unarmedSpeedUnit = 1.6f;
        public Sprite projectileSprite;

        public WeaponState CreateState() => new WeaponState(kind, damage, reach, rotationSpeed, growth, 1, shape,
            cooldown, projectileSpeed, blastRadius, waveSpeed, burstInterval, unarmedMaxSpeed, unarmedSpeedUnit);
    }

}
