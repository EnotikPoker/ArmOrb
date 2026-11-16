using UnityEngine;

namespace ArmOrb
{
    public enum WeaponKind { Sword, Spear, Blade, Scythe, Shield }

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

        public WeaponState CreateState() => new WeaponState(kind, damage, reach, rotationSpeed, growth, initialReflection);
    }

}
