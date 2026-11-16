using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SpreadsheetFileStructureAssistantCLI;

public static class DocumentHelper
{
    public static void WriteDocument(string filePath, IEnumerable<string> lines, bool asDocx)
    {
        if (!asDocx)
        {
            File.WriteAllLines(filePath, lines);
            return;
        }

        using var wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        foreach (string line in lines)
        {
            var para = body.AppendChild(new Paragraph());
            int colonIdx = line.IndexOf(':');
            if (colonIdx > 0 && colonIdx < line.Length - 1)
            {
                string keyPart = line[..(colonIdx + 1)];
                string valPart = line[(colonIdx + 1)..];

                var runKey = para.AppendChild(new Run());
                runKey.AppendChild(new RunProperties(new Bold()));
                runKey.AppendChild(new Text(keyPart) { Space = SpaceProcessingModeValues.Preserve });

                var runVal = para.AppendChild(new Run());
                runVal.AppendChild(new Text(valPart) { Space = SpaceProcessingModeValues.Preserve });
            }
            else
            {
                var run = para.AppendChild(new Run());
                run.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
            }
        }

        mainPart.Document.Save();
    }

    public static List<string> ReadDocumentLines(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".txt")
        {
            return File.ReadAllLines(filePath).ToList();
        }

        if (ext == ".docx")
        {
            var lines = new List<string>();
            using var wordDoc = WordprocessingDocument.Open(filePath, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                foreach (var para in body.Elements<Paragraph>())
                {
                    lines.Add(para.InnerText);
                }
            }
            return lines;
        }

        return [];
    }
}