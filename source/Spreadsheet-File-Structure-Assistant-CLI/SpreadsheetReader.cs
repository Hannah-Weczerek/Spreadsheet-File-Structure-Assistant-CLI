using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SpreadsheetFileStructureAssistantCLI;

public static class SpreadsheetReader
{
    public static Dictionary<string, List<List<string>>> ReadAllSheets(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Datei nicht gefunden.", filePath);

        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".xlsx" => ReadXlsx(filePath),
            ".ods"  => ReadOds(filePath),
            _       => throw new NotSupportedException($"Format nicht unterstützt: {Path.GetExtension(filePath)}")
        };
    }

    private static Dictionary<string, List<List<string>>> ReadXlsx(string path)
    {
        var result = new Dictionary<string, List<List<string>>>();

        using var doc = SpreadsheetDocument.Open(path, false);
        var workbookPart = doc.WorkbookPart!;
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable
            .Elements<SharedStringItem>()
            .Select(s => s.InnerText)
            .ToArray() ?? [];

        foreach (var sheet in workbookPart.Workbook.Descendants<Sheet>())
        {
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
            var rows = new List<List<string>>();

            foreach (var row in worksheetPart.Worksheet.Descendants<Row>())
            {
                var cellValues = new List<string>();
                int currentColumnIndex = 0;

                foreach (var cell in row.Elements<Cell>())
                {
                    int targetColumnIndex = GetColumnIndexFromCellReference(cell.CellReference?.Value, currentColumnIndex);

                    while (cellValues.Count < targetColumnIndex)
                    {
                        cellValues.Add(string.Empty);
                    }

                    string value = GetXlsxCellValue(cell, sharedStrings);
                    cellValues.Add(value);
                    currentColumnIndex = targetColumnIndex + 1;
                }

                if (cellValues.Any(v => !string.IsNullOrWhiteSpace(v)))
                {
                    rows.Add(cellValues);
                }
            }

            NormalizeRowLengths(rows);
            result[sheet.Name?.Value ?? "Sheet"] = rows;
        }

        return result;
    }

    private static string GetXlsxCellValue(Cell cell, string[] sharedStrings)
    {
        string value = cell.CellValue?.InnerText ?? cell.InnerText;

        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, out int index) && index >= 0 && index < sharedStrings.Length)
        {
            return sharedStrings[index];
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? value;
        }

        return value;
    }

    private static int GetColumnIndexFromCellReference(string? cellReference, int fallbackIndex)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            return fallbackIndex;

        int columnIndex = 0;
        bool hasLetters = false;

        foreach (char ch in cellReference)
        {
            if (char.IsLetter(ch))
            {
                hasLetters = true;
                columnIndex = columnIndex * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            }
            else
            {
                break;
            }
        }

        return hasLetters ? columnIndex - 1 : fallbackIndex;
    }

    private static Dictionary<string, List<List<string>>> ReadOds(string path)
    {
        var result = new Dictionary<string, List<List<string>>>();

        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("content.xml")
            ?? throw new InvalidDataException("Ungültige .ods-Datei: content.xml fehlt.");

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);

        XNamespace table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

        foreach (var sheet in doc.Descendants(table + "table"))
        {
            string sheetName = sheet.Attribute(table + "name")?.Value ?? "Sheet";
            var rows = new List<List<string>>();

            foreach (var row in sheet.Elements(table + "table-row"))
            {
                var cellValues = new List<string>();

                foreach (var cell in row.Elements())
                {
                    if (cell.Name != table + "table-cell" && cell.Name != table + "covered-table-cell")
                        continue;

                    string value = string.Join(" ", cell.Elements(text + "p").Select(p => p.Value));

                    int repeatCount = 1;
                    var repeatAttr = cell.Attribute(table + "number-columns-repeated");
                    if (repeatAttr != null && int.TryParse(repeatAttr.Value, out int parsedRepeat) && parsedRepeat > 1)
                    {
                        repeatCount = string.IsNullOrWhiteSpace(value) ? Math.Min(parsedRepeat, 256) : parsedRepeat;
                    }

                    for (int i = 0; i < repeatCount; i++)
                    {
                        cellValues.Add(value);
                    }
                }

                while (cellValues.Count > 0 && string.IsNullOrWhiteSpace(cellValues[^1]))
                {
                    cellValues.RemoveAt(cellValues.Count - 1);
                }

                if (cellValues.Any(v => !string.IsNullOrWhiteSpace(v)))
                {
                    rows.Add(cellValues);
                }
            }

            NormalizeRowLengths(rows);
            result[sheetName] = rows;
        }

        return result;
    }

    private static void NormalizeRowLengths(List<List<string>> rows)
    {
        if (rows.Count == 0)
            return;

        int maxColumns = rows.Max(r => r.Count);
        foreach (var row in rows)
        {
            while (row.Count < maxColumns)
            {
                row.Add(string.Empty);
            }
        }
    }
}
