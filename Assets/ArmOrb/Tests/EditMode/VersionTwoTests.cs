using NUnit.Framework;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionTwoTests
    {
        static ArenaSimulation Fight(float size=8,bool gravity=true) => new ArenaSimulation(
            new WeaponState(WeaponKind.Sword),new WeaponState(WeaponKind.Spear,1,3,300,0.5f),99999,99999,731,size,gravity:gravity);

        [Test] public void PlaybackMultipliersProduceTheSameFullSimulationState()
        {
            var states=new ArenaSimulation[3];float[] speeds={0.5f,1,2};
            for(int i=0;i<speeds.Length;i++)
            {
                var clock=new SimulationClock(120);var sim=Fight();
                int frames=(int)(12/speeds[i]*60);
                for(int frame=0;frame<frames;frame++)
                {
                    int ticks=clock.Consume(1.0/60,speeds[i]);
                    for(int tick=0;tick<ticks;tick++)sim.Step((float)clock.StepSeconds);
                }
                states[i]=sim;
            }
            for(int i=1;i<3;i++)for(int side=0;side<2;side++)
            {
                Assert.That(states[i].Orbs[side].Position,Is.EqualTo(states[0].Orbs[side].Position));
                Assert.That(states[i].Orbs[side].Angle,Is.EqualTo(states[0].Orbs[side].Angle));
                Assert.That(states[i].Orbs[side].Health.Current,Is.EqualTo(states[0].Orbs[side].Health.Current));
                Assert.That(states[i].Orbs[side].Weapon.Hits,Is.EqualTo(states[0].Orbs[side].Weapon.Hits));
            }
        }
        [Test] public void SwitchingPlaybackSpeedDoesNotLoseFractionalTime()
        {
            var clock=new SimulationClock(120);
            Assert.That(clock.Consume(1.0/240,1),Is.Zero);
            Assert.That(clock.Interpolation,Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(clock.Consume(1.0/480,2),Is.EqualTo(1));
            Assert.That(clock.Interpolation,Is.EqualTo(0).Within(0.00001f));
        }
        [Test] public void GravityProducesArcsAndElasticBouncesDoNotCreateEnergy()
        {
            var orb=new CombatantState(100,new WeaponState(WeaponKind.Sword));
            orb.Position=new Vector2(0,1);orb.Velocity=new Vector2(1,0);
            const float g=9.8f,limit=3.45f;
            float energy=0.5f*orb.Velocity.sqrMagnitude+g*(orb.Position.y+limit);
            bool bounced=false;
            for(int i=0;i<2400;i++)
            {
                if(OrbMotion.Move(orb,1f/120,4,0.55f,g,1,8))bounced=true;
                float current=0.5f*orb.Velocity.sqrMagnitude+g*(orb.Position.y+limit);
                Assert.That(current,Is.EqualTo(energy).Within(0.03f));
            }
            Assert.That(bounced,Is.True);
            orb.Position=new Vector2(0,-limit);orb.Velocity=new Vector2(0,-0.01f);
            OrbMotion.Move(orb,1f/120,4,0.55f,g,1,8);
            Assert.That(orb.Velocity.y,Is.GreaterThan(7.8f));
        }
        [Test] public void AllArenaSizesStayBoundedWithAndWithoutGravity()
        {
            foreach(float size in new[]{6f,8,10})foreach(bool gravity in new[]{false,true})
            {
                var sim=Fight(size,gravity);
                for(int i=0;i<120*12;i++)
                {
                    sim.Step(1f/120);
                    foreach(var orb in sim.Orbs)
                    {
                        if(float.IsNaN(orb.Position.x)||float.IsNaN(orb.Position.y)||
                            Mathf.Abs(orb.Position.x)>size/2-sim.Radius+0.0001f||Mathf.Abs(orb.Position.y)>size/2-sim.Radius+0.0001f)
                            Assert.Fail($"Out of bounds: {size}, gravity={gravity}, {orb.Position}");
                    }
                }
            }
        }
        [Test] public void WeaponCollidersMatchOpaqueTexelsAfterRotation()
        {
            for(int kind=0;kind<5;kind++)
            {
                var state=new WeaponState((WeaponKind)kind,reach:kind==1?3:2.6f);
                var parts=new WeaponPartPose[2];int count=WeaponGeometry.GetParts(state,parts);
                var collider=new WeaponColliderBuffer();
                Vector2 position=new Vector2(1.1f,-0.7f);const float angle=37,radius=0.55f;
                WeaponGeometry.Fill(state,radius,position,angle,collider);
                float rad=angle*Mathf.Deg2Rad;Vector2 axis=new Vector2(Mathf.Cos(rad),Mathf.Sin(rad)),normal=new Vector2(-axis.y,axis.x);
                for(int p=0;p<count;p++)
                {
                    var part=parts[p];var texture=part.Mask.sprite.texture;var pixels=texture.GetPixels32();
                    for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                    {
                        Vector2 uv=new Vector2((x+0.5f)/texture.width,(y+0.5f)/texture.height-0.5f);
                        Vector2 local=(part.Offset+Vector2.Scale(part.Size,uv))*radius;
                        Vector2 point=position+axis*local.x+normal*local.y;
                        bool expected=pixels[y*texture.width+x].a>=128;
                        bool actual=WeaponGeometry.TouchesBody(collider,point,0);
                        if(actual!=expected)Assert.Fail($"{(WeaponKind)kind} part={p}, texel=({x},{y}) expected={expected}");
                    }
                }
            }
        }
        [Test] public void SpearGrowthMovesHeadAndExtendsShaftWithoutWidening()
        {
            var weapon=new WeaponState(WeaponKind.Spear,1,3,300,0.5f);
            var buffer=new WeaponColliderBuffer();WeaponGeometry.Fill(weapon,0.55f,Vector2.zero,0,buffer);
            Rect before=buffer.Bounds;weapon.OnHit(new HealthState(100));
            WeaponGeometry.Fill(weapon,0.55f,Vector2.zero,0,buffer);
            Assert.That(buffer.Bounds.xMax-before.xMax,Is.EqualTo(0.275f).Within(0.00001f));
            Assert.That(buffer.Bounds.height,Is.EqualTo(before.height).Within(0.00001f));
        }
        [Test] public void ExistingWeaponContactDoesNotGloballyCancelAnIndependentBodyHit()
        {
            var sim=new ArenaSimulation(new WeaponState(WeaponKind.Sword,1,2.6f,0),new WeaponState(WeaponKind.Sword,1,2.6f,0),speed:0);
            sim.Orbs[0].Position=new Vector2(-1.2f,0);sim.Orbs[0].Angle=0;
            sim.Orbs[1].Position=new Vector2(1.2f,0);sim.Orbs[1].Angle=180;
            sim.Step(1f/120);
            Assert.That(sim.Orbs[1].Health.Current,Is.EqualTo(100));
            sim.Orbs[1].Position=new Vector2(0.7f,0.22f);sim.Step(1f/120);
            Assert.That(sim.Orbs[1].Health.Current,Is.LessThan(100));
        }
    }
}
