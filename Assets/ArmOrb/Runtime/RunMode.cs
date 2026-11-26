using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArmOrb
{
    public enum RunPhase { Preview, Battle, RewardPending, RewardChoice, Finished }
    [Serializable] public sealed class RunFighterState
    {
        public int[] levels=new int[BuildSkills.Count];
        public float hp=300;
        public bool resurrectionUsed,secondWindUsed;
        public float Maximum=>300+BuildSkills.Value(levels[(int)BuildSkill.Vitality],150,250,350);
        public static bool Valid(RunFighterState s,bool allowDead=false)
        {
            if(s==null||s.levels==null||s.levels.Length!=BuildSkills.Count||float.IsNaN(s.hp)||float.IsInfinity(s.hp)||(allowDead?s.hp<0:s.hp<=0)||s.hp>s.Maximum+.001f)return false;
            for(int i=0;i<s.levels.Length;i++)if(s.levels[i]<0||s.levels[i]>BuildSkills.MaxLevel((BuildSkill)i))return false;
            return true;
        }
        public static RunFighterState Capture(ArenaSimulation sim)
        {
            var b=sim.Builds[0];return new RunFighterState{hp=sim.Orbs[0].Health.Current,levels=(int[])b.Levels.Clone(),resurrectionUsed=b.ResurrectionUsed,secondWindUsed=b.SecondWindUsed};
        }
    }
    [Serializable] public sealed class RunReward
    { public bool heal; public BuildSkill skill; }
    [Serializable] public sealed class RunData
    {
        public int version=1;
        public string rules="0.10",id;
        public int seed,weapon,stage,completed,earned,outcome;
        public BattleAbility ability;
        public RunPhase phase;
        public RunFighterState player=new RunFighterState();
        public BuildMatchSave battle;
        public bool hasBattle;
        public RunReward[] rewards=Array.Empty<RunReward>();
        public float totalSeconds;
        public int battleOrdinal;
    }
    public sealed class RunStore
    {
        readonly string path;
        public RunData Pending { get; private set; }
        public string Error { get; private set; }
        public RunStore(string directory)
        {
            Directory.CreateDirectory(directory);path=Path.Combine(directory,"run.json");Pending=Read(path)??Read(path+".bak");
            if(Pending==null&&(File.Exists(path)||File.Exists(path+".bak")))Error="Сохранение забега повреждено. Можно начать новую попытку.";
        }
        static RunData Read(string file)
        {
            try
            {
                if(!File.Exists(file))return null;var d=JsonUtility.FromJson<RunData>(File.ReadAllText(file));
                if(d==null||d.version!=1||d.rules!="0.10"||string.IsNullOrEmpty(d.id)||d.weapon<0||d.weapon>9||d.stage<0||d.stage>4||d.completed<0||d.completed>5||d.earned<0||d.earned>200||
                    (int)d.ability<0||(int)d.ability>1||(int)d.phase<0||(int)d.phase>4||float.IsNaN(d.totalSeconds)||float.IsInfinity(d.totalSeconds)||d.totalSeconds<0||d.totalSeconds>601||
                    !RunFighterState.Valid(d.player,d.phase==RunPhase.Finished)||d.rewards==null||d.rewards.Length>3)return null;
                if(d.hasBattle)
                {
                    var b=d.battle;
                    if(b==null||!b.runBattle||b.version!=4||b.rules!="0.8"||b.choiceSeconds!=8||b.fight==null||!b.fight.IsValid()||b.fight.danger||b.runStage!=d.stage||b.frequency<30||b.frequency>240||b.ticks<0||b.ticks>b.frequency*120+1||
                        !RunFighterState.Valid(b.runPlayer)||b.commands==null||b.commands.Count>5000||b.offers==null||b.offers.Length!=b.fight.Count||b.abilities==null||b.abilities.Length!=b.fight.Count)return null;
                    if(b.variant!=BattleVariant.Ability||b.fight.weapons[0]!=d.weapon||b.runFirst!=(d.stage==0)||b.draftTicks<0||b.draftTicks>b.frequency*8||b.nextTimedTick<=b.ticks||b.nextTimedTick>b.ticks+20*b.frequency||
                        (int)b.phase<0||(int)b.phase>2||!b.started&&d.stage!=0)return null;
                    foreach(var offer in b.offers)
                    {
                        if(offer==null||offer.cards==null||offer.cards.Length>3||(offer.open&&(offer.cards.Length==0||offer.deadlineTick<b.ticks||offer.deadlineTick>b.ticks+8*b.frequency)))return null;
                        var ids=new HashSet<BuildSkill>();foreach(var card in offer.cards)if(card==null||(int)card.skill<0||(int)card.skill>=BuildSkills.Count||!ids.Add(card.skill))return null;
                    }
                    if((b.phase==BotPhase.Choice)!=b.offers[0].open)return null;
                    int tick=0;foreach(var command in b.commands)
                    {if(command==null||command.tick<tick||command.tick>b.ticks||command.actor<0||command.actor>=b.fight.Count||(int)command.kind<0||(int)command.kind>4)return null;if(command.kind==BuildCommandKind.Upgrade&&(command.argument<0||command.argument>=BuildSkills.Count))return null;if(command.kind==BuildCommandKind.Spark)return null;tick=command.tick;}
                }
                else d.battle=null;
                if(d.phase==RunPhase.Battle&&!d.hasBattle)return null;
                foreach(var reward in d.rewards)if(reward==null||(int)reward.skill<0||(int)reward.skill>=BuildSkills.Count)return null;
                return d;
            }
            catch(Exception e)when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException){return null;}
        }
        public void Save(RunData data)
        {
            File.WriteAllText(path+".tmp",JsonUtility.ToJson(data,true));
            if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
            Pending=JsonUtility.FromJson<RunData>(JsonUtility.ToJson(data));if(!Pending.hasBattle)Pending.battle=null;Error=null;
        }
        public void Clear(){foreach(string suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);Pending=null;Error=null;}
    }
    public sealed class RunController
    {
        public RunData Data { get; }
        public BuildMatchController Battle { get; private set; }
        readonly SimulationFactory factory;
        readonly RunStore store;
        readonly CollectionProfile profile;
        public bool Active=>Data.phase!=RunPhase.Finished;
        public RunController(SimulationFactory factory,RunStore store,CollectionProfile profile,int weapon,BattleAbility ability,int seed)
        {
            this.factory=factory;this.store=store;this.profile=profile;
            Data=new RunData{id=Guid.NewGuid().ToString("N"),seed=seed,weapon=Mathf.Clamp(weapon,0,9),ability=ability};
            Save();profile.BeginRun(Data.id,Data.weapon);
        }
        public RunController(SimulationFactory factory,RunStore store,CollectionProfile profile,RunData data)
        {
            this.factory=factory;this.store=store;this.profile=profile;Data=data;
            profile.BeginRun(data.id,data.weapon);
            if(data.hasBattle)Battle=new BuildMatchController(factory,data.battle);
            if(data.phase==RunPhase.RewardPending)ResolveReward();
            if(data.phase==RunPhase.Finished)profile.EndRun(data.id,data.weapon,data.completed,data.totalSeconds,data.outcome==1);
        }
        public static FightDefinition StageFight(int weapon,int seed,int stage)
        {
            if(stage<0||stage>4)throw new ArgumentOutOfRangeException(nameof(stage));
            int count=new[]{2,3,2,4,2}[stage],hp=new[]{200,250,400,350,800}[stage];
            var random=new System.Random(unchecked(seed+stage*104729));
            var fight=new FightDefinition{seed=random.Next(1,int.MaxValue),weapons=new int[count],health=new int[count],danger=false,gravity=true,activeMotion=true,
                arena=new[]{ArenaSize.Medium,ArenaSize.Large,ArenaSize.Medium,ArenaSize.ExtraLarge,ArenaSize.Large}[stage]};
            fight.weapons[0]=weapon;fight.health[0]=300;
            for(int i=1;i<count;i++)
            {
                int enemy=stage==4?new[]{0,1,7}[random.Next(3)]:random.Next(10);
                if(count==2&&weapon==4&&enemy==4)enemy=0;
                fight.weapons[i]=enemy;fight.health[i]=hp;
            }
            bool allShields=true;foreach(int kind in fight.weapons)if(kind!=4)allShields=false;
            if(allShields)fight.weapons[count-1]=0;
            return fight;
        }
        public FightDefinition Preview=>StageFight(Data.weapon,Data.seed,Data.stage);
        public void StartStage()
        {
            if(Data.phase!=RunPhase.Preview)return;
            Battle=new BuildMatchController(factory,Preview,Data.player,Data.stage,Data.stage==0,Data.ability);
            Data.battle=Battle.Data;Data.hasBattle=true;Data.phase=RunPhase.Battle;Save();
        }
        public void Tick(float dt)
        {
            if(Data.phase!=RunPhase.Battle)return;
            Battle.Tick(dt);
            if(Battle.Active)return;
            Data.totalSeconds+=Battle.Simulation.Elapsed;
            if(Battle.Data.outcome!=1)
            {
                Data.player=RunFighterState.Capture(Battle.Simulation);Data.outcome=Battle.Data.outcome;Data.phase=RunPhase.Finished;Data.hasBattle=false;Data.battle=null;
                // The final build is retained, including skills gained in the failed battle.
                Save();profile.EndRun(Data.id,Data.weapon,Data.completed,Data.totalSeconds,false);return;
            }
            Data.player=RunFighterState.Capture(Battle.Simulation);Data.completed=Data.stage+1;
            Data.hasBattle=false;Data.battle=null;Data.phase=RunPhase.RewardPending;Save();ResolveReward();
        }
        public void ResolveReward()
        {
            if(Data.phase!=RunPhase.RewardPending)return;
            profile.GrantStage(Data.id,Data.stage,Data.weapon,Data.totalSeconds);
            int earned=0;for(int i=0;i<=Data.stage;i++)earned+=CollectionProfile.StageCoins(i);Data.earned=earned;
            Data.hasBattle=false;Data.battle=null;
            if(Data.stage==4){Data.outcome=1;Data.phase=RunPhase.Finished;Data.rewards=Array.Empty<RunReward>();}
            else {Data.phase=RunPhase.RewardChoice;Data.rewards=RewardOptions();}
            Save();
        }
        RunReward[] RewardOptions()
        {
            var pool=new List<BuildSkill>();
            for(int i=0;i<BuildSkills.Count;i++)if(BuildSkills.Applicable((BuildSkill)i,(WeaponKind)Data.weapon)&&Data.player.levels[i]<BuildSkills.MaxLevel((BuildSkill)i))pool.Add((BuildSkill)i);
            uint random=(uint)Data.seed^((uint)(Data.stage+1)*0x9e3779b9u);
            for(int i=pool.Count-1;i>0;i--){int j=MatchRandom.Range(ref random,i+1);var swap=pool[i];pool[i]=pool[j];pool[j]=swap;}
            var rewards=new List<RunReward>();bool heal=Data.player.hp<Data.player.Maximum-.001f;
            if(heal)rewards.Add(new RunReward{heal=true});
            foreach(var skill in pool){if(rewards.Count==3)break;rewards.Add(new RunReward{skill=skill});}
            return rewards.ToArray();
        }
        public bool ChooseReward(int slot)
        {
            if(Data.phase!=RunPhase.RewardChoice||slot<0||slot>=Data.rewards.Length)return false;
            var reward=Data.rewards[slot];
            if(reward.heal)Data.player.hp=Mathf.Min(Data.player.Maximum,Data.player.hp+Data.player.Maximum*.4f);
            else
            {
                int id=(int)reward.skill;if(Data.player.levels[id]>=BuildSkills.MaxLevel(reward.skill))return false;
                float previous=Data.player.Maximum;Data.player.levels[id]++;
                if(reward.skill==BuildSkill.Vitality)Data.player.hp+=Data.player.Maximum-previous;
            }
            NextStage();return true;
        }
        public bool SkipReward()
        {if(Data.phase!=RunPhase.RewardChoice||Data.rewards.Length!=0)return false;NextStage();return true;}
        void NextStage(){Data.stage++;Data.phase=RunPhase.Preview;Data.rewards=Array.Empty<RunReward>();Battle=null;Save();}
        public void Save(){if(Data.hasBattle&&Battle!=null)Data.battle=Battle.Data;store.Save(Data);}
        public void Abandon()
        {
            if(Data.phase==RunPhase.RewardPending)ResolveReward();
            if(Data.phase==RunPhase.Battle&&Battle!=null)Data.totalSeconds+=Battle.Simulation.Elapsed;
            Data.outcome=0;Data.phase=RunPhase.Finished;Data.hasBattle=false;Data.battle=null;Save();profile.EndRun(Data.id,Data.weapon,Data.completed,Data.totalSeconds,false);
        }
    }
}
