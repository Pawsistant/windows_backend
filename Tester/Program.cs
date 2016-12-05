using PF.Connectors;
using PF.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
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
            repo.properties.Add("username", ConfigurationManager.AppSettings["username"]);
            repo.properties.Add("password", ConfigurationManager.AppSettings["password"]);
            repo.properties.Add("domain", ConfigurationManager.AppSettings["domain"]);
            repo.properties.Add("share_url", ConfigurationManager.AppSettings["scanning_path"]);
            repo.id = ConfigurationManager.AppSettings["repo_id"];
            Console.WriteLine("Would you like to prepaire (1) or scan (2)?");
            string val = Console.ReadLine();
            if (val == "1")
            {
                await connector.Prepare(repo);
            }
            else
            {
                bool r = await connector.Scan(repo);
            }
            Console.WriteLine("done");
            Console.ReadLine();
        }
    }
}
