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
        LineRenderer focusRing, focusSeparator;
        public static readonly Color FocusColor = new Color32(47,107,255,255);
        public bool HasFocus => focusRing != null;
        public static bool IsFocus(bool botFight, bool betFight, int betSelection, int id) => botFight ? id == 0 : betFight && id == betSelection;
        public static string HealthText(float health) => Mathf.CeilToInt(health).ToString();
        public void SetFocus(bool selected, float radius, float arenaSize, Material material)
        {
            if (!selected) return;
            float width = arenaSize * .006f;
            focusSeparator = MakeRing("Focus separator", radius + width*.9f, width*1.7f, new Color32(25,25,28,255), 8, material);
            focusRing = MakeRing("Selected orb", radius + width*.9f, width, FocusColor, 9, material);
        }
        LineRenderer MakeRing(string label,float radius,float width,Color tint,int order,Material material)
        {
            var go = new GameObject(label);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.loop=true;line.useWorldSpace=false;
            line.positionCount=64;line.widthMultiplier=width;line.startColor=line.endColor=tint;line.sortingOrder=order;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));}
            return line;
        }
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
            color = tint;
            body.transform.localScale = Vector3.one * (radius * 2 / body.sprite.bounds.size.x);
            shadow.transform.localScale = body.transform.localScale;
            Weapon = Instantiate(weaponPrefab, transform.parent);
        }
        public void Hit() => flash = 0.12f;
        public void Draw(CombatantState state, float radius, float dt, Vector2 position, float angle)
        {
            bool dead=state.Health.IsDead;
            body.enabled=!dead;healthLabel.gameObject.SetActive(!dead);Weapon.gameObject.SetActive(!dead);
            if(focusRing){focusRing.enabled=!dead;focusSeparator.enabled=!dead;}
            if(dead){foreach(var ghost in trail)if(ghost)ghost.enabled=false;return;}
            DrawTrail(state,dt,position);
            transform.position = position;
            flash = Mathf.Max(0, flash - dt);
            body.color = Color.Lerp(color, Color.white, flash / 0.12f);
            healthLabel.text = HealthText(state.Health.Current);
            healthLabel.characterSize = 0.09f * Mathf.Min(1, 3f / healthLabel.text.Length);
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
