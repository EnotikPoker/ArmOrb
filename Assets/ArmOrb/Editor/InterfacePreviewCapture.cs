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
    public static class InterfacePreviewCapture
    {
        const string Flag="ArmOrb.InterfacePreview";
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
            Directory.CreateDirectory("Artifacts/v0.9-preview");
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
                        Capture("main-menu");game.SelectMode(GameMode.Bots);game.BotCount=4;game.BotWeapon=2;
                        Set(game,"BotOffer",BuildMatchController.CreateOffer(2,4,90129));Ui().ShowHome();break;
                    case 2:
                        Capture("setup");
                        game.StartBots();break;
                    case 3:
                        Capture("choice");game.ChooseBotSkill(game.BotMatch.AutoChoice());break;
                    case 4:
                        Capture("battle");
                        game.BackToSetup();game.SelectMode(GameMode.Test);break;
                    case 5:
                        Capture("test");
                        typeof(GameInterface).GetMethod("ShowNumberEntry",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Ui(),new object[]{"Здоровье",100L,1L,99999L,(Action<long>)(_=>{})});break;
                    case 6:
                        Capture("number-entry");game.OpenMainMenu();Set(game,"BotMatch",null);
                        PlayerPrefs.SetInt("ArmOrb.Mode",SessionState.GetInt(Flag+".Mode",0));PlayerPrefs.Save();
                        SessionState.SetBool(Flag,false);EditorApplication.update-=Tick;
                        if(Directory.Exists(directory))Directory.Delete(directory,true);
                        Debug.Log("ArmOrb v0.9 runtime UI preview captured successfully.");EditorApplication.Exit(0);break;
                }
            }
            catch(Exception e){SessionState.SetBool(Flag,false);EditorApplication.update-=Tick;Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Capture(string name)
        {
            var old=RenderTexture.active;RenderTexture.active=output;
            var image=new Texture2D(output.width,output.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,output.width,output.height),0,0);image.Apply();
            File.WriteAllBytes("Artifacts/v0.9-preview/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=old;
        }
    }
}
