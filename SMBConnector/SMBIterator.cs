using PF.Dal;
using PF.Model;
using PF.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Connectors
{
    public class SMBIterator
    {
        static Dictionary<string,string> _currentFolder = null;
        static IList<string> _files = null;
        static int _currentFileNumber = 0;
        static object SpinLock = new object();

        // Make it single tone
        // On first load - get the first page of the records that need to be scanned

        private static async Task<IEnumerable<string>> GetFolderEnumerator(string folder)
        {
            IEnumerable<string> files = await FileUtils.EnumerateFiles(folder, "*.txt|*.doc|*.docx|*.pdf|*.csv|*.html");

            return files;
        }
        private static async Task<IList<string>> GetFolderFiles(string folder)
        {
            IList<string> files = await FileUtils.GetFiles(folder, "*.txt|*.doc|*.docx|*.pdf|*.csv|*.html");

            return files;
        }

        static async void getNextFolder()
        {
            _currentFolder = await SMBDal.GetNextFolderForScanning(_currentFolder);
            _files = await GetFolderFiles(_currentFolder["path"]);
        }

        public static async Task<IteratorItem> NextItem()
        {
            string retVal;
            // Get the folder to process
            if (_currentFolder == null || _currentFileNumber == _files.Count)
            {
                //TODO: Add lock
                getNextFolder();
            }

            lock (SpinLock)
            {
                retVal = _files[_currentFileNumber];
                _currentFileNumber++;
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
