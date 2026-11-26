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
        LineRenderer focusRing, focusSeparator, buildBarrierRing;
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
        public void SetBuildVisuals(float radius, Material material)
        { buildBarrierRing=MakeRing("Build barrier",radius*1.24f,.035f,new Color32(90,170,255,255),7,material);buildBarrierRing.enabled=false; }
        LineRenderer MakeRing(string label,float radius,float width,Color tint,int order,Material material)
        {
            var go = new GameObject(label);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.loop=true;line.useWorldSpace=false;
            line.positionCount=64;line.widthMultiplier=width;line.startColor=line.endColor=tint;line.sortingOrder=order;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));}
            return line;
        }
        SpriteRenderer character;
        string cosmeticTrail="none";
        public void SetCosmetic(string skin,string selectedTrail,float radius)
        {
            cosmeticTrail=selectedTrail;
            var sprite=CosmeticCatalog.Sprite(skin);if(sprite==null)return;
            var go=new GameObject("Character cosmetic");go.transform.SetParent(transform,false);character=go.AddComponent<SpriteRenderer>();
            character.sprite=sprite;character.sortingOrder=body.sortingOrder+1;
            go.transform.localScale=Vector3.one*(radius*2/sprite.bounds.size.x);
            healthLabel.transform.localPosition+=Vector3.down*radius*.35f;
        }
        static Sprite sparkSprite,starSprite;
        static Sprite TrailSprite(bool star)
        {
            if(star&&starSprite!=null)return starSprite;if(!star&&sparkSprite!=null)return sparkSprite;
            var texture=new Texture2D(9,9,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var pixels=new Color[81];
            for(int y=0;y<9;y++)for(int x=0;x<9;x++)
            {int dx=Mathf.Abs(x-4),dy=Mathf.Abs(y-4);bool on=star?(dx+dy<=2||dx==0||dy==0):(dx+dy<=3);pixels[y*9+x]=on?Color.white:Color.clear;}
            texture.SetPixels(pixels);texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,9,9),new Vector2(.5f,.5f),9);
            if(star)starSprite=sprite;else sparkSprite=sprite;return sprite;
        }
        float flash;
        readonly SpriteRenderer[] trail=new SpriteRenderer[10];
        readonly Vector2[] trailPositions=new Vector2[10];
        readonly float[] trailAges=new float[10];
        int trailCount;
        static int budgetFrame=-1,visibleGhosts;
        void DrawTrail(CombatantState state,float dt,Vector2 position)
        {
            if(budgetFrame!=Time.frameCount){budgetFrame=Time.frameCount;visibleGhosts=0;}
            if(state.Weapon.Kind!=WeaponKind.Unarmed&&cosmeticTrail=="none")return;
            if(trail[0]==null) for(int i=0;i<trail.Length;i++)
            {
                var go=new GameObject("Unarmed trail");go.transform.SetParent(transform.parent,false);
                trail[i]=go.AddComponent<SpriteRenderer>();trail[i].sprite=cosmeticTrail=="stars"?TrailSprite(true):cosmeticTrail=="sparks"?TrailSprite(false):body.sprite;trail[i].sortingOrder=2;
                trail[i].transform.localScale=cosmeticTrail=="none"?body.transform.localScale:Vector3.one*(cosmeticTrail=="dots"?.13f:.19f);trail[i].enabled=false;
            }
            if(dt>0)
            {
                for(int i=Mathf.Min(trailCount,trail.Length-1);i>0;i--) { trailPositions[i]=trailPositions[i-1];trailAges[i]=trailAges[i-1]+dt; }
                trailPositions[0]=position;trailAges[0]=0;trailCount=Mathf.Min(trail.Length,trailCount+1);
            }
            for(int i=0;i<trailCount;i++)
            {
                trail[i].transform.position=trailPositions[i];
                if(cosmeticTrail=="stars"||cosmeticTrail=="sparks")trail[i].transform.rotation=Quaternion.Euler(0,0,i*37);
                Color c=color;c.a=(cosmeticTrail=="none"?.24f:.18f)*Mathf.Clamp01(1-trailAges[i]/(cosmeticTrail=="none"?.22f:.16f));trail[i].color=c;trail[i].enabled=c.a>0&&visibleGhosts<32;if(trail[i].enabled)visibleGhosts++;
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
        public void Draw(CombatantState state, float radius, float dt, Vector2 position, float angle, BuildFighter build = null)
        {
            bool dead=state.Health.IsDead;
            body.enabled=!dead;if(character)character.enabled=!dead;healthLabel.gameObject.SetActive(!dead);Weapon.gameObject.SetActive(!dead);
            if(focusRing){focusRing.enabled=!dead;focusSeparator.enabled=!dead;}
            if(buildBarrierRing)buildBarrierRing.enabled=!dead&&build!=null&&build.BarrierTotal>0;
            if(dead){foreach(var ghost in trail)if(ghost)ghost.enabled=false;return;}
            DrawTrail(state,dt,position);
            transform.position = position;
            flash = Mathf.Max(0, flash - dt);
            Color tint=color;
            if(build!=null)
            {
                if(build.BurnTime>0)tint=Color.Lerp(tint,new Color(1,.45f,.1f),.22f);
                if(build.HealFlash>0)tint=Color.Lerp(tint,new Color(.3f,1,.5f),build.HealFlash/.18f*.65f);
                if(build.CriticalFlash>0)tint=Color.Lerp(tint,new Color(1,.9f,.25f),build.CriticalFlash/.16f*.7f);
                if(build.Invulnerable>0)tint=Color.Lerp(tint,Color.white,.6f);
            }
            body.color = Color.Lerp(tint, Color.white, flash / 0.12f);if(character)character.color=body.color;
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
