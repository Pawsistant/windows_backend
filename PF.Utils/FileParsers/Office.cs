﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using System.IO;
using DocumentFormat.OpenXml.Spreadsheet;
using MsgReader;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using NPOI.HSSF.UserModel;
using NPOI.DDF;
using NPOI.WP;
using NPOI.WP.UserModel;
using NPOI.XWPF.UserModel;
using NPOI.HSSF.Extractor;
using PF.Infrastructure;
using System.Runtime.Serialization.Formatters.Binary;
using System.Configuration;
using Spire.Doc;
using Spire.Doc.Documents;

namespace PF.Utils.FileParsers
{
    public class Word : BaseClass
    {
        public Word() :base()
        { }

        public static string Parse(FileStream file)
        {
            
            string retVal = "";

            try
            {
                WordprocessingDocument wordDoc =
                       WordprocessingDocument.Open(file, false);
                retVal = wordDoc.MainDocumentPart.Document.Body.InnerText;
            }
            catch (Exception ex)
            {
                ParsersInfra.RecordParsingFailure(Log, ex, file);
            }

            return retVal;
        }
    }

    public class Excel : BaseClass
    {
        public Excel() :base()
        { }
       
        public static string Parse(FileStream file)
        {
            string retVal = "";

            try
            {
                SpreadsheetDocument excelDoc =
                   SpreadsheetDocument.Open(file, false);
                foreach (Sheet sheet in excelDoc.WorkbookPart.Workbook.Descendants<Sheet>())
                {
                    WorksheetPart worksheetPart = (WorksheetPart)excelDoc.WorkbookPart.GetPartById(sheet.Id);
                    Worksheet worksheet = worksheetPart.Worksheet;

                    SharedStringTablePart shareStringPart = excelDoc.WorkbookPart.GetPartsOfType<SharedStringTablePart>().First();
                    SharedStringItem[] items = shareStringPart.SharedStringTable.Elements<SharedStringItem>().ToArray();

                    // Create a new filename and save this file out.
              
                    foreach (var row in worksheet.Descendants<Row>())
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (Cell cell in row)
                        {
                            string value = string.Empty;
                            if (cell.CellValue != null)
                            {
                                // If the content of the first cell is stored as a shared string, get the text
                                // from the SharedStringTablePart. Otherwise, use the string value of the cell.
                                if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
                                    value = items[int.Parse(cell.CellValue.Text)].InnerText;
                                else
                                    value = cell.CellValue.Text;
                            }

                            // to be safe, always use double quotes.
                            sb.Append(string.Format("\"{0}\"\t", value.Trim()));
                        }
                    retVal += sb.ToString().TrimEnd(',') + System.Environment.NewLine;
                    }
                }
            }
            catch (Exception ex)
            {
                ParsersInfra.RecordParsingFailure(Log, ex, file);
            }
            return retVal;
        }
    }

    public class Outlook : BaseClass
    {
        public Outlook() :base()
        { }
        static string _tmpFilesFolder = ConfigurationManager.AppSettings["tmp_files_folder"];

        public static string Parse(FileStream file)
        {
            MsgReader.Reader read = new Reader();
            string retVal = "";

            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    if (file.Name.ToLower().EndsWith("eml"))
                    {
                        file.CopyTo(ms);
                        MsgReader.Mime.Message msg = new MsgReader.Mime.Message(ms.ToArray());
                        retVal += "From: " + (msg.Headers.From == null ? "" : msg.Headers.From.DisplayName) + Environment.NewLine;
                        retVal += "To: " + ParseAddresses(msg.Headers.To) + Environment.NewLine;
                        retVal += "Cc: " + ParseAddresses(msg.Headers.Cc) + Environment.NewLine;
                        retVal += "Bcc: " + ParseAddresses(msg.Headers.Bcc) + Environment.NewLine;
                        retVal += "Subject: " + msg.Headers.Subject + Environment.NewLine;

                        retVal += msg.TextBody.GetBodyAsText();
                    }
                    else if (file.Name.ToLower().EndsWith("msg"))
                    {
                        MsgReader.Outlook.Storage.Message msg = new MsgReader.Outlook.Storage.Message(file, FileAccess.Read);
                         retVal += "From: " + (msg.Sender == null ? "" : msg.Sender.DisplayName + " " + msg.Sender.Email) + Environment.NewLine;
                        foreach(MsgReader.Outlook.Storage.Recipient rec in msg.Recipients)
                        {
                            retVal += "To: " + rec.DisplayName + " " + rec.Email + Environment.NewLine;
                        }
                         
                        retVal += "Subject: " + msg.Subject + Environment.NewLine;
                        retVal += msg.BodyText;

                        foreach(MsgReader.Outlook.Storage.Attachment a in msg.Attachments)
                        {
                            string fileName = DateTime.Now.Ticks.ToString() + a.FileName;
                            File.WriteAllBytes(_tmpFilesFolder + "\\" + fileName, a.Data);
                            FileInfo f = new FileInfo(fileName);
                            retVal += fileName + ": " + FileUtils.ParseSync(new FileInfo(_tmpFilesFolder + "\\" + fileName));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ParsersInfra.RecordParsingFailure(Log, ex, file);
            }
            return retVal;
        }

        static string ParseAddresses(List<MsgReader.Mime.Header.RfcMailAddress> addresses)
        {
            string retVal = "";
            if (addresses != null && addresses.Count > 0)
            {
                foreach (MsgReader.Mime.Header.RfcMailAddress address in addresses)
                    retVal += address.DisplayName + ";";
            }

            return retVal;
        }
    }

    public class PowerPoint : BaseClass
    {
        public PowerPoint() :base()
        { }

        public static string Parse(FileStream file)
        {
            string retVal = "";
            try
            { 
                PresentationDocument doc;
                int numberOfSlides = CountSlides(out doc, file);
                for (int i = 0; i < numberOfSlides; i++)
                {
                    string slideText;
                    GetSlideIdAndText(out slideText, doc, i);
                    retVal += slideText + Environment.NewLine;
                }
            }
            catch (Exception ex)
            {
                ParsersInfra.RecordParsingFailure(Log, ex, file);
            }
            return retVal;
        }

        public static int CountSlides(out PresentationDocument doc, FileStream presentationFile)
        {
            // Open the presentation as read-only.
            doc = PresentationDocument.Open(presentationFile, false);
           
            // Pass the presentation to the next CountSlides method
            // and return the slide count.
            return CountSlides(doc);
        }

        // Count the slides in the presentation.
        public static int CountSlides(PresentationDocument presentationDocument)
        {
            // Check for a null document object.
            if (presentationDocument == null)
            {
                throw new ArgumentNullException("presentationDocument");
            }

            int slidesCount = 0;

            // Get the presentation part of document.
            PresentationPart presentationPart = presentationDocument.PresentationPart;
            // Get the slide count from the SlideParts.
            if (presentationPart != null)
            {
                slidesCount = presentationPart.SlideParts.Count();
            }
            // Return the slide count to the previous method.
            return slidesCount;
        }

        public static void GetSlideIdAndText(out string sldText, PresentationDocument ppt, int index)
        {
            // Get the relationship ID of the first slide.
            PresentationPart part = ppt.PresentationPart;
            OpenXmlElementList slideIds = part.Presentation.SlideIdList.ChildElements;

            string relId = (slideIds[index] as SlideId).RelationshipId;

            // Get the slide part from the relationship ID.
            SlidePart slide = (SlidePart)part.GetPartById(relId);

            // Build a StringBuilder object.
            StringBuilder paragraphText = new StringBuilder();

            // Get the inner text of the slide:
            IEnumerable<A.Text> texts = slide.Slide.Descendants<A.Text>();
            foreach (A.Text text in texts)
            {
                paragraphText.Append(text.Text);
            }
            sldText = paragraphText.ToString();
        }
    }

    public class OldWord : BaseClass
    {
        public OldWord() :base()
        { }

        public static string Parse(FileStream file)
        {
            string retVal = "";

            try
            {
                Spire.Doc.Document doc = new Spire.Doc.Document(file);
                retVal = doc.GetText();
            }
            catch (Exception ex)
            {
                ParsersInfra.RecordParsingFailure(Log, ex, file);
            }

            return retVal;
        }
    }

    public class OldExcel : BaseClass
    {
        public OldExcel() :base()
        { }

        public static string Parse(FileStream file)
        {
            string retVal = "";

            try
            {                
                HSSFWorkbook hssfwb = new HSSFWorkbook(file);
                ExcelExtractor extractor = new ExcelExtractor(hssfwb);
                retVal = extractor.Text;
            }
            catch (Exception ex)
            {
                ParsersInfra.RecordParsingFailure(Log, ex, file);
            }

            return retVal;
        }
    }
}
