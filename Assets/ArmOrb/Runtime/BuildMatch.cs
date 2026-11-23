using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArmOrb
{
    [Serializable] public sealed class BuildMatchSave
    {
        public int version = 3;
        public string rules = "0.8";
        public FightDefinition fight;
        public BattleVariant variant;
        public BotDifficulty difficulty;
        public BattleAbility[] abilities;
        public int frequency = 120, ticks, draftTicks, outcome, sequence, nextTimedTick;
        public bool started;
        public BotPhase phase;
        public BuildOffer[] offers;
        public List<BuildCommand> commands = new List<BuildCommand>();
        public string notice = "";
        public int noticeTick;
    }

    public sealed class BuildMatchController
    {
        public BuildMatchSave Data { get; }
        public ArenaSimulation Simulation { get; }
        public bool Active => Data.phase != BotPhase.Finished;
        public int NextOfferSeconds => Data.nextTimedTick / Data.frequency;
        public float ChoiceRemaining => Mathf.Max(0, !Data.started ? 7-Data.draftTicks/(float)Data.frequency : (Data.offers[0].deadlineTick-Data.ticks)/(float)Data.frequency);
        public string Notice => Data.ticks-Data.noticeTick < Data.frequency*3 ? Data.notice : "";
        public static FightDefinition CreateOffer(int weapon,int bots,int seed)
        {var f=BotMatchController.CreateOffer(weapon,bots,seed);f.danger=false;return f;}
        static BuildMatchSave NewSave(SimulationFactory factory,FightDefinition fight,BattleVariant variant,BotDifficulty difficulty,BattleAbility ability)
        {
            var save=new BuildMatchSave{fight=fight.Copy(),variant=variant,difficulty=difficulty,frequency=factory.Frequency,nextTimedTick=factory.Frequency*20,
                offers=new BuildOffer[fight.Count],abilities=new BattleAbility[fight.Count]};
            save.fight.danger=false;save.abilities[0]=ability;uint rng=(uint)fight.seed^0x27d4eb2f;
            for(int i=0;i<fight.Count;i++){save.offers[i]=new BuildOffer();if(i>0)save.abilities[i]=(BattleAbility)MatchRandom.Range(ref rng,2);}
            return save;
        }
        public BuildMatchController(SimulationFactory factory,FightDefinition fight,BattleVariant variant=BattleVariant.Builds,BotDifficulty difficulty=BotDifficulty.Normal,BattleAbility ability=BattleAbility.Heal)
            : this(factory,NewSave(factory,fight,variant,difficulty,ability),false) { }
        public BuildMatchController(SimulationFactory factory,BuildMatchSave save) : this(factory,save,true) { }
        BuildMatchController(SimulationFactory factory,BuildMatchSave save,bool restore)
        {
            if(save.frequency!=factory.Frequency||save.rules!="0.8"||save.fight.danger)throw new InvalidDataException("Battle rules changed");
            Data=save;Simulation=factory.Create(save.fight);Simulation.EnableBuildRules(save.variant,save.abilities);
            if(!restore){for(int i=0;i<save.fight.Count;i++)OpenOffer(i);return;}
            int command=0;
            while(true)
            {
                while(command<save.commands.Count&&save.commands[command].tick==Simulation.TickCount)Replay(save.commands[command++]);
                if(Simulation.TickCount==save.ticks)break;
                if(Simulation.Result!=MatchResult.InProgress)throw new InvalidDataException("Invalid battle tick");
                Simulation.Step(1f/save.frequency);
            }
            if(command!=save.commands.Count)throw new InvalidDataException("Invalid command time");
            for(int i=0;i<save.fight.Count;i++)if(Simulation.Builds[i].OfferOpen!=save.offers[i].open)throw new InvalidDataException("Invalid offer state");
        }
        void Replay(BuildCommand command)
        {
            bool valid=command.kind switch {
                BuildCommandKind.Upgrade=>Simulation.ApplyBuildSkill(command.actor,(BuildSkill)command.argument),
                BuildCommandKind.Ability=>Simulation.UseBuildAbility(command.actor),
                BuildCommandKind.Spark=>Simulation.CollectBuildSpark(command.actor,command.argument),
                BuildCommandKind.OfferOpen=>SetOffer(command.actor,true),
                BuildCommandKind.OfferClose=>SetOffer(command.actor,false), _=>false };
            if(!valid)throw new InvalidDataException("Invalid battle command");
        }
        bool SetOffer(int actor,bool open){if(Simulation.Builds[actor].OfferOpen==open)return false;Simulation.Builds[actor].OfferOpen=open;return true;}
        void Record(BuildCommandKind kind,int actor,int argument=0) => Data.commands.Add(new BuildCommand{tick=Data.ticks,kind=kind,actor=actor,argument=argument});
        public void Tick(float dt)
        {
            if(!Active)return;
            if(!Data.started)
            {Data.draftTicks++;if(Data.draftTicks>=Data.frequency*7)Choose(AutoChoice());return;}
            Simulation.Step(dt);Data.ticks=Simulation.TickCount;
            if(Simulation.Result!=MatchResult.InProgress||Simulation.Orbs[0].Health.IsDead)
            {
                Data.outcome=Simulation.Result==MatchResult.Draw?2:Simulation.WinnerIndex==0?1:0;Data.phase=BotPhase.Finished;
                for(int i=0;i<Data.offers.Length;i++){Data.offers[i].open=false;Simulation.Builds[i].OfferOpen=false;}return;
            }
            if(Data.ticks>=Data.nextTimedTick)
            {Data.nextTimedTick+=20*Data.frequency;for(int i=0;i<Data.fight.Count;i++)OpenOffer(i);}
            for(int i=0;i<Data.fight.Count;i++)
            {
                var offer=Data.offers[i];
                if(offer.open)
                {
                    if(Simulation.Orbs[i].Health.IsDead)CloseOffer(i);
                    else if(Data.ticks>=(i==0?offer.deadlineTick:offer.actionTick))Purchase(i,AutoChoice(i));
                }
                if(i>0&&!Simulation.Orbs[i].Health.IsDead)BotAction(i);
            }
        }
        public bool CanChoose(int actor,int slot) => Active&&actor>=0&&actor<Data.fight.Count&&Data.offers[actor].open&&slot>=0&&slot<Data.offers[actor].cards.Length&&Simulation.CanBuildSkill(actor,Data.offers[actor].cards[slot].skill);
        public bool Choose(int slot)
        {
            if(Data.started&&Data.ticks>=Data.offers[0].deadlineTick)return false;
            if(!Purchase(0,slot))return false;
            if(!Data.started)
            {
                for(int i=1;i<Data.fight.Count;i++)Purchase(i,AutoChoice(i));
                Data.started=true;Data.phase=BotPhase.Combat;
            }
            return true;
        }
        bool Purchase(int actor,int slot)
        {
            if(!CanChoose(actor,slot)){if(Data.offers[actor].open&&slot<0)CloseOffer(actor);return false;}
            var skill=Data.offers[actor].cards[slot].skill;
            if(!Simulation.ApplyBuildSkill(actor,skill))return false;
            Record(BuildCommandKind.Upgrade,actor,(int)skill);
            Data.notice=(actor==0?"Твой шар":$"#{actor+1}")+": "+BuildSkills.Name(skill)+$" {Simulation.Builds[actor].Level(skill)}";Data.noticeTick=Data.ticks;
            CloseOffer(actor);return true;
        }
        void CloseOffer(int actor)
        {Data.offers[actor].open=false;Simulation.Builds[actor].OfferOpen=false;Record(BuildCommandKind.OfferClose,actor);if(actor==0)Data.phase=BotPhase.Combat;}
        bool OpenOffer(int actor)
        {
            if(!Active||Data.offers[actor].open||Simulation.Orbs[actor].Health.IsDead)return false;
            var candidates=new List<BuildSkill>();
            for(int i=0;i<BuildSkills.Count;i++)if(Simulation.CanBuildSkill(actor,(BuildSkill)i))candidates.Add((BuildSkill)i);
            if(candidates.Count==0)return false;
            uint rng=(uint)Data.fight.seed ^ (0x9e3779b9u*(uint)(actor+1)) ^ (0x85ebca6bu*(uint)(Data.ticks/(20*Data.frequency)+1));
            for(int i=candidates.Count-1;i>0;i--){int j=MatchRandom.Range(ref rng,i+1);var t=candidates[i];candidates[i]=candidates[j];candidates[j]=t;}
            var chosen=new List<BuildSkill>();
            if(!Data.started)
            {
                int defense=candidates.FindIndex(BuildSkills.Defensive);if(defense>=0){chosen.Add(candidates[defense]);candidates.RemoveAt(defense);}
                if(Simulation.Orbs[actor].Weapon.Kind!=WeaponKind.Shield)
                {int attack=candidates.FindIndex(s=>!BuildSkills.Defensive(s));if(attack>=0){chosen.Add(candidates[attack]);candidates.RemoveAt(attack);}}
            }
            foreach(var skill in candidates){if(chosen.Count>=3)break;chosen.Add(skill);}
            var cards=new BuildCard[chosen.Count];for(int i=0;i<cards.Length;i++)cards[i]=new BuildCard{skill=chosen[i]};
            int delay=Data.difficulty==BotDifficulty.Easy?4:Data.difficulty==BotDifficulty.Normal?2:1;
            Data.offers[actor]=new BuildOffer{open=true,id=++Data.sequence,cards=cards,deadlineTick=Data.ticks+7*Data.frequency,actionTick=Data.ticks+delay*Data.frequency};
            Simulation.Builds[actor].OfferOpen=true;Record(BuildCommandKind.OfferOpen,actor);
            if(actor==0)Data.phase=BotPhase.Choice;return true;
        }
        public int AutoChoice(int actor=0)
        {
            var valid=new List<int>();for(int i=0;i<Data.offers[actor].cards.Length;i++)if(CanChoose(actor,i))valid.Add(i);
            if(valid.Count==0)return -1;if(actor==0)return valid[0];
            if(Data.difficulty==BotDifficulty.Easy){uint rng=(uint)Data.fight.seed^(uint)(Data.offers[actor].id*7919);return valid[MatchRandom.Range(ref rng,valid.Count)];}
            int best=valid[0];float score=Score(actor,Data.offers[actor].cards[best].skill);
            foreach(int slot in valid){float value=Score(actor,Data.offers[actor].cards[slot].skill);if(value>score){score=value;best=slot;}}
            return best;
        }
        public float Score(int actor,BuildSkill skill)
        {
            var orb=Simulation.Orbs[actor];var b=Simulation.Builds[actor];bool low=orb.Health.Current<orb.Health.Maximum*.4f;
            float score=BuildSkills.Defensive(skill)?(low?100:30):50;
            if(low&&skill is BuildSkill.Vitality or BuildSkill.Barrier or BuildSkill.SecondWind)score+=20;
            if(orb.Weapon.Kind==WeaponKind.Shield&&skill==BuildSkill.ShieldStorage)score+=50;
            if(Data.difficulty!=BotDifficulty.Hard)return score;
            if(skill==BuildSkills.Special(orb.Weapon.Kind))score+=25;
            if((skill==BuildSkill.Vampirism&&b.Has(BuildSkill.Critical))||(skill==BuildSkill.Critical&&b.Has(BuildSkill.Vampirism)))score+=25;
            if(skill is BuildSkill.Burn or BuildSkill.Bleed or BuildSkill.Suppression && orb.Weapon.Kind is WeaponKind.Blade or WeaponKind.Bow or WeaponKind.Crossbow)score+=20;
            if((skill==BuildSkill.Regeneration&&b.Has(BuildSkill.Armor))||(skill==BuildSkill.Armor&&b.Has(BuildSkill.Regeneration)))score+=20;
            if(skill==BuildSkill.Suppression)for(int i=0;i<Data.fight.Count;i++)if(i!=actor&&!Simulation.Orbs[i].Health.IsDead&&(Simulation.Builds[i].Has(BuildSkill.Regeneration)||Simulation.Builds[i].Has(BuildSkill.Vampirism))) {score+=35;break;}
            return score;
        }
        void BotAction(int actor)
        {
            var b=Simulation.Builds[actor];float reaction=Data.difficulty==BotDifficulty.Easy?1.5f:Data.difficulty==BotDifficulty.Normal?.8f:.3f;
            if(Data.variant==BattleVariant.Sparks)
            {if(b.Spark.Active&&Simulation.Elapsed-b.Spark.SpawnTime>=reaction)CollectSpark(actor,b.Spark.Id);}
            else if(Data.variant==BattleVariant.Ability&&Data.ticks%Mathf.Max(1,Mathf.RoundToInt(reaction*Data.frequency))==0)
            {
                if(b.Ability==BattleAbility.Heal ? Simulation.Orbs[actor].Health.Current<=Simulation.Orbs[actor].Health.Maximum*.6f : Simulation.BuildThreat(actor))UseAbility(actor);
            }
        }
        public bool UseAbility(int actor=0)
        {if(!Active||!Data.started||!Simulation.UseBuildAbility(actor))return false;Record(BuildCommandKind.Ability,actor);return true;}
        public bool CollectSpark(int actor,int id)
        {if(!Active||!Data.started||!Simulation.CollectBuildSpark(actor,id))return false;Record(BuildCommandKind.Spark,actor,id);return true;}
    }

    public sealed class BuildMatchStore
    {
        readonly string path, noticePath;
        public BuildMatchSave Pending { get; private set; }
        public string Error { get; private set; }
        public BuildMatchStore(string directory)
        {
            Directory.CreateDirectory(directory);path=Path.Combine(directory,"bot-match.json");noticePath=Path.Combine(directory,"v08-notice");
            ArchiveLegacy();Pending=Read(path)??Read(path+".bak");
            if(File.Exists(noticePath))Error="Режим обновлён. Начни новую битву. Предыдущая попытка сохранена в архиве.";
            else if(Pending==null&&(File.Exists(path)||File.Exists(path+".bak")))Error="Сохранённая битва повреждена. Можно начать новую.";
        }
        void ArchiveLegacy()
        {
            foreach(string suffix in new[]{"",".bak"})
            {
                string file=path+suffix;if(!File.Exists(file))continue;
                try
                {
                    var old=JsonUtility.FromJson<BuildMatchSave>(File.ReadAllText(file));
                    if(old==null||!((old.version==1&&old.rules=="0.6")||(old.version==2&&old.rules=="0.7")))continue;
                    string archive=file+".legacy-"+old.rules+"-"+DateTime.UtcNow.Ticks;File.Move(file,archive);File.WriteAllText(noticePath,"0.8");
                }
                catch(Exception e) when(e is IOException||e is ArgumentException){Error="Не удалось архивировать предыдущую попытку: "+e.Message;}
            }
        }
        public static BuildMatchSave Read(string file)
        {
            try
            {
                if(!File.Exists(file))return null;var d=JsonUtility.FromJson<BuildMatchSave>(File.ReadAllText(file));
                if(d==null||d.version!=3||d.rules!="0.8"||d.fight==null||!d.fight.IsValid()||d.fight.danger||
                    d.frequency<30||d.frequency>240||d.ticks<0||d.ticks>d.frequency*300||d.draftTicks<0||d.draftTicks>d.frequency*7||
                    (int)d.variant<0||(int)d.variant>2||(int)d.difficulty<0||(int)d.difficulty>2||(int)d.phase<0||(int)d.phase>=2||
                    d.abilities==null||d.abilities.Length!=d.fight.Count||d.offers==null||d.offers.Length!=d.fight.Count||d.commands==null||d.commands.Count>5000||
                    d.nextTimedTick<=d.ticks||d.nextTimedTick>d.ticks+20*d.frequency||(!d.started&&d.ticks!=0))return null;
                foreach(var ability in d.abilities)if((int)ability<0||(int)ability>1)return null;
                int previous=0;foreach(var c in d.commands)
                {
                    if(c==null||c.tick<previous||c.tick>d.ticks||c.actor<0||c.actor>=d.fight.Count||(int)c.kind<0||(int)c.kind>4)return null;
                    if(c.kind==BuildCommandKind.Upgrade&&(c.argument<0||c.argument>=BuildSkills.Count))return null;
                    if(c.kind==BuildCommandKind.Spark&&c.argument<1)return null;previous=c.tick;
                }
                foreach(var offer in d.offers)
                {
                    if(offer==null||offer.cards==null||offer.cards.Length>3||(offer.open&&(offer.cards.Length==0||offer.deadlineTick<d.ticks||offer.deadlineTick>d.ticks+7*d.frequency)))return null;
                    var seen=new HashSet<BuildSkill>();foreach(var card in offer.cards)if(card==null||(int)card.skill<0||(int)card.skill>=BuildSkills.Count||!seen.Add(card.skill))return null;
                }
                if((d.phase==BotPhase.Choice)!=d.offers[0].open)return null;return d;
            }
            catch(Exception e) when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException){return null;}
        }
        public void Save(BuildMatchSave save)
        {
            if(save.phase==BotPhase.Finished){Clear();return;}
            File.WriteAllText(path+".tmp",JsonUtility.ToJson(save,true));
            if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
            Pending=JsonUtility.FromJson<BuildMatchSave>(JsonUtility.ToJson(save));
            if(File.Exists(noticePath))File.Delete(noticePath);Error=null;
        }
        public void Clear(){foreach(string suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);Pending=null;}
    }
}
