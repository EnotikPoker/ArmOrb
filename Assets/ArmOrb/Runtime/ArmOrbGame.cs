using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb
{
    public sealed class ArmOrbGame : MonoBehaviour
    {
        public ArenaSettings settings;
        public WeaponDefinition[] weapons;
        public OrbView orbPrefab;
        public Camera arenaCamera;
        public ArenaView arenaView;
        public UIDocument document;
        public StyleSheet styleSheet;
        public Font uiFont;
        public CombatFeedback feedback;
        public ArenaSimulation Simulation { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsInFight { get; private set; }
        public MatchOptions Options { get; } = new MatchOptions();
        [Tooltip("Show the actual opaque-pixel hitboxes in Scene/Game view with Gizmos enabled.")]
        public bool showHitboxes;
        public int[] Selection { get; } = { 0, 1 };
        public int[] Health { get; } = { 100, 100 };
        public int LastSeed { get; private set; }
        public RenderTexture ArenaTexture { get; private set; }
        readonly OrbView[] views = new OrbView[2];
        GameInterface ui;
        SimulationClock clock;
        readonly Vector2[] previousPositions = new Vector2[2];
        readonly float[] previousAngles = new float[2];
        readonly WeaponColliderBuffer debugGeometry = new WeaponColliderBuffer();
        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
            Options.Load();
            ArenaTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32) { name = "ArmOrb Arena" };
            ArenaTexture.Create(); arenaCamera.targetTexture = ArenaTexture;
            ui = new GameInterface(this); ui.ShowSetup();
            TryAndroidQa();
        }
        public void StartFight(bool replaySeed = false)
        {
            ClearFight(); clock = new SimulationClock(settings.simulationHz); IsPaused = false; IsInFight = true;
            Options.Save(); arenaView.Configure(settings.GetSize(Options.arena));
            if (!replaySeed || LastSeed == 0) LastSeed = Environment.TickCount & int.MaxValue;
            Simulation = new ArenaSimulation(weapons[Selection[0]].CreateState(), weapons[Selection[1]].CreateState(),
                Health[0], Health[1], LastSeed, settings.GetSize(Options.arena), settings.orbRadius, settings.moveSpeed,
                Options.gravity, settings.gravity, settings.restitution, settings.minimumFloorBounce);
            Simulation.Event += OnCombatEvent;
            for (int i = 0; i < 2; i++)
            {
                views[i] = Instantiate(orbPrefab, transform);
                views[i].Configure(i == 0 ? settings.playerColor : settings.enemyColor, settings.orbRadius, weapons[Selection[i]].prefab);
                previousPositions[i] = Simulation.Orbs[i].Position; previousAngles[i] = Simulation.Orbs[i].Angle;
                views[i].Draw(Simulation.Orbs[i], settings.orbRadius, 0, previousPositions[i], previousAngles[i]);
            }
            ui.ShowFight();
            Debug.Log($"ArmOrb START {weapons[Selection[0]].kind} vs {weapons[Selection[1]].kind}, HP={Health[0]}/{Health[1]}, seed={LastSeed}");
        }
        public void BackToSetup()
        {
            IsInFight = false; IsPaused = false; ClearFight(); Simulation = null; ui.ShowSetup();
        }
        public void TogglePause()
        {
            if (!IsInFight || Simulation.Result != MatchResult.InProgress) return;
            IsPaused = !IsPaused; ui.ShowPause(IsPaused);
        }
        public void ToggleSound() { feedback.ToggleMute(); ui.RefreshSound(); }
        public void SetPlaybackSpeed(float speed)
        {
            Options.playbackSpeed = MatchOptions.ValidSpeed(speed); Options.Save(); ui.RefreshSpeed();
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsInFight && Simulation.Result == MatchResult.InProgress) TogglePause();
                else if (IsInFight) BackToSetup();
            }
            ui?.UpdateSafeArea();
            if (!IsInFight || Simulation == null) return;
            if (!IsPaused && Simulation.Result == MatchResult.InProgress)
            {
                int ticks = clock.Consume(Mathf.Min(Time.unscaledDeltaTime, 0.25f), Options.playbackSpeed);
                for (int tick = 0; tick < ticks && Simulation.Result == MatchResult.InProgress; tick++)
                {
                    for (int i = 0; i < 2; i++) { previousPositions[i] = Simulation.Orbs[i].Position; previousAngles[i] = Simulation.Orbs[i].Angle; }
                    Simulation.Step((float)clock.StepSeconds);
                }
            }
            float alpha = IsPaused || Simulation.Result != MatchResult.InProgress ? 1 : clock.Interpolation;
            for (int i = 0; i < 2; i++)
                views[i].Draw(Simulation.Orbs[i], settings.orbRadius, IsPaused ? 0 : Time.unscaledDeltaTime,
                    Vector2.Lerp(previousPositions[i], Simulation.Orbs[i].Position, alpha),
                    Mathf.LerpAngle(previousAngles[i], Simulation.Orbs[i].Angle, alpha));
            feedback.Draw(IsPaused ? 0 : Time.unscaledDeltaTime);
            ui.RefreshFight();
        }
        void OnCombatEvent(CombatEvent e)
        {
            if (e.Kind == CombatEventKind.Finished)
            {
                Debug.Log($"ArmOrb END {Simulation.Result}, t={Simulation.Elapsed:0.00}, HP={Simulation.Orbs[0].Health.Current:0.#}/{Simulation.Orbs[1].Health.Current:0.#}");
                ui.ShowResult(); return;
            }
            feedback.Show(e, e.Side == 0 ? settings.playerColor : settings.enemyColor);
            if (e.Amount > 0 && views[e.Side]) views[e.Side].Hit();
        }
        void ClearFight()
        {
            if (Simulation != null) Simulation.Event -= OnCombatEvent;
            foreach (var view in views) if (view) view.Remove();
            feedback.Clear();
        }
        void OnApplicationPause(bool paused)
        {
            if (paused && IsInFight && !IsPaused && Simulation.Result == MatchResult.InProgress) TogglePause();
        }
        void OnDestroy()
        {
            if (ArenaTexture) { ArenaTexture.Release(); Destroy(ArenaTexture); }
        }
        // Only debug APKs accept reproducible QA setups via an explicit adb intent extra.
        void TryAndroidQa()
        {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                string value = intent.Call<string>("getStringExtra", "armorb.qa");
                if (string.IsNullOrEmpty(value)) return;
                string[] parts = value.Split(',');
                if (parts.Length < 5) return;
                Selection[0] = Mathf.Clamp(int.Parse(parts[0]), 0, 4); Selection[1] = Mathf.Clamp(int.Parse(parts[1]), 0, 4);
                Health[0] = Mathf.Clamp(int.Parse(parts[2]), 1, 99999); Health[1] = Mathf.Clamp(int.Parse(parts[3]), 1, 99999);
                LastSeed = int.Parse(parts[4]); StartFight(true);
                intent.Call<AndroidJavaObject>("removeExtra", "armorb.qa")?.Dispose();
            }
            catch (Exception e) { Debug.LogWarning("ArmOrb QA intent ignored: " + e.Message); }
#endif
        }
        void OnDrawGizmos()
        {
            if (!showHitboxes || Simulation == null) return;
            for (int i = 0; i < 2; i++)
            {
                var position = views[i] ? (Vector2)views[i].transform.position : Simulation.Orbs[i].Position;
                var angle = views[i] ? views[i].Weapon.transform.eulerAngles.z : Simulation.Orbs[i].Angle;
                Gizmos.color = i == 0 ? Color.red : Color.cyan;
                Gizmos.DrawWireSphere(position, Simulation.Radius);
                WeaponGeometry.Fill(Simulation.Orbs[i].Weapon, Simulation.Radius, position, angle, debugGeometry);
                for (int n = 0; n < debugGeometry.Count; n++)
                {
                    var b = debugGeometry.Boxes[n];
                    Gizmos.DrawLine(b.Corner(-1,-1),b.Corner(-1,1)); Gizmos.DrawLine(b.Corner(-1,1),b.Corner(1,1));
                    Gizmos.DrawLine(b.Corner(1,1),b.Corner(1,-1)); Gizmos.DrawLine(b.Corner(1,-1),b.Corner(-1,-1));
                }
            }
        }
    }
}
