using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmOrb
{
    public enum BuildDamageKind { Direct, Periodic, Extra, Reflected }

    public sealed partial class ArenaSimulation
    {
        public bool BuildRules { get; private set; }
        public BattleVariant BuildVariant { get; private set; }
        public BuildFighter[] Builds { get; private set; }
        sealed class Echo
        { public float Time, Radius, Speed, Damage, Original; public int Owner; public Vector2 Position; public BuildAttackSnapshot Attack; }
        readonly List<Echo> echoes = new List<Echo>();

        public void EnableBuildRules(BattleVariant variant, BattleAbility[] abilities)
        {
            if (BuildRules || TickCount != 0 || Danger || abilities == null || abilities.Length != Orbs.Length)
                throw new ArgumentException("Build rules require a fresh battle without the danger zone.");
            BuildRules = true; BuildVariant = variant; Builds = new BuildFighter[Orbs.Length];
            for (int i = 0; i < Orbs.Length; i++)
            {
                Orbs[i].Health.EnablePrecision();
                var b = Builds[i] = new BuildFighter { InitialHP = Orbs[i].Health.Maximum, Ability = abilities[i], Random = (uint)Seed ^ (0x85ebca6bu * (uint)(i+1)) };
                b.Spark.Random = (uint)Seed ^ (0xc2b2ae35u * (uint)(i+1));
            }
        }
        public bool CanBuildSkill(int actor, BuildSkill skill) => BuildRules && actor >= 0 && actor < Orbs.Length &&
            (int)skill >= 0 && (int)skill < BuildSkills.Count && !Orbs[actor].Health.IsDead &&
            BuildSkills.Applicable(skill, Orbs[actor].Weapon.Kind) && Builds[actor].Level(skill) < BuildSkills.MaxLevel(skill);
        public bool ApplyBuildSkill(int actor, BuildSkill skill)
        {
            if (!CanBuildSkill(actor, skill)) return false;
            var orb = Orbs[actor]; var b = Builds[actor]; int old = b.Level(skill), level = ++b.Levels[(int)skill];
            if (skill == BuildSkill.Vitality) orb.Health.IncreaseMaximum(BuildSkills.Value(level,150,250,350)-BuildSkills.Value(old,150,250,350));
            if (skill == BuildSkill.Barrier) b.PassiveBarrier += BuildSkills.Value(level,80,120,160)-BuildSkills.Value(old,80,120,160);
            if (skill == BuildSkill.Growth) orb.Weapon.GrowthMultiplier = 1 + BuildSkills.Value(level,.5f,.75f,1);
            if (skill == BuildSkill.SpearLunge) b.LungeCycle = -1;
            if (skill == BuildSkill.Rhythm) b.DirectHits = 0;
            if (skill == BuildSkill.SwordHeavy) b.SwordHits = 0;
            Emit(CombatEventKind.Upgrade,orb.Position,actor);
            return true;
        }
        public bool UseBuildAbility(int actor)
        {
            if (!BuildRules || BuildVariant != BattleVariant.Ability || actor < 0 || actor >= Orbs.Length || Orbs[actor].Health.IsDead) return false;
            var b = Builds[actor]; if (b.AbilityCooldown > .00001f) return false;
            if (b.Ability == BattleAbility.Heal)
            {
                if (Orbs[actor].Health.Current >= Orbs[actor].Health.Maximum) return false;
                HealBuild(actor,100,true);
            }
            else { b.ActiveBarrier = 120; b.ActiveBarrierTime = 5; Emit(CombatEventKind.Barrier,Orbs[actor].Position,actor,120); }
            b.AbilityCooldown = 20; return true;
        }
        public bool CollectBuildSpark(int actor, int id)
        {
            if (!BuildRules || BuildVariant != BattleVariant.Sparks || actor < 0 || actor >= Orbs.Length || Orbs[actor].Health.IsDead) return false;
            var b = Builds[actor]; var spark = b.Spark;
            if (!spark.Active || spark.Id != id || Elapsed - spark.SpawnTime >= 4) return false;
            spark.Active = false;
            if (spark.Kind == 0) HealBuild(actor,30,true); else { b.SparkBoost = 6; Emit(CombatEventKind.Upgrade,Orbs[actor].Position,actor); }
            return true;
        }
        public bool BuildThreat(int actor)
        {
            var orb = Orbs[actor];
            for (int i=0;i<Orbs.Length;i++) if(i!=actor && !Orbs[i].Health.IsDead && (Orbs[i].Position-orb.Position).sqrMagnitude <= 9*Radius*Radius) return true;
            foreach(var p in projectiles) if(p.Active && p.Owner!=actor)
            {
                Vector2 relative=p.Position-orb.Position, velocity=p.Velocity-orb.Velocity;
                float t=Mathf.Clamp(-Vector2.Dot(relative,velocity)/Mathf.Max(.0001f,velocity.sqrMagnitude),0,.5f);
                if((relative+velocity*t).sqrMagnitude <= (Radius+p.Radius)*(Radius+p.Radius)) return true;
            }
            return false;
        }
        public float HealBuild(int actor, float amount, bool feedback = false)
        {
            var orb = Orbs[actor]; if (orb.Health.IsDead) return 0;
            float before = orb.Health.Current; orb.Health.Heal(amount * Builds[actor].HealingFactor);
            float restored = orb.Health.Current - before;
            if (restored > 0) { Builds[actor].HealFlash = .18f; if (feedback) Emit(CombatEventKind.Healing,orb.Position,actor,restored); }
            return restored;
        }
        float OutgoingBuild(int actor)
        {
            var orb=Orbs[actor];var b=Builds[actor];
            return 1+BuildSkills.Value(b.Level(BuildSkill.Power),.4f,.7f,1)+
                (b.Has(BuildSkill.Rage)?.6f*(1-orb.Health.Current/orb.Health.Maximum):0)+
                (b.Has(BuildSkill.Overload)?.7f:0)+.2f*b.HunterKills+(b.SparkBoost>0?.4f:0);
        }
        public float CaptureBuildDamage(int actor,float damage) => BuildRules ? damage*OutgoingBuild(actor) : damage;
        public BuildAttackSnapshot CaptureBuildAttack(int actor)
        {
            var b=Builds[actor];uint skills=0;for(int i=0;i<BuildSkills.Count;i++)if(b.Levels[i]>0)skills|=1u<<i;
            return new BuildAttackSnapshot{Captured=true,Skills=skills,CriticalChance=BuildSkills.Value(b.Level(BuildSkill.Critical),.25f,.35f,.45f),Vampirism=BuildSkills.Value(b.Level(BuildSkill.Vampirism),.15f,.2f,.25f)};
        }
        static bool AttackHas(BuildFighter fighter,BuildAttackSnapshot snapshot,BuildSkill skill) => snapshot.Captured?snapshot.Has(skill):fighter.Has(skill);
        float StrikeBuild(int actor,int target,float damage,bool prepared,float original,BuildAttackSnapshot snapshot=default)
        {
            var orb=Orbs[actor];var b=Builds[actor];
            float result=prepared?damage:damage*OutgoingBuild(actor);
            float raw=prepared?original:damage;
            if(AttackHas(b,snapshot,BuildSkill.Execution)&&Orbs[target].Health.Current<.3f*Orbs[target].Health.Maximum)result+=raw*.6f;
            if(AttackHas(b,snapshot,BuildSkill.Critical)&&MatchRandom.Unit(ref b.Random)<(snapshot.Captured?snapshot.CriticalChance:BuildSkills.Value(b.Level(BuildSkill.Critical),.25f,.35f,.45f)))
            {result*=2;b.CriticalFlash=.16f;Emit(CombatEventKind.Critical,Orbs[target].Position,target,0,actor);}
            if(AttackHas(b,snapshot,BuildSkill.Rhythm)&&(b.DirectHits+1)%4==0)result*=2;
            if(AttackHas(b,snapshot,BuildSkill.SwordHeavy)&&(b.SwordHits+1)%4==0)result*=3;
            if(AttackHas(b,snapshot,BuildSkill.SpearLunge)&&orb.Weapon.BuildReachMultiplier>1&&!b.LungeUsed)result*=2;
            if(AttackHas(b,snapshot,BuildSkill.BladeMomentum))result*=1+.1f*b.MomentumStacks;
            if(AttackHas(b,snapshot,BuildSkill.UnarmedRam))result*=1.5f;
            return result;
        }
        // Only this entry point applies the new combat effects. Legacy bets keep their original path.
        public float DealBuildDamage(int actor,int target,float damage,BuildDamageKind kind=BuildDamageKind.Direct,bool prepared=false,float original=0,BuildAttackSnapshot snapshot=default)
        {
            if(!BuildRules||actor<0||actor>=Orbs.Length||target<0||target>=Orbs.Length||actor==target||Orbs[target].Health.IsDead)return 0;
            var source=Builds[actor];var victim=Builds[target];var health=Orbs[target].Health;
            if(victim.Invulnerable>0||damage<=0)return 0;
            bool direct=kind==BuildDamageKind.Direct;
            float amount=direct?StrikeBuild(actor,target,damage,prepared,original,snapshot):damage;
            float reduction=BuildSkills.Value(victim.Level(BuildSkill.Armor),.25f,.35f,.45f);
            float protection=1-reduction;
            int layers=0;foreach(float time in victim.Adaptation)if(time>0)layers++;
            protection*=1-layers*.05f;if(victim.RamTime>0)protection*=.6f;
            reduction=Mathf.Min(.6f,1-protection);
            if(direct&&AttackHas(source,snapshot,BuildSkill.Pierce))reduction*=.5f;
            amount*=1-reduction;if(victim.Has(BuildSkill.Overload))amount*=1.25f;
            float applied=amount;
            Absorb(ref victim.ActiveBarrier,ref amount);Absorb(ref victim.StorageBarrier,ref amount);
            float oldPassive=victim.PassiveBarrier;Absorb(ref victim.PassiveBarrier,ref amount);
            if(oldPassive>0&&victim.PassiveBarrier<=0)victim.BarrierReload=15;
            float before=health.Current;health.Damage(amount);float lost=before-health.Current;
            if(applied>0)
            {
                lastDamage=Elapsed;MarkContact(actor,target);
                if(direct)
                {
                    if(victim.Has(BuildSkill.Adaptation))AddLayer(victim.Adaptation,3);
                    source.DirectHits++;if(Orbs[actor].Weapon.Kind==WeaponKind.Sword)source.SwordHits++;
                    if(source.Has(BuildSkill.SpearLunge)&&Orbs[actor].Weapon.BuildReachMultiplier>1)source.LungeUsed=true;
                    source.MomentumStacks=0;source.MomentumTravel=0;
                    var weapon=Orbs[actor].Weapon;float poison=weapon.Kind==WeaponKind.Scythe?weapon.Growth*weapon.GrowthMultiplier:0;
                    weapon.OnHit(health);if(poison>0)victim.ScythePoison[actor]+=poison;
                    if(weapon.Kind==WeaponKind.Unarmed&&Speed>0)
                    {
                        float current=Orbs[actor].Velocity.magnitude,wanted=Mathf.Min(current+weapon.Growth*weapon.GrowthMultiplier*weapon.SpeedUnit,weapon.UnarmedWorldLimit);
                        if(wanted>current)Orbs[actor].Velocity=OrbMotion.Direction(Orbs[actor].Velocity,Vector2.up)*wanted;
                    }
                    if(lost>0&&AttackHas(source,snapshot,BuildSkill.Vampirism))HealBuild(actor,lost*(snapshot.Captured?snapshot.Vampirism:BuildSkills.Value(source.Level(BuildSkill.Vampirism),.15f,.2f,.25f)));
                    if(!health.IsDead)
                    {
                        if(AttackHas(source,snapshot,BuildSkill.Burn)){victim.BurnTime=4;victim.BurnOwner=actor;Emit(CombatEventKind.Burning,Orbs[target].Position,target);}
                        if(AttackHas(source,snapshot,BuildSkill.Bleed)){int index=Shortest(victim.Bleeds);victim.Bleeds[index]=5;victim.BleedOwners[index]=actor;}
                        if(AttackHas(source,snapshot,BuildSkill.Suppression))victim.SuppressionTime=5;
                        if(AttackHas(source,snapshot,BuildSkill.ScytheHarvest))
                        {float total=0;foreach(float value in victim.ScythePoison)total+=value;DealBuildDamage(actor,target,total*3,BuildDamageKind.Extra);}
                    }
                }
                else if(kind==BuildDamageKind.Reflected)
                {
                    if(lost>0)Orbs[actor].Weapon.OnHit(health);
                    if(source.Has(BuildSkill.ShieldStorage)&&!Orbs[actor].Health.IsDead)
                        source.StorageBarrier=Mathf.Min(150,source.StorageBarrier+lost*.3f);
                }
            }
            // A lethal hit can revive once, before kills, winner detection and subsequent contacts.
            if(health.IsDead&&victim.Has(BuildSkill.Resurrection)&&!victim.ResurrectionUsed)
            {victim.ResurrectionUsed=true;health.Revive(.35f);victim.Invulnerable=1;Emit(CombatEventKind.Revive,Orbs[target].Position,target,health.Current);}
            else if(!health.IsDead&&health.Current<=health.Maximum*.25f&&victim.Has(BuildSkill.SecondWind)&&!victim.SecondWindUsed)
            {victim.SecondWindUsed=true;HealBuild(target,120,true);}
            if(health.IsDead&&!victim.DeathCredited)
            {
                victim.DeathCredited=true;
                if(source.Has(BuildSkill.Hunter)&&!Orbs[actor].Health.IsDead)
                {if(Orbs[actor].Weapon.Kind!=WeaponKind.Shield)source.HunterKills++;HealBuild(actor,100,true);}
            }
            if(kind==BuildDamageKind.Periodic)
            {victim.DotFeedback+=lost;if(victim.DotFeedback>=1){Emit(CombatEventKind.Poison,Orbs[target].Position,target,victim.DotFeedback,actor);victim.DotFeedback=0;}}
            else Emit(kind==BuildDamageKind.Reflected?CombatEventKind.Block:CombatEventKind.Hit,Orbs[target].Position,target,lost,actor);
            return lost;
        }
        static void Absorb(ref float barrier,ref float amount){float used=Mathf.Min(barrier,amount);barrier-=used;amount-=used;}
        static int Shortest(float[] layers){int best=0;for(int i=1;i<layers.Length;i++)if(layers[i]<layers[best])best=i;return best;}
        static void AddLayer(float[] layers,float time){layers[Shortest(layers)]=time;}
        void ReturnBuildMelee(int defender,int attacker)
        {
            float amount=StrikeBuild(attacker,defender,Orbs[attacker].Weapon.Damage,false,0);
            Orbs[defender].Weapon.Block(amount);DealBuildDamage(defender,attacker,amount,BuildDamageKind.Reflected);
        }
        void TickBuild(float dt)
        {
            if(!BuildRules)return;
            for(int i=0;i<Orbs.Length;i++)
            {
                var orb=Orbs[i];var b=Builds[i];
                b.AbilityCooldown=Mathf.Max(0,b.AbilityCooldown-dt);b.ActiveBarrierTime=Mathf.Max(0,b.ActiveBarrierTime-dt);
                if(b.ActiveBarrierTime<=0)b.ActiveBarrier=0;
                b.SuppressionTime=Mathf.Max(0,b.SuppressionTime-dt);b.RamTime=Mathf.Max(0,b.RamTime-dt);
                b.Invulnerable=Mathf.Max(0,b.Invulnerable-dt);b.SparkBoost=Mathf.Max(0,b.SparkBoost-dt);
                b.HealFlash=Mathf.Max(0,b.HealFlash-dt);b.CriticalFlash=Mathf.Max(0,b.CriticalFlash-dt);
                for(int j=0;j<b.Adaptation.Length;j++)b.Adaptation[j]=Mathf.Max(0,b.Adaptation[j]-dt);
                if(orb.Health.IsDead){b.Spark.Active=false;continue;}
                if(b.Has(BuildSkill.Barrier)&&b.PassiveBarrier<=0)
                {b.BarrierReload=Mathf.Max(0,b.BarrierReload-dt);if(b.BarrierReload<=.00001f){b.PassiveBarrier=BuildSkills.Value(b.Level(BuildSkill.Barrier),80,120,160);Emit(CombatEventKind.Barrier,orb.Position,i,b.PassiveBarrier);}}
                if(b.Has(BuildSkill.Regeneration))HealBuild(i,BuildSkills.Value(b.Level(BuildSkill.Regeneration),4,6,8)*dt);
                for(int actor=0;actor<Orbs.Length&&!orb.Health.IsDead;actor++)if(actor!=i&&b.ScythePoison[actor]>0)DealBuildDamage(actor,i,b.ScythePoison[actor]*dt,BuildDamageKind.Periodic);
                if(b.BurnTime>0){float time=Mathf.Min(dt,b.BurnTime);b.BurnTime=Mathf.Max(0,b.BurnTime-dt);DealBuildDamage(b.BurnOwner,i,5*time,BuildDamageKind.Periodic);}
                for(int j=0;j<b.Bleeds.Length;j++)if(b.Bleeds[j]>0){float time=Mathf.Min(dt,b.Bleeds[j]);b.Bleeds[j]=Mathf.Max(0,b.Bleeds[j]-dt);DealBuildDamage(b.BleedOwners[j],i,2*time,BuildDamageKind.Periodic);}
                if(b.Has(BuildSkill.SpearLunge))
                {
                    int cycle=Mathf.FloorToInt(Elapsed/4);if(cycle!=b.LungeCycle){b.LungeCycle=cycle;b.LungeUsed=false;}
                    orb.Weapon.BuildReachMultiplier=Elapsed-cycle*4<1?1.5f:1;
                }
                var spark=b.Spark;
                if(BuildVariant==BattleVariant.Sparks)
                {
                    if(spark.Active&&Elapsed-spark.SpawnTime>=4)spark.Active=false;
                    if(!spark.Active&&Elapsed>=spark.NextTime&&!b.OfferOpen)
                    {
                        spark.Active=true;spark.Id++;spark.Kind=MatchRandom.Range(ref spark.Random,2);spark.SpawnTime=Elapsed;spark.NextTime=Elapsed+14;
                        float edge=Size*.5f-Mathf.Max(Radius*2,Size*.08f);
                        spark.Position=new Vector2((MatchRandom.Unit(ref spark.Random)*2-1)*edge,(MatchRandom.Unit(ref spark.Random)*2-1)*edge);
                    }
                }
            }
            for(int n=echoes.Count-1;n>=0;n--)
            {
                var e=echoes[n];e.Time-=dt;if(e.Time>0)continue;
                AddBuildWave(e.Owner,e.Position,e.Radius,e.Speed,e.Damage,e.Original,true,e.Attack);echoes.RemoveAt(n);
            }
        }
        void BuildTravel(int actor,float angle)
        {
            if(!BuildRules)return;var b=Builds[actor];if(!b.Has(BuildSkill.BladeMomentum))return;
            b.MomentumTravel+=Mathf.Abs(angle);
            while(b.MomentumTravel>=360){b.MomentumTravel-=360;b.MomentumStacks=Mathf.Min(10,b.MomentumStacks+1);}
        }
        void BuildBodyContact(int a,int b)
        {
            if(!BuildRules)return;
            if(Builds[a].Has(BuildSkill.UnarmedRam))Builds[a].RamTime=1;
            if(Builds[b].Has(BuildSkill.UnarmedRam))Builds[b].RamTime=1;
        }
        void AddBuildWave(int owner,Vector2 position,float radius,float speed,float damage,float original,bool extra,BuildAttackSnapshot attack=default)
        {
            var w=freeWaves.Count>0?freeWaves.Pop():new WaveState();
            w.Id=++nextEffectId;w.Owner=owner;w.Position=position;w.Radius=w.PreviousRadius=0;w.MaximumRadius=radius;w.Speed=speed;
            w.Damage=damage;w.OriginalDamage=original;w.BuildAttack=attack;w.Extra=extra;w.Reflected=false;w.Hit=false;w.HitMask=0;w.Active=true;waves.Add(w);
            Emit(CombatEventKind.Explosion,position,owner);
        }
        public void ResolveBuildTimeLimit()
        {
            if(!BuildRules||Result!=MatchResult.InProgress)return;
            int winner=-1;float best=-1;bool tie=false;
            for(int i=0;i<Orbs.Length;i++)if(!Orbs[i].Health.IsDead)
            {
                float fraction=Orbs[i].Health.Current/Orbs[i].Health.Maximum;
                if(fraction>best+.000001f){best=fraction;winner=i;tie=false;}
                else if(Mathf.Abs(fraction-best)<.000001f)tie=true;
            }
            WinnerIndex=tie?-1:winner;Result=WinnerIndex<0?MatchResult.Draw:WinnerIndex==0?MatchResult.Player:WinnerIndex==1?MatchResult.Enemy:MatchResult.Other;
            Emit(CombatEventKind.Finished,Vector2.zero,WinnerIndex);
        }
    }
}
