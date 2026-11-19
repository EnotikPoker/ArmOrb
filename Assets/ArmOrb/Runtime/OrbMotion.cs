using UnityEngine;

namespace ArmOrb
{
    public static class OrbMotion
    {
        public static bool Move(CombatantState orb, float dt, float halfSize, float radius,
            float gravity = 0, float restitution = 1, float minimumBounce = 0)
        {
            float limit = halfSize - radius;
            bool hit = false;
            float x = Mathf.Clamp(orb.Position.x, -limit, limit), vx = orb.Velocity.x;
            if (orb.Position.x != x && Mathf.Sign(vx) == Mathf.Sign(orb.Position.x)) { vx = -vx * restitution; hit = true; }
            x += vx * dt;
            if (x > limit) { x = 2 * limit - x; vx = -Mathf.Abs(vx) * restitution; hit = true; }
            if (x < -limit) { x = -2 * limit - x; vx = Mathf.Abs(vx) * restitution; hit = true; }
            float y = Mathf.Clamp(orb.Position.y, -limit, limit), vy = orb.Velocity.y;
            if (orb.Position.y > limit && vy > 0) { vy = -vy * restitution; hit = true; }
            if (orb.Position.y < -limit && vy < 0) { vy = Mathf.Max(-vy * restitution, minimumBounce); hit = true; }
            float remaining = dt;
            // Resolve the impact time and integrate the remaining arc: overshoot must not add bounce energy.
            for (int contact = 0; contact < 4 && remaining > 0; contact++)
            {
                float end = y + vy * remaining - 0.5f * gravity * remaining * remaining;
                if (end >= -limit && end <= limit) { y = end; vy -= gravity * remaining; remaining = 0; break; }
                float wall = end < -limit ? -limit : limit;
                float time = HitTime(y - wall, vy, -gravity, remaining);
                y = wall; vy -= gravity * time; remaining -= time;
                vy = wall < 0 ? Mathf.Max(Mathf.Abs(vy) * restitution, minimumBounce) : -Mathf.Abs(vy) * restitution;
                hit = true;
                if (time < 0.0000001f && Mathf.Abs(vy) < 0.00001f) { remaining = 0; break; }
            }
            orb.Position = new Vector2(Mathf.Clamp(x, -limit, limit), Mathf.Clamp(y, -limit, limit));
            orb.Velocity = new Vector2(vx, vy); return hit;
        }
        static float HitTime(float offset, float velocity, float acceleration, float maxTime)
        {
            if (Mathf.Abs(acceleration) < 0.000001f) return Mathf.Clamp(-offset / velocity, 0, maxTime);
            float root = Mathf.Sqrt(Mathf.Max(0, velocity * velocity - 2 * acceleration * offset));
            float a = (-velocity + root) / acceleration, b = (-velocity - root) / acceleration;
            float time = maxTime;
            if (a >= 0 && a <= time) time = a;
            if (b >= 0 && b <= time) time = b;
            return time;
        }
        public static bool Resolve(CombatantState a, CombatantState b, float radius, float speed,
            bool preserveEnergy = false, float restitution = 1, float secondSpeed = -1)
        {
            Vector2 delta = b.Position - a.Position;
            if (delta.sqrMagnitude >= 4 * radius * radius) return false;
            float distance = delta.magnitude;
            Vector2 normal = distance > 0.00001f ? delta / distance : Vector2.right;
            float correction = (2 * radius - distance) * 0.5f + 0.0001f;
            a.Position -= normal * correction; b.Position += normal * correction;
            float relative = Vector2.Dot(b.Velocity - a.Velocity, normal);
            if (relative < 0)
            {
                float impulse = relative * (1 + restitution) * 0.5f;
                a.Velocity += normal * impulse; b.Velocity -= normal * impulse;
                if (!preserveEnergy)
                {
                    a.Velocity = Direction(a.Velocity, -normal) * speed;
                    b.Velocity = Direction(b.Velocity, normal) * (secondSpeed < 0 ? speed : secondSpeed);
                }
            }
            return true;
        }
        public static Vector2 Direction(Vector2 velocity, Vector2 fallback) => velocity.sqrMagnitude > 0.00001f ? velocity.normalized : fallback;
    }
}
