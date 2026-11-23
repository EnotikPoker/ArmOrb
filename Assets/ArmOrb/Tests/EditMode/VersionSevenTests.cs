using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionSevenTests
    {
        static SimulationFactory Factory()
        {
            var assets=Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray();
            return new SimulationFactory(assets,AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
        }
        static BotMatchController Match(RewardMode mode=RewardMode.Gauge,int bots=2,MiniGameKind mini=MiniGameKind.Drawing,BotDifficulty difficulty=BotDifficulty.Normal)
        {
            var f=BotMatchController.CreateOffer(4,bots,10891);for(int i=0;i<f.Count;i++)f.weapons[i]=4;
            return new BotMatchController(Factory(),f,mini,difficulty,mode);
        }
        static void Run(BotMatchController m,int ticks){for(int i=0;i<ticks&&m.Active;i++)m.Tick(1f/120);}
        static void Fund(BotMatchController m,int amount){while(amount>0){int n=Math.Min(20,amount);m.Earn(n);amount-=n;}}
        static BotMatchSave Clone(BotMatchSave d)=>JsonUtility.FromJson<BotMatchSave>(JsonUtility.ToJson(d));
        static void Same(BotMatchController a,BotMatchController b)
        {
            Assert.That(JsonUtility.ToJson(b.Data),Is.EqualTo(JsonUtility.ToJson(a.Data)));
            Assert.That(b.Simulation.Projectiles.Count,Is.EqualTo(a.Simulation.Projectiles.Count));
            for(int i=0;i<a.Data.fight.Count;i++)
            {
                var x=a.Simulation.Orbs[i];var y=b.Simulation.Orbs[i];
                Assert.That(y.Position,Is.EqualTo(x.Position));Assert.That(y.Angle,Is.EqualTo(x.Angle));Assert.That(y.Health.Current,Is.EqualTo(x.Health.Current));
                Assert.That(y.VulnerableRemaining,Is.EqualTo(x.VulnerableRemaining));Assert.That(y.ToxicRemaining,Is.EqualTo(x.ToxicRemaining));Assert.That(y.WeaponRanks,Is.EqualTo(x.WeaponRanks));
            }
        }
        [TestCase(MiniGameKind.Drawing,12)] [TestCase(MiniGameKind.Tiles,8)] [TestCase(MiniGameKind.Rhythm,12)]
        public void SuccessfulTaskGivesRewardAndThreeSecondsWithoutInput(MiniGameKind kind,int expected)
        {
            var data=new MiniGameData{random=123};var model=new MiniGameModel(kind,data);int reward;
            if(kind==MiniGameKind.Drawing){data.drawingTarget=1;reward=model.SubmitDrawing(MiniGameModel.Template(1),1);}
            else if(kind==MiniGameKind.Tiles){data.tiles[0]=data.tiles[1]=data.tiles[2]=0;reward=model.SubmitChain(new[]{0,1,2});}
            else{data.clock=data.rhythmTarget*1.2f;reward=model.TapRhythm();}
            Assert.That(reward,Is.EqualTo(expected));Assert.That(model.Ready,Is.False);string board=JsonUtility.ToJson(data);
            Assert.That(model.SubmitChain(new[]{0,1,2}),Is.Zero);Assert.That(model.SubmitDrawing(MiniGameModel.Template(1),1),Is.Zero);Assert.That(model.TapRhythm(),Is.Zero);
            Assert.That(JsonUtility.ToJson(data),Is.EqualTo(board));model.Tick(2.9f);Assert.That(model.Ready,Is.False);model.Tick(.1f);Assert.That(model.Ready,Is.True);
            if(kind==MiniGameKind.Rhythm){Assert.That(data.clock,Is.Zero);Assert.That(data.rhythmTapped,Is.False);}
        }
        [Test] public void GaugeOfferWaitsForSixtyAndOnlySpendsOnSuccessfulChoice()
        {
            var m=Match();Fund(m,59);Assert.That(m.Data.offers[0].open,Is.False);m.Earn(1);
            Assert.That(m.Data.phase,Is.EqualTo(BotPhase.Choice));Assert.That(m.Data.offers[0].cards.Length,Is.EqualTo(3));Assert.That(m.Balance,Is.EqualTo(60));
            int selected=m.AutoChoice();Assert.That(m.Choose(selected),Is.True);Assert.That(m.Balance,Is.Zero);Assert.That(m.Choose(selected),Is.False);
            Assert.That(m.Data.commands.Count(c=>c.actor==0),Is.EqualTo(1));Assert.That(m.TasksReady,Is.True);
        }
        [Test] public void BattleAndDamageContinueDuringChoiceAndAutoAppliesAtSevenSeconds()
        {
            var m=Match();m.Simulation.ApplyUpgrade(1,UpgradeKind.Poison,0);Fund(m,60);int automatic=m.AutoChoice();var kind=m.Data.offers[0].cards[automatic].kind;
            var pos=m.Simulation.Orbs[0].Position;Run(m,839);Assert.That(m.Data.offers[0].open,Is.True);Assert.That(m.Simulation.Orbs[0].Position,Is.Not.EqualTo(pos));
            Assert.That(m.Simulation.Elapsed,Is.GreaterThan(6.9f));Assert.That(m.Simulation.Orbs[0].Health.Current,Is.LessThan(300));Assert.That(m.MiniGame.Data.clock,Is.Zero);
            Run(m,1);Assert.That(m.Data.offers[0].open,Is.False);Assert.That(m.Data.commands.Single(c=>c.actor==0).kind,Is.EqualTo(kind));Assert.That(m.Balance,Is.Zero);
            Assert.That(m.Choose(automatic),Is.False);Assert.That(m.TasksReady,Is.True);
        }
        [Test] public void TimedOffersAppearAtThirtyAndSixtyNotTwenty()
        {
            var m=Match(RewardMode.Timed);Fund(m,100);Run(m,2400);Assert.That(m.Data.offers[0].open,Is.False);
            Run(m,1200);Assert.That(m.Data.offers[0].open,Is.True);Assert.That(m.Data.ticks,Is.EqualTo(3600));Assert.That(m.Choose(m.AutoChoice()),Is.True);
            Run(m,3599);Assert.That(m.Data.offers[0].open,Is.False);Run(m,1);Assert.That(m.Data.offers[0].open,Is.True);Assert.That(m.Data.ticks,Is.EqualTo(7200));
        }
        [Test] public void TimedOfferWithoutMoneyIsSkippedAndResourceIsPreserved()
        {
            var m=Match(RewardMode.Timed);Fund(m,24);Run(m,3600);Assert.That(m.Data.offers[0].open,Is.False);Assert.That(m.Balance,Is.EqualTo(24));Assert.That(m.Data.phase,Is.EqualTo(BotPhase.Combat));Assert.That(m.NextOfferSeconds,Is.EqualTo(60));
        }
        [Test] public void TimedCardsGuaranteeAffordableChoiceAndDebitOnlyOnce()
        {
            var m=Match(RewardMode.Timed);Fund(m,30);Run(m,3600);Assert.That(m.Data.offers[0].open,Is.True);
            int slot=m.AutoChoice();var card=m.Data.offers[0].cards[slot];Assert.That(BotMatchController.Cost(card.kind),Is.LessThanOrEqualTo(30));
            Assert.That(m.Choose(slot),Is.True);Assert.That(m.Balance,Is.EqualTo(30-BotMatchController.Cost(card.kind)));Assert.That(m.Choose(slot),Is.False);
        }
        [Test] public void DeadTargetDisablesCardWithoutRetargetingOrSpending()
        {
            var m=Match();Fund(m,60);int slot=Array.FindIndex(m.Data.offers[0].cards,c=>c.target>=0);Assert.That(slot,Is.GreaterThanOrEqualTo(0));
            int target=m.Data.offers[0].cards[slot].target;m.Simulation.Orbs[target].Health.Damage(300);Run(m,1);
            Assert.That(m.CanChoose(0,slot),Is.False);Assert.That(m.Choose(slot),Is.False);Assert.That(m.Balance,Is.EqualTo(60));Assert.That(m.Data.offers[0].cards[slot].target,Is.EqualTo(target));
        }
        [Test] public void InvalidatedOffersCloseWithoutSpending()
        {
            var m=Match();Fund(m,60);
            // All three cards refer to the same enemy who dies while another enemy is alive.
            foreach(var card in m.Data.offers[0].cards){card.kind=UpgradeKind.Poison;card.target=1;}
            m.Simulation.Orbs[1].Health.Damage(300);Run(m,840);Assert.That(m.Data.offers[0].open,Is.False);Assert.That(m.Balance,Is.EqualTo(60));Assert.That(m.Data.commands.All(c=>c.actor!=0),Is.True);
        }
        [Test] public void PlayerDeathClosesChoiceBeforeAnyAutomaticPurchase()
        {
            var m=Match();Fund(m,60);m.Simulation.Orbs[0].Health.Damage(300);Run(m,1);
            Assert.That(m.Data.phase,Is.EqualTo(BotPhase.Finished));Assert.That(m.Data.offers.All(o=>!o.open),Is.True);Assert.That(m.Data.commands,Is.Empty);Assert.That(m.Balance,Is.EqualTo(60));
        }
        [TestCase(RewardMode.Gauge)] [TestCase(RewardMode.Timed)]
        public void ChoiceAndItsRemainingTimeRestoreWithExactFutureCombat(RewardMode mode)
        {
            var m=Match(mode,mini:MiniGameKind.Rhythm);Fund(m,60);if(mode==RewardMode.Timed)Run(m,3600);Run(m,211);
            var replay=new BotMatchController(Factory(),Clone(m.Data));Same(m,replay);Assert.That(replay.ChoiceRemaining,Is.EqualTo(m.ChoiceRemaining));
            for(int i=0;i<900;i++){m.Tick(1f/120);replay.Tick(1f/120);}Same(m,replay);
        }
        [TestCase(MiniGameKind.Drawing)] [TestCase(MiniGameKind.Tiles)] [TestCase(MiniGameKind.Rhythm)]
        public void RestAndPuzzleRestoreWithoutLosingTime(MiniGameKind kind)
        {
            var m=Match(mini:kind);m.Data.puzzle.restRemaining=3;Run(m,100);var replay=new BotMatchController(Factory(),Clone(m.Data));Same(m,replay);
            Run(m,260);Run(replay,260);Assert.That(m.TasksReady,Is.True);Same(m,replay);
        }
        [Test] public void ChoiceReturnsImmediatelyToTasksAndDoesNotQueueExtraWindows()
        {
            var m=Match();m.Data.puzzle.restRemaining=3;m.Data.essence[0]=160;m.Earn(12);Assert.That(m.Data.offers[0].open,Is.True);
            m.Choose(m.AutoChoice());Assert.That(m.TasksReady,Is.True);Assert.That(m.Balance,Is.EqualTo(112));Run(m,1);Assert.That(m.Data.offers[0].open,Is.False);
            m.Earn(8);Assert.That(m.Data.offers[0].open,Is.True);
        }
        [TestCase(BotDifficulty.Easy)] [TestCase(BotDifficulty.Normal)] [TestCase(BotDifficulty.Hard)]
        public void BotsChooseOnlyOneCardWithinSevenSeconds(BotDifficulty difficulty)
        {
            var m=Match(RewardMode.Timed,difficulty:difficulty);Run(m,3600);
            foreach(var offer in m.Data.offers.Skip(1)){Assert.That(offer.open,Is.True);Assert.That(offer.cards.Length,Is.EqualTo(3));Assert.That(offer.actionTick-offer.deadlineTick+840,Is.InRange(120,840));}
            Run(m,840);for(int actor=1;actor<m.Data.fight.Count;actor++)Assert.That(m.Data.commands.Count(c=>c.actor==actor),Is.EqualTo(1));
        }
        [Test] public void VersionSixShopMigratesWithPurchasesEssenceAndExactBattle()
        {
            string dir=Path.Combine(Path.GetTempPath(),"armorb-v07-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                var m=Match();Run(m,200);Assert.That(m.Simulation.ApplyUpgrade(0,UpgradeKind.Weapon),Is.True);
                m.Data.commands.Add(new UpgradeCommand{tick=200,actor=0,target=-1,kind=UpgradeKind.Weapon});Run(m,200);
                var fractions=(int[])m.Data.incomeRemainder.Clone();
                for(int i=1;i<m.Data.essence.Length;i++)m.Data.essence[i]+=fractions[i]/1200.0;
                m.Data.version=1;m.Data.rules="0.6";m.Data.phase=BotPhase.Choice;m.Data.offers=null;m.Data.essence[0]=43;m.Data.nextTimedTick=0;
                File.WriteAllText(Path.Combine(dir,"bot-match.json"),JsonUtility.ToJson(m.Data));var store=new BotMatchStore(dir);Assert.That(store.Pending,Is.Not.Null);
                var b=new BotMatchController(Factory(),store.Pending);Assert.That(b.Data.rewardMode,Is.EqualTo(RewardMode.Timed));Assert.That(b.Data.phase,Is.EqualTo(BotPhase.Combat));Assert.That(b.NextOfferSeconds,Is.EqualTo(30));
                Assert.That(b.Balance,Is.EqualTo(43));Assert.That(b.Data.incomeRemainder,Is.EqualTo(fractions));Assert.That(b.Simulation.Orbs[0].WeaponRanks,Is.EqualTo(1));Assert.That(b.Simulation.Orbs[0].Position,Is.EqualTo(m.Simulation.Orbs[0].Position));
                store.Save(b.Data);Assert.That(new BotMatchStore(dir).Pending.version,Is.EqualTo(2));
            }
            finally{Directory.Delete(dir,true);}
        }
        [Test] public void SaveAfterThirtySecondsInGaugeModeAndBackupBothRemainReadable()
        {
            string dir=Path.Combine(Path.GetTempPath(),"armorb-v07-"+Guid.NewGuid().ToString("N"));
            try
            {
                var m=Match();Run(m,3800);var store=new BotMatchStore(dir);store.Save(m.Data);Run(m,10);store.Save(m.Data);
                Assert.That(new BotMatchStore(dir).Pending.ticks,Is.EqualTo(3810));File.WriteAllText(Path.Combine(dir,"bot-match.json"),"broken");Assert.That(new BotMatchStore(dir).Pending.ticks,Is.EqualTo(3800));
            }
            finally{Directory.Delete(dir,true);}
        }
        [TestCase("null")] [TestCase("[]")] [TestCase("[1,2,3,4,5]")]
        public void MalformedLegacyBalanceDoesNotCrashLoading(string balance)
        {
            string dir=Path.Combine(Path.GetTempPath(),"armorb-v07-invalid-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                string json="{\"version\":1,\"rules\":\"0.6\",\"fight\":{\"weapons\":[4,4],\"health\":[300,300]},\"puzzle\":{},\"essence\":"+balance+"}";
                File.WriteAllText(Path.Combine(dir,"bot-match.json"),json);BotMatchStore store=null;
                Assert.DoesNotThrow(()=>store=new BotMatchStore(dir));Assert.That(store.Pending,Is.Null);
            }
            finally{Directory.Delete(dir,true);}
        }
        [TestCase(true,false,-1,0,true)] [TestCase(true,false,-1,1,false)]
        [TestCase(false,true,3,3,true)] [TestCase(false,true,3,0,false)] [TestCase(false,false,0,0,false)]
        public void FocusIdentifiesOnlyPlayerOrActualBet(bool bots,bool bet,int selection,int orb,bool expected)
        { Assert.That(OrbView.IsFocus(bots,bet,selection,orb),Is.EqualTo(expected)); }
        [TestCase(50.1f,"51")] [TestCase(300f,"300")] [TestCase(99999f,"99999")]
        public void OrbLabelContainsOnlyHp(float hp,string expected){Assert.That(OrbView.HealthText(hp),Is.EqualTo(expected));}
        [Test] public void ExistingVersionSixBetKeepsItsStakeAndBalance()
        {
            string dir=Path.Combine(Path.GetTempPath(),"armorb-v07-wallet-"+Guid.NewGuid().ToString("N"));
            try{var w=new WalletStore(dir);w.Place(BotMatchController.CreateOffer(0,1,7),0,100,200);w.SaveTicks(240);string id=w.Data.pending.id;w=new WalletStore(dir);Assert.That(w.Data.balance,Is.EqualTo(900));Assert.That(w.Data.pending.id,Is.EqualTo(id));Assert.That(w.Data.pending.rules,Is.EqualTo("0.6"));}
            finally{Directory.Delete(dir,true);}
        }
    }
}
