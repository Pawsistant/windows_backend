using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Utils.FileParsers
{
    public class ParsersInfra
    {
        public static void RecordParsingFailure(Logger log, Exception ex, FileStream file)
        {
            log.Error(ex, "Failed to parse: " + file.Name);
            Counter.Add("total_failed_to_parse", 1);
            var arr = file.Name.Split('.');
            Counter.Add("failed_to_parse_" + arr[arr.Length - 1], 1);
        }
    }
}
