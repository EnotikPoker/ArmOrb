using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Editor
{
    public static class VersionSixReport
    {
        public static void Run()
        {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            var definitions=Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray();
            var factory=new SimulationFactory(definitions,AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
            Directory.CreateDirectory("Artifacts/v0.6-validation");
            using var csv=new StreamWriter("Artifacts/v0.6-validation/simulations.csv");
            csv.WriteLine("mode,weapons,hp,gravity,seed,seconds,winner,shops,peak_blade,wall_seconds");
            int count=0;
            for(int a=0;a<10;a++)for(int b=a;b<10;b++)
            {
                if(!new[]{2,6,8}.Contains(a)&&!new[]{2,6,8}.Contains(b))continue;
                foreach(bool gravity in new[]{false,true})
                {
                    var f=new FightDefinition{weapons=new[]{a,b},health=new[]{100,100},gravity=gravity,seed=104729+a*101+b*17};
                    var sim=factory.Create(f);var watch=Stopwatch.StartNew();float peak=0;
                    for(int t=0;t<120*136&&sim.Result==MatchResult.InProgress;t++){sim.Step(1f/120);Validate(sim,ref peak);}
                    if(sim.Result==MatchResult.InProgress)throw new Exception("Unfinished duel");
                    Write(csv,"duel",f,sim,0,peak,watch.Elapsed.TotalSeconds);count++;
                }
            }
            for(int bots=1;bots<=4;bots++)for(int level=0;level<3;level++)
            {
                var f=BotMatchController.CreateOffer((bots+level)*2%10,bots,8219+bots*101+level*17);
                var m=new BotMatchController(factory,f,(MiniGameKind)level,(BotDifficulty)level);var watch=Stopwatch.StartNew();float peak=0;int tick=0;
                while(m.Data.phase!=BotPhase.Finished&&tick++<120*136)
                {
                    if(tick%120==0&&m.Data.phase==BotPhase.Combat)m.Earn(2);
                    m.Tick(1f/120);Validate(m.Simulation,ref peak);
                    if(m.Data.offers[0].open)m.Choose(m.AutoChoice());
                }
                if(m.Data.phase!=BotPhase.Finished)throw new Exception("Unfinished bot match");
                Write(csv,"bots-"+level,f,m.Simulation,m.Data.visit,peak,watch.Elapsed.TotalSeconds);count++;
            }
            UnityEngine.Debug.Log($"ArmOrb V06 REPORT {count} fights; Artifacts/v0.6-validation/simulations.csv");
        }
        static void Validate(ArenaSimulation sim,ref float peak)
        {
            foreach(var orb in sim.Orbs)
            {
                if(!float.IsFinite(orb.Position.x)||!float.IsFinite(orb.Position.y)||Mathf.Abs(orb.Position.x)>sim.Size/2-sim.Radius+.003f||Mathf.Abs(orb.Position.y)>sim.Size/2-sim.Radius+.003f)throw new Exception("Invalid position");
                if(orb.Weapon.Kind==WeaponKind.Blade)peak=Mathf.Max(peak,orb.Weapon.RotationSpeed);
            }
        }
        static void Write(StreamWriter csv,string mode,FightDefinition f,ArenaSimulation sim,int shops,float peak,double wall)
        {csv.WriteLine($"{mode},{string.Join("/",f.weapons)},{string.Join("/",f.health)},{f.gravity},{f.seed},{sim.Elapsed:0.000},{sim.WinnerIndex},{shops},{peak:0.0},{wall:0.000}");csv.Flush();}
    }
}
