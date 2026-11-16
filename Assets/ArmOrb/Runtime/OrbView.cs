using UnityEngine;

namespace ArmOrb
{
    public sealed class OrbView : MonoBehaviour
    {
        public SpriteRenderer body;
        public SpriteRenderer shadow;
        public TextMesh healthLabel;
        public WeaponView Weapon { get; private set; }
        Color color;
        float flash;
        public void Configure(Color tint, float radius, WeaponView weaponPrefab)
        {
            color = tint;
            body.transform.localScale = Vector3.one * (radius * 2 / body.sprite.bounds.size.x);
            shadow.transform.localScale = body.transform.localScale;
            Weapon = Instantiate(weaponPrefab, transform.parent);
        }
        public void Hit() => flash = 0.12f;
        public void Draw(CombatantState state, float radius, float dt, Vector2 position, float angle)
        {
            transform.position = position;
            flash = Mathf.Max(0, flash - dt);
            body.color = Color.Lerp(color, Color.white, flash / 0.12f);
            healthLabel.text = Mathf.CeilToInt(state.Health.Current).ToString();
            healthLabel.characterSize = 0.09f * Mathf.Min(1, 2.4f / healthLabel.text.Length);
            Weapon.Draw(state, radius, position, angle);
        }
        public void Remove()
        {
            if (Weapon) Destroy(Weapon.gameObject);
            Destroy(gameObject);
        }
    }
}
