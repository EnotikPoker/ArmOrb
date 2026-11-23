using UnityEngine;

namespace ArmOrb
{
    public sealed class ProjectileState
    {
        public int Id, Owner, Bounces, Reflections, PierceRemaining, HitMask, WallBounces;
        public WeaponKind Kind;
        public Vector2 Position, PreviousPosition, Velocity;
        public float Damage, Radius, HalfShaft, BlastRadius, WaveSpeed, Age;
        public bool Active, Special, Reflected;
        public float OriginalDamage, BounceBaseDamage;
        public BuildAttackSnapshot BuildAttack;
    }

    public sealed class WaveState
    {
        public int Id, Owner;
        public Vector2 Position;
        public float Radius, PreviousRadius, MaximumRadius, Speed, Damage;
        public bool Hit, Active, Extra, Reflected;
        public float OriginalDamage;
        public BuildAttackSnapshot BuildAttack;
        public int HitMask;
    }

    public static class ProjectileCollision
    {
        // First intersection of a moving point with an expanding circle; also used for moving bodies.
        public static float Circle(Vector2 from, Vector2 to, float radius, float expansion = 0)
        {
            float c = from.sqrMagnitude - radius * radius;
            if (c <= 0) return 0;
            Vector2 d = to - from;
            float a = d.sqrMagnitude - expansion * expansion;
            float b = 2 * (Vector2.Dot(from, d) - radius * expansion);
            return Root(a, b, c);
        }
        static float Root(float a, float b, float c)
        {
            if (Mathf.Abs(a) < 0.0000001f)
                return Mathf.Abs(b) < 0.0000001f ? float.PositiveInfinity : Valid(-c / b);
            float discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return float.PositiveInfinity;
            float root = Mathf.Sqrt(discriminant);
            return Mathf.Min(Valid((-b - root) / (2 * a)), Valid((-b + root) / (2 * a)));
        }
        static float Valid(float t) => t >= 0 && t <= 1 ? t : float.PositiveInfinity;
        public static bool Ring(Vector2 from, Vector2 to, float before, float after, float bodyRadius)
        {
            if (Mathf.Abs(from.magnitude - before) <= bodyRadius) return true;
            float growth = after - before;
            float t = Circle(from, to, before + bodyRadius, growth);
            // A body initially inside the ring can leave through its inner edge.
            if (from.magnitude < before - bodyRadius)
            {
                Vector2 d = to - from;
                float r = before - bodyRadius;
                t = Root(d.sqrMagnitude - growth * growth,
                    2 * (Vector2.Dot(from, d) - r * growth), from.sqrMagnitude - r * r);
            }
            return t <= 1;
        }
        public static float Shield(Vector2 from, Vector2 to, float radius, WeaponColliderBuffer shield)
        {
            float earliest = float.PositiveInfinity;
            for (int i = 0; i < shield.Count; i++)
            {
                var box = shield.Boxes[i];
                Vector2 a = from - box.Centre, b = to - box.Centre;
                a = new Vector2(Vector2.Dot(a, box.X), Vector2.Dot(a, box.Y));
                b = new Vector2(Vector2.Dot(b, box.X), Vector2.Dot(b, box.Y));
                earliest = Mathf.Min(earliest, RoundedRect(a, b, box.Half, radius));
            }
            return earliest;
        }
        public static float Body(Vector2 from, Vector2 to, Vector2 direction, float halfShaft, float radius)
        {
            Vector2 normal = new Vector2(-direction.y, direction.x);
            return RoundedRect(new Vector2(Vector2.Dot(from, direction), Vector2.Dot(from, normal)),
                new Vector2(Vector2.Dot(to, direction), Vector2.Dot(to, normal)), new Vector2(halfShaft, 0), radius);
        }
        static float RoundedRect(Vector2 a, Vector2 b, Vector2 half, float radius)
        {
            float earliest = Mathf.Min(Rect(a, b, half + new Vector2(radius, 0)), Rect(a, b, half + new Vector2(0, radius)));
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
            {
                Vector2 corner = new Vector2(x * half.x, y * half.y);
                earliest = Mathf.Min(earliest, Circle(a - corner, b - corner, radius));
            }
            return earliest;
        }
        static float Rect(Vector2 a, Vector2 b, Vector2 half)
        {
            Vector2 d = b - a; float lo = 0, hi = 1;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(d[axis]) < 0.0000001f) { if (Mathf.Abs(a[axis]) > half[axis]) return float.PositiveInfinity; }
                else
                {
                    float t0 = (-half[axis] - a[axis]) / d[axis], t1 = (half[axis] - a[axis]) / d[axis];
                    lo = Mathf.Max(lo, Mathf.Min(t0, t1)); hi = Mathf.Min(hi, Mathf.Max(t0, t1));
                    if (lo > hi) return float.PositiveInfinity;
                }
            }
            return lo;
        }
    }
}
