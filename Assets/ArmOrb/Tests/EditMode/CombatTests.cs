using System;
using NUnit.Framework;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class CombatTests
    {
        static WeaponState Weapon(WeaponKind kind) => new WeaponState(kind, 1, kind == WeaponKind.Spear ? 3 : 2.6f,
            180, kind switch { WeaponKind.Spear => 0.5f, WeaponKind.Blade => 30, WeaponKind.Shield => 0.25f, _ => 1 });

        [Test] public void SwordDealsCurrentDamageThenGrows()
        {
            var target = new HealthState(100); var sword = Weapon(WeaponKind.Sword);
            for (int i = 0; i < 3; i++) { target.Damage(sword.Damage); sword.OnHit(target); }
            Assert.That(target.Current, Is.EqualTo(94)); Assert.That(sword.Damage, Is.EqualTo(4));
        }
        [Test] public void SpearAndBladeGrowWithoutGameplayCaps()
        {
            var target = new HealthState(10000); var spear = Weapon(WeaponKind.Spear); var blade = Weapon(WeaponKind.Blade);
            for (int i = 0; i < 200; i++) { spear.OnHit(target); blade.OnHit(target); }
            Assert.That(spear.Reach, Is.EqualTo(103)); Assert.That(blade.RotationSpeed, Is.EqualTo(6180));
            Assert.That(spear.Damage, Is.EqualTo(1)); Assert.That(blade.Damage, Is.EqualTo(1));
        }
        [Test] public void PoisonStacksOnTargetAndPersists()
        {
            var target = new HealthState(100); var scythe = Weapon(WeaponKind.Scythe);
            scythe.OnHit(target); scythe.OnHit(target); scythe.OnHit(target);
            Assert.That(target.PoisonPerSecond, Is.EqualTo(3));
            target.Damage(target.PoisonPerSecond * 8); Assert.That(target.Current, Is.EqualTo(76));
            Assert.That(target.PoisonPerSecond, Is.EqualTo(3));
        }
        [Test] public void ShieldUsesCurrentReflectionThenIncreasesIt()
        {
            var shield = Weapon(WeaponKind.Shield); var attacker = new HealthState(100);
            attacker.Damage(shield.Block(4)); attacker.Damage(shield.Block(4));
            Assert.That(attacker.Current, Is.EqualTo(91)); Assert.That(shield.Reflection, Is.EqualTo(1.5f));
            Assert.That(shield.Hits, Is.Zero); Assert.That(shield.Blocks, Is.EqualTo(2));
        }
        [Test] public void FreshFightDoesNotRetainPoisonOrGrowth()
        {
            var old = new ArenaSimulation(Weapon(WeaponKind.Sword), Weapon(WeaponKind.Scythe));
            old.Orbs[0].Weapon.OnHit(old.Orbs[1].Health); old.Orbs[0].Health.AddPoison(10);
            var fresh = new ArenaSimulation(Weapon(WeaponKind.Sword), Weapon(WeaponKind.Scythe));
            Assert.That(fresh.Orbs[0].Weapon.Damage, Is.EqualTo(1)); Assert.That(fresh.Orbs[0].Health.PoisonPerSecond, Is.Zero);
        }
        [Test] public void ShieldContactBlocksAndReflectsOnlyOnceUntilSeparation()
        {
            var sim = new ArenaSimulation(new WeaponState(WeaponKind.Shield,1,2.6f,0,0.25f),
                new WeaponState(WeaponKind.Sword,4,2.6f,0,1),100,100,speed:0);
            sim.Orbs[0].Position = new Vector2(-1.1f,0); sim.Orbs[0].Angle = 0;
            sim.Orbs[1].Position = new Vector2(1.1f,0); sim.Orbs[1].Angle = 180;
            for(int i=0;i<30;i++) sim.Step(1f/120);
            Assert.That(sim.Orbs[0].Health.Current, Is.EqualTo(100));
            Assert.That(sim.Orbs[1].Health.Current, Is.EqualTo(96));
            Assert.That(sim.Orbs[0].Weapon.Blocks, Is.EqualTo(1));
            Assert.That(sim.Orbs[1].Weapon.Hits, Is.Zero);
        }
        [Test] public void WallAndBodyContactsPreserveBoundsAndMovement()
        {
            var sim = new ArenaSimulation(Weapon(WeaponKind.Shield), Weapon(WeaponKind.Shield), 99999, 99999);
            sim.Orbs[0].Position = new Vector2(4.44f, 4.44f); sim.Orbs[0].Velocity = Vector2.one.normalized * sim.Speed;
            sim.Step(1f/120);
            Assert.That(sim.Orbs[0].Velocity.x, Is.LessThan(0)); Assert.That(sim.Orbs[0].Velocity.y, Is.LessThan(0));
            sim.Orbs[0].Position = Vector2.zero; sim.Orbs[1].Position = Vector2.right * 0.5f;
            sim.Orbs[0].Velocity = Vector2.right * sim.Speed; sim.Orbs[1].Velocity = Vector2.left * sim.Speed;
            sim.Step(1f/120);
            Assert.That(Vector2.Distance(sim.Orbs[0].Position,sim.Orbs[1].Position), Is.GreaterThanOrEqualTo(sim.Radius * 2 - 0.001f));
            Assert.That(sim.Orbs[0].Velocity.magnitude, Is.EqualTo(sim.Speed).Within(0.001f));
        }
        [Test] public void ContinuousContactCannotHitEveryPhysicsTick()
        {
            var sim = new ArenaSimulation(new WeaponState(WeaponKind.Sword,1,2.6f,0,1),
                new WeaponState(WeaponKind.Sword,1,2.6f,0,1),100,100,speed:0);
            sim.Orbs[0].Position = new Vector2(-0.7f,0); sim.Orbs[0].Angle = 0;
            sim.Orbs[1].Position = new Vector2(0.7f,0); sim.Orbs[1].Angle = 90;
            for (int i = 0; i < 30; i++) sim.Step(1f/120);
            Assert.That(sim.Orbs[0].Weapon.Hits, Is.EqualTo(1)); Assert.That(sim.Orbs[1].Health.Current, Is.EqualTo(99));
            sim.Orbs[1].Position = new Vector2(4,0); sim.Step(1f/120);
            sim.Orbs[1].Position = new Vector2(0.7f,0); sim.Step(1f/120);
            Assert.That(sim.Orbs[0].Weapon.Hits, Is.EqualTo(2)); Assert.That(sim.Orbs[1].Health.Current, Is.EqualTo(97));
        }
        [Test] public void EqualPoisonDeathsAreADrawAndFinishedSimulationFreezes()
        {
            var sim = new ArenaSimulation(Weapon(WeaponKind.Sword), Weapon(WeaponKind.Sword),1,1,speed:0);
            sim.Orbs[0].Health.AddPoison(1); sim.Orbs[1].Health.AddPoison(1);
            for(int i=0; i<125; i++) sim.Step(1f/120);
            Assert.That(sim.Result, Is.EqualTo(MatchResult.Draw));
            float time = sim.Elapsed; sim.Step(1); Assert.That(sim.Elapsed, Is.EqualTo(time));
        }
        [Test] public void SameSeedReplaysIdentically()
        {
            var a = new ArenaSimulation(Weapon(WeaponKind.Spear), Weapon(WeaponKind.Blade), seed: 342);
            var b = new ArenaSimulation(Weapon(WeaponKind.Spear), Weapon(WeaponKind.Blade), seed: 342);
            for(int i=0;i<120*30;i++) { a.Step(1f/120); b.Step(1f/120); }
            for(int i=0;i<2;i++)
            {
                Assert.That(a.Orbs[i].Position, Is.EqualTo(b.Orbs[i].Position));
                Assert.That(a.Orbs[i].Health.Current, Is.EqualTo(b.Orbs[i].Health.Current));
                Assert.That(a.Orbs[i].Weapon.Hits, Is.EqualTo(b.Orbs[i].Weapon.Hits));
            }
        }
        [Test] public void FastBladeAndLongSpearStillHit()
        {
            var blade = new WeaponState(WeaponKind.Blade,1,2.6f,12000,0);
            var sim = new ArenaSimulation(blade,new WeaponState(WeaponKind.Spear,1,40,180,0),9999,9999,12);
            int hits=0; sim.Event += e => { if(e.Kind==CombatEventKind.Hit || e.Kind==CombatEventKind.Block) hits++; };
            for(int i=0;i<120*15;i++) sim.Step(1f/120);
            Assert.That(hits, Is.GreaterThan(0));
            for(int i=0;i<2;i++) Assert.That(float.IsNaN(sim.Orbs[i].Position.x), Is.False);
        }
        [Test] public void SegmentCollisionHandlesCrossingParallelAndSeparatedWeapons()
        {
            var a = new WeaponSegment(new Vector2(-1,0),new Vector2(1,0),0.1f);
            var crossing = new WeaponSegment(new Vector2(0,-1),new Vector2(0,1),0.1f);
            var parallel = new WeaponSegment(new Vector2(-1,0.1f),new Vector2(1,0.1f),0.1f);
            var separate = new WeaponSegment(new Vector2(3,0),new Vector2(4,0),0.1f);
            Assert.That(WeaponGeometry.SegmentDistanceSquared(a,crossing), Is.Zero);
            Assert.That(WeaponGeometry.SegmentDistanceSquared(a,parallel), Is.EqualTo(0.01f).Within(0.00001f));
            Assert.That(WeaponGeometry.SegmentDistanceSquared(a,separate), Is.EqualTo(4));
        }

        [TestCase(0,0)] [TestCase(0,1)] [TestCase(0,2)] [TestCase(0,3)] [TestCase(0,4)]
        [TestCase(1,1)] [TestCase(1,2)] [TestCase(1,3)] [TestCase(1,4)]
        [TestCase(2,2)] [TestCase(2,3)] [TestCase(2,4)]
        [TestCase(3,3)] [TestCase(3,4)] [TestCase(4,4)]
        public void EveryMatchupMakesProgressAndStaysValid(int first, int second)
        {
            int finished = 0;
            for (int seed = 1; seed <= 3; seed++)
            {
                // Low health makes completion meaningful even for shield mirror matches.
                var sim = new ArenaSimulation(Weapon((WeaponKind)first), Weapon((WeaponKind)second),10,10,seed);
                for(int tick=0; tick<120*240 && sim.Result==MatchResult.InProgress; tick++)
                {
                    sim.Step(1f/120);
                    for(int i=0;i<2;i++)
                    {
                        var p=sim.Orbs[i].Position;
                        Assert.That(float.IsNaN(p.x) || float.IsNaN(p.y), Is.False);
                        Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(sim.Size/2-sim.Radius+0.001f));
                        Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(sim.Size/2-sim.Radius+0.001f));
                    }
                }
                Assert.That(sim.Orbs[0].Health.Current + sim.Orbs[1].Health.Current, Is.LessThan(20), "A matchup caused no damage.");
                if(sim.Result!=MatchResult.InProgress) finished++;
                TestContext.WriteLine($"{(WeaponKind)first} vs {(WeaponKind)second}, seed={seed}, {sim.Result}, t={sim.Elapsed:0.00}, HP={sim.Orbs[0].Health.Current}/{sim.Orbs[1].Health.Current}");
            }
            Assert.That(finished, Is.GreaterThan(0), "No seed finished in four simulated minutes.");
        }
    }
}
