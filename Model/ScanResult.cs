using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Model
{
    public class ScanResult
    {
        public ScanResult()
        {
            Identifiers = new Dictionary<string, List<string>>();
            Metadata = new List<MetadataItem>();
        }

        public string DataObjectIdentifier { get; set; }
        public Dictionary<string, List<string>> Identifiers { get; set; }
        public List<MetadataItem> Metadata { get; set; }
        public string RepositoryId { get; set; }

        public BsonDocument ToBsonDocument()
        {
            BsonDocument retVal = new BsonDocument();
            BsonArray bsonMetadata = new BsonArray();
            BsonArray bsonIdentifiers = new BsonArray();
            retVal.Add(new BsonElement("repositoryId", new BsonObjectId(new ObjectId(RepositoryId))));
            retVal.Add(new BsonElement("data_object_identifier", new BsonString(DataObjectIdentifier)));
            if (Identifiers != null)
            {
                foreach (string key in Identifiers.Keys)
                {
                    BsonArray arr = new BsonArray();
                    foreach (string element in Identifiers[key])
                    {
                        arr.Add(new BsonString(element));
                    }
                    BsonDocument el = new BsonDocument() { { "type", key }, { "values", arr } };
                    bsonIdentifiers.Add(el);
                }
            }

            foreach (MetadataItem item in Metadata)
            {
                BsonDocument el = new BsonDocument() { { "type", item.Name }, { "value", item.Value }, { "additional_data", item.AdditionalData } };
                bsonMetadata.Add(el);
            }

            //Add the arrays to the doc
            retVal.Add(new BsonElement("metadata", bsonMetadata));
            retVal.Add(new BsonElement("identifiers", bsonIdentifiers));

            return retVal;
        }
    }
}
