using UnityEngine;

namespace ArmOrb
{
    public readonly struct WeaponPartPose
    {
        public readonly PixelMask Mask;
        public readonly Vector2 Offset, Size;
        public readonly bool Tiled;
        public WeaponPartPose(PixelMask mask, Vector2 offset, Vector2 size, bool tiled = false)
        { Mask = mask; Offset = offset; Size = size; Tiled = tiled; }
    }
    public readonly struct WeaponBox
    {
        public readonly Vector2 Centre, Half, X, Y;
        public WeaponBox(Vector2 centre, Vector2 half, Vector2 axis)
        { Centre = centre; Half = half; X = axis; Y = new Vector2(-axis.y, axis.x); }
        public Vector2 Corner(float x, float y) => Centre + X * Half.x * x + Y * Half.y * y;
    }
    public sealed class WeaponColliderBuffer
    {
        public readonly WeaponBox[] Boxes = new WeaponBox[256];
        public readonly WeaponPartPose[] Parts = new WeaponPartPose[2];
        public int Count;
        public Rect Bounds;
    }
    public static class WeaponGeometry
    {
        // This layout is shared by SpriteRenderers and opaque-pixel collision masks.
        public static int GetParts(WeaponState weapon, WeaponPartPose[] parts)
        {
            var shape = weapon.Shape;
            if (weapon.Kind == WeaponKind.Spear)
            {
                float shaft = Mathf.Max(0.1f, weapon.Reach - 0.85f - 0.8f);
                parts[0] = new WeaponPartPose(shape.shaft, new Vector2(0.85f,0), new Vector2(shaft,0.4f), true);
                parts[1] = new WeaponPartPose(shape.head, new Vector2(0.85f+shaft,0), new Vector2(0.8f,1));
                return 2;
            }
            Vector2 offset = new Vector2(0.85f,0);
            Vector2 size = new Vector2(weapon.Reach - 0.85f, weapon.Kind == WeaponKind.Scythe ? 3 : 1.6f);
            if (weapon.Kind == WeaponKind.Shield) { offset.x = 1.1f; size = new Vector2(0.75f,2.1f); }
            parts[0] = new WeaponPartPose(shape.artwork, offset, size); return 1;
        }
        public static void Fill(CombatantState orb, float radius, WeaponColliderBuffer buffer)
            => Fill(orb.Weapon, radius, orb.Position, orb.Angle, buffer);
        public static void Fill(WeaponState weapon, float radius, Vector2 position, float angle, WeaponColliderBuffer buffer)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 axis = new Vector2(Mathf.Cos(radians),Mathf.Sin(radians)), normal = new Vector2(-axis.y,axis.x);
            int parts = GetParts(weapon, buffer.Parts); buffer.Count = 0;
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            for (int p=0;p<parts;p++)
            {
                var part = buffer.Parts[p];
                foreach (Rect rect in part.Mask.rectangles)
                {
                    Vector2 local = (part.Offset + Vector2.Scale(part.Size, rect.center - new Vector2(0,0.5f))) * radius;
                    Vector2 centre = position + axis * local.x + normal * local.y;
                    Vector2 half = Vector2.Scale(part.Size, rect.size) * (radius * 0.5f);
                    var box = new WeaponBox(centre,half,axis);
                    buffer.Boxes[buffer.Count++] = box;
                    Vector2 extent = new Vector2(Mathf.Abs(axis.x)*half.x + Mathf.Abs(normal.x)*half.y,
                        Mathf.Abs(axis.y)*half.x + Mathf.Abs(normal.y)*half.y);
                    min = Vector2.Min(min, centre-extent); max = Vector2.Max(max, centre+extent);
                }
            }
            buffer.Bounds = Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        public static bool TouchesBody(WeaponColliderBuffer buffer, Vector2 centre, float radius)
        {
            if (PointRectDistance(centre,buffer.Bounds) > radius*radius) return false;
            for (int i=0;i<buffer.Count;i++)
            {
                var box = buffer.Boxes[i]; Vector2 d=centre-box.Centre;
                float x = Mathf.Max(0, Mathf.Abs(Vector2.Dot(d,box.X))-box.Half.x);
                float y = Mathf.Max(0, Mathf.Abs(Vector2.Dot(d,box.Y))-box.Half.y);
                if (x*x+y*y <= radius*radius) return true;
            }
            return false;
        }
        static float PointRectDistance(Vector2 point, Rect rect)
        {
            float x=Mathf.Max(0,Mathf.Max(rect.xMin-point.x,point.x-rect.xMax));
            float y=Mathf.Max(0,Mathf.Max(rect.yMin-point.y,point.y-rect.yMax));
            return x*x+y*y;
        }
        public static bool Clash(WeaponColliderBuffer a, WeaponColliderBuffer b)
        {
            if (a.Bounds.xMin>b.Bounds.xMax || b.Bounds.xMin>a.Bounds.xMax || a.Bounds.yMin>b.Bounds.yMax || b.Bounds.yMin>a.Bounds.yMax) return false;
            for (int i=0;i<a.Count;i++) for (int j=0;j<b.Count;j++)
                if (BoxesTouch(a.Boxes[i],b.Boxes[j])) return true;
            return false;
        }
        public static bool BoxesTouch(WeaponBox a, WeaponBox b)
        {
            Vector2 d=b.Centre-a.Centre;
            float c=Mathf.Abs(Vector2.Dot(a.X,b.X)), s=Mathf.Abs(Vector2.Dot(a.X,b.Y));
            return Mathf.Abs(Vector2.Dot(d,a.X)) <= a.Half.x+b.Half.x*c+b.Half.y*s &&
                Mathf.Abs(Vector2.Dot(d,a.Y)) <= a.Half.y+b.Half.x*s+b.Half.y*c &&
                Mathf.Abs(Vector2.Dot(d,b.X)) <= b.Half.x+a.Half.x*c+a.Half.y*s &&
                Mathf.Abs(Vector2.Dot(d,b.Y)) <= b.Half.y+a.Half.x*s+a.Half.y*c;
        }
    }
}
