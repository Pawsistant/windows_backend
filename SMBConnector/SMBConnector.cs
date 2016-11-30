using PF.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PF.Dal;
using System.Net;
using System.Threading;
using PF.Utils;
using System.Security.AccessControl;

namespace PF.Connectors
{
    public class SMBConnector : BaseClass
    {
        int _maxThreads = 10;

        public SMBConnector() :base()
        { }

        public async Task<bool> Prepare(Repository fsRepo)
        {
            // Setup credentials for file share
            FileUtils.SetupCredentials(fsRepo.properties["domain"], fsRepo.properties["username"], fsRepo.properties["password"], fsRepo.properties["share_url"]);

            // Go over all the folders and store them in the database for scanning
            await TraverseTree(fsRepo.properties["share_url"], _maxThreads);
            
            return true;
        }

        private async Task<IteratorItem> CollectFileMetadata(IteratorItem item)
        {
            FileInfo info = await FileUtils.GetFileInfo(item.DataObjectIdentifier);
            item.Metadata.Add("Created_At", new MetadataItem(info.CreationTimeUtc.ToString()));
            item.Metadata.Add("Last_Accessed_At", new MetadataItem(info.LastAccessTimeUtc.ToString()));
            item.Metadata.Add("Updated_At", new MetadataItem(info.LastWriteTimeUtc.ToString()));
            item.Metadata.Add("Size", new MetadataItem(info.Length.ToString()));
            FileSecurity fs = info.GetAccessControl(AccessControlSections.Access);

            foreach (FileSystemAccessRule ar in fs.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount)))
            {
                item.Metadata.Add("Access_Rights", new MetadataItem(ar.IdentityReference.Value, ar.ToString()));
            }

            return item;
        }


        public async Task<ScanResult> ScanNext()
        {
            IteratorItem item = await SMBIterator.NextItem();

            string txt = await FileUtils.Parse(item.DataObjectIdentifier);

            if (txt != null && txt.Length > 0)
            {
                //Do NER
                Dictionary<string, List<string>> identifiers = await NER.Parse(txt);

                if (identifiers != null && identifiers.Count > 0)
                {
                    // Get metadata
                    item = await CollectFileMetadata(item);

                    // Write into Mongo
                    await SMBDal.AddDataObject(item);
                }
            }

            return null;
        }

      

        private async void ProcessDir(string dir)
        {
            // Process sub dirs
            string[] directories = Directory.GetDirectories(dir);

            //TODO: If this fails - enumerate

            // Store all the sub dirs in the data store wiht flag - need to traverse
            await SMBDal.BulkAddFolders(directories.ToList());
        }

        private async Task<bool> TraverseTree(string root, int maxTasks)
        {
            string currentDir = root;

            using (var throttler = new SemaphoreSlim(maxTasks))
            {
                var postTaskTasks = new List<Task>();

                while (currentDir != null)
                {
                    if (currentDir == root)
                    {
                        // First time get the directories syncroniously to populate the DB with directories to run on
                        IEnumerable<string> directories = Directory.EnumerateDirectories(currentDir);
                        await SMBDal.BulkAddFolders(directories.ToList());
                    }
                    else
                    {
                        postTaskTasks.Add(Task.Factory.StartNew(() => ProcessDir(currentDir)).ContinueWith(tsk => throttler.Release()));
                    }

                    // Don't continue if there already are maxTasks running
                    throttler.Wait();

                    // Fetch the next directory to traverse and do it again
                    currentDir = await SMBIterator.GetNextFolderForTraverse();

                    if (currentDir == null)
                    {
                        Task.WaitAll(postTaskTasks.ToArray());
                        currentDir = await SMBIterator.GetNextFolderForTraverse();
                    }
                }
            }

            return true;
        }
    }
}
