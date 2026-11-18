using UnityEngine;

namespace ArmOrb
{
    public enum WeaponKind { Sword = 0, Spear = 1, Blade = 2, Scythe = 3, Shield = 4, Unarmed = 5, Bow = 6, Staff = 7, Shuriken = 8 }

    [CreateAssetMenu(menuName = "ArmOrb/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public WeaponKind kind;
        public string displayName;
        [TextArea] public string description;
        [Min(0.01f)] public float damage = 1;
        [Tooltip("Weapon reach in orb radii, measured from its centre.")]
        [Min(1)] public float reach = 2.6f;
        [Min(1)] public float rotationSpeed = 180;
        [Tooltip("Damage, reach in radii, degrees/second, poison DPS or reflection multiplier per successful hit/block.")]
        [Min(0)] public float growth = 1;
        [Min(0)] public float initialReflection = 1;
        public Sprite sprite;
        public WeaponView prefab;
        public WeaponShapeDefinition shape;

        [Header("Ranged combat")]
        [Min(0.05f)] public float cooldown = 1;
        [Min(0.1f)] public float projectileSpeed = 14;
        [Min(0.01f)] public float blastRadius = 1.1f;
        [Min(0.1f)] public float waveSpeed = 6;
        public Sprite projectileSprite;

        public WeaponState CreateState() => new WeaponState(kind, damage, reach, rotationSpeed, growth, initialReflection, shape,
            cooldown, projectileSpeed, blastRadius, waveSpeed);
    }

}
