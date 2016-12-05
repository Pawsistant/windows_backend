using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PF.Infrastructure;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace PF.Utils.FileParsers
{
    public class Pdf : BaseClass
    {
        public static string Parse(FileStream file)
        {
            string retVal = "";
            var sb = new StringBuilder();

            try
            {
                var reader = new PdfReader(file);
                var numberOfPages = reader.NumberOfPages;

                for (var currentPageIndex = 1; currentPageIndex <= numberOfPages; currentPageIndex++)
                {
                    sb.Append(PdfTextExtractor.GetTextFromPage(reader, currentPageIndex));
                }
                retVal = sb.ToString();
            }
            catch (Exception ex)
            {
                ParsersInfra.RecordParsingFailure(Log, ex, file);
            }
            return retVal;
        }
    }
}
