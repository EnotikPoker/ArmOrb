using System;
using UnityEngine;

namespace ArmOrb
{
    // Construct these on the main thread. Workers use only numbers and copied pixel rectangles.
    public sealed class WeaponTemplate
    {
        public readonly WeaponKind Kind;
        readonly float damage,reach,rotation,growth,cooldown,projectileSpeed,blastRadius,waveSpeed,burstInterval,maxSpeed,speedUnit,maxRotation;
        readonly WeaponShapeData shape;
        public WeaponTemplate(WeaponDefinition source)
        {
            Kind=source.kind;damage=source.damage;reach=source.reach;rotation=source.rotationSpeed;growth=source.growth;
            cooldown=source.cooldown;projectileSpeed=source.projectileSpeed;blastRadius=source.blastRadius;waveSpeed=source.waveSpeed;
            burstInterval=source.burstInterval;maxSpeed=source.unarmedMaxSpeed;speedUnit=source.unarmedSpeedUnit;maxRotation=source.maximumRotationSpeed;
            shape=new WeaponShapeData(source.shape);
        }
        public WeaponState CreateState()=>new WeaponState(Kind,damage,reach,rotation,growth,1,null,cooldown,projectileSpeed,blastRadius,waveSpeed,burstInterval,maxSpeed,speedUnit,maxRotation,shape);
    }
    [Serializable]
    public sealed class FightDefinition
    {
        public int[] weapons={0,1};
        public int[] health={100,100};
        public ArenaSize arena=ArenaSize.Medium;
        public bool gravity=true,activeMotion=true,danger=true;
        public int seed;
        public int Count=>weapons.Length;
        public bool IsValid(int weaponCount=10)
        {
            if(weapons==null||health==null||Count<2||Count>5||health.Length!=Count||(int)arena<0||(int)arena>4)return false;
            for(int i=0;i<Count;i++)if(weapons[i]<0||weapons[i]>=weaponCount||health[i]<1||health[i]>99999)return false;
            return true;
        }
        public FightDefinition Copy()=>new FightDefinition{weapons=(int[])weapons.Clone(),health=(int[])health.Clone(),arena=arena,gravity=gravity,activeMotion=activeMotion,danger=danger,seed=seed};
        public static FightDefinition RandomOffer(System.Random random)
        {
            int count=random.Next(2,6);var f=new FightDefinition{weapons=new int[count],health=new int[count],gravity=random.Next(2)==1,seed=random.Next(1,int.MaxValue)};
            int[][] sizes={new[]{0,1,2},new[]{1,2,3},new[]{2,3,4},new[]{3,4}};
            var allowed=sizes[count-2];f.arena=(ArenaSize)allowed[random.Next(allowed.Length)];
            for(int i=0;i<count;i++){f.weapons[i]=random.Next(10);f.health[i]=50*random.Next(1,7);}return f;
        }
    }
    public sealed class SimulationFactory
    {
        readonly WeaponTemplate[] weapons;
        readonly float[] sizes;
        readonly float radius,speed,gravity,restitution;
        public readonly int Frequency;
        public SimulationFactory(WeaponDefinition[] definitions,ArenaSettings settings)
        {
            weapons=Array.ConvertAll(definitions,w=>w.Snapshot());sizes=new float[5];
            for(int i=0;i<5;i++)sizes[i]=settings.GetSize((ArenaSize)i);
            radius=settings.orbRadius;speed=settings.moveSpeed;gravity=settings.gravity;restitution=settings.restitution;Frequency=settings.simulationHz;
        }
        public ArenaSimulation Create(FightDefinition definition,int? seed=null)
        {
            var states=new WeaponState[definition.Count];var health=new float[definition.Count];
            for(int i=0;i<states.Length;i++){states[i]=weapons[definition.weapons[i]].CreateState();health[i]=definition.health[i];}
            return new ArenaSimulation(states,health,seed??definition.seed,sizes[(int)definition.arena],radius,speed,definition.gravity,gravity,restitution,definition.activeMotion,definition.danger);
        }
    }
}
