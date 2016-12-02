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
using System.Configuration;

using PF.Utils;
using System.Security.AccessControl;

namespace PF.Connectors
{
    public class SMBConnector : BaseClass
    {
        int _maxThreads = int.Parse(ConfigurationManager.AppSettings["max_preparation_threads"]);
        int _maxFilesScanCocur = int.Parse(ConfigurationManager.AppSettings["max_file_scan_threads"]);
        int _maxFileSize = int.Parse(ConfigurationManager.AppSettings["max_file_size"]);

        public SMBConnector() :base()
        { }

        static void PrepCredentials(Repository fsRepo)
        {
            if (Boolean.Parse(ConfigurationManager.AppSettings["with_credentials"]))
            {
                // Setup credentials for file share
                FileUtils.SetupCredentials(fsRepo.properties["domain"], fsRepo.properties["username"], fsRepo.properties["password"], fsRepo.properties["share_url"]);
            }
        }
        public async Task<bool> Prepare(Repository fsRepo)
        {
            PrepCredentials(fsRepo);

            // Go over all the folders and store them in the database for scanning
            await TraverseTree(fsRepo.properties["share_url"], _maxThreads);
            
            return true;
        }

        private async Task<IteratorItem> CollectFileMetadata(IteratorItem item, FileInfo info)
        {
            try
            { 
                item.Metadata.Add(new MetadataItem("Created_At", info.CreationTimeUtc.ToString()));
                item.Metadata.Add(new MetadataItem("Last_Accessed_At", info.LastAccessTimeUtc.ToString()));
                item.Metadata.Add(new MetadataItem("Updated_At", info.LastWriteTimeUtc.ToString()));
                item.Metadata.Add(new MetadataItem("Size", info.Length.ToString()));
                FileSecurity fs = info.GetAccessControl(AccessControlSections.Access);

                foreach (FileSystemAccessRule ar in fs.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount)))
                {
                    item.Metadata.Add(new MetadataItem("Access_Rights", ar.IdentityReference.Value, ar.FileSystemRights.ToString()));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to collect metadata: " + info.FullName);
            }
            return item;
        }

        public async Task<ScanResult> ScanNext()
        {
            IteratorItem item = await SMBIterator.NextItemAsync();
            return await ScanNext(item);
        }
        public async Task<ScanResult> ScanNext(IteratorItem item)
        {
            ScanResult retVal = null;
            Log.Trace("Started processing: " + item.DataObjectIdentifier + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
            try
            { 
                if (item == null)
                    return null;

                Dictionary<string, List<string>> identifiers = null;

                FileInfo file = new FileInfo(item.DataObjectIdentifier);

                if (file.Length > _maxFileSize * 1000000)
                {
                    Log.Warn("File too big:" + file.FullName + ", Size:" + file.Length);
                }
                else
                {
                    // Get text of the file
                    string txt = await FileUtils.Parse(file);

                    if (txt != null && txt.Length > 0)
                    {
                        //Do NER
                        identifiers = await NER.Parse(txt);

                        if (identifiers != null && identifiers.Count > 0)
                        {
                            // Get metadata
                            item = await CollectFileMetadata(item, file);

                            retVal = new ScanResult() { DataObjectIdentifier = item.DataObjectIdentifier, Identifiers = identifiers, Metadata = item.Metadata };

                            // Store results
                            await SMBDal.AddDataObject(retVal);
                        }
                    }
                    Log.Info("Processed file:" + file.FullName + ", Identifiers: " + (identifiers == null ? "0" : identifiers.Count.ToString()));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process file: " + item.DataObjectIdentifier);
            }

            Log.Trace("Finished processing: " + retVal.DataObjectIdentifier + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
            return retVal;
        }

        public async Task<bool> Scan(Repository fsRepo)
        {
            bool scanDone = false;
            var postTaskTasks = new List<Task>();
            object SpinLock = new object();

            PrepCredentials(fsRepo);
            using (var throttler = new SemaphoreSlim(_maxFilesScanCocur))
            {
                // Scan files
                while (!scanDone)
                {
                    await throttler.WaitAsync();
                    IteratorItem item = null;
                    lock (SpinLock)
                    {
                        item = SMBIterator.NextItem();
                    }
                    if (item != null)
                        postTaskTasks.Add(Task.Run<ScanResult>(() => ScanNext(item)).ContinueWith(tsk => release(throttler)));
                    else
                        scanDone = true;
                    /*  List<Task> toRemove = new List<Task>();

                      // Clean the completed tasks from the array
                      foreach (Task<ScanResult> t in postTaskTasks)
                      {
                          toRemove = new List<Task>();
                          if (t.IsCompleted || t.IsCanceled || t.IsFaulted)
                          {
                              if (t.IsCompleted && t.Result == null)
                                  scanDone = true;

                              toRemove.Add(t);
                          }
                      }
                      foreach (Task t in toRemove)
                      {
                          postTaskTasks.Remove(t);
                      }*/

                    if (scanDone)
                    {
                        Task.WaitAll(postTaskTasks.ToArray());
                    }
                }
            }

            return true;
        }
        private async Task<bool> ProcessDir(string dir)
        {
            Log.Trace("Started processing folder: " + dir + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

            try
            {
                // Process sub dirs
                string[] directories = Directory.GetDirectories(dir);

                //TODO: If this fails - enumerate

                // Store all the sub dirs in the data store wiht flag - need to traverse
                if (directories.Count() > 0)
                {
                    Log.Trace("Sent to BulkAdd " + directories.Count() + " records" + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                    await SMBDal.BulkAddFolders(directories.ToList());
                    Log.Trace("Line after BulkAdd of " + directories.Count() + " records" + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Failed to process folder: " + dir);
            }
            return true;
        }
        private static void release(SemaphoreSlim sem)
        {
            Log.Trace("Sem released" + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
            sem.Release();
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
                        Log.Trace("Before wait: " + currentDir + ", wait: " + throttler.CurrentCount + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                        await throttler.WaitAsync();
                        Log.Trace("After wait: " + currentDir + ", wait: " + throttler.CurrentCount + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                        string value = currentDir.ToString();
                        postTaskTasks.Add(Task.Run(() => ProcessDir(value)).ContinueWith(tsk => release(throttler)));
                        Log.Trace("After add task: " + currentDir + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

                    }

                    // Fetch the next directory to traverse and do it again
                    currentDir = await SMBIterator.GetNextFolderForTraverse();
                    Log.Trace("GetNextFolderForTraverse returned: " + currentDir + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

                    // Clean the completed tasks from the array
                    List<Task> toRemove = new List<Task>();
                    foreach (Task t in postTaskTasks)
                    {
                        toRemove = new List<Task>();
                        if (t.IsCompleted || t.IsCanceled || t.IsFaulted)
                        {
                            toRemove.Add(t);
                        }
                    }
                    foreach (Task t in toRemove)
                    {
                        postTaskTasks.Remove(t);
                    }

                    if (currentDir == null)
                    {
                        Log.Trace("WaitAll" + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                        Task.WaitAll(postTaskTasks.ToArray());
                        currentDir = await SMBIterator.GetNextFolderForTraverse();
                        Log.Trace("Got directory after wait all: " + currentDir + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                    }
                }
            }

            return true;
        }
    }
}
