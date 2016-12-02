using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Model
{
    public class MetadataItem
    {
        public MetadataItem(string name, string value)
        {
            Name = name;
            Value = value;
            if (Value == null)
                Value = "";
            AdditionalData = "";
        }
        public MetadataItem(string name, string value, string additionalData)
        {
            Name = name;
            Value = value;
            if (Value == null)
                Value = "";
            AdditionalData = additionalData;
            if (AdditionalData == null)
                AdditionalData = "";
        }
        public string Name { get; set; }
        public string Value { get; set; }
        public string AdditionalData { get; set; }
    }
    public class IteratorItem
    {
        public IteratorItem()
        {
            Metadata = new List<MetadataItem>();
            ItemId = Guid.NewGuid();
        }
        public Guid ItemId { get; set; }
        public string DataObjectIdentifier { get; set; }
        public List<MetadataItem> Metadata {get;set;}

       
    }
}
