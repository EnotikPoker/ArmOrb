using System.Collections.Generic;
using UnityEngine;

namespace ArmOrb
{
    // Views are pooled separately from the deterministic simulation. No Unity physics is involved.
    public sealed class ProjectileView
    {
        sealed class Item
        {
            public GameObject Root;
            public SpriteRenderer Sprite;
            public LineRenderer Ring;
            public int Seen;
        }
        readonly Transform parent;
        readonly WeaponDefinition[] weapons;
        readonly Material material;
        readonly Dictionary<int, Item> active = new Dictionary<int, Item>();
        readonly Stack<Item> sprites = new Stack<Item>(), rings = new Stack<Item>();
        readonly List<int> removed = new List<int>();
        readonly Vector3[] circle = new Vector3[64];
        int frame;
        public ProjectileView(Transform parent, WeaponDefinition[] weapons, Material material)
        { this.parent = parent; this.weapons = weapons; this.material = material; }
        Item Get(int id, bool ring)
        {
            if (active.TryGetValue(id, out var item)) { item.Seen = frame; return item; }
            var pool = ring ? rings : sprites;
            if (pool.Count > 0) item = pool.Pop();
            else
            {
                item = new Item { Root = new GameObject(ring ? "Blast wave" : "Projectile") };
                item.Root.transform.SetParent(parent, false);
                if (ring)
                {
                    item.Ring = item.Root.AddComponent<LineRenderer>(); item.Ring.sharedMaterial = material;
                    item.Ring.loop = true; item.Ring.useWorldSpace = false; item.Ring.positionCount = circle.Length;
                    item.Ring.widthMultiplier = 0.045f; item.Ring.sortingOrder = 7;
                    item.Ring.startColor = item.Ring.endColor = new Color(0.96f, 0.38f, 0.12f);
                }
                else { item.Sprite = item.Root.AddComponent<SpriteRenderer>(); item.Sprite.sortingOrder = 6; }
            }
            item.Seen = frame; item.Root.SetActive(true); active.Add(id, item); return item;
        }
        public void Draw(ArenaSimulation simulation, float alpha)
        {
            frame++;
            foreach (var p in simulation.Projectiles)
            {
                var item = Get(p.Id, false);
                item.Sprite.sprite = weapons[(int)p.Kind].projectileSprite;
                item.Sprite.color=p.Reflections>0?(p.Owner==0?new Color32(255,98,106,255):new Color32(0,211,220,255)):Color.white;
                item.Root.transform.position = Vector2.Lerp(p.PreviousPosition, p.Position, alpha);
                float angle = Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg;
                item.Root.transform.rotation = Quaternion.Euler(0, 0, p.Kind == WeaponKind.Shuriken ? p.Age * 900 : angle);
                var size = item.Sprite.sprite.bounds.size;
                float height = p.Radius * 2;
                item.Root.transform.localScale = new Vector3(height / size.y, height / size.y, 1);
            }
            foreach (var w in simulation.Waves)
            {
                var item = Get(w.Id, true); item.Root.transform.position = w.Position;
                float radius = Mathf.Lerp(w.PreviousRadius, w.Radius, alpha);
                for (int i = 0; i < circle.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / circle.Length;
                    circle[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                item.Ring.SetPositions(circle);
            }
            removed.Clear();
            foreach (var pair in active) if (pair.Value.Seen != frame) removed.Add(pair.Key);
            foreach (int id in removed) Release(id);
        }
        void Release(int id)
        {
            var item = active[id]; item.Root.SetActive(false);
            (item.Ring ? rings : sprites).Push(item); active.Remove(id);
        }
        public void Clear()
        {
            removed.Clear(); foreach (int id in active.Keys) removed.Add(id);
            foreach (int id in removed) Release(id);
        }
    }
}
