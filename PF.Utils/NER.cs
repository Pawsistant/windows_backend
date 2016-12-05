using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResourceInterogator;

namespace PF.Utils
{
    public class NER
    {
        public static async Task<Dictionary<String, List<String>>> Parse(string text, string source)
        {
            Dictionary<String, List<String>> retVal = await NERAccessor.Parse(text, source);

            return retVal;
        }
    }
}
