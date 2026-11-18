using System;
using UnityEngine;

namespace ArmOrb
{
    public sealed partial class ArenaSimulation
    {
        public CombatantState[] Orbs { get; }
        double elapsed, poisonClock;
        public float Elapsed => (float)elapsed;
        public MatchResult Result { get; private set; }
        public readonly float Size, Radius, Speed, Gravity, Restitution, MinimumBounce;
        public readonly bool UsesGravity;
        public readonly int Seed;
        public event Action<CombatEvent> Event;
        readonly WeaponColliderBuffer first=new WeaponColliderBuffer(),second=new WeaponColliderBuffer();
        readonly WeaponColliderBuffer sampleFirst=new WeaponColliderBuffer(),sampleSecond=new WeaponColliderBuffer();
        readonly bool[] bodyContact=new bool[2];
        readonly Vector2[] start=new Vector2[2],end=new Vector2[2];
        readonly float[] startAngle=new float[2],deltaAngle=new float[2];
        bool weaponContact,orbContact;
        float clashCooldown;
        readonly float minimumHorizontal;
        readonly bool[] unarmedShieldContact = new bool[2];

        public ArenaSimulation(WeaponState player,WeaponState enemy,float playerHP=100,float enemyHP=100,
            int seed=1,float size=10,float radius=0.55f,float speed=6.4f,bool gravity=false,
            float gravityAcceleration=20,float restitution=1,float minimumBounce=-1,float minimumHorizontal=0.4f)
        {
            Size=Mathf.Max(size,radius*8); Radius=radius; Speed=speed; Seed=seed;
            UsesGravity=gravity; Gravity=gravity?gravityAcceleration:0; Restitution=restitution; MinimumBounce=gravity?(minimumBounce < 0 ? Mathf.Sqrt(2*Gravity*0.85f*(Size-2*radius)) : minimumBounce):0;
            this.minimumHorizontal=minimumHorizontal;
            Orbs=new[]{new CombatantState(playerHP,player),new CombatantState(enemyHP,enemy)};
            var random=new System.Random(seed);
            for(int i=0;i<2;i++)
            {
                float angle=(float)(random.NextDouble()*Math.PI*2);
                Orbs[i].Position=new Vector2((i==0?-1:1)*Size*0.23f,(float)(random.NextDouble()-0.5)*Size*0.15f);
                Orbs[i].Velocity=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*MoveSpeed(i);
                if(gravity)
                {
                    float horizontal=(0.5f+(float)random.NextDouble()*0.3f)*MoveSpeed(i);
                    Orbs[i].Velocity=new Vector2(i==0?horizontal:-horizontal,2+(float)random.NextDouble()*3);
                }
                Orbs[i].Angle=(float)random.NextDouble()*360;
                Orbs[i].RotationDirection=i==0?1:-1;
            }
        }
        public void Step(float dt)
        {
            if(Result!=MatchResult.InProgress || dt<=0) return;
            foreach(var p in projectiles) p.PreviousPosition=p.Position;
            foreach(var w in waves) w.PreviousRadius=w.Radius;
            float travel=0;
            for(int i=0;i<2;i++)
                travel+=Orbs[i].Velocity.magnitude+Gravity*dt+(Orbs[i].Weapon.IsMelee ? Orbs[i].Weapon.RotationSpeed*Mathf.Deg2Rad*Radius*(Orbs[i].Weapon.Reach+2) : 0);
            // Include BOTH moving contours and real ballistic velocities. Even at x2 every tick is
            // consumed unchanged, with contour travel below the thinnest visible weapon features.
            int steps=Mathf.Max(1,Mathf.CeilToInt(travel*dt/(Radius*0.08f)));
            float h=dt/steps;
            for(int s=0;s<steps && Result==MatchResult.InProgress;s++) Substep(h);
        }
        void Substep(float dt)
        {
            elapsed+=dt; clashCooldown=Mathf.Max(0,clashCooldown-dt);
            for(int i=0;i<2;i++)
            {
                var orb=Orbs[i]; start[i]=orb.Position; startAngle[i]=orb.Angle;
                deltaAngle[i]=orb.RotationDirection*orb.Weapon.RotationSpeed*dt;
                if(OrbMotion.Move(orb,dt,Size*0.5f,Radius,Gravity,Restitution,Bounce(i))) Emit(CombatEventKind.Wall,orb.Position,i);
                orb.Angle=Mathf.Repeat(orb.Angle+deltaAngle[i],360);
            }
            bool touches=OrbMotion.Resolve(Orbs[0],Orbs[1],Radius,MoveSpeed(0),UsesGravity,Restitution,MoveSpeed(1));
            if(touches && !orbContact) Emit(CombatEventKind.Body,(Orbs[0].Position+Orbs[1].Position)/2);
            bool newBodyContact=touches && !orbContact;
            orbContact=touches;
            for(int i=0;i<2;i++)
            {
                if(UsesGravity && Mathf.Abs(Orbs[i].Velocity.x)<MoveSpeed(i)*minimumHorizontal)
                    Orbs[i].Velocity.x=(Orbs[i].Velocity.x!=0?Mathf.Sign(Orbs[i].Velocity.x):(i==0?1:-1))*MoveSpeed(i)*minimumHorizontal;
                OrbMotion.Move(Orbs[i],0,Size*0.5f,Radius,Gravity,Restitution,Bounce(i)); end[i]=Orbs[i].Position;
            }
            WeaponGeometry.Fill(Orbs[0],Radius,first); WeaponGeometry.Fill(Orbs[1],Radius,second);
            bool clash=WeaponGeometry.Clash(first,second);
            bool hitA=WeaponGeometry.TouchesBody(first,Orbs[1].Position,Radius);
            bool hitB=WeaponGeometry.TouchesBody(second,Orbs[0].Position,Radius);
            bool blockA=false,blockB=false;
            if(clash && !weaponContact && clashCooldown<=0)
            {
                float impact=Fraction(2);
                // Compare actual impact times. An unrelated overlap no longer disables all body hits.
                blockA=hitA && impact<=Fraction(0)+0.001f;
                blockB=hitB && impact<=Fraction(1)+0.001f;
                bool shieldA=Orbs[0].Weapon.Kind==WeaponKind.Shield,shieldB=Orbs[1].Weapon.Kind==WeaponKind.Shield;
                if(shieldA!=shieldB)
                {
                    int defender=shieldA?0:1,attacker=1-defender;
                    float returned=Orbs[defender].Weapon.Block(Orbs[attacker].Weapon.Damage);
                    Orbs[attacker].Health.Damage(returned);
                    Emit(CombatEventKind.Block,Orbs[attacker].Position,attacker,returned);
                }
                else Emit(CombatEventKind.Clash,(Orbs[0].Position+Orbs[1].Position)*0.5f);
                for(int i=0;i<2;i++)
                {
                    Orbs[i].RotationDirection*=-1;
                    Orbs[i].Angle=Mathf.Repeat(startAngle[i]+deltaAngle[i]*(2*impact-1),360);
                }
                Vector2 n=OrbMotion.Direction(Orbs[1].Position-Orbs[0].Position,Vector2.right);
                float approach=Vector2.Dot(Orbs[1].Velocity-Orbs[0].Velocity,n);
                if(approach<0)
                {
                    float impulse=-approach*0.08f;
                    Orbs[0].Velocity-=n*impulse; Orbs[1].Velocity+=n*impulse;
                    if(!UsesGravity)
                        for(int i=0;i<2;i++) Orbs[i].Velocity=OrbMotion.Direction(Orbs[i].Velocity,i==0?-n:n)*MoveSpeed(i);
                }
                clashCooldown=0.045f;
            }
            // A shield remains a barrier during its single continuous blocking contact.
            if(clash && Orbs[1].Weapon.Kind==WeaponKind.Shield) blockA=true;
            if(clash && Orbs[0].Weapon.Kind==WeaponKind.Shield) blockB=true;
            weaponContact=clash;
            if(hitA && !bodyContact[0] && !blockA) Hit(0);
            if(hitB && !bodyContact[1] && !blockB) Hit(1);
            bodyContact[0]=hitA; bodyContact[1]=hitB;
            for(int i=0;i<2;i++)
            {
                bool blocked=Orbs[i].Weapon.Kind==WeaponKind.Unarmed && Orbs[1-i].Weapon.Kind==WeaponKind.Shield &&
                    WeaponGeometry.TouchesBody(i==0?second:first,Orbs[i].Position,Radius);
                if(blocked && !unarmedShieldContact[i])
                {
                    float returned=Orbs[1-i].Weapon.Block(Orbs[i].Weapon.Damage);
                    Orbs[i].Health.Damage(returned); Emit(CombatEventKind.Block,Orbs[i].Position,i,returned);
                }
                if(Orbs[i].Weapon.Kind==WeaponKind.Unarmed && newBodyContact && !blocked) Hit(i);
                unarmedShieldContact[i]=blocked;
            }
            TickProjectiles(dt);
            poisonClock+=dt;
            if(poisonClock>=1)
            {
                poisonClock-=1;
                for(int i=0;i<2;i++) if(Orbs[i].Health.PoisonPerSecond>0)
                {
                    Orbs[i].Health.Damage(Orbs[i].Health.PoisonPerSecond);
                    Emit(CombatEventKind.Poison,Orbs[i].Position,i,Orbs[i].Health.PoisonPerSecond);
                }
            }
            if(Orbs[0].Health.IsDead || Orbs[1].Health.IsDead)
            {
                Result=Orbs[0].Health.IsDead && Orbs[1].Health.IsDead ? MatchResult.Draw : Orbs[1].Health.IsDead ? MatchResult.Player : MatchResult.Enemy;
                Emit(CombatEventKind.Finished,Vector2.zero);
            }
        }
        float Fraction(int type)
        {
            if(Sample(type,0)) return 0;
            float low=0,high=1;
            for(int i=0;i<6;i++) { float mid=(low+high)*0.5f; if(Sample(type,mid)) high=mid; else low=mid; }
            return high;
        }
        bool Sample(int type,float t)
        {
            Vector2 a=Vector2.Lerp(start[0],end[0],t),b=Vector2.Lerp(start[1],end[1],t);
            if(type!=1) WeaponGeometry.Fill(Orbs[0].Weapon,Radius,a,startAngle[0]+deltaAngle[0]*t,sampleFirst);
            if(type!=0) WeaponGeometry.Fill(Orbs[1].Weapon,Radius,b,startAngle[1]+deltaAngle[1]*t,sampleSecond);
            return type==0 ? WeaponGeometry.TouchesBody(sampleFirst,b,Radius) : type==1 ? WeaponGeometry.TouchesBody(sampleSecond,a,Radius) : WeaponGeometry.Clash(sampleFirst,sampleSecond);
        }
        float MoveSpeed(int side) => Speed+Orbs[side].Weapon.SpeedBonus;
        float Bounce(int side) => Speed>0 ? MinimumBounce*MoveSpeed(side)/Speed : MinimumBounce;
        void Hit(int attacker) => DamageHit(attacker,Orbs[attacker].Weapon.Damage,Orbs[1-attacker].Position);
        void Emit(CombatEventKind kind,Vector2 position,int side=0,float amount=0) => Event?.Invoke(new CombatEvent(kind,position,side,amount));
    }
}
