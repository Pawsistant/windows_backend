using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PF.Utils
{
    public class FileUtils
    {
        public static async Task<string> Parse(string path)
        {
            //TODO: Implement
            return "";
        }

        public static Task<IEnumerable<String>> EnumerateFiles(String path, string filter)
        {
            return Task.Run(() => { return Directory.EnumerateFiles(path, filter, SearchOption.TopDirectoryOnly); });
        }
        public static Task<List<String>> GetFiles(String path, string filter)
        {
            return Task.Run(() => { return Directory.GetFiles(path, filter, SearchOption.TopDirectoryOnly).ToList(); });
        }

        private static string ExtractComputerNameFromUrl(string url)
        {
            url = url.Replace("\\\\", "");
            return url.Split('\\')[0];
        }

        public static void SetupCredentials(string domainName, string username, string password, string path)
        {
            NetworkCredential theNetworkCredential = new NetworkCredential(domainName + "\\" + username, password);
            CredentialCache theNetCache = new CredentialCache();
            theNetCache.Add(new Uri(ExtractComputerNameFromUrl(path)), "Basic", theNetworkCredential);
        }

        public static Task<FileInfo> GetFileInfo(String path)
        {
            return Task.Run(() => { return new FileInfo(path); });
        }
    }
}
