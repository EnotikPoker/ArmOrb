using NUnit.Framework;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionThreeTests
    {
        static WeaponState W(WeaponKind kind, float cooldown=1, float projectileSpeed=14) => new WeaponState(kind,
            rotationSpeed:0, growth:kind==WeaponKind.Unarmed?0.4f:kind==WeaponKind.Staff?0.275f:kind==WeaponKind.Shield?0.25f:1,
            cooldown:cooldown,projectileSpeed:projectileSpeed);
        static ArenaSimulation Still(WeaponKind first,WeaponKind second=WeaponKind.Bow,float cooldown=1)
        {
            var sim=new ArenaSimulation(W(first,cooldown),W(second),100,100,size:8,speed:0);
            sim.Orbs[0].Position=new Vector2(-2,0);sim.Orbs[1].Position=new Vector2(2,0);
            sim.Orbs[0].Velocity=sim.Orbs[1].Velocity=Vector2.zero;
            sim.Orbs[0].Angle=0;sim.Orbs[1].Angle=180;
            sim.Orbs[1].Weapon.ReloadRemaining=1000;
            return sim;
        }
        static ProjectileState Shot(ArenaSimulation sim)
        {
            sim.Orbs[0].Weapon.ReloadRemaining=0;sim.Step(1f/120);
            Assert.That(sim.Projectiles.Count,Is.GreaterThan(0));
            sim.Orbs[0].Weapon.ReloadRemaining=1000;
            return sim.Projectiles[0];
        }
        static void Run(ArenaSimulation sim,float seconds)
        { for(int i=0;i<Mathf.CeilToInt(seconds*120);i++)sim.Step(1f/120); }

        [Test] public void UnarmedBodyHitsBothSidesOnceAndKeepsOneDamage()
        {
            var sim=Still(WeaponKind.Unarmed,WeaponKind.Unarmed);
            sim.Orbs[0].Position=new Vector2(-0.5f,0);sim.Orbs[1].Position=new Vector2(0.5f,0);
            Run(sim,0.2f);
            for(int i=0;i<2;i++)
            {
                Assert.That(sim.Orbs[i].Health.Current,Is.EqualTo(99));
                Assert.That(sim.Orbs[i].Weapon.SpeedBonus,Is.EqualTo(0.4f));
                Assert.That(sim.Orbs[i].Weapon.Damage,Is.EqualTo(1));
            }
            sim.Orbs[0].Position=new Vector2(-2,0);sim.Step(1f/120);
            sim.Orbs[0].Position=sim.Orbs[1].Position-Vector2.right;
            sim.Step(1f/120);Assert.That(sim.Orbs[0].Weapon.Hits,Is.EqualTo(2));
        }
        [Test] public void UnarmedGrowthImmediatelyIncreasesVelocityAndSurvivesBodyBounce()
        {
            var sim=new ArenaSimulation(W(WeaponKind.Unarmed),W(WeaponKind.Bow));
            sim.Orbs[0].Position=new Vector2(-0.5f,0);sim.Orbs[1].Position=new Vector2(0.5f,0);
            sim.Orbs[0].Velocity=Vector2.right*sim.Speed;sim.Orbs[1].Velocity=Vector2.left*sim.Speed;
            sim.Step(1f/120);
            Assert.That(sim.Orbs[0].Velocity.magnitude,Is.EqualTo(6.8f).Within(0.001f));
            Assert.That(sim.Orbs[1].Velocity.magnitude,Is.EqualTo(6.4f).Within(0.001f));
        }
        [Test] public void BowUsesCooldownAndSnapshotsSimultaneousFan()
        {
            var sim=Still(WeaponKind.Bow);
            Run(sim,0.5f);Assert.That(sim.Projectiles.Count,Is.Zero);
            sim.Orbs[0].Weapon.OnHit(sim.Orbs[1].Health);sim.Orbs[0].Weapon.OnHit(sim.Orbs[1].Health);
            Shot(sim);Assert.That(sim.Projectiles.Count,Is.EqualTo(3));
            Assert.That(Vector2.SignedAngle(sim.Projectiles[0].Velocity,sim.Projectiles[2].Velocity),Is.EqualTo(60).Within(0.001));
            Assert.That(sim.Projectiles[1].Velocity.y,Is.EqualTo(0).Within(0.001));
            sim.Orbs[0].Weapon.OnHit(sim.Orbs[1].Health);
            Assert.That(sim.Projectiles.Count,Is.EqualTo(3));
        }
        [Test] public void FastArrowHitsMovingBodyWithoutTunnelling()
        {
            var sim=Still(WeaponKind.Bow);var p=Shot(sim);
            p.Position=new Vector2(-1,0);p.Velocity=new Vector2(1000,0);
            sim.Orbs[1].Position=Vector2.zero;
            sim.Step(1f/120);
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(99));
            Assert.That(sim.Orbs[0].Weapon.ArrowCount,Is.EqualTo(2));
            Assert.That(sim.Projectiles.Count,Is.Zero);
            Assert.That(ProjectileCollision.Circle(new Vector2(-1,1),new Vector2(1,-1),0.1f),Is.LessThan(1));
        }
        [Test] public void ShieldConsumesArrowAndReturnsDamageWithoutGrowingShooter()
        {
            var sim=Still(WeaponKind.Bow,WeaponKind.Shield);Shot(sim);Run(sim,0.5f);
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(100));
            Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(99));
            Assert.That(sim.Orbs[1].Weapon.Blocks,Is.EqualTo(1));
            Assert.That(sim.Orbs[0].Weapon.ArrowCount,Is.EqualTo(1));
            Assert.That(sim.Projectiles.Count,Is.Zero);
        }
        [Test] public void ShurikenCornerCostsOneBounceAndNextWallDestroysIt()
        {
            var sim=Still(WeaponKind.Shuriken);var p=Shot(sim);
            p.Position=new Vector2(3.75f,3.75f);p.Velocity=new Vector2(11,11);
            sim.Step(1f/120);
            Assert.That(p.Bounces,Is.Zero);Assert.That(p.Velocity.x,Is.LessThan(0));Assert.That(p.Velocity.y,Is.LessThan(0));
            sim.Orbs[0].Position=new Vector2(-3,3);sim.Orbs[1].Position=new Vector2(3,-3);
            Run(sim,0.8f);Assert.That(sim.Projectiles.Count,Is.Zero);
        }
        [Test] public void ShurikenHitIncreasesFutureBounceBudgetOnly()
        {
            var sim=Still(WeaponKind.Shuriken);Shot(sim);Run(sim,0.4f);
            Assert.That(sim.Orbs[0].Weapon.Bounces,Is.EqualTo(2));
            var next=Shot(sim);Assert.That(next.Bounces,Is.EqualTo(2));
        }
        [Test] public void StaffWallExplosionHurtsOnceAndGrowsFutureRadius()
        {
            var sim=Still(WeaponKind.Staff);var p=Shot(sim);
            p.Position=new Vector2(3.8f,0);p.Velocity=Vector2.right*8;
            sim.Orbs[1].Position=new Vector2(3.2f,0);
            sim.Step(1f/120);Assert.That(sim.Waves.Count,Is.EqualTo(1));
            Assert.That(sim.Waves[0].MaximumRadius,Is.EqualTo(1.1f));
            Run(sim,0.4f);
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(99));
            Assert.That(sim.Orbs[0].Weapon.BlastRadius,Is.EqualTo(1.375f).Within(0.00001));
            Assert.That(sim.Projectiles.Count,Is.Zero);Assert.That(sim.Waves.Count,Is.Zero);
            Assert.That(Shot(sim).BlastRadius,Is.EqualTo(1.375f).Within(0.00001));
        }
        [Test] public void StaffExplodesOnShieldAndWavePassesThroughWithoutReflection()
        {
            var sim=Still(WeaponKind.Staff,WeaponKind.Shield);Shot(sim);Run(sim,0.7f);
            Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(100));
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(99));
            Assert.That(sim.Orbs[1].Weapon.Blocks,Is.Zero);
            Assert.That(sim.Orbs[0].Weapon.Hits,Is.EqualTo(1));
        }
        [Test] public void RingDetectsMovingCrossingsAndDoesNotDamageAlreadyPassedInterior()
        {
            Assert.That(ProjectileCollision.Ring(new Vector2(2,0),new Vector2(-2,0),1,1.1f,0.1f),Is.True);
            Assert.That(ProjectileCollision.Ring(Vector2.zero,Vector2.zero,1,1.2f,0.1f),Is.False);
            Assert.That(ProjectileCollision.Ring(new Vector2(1.5f,0),new Vector2(2,0),0,1,0.1f),Is.False);
        }
        [Test] public void RangedSimulationIsIdenticalAcrossPlaybackRatesAndFreshMatchHasNoEffects()
        {
            ArenaSimulation baseline=null;
            foreach(float speed in new[]{0.5f,1,2})
            {
                var sim=new ArenaSimulation(W(WeaponKind.Bow,0.2f),W(WeaponKind.Staff,0.3f),999,999,731,size:8,gravity:true);
                var clock=new SimulationClock(120);
                for(int frame=0;frame<(int)(6/speed*60);frame++)
                    for(int ticks=clock.Consume(1.0/60,speed);ticks>0;ticks--)sim.Step((float)clock.StepSeconds);
                if(baseline!=null)
                {
                    for(int i=0;i<2;i++)
                    {
                        Assert.That(sim.Orbs[i].Position,Is.EqualTo(baseline.Orbs[i].Position));
                        Assert.That(sim.Orbs[i].Health.Current,Is.EqualTo(baseline.Orbs[i].Health.Current));
                        Assert.That(sim.Orbs[i].Weapon.Hits,Is.EqualTo(baseline.Orbs[i].Weapon.Hits));
                        Assert.That(sim.Orbs[i].Weapon.ReloadRemaining,Is.EqualTo(baseline.Orbs[i].Weapon.ReloadRemaining));
                    }
                    Assert.That(sim.Projectiles.Count,Is.EqualTo(baseline.Projectiles.Count));
                    for(int p=0;p<sim.Projectiles.Count;p++)Assert.That(sim.Projectiles[p].Position,Is.EqualTo(baseline.Projectiles[p].Position));
                }
                baseline=sim;
            }
            var fresh=Still(WeaponKind.Bow);Assert.That(fresh.Projectiles.Count,Is.Zero);Assert.That(fresh.Waves.Count,Is.Zero);
            Assert.That(fresh.Orbs[0].Weapon.ArrowCount,Is.EqualTo(1));
        }
        [Test] public void SimultaneousUnarmedDeathsAreDraw()
        {
            var sim=Still(WeaponKind.Unarmed,WeaponKind.Unarmed);
            sim.Orbs[0].Health.Damage(99);sim.Orbs[1].Health.Damage(99);
            sim.Orbs[0].Position=new Vector2(-0.5f,0);sim.Orbs[1].Position=new Vector2(0.5f,0);
            sim.Step(1f/120);Assert.That(sim.Result,Is.EqualTo(MatchResult.Draw));
            float elapsed=sim.Elapsed;sim.Step(1);Assert.That(sim.Elapsed,Is.EqualTo(elapsed));
        }
        [Test] public void GravityMaintainsLateralMotionAndArenaRelativeBounceHeight()
        {
            foreach(float size in new[]{6f,8,10})
            {
                var sim=new ArenaSimulation(W(WeaponKind.Bow),W(WeaponKind.Bow),9999,9999,size:size,gravity:true);
                sim.Orbs[0].Position=new Vector2(-2,-size/2+sim.Radius+0.001f);sim.Orbs[0].Velocity=new Vector2(0,-1);
                sim.Step(1f/120);
                Assert.That(Mathf.Abs(sim.Orbs[0].Velocity.x),Is.GreaterThanOrEqualTo(6.4f*0.4f-0.001));
                Assert.That(sim.Orbs[0].Velocity.y,Is.GreaterThan(sim.MinimumBounce-0.2f));
                Assert.That(sim.MinimumBounce*sim.MinimumBounce/(2*sim.Gravity),Is.EqualTo((size-2*sim.Radius)*0.85f).Within(0.001));
            }
        }
        [Test] public void ShieldBlockDoesNotGiveUnarmedGrowth()
        {
            var sim=Still(WeaponKind.Unarmed,WeaponKind.Shield);
            sim.Orbs[0].Position=new Vector2(1,0);sim.Step(1f/120);
            Assert.That(sim.Orbs[0].Weapon.Hits,Is.Zero);
            Assert.That(sim.Orbs[0].Weapon.SpeedBonus,Is.Zero);
            Assert.That(sim.Orbs[1].Weapon.Blocks,Is.EqualTo(1));
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(100));
        }
        [Test] public void ProjectileCannotHitItsOwnerAndMissDoesNotGrow()
        {
            var sim=Still(WeaponKind.Bow);var p=Shot(sim);
            p.Position=sim.Orbs[0].Position;p.Velocity=Vector2.up*14;
            Run(sim,0.4f);
            Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(100));
            Assert.That(sim.Orbs[0].Weapon.Hits,Is.Zero);
            Assert.That(sim.Orbs[0].Weapon.ArrowCount,Is.EqualTo(1));
            Assert.That(sim.Projectiles.Count,Is.Zero);
        }
        [Test] public void AllNewMatchupsProduceDamageAndStayBounded()
        {
            for(int a=0;a<9;a++)for(int b=Mathf.Max(a,5);b<9;b++)
            {
                var sim=new ArenaSimulation(W((WeaponKind)a),W((WeaponKind)b),20,20,seed:37+a*9+b,size:8,gravity:true);
                for(int tick=0;tick<120*45 && sim.Result==MatchResult.InProgress;tick++)sim.Step(1f/120);
                Assert.That(sim.Orbs[0].Health.Current+sim.Orbs[1].Health.Current,Is.LessThan(40),$"No damage: {a}/{b}");
                foreach(var orb in sim.Orbs)
                {
                    Assert.That(float.IsNaN(orb.Position.x)||float.IsNaN(orb.Position.y),Is.False);
                    Assert.That(Mathf.Abs(orb.Position.x),Is.LessThanOrEqualTo(3.4501f));
                    Assert.That(Mathf.Abs(orb.Position.y),Is.LessThanOrEqualTo(3.4501f));
                }
            }
        }
    }
}
