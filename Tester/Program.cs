using PF.Connectors;
using PF.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tester
{
    class Program
    {
        public static object AsyncContext { get; private set; }

        static void Main(string[] args)
        {
            MainAsync().Wait();
        }

        static async Task MainAsync()
        {
            SMBConnector connector = new SMBConnector();
            Repository repo = new Repository();
            repo.properties.Add("username", "");
            repo.properties.Add("password", "");
            repo.properties.Add("domain", "");
            repo.properties.Add("share_url", "");
            await connector.Prepare(repo);
        }
    }
}
