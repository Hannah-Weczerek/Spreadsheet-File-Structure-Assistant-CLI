using ClosedXML.Excel;

namespace SpreadsheetFileStructureAssistantCLI;

public static class StructureToSpreadsheetExporter
{
    private sealed record ExtractedRecord(
        List<string> FolderSegments,
        Dictionary<string, string> Fields);

    public static void RunExportFlow(Action drawBanner)
    {
        const int totalSteps = 3;
        int step = 1;

        string? sourceRootFolder = null;
        bool includeFolderColumns = true;
        string? outputDirectory = null;
        string outputFileName = string.Empty;

        while (step <= totalSteps)
        {
            if (step == 1)
            {
                var pickRes = ConsoleFolderPicker.PickDestinationFolder(
                    drawBanner,
                    $"Schritt 1 / {totalSteps}: Quell-Dateistruktur auswählen",
                    "Wählen Sie den Hauptordner der Dateistruktur aus, die in Excel exportiert werden soll:");

                if (pickRes.Outcome == PickerOutcome.Back || pickRes.Outcome == PickerOutcome.Cancelled || pickRes.FolderPath is null)
                    return;

                sourceRootFolder = pickRes.FolderPath;
                step = 2;
            }
            else if (step == 2)
            {
                Console.Clear();
                drawBanner();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Schritt 2 / {totalSteps}: Export-Optionen konfigurieren");
                Console.ResetColor();
                Console.WriteLine(new string('-', 76));
                Console.WriteLine($"Gewählte Struktur: {sourceRootFolder}\n");

                Console.WriteLine("Sollen die Ordnernamen (z. B. Gruppenebenen & Unterordnername) als eigene Spalten");
                Console.WriteLine("am Anfang der Excel-Tabelle eingefügt werden?");
                Console.Write("[Y/N | Backspace = Zurück]: ");

                bool backRequested = false;
                while (true)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Backspace)
                    {
                        backRequested = true;
                        break;
                    }

                    char ch = char.ToUpperInvariant(key.KeyChar);
                    if (ch is 'Y' or 'J')
                    {
                        Console.WriteLine("Y");
                        includeFolderColumns = true;
                        break;
                    }
                    if (ch == 'N')
                    {
                        Console.WriteLine("N");
                        includeFolderColumns = false;
                        break;
                    }
                }

                if (backRequested)
                {
                    step = 1;
                    continue;
                }

                step = 3;
            }
            else if (step == 3)
            {
                var destRes = ConsoleFolderPicker.PickDestinationFolder(
                    drawBanner,
                    $"Schritt 3 / {totalSteps}: Zielordner & Dateiname für .xlsx wählen",
                    "Wählen Sie den Speicherort für die neue Excel-Datei (.xlsx):");

                if (destRes.Outcome == PickerOutcome.Back)
                {
                    step = 2;
                    continue;
                }
                if (destRes.Outcome == PickerOutcome.Cancelled || destRes.FolderPath is null)
                    return;

                outputDirectory = destRes.FolderPath;

                Console.Clear();
                drawBanner();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Schritt 3 / {totalSteps}: Excel-Dateiname festlegen");
                Console.ResetColor();
                Console.WriteLine(new string('-', 76));
                Console.WriteLine($"Zielordner: {outputDirectory}");

                string defaultName = $"{new DirectoryInfo(sourceRootFolder!).Name}_Export";
                Console.Write($"\nDateiname für die .xlsx-Datei (Enter für '{defaultName}' oder 'zurück'): ");
                string? rawName = Console.ReadLine()?.Trim();

                if (string.Equals(rawName, "zurück", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(rawName, "zurueck", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(rawName, "back", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string cleanName = string.IsNullOrWhiteSpace(rawName) ? defaultName : rawName;
                if (cleanName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Ungültige Zeichen im Dateinamen. Drücken Sie eine Taste...");
                    Console.ResetColor();
                    Console.ReadKey(intercept: true);
                    continue;
                }

                outputFileName = cleanName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? cleanName
                    : $"{cleanName}.xlsx";

                ExecuteExport(drawBanner, sourceRootFolder!, Path.Combine(outputDirectory, outputFileName), includeFolderColumns);
                break;
            }
        }
    }

    private static void ExecuteExport(Action drawBanner, string rootFolder, string targetXlsxPath, bool includeFolderColumns)
    {
        Console.Clear();
        drawBanner();
        Console.WriteLine("Scanne Verzeichnisse und lese .txt / .docx Dateien aus...");

        var dataFiles = Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
            .Where(f =>
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                return ext is ".txt" or ".docx";
            })
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dataFiles.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Keine .txt- oder .docx-Dateien in der ausgewählten Ordnerstruktur gefunden.");
            Console.ResetColor();
            return;
        }

        var records = new List<ExtractedRecord>();
        var dynamicHeaders = new List<string>();
        var headerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int maxFolderDepth = 0;

        foreach (string filePath in dataFiles)
        {
            string relativeDir = Path.GetRelativePath(rootFolder, Path.GetDirectoryName(filePath) ?? rootFolder);
            var segments = relativeDir == "."
                ? new List<string>()
                : relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

            if (segments.Count > maxFolderDepth)
                maxFolderDepth = segments.Count;

            var lines = DocumentHelper.ReadDocumentLines(filePath);
            var fieldDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int noteCounter = 1;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line == "--- Zusammengeführte Daten ---")
                    continue;

                int colonIdx = line.IndexOf(':');
                string key;
                string value;

                if (colonIdx > 0)
                {
                    key = line[..colonIdx].Trim();
                    value = line[(colonIdx + 1)..].Trim();
                }
                else
                {
                    key = $"Freitext_{noteCounter++}";
                    value = line;
                }

                if (headerSet.Add(key))
                {
                    dynamicHeaders.Add(key);
                }

                if (fieldDict.TryGetValue(key, out string? existingVal) && !string.IsNullOrEmpty(existingVal))
                {
                    if (!string.IsNullOrEmpty(value) && !existingVal.Equals(value, StringComparison.OrdinalIgnoreCase))
                    {
                        fieldDict[key] = $"{existingVal} | {value}";
                    }
                }
                else
                {
                    fieldDict[key] = value;
                }
            }

            records.Add(new ExtractedRecord(segments, fieldDict));
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Archiv_Export");

        var finalColumns = new List<string>();
        if (includeFolderColumns && maxFolderDepth > 0)
        {
            for (int d = 0; d < maxFolderDepth; d++)
            {
                string colName = (d == maxFolderDepth - 1)
                    ? "Unterordner"
                    : $"Ordner_Ebene_{d + 1}";
                finalColumns.Add(colName);
            }
        }

        finalColumns.AddRange(dynamicHeaders);

        for (int c = 0; c < finalColumns.Count; c++)
        {
            var cell = worksheet.Cell(1, c + 1);
            cell.Value = finalColumns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        for (int r = 0; r < records.Count; r++)
        {
            var rec = records[r];
            int rowNum = r + 2;
            int colCursor = 1;

            if (includeFolderColumns && maxFolderDepth > 0)
            {
                for (int d = 0; d < maxFolderDepth; d++)
                {
                    string segVal = d < rec.FolderSegments.Count ? rec.FolderSegments[d] : string.Empty;
                    worksheet.Cell(rowNum, colCursor++).Value = segVal;
                }
            }

            foreach (string header in dynamicHeaders)
            {
                rec.Fields.TryGetValue(header, out string? val);
                worksheet.Cell(rowNum, colCursor++).Value = val ?? string.Empty;
            }
        }

        if (finalColumns.Count > 0 && records.Count > 0)
        {
            worksheet.Range(1, 1, records.Count + 1, finalColumns.Count).SetAutoFilter();
            worksheet.Columns().AdjustToContents(5.0, 60.0);
        }

        workbook.SaveAs(targetXlsxPath);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nErfolg! {records.Count} Datensätze wurden in die Excel-Datei exportiert:");
        Console.ResetColor();
        Console.WriteLine(targetXlsxPath);
    }
}