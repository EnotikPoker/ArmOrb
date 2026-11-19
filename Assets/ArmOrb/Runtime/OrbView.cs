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
        int participant=-1;
        float flash;
        readonly SpriteRenderer[] trail=new SpriteRenderer[10];
        readonly Vector2[] trailPositions=new Vector2[10];
        readonly float[] trailAges=new float[10];
        int trailCount;
        void DrawTrail(CombatantState state,float dt,Vector2 position)
        {
            if(state.Weapon.Kind!=WeaponKind.Unarmed) return;
            if(trail[0]==null) for(int i=0;i<trail.Length;i++)
            {
                var go=new GameObject("Unarmed trail");go.transform.SetParent(transform.parent,false);
                trail[i]=go.AddComponent<SpriteRenderer>();trail[i].sprite=body.sprite;trail[i].sortingOrder=2;
                trail[i].transform.localScale=body.transform.localScale;trail[i].enabled=false;
            }
            if(dt>0)
            {
                for(int i=Mathf.Min(trailCount,trail.Length-1);i>0;i--) { trailPositions[i]=trailPositions[i-1];trailAges[i]=trailAges[i-1]+dt; }
                trailPositions[0]=position;trailAges[0]=0;trailCount=Mathf.Min(trail.Length,trailCount+1);
            }
            for(int i=0;i<trailCount;i++)
            {
                trail[i].transform.position=trailPositions[i];
                Color c=color;c.a=0.24f*Mathf.Clamp01(1-trailAges[i]/0.22f);trail[i].color=c;trail[i].enabled=c.a>0;
            }
        }
        public void Configure(Color tint, float radius, WeaponView weaponPrefab, int participantIndex=-1)
        {
            color = tint; participant=participantIndex;
            body.transform.localScale = Vector3.one * (radius * 2 / body.sprite.bounds.size.x);
            shadow.transform.localScale = body.transform.localScale;
            Weapon = Instantiate(weaponPrefab, transform.parent);
        }
        public void Hit() => flash = 0.12f;
        public void Draw(CombatantState state, float radius, float dt, Vector2 position, float angle)
        {
            bool dead=state.Health.IsDead;
            body.enabled=!dead;healthLabel.gameObject.SetActive(!dead);Weapon.gameObject.SetActive(!dead);
            if(dead){foreach(var ghost in trail)if(ghost)ghost.enabled=false;return;}
            DrawTrail(state,dt,position);
            transform.position = position;
            flash = Mathf.Max(0, flash - dt);
            body.color = Color.Lerp(color, Color.white, flash / 0.12f);
            healthLabel.text = (participant>=0?"#"+(participant+1)+"\n":"")+Mathf.CeilToInt(state.Health.Current);
            healthLabel.characterSize = (participant>=0?0.065f:0.09f) * Mathf.Min(1, 3f / Mathf.CeilToInt(state.Health.Current).ToString().Length);
            Weapon.Draw(state, radius, position, angle);
        }
        public void Remove()
        {
            foreach(var ghost in trail) if(ghost) Destroy(ghost.gameObject);
            if (Weapon) Destroy(Weapon.gameObject);
            Destroy(gameObject);
        }
    }
}
