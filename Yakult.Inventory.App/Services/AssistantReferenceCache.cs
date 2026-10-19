using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Bounded session-scoped reference data only. Business quantities never belong in this cache.</summary>
    internal sealed class AssistantReferenceCache<T>
    {
        private sealed class Entry {internal T Value;internal DateTime Expires;internal long Used;}
        private readonly Dictionary<string,Entry> _entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _gate=new SemaphoreSlim(1,1);
        private readonly int _capacity;private readonly string _metric;private readonly Func<DateTime> _clock;
        private string _scope;private long _usage;
        internal AssistantReferenceCache(int capacity,string metric,Func<DateTime> clock=null)
        {_capacity=capacity;_metric=metric;_clock=clock ?? (()=>DateTime.UtcNow);}
        internal async Task<T> GetAsync(string scope,string key,Func<CancellationToken,Task<T>> load,CancellationToken ct)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                if(_scope!=scope) {_entries.Clear();_scope=scope;}
                DateTime now=_clock();Entry entry;
                if(_entries.TryGetValue(key,out entry) && now<entry.Expires)
                {entry.Used=++_usage;AssistantPerformance.Count(_metric+"-hit");return entry.Value;}
                _entries.Remove(key);AssistantPerformance.Count(_metric+"-miss");
                T value=await load(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();now=_clock();
                foreach(string expired in _entries.Where(p=>now>=p.Value.Expires).Select(p=>p.Key).ToArray()) _entries.Remove(expired);
                if(_entries.Count>=_capacity) _entries.Remove(_entries.OrderBy(p=>p.Value.Used).First().Key);
                _entries[key]=new Entry {Value=value,Expires=now.AddMinutes(5),Used=++_usage};
                return value;
            }
            finally {_gate.Release();}
        }
    }
}
