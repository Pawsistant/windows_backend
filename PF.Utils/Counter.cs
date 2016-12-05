using PF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Utils
{
    public class Counter : BaseClass
    {
        static Dictionary<string, long> _counters = new Dictionary<string, long>();
        static object _spinLock = new object();

        public static void PrintAll()
        {
            foreach(string key in _counters.Keys)
            {
                Log.Info("Counter " + key + ": " + _counters[key].ToString());
            }
        }
        public static void Add(string counterName, int count)
        {
            if(_counters.ContainsKey(counterName))
            {
                _counters[counterName] = _counters[counterName] + count;
            }
            else
            {
                lock (_spinLock)
                {
                    if (_counters.ContainsKey(counterName))
                        _counters[counterName] = _counters[counterName] + count;
                    else
                        _counters.Add(counterName, count);
                }
            }
        }
    }
}
