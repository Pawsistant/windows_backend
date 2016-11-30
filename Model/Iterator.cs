using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Model
{
    public class MetadataItem
    {
        public MetadataItem(string value)
        {
            Value = value;
        }
        public MetadataItem(string value, string additionalData)
        {
            Value = value;
            AdditionalData = additionalData;
        }
        public string Value { get; set; }
        public string AdditionalData { get; set; }
    }
    public class IteratorItem
    {
        public IteratorItem()
        {
            Metadata = new Dictionary<string, MetadataItem>();
            ItemId = Guid.NewGuid();
        }
        public Guid ItemId { get; set; }
        public string DataObjectIdentifier { get; set; }
        public Dictionary<string, MetadataItem> Metadata {get;set;}

       
    }
}
