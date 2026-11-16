using UnityEngine;

namespace ArmOrb
{
    public static class OrbMotion
    {
        public static bool Move(CombatantState orb, float dt, float halfSize, float radius)
        {
            orb.Position += orb.Velocity * dt;
            float limit = halfSize - radius;
            bool hit = false;
            if (Mathf.Abs(orb.Position.x) > limit)
            {
                orb.Position.x = Mathf.Sign(orb.Position.x) * (2 * limit - Mathf.Abs(orb.Position.x));
                orb.Velocity.x = -orb.Velocity.x; hit = true;
            }
            if (Mathf.Abs(orb.Position.y) > limit)
            {
                orb.Position.y = Mathf.Sign(orb.Position.y) * (2 * limit - Mathf.Abs(orb.Position.y));
                orb.Velocity.y = -orb.Velocity.y; hit = true;
            }
            orb.Position = Vector2.Max(Vector2.one * -limit, Vector2.Min(Vector2.one * limit, orb.Position));
            return hit;
        }
        public static bool Resolve(CombatantState a, CombatantState b, float radius, float speed)
        {
            Vector2 delta = b.Position - a.Position;
            if (delta.sqrMagnitude >= 4 * radius * radius) return false;
            float distance = delta.magnitude;
            Vector2 normal = distance > 0.00001f ? delta / distance : Vector2.right;
            float correction = (2 * radius - distance) * 0.5f + 0.001f;
            a.Position -= normal * correction; b.Position += normal * correction;
            float relative = Vector2.Dot(b.Velocity - a.Velocity, normal);
            if (relative < 0)
            {
                a.Velocity += normal * relative; b.Velocity -= normal * relative;
                a.Velocity = Direction(a.Velocity, -normal) * speed;
                b.Velocity = Direction(b.Velocity, normal) * speed;
            }
            return true;
        }
        public static Vector2 Direction(Vector2 velocity, Vector2 fallback) => velocity.sqrMagnitude > 0.00001f ? velocity.normalized : fallback;
    }
}
