using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArmOrb.Tests
{
    public sealed class VersionNineTests
    {
        static SimulationFactory Factory()
        {return new SimulationFactory(Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(k=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/ArmOrb/Settings/{k}.asset")).ToArray(),AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/ArmOrb/Settings/Arena.asset"));}
        static BuildMatchController Match(BattleVariant variant=BattleVariant.Ability)
        {
            var fight=BuildMatchController.CreateOffer(4,1,10891);fight.weapons[1]=4;
            return new BuildMatchController(Factory(),fight,variant);
        }
        static void Run(BuildMatchController match,int ticks){for(int i=0;i<ticks&&match.Active;i++)match.Tick(1f/120);}
        [Test] public void NewDraftAndCombatOffersGiveEightSecondsAndAutoselectOnce()
        {
            var m=Match();Assert.That(m.Data.version,Is.EqualTo(4));Assert.That(m.Data.choiceSeconds,Is.EqualTo(8));
            Run(m,959);Assert.That(m.Data.started,Is.False);Run(m,1);Assert.That(m.Data.started,Is.True);Assert.That(m.Data.ticks,Is.Zero);
            Run(m,2400);Assert.That(m.Data.offers[0].open,Is.True);Run(m,959);Assert.That(m.Data.offers[0].open,Is.True);Run(m,1);Assert.That(m.Data.offers[0].open,Is.False);
            Assert.That(m.Data.commands.Count(c=>c.kind==BuildCommandKind.Upgrade&&c.actor==0),Is.EqualTo(2));Run(m,1);Assert.That(m.Choose(0),Is.False);
        }
        [TestCase(BattleVariant.Builds)] [TestCase(BattleVariant.Ability)] [TestCase(BattleVariant.Sparks)]
        public void OldOpenOfferGainsOneSecondWithoutChangingBattleOrJournal(BattleVariant variant)
        {
            var m=Match(variant);m.Choose(m.AutoChoice());Run(m,2460);
            var old=JsonUtility.FromJson<BuildMatchSave>(JsonUtility.ToJson(m.Data));old.version=3;old.choiceSeconds=7;
            foreach(var offer in old.offers)if(offer.open)offer.deadlineTick-=120;
            string commands=JsonUtility.ToJson(new Commands{items=old.commands.ToArray()});int deadline=old.offers[0].deadlineTick;
            var restored=new BuildMatchController(Factory(),old);
            Assert.That(restored.Data.version,Is.EqualTo(4));Assert.That(restored.Data.variant,Is.EqualTo(variant));
            Assert.That(old.offers[0].deadlineTick,Is.EqualTo(deadline+120));Assert.That(restored.ChoiceRemaining,Is.EqualTo(7.5f));
            Assert.That(JsonUtility.ToJson(new Commands{items=old.commands.ToArray()}),Is.EqualTo(commands));
            Assert.That(restored.Simulation.Orbs[0].Position,Is.EqualTo(m.Simulation.Orbs[0].Position));Assert.That(restored.Simulation.Orbs[0].Health.Current,Is.EqualTo(m.Simulation.Orbs[0].Health.Current));
            BuildMatchSave.Migrate(old);Assert.That(old.offers[0].deadlineTick,Is.EqualTo(deadline+120));
        }
        [Serializable] class Commands {public BuildCommand[] items;}
        [Test] public void OldStartingDraftKeepsElapsedTimeAndCanBeReadAgainAfterSave()
        {
            string dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                var m=Match();Run(m,600);var old=m.Data;old.version=3;old.choiceSeconds=7;foreach(var offer in old.offers)if(offer.open)offer.deadlineTick-=120;
                File.WriteAllText(Path.Combine(dir,"bot-match.json"),JsonUtility.ToJson(old));var store=new BuildMatchStore(dir);
                Assert.That(store.Pending,Is.Not.Null);var restored=new BuildMatchController(Factory(),store.Pending);Assert.That(restored.ChoiceRemaining,Is.EqualTo(3));
                store.Save(restored.Data);Assert.That(new BuildMatchStore(dir).Pending.choiceSeconds,Is.EqualTo(8));Run(restored,359);Assert.That(restored.Data.started,Is.False);Run(restored,1);Assert.That(restored.Data.started,Is.True);
            }
            finally{Directory.Delete(dir,true);}
        }
        [Test] public void SummariesCoverAllSkillsAndUseLevelChangesRatherThanRepeatedFullValue()
        {
            for(int i=0;i<BuildSkills.Count;i++)for(int level=1;level<=BuildSkills.MaxLevel((BuildSkill)i);level++)
            {string summary=InterfacePresentation.SkillSummary((BuildSkill)i,level);Assert.That(summary,Is.Not.Empty);Assert.That(summary.Length,Is.LessThan(90));}
            Assert.That(InterfacePresentation.SkillSummary(BuildSkill.Vitality,2),Does.Contain("450 → 550"));
            Assert.That(InterfacePresentation.SkillSummary(BuildSkill.Regeneration,3),Does.Contain("6 → 8"));
        }
        [TestCase(WeaponKind.Sword,"Урон")] [TestCase(WeaponKind.Spear,"Длина")] [TestCase(WeaponKind.Blade,"Вращение")]
        [TestCase(WeaponKind.Scythe,"Яд")] [TestCase(WeaponKind.Shield,"Возврат 100%")] [TestCase(WeaponKind.Unarmed,"Разгон")]
        [TestCase(WeaponKind.Bow,"След. очередь")] [TestCase(WeaponKind.Crossbow,"След. залп")] [TestCase(WeaponKind.Staff,"Радиус")] [TestCase(WeaponKind.Shuriken,"Отскоки")]
        public void EveryWeaponHasActualReadableStatistics(WeaponKind kind,string expected)
        {
            var w=new WeaponState(kind,2,3.5f,573,1);var orb=new CombatantState(300,w);w.MeasureSpeed(new Vector2(6.4f,0));
            string text=InterfacePresentation.WeaponStats(orb);Assert.That(text,Does.Contain(expected));Assert.That(text.Length,Is.LessThan(85));
        }
        [Test] public void WeaponStatsAccountForCurrentBuildButExplainConditionalBonuses()
        {
            var orb=new CombatantState(300,new WeaponState(WeaponKind.Bow,10));var b=new BuildFighter();
            b.Levels[(int)BuildSkill.Power]=2;b.Levels[(int)BuildSkill.BowDouble]=1;b.Levels[(int)BuildSkill.Critical]=3;
            Assert.That(InterfacePresentation.WeaponStats(orb,b),Is.EqualTo("След. очередь 2 · Стрела 17"));
            Assert.That(InterfacePresentation.CombatDetails(orb,b),Does.Contain("45%"));Assert.That(orb.Weapon.ArrowCount,Is.EqualTo(1));
            var spear=new WeaponState(WeaponKind.Spear,2,4);spear.BuildReachMultiplier=1.5f;Assert.That(InterfacePresentation.WeaponStats(new CombatantState(300,spear)),Does.Contain("Длина 6r"));
        }
        [Test] public void AllSkillIconsUseStableDistinctVectorIds()
        {
            for(int i=0;i<30;i++){var icon=new InterfaceIcon((BuildSkill)i);Assert.That(icon.Symbol,Is.EqualTo(i));Assert.That(icon.Tint.a,Is.EqualTo(1));}
        }
    }
}
