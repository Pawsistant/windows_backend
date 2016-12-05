using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PF.Utils.FileParsers;
using PF.Infrastructure;

namespace PF.Utils
{
    public class FileUtils : BaseClass
    {
        public FileUtils() : base()
        { }

        public static string ParseSync(FileInfo file)
        {
            string text = null;
            try
            {
                FileStream fileStream = file.OpenRead();

                if (file.Extension.ToLower().EndsWith("docx"))
                {
                    text = Word.Parse(fileStream);
                }
                else if (file.Extension.ToLower().EndsWith("doc"))
                {
                    text = OldWord.Parse(fileStream);
                }
                else if (file.Extension.ToLower().EndsWith("txt") || file.Extension.ToLower().EndsWith("csv") || file.Extension.ToLower().EndsWith("htm") || file.Extension.ToLower().EndsWith("html"))
                {
                    StreamReader sr = new StreamReader(fileStream, Encoding.UTF8);
                    return sr.ReadToEnd();
                }
                else if (file.Extension.ToLower().EndsWith("xlsx"))
                {
                    text = Excel.Parse(fileStream);
                }
                else if (file.Extension.ToLower().EndsWith("xls"))
                {
                    text = OldExcel.Parse(fileStream);
                }
                else if (file.Extension.ToLower().EndsWith("eml") || file.Extension.ToLower().EndsWith("msg"))
                {
                    text = Outlook.Parse(fileStream);
                }
                else if (file.Extension.ToLower().EndsWith("pptx"))
                {
                    text = PowerPoint.Parse(fileStream);
                }
                else if (file.Extension.ToLower().EndsWith("pdf"))
                {
                    text = Pdf.Parse(fileStream);
                }

                if (text != null && text != "")
                {
                    text += " " + file.FullName;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start Parser for: " + file.FullName);
                Counter.Add("failed_to_open", 1);
                return null;
            }

            if (text.Length == 0)
                Counter.Add("empty_file", 1);

            return text;
        }

        public static async Task<string> Parse(FileInfo file)
        {
            return (ParseSync(file));
        }

        public static Task<IEnumerable<String>> EnumerateFiles(String path, string filter)
        {
            return Task.Run(() => { return Directory.EnumerateFiles(path, filter, SearchOption.TopDirectoryOnly); });
        }
        public static Task<List<String>> GetFilesAsync(String path, string filter)
        {
            return Task.Run(() => {
                return GetFiles(path, filter);
            });
        }

        public static List<String> GetFiles(String path, string filter)
        {
            //TODO: Add RTF
            List<string> retVal = new List<string>();

            try
            {
                retVal = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(".doc", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".eml", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".msg", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            catch (UnauthorizedAccessException ex)
            {
                // Iterate through all the files to not get access denied
                IEnumerable<string> files = Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(".doc", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".eml", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".msg", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));

                foreach (string ff in files)
                {
                    try
                    {
                        FileInfo f = new FileInfo(ff);
                        retVal.Add(f.FullName);
                    }
                    catch (Exception e)
                    {
                        Log.Info(e, "User doesn't have permissions to the file: " + ff);
                    }
                }
            }
            return retVal;
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
            string server = ExtractComputerNameFromUrl(path);
            theNetCache.Add(new Uri(path), "Digest", theNetworkCredential);
        }

        public static Task<FileInfo> GetFileInfo(String path)
        {
            return Task.Run(() => { return new FileInfo(path); });
        }
    }
}
