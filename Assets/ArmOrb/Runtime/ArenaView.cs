using UnityEngine;

namespace ArmOrb
{
    public sealed class ArenaView : MonoBehaviour
    {
        public LineRenderer boundary;
        public Camera arenaCamera;
        public void Configure(float size)
        {
            float h = size * 0.5f;
            boundary.positionCount = 4; boundary.loop = true;
            boundary.SetPositions(new[] { new Vector3(-h,-h), new Vector3(-h,h), new Vector3(h,h), new Vector3(h,-h) });
            boundary.widthMultiplier = size * 0.004f;
            arenaCamera.orthographicSize = h + size * 0.025f;
        }
    }
}
