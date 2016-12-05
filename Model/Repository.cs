using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Model
{
    public class Repository
    {
        public Repository()
        {
            properties = new Dictionary<string, string>();
        }
        public string id { get; set; }
        public string name { get; set; }
        public string type_id { get; set; }
        public string description { get; set; }
        public string version { get; set; }
        public bool show_data { get; set; }
        public bool active { get; set; }
        public DateTime last_sync { get; set; }
        public bool deleted { get; set; }
        public Dictionary<string,string> properties { get; set; }
    }
}
