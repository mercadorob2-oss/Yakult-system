using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services
{
    internal sealed class AssistantPerformanceReport
    {
        internal long TotalMs;
        internal IReadOnlyDictionary<string,long> Stages;
        internal IReadOnlyDictionary<string,int> Counters;
        internal string LogLine(AssistantOutcome? outcome)
        {
            // Fixed stage names and durations only. Never log questions, reference names, rows or credentials.
            return "[Yakumi timing] outcome="+(outcome?.ToString() ?? "Interrupted")+" total_ms="+TotalMs+" "+
                string.Join(" ",Stages.OrderBy(p=>p.Key).Select(p=>p.Key+"_ms="+p.Value))+" "+
                string.Join(" ",Counters.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value));
        }
    }
    internal static class AssistantPerformance
    {
        private sealed class Turn
        {
            internal readonly Stopwatch Clock=Stopwatch.StartNew();
            internal readonly Dictionary<string,long> Stages=new Dictionary<string,long>();
            internal readonly Dictionary<string,int> Counters=new Dictionary<string,int>();
        }
        private sealed class Stage : IDisposable
        {
            private readonly Turn _turn;private readonly string _name;private readonly Stopwatch _clock=Stopwatch.StartNew();
            internal Stage(Turn turn,string name) {_turn=turn;_name=name;}
            public void Dispose()
            {
                if(_turn==null) return;
                lock(_turn) {long elapsed;_turn.Stages.TryGetValue(_name,out elapsed);_turn.Stages[_name]=elapsed+_clock.ElapsedMilliseconds;}
            }
        }
        private static readonly AsyncLocal<Turn> Current=new AsyncLocal<Turn>();
        internal static IDisposable Measure(string name) {return new Stage(Current.Value,name);}
        internal static async Task<T> TimeAsync<T>(string name,Func<Task<T>> action)
        {using(Measure(name)) return await action().ConfigureAwait(false);}
        internal static void Count(string name)
        {var turn=Current.Value;if(turn!=null) lock(turn) {int count;turn.Counters.TryGetValue(name,out count);turn.Counters[name]=count+1;}}
        internal static void FirstText()
        {var turn=Current.Value;if(turn!=null) lock(turn) if(!turn.Stages.ContainsKey("first-text")) turn.Stages["first-text"]=turn.Clock.ElapsedMilliseconds;}
        internal static async Task<AssistantResult> TrackAsync(Func<Task<AssistantResult>> action)
        {
            if(Current.Value!=null) return await action();
            var turn=new Turn();Current.Value=turn;AssistantResult result=null;
            try {result=await action();return result;}
            finally
            {
                AssistantPerformanceReport report;
                lock(turn) report=new AssistantPerformanceReport {TotalMs=turn.Clock.ElapsedMilliseconds,
                    Stages=new ReadOnlyDictionary<string,long>(new Dictionary<string,long>(turn.Stages)),
                    Counters=new ReadOnlyDictionary<string,int>(new Dictionary<string,int>(turn.Counters))};
                if(result!=null) result.Performance=report;
                Current.Value=null;
                try {Logger.LogInfo(report.LogLine(result?.Outcome));} catch { }
            }
        }
    }
}
