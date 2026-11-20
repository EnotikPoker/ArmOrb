using System;
using UnityEngine;

namespace ArmOrb
{
    // Pure numerical simulation. All participants retain their original indices after elimination.
    public sealed partial class ArenaSimulation
    {
        public CombatantState[] Orbs { get; }
        double elapsed, poisonClock;
        public float Elapsed => (float)elapsed;
        public int TickCount { get; private set; }
        public MatchResult Result { get; private set; }
        public int WinnerIndex { get; private set; } = -1;
        public readonly float Size, Radius, Speed, Gravity, Restitution, MinimumBounce;
        public readonly bool UsesGravity, ActiveMotion, Danger;
        public readonly int Seed;
        public float SafeRadius => Size * 0.707107f * Mathf.Clamp01((120-Elapsed)/30);
        public float DangerDamage => Elapsed < 90 ? 0 : Mathf.Lerp(5,20,Mathf.Clamp01((Elapsed-90)/30));
        public event Action<CombatEvent> Event;
        readonly WeaponColliderBuffer[] geometry;
        readonly WeaponColliderBuffer sampleFirst=new WeaponColliderBuffer(),sampleSecond=new WeaponColliderBuffer();
        readonly Vector2[] start,end,incoming;
        readonly float[] startAngle,deltaAngle,impactDamage,lastContact;
        readonly bool[] alive;
        readonly PairContact[,] pairs;
        float lastDamage;
        sealed class PairContact
        {
            public bool Orb,Weapon,BodyA,BodyB,UnarmedA,UnarmedB;
            public float Cooldown;
        }
        public ArenaSimulation(WeaponState player,WeaponState enemy,float playerHP=100,float enemyHP=100,
            int seed=1,float size=10,float radius=0.55f,float speed=6.4f,bool gravity=false,
            float gravityAcceleration=16.7f,float restitution=1,float minimumBounce=0)
            : this(new[]{player,enemy},new[]{playerHP,enemyHP},seed,size,radius,speed,gravity,gravityAcceleration,restitution,false,false,minimumBounce) { }

        public ArenaSimulation(WeaponState[] weapons,float[] health,int seed=1,float size=8,float radius=0.55f,
            float speed=6.4f,bool gravity=true,float gravityAcceleration=16.7f,float restitution=1,
            bool activeMotion=true,bool danger=true,float minimumBounce=0)
        {
            if(weapons==null || weapons.Length<2 || weapons.Length>5 || health==null || health.Length!=weapons.Length)
                throw new ArgumentException("A fight requires 2–5 weapons and matching health values.");
            Size=Mathf.Max(size,radius*8); Radius=radius; Speed=speed; Seed=seed;
            UsesGravity=gravity; Gravity=gravity?Mathf.Max(0,gravityAcceleration):0; Restitution=restitution;
            MinimumBounce=gravity?Mathf.Max(0,minimumBounce):0;ActiveMotion=activeMotion;Danger=danger;
            int count=weapons.Length;
            Orbs=new CombatantState[count];geometry=new WeaponColliderBuffer[count];pairs=new PairContact[count,count];
            start=new Vector2[count];end=new Vector2[count];incoming=new Vector2[count];
            startAngle=new float[count];deltaAngle=new float[count];impactDamage=new float[count];lastContact=new float[count];alive=new bool[count];
            var random=new System.Random(seed);float phase=(float)random.NextDouble()*Mathf.PI*2;
            for(int i=0;i<count;i++)
            {
                Orbs[i]=new CombatantState(health[i],weapons[i]);geometry[i]=new WeaponColliderBuffer();
                for(int j=0;j<i;j++)pairs[j,i]=new PairContact();
                float angle=phase+i*Mathf.PI*2/count;
                var orb=Orbs[i];orb.Position=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Size*0.27f;
                float jitter=((float)random.NextDouble()-0.5f)*0.7f;
                Vector2 direction=-orb.Position.normalized;
                direction=Rotate(direction,jitter);
                orb.Velocity=direction*MoveSpeed(i);
                if(gravity) orb.Velocity=new Vector2(direction.x*MoveSpeed(i),2+(float)random.NextDouble()*3).normalized*MoveSpeed(i);
                orb.Angle=(float)random.NextDouble()*360;orb.RotationDirection=random.Next(2)==0?1:-1;
                orb.Weapon.MeasureSpeed(orb.Velocity);
            }
        }
        static Vector2 Rotate(Vector2 value,float radians) => new Vector2(value.x*Mathf.Cos(radians)-value.y*Mathf.Sin(radians),value.x*Mathf.Sin(radians)+value.y*Mathf.Cos(radians));
        public void Step(float dt)
        {
            if(Result!=MatchResult.InProgress || dt<=0) return;
            foreach(var orb in Orbs) orb.AngularTravel=0;
            foreach(var p in projectiles) p.PreviousPosition=p.Position;
            foreach(var w in waves) w.PreviousRadius=w.Radius;
            float largest=0,next=0;
            for(int i=0;i<Orbs.Length;i++) if(!Orbs[i].Health.IsDead)
            {
                var weapon=Orbs[i].Weapon;
                float extent=weapon.Kind==WeaponKind.Shield?Mathf.Max(weapon.Reach+2,weapon.ShieldWidth*0.5f+2):weapon.Reach+2;
                float travel=Orbs[i].Velocity.magnitude+Gravity*dt+(weapon.Kind!=WeaponKind.Unarmed?weapon.RotationSpeed*Mathf.Deg2Rad*Radius*extent:0);
                if(travel>largest){next=largest;largest=travel;}else next=Mathf.Max(next,travel);
            }
            int steps=Mathf.Max(1,Mathf.CeilToInt((largest+next)*dt/(Radius*0.08f)));
            float h=dt/steps;
            for(int s=0;s<steps && Result==MatchResult.InProgress;s++) Substep(h);
            TickCount++;
        }
        void Substep(float dt)
        {
            elapsed+=dt;
            TickUpgrades(dt);
            for(int i=0;i<Orbs.Length;i++)
            {
                var orb=Orbs[i];alive[i]=!orb.Health.IsDead;
                start[i]=end[i]=orb.Position;startAngle[i]=orb.Angle;deltaAngle[i]=0;
                if(!alive[i]){geometry[i].Count=0;continue;}
                Guide(i,dt);incoming[i]=orb.Velocity;
                deltaAngle[i]=orb.RotationDirection*orb.Weapon.RotationSpeed*dt;
                if(OrbMotion.Move(orb,dt,Size*0.5f,Radius,Gravity,Restitution,MinimumBounce)) Emit(CombatEventKind.Wall,orb.Position,i);
                orb.Weapon.MeasureSpeed(orb.Velocity);impactDamage[i]=orb.Weapon.Damage;
                orb.Angle=Mathf.Repeat(orb.Angle+deltaAngle[i],360);orb.AngularTravel+=deltaAngle[i];
            }
            // All living pairs participate in the same substep, including simultaneous lethal hits.
            for(int a=0;a<Orbs.Length;a++)for(int b=a+1;b<Orbs.Length;b++) if(alive[a]&&alive[b])
            {
                var pair=pairs[a,b];pair.Cooldown=Mathf.Max(0,pair.Cooldown-dt);
                bool touches=OrbMotion.Resolve(Orbs[a],Orbs[b],Radius,MoveSpeed(a),UsesGravity,Restitution,MoveSpeed(b));
                bool newContact=touches&&!pair.Orb;
                if(newContact){Emit(CombatEventKind.Body,(Orbs[a].Position+Orbs[b].Position)*0.5f,a,0,b);MarkContact(a,b);}
                if(touches)pair.Orb=true;
                else if((Orbs[a].Position-Orbs[b].Position).sqrMagnitude>(2*Radius+0.03f)*(2*Radius+0.03f))pair.Orb=false;
                // Save the new body-contact pulse separately from the persistent latch.
                bodyPulses[a,b]=newContact;
            }
            for(int i=0;i<Orbs.Length;i++)if(alive[i])
            {
                OrbMotion.Move(Orbs[i],0,Size*0.5f,Radius,Gravity,Restitution,MinimumBounce);end[i]=Orbs[i].Position;
                WeaponGeometry.Fill(Orbs[i],Radius,geometry[i]);
            }
            for(int a=0;a<Orbs.Length;a++)for(int b=a+1;b<Orbs.Length;b++)if(alive[a]&&alive[b]) ResolveWeapons(a,b,bodyPulses[a,b]);
            TickProjectiles(dt);
            poisonClock+=dt;
            if(poisonClock>=1)
            {
                poisonClock-=1;
                for(int i=0;i<Orbs.Length;i++) if(alive[i]&&Orbs[i].Health.PoisonPerSecond>0)
                { Orbs[i].Health.Damage(Orbs[i].Health.PoisonPerSecond);lastDamage=Elapsed;Emit(CombatEventKind.Poison,Orbs[i].Position,i,Orbs[i].Health.PoisonPerSecond); }
            }
            if(Danger&&Elapsed>=90)for(int i=0;i<Orbs.Length;i++)if(alive[i]&&(Elapsed>=120||Orbs[i].Position.sqrMagnitude>SafeRadius*SafeRadius))
                Orbs[i].Health.DangerDamage(DangerDamage*dt);
            int remaining=0,winner=-1;
            for(int i=0;i<Orbs.Length;i++) { Orbs[i].Weapon.MeasureSpeed(Orbs[i].Velocity);if(!Orbs[i].Health.IsDead){remaining++;winner=i;} }
            if(remaining<=1)
            {
                WinnerIndex=remaining==1?winner:-1;
                Result=remaining==0?MatchResult.Draw:winner==0?MatchResult.Player:winner==1?MatchResult.Enemy:MatchResult.Other;
                Emit(CombatEventKind.Finished,Vector2.zero,winner);
            }
        }
        readonly bool[,] bodyPulses=new bool[5,5];
        void Guide(int side,float dt)
        {
            if(!ActiveMotion||Speed<=0)return;
            float quiet=Mathf.Max(Elapsed-lastContact[side]-4,Elapsed-lastDamage-8);
            if(quiet<=0)return;
            int target=-1;float nearest=float.PositiveInfinity;
            for(int j=0;j<Orbs.Length;j++)if(j!=side&&!Orbs[j].Health.IsDead)
            {
                float distance=(Orbs[j].Position-Orbs[side].Position).sqrMagnitude;
                if(distance<nearest){nearest=distance;target=j;}
            }
            if(target<0)return;
            var orb=Orbs[side];Vector2 towards=Orbs[target].Position+Orbs[target].Velocity*0.3f-orb.Position;
            float blend=Mathf.Clamp01(quiet);
            if(UsesGravity)
            {
                float desired=Mathf.Clamp(towards.x*2,-MoveSpeed(side),MoveSpeed(side));
                orb.Velocity.x=Mathf.MoveTowards(orb.Velocity.x,desired,3.2f*blend*dt);
            }
            else
            {
                float current=Mathf.Atan2(orb.Velocity.y,orb.Velocity.x)*Mathf.Rad2Deg;
                float desired=Mathf.Atan2(towards.y,towards.x)*Mathf.Rad2Deg;
                float angle=Mathf.MoveTowardsAngle(current,desired,90*blend*dt)*Mathf.Deg2Rad;
                orb.Velocity=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*MoveSpeed(side);
            }
        }
        void MarkContact(int a,int b){lastContact[a]=lastContact[b]=Elapsed;}
        void ResolveWeapons(int a,int b,bool newBodyContact)
        {
            var pair=pairs[a,b];var wa=Orbs[a].Weapon;var wb=Orbs[b].Weapon;
            bool clash=WeaponGeometry.Clash(geometry[a],geometry[b]);
            bool ca=WeaponGeometry.TouchesBody(geometry[a],Orbs[b].Position,Radius),cb=WeaponGeometry.TouchesBody(geometry[b],Orbs[a].Position,Radius);
            bool blockA=false,blockB=false;
            if(clash&&!pair.Weapon&&pair.Cooldown<=0)
            {
                float impact=Fraction(a,b,2);
                blockA=ca&&impact<=Fraction(a,b,0)+0.001f;blockB=cb&&impact<=Fraction(a,b,1)+0.001f;
                bool sa=wa.Kind==WeaponKind.Shield,sb=wb.Kind==WeaponKind.Shield;
                if(sa!=sb && (sa?wb:wa).IsMelee)ReturnMelee(sa?a:b,sa?b:a);
                else Emit(CombatEventKind.Clash,(Orbs[a].Position+Orbs[b].Position)*0.5f,a,0,b);
                if(WeaponGeometry.Contact(geometry[a],geometry[b],out Vector2 point,out Vector2 normal))
                {DeflectRotation(a,point,normal,impact);DeflectRotation(b,point,-normal,impact);}
                pair.Cooldown=0.045f;MarkContact(a,b);
            }
            if(clash&&wb.Kind==WeaponKind.Shield)blockA=true;
            if(clash&&wa.Kind==WeaponKind.Shield)blockB=true;
            pair.Weapon=clash;
            if(ca&&!pair.BodyA&&!blockA)
            {
                if(wa.IsMelee&&wa.Kind!=WeaponKind.Shield)DamageHit(a,b,wa.Damage,Orbs[b].Position);
                if(WeaponGeometry.BodyContact(geometry[a],Orbs[b].Position,Radius,out Vector2 p,out Vector2 n))DeflectRotation(a,p,n,Fraction(a,b,0));
            }
            if(cb&&!pair.BodyB&&!blockB)
            {
                if(wb.IsMelee&&wb.Kind!=WeaponKind.Shield)DamageHit(b,a,wb.Damage,Orbs[a].Position);
                if(WeaponGeometry.BodyContact(geometry[b],Orbs[a].Position,Radius,out Vector2 p,out Vector2 n))DeflectRotation(b,p,n,Fraction(a,b,1));
            }
            pair.BodyA=ca;pair.BodyB=cb;
            bool ua=wa.Kind==WeaponKind.Unarmed&&wb.Kind==WeaponKind.Shield&&cb;
            bool ub=wb.Kind==WeaponKind.Unarmed&&wa.Kind==WeaponKind.Shield&&ca;
            if(ua&&!pair.UnarmedA)ReturnMelee(b,a);
            if(ub&&!pair.UnarmedB)ReturnMelee(a,b);
            if(newBodyContact)
            {
                if(wa.Kind==WeaponKind.Unarmed&&!ua)DamageHit(a,b,impactDamage[a],Orbs[b].Position);
                if(wb.Kind==WeaponKind.Unarmed&&!ub)DamageHit(b,a,impactDamage[b],Orbs[a].Position);
            }
            pair.UnarmedA=ua;pair.UnarmedB=ub;
        }
        float Fraction(int a,int b,int type)
        {
            if(Sample(a,b,type,0))return 0;
            float low=0,high=1;
            for(int i=0;i<6;i++){float mid=(low+high)*0.5f;if(Sample(a,b,type,mid))high=mid;else low=mid;}return high;
        }
        bool Sample(int a,int b,int type,float t)
        {
            Vector2 pa=Vector2.Lerp(start[a],end[a],t),pb=Vector2.Lerp(start[b],end[b],t);
            if(type!=1)WeaponGeometry.Fill(Orbs[a].Weapon,Radius,pa,startAngle[a]+deltaAngle[a]*t,sampleFirst);
            if(type!=0)WeaponGeometry.Fill(Orbs[b].Weapon,Radius,pb,startAngle[b]+deltaAngle[b]*t,sampleSecond);
            return type==0?WeaponGeometry.TouchesBody(sampleFirst,pb,Radius):type==1?WeaponGeometry.TouchesBody(sampleSecond,pa,Radius):WeaponGeometry.Clash(sampleFirst,sampleSecond);
        }
        void ReturnMelee(int defender,int attacker)
        {
            float returned=Orbs[defender].Weapon.Block(Orbs[attacker].Weapon.Damage);
            float before=Orbs[attacker].Health.Current;Orbs[attacker].Health.Damage(returned);
            if(Orbs[attacker].Health.Current<before){Orbs[defender].Weapon.OnHit(Orbs[attacker].Health);lastDamage=Elapsed;MarkContact(defender,attacker);}
            Emit(CombatEventKind.Block,Orbs[attacker].Position,attacker,returned,defender);
        }
        void DeflectRotation(int side,Vector2 point,Vector2 towardsContact,float fraction)
        {
            var orb=Orbs[side];if(Mathf.Abs(deltaAngle[side])<0.0000001f)return;
            Vector2 lever=point-orb.Position;
            Vector2 velocity=new Vector2(-lever.y,lever.x)*orb.RotationDirection*orb.Weapon.RotationSpeed*Mathf.Deg2Rad;
            if(Vector2.Dot(velocity,towardsContact)<=0.0001f)return;
            float correction=-2*orb.RotationDirection*Mathf.Abs(deltaAngle[side])*(1-fraction);
            orb.AngularTravel+=correction;orb.Angle=Mathf.Repeat(orb.Angle+correction,360);orb.RotationDirection*=-1;
        }
        float MoveSpeed(int side)=>Orbs[side].Weapon.Kind==WeaponKind.Unarmed?(Speed>0?Orbs[side].Weapon.UnarmedWorldLimit:0):Speed;
        void Emit(CombatEventKind kind,Vector2 position,int side=0,float amount=0,int actor=-1)=>Event?.Invoke(new CombatEvent(kind,position,side,amount,actor));
    }
}
