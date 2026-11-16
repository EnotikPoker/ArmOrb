using UnityEngine;

namespace ArmOrb
{
    [CreateAssetMenu(menuName = "ArmOrb/Arena settings")]
    public sealed class ArenaSettings : ScriptableObject
    {
        [Min(4)] public float size = 10;
        [Min(0.1f)] public float orbRadius = 0.55f;
        [Min(0.1f)] public float moveSpeed = 3.8f;
        [Range(30, 240)] public int simulationHz = 120;
        public Color playerColor = new Color32(240, 101, 101, 255);
        public Color enemyColor = new Color32(29, 185, 190, 255);
    }
}
