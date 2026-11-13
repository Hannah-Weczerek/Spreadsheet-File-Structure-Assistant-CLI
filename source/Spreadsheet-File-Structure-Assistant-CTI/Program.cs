using SpreadsheetFileStructureAssistantCTI;

bool exit = false;
while (!exit)
{
    Console.Clear();
    DisplayBanner();
    DisplayMenu();

    int option = GetMenuOption();

    Console.Clear();
    DisplayBanner();
    ProcessMenuOption(option);

    if (option == 3)
    {
        Environment.Exit(0);
    }

    Console.WriteLine("\nDrücken Sie eine Taste, um zum Menü zurückzukehren.");
    Console.ReadKey(intercept: true);
}

void DisplayBanner()
{
    Console.WriteLine("#####################################################");
    Console.WriteLine("Welcome to Spreadsheet-File-Structure-Assistant v1.0");
    Console.WriteLine("#####################################################");
    Console.WriteLine("");
}

void DisplayMenu()
{
    Console.WriteLine("Bitte wählen Sie eine gewünschte Operation.");
    Console.WriteLine("1.   Neue Dateistruktur erstellen.");
    Console.WriteLine("2.   Neue Dateistruktur mittels Vorlage erstellen.");
    Console.WriteLine("3.   Verlassen");
}

int GetMenuOption()
{
    Console.Write("\nWählen Sie aus: ");
    while (true)
    {
        ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

        if (keyInfo.KeyChar is '1' or '2' or '3')
        {
            Console.WriteLine(keyInfo.KeyChar);
            return keyInfo.KeyChar - '0';
        }
    }
}

bool AskYesNo(string prompt)
{
    Console.Write($"\n{prompt} [Y/N]: ");
    while (true)
    {
        char key = char.ToUpperInvariant(Console.ReadKey(intercept: true).KeyChar);
        if (key is 'Y' or 'J')
        {
            Console.WriteLine("Y");
            return true;
        }
        if (key == 'N')
        {
            Console.WriteLine("N");
            return false;
        }
    }
}

string AskValidName(string prompt)
{
    while (true)
    {
        Console.Write($"\n{prompt}: ");
        string? input = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("Der Name darf nicht leer sein.");
            continue;
        }

        if (input.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Console.WriteLine("Der Name enthaelt ungueltige Zeichen. Bitte erneut versuchen.");
            continue;
        }

        return input;
    }
}

int AskRowNumber(string prompt, int maxRow)
{
    while (true)
    {
        Console.Write($"\n{prompt} (1 - {maxRow}): ");
        string? input = Console.ReadLine()?.Trim();

        if (int.TryParse(input, out int rowNum) && rowNum >= 1 && rowNum <= maxRow)
        {
            return rowNum;
        }

        Console.WriteLine($"Bitte eine gueltige Zeilennummer zwischen 1 und {maxRow} eingeben.");
    }
}

string SanitizeFileSystemName(string rawName)
{
    char[] invalidChars = Path.GetInvalidFileNameChars();
    string cleaned = new(rawName.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
    cleaned = cleaned.Trim().TrimEnd('.');
    return string.IsNullOrWhiteSpace(cleaned) ? "Unbenannt" : cleaned;
}

void ProcessMenuOption(int option)
{
    switch (option)
    {
        case 1:
            RunCreateFileStructureFlow();
            break;

        case 2:
            RunCreateFromTemplateFlow();
            break;

        case 3:
            Console.WriteLine("Verlasse Programm...");
            Environment.Exit(0);
            break;
    }
}

(bool UseGrouping, int GroupColIndex, Dictionary<string, string> GroupMap)? ConfigureGrouping(IReadOnlyList<string> headers, IReadOnlyList<string> sampleRow)
{
    Console.Clear();
    DisplayBanner();
    Console.WriteLine("Schritt 6 (optional): Unterordner nach Spaltenwert gruppieren");
    Console.WriteLine("Beispiel: Alle Zeilen mit Wert 'm' in Spalte 'Geschlecht' landen im Ordner 'maennlich'.");
    Console.WriteLine("Abweichende Werte landen automatisch in 'Manuelle Sortierung erforderlich'.");

    if (!AskYesNo("Möchten Sie eine solche Gruppierung aktivieren?"))
    {
        return (false, -1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    var selectedCols = ConsoleColumnSelector.SelectColumns(
        DisplayBanner,
        "Gruppierungs-Spalte auswählen",
        "Wählen Sie genau EINE Spalte aus, nach der sortiert werden soll (bei Mehrfachauswahl zählt die erste!):",
        headers,
        sampleRow,
        " | ");

    if (selectedCols is null || selectedCols.Count == 0)
        return null;

    int groupColIdx = selectedCols[0];
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    Console.Clear();
    DisplayBanner();
    Console.WriteLine($"Gewählte Gruppierungs-Spalte: {headers[groupColIdx]} (Spalte {TemplateManager.GetExcelColumnLetter(groupColIdx)})");
    Console.WriteLine("Definieren Sie jetzt die Werte aus der Tabelle, nach denen sortiert werden soll, und benennen Sie die dazugehörigen Ziel-Unterordner.");
    Console.WriteLine("Lassen Sie den Tabellenwert leer und drücken Sie [Enter], sobald Sie fertig sind. \n");

    while (true)
    {
        Console.Write("Tabellenwert (z.B. 'm' oder 'maennlich' - leer lassen zum Beenden): ");
        string? cellValue = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(cellValue))
        {
            if (map.Count == 0)
            {
                Console.WriteLine("Bitte definieren Sie mindestens einen Wert!");
                continue;
            }
            break;
        }

        string targetFolder = AskValidName($"Name des Ziel-Unterordners für den Wert '{cellValue}'");
        map[cellValue] = targetFolder;
        Console.WriteLine($"  -> Regel gespeichert: '{cellValue}'  =>  Ordner '{targetFolder}'\n");
    }

    return (true, groupColIdx, map);
}

void RunCreateFileStructureFlow()
{
    // SCHRITT 1 --- AUSWÄHLEN UND ERSTELLEN
    string? selectedFile = ConsoleFilePicker.PickSpreadsheet(DisplayBanner);
    Console.Clear();
    DisplayBanner();

    if (selectedFile is null)
    {
        Console.WriteLine("Dateiauswahl abgebrochen.");
        return;
    }

    Console.WriteLine($"Ausgewählte Datei:\n{selectedFile}\n");

    var sheets = SpreadsheetReader.ReadAllSheets(selectedFile);
    if (sheets.Count == 0)
    {
        Console.WriteLine("Die Tabelle enthält keine Daten.");
        return;
    }

    foreach (var (sheetName, rows) in sheets)
    {
        Console.WriteLine($"Sheet: {sheetName} ({rows.Count} Zeilen)");
        foreach (var row in rows.Take(5))
        {
            Console.WriteLine("  " + string.Join(" | ", row));
        }
        Console.WriteLine();
    }

    if (!AskYesNo("Ist dies die richtige Tabelle?"))
    {
        Console.WriteLine("Vorgang abgebrochen.");
        return;
    }

    // Aktives Tabellenblatt
    var selectedSheet = sheets.First();
    if (sheets.Count > 1)
    {
        var sheetList = sheets.ToList();
        Console.WriteLine("\nMehrere Tabellenblätter gefunden:");
        for (int i = 0; i < sheetList.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {sheetList[i].Key} ({sheetList[i].Value.Count} Zeilen)");
        }
        int chosenSheetIdx = AskRowNumber("Welches Tabellenblatt soll verwendet werden?", sheetList.Count) - 1;
        selectedSheet = sheetList[chosenSheetIdx];
    }

    var allRows = selectedSheet.Value;
    if (allRows.Count < 2)
    {
        Console.WriteLine("Das Tabellenblatt benötigt mindestens eine Titelzeile und eine Datenzeile.");
        return;
    }

    // SCHRITT 2 --- ZIELORDNER WÄHLEN
    string? destinationFolder;
    while (true)
    {
        destinationFolder = ConsoleFolderPicker.PickDestinationFolder(DisplayBanner);
        Console.Clear();
        DisplayBanner();

        if (destinationFolder is null)
        {
            Console.WriteLine("Ordnerauswahl abgebrochen.");
            return;
        }

        Console.WriteLine($"Gewählter Zielordner:\n{destinationFolder}");
        if (AskYesNo("Soll dieser Zielordner verwendet werden?"))
        {
            break;
        }
    }

    // SCHRITT 3 --- NAME DES HAUPTORDNERS
    Console.Clear();
    DisplayBanner();
    Console.WriteLine($"Tabelle:    {Path.GetFileName(selectedFile)}");
    Console.WriteLine($"Zielordner: {destinationFolder}");

    string rootFolderName = AskValidName("Name des neuen Hauptordners für die Ordnerstruktur");
    string fullRootPath = Path.Combine(destinationFolder, rootFolderName);

    // SCHRITT 4: TITELZEILE UND UNTERORDNER BENENNUNGSVORLAGE
    Console.Clear();
    DisplayBanner();
    Console.WriteLine("Schritt 4: Spaltentitel festlegen");
    Console.WriteLine($"Aktuelle Zeile 1: {string.Join(" | ", allRows[0])}");

    int headerRowIndex = 0;
    if (!AskYesNo("Soll standardmäßig Zeile 1 für die Spaltentitel verwendet werden?"))
    {
        headerRowIndex = AskRowNumber("Welche Zeile enthält die Spaltentitel?", allRows.Count - 1) - 1;
    }

    var headers = allRows[headerRowIndex]
        .Select((h, idx) => string.IsNullOrWhiteSpace(h) ? $"Spalte_{idx + 1}" : h.Trim())
        .ToList();

    var dataRows = allRows.Skip(headerRowIndex + 1).ToList();
    if (dataRows.Count == 0)
    {
        Console.WriteLine("Unterhalb der gewählten Titelzeile befinden sich keine Datenzeilen.");
        return;
    }

    var folderNameColumns = ConsoleColumnSelector.SelectColumns(
        DisplayBanner,
        "Schritt 4: Benennungsvorlage für die Unterordner erstellen",
        "Wähle die Spalten in der gewünschten Reihenfolge aus (1. Auswahl = 1. Teil des Ordnernamens):",
        headers,
        dataRows[0],
        "_");

    if (folderNameColumns is null)
    {
        Console.WriteLine("Vorlagenerstellung abgebrochen.");
        return;
    }

    // SCHRITT 5 --- .txt FÜR JEDEN UNTERORDNER
    Console.Clear();
    DisplayBanner();
    Console.WriteLine("Schritt 5: Textdatei (.txt) für jeden Unterordner konfigurieren");

    string rawTxtName = AskValidName("Name der .txt-Datei in jedem Unterordner");
    string txtFileName = rawTxtName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        ? rawTxtName
        : $"{rawTxtName}.txt";

    var txtContentColumns = ConsoleColumnSelector.SelectColumns(
        DisplayBanner,
        $"Schritt 5: Inhalt für '{txtFileName}' auswählen",
        "Wählen Sie die gewünschten Spalten aus (jede Auswahl = 1 Zeile in der .txt):",
        headers,
        dataRows[0],
        ", ",
        allowSelectAll: true);

    if (txtContentColumns is null)
    {
        Console.WriteLine("Konfiguration der .txt-Datei abgebrochen.");
        return;
    }

    var grouping = ConfigureGrouping(headers, dataRows[0]);
    if (grouping is null)
    {
        Console.WriteLine("Gruppierungskonfiguration abgebrochen.");
        return;
    }


    // AUSFÜHRUNG: ALLES ERSTELLEN
    Console.Clear();
    DisplayBanner();
    Console.WriteLine("Zusammenfassung vor dem Erstellen:");
    Console.WriteLine($"  Hauptordner:       {fullRootPath}");
    Console.WriteLine($"  Datensätze:        {dataRows.Count} Zeilen");
    Console.WriteLine($"  Ordner-Vorlage:    {string.Join("_", folderNameColumns.Select(i => headers[i]))}");
    Console.WriteLine($"  Datei je Ordner:   {txtFileName}");
    Console.WriteLine($"  Inhalt der Datei:  {string.Join(", ", txtContentColumns.Select(i => headers[i]))}");
    
    if (grouping.Value.UseGrouping)
    {
        string rules = string.Join(", ", grouping.Value.GroupMap.Select(kv => $"'{kv.Key}' -> '{kv.Value}'"));
        Console.WriteLine($"  Gruppierung:       Spalte {headers[grouping.Value.GroupColIndex]} [{rules}]");
    }

    if (!AskYesNo("Struktur jetzt auf der Festplatte erstellen?"))
    {
        Console.WriteLine("Erstellung abgebrochen.");
        return;
    }

    Directory.CreateDirectory(fullRootPath);

    int createdCount = 0;
    int manualReviewCount = 0;
    var usedFolderPaths = new HashSet<string>(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    foreach (var row in dataRows)
    {
        string targetParentDir = fullRootPath;

        if (grouping.Value.UseGrouping && grouping.Value.GroupColIndex >= 0)
        {
            string rawGroupVal = grouping.Value.GroupColIndex < row.Count
                ? row[grouping.Value.GroupColIndex].Trim()
                : string.Empty;

            if (!string.IsNullOrEmpty(rawGroupVal) && grouping.Value.GroupMap.TryGetValue(rawGroupVal, out string? mappedGroupFolder))
            {
                targetParentDir = Path.Combine(fullRootPath, SanitizeFileSystemName(mappedGroupFolder));
            }
            else
            {
                targetParentDir = Path.Combine(fullRootPath, "Manuelle Sortierung erforderlich");
                manualReviewCount++;
            }

            Directory.CreateDirectory(targetParentDir);
        }

        var nameParts = folderNameColumns.Select(colIdx =>
            colIdx < row.Count && !string.IsNullOrWhiteSpace(row[colIdx])
                ? row[colIdx].Trim()
                : "Leer");

        string baseSubfolderName = SanitizeFileSystemName(string.Join("_", nameParts));
        string subfolderPath = Path.Combine(targetParentDir, baseSubfolderName);

        int duplicateCounter = 2;
        while (!usedFolderPaths.Add(subfolderPath))
        {
            subfolderPath = Path.Combine(targetParentDir, $"{baseSubfolderName}_{duplicateCounter}");
            duplicateCounter++;
        }

        Directory.CreateDirectory(subfolderPath);

        var txtLines = txtContentColumns.Select(colIdx =>
        {
            string title = headers[colIdx];
            string value = colIdx < row.Count ? row[colIdx].Trim() : string.Empty;
            return $"{title}: {value}";
        });

        string txtFilePath = Path.Combine(subfolderPath, txtFileName);
        File.WriteAllLines(txtFilePath, txtLines);
        createdCount++;
    }

    Console.WriteLine($"\nFertig! {createdCount} Unterordner inklusive '{txtFileName}' wurden erfolgreich erstellt unter:\n{fullRootPath}");
        if (AskYesNo("Möchten Sie diese Spalten-Einstellungen als Vorlage für später speichern?"))
    {
        string tplName = AskValidName("Name für die Vorlage");
        var template = new StructureTemplate
        {
            TemplateName = tplName,
            HeaderRowIndex = headerRowIndex,
            FolderNameColumnIndices = folderNameColumns,
            TxtFileName = txtFileName,
            TxtContentColumnIndices = txtContentColumns
        };
        TemplateManager.SaveTemplate(template, SanitizeFileSystemName);
        Console.WriteLine($"Vorlage '{tplName}' wurde gespeichert!");
    }
}

void RunCreateFromTemplateFlow()
{
    var existingTemplates = TemplateManager.LoadAllTemplates();
    if (existingTemplates.Count == 0)
    {
        Console.WriteLine("Es sind noch keine Vorlagen gespeichert.");
        Console.WriteLine("Erstelle zuerst eine Struktur über Option 1 und speichere sie am Ende als Vorlage.");
        return;
    }

    var chosenTemplate = TemplateManager.PickTemplate(DisplayBanner);
    Console.Clear();
    DisplayBanner();

    if (chosenTemplate is null)
    {
        Console.WriteLine("Vorlagenauswahl abgebrochen.");
        return;
    }

    // Schritt 1: Tabelle wählen
    string? selectedFile = ConsoleFilePicker.PickSpreadsheet(DisplayBanner);
    Console.Clear();
    DisplayBanner();

    if (selectedFile is null)
    {
        Console.WriteLine("Dateiauswahl abgebrochen.");
        return;
    }

    var sheets = SpreadsheetReader.ReadAllSheets(selectedFile);
    if (sheets.Count == 0)
    {
        Console.WriteLine("Die Tabelle enthält keine Daten.");
        return;
    }

    var selectedSheet = sheets.First();
    if (sheets.Count > 1)
    {
        var sheetList = sheets.ToList();
        Console.WriteLine("Mehrere Tabellenblätter gefunden:");
        for (int i = 0; i < sheetList.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {sheetList[i].Key} ({sheetList[i].Value.Count} Zeilen)");
        }
        int chosenSheetIdx = AskRowNumber("Welches Tabellenblatt soll verwendet werden?", sheetList.Count) - 1;
        selectedSheet = sheetList[chosenSheetIdx];
    }

    var allRows = selectedSheet.Value;
    int maxRequiredCol = chosenTemplate.FolderNameColumnIndices
        .Concat(chosenTemplate.TxtContentColumnIndices)
        .DefaultIfEmpty(0)
        .Max();

    if (allRows.Count <= chosenTemplate.HeaderRowIndex + 1 || allRows[chosenTemplate.HeaderRowIndex].Count <= maxRequiredCol)
    {
        Console.WriteLine($"Fehler: Die Tabelle hat zu wenige Zeilen oder Spalten (benötigt mindestens Spalte {TemplateManager.GetExcelColumnLetter(maxRequiredCol)}).");
        return;
    }

    // Schritt 2: Zielordner wählen
    string? destinationFolder;
    while (true)
    {
        destinationFolder = ConsoleFolderPicker.PickDestinationFolder(DisplayBanner);
        Console.Clear();
        DisplayBanner();

        if (destinationFolder is null)
        {
            Console.WriteLine("Ordnerauswahl abgebrochen.");
            return;
        }

        Console.WriteLine($"Gewählter Zielordner:\n{destinationFolder}");
        if (AskYesNo("Soll dieser Zielordner verwendet werden?"))
            break;
    }

    // Schritt 3: Hauptordner benennen & ausführen
    Console.Clear();
    DisplayBanner();
    string rootFolderName = AskValidName("Name des neuen Hauptordners für die Ordnerstruktur");
    string fullRootPath = Path.Combine(destinationFolder, rootFolderName);

    var headers = allRows[chosenTemplate.HeaderRowIndex]
        .Select((h, idx) => string.IsNullOrWhiteSpace(h) ? $"Spalte_{idx + 1}" : h.Trim())
        .ToList();

    var dataRows = allRows.Skip(chosenTemplate.HeaderRowIndex + 1).ToList();

    Console.Clear();
    DisplayBanner();
    Console.WriteLine("Zusammenfassung (aus Vorlage):");
    Console.WriteLine($"  Vorlage:           {chosenTemplate.TemplateName}");
    Console.WriteLine($"  Hauptordner:       {fullRootPath}");
    Console.WriteLine($"  Datensätze:        {dataRows.Count} Zeilen");
    Console.WriteLine($"  Ordner-Vorlage:    {string.Join("_", chosenTemplate.FolderNameColumnIndices.Select(i => $"{headers[i]} ({TemplateManager.GetExcelColumnLetter(i)})"))}");
    Console.WriteLine($"  Datei je Ordner:   {chosenTemplate.TxtFileName}");
    Console.WriteLine($"  Inhalt der Datei:  {string.Join(", ", chosenTemplate.TxtContentColumnIndices.Select(i => $"{headers[i]} ({TemplateManager.GetExcelColumnLetter(i)})"))}");

    if (!AskYesNo("Struktur jetzt auf der Festplatte erstellen?"))
    {
        Console.WriteLine("Erstellung abgebrochen.");
        return;
    }

    Directory.CreateDirectory(fullRootPath);

    int createdCount = 0;
    var usedFolderNames = new HashSet<string>(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    foreach (var row in dataRows)
    {
        var nameParts = chosenTemplate.FolderNameColumnIndices.Select(colIdx =>
            colIdx < row.Count && !string.IsNullOrWhiteSpace(row[colIdx])
                ? row[colIdx].Trim()
                : "Leer");

        string baseSubfolderName = SanitizeFileSystemName(string.Join("_", nameParts));
        string finalSubfolderName = baseSubfolderName;

        int duplicateCounter = 2;
        while (!usedFolderNames.Add(finalSubfolderName))
        {
            finalSubfolderName = $"{baseSubfolderName}_{duplicateCounter}";
            duplicateCounter++;
        }

        string subfolderPath = Path.Combine(fullRootPath, finalSubfolderName);
        Directory.CreateDirectory(subfolderPath);

        var txtLines = chosenTemplate.TxtContentColumnIndices.Select(colIdx =>
        {
            string title = colIdx < headers.Count ? headers[colIdx] : $"Spalte_{colIdx + 1}";
            string value = colIdx < row.Count ? row[colIdx].Trim() : string.Empty;
            return $"{title}: {value}";
        });

        File.WriteAllLines(Path.Combine(subfolderPath, chosenTemplate.TxtFileName), txtLines);
        createdCount++;
    }

    Console.WriteLine($"\nFertig! {createdCount} Unterordner inklusive '{chosenTemplate.TxtFileName}' wurden erfolgreich erstellt unter:\n{fullRootPath}");
}
