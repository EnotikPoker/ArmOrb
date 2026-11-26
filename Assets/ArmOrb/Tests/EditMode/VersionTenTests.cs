using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionTenTests
    {
        string directory;
        CollectionProfile profile;
        RunStore store;
        static SimulationFactory Factory()=>new SimulationFactory(Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray(),AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"armorb-v10-"+Guid.NewGuid().ToString("N"));profile=new CollectionProfile(directory);store=new RunStore(directory);}
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        RunController Run(int weapon=0,int seed=12345)=>new RunController(Factory(),store,profile,weapon,BattleAbility.Heal,seed);
        static void Win(RunController run)
        {
            if(run.Data.phase==RunPhase.Preview)run.StartStage();
            if(!run.Battle.Data.started)run.Battle.Choose(run.Battle.AutoChoice());
            foreach(var orb in run.Battle.Simulation.Orbs.Skip(1))orb.Health.Damage(100000);
            run.Tick(1f/120);
        }
        [Test] public void IndependentCoinsStartAtZeroAndOldWalletNeverChanges()
        {
            var wallet=new WalletStore(directory+"/wallet");string before=JsonUtility.ToJson(wallet.Data);var run=Run();Win(run);
            Assert.That(profile.Data.coins,Is.EqualTo(20));Assert.That(JsonUtility.ToJson(new WalletStore(directory+"/wallet").Data),Is.EqualTo(before));
            Assert.That(profile.Owns("classic"),Is.True);Assert.That(profile.Owns("none"),Is.True);Assert.That(profile.Owns("knight"),Is.False);
        }
        [TestCase(0,2,200,0)] [TestCase(1,3,250,1)] [TestCase(2,2,400,3)] [TestCase(3,4,350,3)] [TestCase(4,2,800,5)]
        public void StageConditionsAndEnemyBuildsMatchPreview(int stage,int count,int hp,int ranks)
        {
            var fight=RunController.StageFight(0,777,stage);Assert.That(fight.Count,Is.EqualTo(count));Assert.That(fight.danger,Is.False);
            Assert.That(fight.health.Skip(1).All(h=>h==hp),Is.True);
            var battle=new BuildMatchController(Factory(),fight,new RunFighterState(),stage,false,BattleAbility.Heal);
            Assert.That(battle.Simulation.BuildTimeLimit,Is.EqualTo(120));Assert.That(battle.Simulation.Builds[1].Levels.Sum(),Is.EqualTo(ranks));Assert.That(battle.Data.started,Is.True);
        }
        [Test] public void GeneratedRunNeverCreatesAnAllShieldFightAndBossUsesItsThreeWeapons()
        {
            for(int seed=1;seed<=100;seed++)for(int stage=0;stage<5;stage++)
            {var f=RunController.StageFight(4,seed,stage);Assert.That(f.weapons.Any(w=>w!=4),Is.True);if(stage==4)Assert.That(new[]{0,1,7},Does.Contain(f.weapons[1]));}
        }
        [Test] public void HealthSkillsAndSpentRescuesCarryButWeaponGrowthAndTemporaryEffectsReset()
        {
            var f=RunController.StageFight(0,100,0);var b=new BuildMatchController(Factory(),f,new RunFighterState(),0,true,BattleAbility.Heal);
            b.Choose(b.AutoChoice());b.Simulation.ApplyBuildSkill(0,BuildSkill.Vitality);b.Simulation.ApplyBuildSkill(0,BuildSkill.Resurrection);b.Simulation.ApplyBuildSkill(0,BuildSkill.SecondWind);
            b.Simulation.Orbs[0].Health.Damage(100);b.Simulation.Orbs[0].Weapon.OnHit(b.Simulation.Orbs[1].Health);b.Simulation.Builds[0].ResurrectionUsed=b.Simulation.Builds[0].SecondWindUsed=true;
            b.Simulation.Builds[0].BurnTime=4;b.Simulation.Builds[0].HunterKills=2;b.Simulation.Builds[0].StorageBarrier=80;
            var carried=RunFighterState.Capture(b.Simulation);
            var next=new BuildMatchController(Factory(),RunController.StageFight(0,100,1),carried,1,false,BattleAbility.Heal);
            Assert.That(next.Simulation.Orbs[0].Health.Current,Is.EqualTo(carried.hp));Assert.That(next.Simulation.Orbs[0].Health.Maximum,Is.EqualTo(carried.Maximum));
            Assert.That(next.Simulation.Builds[0].Levels,Is.EqualTo(carried.levels));Assert.That(next.Simulation.Builds[0].ResurrectionUsed,Is.True);Assert.That(next.Simulation.Builds[0].SecondWindUsed,Is.True);
            Assert.That(next.Simulation.Builds[0].BurnTime,Is.Zero);Assert.That(next.Simulation.Builds[0].HunterKills,Is.Zero);Assert.That(next.Simulation.Builds[0].StorageBarrier,Is.Zero);
            Assert.That(next.Simulation.Orbs[0].Weapon.Damage,Is.EqualTo(1));Assert.That(next.Simulation.Builds[0].AbilityCooldown,Is.Zero);
            next.Simulation.DealBuildDamage(1,0,100000);Assert.That(next.Simulation.Orbs[0].Health.IsDead,Is.True);
        }
        [Test] public void RewardsDoNotTickAndOfferHealingOrTwoDifferentSkills()
        {
            var run=Run();run.StartStage();run.Battle.Simulation.Orbs[0].Health.Damage(100);Win(run);
            Assert.That(run.Data.phase,Is.EqualTo(RunPhase.RewardChoice));Assert.That(run.Data.rewards.Length,Is.EqualTo(3));Assert.That(run.Data.rewards[0].heal,Is.True);
            float hp=run.Data.player.hp;for(int i=0;i<1200;i++)run.Tick(1f/120);Assert.That(run.Data.player.hp,Is.EqualTo(hp));
            Assert.That(run.ChooseReward(0),Is.True);Assert.That(run.Data.player.hp,Is.EqualTo(Mathf.Min(run.Data.player.Maximum,hp+run.Data.player.Maximum*.4f)));Assert.That(run.Data.stage,Is.EqualTo(1));
            Assert.That(run.ChooseReward(0),Is.False);
        }
        [Test] public void FullHealthRewardOffersThreeSkillsWithoutHealing()
        {var run=Run();Win(run);Assert.That(run.Data.rewards.All(r=>!r.heal),Is.True);Assert.That(run.Data.rewards.Select(r=>r.skill).Distinct().Count(),Is.EqualTo(3));}
        [Test] public void VictoryAwardsTwoHundredAndUnlocksKnightExactlyOnce()
        {
            var run=Run();
            for(int stage=0;stage<5;stage++){Win(run);if(stage<4)run.ChooseReward(0);}
            Assert.That(run.Data.phase,Is.EqualTo(RunPhase.Finished));Assert.That(run.Data.outcome,Is.EqualTo(1));Assert.That(profile.Data.coins,Is.EqualTo(200));Assert.That(run.Data.earned,Is.EqualTo(200));
            Assert.That(profile.Owns("knight"),Is.True);Assert.That(profile.Data.receipts.Count,Is.EqualTo(5));
            new RunController(Factory(),store,profile,store.Pending);Assert.That(profile.Data.coins,Is.EqualTo(200));Assert.That(profile.Data.runs.Single().won,Is.True);
        }
        [Test] public void FailedStageAndAbandonKeepOnlyCompletedStageRewards()
        {
            var run=Run();Win(run);run.ChooseReward(0);run.StartStage();run.Battle.Simulation.Orbs[0].Health.Damage(100000);run.Tick(1f/120);
            Assert.That(run.Data.phase,Is.EqualTo(RunPhase.Finished));Assert.That(profile.Data.coins,Is.EqualTo(20));Assert.That(profile.Data.runs.Single().stage,Is.EqualTo(1));
            var other=Run(1);other.Abandon();Assert.That(profile.Data.coins,Is.EqualTo(20));Assert.That(profile.Data.runs.Count,Is.EqualTo(2));
        }
        [Test] public void CrashAfterProfileCreditBeforeRunAdvanceDoesNotDuplicateReward()
        {
            var run=Run();run.Data.phase=RunPhase.RewardPending;run.Data.completed=1;run.Save();
            profile.GrantStage(run.Data.id,0,run.Data.weapon,30);var restored=new RunController(Factory(),new RunStore(directory),new CollectionProfile(directory),new RunStore(directory).Pending);
            Assert.That(restored.Data.phase,Is.EqualTo(RunPhase.RewardChoice));Assert.That(new CollectionProfile(directory).Data.coins,Is.EqualTo(20));Assert.That(restored.Data.earned,Is.EqualTo(20));
        }
        [Test] public void ActiveBattleReplaysExactlyAndDoesNotOverwriteNormalBotSave()
        {
            var normal=new BuildMatchController(Factory(),BuildMatchController.CreateOffer(1,1,404));var bots=new BuildMatchStore(directory+"/bots");bots.Save(normal.Data);string before=File.ReadAllText(directory+"/bots/bot-match.json");
            var run=Run(4);run.StartStage();run.Battle.Choose(run.Battle.AutoChoice());for(int i=0;i<450;i++)run.Tick(1f/120);run.Save();
            var restored=new RunController(Factory(),new RunStore(directory),profile,new RunStore(directory).Pending);
            Assert.That(JsonUtility.ToJson(restored.Battle.Data),Is.EqualTo(JsonUtility.ToJson(run.Battle.Data)));
            for(int i=0;i<run.Battle.Simulation.Orbs.Length;i++)Assert.That(restored.Battle.Simulation.Orbs[i].Position,Is.EqualTo(run.Battle.Simulation.Orbs[i].Position));
            for(int i=0;i<150;i++){run.Tick(1f/120);restored.Tick(1f/120);}Assert.That(restored.Battle.Simulation.Orbs[0].Health.Current,Is.EqualTo(run.Battle.Simulation.Orbs[0].Health.Current));
            Assert.That(File.ReadAllText(directory+"/bots/bot-match.json"),Is.EqualTo(before));
        }
        [Test] public void PurchasesAndEquipmentAreAtomicAndSurviveReload()
        {
            var run=Run();for(int i=0;i<3;i++)profile.GrantStage(run.Data.id,i,0,30*(i+1));
            Assert.That(profile.Data.coins,Is.EqualTo(75));Assert.That(profile.Purchase("robot"),Is.True);Assert.That(profile.Data.coins,Is.EqualTo(15));Assert.That(profile.Purchase("robot"),Is.False);
            Assert.That(profile.Purchase("cat"),Is.False);Assert.That(profile.Purchase("knight"),Is.False);Assert.That(profile.Equip("robot"),Is.True);Assert.That(profile.Equip("stars"),Is.False);
            var restored=new CollectionProfile(directory);Assert.That(restored.Data.skin,Is.EqualTo("robot"));Assert.That(restored.Data.coins,Is.EqualTo(15));
        }
        [Test] public void ProfileAndRunReadBackupsWithoutTouchingEachOther()
        {
            var run=Run();run.Save();profile.GrantStage(run.Data.id,0,0,10);profile.Equip("classic");
            File.WriteAllText(directory+"/collection.json","broken");File.WriteAllText(directory+"/run.json","broken");
            Assert.That(new CollectionProfile(directory).Data.coins,Is.EqualTo(20));Assert.That(new RunStore(directory).Pending.id,Is.EqualTo(run.Data.id));
        }
        [Test] public void RunTimeLimitIsExactlyTwoMinutesAndATieEndsTheAttempt()
        {
            var f=new FightDefinition{weapons=new[]{4,4},health=new[]{300,300},danger=false,gravity=false,activeMotion=false,seed=19};
            var battle=new BuildMatchController(Factory(),f,new RunFighterState(),0,true,BattleAbility.Heal);battle.Choose(battle.AutoChoice());
            for(int i=0;i<14399;i++)battle.Tick(1f/120);Assert.That(battle.Active,Is.True);battle.Tick(1f/120);
            Assert.That(battle.Active,Is.False);Assert.That(battle.Data.outcome,Is.EqualTo(2));Assert.That(battle.Simulation.Elapsed,Is.EqualTo(120).Within(.01));
        }
        [Test] public void InvalidBattleCommandUsesBackupAndADeathResultRestoresWithFinalBuild()
        {
            var run=Run();run.StartStage();run.Save();
            var bad=JsonUtility.FromJson<RunData>(JsonUtility.ToJson(run.Data));bad.battle.commands.Add(new BuildCommand{tick=0,actor=0,kind=BuildCommandKind.Upgrade,argument=999});
            File.WriteAllText(directory+"/run.json",JsonUtility.ToJson(bad));Assert.That(new RunStore(directory).Pending.battle.commands.Count,Is.EqualTo(run.Data.battle.commands.Count));
            run.Battle.Choose(run.Battle.AutoChoice());run.Battle.Simulation.Orbs[0].Health.Damage(100000);run.Tick(1f/120);
            var restored=new RunStore(directory);Assert.That(restored.Pending,Is.Not.Null);Assert.That(restored.Pending.player.hp,Is.Zero);Assert.That(restored.Pending.player.levels.Sum(),Is.EqualTo(1));
        }
        [Test] public void NormalBotRulesRemainFiveMinutesAndCosmeticsHaveNoSimulationInputs()
        {var b=new BuildMatchController(Factory(),BuildMatchController.CreateOffer(0,1,777));Assert.That(b.Simulation.BuildTimeLimit,Is.EqualTo(300));Assert.That(b.Data.runBattle,Is.False);Assert.That(CosmeticCatalog.Items.Length,Is.EqualTo(10));}
        [TestCase("robot")] [TestCase("cat")] [TestCase("ninja")] [TestCase("astronaut")] [TestCase("knight")]
        public void CharacterAssetsAreImportedAsSmallTransparentSprites(string id)
        {
            var sprite=CosmeticCatalog.Sprite(id);Assert.That(sprite,Is.Not.Null);
            Assert.That(sprite.texture.width,Is.LessThanOrEqualTo(128));Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
    }
}
