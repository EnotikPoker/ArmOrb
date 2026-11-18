using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionFiveTests
    {
        static WeaponDefinition[] Assets()=>Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray();
        static SimulationFactory Factory()=>new SimulationFactory(Assets(),AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
        static FightDefinition F(int[] weapons,int hp=100,bool gravity=true)=>new FightDefinition{weapons=weapons,health=Enumerable.Repeat(hp,weapons.Length).ToArray(),gravity=gravity,seed=104729};
        static void Run(ArenaSimulation sim,float seconds){for(int t=0;t<Mathf.CeilToInt(seconds*120)&&sim.Result==MatchResult.InProgress;t++)sim.Step(1f/120);}
        [Test] public void RuntimeAssetsHaveNewBalanceAndRealBladeCap()
        {
            var definitions=Assets();var blade=definitions[2].CreateState();for(int i=0;i<100;i++)blade.OnHit(new HealthState(100));
            Assert.That(blade.RotationSpeed,Is.EqualTo(1800));Assert.That(definitions[2].growth,Is.EqualTo(300));Assert.That(definitions[2].damage,Is.EqualTo(2));
            Assert.That(definitions[1].damage,Is.EqualTo(2));Assert.That(definitions[1].reach,Is.EqualTo(3.5f));
            Assert.That(definitions[7].damage,Is.EqualTo(3));Assert.That(definitions[7].cooldown,Is.EqualTo(1));Assert.That(definitions[7].blastRadius,Is.EqualTo(1.5f));
        }
        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EverySizeHasIndependentSpawnsAndReplaysOnWorker(int count)
        {
            var factory=Factory();var f=F(Enumerable.Range(0,count).ToArray());
            var a=factory.Create(f);for(int i=0;i<count;i++)for(int j=0;j<i;j++)Assert.That(Vector2.Distance(a.Orbs[i].Position,a.Orbs[j].Position),Is.GreaterThan(a.Radius*2));
            Run(a,3);var task=Task.Run(()=>{var b=factory.Create(f);Run(b,3);return b;});Assert.That(task.Wait(30000),Is.True);var replay=task.Result;
            for(int i=0;i<count;i++){Assert.That(replay.Orbs[i].Position,Is.EqualTo(a.Orbs[i].Position));Assert.That(replay.Orbs[i].Health.Current,Is.EqualTo(a.Orbs[i].Health.Current));}
        }
        [TestCase(false)] [TestCase(true)] public void QuietChaseIsCorrectedWithoutTeleportation(bool gravity)
        {
            var f=F(new[]{4,4},300,gravity);f.danger=false;var sim=Factory().Create(f);
            sim.Orbs[0].Position=new Vector2(-2,-2);sim.Orbs[1].Position=new Vector2(2,2);
            sim.Orbs[0].Velocity=sim.Orbs[1].Velocity=Vector2.right*0.03f;
            int contacts=0;sim.Event+=e=>{if(e.Kind==CombatEventKind.Body||e.Kind==CombatEventKind.Clash)contacts++;};
            Run(sim,12);Assert.That(contacts,Is.GreaterThan(0));
            foreach(var orb in sim.Orbs)Assert.That(Mathf.Abs(orb.Position.x),Is.LessThanOrEqualTo(sim.Size/2-sim.Radius+0.001f));
        }
        [Test] public void UnarmedActuallyAcceleratesAndFreeFallIsNotClipped()
        {
            var f=F(new[]{5,6});f.danger=f.activeMotion=false;var sim=Factory().Create(f);
            sim.Orbs[0].Position=new Vector2(-0.56f,0);sim.Orbs[1].Position=new Vector2(0.56f,0);
            sim.Orbs[0].Velocity=Vector2.right*6.4f;sim.Orbs[1].Velocity=Vector2.left*6.4f;
            sim.Step(1f/120);Assert.That(sim.Orbs[0].Weapon.Hits,Is.EqualTo(1));Assert.That(sim.Orbs[0].Velocity.magnitude,Is.GreaterThan(7.1f));
            sim.Orbs[0].Position=new Vector2(-2,2);sim.Orbs[1].Position=new Vector2(2,0);sim.Orbs[0].Velocity=Vector2.down*12;
            sim.Step(1f/120);Assert.That(sim.Orbs[0].Velocity.magnitude,Is.GreaterThan(12));
        }
        [Test] public void ProjectileHitsNearestOfSeveralEnemies()
        {
            var f=F(new[]{6,6,6});f.danger=f.activeMotion=false;var sim=Factory().Create(f);
            for(int i=0;i<3;i++){sim.Orbs[i].Position=new Vector2(-3+i*2,0);sim.Orbs[i].Velocity=Vector2.zero;sim.Orbs[i].Angle=0;sim.Orbs[i].Weapon.ReloadRemaining=1000;}
            sim.Orbs[0].Weapon.ReloadRemaining=0;sim.Step(1f/120);sim.Orbs[0].Weapon.ReloadRemaining=1000;Run(sim,0.4f);
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(99));Assert.That(sim.Orbs[2].Health.Current,Is.EqualTo(100));
        }
        [Test] public void StaffWaveHitsEveryEnemyOnlyOnce()
        {
            var f=F(new[]{7,6,6});f.danger=f.activeMotion=false;var sim=Factory().Create(f);
            for(int i=0;i<3;i++){sim.Orbs[i].Velocity=Vector2.zero;sim.Orbs[i].Weapon.ReloadRemaining=1000;}
            sim.Orbs[0].Position=new Vector2(-3,0);sim.Orbs[0].Angle=0;
            sim.Orbs[1].Position=new Vector2(2,0.7f);sim.Orbs[2].Position=new Vector2(2,-0.7f);
            sim.Orbs[0].Weapon.ReloadRemaining=0;sim.Step(1f/120);sim.Orbs[0].Weapon.ReloadRemaining=1000;
            var p=sim.Projectiles[0];p.Position=new Vector2(3.8f,0);p.Velocity=Vector2.right*10;p.BlastRadius=3;
            Run(sim,0.8f);Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(97));Assert.That(sim.Orbs[2].Health.Current,Is.EqualTo(97));Assert.That(sim.Orbs[0].Weapon.Hits,Is.EqualTo(2));
        }
        [Test] public void ReflectedArrowCanHitThirdParticipantAndCreditsShield()
        {
            var f=F(new[]{6,4,6},100,false);f.activeMotion=f.danger=false;var sim=Factory().Create(f);
            for(int i=0;i<3;i++){sim.Orbs[i].Velocity=Vector2.zero;sim.Orbs[i].Weapon.ReloadRemaining=1000;}
            sim.Orbs[0].Position=new Vector2(-2,0);sim.Orbs[1].Position=new Vector2(2,0);sim.Orbs[1].Angle=180;sim.Orbs[2].Position=new Vector2(-2,2);
            sim.Orbs[0].Angle=0;sim.Orbs[0].Weapon.ReloadRemaining=0;sim.Step(1f/120);sim.Orbs[0].Weapon.ReloadRemaining=1000;
            var shot=sim.Projectiles[0];for(int n=0;n<120&&shot.Owner==0;n++){sim.Orbs[1].Angle=180;sim.Step(1f/120);}
            Assert.That(shot.Owner,Is.EqualTo(1));sim.Orbs[0].Position=new Vector2(-2,-2);sim.Orbs[2].Position=new Vector2(-2,0);sim.Orbs[1].Angle=180;
            Run(sim,0.5f);Assert.That(sim.Orbs[2].Health.Current,Is.EqualTo(99));Assert.That(sim.Orbs[1].Weapon.ShieldWidth,Is.EqualTo(2.35f).Within(0.001));
        }
        [Test] public void EliminatedParticipantsDoNotAttackAndHigherIndexCanWin()
        {
            var sim=Factory().Create(F(new[]{0,1,7}));sim.Orbs[0].Health.Damage(100);sim.Orbs[1].Health.Damage(100);var position=sim.Orbs[0].Position;sim.Step(1f/120);
            Assert.That(sim.WinnerIndex,Is.EqualTo(2));Assert.That(sim.Result,Is.EqualTo(MatchResult.Other));Assert.That(sim.Orbs[0].Position,Is.EqualTo(position));
        }
        [TestCase(false)] [TestCase(true)] public void ShieldOnlyFightsFinishWithDanger(bool gravity)
        {
            var sim=Factory().Create(F(new[]{4,4,4,4,4},300,gravity));Run(sim,136);Assert.That(sim.Result,Is.Not.EqualTo(MatchResult.InProgress));Assert.That(sim.Elapsed,Is.LessThan(136));
        }
        [Test] public void DisabledDangerLeavesShieldMirrorUndamaged()
        {
            var f=F(new[]{4,4});f.danger=false;var sim=Factory().Create(f);Run(sim,95);Assert.That(sim.Orbs.All(o=>o.Health.Current==100),Is.True);
        }
        [Test] public void GeneratedConditionsAlwaysUseAllowedHpSizesAndDuplicates()
        {
            var random=new System.Random(913);bool duplicates=false,gravity=false,flat=false;
            for(int n=0;n<1000;n++)
            {
                var f=FightDefinition.RandomOffer(random);Assert.That(f.IsValid(),Is.True);Assert.That(f.health.All(h=>h>=50&&h<=300&&h%50==0),Is.True);
                if(f.Count==5)Assert.That((int)f.arena,Is.InRange(3,4));duplicates|=f.weapons.Distinct().Count()<f.Count;gravity|=f.gravity;flat|=!f.gravity;
            }
            Assert.That(duplicates&&gravity&&flat,Is.True);
        }
        [Test] public void OddsPoolEqualParticipantsAndTreatDrawAsRefund()
        {
            var f=F(new[]{0,0,1});var quote=OddsQuote.Calculate(f,new[]{6,2,0},2);
            Assert.That(quote.Samples,Is.EqualTo(10));Assert.That(quote.Hundredths[0],Is.EqualTo(quote.Hundredths[1]));
            Assert.That(quote.Hundredths[2],Is.GreaterThan(quote.Hundredths[0]));
            Assert.That(OddsQuote.Calculate(F(new[]{0,0}),new[]{0,0},16).Hundredths,Is.EqualTo(new[]{200,200}));
        }
        [Test] public void RealEstimatorRunsOnWorkerAndDoesNotMutateOffer()
        {
            var f=F(new[]{0,7},50);var factory=Factory();using var calculator=new OddsCalculator(factory,f,2,2,0);
            Assert.That(calculator.Work.Wait(60000),Is.True);Assert.That(calculator.Work.Result.Samples,Is.EqualTo(2));Assert.That(f.health,Is.EqualTo(new[]{50,50}));Assert.That(f.seed,Is.EqualTo(104729));
        }
        [Test] public void WalletPersistsBetTicksPayoutAndRejectsRepeatedSettlement()
        {
            string directory=Path.Combine(Path.GetTempPath(),"armorb-test-"+Guid.NewGuid().ToString("N"));
            try
            {
                var wallet=new WalletStore(directory);Assert.That(wallet.Place(F(new[]{0,1}),1,100,235),Is.True);Assert.That(wallet.Data.balance,Is.EqualTo(900));
                Assert.That(wallet.Place(F(new[]{0,1}),0,10,200),Is.False);wallet.SaveTicks(617);string id=wallet.Data.pending.id;
                wallet=new WalletStore(directory);Assert.That(wallet.Data.pending.ticks,Is.EqualTo(617));Assert.That(wallet.Settle(id,1),Is.EqualTo(235));
                Assert.That(wallet.Data.balance,Is.EqualTo(1135));Assert.That(wallet.Settle(id,1),Is.Zero);Assert.That(new WalletStore(directory).Data.balance,Is.EqualTo(1135));
            }
            finally{Directory.Delete(directory,true);}
        }
        [Test] public void WalletDrawRefundAndFreeRefillFollowRules()
        {
            string directory=Path.Combine(Path.GetTempPath(),"armorb-test-"+Guid.NewGuid().ToString("N"));
            try
            {
                var w=new WalletStore(directory);Assert.That(w.Place(F(new[]{0,1}),0,1000,200),Is.True);Assert.That(w.Refill(),Is.False);
                w.Settle(w.Data.pending.id,-1);Assert.That(w.Data.balance,Is.EqualTo(1000));
                w.Place(F(new[]{0,1}),0,1000,200);w.Settle(w.Data.pending.id,1);Assert.That(w.Data.balance,Is.Zero);Assert.That(w.Refill(),Is.True);Assert.That(w.Refill(),Is.False);
                Assert.That(w.Place(F(new[]{0,1}),0,11,200),Is.False);Assert.That(w.Place(F(new[]{0,1}),0,1010,200),Is.False);
            }
            finally{Directory.Delete(directory,true);}
        }
        [Test] public void WalletRecoversBackupAndRefundsIncompatibleRules()
        {
            string directory=Path.Combine(Path.GetTempPath(),"armorb-test-"+Guid.NewGuid().ToString("N"));
            try
            {
                var w=new WalletStore(directory);w.Place(F(new[]{0,1}),0,100,200);w.SaveTicks(12);
                File.WriteAllText(Path.Combine(directory,"wallet.json"),"{broken");
                w=new WalletStore(directory);Assert.That(w.Data.balance,Is.EqualTo(900));Assert.That(w.Data.pending,Is.Not.Null);
                w.Data.pending.rules="future";w.SaveTicks(14);w=new WalletStore(directory);
                Assert.That(w.Data.pending,Is.Null);Assert.That(w.Data.balance,Is.EqualTo(1000));
                Assert.That(new WalletStore(directory).Data.balance,Is.EqualTo(1000));
            }
            finally{Directory.Delete(directory,true);}
        }
        [Test] public void LargeValidPayoutSurvivesReloadWithoutIntegerOverflow()
        {
            string directory=Path.Combine(Path.GetTempPath(),"armorb-test-"+Guid.NewGuid().ToString("N"));
            try
            {
                var w=new WalletStore(directory);w.Data.balance=1000000000000L;
                Assert.That(w.Place(F(new[]{0,1}),0,1000000000000L,250),Is.True);
                Assert.That(w.Settle(w.Data.pending.id,0),Is.EqualTo(2500000000000L));
                Assert.That(new WalletStore(directory).Data.balance,Is.EqualTo(2500000000000L));
                Assert.That(WalletStore.Payout(10000000000000000L,200),Is.EqualTo(20000000000000000L));
            }
            finally{Directory.Delete(directory,true);}
        }
        [Test] public void SavedTickCountReconstructsExactContactAndProjectileState()
        {
            var factory=Factory();var f=F(new[]{5,7,4,6,2},300);var a=factory.Create(f);for(int i=0;i<1000;i++)a.Step(1f/120);
            int saved=a.TickCount;var b=factory.Create(f);for(int i=0;i<saved;i++)b.Step(1f/120);
            Run(a,2);Run(b,2);Assert.That(b.TickCount,Is.EqualTo(a.TickCount));Assert.That(b.Projectiles.Count,Is.EqualTo(a.Projectiles.Count));
            for(int i=0;i<f.Count;i++){Assert.That(b.Orbs[i].Position,Is.EqualTo(a.Orbs[i].Position));Assert.That(b.Orbs[i].Health.Current,Is.EqualTo(a.Orbs[i].Health.Current));Assert.That(b.Orbs[i].Weapon.Hits,Is.EqualTo(a.Orbs[i].Weapon.Hits));}
        }
    }
}
