using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure
{
    public class BaseClass
    {
        protected BaseClass()
        {
            Log = LogManager.GetLogger(GetType().FullName);
        }

        protected static Logger Log { get; private set; }
    }
}
