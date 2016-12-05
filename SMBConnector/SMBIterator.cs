using PF.Dal;
using PF.Model;
using PF.Utils;
using PF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace PF.Connectors
{
    public class SMBIterator : BaseClass
    {
        public SMBIterator() :base()
        { }

        static Dictionary<string,object> _currentFolder = null;
        static IList<string> _files = null;
        static int _currentFileNumber = 0;
        static object SpinLock = new object();
        static int _maxRetry = int.Parse(ConfigurationManager.AppSettings["max_retries"]);
        // Make it single tone
        // On first load - get the first page of the records that need to be scanned

        private static async Task<IEnumerable<string>> GetFolderEnumerator(string folder)
        {
            IEnumerable<string> files = await FileUtils.EnumerateFiles(folder, "*.txt|*.doc|*.docx|*.pdf|*.csv|*.html|*.eml|*.xls|*.xlsx");

            return files;
        }
        private static IList<string> GetFolderFiles(string folder)
        {
            IList<string> files = new List<string>();
            try
            {
                files = FileUtils.GetFiles(folder, "*.txt|*.doc|*.docx|*.pdf|*.csv|*.html|*.eml|*.xls|*.xlsx");
            }
            catch(UnauthorizedAccessException ex)
            {
                Counter.Add("failed_to_open_folder_permissions", 1);
                Log.Error(ex, "Failed to open folder");
            }
            catch(Exception ex)
            {
                Counter.Add("failed_to_open_folder_generic", 1);
                Log.Error(ex, "Failed to open folder");
            }
            return files;
        }

        static void getNextFolder()
        {
            _currentFolder = SMBDal.GetNextFolderForScanning(_currentFolder);
            if (_currentFolder != null)
                _files = GetFolderFiles(_currentFolder["path"].ToString());
            else
                _files = null;
        }

        public static IteratorItem NextItem()
        {
            string retVal = null;

            lock (SpinLock)
            {
                int retry = 0;
                bool failed = true;

                // Retry this 3 times in case of transient failure
                while (retry < _maxRetry && failed)
                {
                    failed = false;

                    try
                    {
                        if (_currentFolder == null || _currentFileNumber == _files.Count)
                        {
                            getNextFolder();

                            if (_files != null && _files.Count == 0)
                            {
                                while (_files != null && _files.Count == 0)
                                    getNextFolder();
                            }
                            if (_files == null)
                                return null;

                            _currentFileNumber = 0;
                        }

                        retVal = _files[_currentFileNumber];
                        _currentFileNumber++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to get next folder. Current folder: " + _currentFolder + ", retry count: " + retry.ToString());
                        retry++;
                        failed = true;
                    }
                }
            }

            // Return the next item from the list
            return new IteratorItem() { DataObjectIdentifier = retVal };
        }

        public static async Task<IteratorItem> NextItemAsync()
        {
            string retVal = null;
            
            lock (SpinLock)
            {
                try
                {
                    if (_currentFolder == null || _currentFileNumber == _files.Count)
                    {
                        getNextFolder();

                        if (_files.Count == 0)
                        {
                            while (_files.Count == 0)
                                getNextFolder();
                        }

                        _currentFileNumber = 0;
                    }

                    retVal = _files[_currentFileNumber];
                    _currentFileNumber++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to get next folder. Current folder: " + _currentFolder);
                }
            }

            // Return the next item from the list
            return new IteratorItem() {DataObjectIdentifier = retVal};
        }

        public static async Task<string> GetNextFolderForTraverse()
        {
            return await SMBDal.GetNextFolderForTraverse();
        }
    }
}
