using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Editor
{
    public static class VersionEightReport
    {
        public static void Run()
        {
            var weapons=Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray();
            var factory=new SimulationFactory(weapons,AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
            Directory.CreateDirectory("Artifacts/v0.8");
            using var writer=new StreamWriter("Artifacts/v0.8/battle-report.csv");
            writer.WriteLine("variant,participants,build,seed,weapons,outcome,seconds,player_hp,player_max,upgrades,commands,wall_seconds");
            for(int variant=0;variant<3;variant++)for(int count=2;count<=5;count+=3)for(int build=0;build<2;build++)
            {
                int seed=81001+variant*101+count*17+build*7;
                var f=BuildMatchController.CreateOffer(build==0?0:7,count-1,seed);
                var m=new BuildMatchController(factory,f,(BattleVariant)variant,BotDifficulty.Normal,build==0?BattleAbility.Barrier:BattleAbility.Heal);
                var watch=Stopwatch.StartNew();
                for(int t=0;t<factory.Frequency*308&&m.Active;t++)
                {
                    if(m.Data.offers[0].open)
                    {
                        int selected=m.AutoChoice();float best=-1;
                        for(int i=0;i<m.Data.offers[0].cards.Length;i++)
                        {var skill=m.Data.offers[0].cards[i].skill;float value=BuildSkills.Defensive(skill)==(build==0)?2:1;if(skill==BuildSkill.Vitality||skill==BuildSkill.Resurrection)value+=.5f;if(value>best){best=value;selected=i;}}
                        m.Choose(selected);
                    }
                    if(variant==1)m.UseAbility();
                    if(variant==2&&m.Simulation.Builds[0].Spark.Active)m.CollectSpark(0,m.Simulation.Builds[0].Spark.Id);
                    m.Tick(1f/factory.Frequency);
                }
                if(m.Active)throw new Exception("Battle exceeded the five minute limit");
                if(m.Simulation.Danger)throw new Exception("Danger zone enabled in bot battle");
                var orb=m.Simulation.Orbs[0];
                writer.WriteLine($"{variant},{count},{build},{seed},{string.Join("/",f.weapons)},{m.Data.outcome},{m.Simulation.Elapsed:0.000},{orb.Health.Current:0.000},{orb.Health.Maximum:0},{m.Simulation.Builds[0].Levels.Sum()},{m.Data.commands.Count},{watch.Elapsed.TotalSeconds:0.000}");writer.Flush();
                UnityEngine.Debug.Log($"ArmOrb v0.8 report: {variant}/{count}/{build} outcome={m.Data.outcome} t={m.Simulation.Elapsed:0.00}");
            }
        }
    }
}
