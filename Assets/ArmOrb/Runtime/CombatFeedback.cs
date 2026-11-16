using UnityEngine;

namespace ArmOrb
{
    public sealed class CombatFeedback : MonoBehaviour
    {
        public Sprite disc;
        public Font font;
        public AudioSource audioSource;
        public bool Muted { get; private set; }
        sealed class Pulse
        {
            public GameObject Root;
            public SpriteRenderer Ring;
            public TextMesh Label;
            public float Time;
            public Vector3 Origin;
            public Color Color;
        }
        readonly Pulse[] pool = new Pulse[32];
        AudioClip hitSound, clashSound, bounceSound;
        int next;
        float lastSound;
        void Awake()
        {
            Muted = PlayerPrefs.GetInt("ArmOrb.Muted", 0) == 1;
            hitSound = Tone("Hit", 340, 0.07f);
            clashSound = Tone("Clash", 780, 0.09f);
            bounceSound = Tone("Bounce", 170, 0.035f);
            for (int i = 0; i < pool.Length; i++)
            {
                var root = new GameObject("Combat pulse " + i); root.transform.SetParent(transform);
                var ring = root.AddComponent<SpriteRenderer>(); ring.sprite = disc; ring.sortingOrder = 20;
                var text = new GameObject("Damage"); text.transform.SetParent(root.transform);
                var label = text.AddComponent<TextMesh>(); label.font = font; label.fontSize = 64;
                label.characterSize = 0.07f; label.anchor = TextAnchor.MiddleCenter;
                var renderer = label.GetComponent<MeshRenderer>(); renderer.sharedMaterial = font.material; renderer.sortingOrder = 21;
                pool[i] = new Pulse { Root = root, Ring = ring, Label = label };
                root.SetActive(false);
            }
        }
        public void ToggleMute()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt("ArmOrb.Muted", Muted ? 1 : 0);
            PlayerPrefs.Save();
        }
        public void Clear()
        {
            foreach (var p in pool) if (p != null) { p.Time = 0; p.Root.SetActive(false); }
        }
        public void Show(CombatEvent e, Color tint)
        {
            if (e.Kind == CombatEventKind.Finished) return;
            bool impact = e.Kind == CombatEventKind.Hit || e.Kind == CombatEventKind.Poison || e.Kind == CombatEventKind.Block;
            if (impact || e.Kind == CombatEventKind.Clash)
            {
                var p = pool[next++ % pool.Length];
                p.Root.SetActive(true); p.Time = 0.6f; p.Origin = e.Position;
                p.Color = e.Kind == CombatEventKind.Poison ? new Color(0.45f, 0.62f, 0.22f) : tint;
                p.Label.text = e.Amount > 0 ? "−" + e.Amount.ToString("0.#") : "";
                p.Label.color = p.Color;
            }
            if (!Muted && Time.unscaledTime - lastSound > 0.04f)
            {
                lastSound = Time.unscaledTime;
                audioSource.PlayOneShot(impact ? hitSound : e.Kind == CombatEventKind.Clash ? clashSound : bounceSound,
                    impact ? 0.22f : 0.065f);
            }
        }
        public void Draw(float dt)
        {
            foreach (var p in pool)
            {
                if (p.Time <= 0) continue;
                p.Time -= dt;
                if (p.Time <= 0) { p.Root.SetActive(false); continue; }
                float t = 1 - p.Time / 0.6f;
                p.Root.transform.position = p.Origin + Vector3.up * t * 0.8f;
                float scale = Mathf.Lerp(0.25f, 1.2f, t);
                p.Ring.transform.localScale = Vector3.one * scale;
                Color c = p.Color; c.a = (1 - t) * 0.18f; p.Ring.color = c;
                c.a = 1 - t; p.Label.color = c;
            }
        }
        static AudioClip Tone(string name, float frequency, float duration)
        {
            const int sampleRate = 22050;
            var samples = new float[(int)(sampleRate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate, envelope = Mathf.Pow(1 - (float)i / samples.Length, 3);
                samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2 + 2 * Mathf.Sin(t * 90)) * envelope * 0.45f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false); clip.SetData(samples, 0); return clip;
        }
        void OnDestroy()
        {
            if (hitSound) Destroy(hitSound);
            if (clashSound) Destroy(clashSound);
            if (bounceSound) Destroy(bounceSound);
        }
    }
}
