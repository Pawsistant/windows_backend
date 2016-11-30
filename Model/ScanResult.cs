using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Model
{
    public class ScanResult
    {
        public ScanResult()
        {
            Identifiers = new Dictionary<string, List<string>>();
            Metadata = new List<string>();
        }

        public string DataObjectIdentifier { get; set; }
        public Dictionary<string, List<string>> Identifiers { get; set; }
        public List<string> Metadata { get; set; }
    }
}
