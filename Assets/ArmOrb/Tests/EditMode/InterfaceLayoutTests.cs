using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ArmOrb.Tests
{
    public sealed class InterfaceLayoutTests
    {
        public class PreviewWindow : EditorWindow { }
        PreviewWindow window;
        UnityEngine.SceneManagement.Scene scene;
        ArmOrbGame game;
        string directory;
        VisualElement host;
        GameInterface ui;
        static void Set(object obj,string property,object value)=>obj.GetType().GetProperty(property,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);
        static void Invoke(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);
        [SetUp] public void Setup()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Requires a graphics device; run Unity without -nographics for the layout checks.");
            directory=Path.Combine(Path.GetTempPath(),"armorb-ui-"+Guid.NewGuid().ToString("N"));
            scene=EditorSceneManager.OpenScene("Assets/ArmOrb/Scenes/Main.unity",OpenSceneMode.Additive);
            game=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ArmOrbGame>(true)).Single();
            game.enabled=false;
            Set(game,"Wallet",new WalletStore(directory+"/wallet"));Set(game,"BotStore",new BuildMatchStore(directory+"/bots"));
            window=ScriptableObject.CreateInstance<PreviewWindow>();window.position=new Rect(0,0,780,1450);window.Show();
            host=new VisualElement();host.style.position=Position.Absolute;host.style.width=720;host.style.height=1280;window.rootVisualElement.Add(host);
            ui=new GameInterface(game,host);
        }
        [TearDown] public void Cleanup()
        {
            if(window)window.Close();
            if(scene.IsValid())EditorSceneManager.CloseScene(scene,true);
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        IEnumerator Layout(){for(int i=0;i<12;i++){window.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return null;}}
        void Battle(int count)
        {
            var f=BuildMatchController.CreateOffer(2,count-1,10891);
            var factory=new SimulationFactory(game.weapons,game.settings);var match=new BuildMatchController(factory,f);
            Set(game,"CurrentFight",f);Set(game,"BotMatch",match);Set(game,"Simulation",match.Simulation);Set(game,"IsBotFight",true);Set(game,"IsInFight",true);Set(game,"Mode",GameMode.Bots);
            ui.ShowFight();
        }
        [UnityTest] public IEnumerator SkillChoiceAndAbilityFitAllThreePortraitSizesWithoutMovingArena()
        {
            foreach(var size in new[]{new Vector2(360,640),new Vector2(360,800),new Vector2(412,915)})
            {
                host.style.height=size.y/size.x*720;Battle(5);yield return Layout();
                var arena=host.Q("arena");var cards=host.Query<Button>(className:"skill-card").ToList();var ability=host.Q("use-ability");
                Assert.That(cards.Count,Is.EqualTo(3));
                Assert.That(cards[0].worldBound.yMax,Is.LessThanOrEqualTo(cards[1].worldBound.yMin+1));
                Assert.That(cards[1].worldBound.yMax,Is.LessThanOrEqualTo(cards[2].worldBound.yMin+1));
                Assert.That(cards[2].worldBound.yMax,Is.LessThanOrEqualTo(ability.worldBound.yMin+1));
                Assert.That(ability.worldBound.yMax,Is.LessThanOrEqualTo(host.worldBound.yMax+1),$"{size}: active button clips");
                Assert.That(arena.worldBound.height,Is.GreaterThan(120));
                float before=arena.worldBound.height;game.BotMatch.Choose(game.BotMatch.AutoChoice());ui.RefreshFight();yield return Layout();
                Assert.That(arena.worldBound.height,Is.EqualTo(before).Within(1));
                Invoke(ui,"InspectParticipant",1);yield return Layout();Assert.That(arena.worldBound.height,Is.EqualTo(before).Within(1));Assert.That(host.Q("close-inspection"),Is.Not.Null);
                Assert.That(game.IsPaused,Is.False);Assert.That(game.BotMatch.Data.offers[0].open,Is.False);
            }
        }
        [UnityTest] public IEnumerator LongSkillRowsAndLateBuildRemainReadableAndChoiceOverridesInspection()
        {
            Battle(5);var match=game.BotMatch;var offer=match.Data.offers[0];
            offer.cards=new[]{new BuildCard{skill=BuildSkill.SpearLunge},new BuildCard{skill=BuildSkill.Hunter},new BuildCard{skill=BuildSkill.UnarmedRam}};
            offer.id++;ui.RefreshFight();yield return Layout();
            foreach(var description in host.Query<Label>(className:"skill-description").ToList())
            {
                Assert.That(description.worldBound.xMax,Is.LessThanOrEqualTo(description.parent.parent.worldBound.xMax));
                Assert.That(description.worldBound.yMax,Is.LessThanOrEqualTo(description.parent.parent.worldBound.yMax+1));
                Assert.That(description.worldBound.width,Is.GreaterThan(300));
            }
            // Supply a valid choice, then populate a large real build for the scrolling collection.
            offer.cards=new[]{new BuildCard{skill=BuildSkill.Vitality}};match.Choose(0);
            for(int i=0;i<20;i++)if(match.Simulation.CanBuildSkill(0,(BuildSkill)i))match.Simulation.ApplyBuildSkill(0,(BuildSkill)i);
            ui.RefreshFight();yield return Layout();Assert.That(host.Query<Button>(className:"build-icon").ToList().Count,Is.GreaterThan(12));
            Invoke(ui,"InspectParticipant",1);yield return Layout();Assert.That(host.Q(className:"inspection-area").resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
            Invoke(match,"OpenOffer",0);ui.RefreshFight();yield return Layout();Assert.That(host.Q(className:"inspection-area").resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
            Assert.That(host.Q("skill-card-0").worldBound.height,Is.GreaterThanOrEqualTo(96));
        }
        [UnityTest] public IEnumerator BetTestPauseAndResultsShareTheNewUiWithoutDroppingActions()
        {
            Set(game,"AtMainMenu",false);Set(game,"Mode",GameMode.Test);ui.ShowHome();yield return Layout();
            Assert.That(host.Q("start-fight"),Is.Not.Null);Assert.That(host.Q("health-0"),Is.Not.Null);Assert.That(host.Q("arena-size"),Is.Not.Null);
            Set(game,"Mode",GameMode.Bets);ui.ShowHome();yield return Layout();Assert.That(host.Q("new-offer"),Is.Not.Null);
            Battle(2);ui.ShowPause(true);yield return Layout();Assert.That(host.Q("resume"),Is.Not.Null);
            ui.ShowPause(false);Assert.That(host.Q("resume"),Is.Null);game.BotMatch.Data.outcome=1;ui.ShowBotResult();yield return Layout();Assert.That(host.Q("new-bot-battle"),Is.Not.Null);
        }
        static void Submit(Button button)
        {using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        [UnityTest] public IEnumerator NumberPadShowsEditsAndOnlyCommitsOnConfirmation()
        {
            Set(game,"AtMainMenu",false);Set(game,"Mode",GameMode.Bets);ui.ShowHome();long accepted=-1;
            Invoke(ui,"ShowNumberEntry","Здоровье",100L,1L,99999L,(Action<long>)(v=>accepted=v));yield return Layout();
            Assert.That(host.Q<Label>("number-entry-value").text,Is.EqualTo("100"));
            ui.RefreshQuote();yield return Layout();Assert.That(host.Q("number-confirm"),Is.Not.Null);
            Submit(host.Q<Button>("number-key-4"));yield return Layout();Assert.That(host.Q<Label>("number-entry-value").text,Is.EqualTo("4"));
            Assert.That(host.Q("number-confirm").worldBound.yMax,Is.LessThanOrEqualTo(host.worldBound.yMax));
            Submit(host.Q<Button>("number-cancel"));yield return Layout();Assert.That(accepted,Is.EqualTo(-1));
            Invoke(ui,"ShowNumberEntry","Здоровье",100L,1L,99999L,(Action<long>)(v=>accepted=v));yield return Layout();
            Submit(host.Q<Button>("number-key-5"));yield return Layout();Submit(host.Q<Button>("number-confirm"));yield return Layout();Assert.That(accepted,Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator MainMenuSetupAndWeaponSelectionContainRealArtwork()
        {
            ui.ShowHome();yield return Layout();Assert.That(host.Q("mode-bots"),Is.Not.Null);Assert.That(host.Q("mode-bets"),Is.Not.Null);Assert.That(host.Q("mode-test"),Is.Not.Null);
            Set(game,"AtMainMenu",false);Set(game,"Mode",GameMode.Bots);Set(game,"BotOffer",BuildMatchController.CreateOffer(0,4,101));ui.ShowHome();yield return Layout();
            Assert.That(host.Query<Button>(className:"weapon-tile").ToList().Count,Is.EqualTo(10));Assert.That(host.Q("bot-variant"),Is.Null);
            Assert.That(host.Q("bot-ability-0"),Is.Not.Null);Assert.That(host.Q("bot-ability-1"),Is.Not.Null);
            foreach(var image in host.Query<Image>(className:"weapon-tile-image").ToList())Assert.That(image.sprite,Is.Not.Null);
            Assert.That(host.Q("back-main"),Is.Not.Null);
            var field=host.Q<Button>("bot-difficulty");
            Assert.That(field,Is.Not.Null);Assert.That(field.resolvedStyle.backgroundColor.grayscale,Is.LessThan(.3f));
            Assert.That(field.Query<Label>().ToList().Any(l=>l.text=="Обычно"),Is.True);
        }
    }
}
