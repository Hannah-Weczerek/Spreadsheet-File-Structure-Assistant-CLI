using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SpreadsheetFileStructureAssistantCLI;

public static class DocumentHelper
{
    private const string DocxFontName = "Garamond";
    private const string DocxFontSizeHalfPoints = "24"; // 12 pt = 24 half-points

    public static void WriteDocument(string filePath, IEnumerable<string> lines, bool asDocx, bool boldFirstLine = false)
    {
        if (!asDocx)
        {
            File.WriteAllLines(filePath, lines);
            return;
        }

        using var wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document();

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(
                    new RunPropertiesBaseStyle(
                        new RunFonts
                        {
                            Ascii = DocxFontName,
                            HighAnsi = DocxFontName,
                            ComplexScript = DocxFontName,
                            EastAsia = DocxFontName
                        },
                        new FontSize { Val = DocxFontSizeHalfPoints },
                        new FontSizeComplexScript { Val = DocxFontSizeHalfPoints }
                    )
                ),
                new ParagraphPropertiesDefault()
            )
        );
        stylesPart.Styles.Save();

        var body = mainPart.Document.AppendChild(new Body());

        int lineIndex = 0;
        foreach (string line in lines)
        {
            var para = body.AppendChild(new Paragraph());

            if (lineIndex == 0 && boldFirstLine && !string.IsNullOrEmpty(line))
            {
                var headerRun = para.AppendChild(new Run());
                headerRun.AppendChild(CreateRunProperties(bold: true));
                headerRun.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
                lineIndex++;
                continue;
            }

            int colonIdx = line.IndexOf(':');
            if (colonIdx > 0 && colonIdx < line.Length - 1)
            {
                string keyPart = line[..(colonIdx + 1)];
                string valPart = line[(colonIdx + 1)..];

                var runKey = para.AppendChild(new Run());
                runKey.AppendChild(CreateRunProperties(bold: true));
                runKey.AppendChild(new Text(keyPart) { Space = SpaceProcessingModeValues.Preserve });

                var runVal = para.AppendChild(new Run());
                runVal.AppendChild(CreateRunProperties(bold: false));
                runVal.AppendChild(new Text(valPart) { Space = SpaceProcessingModeValues.Preserve });
            }
            else
            {
                var run = para.AppendChild(new Run());
                run.AppendChild(CreateRunProperties(bold: false));
                run.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
            }

            lineIndex++;
        }

        mainPart.Document.Save();
    }

    private static RunProperties CreateRunProperties(bool bold)
    {
        var props = new RunProperties(
            new RunFonts
            {
                Ascii = DocxFontName,
                HighAnsi = DocxFontName,
                ComplexScript = DocxFontName,
                EastAsia = DocxFontName
            },
            new FontSize { Val = DocxFontSizeHalfPoints },
            new FontSizeComplexScript { Val = DocxFontSizeHalfPoints }
        );

        if (bold)
        {
            props.PrependChild(new Bold());
        }

        return props;
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