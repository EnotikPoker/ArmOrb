using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionFourTests
    {
        static WeaponState W(WeaponKind kind,float rotation=0) => new WeaponState(kind,rotationSpeed:rotation,growth:kind==WeaponKind.Shield?0.25f:1);
        static ArenaSimulation Still(WeaponKind a,WeaponKind b=WeaponKind.Shield)
        {
            var sim=new ArenaSimulation(W(a),W(b),100,100,size:8,speed:0);
            sim.Orbs[0].Position=new Vector2(-2,0);sim.Orbs[1].Position=new Vector2(2,0);
            sim.Orbs[0].Velocity=sim.Orbs[1].Velocity=Vector2.zero;sim.Orbs[0].Angle=0;sim.Orbs[1].Angle=180;
            sim.Orbs[1].Weapon.ReloadRemaining=1000;return sim;
        }
        static void Run(ArenaSimulation sim,float seconds) { for(int i=0;i<Mathf.CeilToInt(seconds*120);i++)sim.Step(1f/120); }
        static ProjectileState Shoot(ArenaSimulation sim)
        {
            sim.Orbs[0].Weapon.ReloadRemaining=0;sim.Step(1f/120);
            sim.Orbs[0].Weapon.ReloadRemaining=1000;return sim.Projectiles[0];
        }
        [Test] public void LowBounceConservesEnergyAndDoesNotForceHorizontalMotion()
        {
            var sim=new ArenaSimulation(W(WeaponKind.Bow),W(WeaponKind.Bow),size:8,gravity:true);
            foreach(var orb in sim.Orbs)orb.Weapon.ReloadRemaining=1000;
            var a=sim.Orbs[0];a.Position=new Vector2(-2,-3.35f);a.Velocity=new Vector2(0.03f,-1);
            sim.Orbs[1].Position=new Vector2(2,2);sim.Orbs[1].Velocity=Vector2.zero;
            float energy=0.5f*a.Velocity.sqrMagnitude+sim.Gravity*(a.Position.y+3.45f),highest=a.Position.y;
            for(int i=0;i<360;i++)
            {
                sim.Step(1f/120);highest=Mathf.Max(highest,a.Position.y);
                Assert.That(0.5f*a.Velocity.sqrMagnitude+sim.Gravity*(a.Position.y+3.45f),Is.EqualTo(energy).Within(0.002));
                Assert.That(a.Velocity.x,Is.EqualTo(0.03f).Within(0.00001));
            }
            Assert.That(highest,Is.LessThan(-3.30f));
        }
        [Test] public void BodyCollisionExchangesNormalVelocityWithoutAddingEnergy()
        {
            var a=new CombatantState(100,W(WeaponKind.Bow)){Position=new Vector2(-0.5f,0),Velocity=new Vector2(5,3)};
            var b=new CombatantState(100,W(WeaponKind.Bow)){Position=new Vector2(0.5f,0),Velocity=new Vector2(-2,-1)};
            Vector2 momentum=a.Velocity+b.Velocity;float energy=a.Velocity.sqrMagnitude+b.Velocity.sqrMagnitude;
            OrbMotion.Resolve(a,b,0.55f,6.4f,true);
            Assert.That(a.Velocity,Is.EqualTo(new Vector2(-2,3)));Assert.That(b.Velocity,Is.EqualTo(new Vector2(5,-1)));
            Assert.That(a.Velocity+b.Velocity,Is.EqualTo(momentum));Assert.That(a.Velocity.sqrMagnitude+b.Velocity.sqrMagnitude,Is.EqualTo(energy));
        }
        [Test] public void WallBounceDoesNotReverseWeaponRotation()
        {
            var sim=new ArenaSimulation(W(WeaponKind.Sword,372),W(WeaponKind.Bow),size:8,gravity:true);
            sim.Orbs[0].Position=new Vector2(3.44f,1);sim.Orbs[0].Velocity=new Vector2(6,0);sim.Orbs[0].Angle=90;
            sim.Orbs[1].Position=new Vector2(-2,-2);sim.Step(1f/120);
            Assert.That(sim.Orbs[0].Velocity.x,Is.LessThan(0));Assert.That(sim.Orbs[0].RotationDirection,Is.EqualTo(1));
            Assert.That(sim.Orbs[0].Angle,Is.EqualTo(93.1f).Within(0.001));
        }
        [Test] public void WeaponSettingsUseMeasuredDifferentAngularSpeeds()
        {
            foreach(var pair in new[]{(WeaponKind.Sword,372f),(WeaponKind.Spear,286f),(WeaponKind.Staff,172f),(WeaponKind.Blade,573f)})
            {
                var asset=AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{pair.Item1}.asset");
                Assert.That(asset.CreateState().RotationSpeed,Is.EqualTo(pair.Item2));
            }
            var blade=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/ArmOrb/Settings/Blade.asset");
            var sword=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/ArmOrb/Settings/Sword.asset");
            var bladeBox=new WeaponColliderBuffer();var swordBox=new WeaponColliderBuffer();
            WeaponGeometry.Fill(blade.CreateState(),0.55f,Vector2.zero,0,bladeBox);
            WeaponGeometry.Fill(sword.CreateState(),0.55f,Vector2.zero,0,swordBox);
            Assert.That(bladeBox.Bounds.xMax,Is.LessThan(swordBox.Bounds.xMax-0.25f));
            Assert.That(bladeBox.Bounds.height,Is.LessThan(swordBox.Bounds.height));
        }
        [Test] public void FastBladeKeepsSignedTravelBeyondHalfTurnForInterpolation()
        {
            var sim=new ArenaSimulation(W(WeaponKind.Blade,30000),W(WeaponKind.Bow),size:8,speed:0);
            sim.Orbs[0].Position=new Vector2(-3,0);sim.Orbs[1].Position=new Vector2(3,0);
            sim.Orbs[0].Angle=20;sim.Step(1f/120);
            Assert.That(sim.Orbs[0].AngularTravel,Is.EqualTo(250).Within(0.01));Assert.That(sim.Orbs[0].Angle,Is.EqualTo(270).Within(0.01));
        }
        [Test] public void BowReleasesSequentialParallelArrowsAndReloadsAfterBurst()
        {
            var sim=Still(WeaponKind.Bow,WeaponKind.Bow);var weapon=sim.Orbs[0].Weapon;
            weapon.OnHit(sim.Orbs[1].Health);weapon.OnHit(sim.Orbs[1].Health);
            sim.Orbs[0].Velocity=new Vector2(0.2f,0);
            var times=new List<float>();var offsets=new List<Vector2>();var velocities=new List<Vector2>();
            sim.Event+=e=>
            {
                if(e.Kind!=CombatEventKind.Shot || e.Side!=0)return;
                var p=sim.Projectiles[sim.Projectiles.Count-1];times.Add(sim.Elapsed);offsets.Add(p.Position-sim.Orbs[0].Position);velocities.Add(p.Velocity);
            };
            weapon.ReloadRemaining=0;sim.Step(1f/120);Assert.That(times.Count,Is.EqualTo(1));
            weapon.OnHit(sim.Orbs[1].Health); // Does not extend the current three-arrow burst.
            Run(sim,0.15f);Assert.That(times.Count,Is.EqualTo(3));Assert.That(weapon.BurstRemaining,Is.Zero);
            for(int i=1;i<3;i++)
            {
                Assert.That(times[i]-times[i-1],Is.EqualTo(0.06f).Within(0.009));
                Assert.That(Vector2.Distance(offsets[i],offsets[0]),Is.LessThan(0.0001));Assert.That(velocities[i],Is.EqualTo(velocities[0]));
            }
            Assert.That(weapon.ReloadRemaining,Is.InRange(0.94f,1));
            Run(sim,0.5f);Assert.That(times.Count,Is.EqualTo(3));
        }
        [Test] public void RangedHiltHasPhysicalContourButCannotDealOrReturnMeleeDamage()
        {
            var sim=Still(WeaponKind.Staff);sim.Orbs[0].Position=new Vector2(-1.1f,0);sim.Orbs[1].Position=new Vector2(1.1f,0);
            sim.Orbs[0].Weapon.ReloadRemaining=1000;
            var a=new WeaponColliderBuffer();var b=new WeaponColliderBuffer();
            WeaponGeometry.Fill(sim.Orbs[0],sim.Radius,a);WeaponGeometry.Fill(sim.Orbs[1],sim.Radius,b);
            Assert.That(a.Count,Is.GreaterThan(0));Assert.That(WeaponGeometry.Clash(a,b),Is.True);
            Run(sim,0.2f);Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(100));Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(100));
            Assert.That(sim.Orbs[1].Weapon.Blocks,Is.Zero);Assert.That(sim.Orbs[0].Weapon.Hits,Is.Zero);
        }
        [Test] public void ShieldChangesProjectileOwnerBeforeAnyReturnedDamage()
        {
            var sim=Still(WeaponKind.Bow);var p=Shoot(sim);
            for(int i=0;i<60 && p.Reflections==0;i++)sim.Step(1f/120);
            Assert.That(p.Owner,Is.EqualTo(1));Assert.That(p.Velocity.x,Is.LessThan(0));
            Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(100));Assert.That(sim.Orbs[1].Weapon.ShieldWidth,Is.EqualTo(2.1f));
            Run(sim,0.5f);
            Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(99));Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(100));
            Assert.That(sim.Orbs[1].Weapon.ShieldWidth,Is.EqualTo(2.35f).Within(0.001));Assert.That(sim.Orbs[0].Weapon.ArrowCount,Is.EqualTo(1));
        }
        [Test] public void ReflectedProjectileMissDoesNotGrowShield()
        {
            var sim=Still(WeaponKind.Crossbow);var p=Shoot(sim);
            for(int i=0;i<60 && p.Reflections==0;i++)sim.Step(1f/120);
            sim.Orbs[0].Position=new Vector2(-2,2);Run(sim,1);
            Assert.That(sim.Orbs[1].Weapon.ShieldWidth,Is.EqualTo(2.1f));Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(100));
        }
        [Test] public void FireballIsTransferredInsteadOfExplodingOnShield()
        {
            var sim=Still(WeaponKind.Staff);var p=Shoot(sim);
            for(int i=0;i<60 && p.Reflections==0;i++)sim.Step(1f/120);
            Assert.That(p.Owner,Is.EqualTo(1));Assert.That(sim.Waves.Count,Is.Zero);
            Run(sim,0.7f);Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(99));Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(100));
            Assert.That(sim.Orbs[1].Weapon.ShieldWidth,Is.EqualTo(2.35f).Within(0.001));Assert.That(sim.Orbs[0].Weapon.BlastRadius,Is.EqualTo(1.1f));
        }
        [Test] public void ShieldGrowthWidensVisibleShapeAndColliderWithoutIncreasingThickness()
        {
            var shield=W(WeaponKind.Shield);var buffer=new WeaponColliderBuffer();
            WeaponGeometry.Fill(shield,0.55f,Vector2.zero,0,buffer);var before=buffer.Bounds;
            shield.OnHit(new HealthState(100));WeaponGeometry.Fill(shield,0.55f,Vector2.zero,0,buffer);
            Assert.That(buffer.Bounds.height,Is.GreaterThan(before.height));Assert.That(buffer.Bounds.width,Is.EqualTo(before.width).Within(0.00001));
            var parts=new WeaponPartPose[2];WeaponGeometry.GetParts(shield,parts);Assert.That(parts[0].Size.y,Is.EqualTo(2.35f).Within(0.00001));
            Assert.That(shield.Damage,Is.Zero);Assert.That(shield.Reflection,Is.EqualTo(1));
        }
        [Test] public void TwoShieldsCannotDamageEachOtherByTouch()
        {
            var sim=Still(WeaponKind.Shield,WeaponKind.Shield);sim.Orbs[0].Position=new Vector2(-0.5f,0);sim.Orbs[1].Position=new Vector2(0.5f,0);
            Run(sim,2);Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(100));Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(100));
            Assert.That(sim.Result,Is.EqualTo(MatchResult.InProgress));
        }
        [Test] public void UnarmedMaximumGrowsButDoesNotArtificiallyAccelerateGravityFlight()
        {
            var weapon=new WeaponState(WeaponKind.Unarmed,growth:0.5f);
            var sim=new ArenaSimulation(weapon,W(WeaponKind.Bow),speed:0,gravity:true,gravityAcceleration:0);
            sim.Orbs[0].Position=new Vector2(-2,0);sim.Orbs[1].Position=new Vector2(2,2);
            sim.Orbs[0].Velocity=new Vector2(3.2f,0);sim.Orbs[1].Velocity=Vector2.zero;
            sim.Step(1f/120);Assert.That(weapon.Damage,Is.EqualTo(2));
            weapon.OnHit(sim.Orbs[1].Health);sim.Step(1f/120);
            Assert.That(weapon.UnarmedMaxSpeed,Is.EqualTo(3.5f));Assert.That(sim.Orbs[0].Velocity.magnitude,Is.EqualTo(3.2f));
            sim.Orbs[0].Velocity=new Vector2(12,0);sim.Step(1f/120);
            Assert.That(sim.Orbs[0].Velocity.magnitude,Is.EqualTo(5.6f).Within(0.001));Assert.That(weapon.Damage,Is.EqualTo(3));
        }
        [Test] public void UnarmedDealsPreImpactSpeedDamageEvenWhenBodyExchangeStopsIt()
        {
            var weapon=new WeaponState(WeaponKind.Unarmed,growth:0.5f);
            var sim=new ArenaSimulation(weapon,W(WeaponKind.Bow),speed:0,gravity:true,gravityAcceleration:0);
            sim.Orbs[0].Position=new Vector2(-0.5f,0);sim.Orbs[1].Position=new Vector2(0.5f,0);
            sim.Orbs[0].Velocity=new Vector2(4.8f,0);sim.Orbs[1].Velocity=Vector2.zero;sim.Step(1f/120);
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(97));Assert.That(weapon.UnarmedMaxSpeed,Is.EqualTo(3.5f));
            Assert.That(sim.Orbs[0].Velocity.magnitude,Is.EqualTo(0).Within(0.001));Assert.That(weapon.Damage,Is.EqualTo(1));
        }
        [Test] public void ProjectileCanBeReflectedByBothShieldsWithoutRecursiveDamage()
        {
            var sim=Still(WeaponKind.Bow);var p=Shoot(sim);
            sim.Orbs[0]=new CombatantState(100,W(WeaponKind.Shield)){Position=new Vector2(-2,0),Angle=0};
            Run(sim,1);Assert.That(p.Reflections,Is.GreaterThanOrEqualTo(2));
            Assert.That(sim.Orbs[0].Health.Current,Is.EqualTo(100));Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(100));
            Assert.That(sim.Orbs[0].Weapon.ShieldWidth,Is.EqualTo(2.1f));Assert.That(sim.Orbs[1].Weapon.ShieldWidth,Is.EqualTo(2.1f));
        }
    }
}
