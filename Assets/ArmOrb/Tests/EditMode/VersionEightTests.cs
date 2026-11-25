using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionEightTests
    {
        static ArenaSimulation Arena(WeaponKind first=WeaponKind.Sword,WeaponKind second=WeaponKind.Sword,float hp=300,float growth=0)
        {
            var sim=new ArenaSimulation(new[]{new WeaponState(first,10,.5f,0,growth),new WeaponState(second,10,.5f,0,growth)},new[]{hp,hp},seed:12345,size:20,speed:0,gravity:false,activeMotion:false,danger:false);
            sim.EnableBuildRules(BattleVariant.Ability,new[]{BattleAbility.Heal,BattleAbility.Barrier});return sim;
        }
        static void Run(ArenaSimulation sim,int ticks){for(int i=0;i<ticks;i++)sim.Step(1f/120);}
        static void Rank(ArenaSimulation sim,int actor,BuildSkill skill,int levels=1){for(int i=0;i<levels;i++)Assert.That(sim.ApplyBuildSkill(actor,skill),Is.True);}
        static SimulationFactory Factory()
        {
            var assets=Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray();
            return new SimulationFactory(assets,AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
        }
        static BuildMatchController Match(BattleVariant variant=BattleVariant.Builds,int bots=1,BotDifficulty difficulty=BotDifficulty.Normal)
        {
            var f=BuildMatchController.CreateOffer(4,bots,10891);for(int i=0;i<f.Count;i++)f.weapons[i]=4;
            return new BuildMatchController(Factory(),f,variant,difficulty,BattleAbility.Barrier);
        }
        static void Run(BuildMatchController m,int ticks){for(int i=0;i<ticks&&m.Active;i++)m.Tick(1f/120);}
        static BuildMatchSave Clone(BuildMatchSave d)=>JsonUtility.FromJson<BuildMatchSave>(JsonUtility.ToJson(d));
        static void Same(BuildMatchController a,BuildMatchController b)
        {
            Assert.That(JsonUtility.ToJson(b.Data),Is.EqualTo(JsonUtility.ToJson(a.Data)));
            for(int i=0;i<a.Data.fight.Count;i++)
            {
                var x=a.Simulation.Orbs[i];var y=b.Simulation.Orbs[i];var bx=a.Simulation.Builds[i];var by=b.Simulation.Builds[i];
                Assert.That(y.Position,Is.EqualTo(x.Position));Assert.That(y.Angle,Is.EqualTo(x.Angle));Assert.That(y.Health.Current,Is.EqualTo(x.Health.Current));
                Assert.That(y.Health.Maximum,Is.EqualTo(x.Health.Maximum));Assert.That(by.Levels,Is.EqualTo(bx.Levels));Assert.That(by.BarrierTotal,Is.EqualTo(bx.BarrierTotal));
                Assert.That(by.AbilityCooldown,Is.EqualTo(bx.AbilityCooldown));Assert.That(by.Random,Is.EqualTo(bx.Random));
                Assert.That(by.Spark.Position,Is.EqualTo(bx.Spark.Position));Assert.That(by.Spark.Id,Is.EqualTo(bx.Spark.Id));Assert.That(by.Spark.Active,Is.EqualTo(bx.Spark.Active));
                Assert.That(by.ScythePoison,Is.EqualTo(bx.ScythePoison));Assert.That(by.Bleeds,Is.EqualTo(bx.Bleeds));
            }
        }
        [Test] public void CatalogHasThirtyStableSkillsAndAllWeaponsHaveOneSpecial()
        {
            Assert.That(Enum.GetValues(typeof(BuildSkill)).Length,Is.EqualTo(30));
            foreach(WeaponKind weapon in Enum.GetValues(typeof(WeaponKind)))
            {
                var sim=Arena(weapon);int specials=0;
                for(int i=20;i<30;i++)if(sim.CanBuildSkill(0,(BuildSkill)i))specials++;
                Assert.That(specials,Is.EqualTo(1));
            }
        }
        [TestCase(BuildSkill.Vitality)] [TestCase(BuildSkill.Regeneration)] [TestCase(BuildSkill.SecondWind)] [TestCase(BuildSkill.Barrier)]
        [TestCase(BuildSkill.Vampirism)] [TestCase(BuildSkill.Armor)] [TestCase(BuildSkill.Adaptation)] [TestCase(BuildSkill.Resurrection)]
        [TestCase(BuildSkill.Power)] [TestCase(BuildSkill.Critical)] [TestCase(BuildSkill.Rage)] [TestCase(BuildSkill.Execution)]
        [TestCase(BuildSkill.Growth)] [TestCase(BuildSkill.Overload)] [TestCase(BuildSkill.Rhythm)] [TestCase(BuildSkill.Hunter)]
        [TestCase(BuildSkill.Burn)] [TestCase(BuildSkill.Bleed)] [TestCase(BuildSkill.Suppression)] [TestCase(BuildSkill.Pierce)]
        public void CommonSkillsStopAtTheirDocumentedLevel(BuildSkill skill)
        {
            var sim=Arena();Rank(sim,0,skill,BuildSkills.MaxLevel(skill));Assert.That(sim.ApplyBuildSkill(0,skill),Is.False);
        }
        [Test] public void VitalityLevelsAddOnlyTheDifferenceAndHealByTheSameAmount()
        {
            var s=Arena();s.Orbs[0].Health.Damage(100);Rank(s,0,BuildSkill.Vitality);Assert.That(s.Orbs[0].Health.Maximum,Is.EqualTo(450));Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(350));
            Rank(s,0,BuildSkill.Vitality,2);Assert.That(s.Orbs[0].Health.Maximum,Is.EqualTo(650));Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(550));
        }
        [Test] public void RegenAndSuppressionAffectActualHealing()
        {
            var s=Arena();Rank(s,0,BuildSkill.Regeneration);s.Orbs[0].Health.Damage(100);Run(s,120);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(204).Within(.02));
            Rank(s,1,BuildSkill.Suppression);s.DealBuildDamage(1,0,10);float before=s.Orbs[0].Health.Current;Run(s,120);Assert.That(s.Orbs[0].Health.Current-before,Is.EqualTo(1.6f).Within(.02));
            Assert.That(s.HealBuild(0,100),Is.EqualTo(40).Within(.001));
        }
        [Test] public void SecondWindAndResurrectionAreDifferentAndSingleUse()
        {
            var s=Arena();Rank(s,0,BuildSkill.SecondWind);s.DealBuildDamage(1,0,230);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(190));
            s.DealBuildDamage(1,0,150);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(40));s.DealBuildDamage(1,0,100);Assert.That(s.Orbs[0].Health.IsDead,Is.True);
            s=Arena();Rank(s,0,BuildSkill.Resurrection);s.DealBuildDamage(1,0,1000);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(105));
            s.DealBuildDamage(1,0,1000);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(105));Run(s,121);s.DealBuildDamage(1,0,1000);Assert.That(s.Orbs[0].Health.IsDead,Is.True);
        }
        [Test] public void PassiveBarrierRestoresAfterItsOwnDestruction()
        {
            var s=Arena();Rank(s,0,BuildSkill.Barrier);s.DealBuildDamage(1,0,100);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(280));Assert.That(s.Builds[0].PassiveBarrier,Is.Zero);
            Run(s,1799);Assert.That(s.Builds[0].PassiveBarrier,Is.Zero);Run(s,2);Assert.That(s.Builds[0].PassiveBarrier,Is.EqualTo(80));
        }
        [Test] public void VampirismUsesLostHpNotOverkillOrBarrierAndDoesNotTriggerFromDots()
        {
            var s=Arena();Rank(s,0,BuildSkill.Vampirism);s.Orbs[0].Health.Damage(100);s.DealBuildDamage(0,1,20);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(203));
            Rank(s,1,BuildSkill.Barrier);s.DealBuildDamage(0,1,20);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(203));
            s.DealBuildDamage(0,1,10,BuildDamageKind.Periodic);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(203));
        }
        [Test] public void DefenseCapPierceOverloadAndAdaptationUseOneDamagePipeline()
        {
            var s=Arena();Rank(s,1,BuildSkill.Armor,3);Rank(s,1,BuildSkill.Adaptation);for(int i=0;i<6;i++)s.Builds[1].Adaptation[i]=3;
            s.Builds[1].RamTime=1;Assert.That(s.DealBuildDamage(0,1,100),Is.EqualTo(40).Within(.001));
            Rank(s,0,BuildSkill.Pierce);Assert.That(s.DealBuildDamage(0,1,100),Is.EqualTo(70).Within(.001));
            Rank(s,1,BuildSkill.Overload);Assert.That(s.DealBuildDamage(0,1,100),Is.EqualTo(87.5f).Within(.001));
        }
        [Test] public void OffensiveModifiersAndFourHitCyclesAreVisible()
        {
            var s=Arena();Rank(s,0,BuildSkill.Power);Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(14).Within(.001));
            Rank(s,0,BuildSkill.Rhythm);s.DealBuildDamage(0,1,10);s.DealBuildDamage(0,1,10);s.DealBuildDamage(0,1,10);Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(28).Within(.001));
            s=Arena();Rank(s,0,BuildSkill.Rage);s.Orbs[0].Health.Damage(150);Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(13).Within(.001));
            Rank(s,0,BuildSkill.Execution);s.Orbs[1].Health.Damage(210);Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(19).Within(.001));
        }
        [Test] public void CriticalsAreSeededAndDoNotAffectPeriodicDamage()
        {
            var s=Arena(hp:100000);Rank(s,0,BuildSkill.Critical);int criticals=0;
            for(int i=0;i<1000;i++)if(s.DealBuildDamage(0,1,1)>1.5f)criticals++;
            Assert.That(criticals,Is.InRange(200,300));Assert.That(s.DealBuildDamage(0,1,10,BuildDamageKind.Periodic),Is.EqualTo(10));
        }
        [Test] public void GrowthIncreasesFractionalArrowsAndPreservesBladeFormula()
        {
            foreach(var kind in new[]{WeaponKind.Bow,WeaponKind.Crossbow,WeaponKind.Shuriken})
            {
                var s=Arena(kind,growth:1);Rank(s,0,BuildSkill.Growth);s.DealBuildDamage(0,1,1);s.DealBuildDamage(0,1,1);
                Assert.That(kind==WeaponKind.Shuriken?s.Orbs[0].Weapon.Bounces:s.Orbs[0].Weapon.ArrowCount,Is.EqualTo(4));
            }
            var blade=new WeaponState(WeaponKind.Blade,1,1,573,120);blade.GrowthMultiplier=1.5f;blade.OnHit(new HealthState(300));Assert.That(blade.RotationSpeed,Is.EqualTo(753).Within(.001));
        }
        [Test] public void HunterDoesNotCreditResurrectionAndCanCreditDotKills()
        {
            var s=Arena();Rank(s,0,BuildSkill.Hunter);Rank(s,1,BuildSkill.Resurrection);s.Orbs[0].Health.Damage(150);s.DealBuildDamage(0,1,1000);
            Assert.That(s.Builds[0].HunterKills,Is.Zero);Run(s,121);s.DealBuildDamage(0,1,1000,BuildDamageKind.Periodic);
            Assert.That(s.Builds[0].HunterKills,Is.EqualTo(1));Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(250));
        }
        [Test] public void BurnBleedAndSuppressionDoNotRecursivelyProc()
        {
            var s=Arena();Rank(s,0,BuildSkill.Burn);Rank(s,0,BuildSkill.Bleed);Rank(s,0,BuildSkill.Suppression);s.DealBuildDamage(0,1,10);Run(s,120);
            Assert.That(s.Orbs[1].Health.Current,Is.EqualTo(283).Within(.03));Assert.That(s.Builds[1].Bleeds.Count(t=>t>0),Is.EqualTo(1));Assert.That(s.Builds[1].SuppressionTime,Is.EqualTo(4).Within(.02));
        }
        [Test] public void SwordSpearBladeScytheAndShieldSpecialsChangeCombat()
        {
            var s=Arena();Rank(s,0,BuildSkill.SwordHeavy);for(int i=0;i<3;i++)s.DealBuildDamage(0,1,10);Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(30));
            s=Arena(WeaponKind.Spear);Rank(s,0,BuildSkill.SpearLunge);Run(s,1);Assert.That(s.Orbs[0].Weapon.Reach,Is.EqualTo(.75f));Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(20));Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(10));
            s=Arena(WeaponKind.Blade);Rank(s,0,BuildSkill.BladeMomentum);s.Builds[0].MomentumStacks=10;Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(20));Assert.That(s.Builds[0].MomentumStacks,Is.Zero);
            s=Arena(WeaponKind.Scythe,growth:1);Rank(s,0,BuildSkill.ScytheHarvest);s.DealBuildDamage(0,1,10);Assert.That(s.Orbs[1].Health.Current,Is.EqualTo(287));Assert.That(s.Builds[1].ScythePoison[0],Is.EqualTo(1));
            s=Arena(WeaponKind.Shield);Rank(s,0,BuildSkill.ShieldStorage);s.DealBuildDamage(0,1,100,BuildDamageKind.Reflected);Assert.That(s.Builds[0].StorageBarrier,Is.EqualTo(30).Within(.001));Assert.That(s.Orbs[0].Weapon.Damage,Is.Zero);
        }
        [Test] public void UnarmedRamChangesDamageAndDefense()
        {
            var s=Arena(WeaponKind.Unarmed);Rank(s,0,BuildSkill.UnarmedRam);Assert.That(s.DealBuildDamage(0,1,10),Is.EqualTo(15));
            s.Orbs[0].Position=new Vector2(-.5f,0);s.Orbs[1].Position=new Vector2(.5f,0);Run(s,1);Assert.That(s.Builds[0].RamTime,Is.GreaterThan(0));
            Assert.That(s.DealBuildDamage(1,0,10),Is.EqualTo(6).Within(.001));
        }
        [TestCase(WeaponKind.Bow,BuildSkill.BowDouble)] [TestCase(WeaponKind.Crossbow,BuildSkill.CrossbowPierce)]
        [TestCase(WeaponKind.Staff,BuildSkill.StaffEcho)] [TestCase(WeaponKind.Shuriken,BuildSkill.ShurikenRicochet)]
        public void RangedSpecialsAreCapturedByNewProjectiles(WeaponKind kind,BuildSkill skill)
        {
            var s=Arena(kind);Rank(s,0,skill);s.Orbs[0].Weapon.ReloadRemaining=0;Run(s,1);
            Assert.That(s.Projectiles.Count,Is.EqualTo(1));Assert.That(s.Projectiles[0].Special,Is.True);
            if(kind==WeaponKind.Bow)Assert.That(s.Orbs[0].Weapon.BurstRemaining,Is.EqualTo(1));
            if(kind==WeaponKind.Crossbow)Assert.That(s.Projectiles[0].PierceRemaining,Is.EqualTo(1));
        }
        [Test] public void CrossbowPiercesTwoDistinctBodiesInOneTick()
        {
            var weapons=new[]{new WeaponState(WeaponKind.Crossbow,10,.5f,0,0),new WeaponState(WeaponKind.Sword,10,.5f,0,0),new WeaponState(WeaponKind.Sword,10,.5f,0,0)};
            var sim=new ArenaSimulation(weapons,new[]{300f,300f,300f},size:20,speed:0,gravity:false,activeMotion:false,danger:false);
            sim.EnableBuildRules(BattleVariant.Builds,new BattleAbility[3]);Rank(sim,0,BuildSkill.CrossbowPierce);
            sim.Orbs[0].Position=new Vector2(-5,0);sim.Orbs[1].Position=Vector2.zero;sim.Orbs[2].Position=new Vector2(3,0);sim.Orbs[0].Angle=0;
            sim.Orbs[0].Weapon.ReloadRemaining=0;Run(sim,1);var p=sim.Projectiles.Single();p.Position=new Vector2(-1,0);p.Velocity=new Vector2(600,0);Run(sim,1);
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(290));Assert.That(sim.Orbs[2].Health.Current,Is.EqualTo(290));Assert.That(sim.Projectiles.Count,Is.Zero);
        }
        [Test] public void ShurikenActuallyGainsDamageAtTheWall()
        {
            var s=Arena(WeaponKind.Shuriken);Rank(s,0,BuildSkill.ShurikenRicochet);s.Orbs[0].Weapon.ReloadRemaining=0;Run(s,1);
            var p=s.Projectiles.Single();p.Position=new Vector2(9.8f,0);p.Velocity=new Vector2(100,0);Run(s,1);
            Assert.That(p.Velocity.x,Is.LessThan(0));Assert.That(p.Damage,Is.EqualTo(12.5f));Assert.That(p.Bounces,Is.Zero);
        }
        [Test] public void StaffEchoAppearsAfterDelayWithoutRecursion()
        {
            var s=Arena(WeaponKind.Staff);Rank(s,0,BuildSkill.StaffEcho);s.Orbs[0].Weapon.ReloadRemaining=0;Run(s,1);
            var p=s.Projectiles.Single();p.Position=new Vector2(9.8f,0);p.Velocity=new Vector2(100,0);Run(s,1);
            Assert.That(s.Waves.Count,Is.EqualTo(1));Assert.That(s.Waves[0].Extra,Is.False);Run(s,35);Assert.That(s.Waves.Count,Is.Zero);
            Run(s,14);Assert.That(s.Waves.Count,Is.EqualTo(1));Assert.That(s.Waves[0].Extra,Is.True);Assert.That(s.Waves[0].Damage,Is.EqualTo(6));
            Run(s,32);Assert.That(s.Waves.Count,Is.Zero);
        }
        [Test] public void ReflectionNeverReappliesAttackProcsAndShieldDoesNotGrowOnAbsorption()
        {
            var s=Arena(WeaponKind.Shield);Rank(s,0,BuildSkill.ShieldStorage);Rank(s,1,BuildSkill.Barrier);
            float width=s.Orbs[0].Weapon.ShieldWidth;s.DealBuildDamage(0,1,50,BuildDamageKind.Reflected);
            Assert.That(s.Builds[0].StorageBarrier,Is.Zero);Assert.That(s.Orbs[0].Weapon.ShieldWidth,Is.EqualTo(width));
            s.DealBuildDamage(0,1,50,BuildDamageKind.Reflected);Assert.That(s.Builds[0].StorageBarrier,Is.EqualTo(6).Within(.001));Assert.That(s.Builds[0].DirectHits,Is.Zero);
        }
        [Test] public void SmallContinuousHealingDoesNotDisappearAtHighSubstepRates()
        {
            var health=new HealthState(300);health.EnablePrecision();health.Damage(100);for(int i=0;i<100000;i++)health.Heal(.000001f);
            Assert.That(health.Current,Is.EqualTo(200.1f).Within(.00002f));
        }
        [TestCase(BotDifficulty.Easy,4)] [TestCase(BotDifficulty.Normal,2)] [TestCase(BotDifficulty.Hard,1)]
        public void BotsGetOneSkillOnTheSameStageWithDifficultyDelay(BotDifficulty difficulty,int delay)
        {
            var m=Match(difficulty:difficulty);m.Choose(m.AutoChoice());Run(m,2400+delay*120-1);
            Assert.That(m.Data.commands.Count(c=>c.kind==BuildCommandKind.Upgrade&&c.actor==1),Is.EqualTo(1));Run(m,1);
            Assert.That(m.Data.commands.Count(c=>c.kind==BuildCommandKind.Upgrade&&c.actor==1),Is.EqualTo(2));
        }
        [Test] public void AbilityCannotBeSpentAtFullHealthAndCooldownIsReal()
        {
            var s=Arena();Assert.That(s.UseBuildAbility(0),Is.False);s.Orbs[0].Health.Damage(120);Assert.That(s.UseBuildAbility(0),Is.True);Assert.That(s.Orbs[0].Health.Current,Is.EqualTo(280));Assert.That(s.UseBuildAbility(0),Is.False);
            Assert.That(s.UseBuildAbility(1),Is.True);s.DealBuildDamage(0,1,100);Assert.That(s.Orbs[1].Health.Current,Is.EqualTo(300));Assert.That(s.Builds[1].ActiveBarrier,Is.EqualTo(20));Run(s,601);Assert.That(s.Builds[1].ActiveBarrier,Is.Zero);
        }
        [Test] public void StartingDraftDoesNotMoveBattleAndTimedChoicesAreFree()
        {
            var m=Match();var pos=m.Simulation.Orbs[0].Position;Run(m,120);Assert.That(m.Simulation.TickCount,Is.Zero);Assert.That(m.Simulation.Orbs[0].Position,Is.EqualTo(pos));
            Assert.That(m.Choose(m.AutoChoice()),Is.True);Assert.That(m.Data.commands.Count(c=>c.kind==BuildCommandKind.Upgrade),Is.EqualTo(2));
            Run(m,2399);Assert.That(m.Data.offers[0].open,Is.False);Run(m,1);Assert.That(m.Data.offers[0].open,Is.True);int tick=m.Data.ticks;
            Run(m,960);Assert.That(m.Data.offers[0].open,Is.False);Assert.That(m.Data.ticks,Is.EqualTo(tick+960));Assert.That(m.Data.commands.Count(c=>c.kind==BuildCommandKind.Upgrade&&c.actor==0),Is.EqualTo(2));
        }
        [Test] public void StartingDraftAutomaticallyChoosesAtEightSecondsAndSurvivesReload()
        {
            var m=Match();Run(m,500);var restored=new BuildMatchController(Factory(),Clone(m.Data));Same(m,restored);Run(m,460);Run(restored,460);Same(m,restored);Assert.That(m.Data.started,Is.True);Assert.That(m.Simulation.TickCount,Is.Zero);
        }
        [TestCase(BattleVariant.Builds)] [TestCase(BattleVariant.Ability)] [TestCase(BattleVariant.Sparks)]
        public void SavedChoicesActionsAndFutureStagesReplayExactly(BattleVariant variant)
        {
            var m=Match(variant);m.Choose(m.AutoChoice());Run(m,1230);
            if(variant==BattleVariant.Ability)Assert.That(m.UseAbility(),Is.True);
            if(variant==BattleVariant.Sparks)Assert.That(m.CollectSpark(0,m.Simulation.Builds[0].Spark.Id),Is.True);
            Run(m,1200);var restored=new BuildMatchController(Factory(),Clone(m.Data));Same(m,restored);
            Run(m,700);Run(restored,700);Same(m,restored);
        }
        [Test] public void ShieldOffersContainOnlyUsefulSkillsAndNoDuplicates()
        {
            var m=Match();foreach(var offer in m.Data.offers)
            {Assert.That(offer.cards.Length,Is.EqualTo(3));Assert.That(offer.cards.Select(c=>c.skill).Distinct().Count(),Is.EqualTo(3));foreach(var card in offer.cards)Assert.That(BuildSkills.Applicable(card.skill,WeaponKind.Shield),Is.True);}
            Assert.That(m.Simulation.Danger,Is.False);Assert.That(m.Simulation.BuildRules,Is.True);
        }
        [Test] public void TimeLimitUsesHealthFractionRatherThanAbsoluteHpOrBarrier()
        {
            var s=Arena();Rank(s,0,BuildSkill.Vitality);s.Orbs[0].Health.Damage(150);s.Orbs[1].Health.Damage(50);s.Builds[0].StorageBarrier=150;s.ResolveBuildTimeLimit();Assert.That(s.WinnerIndex,Is.EqualTo(1));
            s=Arena();s.ResolveBuildTimeLimit();Assert.That(s.Result,Is.EqualTo(MatchResult.Draw));
        }
        [Test] public void PreviouslyFiredArrowDoesNotGainNewAttackEffects()
        {
            var s=Arena(WeaponKind.Bow);s.Orbs[0].Weapon.ReloadRemaining=0;Run(s,1);var p=s.Projectiles.Single();
            Rank(s,0,BuildSkill.Power);Rank(s,0,BuildSkill.Burn);Rank(s,0,BuildSkill.Bleed);Rank(s,0,BuildSkill.Critical);
            p.Position=s.Orbs[1].Position+Vector2.left;p.Velocity=Vector2.right*300;Run(s,1);
            Assert.That(s.Orbs[1].Health.Current,Is.EqualTo(290));Assert.That(s.Builds[1].BurnTime,Is.Zero);Assert.That(s.Builds[1].Bleeds.All(t=>t==0),Is.True);
        }
        [Test] public void FiveMinuteShieldBattleEndsWithoutZoneOrHiddenDamage()
        {
            var m=Match();m.Choose(m.AutoChoice());Run(m,35999);Assert.That(m.Active,Is.True);Run(m,1);
            Assert.That(m.Active,Is.False);Assert.That(m.Simulation.Elapsed,Is.EqualTo(300).Within(.01));Assert.That(m.Simulation.Danger,Is.False);
            Assert.That(m.Data.outcome,Is.EqualTo(2));Assert.That(m.Simulation.Orbs.All(o=>o.Health.Current==o.Health.Maximum),Is.True);
        }
        [Test] public void LegacyAttemptIsArchivedWithoutTouchingWalletAndNewSaveHasBackup()
        {
            string dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                string old="{\"version\":2,\"rules\":\"0.7\"}";File.WriteAllText(Path.Combine(dir,"bot-match.json"),old);File.WriteAllText(Path.Combine(dir,"wallet.json"),"wallet-unchanged");
                var store=new BuildMatchStore(dir);Assert.That(store.Pending,Is.Null);Assert.That(store.Error,Does.Contain("Режим обновлён"));Assert.That(File.ReadAllText(Directory.GetFiles(dir,"*.legacy-*").Single()),Is.EqualTo(old));
                Assert.That(File.ReadAllText(Path.Combine(dir,"wallet.json")),Is.EqualTo("wallet-unchanged"));var m=Match();store.Save(m.Data);m.Choose(m.AutoChoice());store.Save(m.Data);
                File.WriteAllText(Path.Combine(dir,"bot-match.json"),"broken");var recovered=new BuildMatchStore(dir);Assert.That(recovered.Pending,Is.Not.Null);Assert.That(recovered.Pending.started,Is.False);
            }
            finally{Directory.Delete(dir,true);}
        }
    }
}
