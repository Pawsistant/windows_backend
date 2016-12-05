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
using PF.Infrastructure;

namespace PF.Connectors
{
    public class SMBConnector : BaseClass
    {
        int _maxThreads = int.Parse(ConfigurationManager.AppSettings["max_preparation_threads"]);
        int _maxFilesScanCocur = int.Parse(ConfigurationManager.AppSettings["max_file_scan_threads"]);
        int _maxFileSize = int.Parse(ConfigurationManager.AppSettings["max_file_size"]);
        int _taskTimeout = int.Parse(ConfigurationManager.AppSettings["task_timeout"]);
        List<PFTask> _tasksToRemove = new List<PFTask>();

        public SMBConnector() :base()
        { }

        static void PrepCredentials(Repository fsRepo)
        {
            try
            {
                if (Boolean.Parse(ConfigurationManager.AppSettings["with_credentials"]))
                {
                    // Setup credentials for file share
                    FileUtils.SetupCredentials(fsRepo.properties["domain"], fsRepo.properties["username"], fsRepo.properties["password"], fsRepo.properties["share_url"]);
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Failed to prepaire credentials for file scan");
                throw;
            }
        }
        public async Task<bool> Prepare(Repository fsRepo)
        {
            try
            {
                PrepCredentials(fsRepo);

                // Go over all the folders and store them in the database for scanning
                await TraverseTree(fsRepo.properties["share_url"], _maxThreads);
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Failed to prepaire folders");
            }
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

        public async Task<ScanResult> ScanNext(Repository repo)
        {
            IteratorItem item = await SMBIterator.NextItemAsync();
            return await ScanNext(repo, item);
        }
        public async Task<ScanResult> ScanNext(Repository repo, IteratorItem item)
        {
            ScanResult retVal = null;
            string extention = "";
            Log.Trace("Started processing: " + item.DataObjectIdentifier + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

            try
            { 
                if (item == null)
                    return null;

                Dictionary<string, List<string>> identifiers = null;
                FileInfo file = new FileInfo(item.DataObjectIdentifier);
                extention = file.Extension;

                // Don't scan files that are too big
                if (file.Length > _maxFileSize * 1000000)
                {
                    Log.Warn("File too big:" + file.FullName + ", Size:" + file.Length);
                    Counter.Add("file_too_big", 1);
                }
                else
                {
                    // Get text of the file
                    string txt = await FileUtils.Parse(file);

                    if (txt != null && txt.Length > 0)
                    {
                        //Do NER
                        identifiers = await NER.Parse(txt, file.FullName);

                        if (identifiers != null && identifiers.Count > 0)
                        {
                            Counter.Add("found_pi", 1);

                            // Get metadata
                            item = await CollectFileMetadata(item, file);

                            retVal = new ScanResult() { DataObjectIdentifier = item.DataObjectIdentifier, Identifiers = identifiers, Metadata = item.Metadata, RepositoryId = repo.id };

                            // Store results
                            await SMBDal.AddDataObjectForMatching(retVal);
                        }
                        else
                            Counter.Add("didnot_find_pi", 1);
                    }
                    Log.Info("Processed file:" + file.FullName + ", Identifiers: " + (identifiers == null ? "0" : identifiers.Count.ToString()));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process file: " + item.DataObjectIdentifier);
                return null;
            }

            Log.Trace("Finished processing: " + retVal.DataObjectIdentifier + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
            Counter.Add(extention, 1);

            return retVal;
        }

        public async Task<bool> Scan(Repository repo)
        {
            bool scanDone = false;
            var postTaskTasks = new List<PFTask>();
            object SpinLock = new object();

            PrepCredentials(repo);
            using (var throttler = new SemaphoreSlim(_maxFilesScanCocur))
            {
                // Scan files
                while (!scanDone)
                {
                    bool semValue = await throttler.WaitAsync(_taskTimeout);
                    if (!semValue)
                    {
                        // See what tasks are hanging for more than 30 seconds and kill them
                        foreach (PFTask t in postTaskTasks)
                        {
                            if (t.StartTime.Subtract(DateTime.UtcNow).Seconds > _taskTimeout)
                            {
                                t.TokenSource.Cancel();
                            }
                        }
                    }

                    IteratorItem item = null;
                    lock (SpinLock)
                    {
                        // Get next item for iteration
                        item = SMBIterator.NextItem();
                    }

                    if (item != null)
                    {
                        // process the next item
                        PFTask tsk = new PFTask();
                        tsk.Task = Task.Run<ScanResult>(() => ScanNext(repo, item), tsk.CancellationToken).ContinueWith(t => release(throttler));
                        postTaskTasks.Add(tsk);
                    }
                    else
                        scanDone = true;

                    // Clean the completed tasks from the wait array
                    foreach (PFTask t in postTaskTasks)
                    {
                        if (t.Task.IsCompleted || t.Task.IsCanceled || t.Task.IsFaulted)
                        {
                            _tasksToRemove.Add(t);
                        }
                    }
                    foreach (PFTask t in _tasksToRemove)
                    {
                        postTaskTasks.Remove(t);
                        t.Task.Dispose();
                    }
                    _tasksToRemove = new List<PFTask>();

                    if (scanDone)
                    {
                        // Wait for all the tasks to finish before exiting
                        Task.WaitAll(postTaskTasks.Select(i => i.Task).ToArray(),30000);
                    }
                }
            }
            Counter.PrintAll();
            return true;
        }
        private async Task<bool> ProcessDir(string dir)
        {
            Log.Trace("Started processing folder: " + dir + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

            try
            {
                // Process sub dirs
                IEnumerable<string> directories = Directory.EnumerateDirectories(dir);

                // Store all the sub dirs in the data store wiht flag - need to traverse
                if (directories.Count() > 0)
                {
                    Log.Trace("Sent to BulkAdd " + directories.Count() + " records" + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

                    List<string> lst = new List<string>();

                    foreach (string d in directories)
                    {
                        try
                        {
                            DirectoryInfo di = new DirectoryInfo(d);
                            lst.Add(di.FullName);
                            Log.Trace("Added folder: " + di.FullName + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                        }
                        catch (Exception ex)
                        {
                            Log.Info(ex, "User doesn't have permissions to folder: " + d);
                        }
                    }

                    await SMBDal.BulkAddFolders(lst);
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
                var postTaskTasks = new List<PFTask>();

                while (currentDir != null)
                {
                    if (currentDir == root)
                    {
                        // First time get the directories syncroniously to populate the DB with directories to run on
                        IEnumerable<string> directories = Directory.EnumerateDirectories(currentDir);
                        List<string> lst = new List<string>();

                        foreach(string dir in directories)
                        {
                            try
                            { 
                                DirectoryInfo di = new DirectoryInfo(dir);
                                lst.Add(di.FullName);
                                Log.Trace("Added folder: " + di.FullName + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                            }
                            catch(Exception ex)
                            {
                                Log.Info(ex, "User doesn't have permissions to folder: " + dir);
                            }
                        }
                        //await SMBDal.BulkAddFolders(directories.ToList());
                        await SMBDal.BulkAddFolders(lst);
                    }
                    else
                    {
                        Log.Trace("Before wait: " + currentDir + ", wait: " + throttler.CurrentCount + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                        await throttler.WaitAsync();
                        Log.Trace("After wait: " + currentDir + ", wait: " + throttler.CurrentCount + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                        string value = currentDir.ToString();
                        PFTask t = new PFTask();
                        t.Task = Task.Run(() => ProcessDir(value), t.CancellationToken).ContinueWith(tsk => release(throttler));
                        postTaskTasks.Add(t);
                        Log.Trace("After add task: " + currentDir + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

                    }

                    // Fetch the next directory to traverse and do it again
                    currentDir = await SMBIterator.GetNextFolderForTraverse();
                    Log.Trace("GetNextFolderForTraverse returned: " + currentDir + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

                    // Clean the completed tasks from the array
                    foreach (PFTask t in postTaskTasks)
                    {
                        if (t.Task.IsCompleted || t.Task.IsCanceled || t.Task.IsFaulted)
                        {
                            _tasksToRemove.Add(t);
                        }
                    }
                    foreach (PFTask t in _tasksToRemove)
                    {
                        postTaskTasks.Remove(t);
                    }
                    _tasksToRemove = new List<PFTask>();

                    if (currentDir == null)
                    {
                        Log.Trace("WaitAll" + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                        Task.WaitAll(postTaskTasks.Select(t=>t.Task).ToArray());
                        currentDir = await SMBIterator.GetNextFolderForTraverse();
                        Log.Trace("Got directory after wait all: " + currentDir + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                    }
                }
            }

            return true;
        }
    }
}
