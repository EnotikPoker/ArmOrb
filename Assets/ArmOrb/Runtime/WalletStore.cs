using System;
using System.IO;
using UnityEngine;

namespace ArmOrb
{
    [Serializable] public sealed class PendingBet
    {
        public string id;
        public string rules="0.5";
        public FightDefinition fight;
        public long stake;
        public int selection,oddsHundredths,ticks;
    }
    [Serializable] public sealed class WalletData
    {
        public int version=1;
        public long balance=1000;
        public PendingBet pending;
        public bool hasPending;
        public string settledId;
    }
    public sealed class WalletStore
    {
        readonly string path;
        public WalletData Data { get; private set; }
        public string RecoveryMessage { get; private set; }
        public WalletStore(string directory)
        {
            Directory.CreateDirectory(directory);path=Path.Combine(directory,"wallet.json");
            Data=Read(path)??Read(path+".bak");
            if(Data==null)
            {
                Data=new WalletData();
                if(File.Exists(path)||File.Exists(path+".bak"))RecoveryMessage="Сохранение не удалось прочитать. Баланс восстановлен.";
            }
            if(Data.pending!=null&&Data.pending.rules!="0.5")
            {
                var copy=Copy();copy.balance+=copy.pending.stake;copy.settledId=copy.pending.id;copy.pending=null;Commit(copy);
                RecoveryMessage="Правила боя изменились. Ставка возвращена.";
            }
        }
        static WalletData Read(string file)
        {
            try
            {
                if(!File.Exists(file))return null;var data=JsonUtility.FromJson<WalletData>(File.ReadAllText(file));
                if(data==null||data.version!=1||data.balance<0)return null;
                if(data.hasPending&&data.pending==null)return null;
                if(!data.hasPending)data.pending=null;
                var p=data.pending;
                if(p!=null&&(string.IsNullOrEmpty(p.id)||p.fight==null||!p.fight.IsValid()||p.selection<0||p.selection>=p.fight.Count||p.stake<10||p.oddsHundredths<100||p.oddsHundredths>100000||p.ticks<0||p.ticks>240*140))return null;
                return data;
            }
            catch(Exception e) when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException){return null;}
        }
        WalletData Copy()
        { var copy=JsonUtility.FromJson<WalletData>(JsonUtility.ToJson(Data));if(!Data.hasPending)copy.pending=null;return copy; }
        void Commit(WalletData next)
        {
            next.hasPending=next.pending!=null;
            string temporary=path+".tmp";File.WriteAllText(temporary,JsonUtility.ToJson(next,true));
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
            Data=next;
        }
        public bool Place(FightDefinition fight,int selection,long stake,int oddsHundredths)
        {
            if(Data.pending!=null||!fight.IsValid()||selection<0||selection>=fight.Count||stake<10||stake%10!=0||stake>Data.balance||oddsHundredths<100||oddsHundredths>100000)return false;
            try { checked { long possible=Data.balance-stake+Payout(stake,oddsHundredths); } } catch(OverflowException){return false;}
            var next=Copy();next.balance-=stake;next.pending=new PendingBet{id=Guid.NewGuid().ToString("N"),fight=fight.Copy(),selection=selection,stake=stake,oddsHundredths=oddsHundredths};Commit(next);return true;
        }
        public void SaveTicks(int ticks)
        {
            if(Data.pending==null||Data.pending.ticks==ticks)return;
            var next=Copy();next.pending.ticks=ticks;Commit(next);
        }
        public static long Payout(long stake,int oddsHundredths)=>checked((long)decimal.Floor((decimal)stake*oddsHundredths/100));
        public long Settle(string id,int winner)
        {
            if(Data.pending==null||Data.pending.id!=id)return 0;
            var next=Copy();var p=next.pending;
            long payment=winner<0?p.stake:winner==p.selection?Payout(p.stake,p.oddsHundredths):0;
            next.balance=checked(next.balance+payment);next.settledId=p.id;next.pending=null;Commit(next);return payment;
        }
        public bool Refill()
        {
            if(Data.pending!=null||Data.balance>=10)return false;
            var next=Copy();next.balance=1000;Commit(next);return true;
        }
    }
}
