using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpreadsheetFileStructureAssistantCTI;

public sealed class StructureTemplate
{
    public string TemplateName { get; set; } = string.Empty;
    public int HeaderRowIndex { get; set; }
    public List<int> FolderNameColumnIndices { get; set; } = [];
    public string TxtFileName { get; set; } = string.Empty;
    public List<int> TxtContentColumnIndices { get; set; } = [];

    public bool UseGrouping { get; set; } = false;
    public int GroupByColumnIndex { get; set; } = -1;
    public Dictionary<string, string> GroupValueToFolderMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StructureTemplate))]
internal partial class TemplateJsonContext : JsonSerializerContext
{
}

public static class TemplateManager
{
    public static string GetTemplatesDirectory()
    {
        string baseDir = AppContext.BaseDirectory;
        string templateDir = Path.Combine(baseDir, "Templates");
        Directory.CreateDirectory(templateDir);
        return templateDir;
    }

    public static void SaveTemplate(StructureTemplate template, Func<string, string> sanitizeFileName)
    {
        string dir = GetTemplatesDirectory();
        string safeFileName = sanitizeFileName(template.TemplateName) + ".json";
        string fullPath = Path.Combine(dir, safeFileName);

        string json = JsonSerializer.Serialize(template, TemplateJsonContext.Default.StructureTemplate);
        File.WriteAllText(fullPath, json);
    }

    public static List<(string FilePath, StructureTemplate Template)> LoadAllTemplates()
    {
        string dir = GetTemplatesDirectory();
        var list = new List<(string, StructureTemplate)>();

        foreach (string file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                string json = File.ReadAllText(file);
                var tpl = JsonSerializer.Deserialize(json, TemplateJsonContext.Default.StructureTemplate);
                if (tpl != null && !string.IsNullOrWhiteSpace(tpl.TemplateName))
                {
                    list.Add((file, tpl));
                }
            }
            catch
            {
            }
        }

        return list;
    }

    public static StructureTemplate? PickTemplate(Action drawBanner)
    {
        var templates = LoadAllTemplates();
        if (templates.Count == 0)
            return null;

        int selectedIndex = 0;

        Console.CursorVisible = false;
        try
        {
            while (true)
            {
                Console.Clear();
                drawBanner();

                Console.WriteLine("Schritt 1 (Vorlage): Gespeicherte Vorlage auswählen");
                Console.WriteLine("Steuerung: [Hoch/Runter] Waehlen | [Enter] Verwenden | [D] Loeschen | [Esc] Abbrechen");
                Console.WriteLine(new string('-', 84));

                for (int i = 0; i < templates.Count; i++)
                {
                    var tpl = templates[i].Template;
                    string folderCols = string.Join(" + ", tpl.FolderNameColumnIndices.Select(GetExcelColumnLetter));
                    string groupInfo = tpl.UseGrouping
                        ? $" | Gruppe: Spalte {GetExcelColumnLetter(tpl.GroupByColumnIndex)}"
                        : string.Empty;

                    string line = $"{tpl.TemplateName}  (Ordner: Spalte {folderCols} | Datei: {tpl.TxtFileName}{groupInfo})";

                    int maxWidth = Math.Max(20, Console.WindowWidth - 1);
                    if (line.Length > maxWidth)
                        line = line[..(maxWidth - 3)] + "...";
                    else
                        line = line.PadRight(maxWidth);

                    if (i == selectedIndex)
                    {
                        Console.BackgroundColor = ConsoleColor.White;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.WriteLine(line);
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(line);
                        Console.ResetColor();
                    }
                }

                Console.WriteLine(new string('-', 84));
                var current = templates[selectedIndex].Template;
                Console.WriteLine($"Details zu '{current.TemplateName}':");
                Console.WriteLine($"  Titelzeile:      Zeile {current.HeaderRowIndex + 1}");
                Console.WriteLine($"  Ordner-Spalten:  {string.Join("_", current.FolderNameColumnIndices.Select(GetExcelColumnLetter))}");
                Console.WriteLine($"  Textdatei:       {current.TxtFileName}");
                Console.WriteLine($"  Text-Spalten:    {string.Join(", ", current.TxtContentColumnIndices.Select(GetExcelColumnLetter))}");

                if (current.UseGrouping)
                {
                    string rules = string.Join(", ", current.GroupValueToFolderMap.Select(kv => $"'{kv.Key}' -> '{kv.Value}'"));
                    Console.WriteLine($"  Gruppierung:     Spalte {GetExcelColumnLetter(current.GroupByColumnIndex)} ({rules})");
                }
                else
                {
                    Console.WriteLine("  Gruppierung:     Nein");
                }

                var key = Console.ReadKey(intercept: true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        if (selectedIndex > 0)
                            selectedIndex--;
                        break;

                    case ConsoleKey.DownArrow:
                        if (selectedIndex < templates.Count - 1)
                            selectedIndex++;
                        break;

                    case ConsoleKey.Enter:
                        return templates[selectedIndex].Template;

                    case ConsoleKey.D:
                        File.Delete(templates[selectedIndex].FilePath);
                        templates.RemoveAt(selectedIndex);
                        if (templates.Count == 0)
                            return null;
                        if (selectedIndex >= templates.Count)
                            selectedIndex = templates.Count - 1;
                        break;

                    case ConsoleKey.Escape:
                        return null;
                }
            }
        }
        finally
        {
            Console.ResetColor();
            Console.CursorVisible = true;
        }
    }

    public static string GetExcelColumnLetter(int zeroBasedIndex)
    {
        int dividend = zeroBasedIndex + 1;
        string columnName = string.Empty;

        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }
}
