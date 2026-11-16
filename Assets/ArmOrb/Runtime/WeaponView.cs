using UnityEngine;

namespace ArmOrb
{
    public sealed class WeaponView : MonoBehaviour
    {
        public SpriteRenderer artwork;
        public SpriteRenderer spearShaft;
        public SpriteRenderer spearHead;
        public void Draw(CombatantState orb, float radius)
        {
            float a = orb.Angle * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            transform.position = orb.Position + d * radius * 0.85f;
            transform.rotation = Quaternion.Euler(0, 0, orb.Angle);
            transform.localScale = Vector3.one;
            if (orb.Weapon.Kind == WeaponKind.Spear && spearShaft && spearHead)
            {
                float headLength = radius * 0.85f;
                float shaft = Mathf.Max(0.1f, radius * (orb.Weapon.Reach - 0.85f) - headLength);
                spearShaft.transform.localScale = new Vector3(shaft / spearShaft.sprite.bounds.size.x,
                    radius * 0.22f / spearShaft.sprite.bounds.size.y, 1);
                spearHead.transform.localPosition = new Vector3(shaft, 0, 0);
                spearHead.transform.localScale = new Vector3(headLength / spearHead.sprite.bounds.size.x,
                    radius * 0.95f / spearHead.sprite.bounds.size.y, 1);
                return;
            }
            float length = radius * (orb.Weapon.Reach - 0.85f);
            float height = radius * (orb.Weapon.Kind == WeaponKind.Scythe ? 2.4f : 1.15f);
            if (orb.Weapon.Kind == WeaponKind.Shield)
            {
                transform.position = orb.Position + d * radius * 1.3f;
                length = radius * 0.7f; height = radius * 2.3f;
            }
            artwork.transform.localScale = new Vector3(length / artwork.sprite.bounds.size.x,
                height / artwork.sprite.bounds.size.y, 1);
        }
    }
}
