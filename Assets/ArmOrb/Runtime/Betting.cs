using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ArmOrb
{
    public sealed class OddsQuote
    {
        public int[] Hundredths;
        public int Samples,Draws;
        public static OddsQuote Calculate(FightDefinition fight,int[] wins,int draws)
        {
            int count=fight.Count,total=draws;foreach(int n in wins)total+=n;
            var pooled=new double[count];
            for(int i=0;i<count;i++)
            {
                double sum=0;int members=0;
                for(int j=0;j<count;j++)if(fight.weapons[i]==fight.weapons[j]&&fight.health[i]==fight.health[j]){sum+=wins[j];members++;}
                pooled[i]=sum/members;
            }
            var result=new OddsQuote{Hundredths=new int[count],Samples=total,Draws=draws};
            double denominator=total-draws+0.5*count;
            for(int i=0;i<count;i++)result.Hundredths[i]=Math.Max(100,(int)Math.Floor(100*denominator/(pooled[i]+0.5)+1e-9));
            return result;
        }
    }
    public sealed class OddsCalculator : IDisposable
    {
        readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        int completed;
        public int Completed=>Volatile.Read(ref completed);
        public Task<OddsQuote> Work { get; }
        public OddsCalculator(SimulationFactory factory,FightDefinition definition,int minimum=16,int maximum=64,double seconds=10)
        {
            var fight=definition.Copy();var token=cancellation.Token;
            // The actual fight seed is not used by the estimator, nor is its outcome inspected.
            int estimatorSeed=Guid.NewGuid().GetHashCode();
            Work=Task.Run(()=>
            {
                var watch=Stopwatch.StartNew();var random=new Random(estimatorSeed);var wins=new int[fight.Count];int draws=0;
                while(completed<maximum)
                {
                    token.ThrowIfCancellationRequested();
                    int seed;do{seed=random.Next(1,int.MaxValue);}while(seed==fight.seed);
                    var sim=factory.Create(fight,seed);
                    // Danger guarantees completion for valid betting HP (<=300), including shields.
                    int limit=factory.Frequency*140;
                    for(int tick=0;tick<limit&&sim.Result==MatchResult.InProgress;tick++)
                    { if((tick&127)==0)token.ThrowIfCancellationRequested();sim.Step(1f/factory.Frequency); }
                    if(sim.Result==MatchResult.InProgress)throw new InvalidOperationException("A sampled fight did not finish.");
                    if(sim.WinnerIndex<0)draws++;else wins[sim.WinnerIndex]++;
                    Interlocked.Increment(ref completed);
                    if(completed>=minimum&&watch.Elapsed.TotalSeconds>=seconds)break;
                }
                return OddsQuote.Calculate(fight,wins,draws);
            },token);
        }
        public void Dispose(){cancellation.Cancel();}
    }
}
