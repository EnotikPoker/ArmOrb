using UnityEngine;

namespace ArmOrb
{
    public readonly struct WeaponSegment
    {
        public readonly Vector2 A, B;
        public readonly float Radius;
        public WeaponSegment(Vector2 a, Vector2 b, float radius) { A = a; B = b; Radius = radius; }
    }

    public static class WeaponGeometry
    {
        // Shared geometry for drawing, weapon clashes and body hits.
        public static int GetSegments(CombatantState orb, float radius, WeaponSegment[] output)
        {
            Vector2 d = new Vector2(Mathf.Cos(orb.Angle * Mathf.Deg2Rad), Mathf.Sin(orb.Angle * Mathf.Deg2Rad));
            Vector2 n = new Vector2(-d.y, d.x);
            Vector2 root = orb.Position + d * radius * 0.85f;
            Vector2 tip = orb.Position + d * radius * orb.Weapon.Reach;
            if (orb.Weapon.Kind == WeaponKind.Shield)
            {
                Vector2 c = orb.Position + d * radius * 1.65f;
                output[0] = new WeaponSegment(c - n * radius * 0.85f, c + n * radius * 0.85f, radius * 0.25f);
                return 1;
            }
            float width = orb.Weapon.Kind == WeaponKind.Spear ? 0.09f : 0.16f;
            output[0] = new WeaponSegment(root, tip, radius * width);
            if (orb.Weapon.Kind != WeaponKind.Scythe) return 1;
            Vector2 bend = tip + n * radius * 0.9f - d * radius * 0.1f;
            Vector2 hook = tip + n * radius * 1.2f - d * radius * 0.7f;
            output[1] = new WeaponSegment(tip, bend, radius * 0.18f);
            output[2] = new WeaponSegment(bend, hook, radius * 0.12f);
            return 3;
        }
        public static float PointDistanceSquared(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0000001f));
            return (p - (a + ab * t)).sqrMagnitude;
        }
        public static bool TouchesBody(WeaponSegment[] segments, int count, Vector2 centre, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                float r = radius + segments[i].Radius;
                if (PointDistanceSquared(centre, segments[i].A, segments[i].B) <= r * r) return true;
            }
            return false;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        public static float SegmentDistanceSquared(WeaponSegment a, WeaponSegment b)
        {
            Vector2 u = a.B - a.A, v = b.B - b.A, w = b.A - a.A;
            float cross = Cross(u, v);
            if (Mathf.Abs(cross) > 0.000001f)
            {
                float t = Cross(w, v) / cross, s = Cross(w, u) / cross;
                if (t >= 0 && t <= 1 && s >= 0 && s <= 1) return 0;
            }
            return Mathf.Min(PointDistanceSquared(a.A, b.A, b.B), PointDistanceSquared(a.B, b.A, b.B),
                PointDistanceSquared(b.A, a.A, a.B), PointDistanceSquared(b.B, a.A, a.B));
        }
        public static bool Clash(WeaponSegment[] a, int ac, WeaponSegment[] b, int bc)
        {
            for (int i = 0; i < ac; i++)
                for (int j = 0; j < bc; j++)
                {
                    float r = a[i].Radius + b[j].Radius;
                    if (SegmentDistanceSquared(a[i], b[j]) <= r * r) return true;
                }
            return false;
        }
    }
}
