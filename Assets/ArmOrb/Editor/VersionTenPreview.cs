using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArmOrb.Editor
{
    // Renders the actual runtime UI to an isolated texture; never sends input to a phone.
    public static class VersionTenPreview
    {
        const string Flag="ArmOrb.VersionTenPreview";
        static int stage,frames;
        static ArmOrbGame game;
        static RenderTexture output;
        static string directory;
        static int previousMode;
        static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(target,value);
        static GameInterface Ui()=>(GameInterface)typeof(ArmOrbGame).GetField("ui",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
        [InitializeOnLoadMethod] static void Resume()
        {if(SessionState.GetBool(Flag,false))EditorApplication.delayCall+=Attach;}
        public static void Run()
        {
            Directory.CreateDirectory("Artifacts/v0.10-preview");
            previousMode=PlayerPrefs.GetInt("ArmOrb.Mode",0);SessionState.SetInt(Flag+".Mode",previousMode);
            EditorSceneManager.OpenScene("Assets/ArmOrb/Scenes/Main.unity");
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        static void Attach(){stage=0;frames=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(++frames<25)return;
                frames=0;
                switch(stage++)
                {
                    case 0:
                        game=UnityEngine.Object.FindFirstObjectByType<ArmOrbGame>();
                        if(game==null||game.Wallet==null){stage=0;return;}
                        directory=Path.Combine(Path.GetTempPath(),"armorb-preview-"+Guid.NewGuid().ToString("N"));
                        Set(game,"Wallet",new WalletStore(directory+"/wallet"));Set(game,"BotStore",new BuildMatchStore(directory+"/bots"));
                        output=new RenderTexture(720,1280,0,RenderTextureFormat.ARGB32);output.Create();
                        var settings=UnityEngine.Object.Instantiate(game.document.panelSettings);settings.targetTexture=output;
                        game.document.panelSettings=settings;game.OpenMainMenu();break;
                    case 1:
                        Set(game,"Collection",new CollectionProfile(directory+"/profile"));Set(game,"Runs",new RunStore(directory+"/runs"));
                        for(int stage=0;stage<5;stage++)game.Collection.GrantStage("preview",stage,0,stage*30);
                        game.Collection.Purchase("robot");game.Collection.Purchase("dots");game.Collection.Equip("robot");game.Collection.Equip("dots");
                        game.OpenMainMenu();break;
                    case 2:
                        Capture("main-menu");game.SelectMode(GameMode.Collection);break;
                    case 3:
                        Capture("collection");game.SelectMode(GameMode.Runs);game.RunWeapon=0;Ui().ShowHome();break;
                    case 4:
                        Capture("run-setup");game.StartRun();break;
                    case 5:
                        Capture("stage-preview");game.StartRunStage();break;
                    case 6:
                        Capture("choice");game.ChooseBotSkill(game.BotMatch.AutoChoice());break;
                    case 7:
                        Capture("battle");foreach(var orb in game.Simulation.Orbs)if(orb!=game.Simulation.Orbs[0])orb.Health.Damage(100000);
                        game.Run.Tick(1f/120);game.BackToSetup();break;
                    case 8:
                        Capture("reward");game.ChooseRunReward(0);break;
                    case 9:
                        Capture("next-stage");game.OpenMainMenu();Set(game,"BotMatch",null);Set(game,"Run",null);
                        PlayerPrefs.SetInt("ArmOrb.Mode",SessionState.GetInt(Flag+".Mode",0));PlayerPrefs.Save();
                        SessionState.SetBool(Flag,false);EditorApplication.update-=Tick;
                        if(Directory.Exists(directory))Directory.Delete(directory,true);
                        Debug.Log("ArmOrb v0.10 runtime UI preview captured successfully.");EditorApplication.Exit(0);break;
                }
            }
            catch(Exception e){SessionState.SetBool(Flag,false);EditorApplication.update-=Tick;Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Capture(string name)
        {
            var old=RenderTexture.active;RenderTexture.active=output;
            var image=new Texture2D(output.width,output.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,output.width,output.height),0,0);image.Apply();
            File.WriteAllBytes("Artifacts/v0.10-preview/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=old;
        }
    }
}
