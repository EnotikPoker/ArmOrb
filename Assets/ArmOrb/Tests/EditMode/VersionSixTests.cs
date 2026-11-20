using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionSixTests
    {
        static WeaponDefinition[] Assets()=>Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray();
        static SimulationFactory Factory()=>new SimulationFactory(Assets(),AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
        static BotMatchController Match(int bots=3,MiniGameKind mini=MiniGameKind.Drawing,BotDifficulty difficulty=BotDifficulty.Normal)
        {
            var f=BotMatchController.CreateOffer(4,bots,10891);for(int i=0;i<f.Count;i++)f.weapons[i]=4;
            return new BotMatchController(Factory(),f,mini,difficulty);
        }
        static void ToShop(BotMatchController m) { for(int n=0;n<2500&&m.Active;n++)m.Tick(1f/120);Assert.That(m.Data.phase,Is.EqualTo(BotPhase.Shop)); }
        static BotMatchSave Clone(BotMatchSave d)=>JsonUtility.FromJson<BotMatchSave>(JsonUtility.ToJson(d));
        static void Same(ArenaSimulation a,ArenaSimulation b)
        {
            Assert.That(b.TickCount,Is.EqualTo(a.TickCount));Assert.That(b.Projectiles.Count,Is.EqualTo(a.Projectiles.Count));Assert.That(b.Waves.Count,Is.EqualTo(a.Waves.Count));
            for(int i=0;i<a.Orbs.Length;i++)
            {
                Assert.That(b.Orbs[i].Position,Is.EqualTo(a.Orbs[i].Position));Assert.That(b.Orbs[i].Angle,Is.EqualTo(a.Orbs[i].Angle));
                Assert.That(b.Orbs[i].Health.Current,Is.EqualTo(a.Orbs[i].Health.Current));Assert.That(b.Orbs[i].Weapon.RotationSpeed,Is.EqualTo(a.Orbs[i].Weapon.RotationSpeed));
                Assert.That(b.Orbs[i].VulnerableRemaining,Is.EqualTo(a.Orbs[i].VulnerableRemaining));Assert.That(b.Orbs[i].ToxicRemaining,Is.EqualTo(a.Orbs[i].ToxicRemaining));
                Assert.That(b.Orbs[i].WeaponRanks,Is.EqualTo(a.Orbs[i].WeaponRanks));
            }
        }
        [Test] public void BladeGrowthStartsAt120AndNeverStopsOrSpeedsUpItsIncrement()
        {
            var blade=Assets()[2].CreateState();var target=new HealthState(99999);float previousDelta=121;
            for(int i=0;i<1000;i++) { float before=blade.RotationSpeed;blade.OnHit(target);float delta=blade.RotationSpeed-before;Assert.That(delta,Is.GreaterThan(0));Assert.That(delta,Is.LessThan(previousDelta));previousDelta=delta; }
            Assert.That(blade.RotationSpeed,Is.GreaterThan(12000));Assert.That(blade.Damage,Is.EqualTo(2));
        }
        [Test] public void BowKeepsRotatingAndEveryArrowUsesItsActualMuzzleAngle()
        {
            var w=Assets()[6].CreateState();w.OnHit(new HealthState(100));w.OnHit(new HealthState(100));
            var sim=new ArenaSimulation(w,Assets()[6].CreateState(),size:14,speed:0);
            sim.Orbs[0].Position=new Vector2(-4,-4);sim.Orbs[1].Position=new Vector2(4,4);sim.Orbs[0].Angle=0;sim.Orbs[0].RotationDirection=1;sim.Orbs[1].Weapon.ReloadRemaining=100;
            var angles=new List<float>();
            sim.Event+=e=> { if(e.Kind==CombatEventKind.Shot&&e.Side==0) { var p=sim.Projectiles[sim.Projectiles.Count-1];float a=Mathf.Atan2(p.Velocity.y,p.Velocity.x)*Mathf.Rad2Deg;Assert.That(Mathf.Abs(Mathf.DeltaAngle(a,sim.Orbs[0].Angle)),Is.LessThan(.001));angles.Add(a); } };
            w.ReloadRemaining=0;for(int t=0;t<20;t++)sim.Step(1f/120);
            Assert.That(angles.Count,Is.EqualTo(3));Assert.That(angles[1]-angles[0],Is.InRange(15,21));Assert.That(angles[2]-angles[1],Is.InRange(15,21));Assert.That(w.ReloadRemaining,Is.InRange(.93f,1));
        }
        [Test] public void ShurikenActuallyFiresEveryPointEightSeconds()
        {
            var sim=new ArenaSimulation(Assets()[8].CreateState(),Assets()[4].CreateState(),size:14,speed:0);
            sim.Orbs[0].Position=new Vector2(-4,-4);sim.Orbs[1].Position=new Vector2(4,4);
            var times=new List<float>();sim.Event+=e=>{if(e.Kind==CombatEventKind.Shot&&e.Side==0)times.Add(sim.Elapsed);};
            for(int t=0;t<300;t++)sim.Step(1f/120);
            Assert.That(times.Count,Is.EqualTo(3));Assert.That(times[1]-times[0],Is.EqualTo(.8f).Within(.009));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void BotOffersUseEqualHpAndScaleMap(int bots)
        {
            var f=BotMatchController.CreateOffer(7,bots,8001);Assert.That(f.Count,Is.EqualTo(bots+1));Assert.That(f.health.All(h=>h==300),Is.True);Assert.That((int)f.arena,Is.EqualTo(bots));Assert.That(f.weapons[0],Is.EqualTo(7));
            Assert.That(BotMatchController.CreateOffer(7,bots,8001).weapons,Is.EqualTo(f.weapons));
        }
        [TestCase(BotDifficulty.Easy,28)] [TestCase(BotDifficulty.Normal,40)] [TestCase(BotDifficulty.Hard,52)]
        public void ShopStopsAllClocksAndBotIncome(BotDifficulty difficulty,int expected)
        {
            var m=Match(difficulty:difficulty);ToShop(m);Assert.That(m.Data.essence[1],Is.EqualTo(expected).Within(.03));
            int ticks=m.Simulation.TickCount;double income=m.Data.essence[1];var pos=m.Simulation.Orbs[1].Position;
            for(int t=0;t<200;t++){m.Tick(1f/120);m.Earn(5);}
            Assert.That(m.Simulation.TickCount,Is.EqualTo(ticks));Assert.That(m.Data.essence[1],Is.EqualTo(income));Assert.That(m.Balance,Is.Zero);Assert.That(m.Simulation.Orbs[1].Position,Is.EqualTo(pos));Assert.That(m.Data.cards.Length,Is.EqualTo(5));
        }
        [Test] public void PurchasesDebitOnceAndRestoreDuringAndAfterShop()
        {
            var m=Match();for(int i=0;i<10;i++)m.Earn(20);ToShop(m);
            int slot=Array.FindIndex(m.Data.cards,c=>c.kind==UpgradeKind.Weapon);Assert.That(slot,Is.GreaterThanOrEqualTo(0));
            int before=m.Balance;Assert.That(m.Buy(slot),Is.True);Assert.That(m.Buy(slot),Is.False);Assert.That(m.Balance,Is.EqualTo(before-40));
            var b=new BotMatchController(Factory(),Clone(m.Data));Same(m.Simulation,b.Simulation);Assert.That(b.Data.cards[slot].sold,Is.True);Assert.That(b.Balance,Is.EqualTo(m.Balance));
            m.Continue();b.Continue();Assert.That(b.Data.purchases,Is.EqualTo(m.Data.purchases));
            for(int i=0;i<241;i++){m.Tick(1f/120);b.Tick(1f/120);}Same(m.Simulation,b.Simulation);
            var c=new BotMatchController(Factory(),Clone(m.Data));Same(m.Simulation,c.Simulation);
        }
        [Test] public void SabotageRequiresLivingEnemyAndCannotSpendOnInvalidTarget()
        {
            var m=Match();for(int i=0;i<10;i++)m.Earn(20);ToShop(m);int slot=Array.FindIndex(m.Data.cards,c=>(int)c.kind>=3);
            int before=m.Balance;Assert.That(m.Buy(slot,0),Is.False);Assert.That(m.Buy(slot,-1),Is.False);Assert.That(m.Balance,Is.EqualTo(before));Assert.That(m.Buy(slot,1),Is.True);
            var b=new BotMatchController(Factory(),Clone(m.Data));Same(m.Simulation,b.Simulation);
        }
        [Test] public void ArmorAndTemporaryPoisonRespectDurationAndDangerBypassesArmor()
        {
            var sim=Factory().Create(BotMatchController.CreateOffer(4,1,9101));
            for(int i=0;i<2;i++){sim.Orbs[i].Weapon.ReloadRemaining=1000;sim.Orbs[i].Velocity=Vector2.zero;}
            sim.ApplyUpgrade(0,UpgradeKind.Armor);sim.ApplyUpgrade(1,UpgradeKind.Poison,0);sim.ApplyUpgrade(1,UpgradeKind.Vulnerability,0);
            var hp=sim.Orbs[0].Health;float before=hp.Current;hp.Damage(10);Assert.That(before-hp.Current,Is.EqualTo(10.8f).Within(.001));
            before=hp.Current;hp.DangerDamage(10);Assert.That(before-hp.Current,Is.EqualTo(10).Within(.001));
            for(int t=0;t<2410;t++)sim.Step(1f/120);
            Assert.That(sim.Orbs[0].ToxicRemaining,Is.Zero);Assert.That(sim.Orbs[0].VulnerableRemaining,Is.Zero);Assert.That(hp.DamageMultiplier,Is.EqualTo(.9f).Within(.001));
        }
        [Test] public void WeaponPurchaseDoesNotChangeAlreadyFlyingProjectileOrGiveShieldAttack()
        {
            var sim=new ArenaSimulation(Assets()[6].CreateState(),Assets()[4].CreateState(),size:14,speed:0);sim.Orbs[0].Position=new Vector2(-4,-4);sim.Orbs[1].Position=new Vector2(4,4);
            sim.Orbs[0].Weapon.ReloadRemaining=0;sim.Step(1f/120);var shot=sim.Projectiles[0];float damage=shot.Damage;
            sim.ApplyUpgrade(0,UpgradeKind.Weapon);Assert.That(shot.Damage,Is.EqualTo(damage));Assert.That(sim.Orbs[0].Weapon.ArrowCount,Is.EqualTo(3));
            sim.ApplyUpgrade(1,UpgradeKind.Weapon);Assert.That(sim.Orbs[1].Weapon.Damage,Is.Zero);Assert.That(sim.Orbs[1].Weapon.Reflection,Is.EqualTo(1));Assert.That(sim.Orbs[1].Weapon.ShieldWidth,Is.EqualTo(2.6f).Within(.001));
        }
        [Test] public void PlayerDeathEndsAttemptBeforeBotBattleEnds()
        {
            var m=Match();m.Simulation.Orbs[0].Health.Damage(300);m.Tick(1f/120);Assert.That(m.Data.phase,Is.EqualTo(BotPhase.Finished));Assert.That(m.Data.outcome,Is.Zero);Assert.That(m.Simulation.Result,Is.EqualTo(MatchResult.InProgress));
        }
        [TestCase(MiniGameKind.Drawing)] [TestCase(MiniGameKind.Tiles)] [TestCase(MiniGameKind.Rhythm)]
        public void SaveRoundTripPreservesPuzzleAndFutureCombat(MiniGameKind kind)
        {
            string dir=Path.Combine(Path.GetTempPath(),"armorb-v06-"+Guid.NewGuid().ToString("N"));
            try
            {
                var m=Match(mini:kind);for(int t=0;t<173;t++)m.Tick(1f/120);m.Earn(5);
                var store=new BotMatchStore(dir);store.Save(m.Data);store=new BotMatchStore(dir);Assert.That(store.Pending,Is.Not.Null);
                var b=new BotMatchController(Factory(),store.Pending);Same(m.Simulation,b.Simulation);Assert.That(JsonUtility.ToJson(b.Data.puzzle),Is.EqualTo(JsonUtility.ToJson(m.Data.puzzle)));
                for(int t=0;t<500;t++){m.Tick(1f/120);b.Tick(1f/120);}Same(m.Simulation,b.Simulation);
                store.Clear();Assert.That(new BotMatchStore(dir).Pending,Is.Null);
            }
            finally{Directory.Delete(dir,true);}
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void DrawingRecognizesReversedScaledTranslatedShapes(int target)
        {
            var points=MiniGameModel.Template(target);points.Reverse();for(int i=0;i<points.Count;i++)points[i]=points[i]*.7f+new Vector2(.1f,.15f);
            var data=new MiniGameData{random=94};var model=new MiniGameModel(MiniGameKind.Drawing,data);data.drawingTarget=target;
            Assert.That(model.SubmitDrawing(points,1),Is.EqualTo(5));Assert.That(model.SubmitDrawing(points,1),Is.Zero);
        }
        [Test] public void TapsAndWrongShapesDoNotEarnDrawingEssence()
        {
            var d=new MiniGameData{random=21};var m=new MiniGameModel(MiniGameKind.Drawing,d);d.drawingTarget=5;
            Assert.That(m.SubmitDrawing(MiniGameModel.Template(0),1),Is.Zero);m.Tick(.2f);Assert.That(m.SubmitDrawing(new[]{Vector2.zero,Vector2.zero},1),Is.Zero);
        }
        [Test] public void TilesRequireValidPathRefillAndNeverAwardOnShuffle()
        {
            var d=new MiniGameData{random=42};var m=new MiniGameModel(MiniGameKind.Tiles,d);d.tiles[0]=d.tiles[1]=d.tiles[2]=0;
            Assert.That(m.SubmitChain(new[]{0,2,1}),Is.Zero);Assert.That(m.SubmitChain(new[]{0,1,0}),Is.Zero);Assert.That(m.SubmitChain(new[]{0,1,2}),Is.EqualTo(2));
            Assert.That(d.tiles.All(t=>t>=0&&t<=3),Is.True);Assert.That(m.HasMove(),Is.True);
        }
        [Test] public void RhythmAwardsOncePerPassAndClockRestores()
        {
            var d=new MiniGameData{random=91};var m=new MiniGameModel(MiniGameKind.Rhythm,d);d.clock=d.rhythmTarget*1.2f;
            Assert.That(m.TapRhythm(),Is.EqualTo(3));Assert.That(m.TapRhythm(),Is.Zero);var b=new MiniGameModel(MiniGameKind.Rhythm,JsonUtility.FromJson<MiniGameData>(JsonUtility.ToJson(d)));Assert.That(b.TapRhythm(),Is.Zero);
            m.Tick(1.2f);b.Tick(1.2f);Assert.That(b.Data.rhythmTarget,Is.EqualTo(d.rhythmTarget));Assert.That(d.rhythmTapped,Is.False);
        }
        [Test] public void VersionFivePendingBetIsRefundedWithoutResettingWallet()
        {
            string dir=Path.Combine(Path.GetTempPath(),"armorb-v06-"+Guid.NewGuid().ToString("N"));
            try { var w=new WalletStore(dir);w.Place(BotMatchController.CreateOffer(0,1,8),0,100,200);w.Data.pending.rules="0.5";w.SaveTicks(2);w=new WalletStore(dir);Assert.That(w.Data.balance,Is.EqualTo(1000));Assert.That(w.Data.pending,Is.Null);Assert.That(new WalletStore(dir).Data.balance,Is.EqualTo(1000)); }
            finally{Directory.Delete(dir,true);}
        }
    }
}
