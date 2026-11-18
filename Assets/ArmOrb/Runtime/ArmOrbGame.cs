using System;
using System.Threading.Tasks;
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
        public bool IsBetFight { get; private set; }
        public bool IsRestoring=>restoring!=null;
        public bool ShowTest { get; private set; }
        public bool showHitboxes;
        public MatchOptions Options { get; }=new MatchOptions();
        public FightDefinition TestFight { get; private set; }=new FightDefinition();
        public FightDefinition Offer { get; private set; }
        public FightDefinition CurrentFight { get; private set; }
        public OddsQuote Quote { get; private set; }
        public OddsCalculator Calculator { get; private set; }
        public WalletStore Wallet { get; private set; }
        public string Error { get; private set; }
        public long LastPayment { get; private set; }
        public long LastStake { get; private set; }
        public int LastBetSelection { get; private set; }=-1;
        public RenderTexture ArenaTexture { get; private set; }
        public static readonly string[] SizeNames={"Маленькая · 6","Средняя · 8","Большая · 10","Очень большая · 12","Огромная · 14"};
        static readonly Color[] palette={new Color32(255,98,106,255),new Color32(0,195,204,255),new Color32(145,109,235,255),new Color32(241,171,41,255),new Color32(82,184,100,255)};
        public Color ParticipantColor(int id)=>palette[Mathf.Clamp(id,0,4)];
        public int[] Selection=>TestFight.weapons;
        public int[] Health=>TestFight.health;
        public int LastSeed=>CurrentFight?.seed??0;
        OrbView[] views=Array.Empty<OrbView>();
        Vector2[] previousPositions;
        float[] previousAngles;
        GameInterface ui;
        ProjectileView projectileView;
        SimulationClock clock;
        SimulationFactory factory;
        Task<ArenaSimulation> restoring;
        readonly System.Random random=new System.Random();
        string activeBetId;
        float saveTimer;
        readonly System.Collections.Generic.List<float> frameTimes=new System.Collections.Generic.List<float>(640);
        float perfTimer;
        void Start()
        {
            Application.targetFrameRate=60;Screen.sleepTimeout=SleepTimeout.NeverSleep;Screen.orientation=ScreenOrientation.Portrait;
            Options.Load();TestFight.arena=Options.arena;TestFight.gravity=Options.gravity;TestFight.activeMotion=Options.activeMotion;TestFight.danger=Options.danger;
            factory=new SimulationFactory(weapons,settings);
            Wallet=new WalletStore(System.IO.Path.Combine(Application.persistentDataPath,"v05"));
            ArenaTexture=new RenderTexture(1024,1024,16,RenderTextureFormat.ARGB32){name="ArmOrb Arena"};ArenaTexture.Create();arenaCamera.targetTexture=ArenaTexture;
            projectileView=new ProjectileView(transform,weapons,arenaView.boundary.sharedMaterial);
            ui=new GameInterface(this);if(Wallet.Data.pending==null)NewOffer();else ui.ShowHome();TryAndroidQa();
        }
        public void SelectMode(bool test)
        {
            if(IsRestoring)return;
            ShowTest=test;Error=null;
            if(test&&Calculator!=null){Calculator.Dispose();Calculator=null;Offer=null;Quote=null;}
            if(!test&&Offer==null&&Wallet.Data.pending==null)NewOffer();else ui.ShowHome();
        }
        public void ResizeTest(int count)
        {
            if(IsRestoring)return;
            count=Mathf.Clamp(count,2,5);var f=TestFight.Copy();Array.Resize(ref f.weapons,count);Array.Resize(ref f.health,count);
            for(int i=TestFight.Count;i<count;i++){f.weapons[i]=i%weapons.Length;f.health[i]=100;}TestFight=f;ui.ShowHome();
        }
        public void NewOffer()
        {
            if(IsRestoring||Wallet.Data.pending!=null)return;
            Calculator?.Dispose();Offer=FightDefinition.RandomOffer(random);Quote=null;Error=null;
            Calculator=new OddsCalculator(factory,Offer);ui.ShowHome();
        }
        public void Refill()
        {
            try{Wallet.Refill();Error=null;}catch(Exception e){Error="Не удалось сохранить монеты: "+e.Message;}ui.ShowHome();
        }
        public void BeginBet(int selection,long stake)
        {
            if(IsRestoring||Quote==null||Offer==null||selection<0||selection>=Offer.Count)return;
            try
            {
                if(!Wallet.Place(Offer,selection,stake,Quote.Hundredths[selection])){Error="Проверь сумму ставки.";ui.ShowHome();return;}
                LastStake=stake;LastBetSelection=selection;StartDefinition(Offer,true);Offer=null;Quote=null;
            }
            catch(Exception e){Error="Не удалось сохранить ставку: "+e.Message;ui.ShowHome();}
        }
        public void WatchOffer()
        {if(Quote!=null&&Offer!=null){StartDefinition(Offer,false);Offer=null;Quote=null;}}
        public void StartFight(bool replaySeed=false)
        {
            if(IsRestoring)return;
            var f=TestFight.Copy();f.arena=Options.arena;f.gravity=Options.gravity;f.activeMotion=Options.activeMotion;f.danger=Options.danger;
            f.seed=replaySeed&&LastSeed!=0?LastSeed:random.Next(1,int.MaxValue);StartDefinition(f,false);
        }
        void StartDefinition(FightDefinition fight,bool bet)
        {
            Calculator?.Dispose();Calculator=null;
            ClearFight();CurrentFight=fight.Copy();IsBetFight=bet;activeBetId=bet?Wallet.Data.pending.id:null;
            LastPayment=0;Error=null;Simulation=factory.Create(CurrentFight);AttachFight();
        }
        void AttachFight()
        {
            clock=new SimulationClock(settings.simulationHz);IsPaused=false;IsInFight=true;saveTimer=0;
            Options.Save();arenaView.Configure(Simulation.Size);Simulation.Event+=OnCombatEvent;
            views=new OrbView[Simulation.Orbs.Length];previousPositions=new Vector2[views.Length];previousAngles=new float[views.Length];
            for(int i=0;i<views.Length;i++)
            {
                views[i]=Instantiate(orbPrefab,transform);views[i].Configure(ParticipantColor(i),settings.orbRadius,weapons[CurrentFight.weapons[i]].prefab,i);
                previousPositions[i]=Simulation.Orbs[i].Position;previousAngles[i]=Simulation.Orbs[i].Angle;
                views[i].Draw(Simulation.Orbs[i],settings.orbRadius,0,previousPositions[i],previousAngles[i]);
            }
            frameTimes.Clear();perfTimer=0;
            ui.ShowFight();Debug.Log($"ArmOrb START n={CurrentFight.Count} weapons={string.Join(",",CurrentFight.weapons)} seed={CurrentFight.seed} bet={IsBetFight}");
        }
        public void ResumeBet()
        {
            if(Wallet.Data.pending==null||IsRestoring)return;
            ClearFight();var pending=Wallet.Data.pending;CurrentFight=pending.fight.Copy();IsBetFight=true;
            LastStake=pending.stake;LastBetSelection=pending.selection;activeBetId=pending.id;
            int ticks=pending.ticks;int frequency=factory.Frequency;var definition=CurrentFight.Copy();
            restoring=Task.Run(()=>
            {
                var sim=factory.Create(definition);for(int t=0;t<ticks&&sim.Result==MatchResult.InProgress;t++)sim.Step(1f/frequency);return sim;
            });ui.ShowHome();
        }
        public void BackToSetup()
        {
            if(IsRestoring)return;
            if(!SavePending())return;
            IsInFight=false;IsPaused=false;ClearFight();Simulation=null;ui.ShowHome();
        }
        public void TogglePause()
        {
            if(!IsInFight||Simulation.Result!=MatchResult.InProgress)return;
            IsPaused=!IsPaused;if(IsPaused)SavePending();ui.ShowPause(IsPaused);
        }
        public void ToggleSound(){feedback.ToggleMute();ui.RefreshSound();}
        public void SetPlaybackSpeed(float value){Options.playbackSpeed=MatchOptions.ValidSpeed(value);Options.Save();ui.RefreshSpeed();}
        bool SavePending()
        {
            if(!IsBetFight||Simulation==null||Wallet.Data.pending==null)return true;
            try{Wallet.SaveTicks(Simulation.TickCount);return true;}catch(Exception e){IsPaused=true;Error="Не удалось сохранить бой: "+e.Message;ui.ShowError(Error);return false;}
        }
        void Update()
        {
            ui?.UpdateSafeArea();
            if(Calculator!=null)
            {
                if(Calculator.Work.IsCompleted)
                {
                    if(Calculator.Work.Status==TaskStatus.RanToCompletion)Quote=Calculator.Work.Result;
                    else if(Calculator.Work.IsFaulted){Error="Расчёт не завершён. Попробуй другой бой.";Debug.LogException(Calculator.Work.Exception);}
                    Calculator.Dispose();Calculator=null;if(!IsInFight)ui.ShowHome();
                }
                else if(!IsInFight)ui.RefreshCalculation();
            }
            if(restoring!=null&&restoring.IsCompleted)
            {
                var task=restoring;restoring=null;
                if(task.Status==TaskStatus.RanToCompletion)
                {
                    Simulation=task.Result;AttachFight();IsPaused=true;ui.ShowPause(true);
                    if(Simulation.Result!=MatchResult.InProgress)FinishFight();
                }
                else{Error="Не удалось восстановить бой. Попробуй снова.";ui.ShowHome();}
            }
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                if(IsInFight&&Simulation.Result==MatchResult.InProgress)TogglePause();else if(IsInFight)BackToSetup();
            }
            if(!IsInFight||Simulation==null)return;
            if(!IsPaused&&Simulation.Result==MatchResult.InProgress)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                frameTimes.Add(Time.unscaledDeltaTime);perfTimer+=Time.unscaledDeltaTime;
                if(perfTimer>=10)
                {
                    frameTimes.Sort();float p95=frameTimes[Mathf.Min(frameTimes.Count-1,Mathf.FloorToInt(frameTimes.Count*0.95f))];
                    Debug.Log($"ArmOrb PERF n={Simulation.Orbs.Length} x={Options.playbackSpeed} fps={frameTimes.Count/perfTimer:0.0} p95ms={p95*1000:0.0}");frameTimes.Clear();perfTimer=0;
                }
#endif
                int ticks=clock.Consume(Mathf.Min(Time.unscaledDeltaTime,0.25f),Options.playbackSpeed);
                for(int tick=0;tick<ticks&&Simulation.Result==MatchResult.InProgress;tick++)
                {
                    for(int i=0;i<views.Length;i++){previousPositions[i]=Simulation.Orbs[i].Position;previousAngles[i]=Simulation.Orbs[i].Angle;}
                    Simulation.Step((float)clock.StepSeconds);
                }
                saveTimer+=Time.unscaledDeltaTime;if(saveTimer>=2){saveTimer=0;SavePending();}
            }
            float alpha=IsPaused||Simulation.Result!=MatchResult.InProgress?1:clock.Interpolation;
            float dt=IsPaused||Simulation.Result!=MatchResult.InProgress?0:Time.unscaledDeltaTime*Options.playbackSpeed;
            for(int i=0;i<views.Length;i++)views[i].Draw(Simulation.Orbs[i],settings.orbRadius,dt,Vector2.Lerp(previousPositions[i],Simulation.Orbs[i].Position,alpha),previousAngles[i]+Simulation.Orbs[i].AngularTravel*alpha);
            projectileView.Draw(Simulation,alpha);arenaView.DrawDanger(Simulation);feedback.Draw(dt);ui.RefreshFight();
        }
        void OnCombatEvent(CombatEvent e)
        {
            if(e.Kind==CombatEventKind.Finished){FinishFight();return;}
            feedback.Show(e,ParticipantColor(e.Side));if(e.Amount>0&&e.Side>=0&&e.Side<views.Length&&views[e.Side])views[e.Side].Hit();
        }
        void FinishFight()
        {
            try
            {
                if(IsBetFight)LastPayment=Wallet.Settle(activeBetId,Simulation.WinnerIndex);
                Debug.Log($"ArmOrb END winner={Simulation.WinnerIndex} t={Simulation.Elapsed:0.00} payment={LastPayment}");ui.ShowResult();
            }
            catch(Exception e){IsPaused=true;Error="Не удалось сохранить выплату: "+e.Message;ui.ShowSettlementError(Error,FinishFight);}
        }
        void ClearFight()
        {
            if(Simulation!=null)Simulation.Event-=OnCombatEvent;
            foreach(var view in views)if(view)view.Remove();views=Array.Empty<OrbView>();feedback.Clear();projectileView?.Clear();arenaView.ClearDanger();
        }
        void OnApplicationPause(bool paused){if(paused&&IsInFight){SavePending();if(!IsPaused&&Simulation.Result==MatchResult.InProgress)TogglePause();}}
        void OnApplicationQuit(){SavePending();Calculator?.Dispose();}
        void OnDestroy(){Calculator?.Dispose();if(ArenaTexture){ArenaTexture.Release();Destroy(ArenaTexture);}}
        readonly WeaponColliderBuffer debugGeometry=new WeaponColliderBuffer();
        void OnDrawGizmos()
        {
            if(!showHitboxes||Simulation==null)return;
            for(int i=0;i<Simulation.Orbs.Length;i++)if(!Simulation.Orbs[i].Health.IsDead)
            {
                Gizmos.color=ParticipantColor(i);var orb=Simulation.Orbs[i];
                Vector2 position=i<views.Length&&views[i]?(Vector2)views[i].transform.position:orb.Position;
                float angle=i<views.Length&&views[i]?views[i].Weapon.transform.eulerAngles.z:orb.Angle;
                Gizmos.DrawWireSphere(position,Simulation.Radius);WeaponGeometry.Fill(orb.Weapon,Simulation.Radius,position,angle,debugGeometry);
                for(int n=0;n<debugGeometry.Count;n++){var b=debugGeometry.Boxes[n];Gizmos.DrawLine(b.Corner(-1,-1),b.Corner(-1,1));Gizmos.DrawLine(b.Corner(-1,1),b.Corner(1,1));Gizmos.DrawLine(b.Corner(1,1),b.Corner(1,-1));Gizmos.DrawLine(b.Corner(1,-1),b.Corner(-1,-1));}
            }
        }
        void TryAndroidQa()
        {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
            try
            {
                using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");using var intent=activity.Call<AndroidJavaObject>("getIntent");
                string value=intent.Call<string>("getStringExtra","armorb.qa");if(string.IsNullOrEmpty(value))return;
                string[] p=value.Split(',');if(p.Length<5)return;
                ShowTest=true;TestFight.weapons[0]=Mathf.Clamp(int.Parse(p[0]),0,weapons.Length-1);TestFight.weapons[1]=Mathf.Clamp(int.Parse(p[1]),0,weapons.Length-1);
                TestFight.health[0]=Mathf.Clamp(int.Parse(p[2]),1,99999);TestFight.health[1]=Mathf.Clamp(int.Parse(p[3]),1,99999);
                TestFight.seed=int.Parse(p[4]);TestFight.arena=Options.arena;TestFight.gravity=Options.gravity;StartDefinition(TestFight,false);intent.Call("removeExtra","armorb.qa");
            }
            catch(Exception e){Debug.LogWarning("ArmOrb QA intent ignored: "+e.Message);}
#endif
        }
    }
}
