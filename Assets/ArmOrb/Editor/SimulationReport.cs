using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Editor
{
    public static class SimulationReport
    {
        // Fixed inputs permit comparisons across builds; this is not a phone UI stress test.
        public static void Run()
        {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            var definitions=Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray();
            var factory=new SimulationFactory(definitions,AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
            Directory.CreateDirectory("Artifacts/v0.5-validation");
            using var csv=new StreamWriter("Artifacts/v0.5-validation/simulations.csv");
            csv.WriteLine("weapons,hp,gravity,seed,first_damage,max_quiet,seconds,winner,overtime,wall_seconds,first_contact,max_contact_quiet");
            var watch=Stopwatch.StartNew();int count=0;
            for(int a=0;a<10;a++)for(int b=a;b<10;b++)foreach(bool gravity in new[]{false,true})
            {
                var f=new FightDefinition{weapons=new[]{a,b},health=new[]{100,100},gravity=gravity,seed=104729+a*101+b*17};
                Measure(factory,f,csv);count++;
            }
            var random=new System.Random(34691);
            for(int i=0;i<16;i++){var f=FightDefinition.RandomOffer(random);Measure(factory,f,csv);count++;}
            UnityEngine.Debug.Log($"ArmOrb REPORT {count} fights in {watch.Elapsed.TotalSeconds:0.0}s; Artifacts/v0.5-validation/simulations.csv");
        }
        static void Measure(SimulationFactory factory,FightDefinition f,StreamWriter csv)
        {
            var sim=factory.Create(f);float first=-1,last=0,quiet=0,firstContact=-1,lastContact=0,contactQuiet=0;var timer=Stopwatch.StartNew();
            sim.Event+=e=>{if(e.Kind==CombatEventKind.Body||e.Kind==CombatEventKind.Clash||e.Kind==CombatEventKind.Hit||e.Kind==CombatEventKind.Block){if(firstContact<0)firstContact=sim.Elapsed;contactQuiet=Mathf.Max(contactQuiet,sim.Elapsed-lastContact);lastContact=sim.Elapsed;}if((e.Kind==CombatEventKind.Hit||e.Kind==CombatEventKind.Poison||e.Kind==CombatEventKind.Block)&&e.Amount>0){if(first<0)first=sim.Elapsed;quiet=Mathf.Max(quiet,sim.Elapsed-last);contactQuiet=Mathf.Max(contactQuiet,sim.Elapsed-lastContact);last=sim.Elapsed;}};
            for(int t=0;t<120*136&&sim.Result==MatchResult.InProgress;t++)sim.Step(1f/120);
            quiet=Mathf.Max(quiet,sim.Elapsed-last);contactQuiet=Mathf.Max(contactQuiet,sim.Elapsed-lastContact);
            foreach(var orb in sim.Orbs)
                if(float.IsNaN(orb.Position.x)||float.IsNaN(orb.Position.y)||Mathf.Abs(orb.Position.x)>sim.Size/2-sim.Radius+0.002f||Mathf.Abs(orb.Position.y)>sim.Size/2-sim.Radius+0.002f)throw new Exception("Invalid simulated position");
            if(sim.Result==MatchResult.InProgress)throw new Exception("Fight exceeded overtime guarantee");
            csv.WriteLine($"{string.Join("/",f.weapons)},{string.Join("/",f.health)},{f.gravity},{f.seed},{first:0.000},{quiet:0.000},{sim.Elapsed:0.000},{sim.WinnerIndex},{sim.Elapsed>=90},{timer.Elapsed.TotalSeconds:0.000},{firstContact:0.000},{contactQuiet:0.000}");csv.Flush();
        }
    }
}
