using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ArmOrb
{
    public sealed partial class ArmOrbGame
    {
        public CollectionProfile Collection { get; private set; }
        public RunStore Runs { get; private set; }
        public RunController Run { get; private set; }
        public bool IsRunFight { get; private set; }
        Task<RunController> runRestoring;
        public int RunWeapon;
        public BattleAbility RunAbility;
        public void ConfigureRun(){if(!IsRestoring)ui.ShowHome();}
        public void StartRun()
        {
            if(IsRestoring||Runs.Pending!=null&&Runs.Pending.phase!=RunPhase.Finished)return;
            try
            {
                if(Runs.Pending!=null){new RunController(factory,Runs,Collection,Runs.Pending);Runs.Clear();}
                Run=new RunController(factory,Runs,Collection,RunWeapon,RunAbility,random.Next(1,int.MaxValue));
                IsRunFight=false;ui.ShowHome();
            }
            catch(Exception e){Error="Не удалось начать забег: "+e.Message;ui.ShowError(Error);}
        }
        void EnsureRun()
        {if(Run==null||Run.Data.id!=Runs.Pending?.id)Run=new RunController(factory,Runs,Collection,Runs.Pending);}
        public void ResumeRun()
        {
            if(IsRestoring||Runs.Pending==null)return;
            if(IsInFight&&!SavePending())return;
            ClearFight();IsInFight=false;AtMainMenu=false;Mode=GameMode.Runs;IsRunFight=false;IsBotFight=false;Simulation=null;
            var save=Runs.Pending;runRestoring=Task.Run(()=>new RunController(factory,Runs,Collection,save));ui.ShowHome();
        }
        void CompleteRunRestore()
        {
            if(runRestoring==null||!runRestoring.IsCompleted)return;
            var task=runRestoring;runRestoring=null;
            if(task.Status!=TaskStatus.RanToCompletion){Error="Не удалось восстановить забег. Можно отказаться от попытки.";Debug.LogWarning(task.Exception);ui.ShowHome();return;}
            Run=task.Result;
            if(Run.Data.phase==RunPhase.Battle)
            {AttachRunBattle();IsPaused=true;ui.ShowPause(true);}
            else ui.ShowHome();
        }
        public void StartRunStage()
        {
            if(IsRestoring||Runs.Pending==null)return;
            try{EnsureRun();Run.StartStage();if(Run.Data.phase==RunPhase.Battle)AttachRunBattle();}
            catch(Exception e){Error="Не удалось начать этап: "+e.Message;ui.ShowError(Error);}
        }
        void AttachRunBattle()
        {
            ClearFight();IsRunFight=true;IsBotFight=true;IsBetFight=false;botResultShown=false;
            BotMatch=Run.Battle;CurrentFight=BotMatch.Data.fight.Copy();Simulation=BotMatch.Simulation;AttachFight();
        }
        void FinishRunStage()
        {
            if(botResultShown)return;botResultShown=true;IsPaused=true;
            ui.ShowRunStageResult();Debug.Log($"ArmOrb RUN STAGE id={Run.Data.id} stage={Run.Data.stage+1} phase={Run.Data.phase} t={Simulation.Elapsed:0.00} earned={Run.Data.earned}");
        }
        void RetryRunReward()
        {
            try{Run.ResolveReward();Run.Save();botResultShown=false;FinishRunStage();}
            catch(Exception e){Error="Не удалось сохранить награду: "+e.Message;ui.ShowSettlementError(Error,RetryRunReward);}
        }
        public void ChooseRunReward(int slot)
        {
            if(IsRestoring||Runs.Pending==null)return;
            try{EnsureRun();if(Run.ChooseReward(slot))ui.ShowHome();}
            catch(Exception e){Error="Не удалось сохранить выбор: "+e.Message;ui.ShowError(Error);}
        }
        public void SkipRunReward()
        {try{EnsureRun();if(Run.SkipReward())ui.ShowHome();}catch(Exception e){ui.ShowError(e.Message);}}
        public void AbandonRun()
        {
            if(IsRestoring||Runs.Pending==null)return;
            try
            {
                EnsureRun();if(Run.Active)Run.Abandon();Runs.Clear();Run=null;
                if(IsRunFight){ClearFight();IsRunFight=IsBotFight=IsInFight=IsPaused=false;Simulation=null;BotMatch=null;}
                ui.ShowHome();
            }
            catch(Exception e){Error="Не удалось завершить попытку: "+e.Message;ui.ShowError(Error);}
        }
        public void PurchaseCosmetic(string id)
        {try{if(!Collection.Purchase(id))Error="Недостаточно монет или предмет уже открыт.";else Error=null;ui.ShowHome();}catch(Exception e){ui.ShowError(e.Message);}}
        public void EquipCosmetic(string id)
        {try{Collection.Equip(id);ui.ShowHome();}catch(Exception e){ui.ShowError(e.Message);}}
    }
}
