using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArmOrb
{
    public sealed class CosmeticItem
    {
        public readonly string Id,Name;
        public readonly int Price;
        public readonly bool Trail;
        public CosmeticItem(string id,string name,int price,bool trail=false){Id=id;Name=name;Price=price;Trail=trail;}
    }
    public static class CosmeticCatalog
    {
        public static readonly CosmeticItem[] Items={
            new CosmeticItem("classic","Стандартный",0),new CosmeticItem("robot","Робот",60),new CosmeticItem("cat","Кот",120),
            new CosmeticItem("ninja","Ниндзя",200),new CosmeticItem("astronaut","Космонавт",300),new CosmeticItem("knight","Рыцарь",-1),
            new CosmeticItem("none","Без следа",0,true),new CosmeticItem("dots","Точки",80,true),new CosmeticItem("sparks","Искры",160,true),new CosmeticItem("stars","Звёзды",240,true)
        };
        public static CosmeticItem Find(string id)=>Array.Find(Items,i=>i.Id==id);
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        public static Sprite Sprite(string id)
        {
            if(id=="classic"||Find(id)==null)return null;
            if(!sprites.TryGetValue(id,out var sprite)){sprite=Resources.Load<Sprite>("Cosmetics/"+id);sprites[id]=sprite;}return sprite;
        }
    }
    [Serializable] public sealed class RunRecord
    {
        public string id;
        public int weapon,stage;
        public bool ended,won;
        public float seconds;
    }
    [Serializable] public sealed class CollectionData
    {
        public int version=1;
        public long coins;
        public string skin="classic",trail="none";
        public List<string> owned=new List<string>{"classic","none"},receipts=new List<string>();
        public List<RunRecord> runs=new List<RunRecord>();
    }
    public sealed class CollectionProfile
    {
        readonly string path;
        public CollectionData Data { get; private set; }
        public string Error { get; private set; }
        public CollectionProfile(string directory)
        {
            Directory.CreateDirectory(directory);path=Path.Combine(directory,"collection.json");
            Data=Read(path)??Read(path+".bak");
            if(Data==null){Data=new CollectionData();if(File.Exists(path)||File.Exists(path+".bak"))Error="Профиль коллекции повреждён. Сохранённые файлы не изменены.";}
        }
        static bool Valid(CollectionData d)
        {
            if(d==null||d.version!=1||d.coins<0||d.owned==null||d.receipts==null||d.runs==null||!d.owned.Contains("classic")||!d.owned.Contains("none"))return false;
            var skin=CosmeticCatalog.Find(d.skin);var trail=CosmeticCatalog.Find(d.trail);
            if(skin==null||skin.Trail||trail==null||!trail.Trail||!d.owned.Contains(d.skin)||!d.owned.Contains(d.trail))return false;
            var ids=new HashSet<string>();foreach(string id in d.owned)if(CosmeticCatalog.Find(id)==null||!ids.Add(id))return false;
            ids.Clear();foreach(string id in d.receipts)if(string.IsNullOrEmpty(id)||!ids.Add(id))return false;
            ids.Clear();foreach(var run in d.runs)if(run==null||string.IsNullOrEmpty(run.id)||!ids.Add(run.id)||run.weapon<0||run.weapon>9||run.stage<0||run.stage>5||float.IsNaN(run.seconds)||float.IsInfinity(run.seconds)||run.seconds<0||run.seconds>601)return false;
            return true;
        }
        static CollectionData Read(string file)
        {try{if(!File.Exists(file))return null;var d=JsonUtility.FromJson<CollectionData>(File.ReadAllText(file));return Valid(d)?d:null;}catch(Exception e)when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException){return null;}}
        CollectionData Copy()=>JsonUtility.FromJson<CollectionData>(JsonUtility.ToJson(Data));
        void Commit(CollectionData next)
        {
            if(!Valid(next))throw new InvalidDataException("Invalid collection profile");
            File.WriteAllText(path+".tmp",JsonUtility.ToJson(next,true));
            if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
            Data=next;Error=null;
        }
        static RunRecord Record(CollectionData d,string id,int weapon)
        {
            var run=d.runs.Find(r=>r.id==id);if(run!=null)return run;
            run=new RunRecord{id=id,weapon=weapon};d.runs.Add(run);return run;
        }
        public void BeginRun(string id,int weapon)
        {if(Data.runs.Exists(r=>r.id==id))return;var next=Copy();Record(next,id,weapon);Commit(next);}
        public static int StageCoins(int stage)=>stage switch{0=>20,1=>25,2=>30,3=>35,4=>90,_=>0};
        public bool GrantStage(string runId,int stage,int weapon,float elapsed)
        {
            if(string.IsNullOrEmpty(runId)||stage<0||stage>4)throw new ArgumentException("Invalid reward");
            string receipt=runId+":stage:"+stage;if(Data.receipts.Contains(receipt))return false;
            var next=Copy();next.coins=checked(next.coins+StageCoins(stage));next.receipts.Add(receipt);
            var record=Record(next,runId,weapon);record.stage=Math.Max(record.stage,stage+1);record.seconds=elapsed;
            if(stage==4){record.ended=record.won=true;if(!next.owned.Contains("knight"))next.owned.Add("knight");}
            Commit(next);return true;
        }
        public void EndRun(string id,int weapon,int completed,float elapsed,bool won)
        {
            var old=Data.runs.Find(r=>r.id==id);if(old!=null&&old.ended)return;
            var next=Copy();var record=Record(next,id,weapon);record.stage=completed;record.seconds=elapsed;record.ended=true;record.won=won;Commit(next);
        }
        public bool Owns(string id)=>Data.owned.Contains(id);
        public bool Purchase(string id)
        {
            var item=CosmeticCatalog.Find(id);if(item==null||item.Price<=0||Owns(id)||Data.coins<item.Price)return false;
            var next=Copy();next.coins-=item.Price;next.owned.Add(id);Commit(next);return true;
        }
        public bool Equip(string id)
        {
            var item=CosmeticCatalog.Find(id);if(item==null||!Owns(id))return false;
            var next=Copy();if(item.Trail)next.trail=id;else next.skin=id;Commit(next);return true;
        }
    }
}
