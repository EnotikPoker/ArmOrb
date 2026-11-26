using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Editor
{
    public static class VersionTenReport
    {
        public static void Run()
        {
            var weapons=Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray();
            var factory=new SimulationFactory(weapons,AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));
            string temp=Path.Combine(Path.GetTempPath(),"armorb-report-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory("Artifacts/v0.10");
            try
            {
                using var writer=new StreamWriter("Artifacts/v0.10/run-report.csv");writer.WriteLine("weapon,seed,completed,outcome,seconds,coins,wall_seconds");
                for(int weapon=0;weapon<10;weapon++)
                {
                    var store=new RunStore(temp+"/run"+weapon);var profile=new CollectionProfile(temp+"/profile"+weapon);int seed=100003+weapon*7919;
                    var run=new RunController(factory,store,profile,weapon,BattleAbility.Heal,seed);var watch=Stopwatch.StartNew();int operations=0;
                    while(run.Active&&operations++<factory.Frequency*610)
                    {
                        if(run.Data.phase==RunPhase.Preview)run.StartStage();
                        else if(run.Data.phase==RunPhase.RewardChoice)
                        {
                            int slot=Array.FindIndex(run.Data.rewards,r=>r.heal&&run.Data.player.hp<run.Data.player.Maximum*.75f);
                            if(slot<0)slot=Array.FindIndex(run.Data.rewards,r=>!r.heal);if(slot<0&&run.Data.rewards.Length>0)slot=0;
                            if(slot<0)run.SkipReward();else run.ChooseReward(slot);
                        }
                        else if(run.Data.phase==RunPhase.Battle)
                        {
                            var battle=run.Battle;if(battle.Data.offers[0].open)battle.Choose(battle.AutoChoice());
                            var player=battle.Simulation.Orbs[0];if(player.Health.Maximum-player.Health.Current>=90)battle.UseAbility();
                            run.Tick(1f/factory.Frequency);
                        }
                        else if(run.Data.phase==RunPhase.RewardPending)run.ResolveReward();
                    }
                    if(run.Active)throw new Exception("Run exceeded bound");
                    writer.WriteLine($"{weapon},{seed},{run.Data.completed},{run.Data.outcome},{run.Data.totalSeconds:0.000},{profile.Data.coins},{watch.Elapsed.TotalSeconds:0.000}");writer.Flush();
                    UnityEngine.Debug.Log($"ArmOrb RUN REPORT weapon={weapon} stages={run.Data.completed} outcome={run.Data.outcome} t={run.Data.totalSeconds:0.00}");
                }
            }
            finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
        }
    }
}
