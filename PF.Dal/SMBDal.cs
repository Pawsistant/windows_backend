using PF.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading;
using PF.Utils;

namespace PF.Dal
{
    //TODO: Change from added at to sequence

    public class SMBDal : BaseClass
    {
        public SMBDal() :base()
        { }

        protected static IMongoClient _client = new MongoClient(ConfigurationManager.AppSettings["mongo_connection_string"]);
        protected static IMongoDatabase _database = _client.GetDatabase(ConfigurationManager.AppSettings["mongo_database_name"]);
        static IMongoCollection<BsonDocument> _countersCollection = _database.GetCollection<BsonDocument>("counters");

        private static List<BsonDocument> _currentPage = new List<BsonDocument>();
        private static int _currentPlaceInTheList = 0;
        static object SpinLock = new object();
        static object SpinLockScanning = new object();
        static long filterNumber = 0;
        
        static async Task<long> GetNextCounter(string counterName)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(new StringFieldDefinition<BsonDocument, BsonString>("_id"), counterName);
            var update = Builders<BsonDocument>.Update.Inc("sequence_value", 1);
            var newCounter = await _countersCollection.FindOneAndUpdateAsync(filter, update);

            return (long)newCounter["sequence_value"].AsDouble;
        }

        public static async Task<bool> BulkAddFolders(IList<string> folders)
        {
            var collection = _database.GetCollection<BsonDocument>("folders");
            List<BsonDocument> documents = new List<BsonDocument>();

            foreach (string folder in folders)
            {
                long counter = await GetNextCounter("folderid");

                documents.Add(new BsonDocument() { { "added_at", new BsonDateTime(DateTime.UtcNow) }, { "path", folder }, { "scanned", new BsonBoolean(false) }, { "counter", new BsonInt64(counter) } });
                Log.Trace("saving: " + folder + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
            }

            await collection.InsertManyAsync(documents);
            Log.Trace("Added " + documents.Count + " records to DB");
            return true;
        }

        public static async Task<string> GetNextFolderForTraverse()
        {
            Log.Trace("start spinlock wait");
            string retVal;
            lock (SpinLock)
            {
                var collection = _database.GetCollection<BsonDocument>("folders");
            
                if (_currentPlaceInTheList == _currentPage.Count)
                {
                
                        if (_currentPlaceInTheList == _currentPage.Count)
                        {
                            if (_currentPage.Count > 0)
                                filterNumber = _currentPage[_currentPlaceInTheList - 1]["counter"].AsInt64;

                            Log.Trace("GetNextFolderForTraverse, going to fetch new page after id: " + filterNumber);
                            var filter = Builders<BsonDocument>.Filter.Gt(new StringFieldDefinition<BsonDocument, BsonInt64>("counter"), new BsonInt64(filterNumber));
                            _currentPage = collection.Find(filter).SortBy(bson => bson["counter"]).Skip(0).Limit(100).ToList();
                            _currentPlaceInTheList = 0;
                        }
                
                }

                if (_currentPage.Count > 0)
                {
                    BsonDocument doc = null;
                    //return the next page for traverse with lock
                    Log.Trace("GetNextFolderForTraverse, _currentPlaceInTheList: " + _currentPlaceInTheList + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

                    doc = _currentPage[_currentPlaceInTheList];
                    _currentPlaceInTheList++;
                    retVal = doc["path"].AsString;

                    Log.Trace("GetNextFolderForTraverse, returns: " + retVal + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);
                }
                else
                    retVal = null;

                Log.Trace("end spinlock wait, Thread Id: " + Thread.CurrentThread.ManagedThreadId);
            }

            return retVal;
        }

        public static Dictionary<string,object> GetNextFolderForScanning(Dictionary<string, object> currentFolder)
        {
            var collection = _database.GetCollection<BsonDocument>("folders");
            List<BsonDocument> newFolder = null;
            Dictionary<string, object> retVal = null;
            long filterNumber = 0;

            lock (SpinLockScanning)
            {
                if (retVal == null)
                {
                    if (currentFolder != null)
                    {
                        // Mark current folder as scanned
                        var counterFilter = Builders<BsonDocument>.Filter.Eq("counter", new BsonInt64((long)currentFolder["counter"]));
                        var update = Builders<BsonDocument>.Update
                            .Set("scanned", new BsonBoolean(true));
                        var result = collection.UpdateOne(counterFilter, update);

                        filterNumber = (long)currentFolder["counter"];
                    }

                    var builder = Builders<BsonDocument>.Filter;
                    var filter = builder.Gt("counter", new BsonInt64(filterNumber)) & builder.Eq("scanned", new BsonBoolean(false));
                    newFolder = collection.Find(filter).SortBy(bson => bson["counter"]).Skip(0).Limit(1).ToList();

                    if (newFolder != null && newFolder.Count > 0)
                    {
                        retVal = newFolder[0].ToDictionary();
                    }
                }
            }

            if (retVal != null)
                Log.Trace("Getnextfolder: " + retVal["path"] + ", Thread Id: " + Thread.CurrentThread.ManagedThreadId);

            return retVal;
        }

        public static async Task<bool> AddDataObject(ScanResult dbItem)
        {
            try
            {
                var collection = _database.GetCollection<BsonDocument>("data_objects_for_matching");
                BsonDocument doc = dbItem.ToBsonDocument();
                doc.Add(new BsonElement("counter", new BsonInt64(await GetNextCounter("data_objects_for_matching_id"))));
                await collection.InsertOneAsync(doc);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add Data Object to DB: " + dbItem.DataObjectIdentifier);
            }
            return true;
        }
    }
}
