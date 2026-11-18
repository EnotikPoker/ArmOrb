using UnityEngine;

namespace ArmOrb
{
    [CreateAssetMenu(menuName = "ArmOrb/Arena settings")]
    public sealed class ArenaSettings : ScriptableObject
    {
        [Min(4)] public float smallSize = 6;
        [Min(4)] public float mediumSize = 8;
        [Min(4)] public float largeSize = 10;
        [Min(0.1f)] public float orbRadius = 0.55f;
        [Min(0.1f)] public float moveSpeed = 6.4f;
        [Min(0)] public float gravity = 16.7f;
        [Range(0, 1)] public float restitution = 1;
        [Range(30, 240)] public int simulationHz = 120;
        public Color playerColor = new Color32(240, 101, 101, 255);
        public Color enemyColor = new Color32(29, 185, 190, 255);
        public float GetSize(ArenaSize preset) => preset == ArenaSize.Small ? smallSize : preset == ArenaSize.Large ? largeSize : mediumSize;
    }
}
