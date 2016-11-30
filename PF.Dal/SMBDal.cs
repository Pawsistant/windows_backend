using PF.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MongoDB.Bson;
using MongoDB.Driver;

namespace PF.Dal
{
    //TODO: Change from added at to sequence

    public class SMBDal
    {
        protected static IMongoClient _client = new MongoClient("MongoConnetionString");
        protected static IMongoDatabase _database = _client.GetDatabase("Prifender");
        private static List<BsonDocument> _currentPage = new List<BsonDocument>();
        private static int _currentPlaceInTheList = 0;
        static object SpinLock = new object();
        static object SpinLockScanning = new object();

        public static async Task<bool> BulkAddFolders(IList<string> folders)
        {
            var collection = _database.GetCollection<BsonDocument>("folders");
            List<Dictionary<string, string>> documents = new List<Dictionary<string, string>>();

            foreach (string folder in folders)
            {
                documents.Add(new Dictionary<string, string>() { { "added_at", DateTime.UtcNow.ToString() }, { "path", folder } });
            }

            await collection.InsertManyAsync(documents.Select(d => new BsonDocument(d)));

            return true;
        }

        public static async Task<string> GetNextFolderForTraverse()
        {
            var collection = _database.GetCollection<BsonDocument>("folders");
            
            if (_currentPlaceInTheList == _currentPage.Count)
            {
                lock (SpinLock)
                {
                    if (_currentPlaceInTheList == _currentPage.Count)
                    {
                        DateTime filterDate;

                        if (_currentPage.Count == 0)
                        {
                            // Get the first page
                            filterDate = DateTime.UtcNow.Subtract(new TimeSpan(5, 0, 0, 0));
                        }
                        else
                        {
                            // Get next page
                            filterDate = _currentPage[_currentPlaceInTheList - 1]["added_at"].ToUniversalTime();
                        }

                        var filter = Builders<BsonDocument>.Filter.Gt(new StringFieldDefinition<BsonDocument, BsonDateTime>("added_at"), new BsonDateTime(filterDate));
                        _currentPage = collection.Find(filter).SortBy(bson => bson["added_at"]).Skip(0).Limit(100).ToList();
                        _currentPlaceInTheList = 0;
                    }
                }
            }

            //return the next page for traverse with lock
            BsonDocument doc = _currentPage[_currentPlaceInTheList];
            _currentPlaceInTheList++;

            return doc["path"].AsString;
        }

        public static async Task<Dictionary<string,string>> GetNextFolderForScanning(Dictionary<string, string> currentFolder)
        {
            var collection = _database.GetCollection<BsonDocument>("folders");
            List<BsonDocument> newFolder = null;
            Dictionary<string, string> retVal = null;

            lock (SpinLockScanning)
            {
                if (retVal == null)
                {
                    if (currentFolder == null)
                    {
                        var filter = new BsonDocument();
                        newFolder = collection.Find(filter).SortBy(bson => bson["added_at"]).Skip(0).Limit(1).ToList();
                    }
                    else
                    {
                        var filter = Builders<BsonDocument>.Filter.Gt(new StringFieldDefinition<BsonDocument, BsonDateTime>("added_at"), new BsonDateTime(DateTime.Parse(currentFolder["added_at"])));
                        newFolder = collection.Find(filter).SortBy(bson => bson["added_at"]).Skip(0).Limit(1).ToList();
                    }
                        

                    if (newFolder != null && newFolder.Count > 0)
                    {
                        retVal = new Dictionary<string, string>();
                        retVal.Add("added_at", newFolder[0]["added_at"].AsString);
                        retVal.Add("path", newFolder[0]["path"].AsString);
                    }
                }
            }

            return retVal;
        }
        static BsonDocument ParseIteratorItem(IteratorItem item)
        {
            //TODO: implement
            return new BsonDocument();
        }
        public static async Task<bool> AddDataObject(IteratorItem dbItem)
        {
            var collection = _database.GetCollection<BsonDocument>("data_objects_for_matching");
            BsonDocument doc = ParseIteratorItem(dbItem);
            await collection.InsertOneAsync(doc);
            return true;
        }
    }
}
