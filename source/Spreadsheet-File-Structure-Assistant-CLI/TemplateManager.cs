using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpreadsheetFileStructureAssistantCLI;

public sealed class GroupingRule
{
    public int GroupByColumnIndex { get; set; } = -1;
    public Dictionary<string, string> GroupValueToFolderMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

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
    public List<GroupingRule> GroupingRules { get; set; } = [];
    public bool SaveAsDocx { get; set; } = false;

    public List<GroupingRule> GetEffectiveGroupingRules()
    {
        if (GroupingRules.Count > 0)
            return GroupingRules;

        if (UseGrouping && GroupByColumnIndex >= 0 && GroupValueToFolderMap.Count > 0)
        {
            return
            [
                new GroupingRule
                {
                    GroupByColumnIndex = GroupByColumnIndex,
                    GroupValueToFolderMap = new Dictionary<string, string>(GroupValueToFolderMap, StringComparer.OrdinalIgnoreCase)
                }
            ];
        }

        return [];
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StructureTemplate))]
[JsonSerializable(typeof(GroupingRule))]
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

    public static (PickerOutcome Outcome, StructureTemplate? Template) PickTemplate(Action drawBanner, string stepIndicator = "")
    {
        var templates = LoadAllTemplates();
        if (templates.Count == 0)
            return (PickerOutcome.Cancelled, null);

        int selectedIndex = 0;

        Console.CursorVisible = false;
        try
        {
            while (true)
            {
                Console.Clear();
                drawBanner();

                if (!string.IsNullOrWhiteSpace(stepIndicator))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(stepIndicator);
                    Console.ResetColor();
                }

                Console.WriteLine("Gespeicherte Vorlage auswählen");
                Console.WriteLine("Steuerung: [Hoch/Runter] Wählen | [Enter] Verwenden | [D] Löschen | [Backspace] Zurück | [Esc] Abbruch");
                Console.WriteLine(new string('-', 90));

                for (int i = 0; i < templates.Count; i++)
                {
                    var tpl = templates[i].Template;
                    var rules = tpl.GetEffectiveGroupingRules();
                    string folderCols = string.Join(" + ", tpl.FolderNameColumnIndices.Select(GetExcelColumnLetter));
                    string groupInfo = rules.Count > 0
                        ? $" | Gruppen: {rules.Count} Ebene(n)"
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

                Console.WriteLine(new string('-', 90));
                var current = templates[selectedIndex].Template;
                var activeRules = current.GetEffectiveGroupingRules();

                Console.WriteLine($"Details zu '{current.TemplateName}':");
                Console.WriteLine($"  Titelzeile:      Zeile {current.HeaderRowIndex + 1}");
                Console.WriteLine($"  Ordner-Spalten:  {string.Join("_", current.FolderNameColumnIndices.Select(GetExcelColumnLetter))}");
                Console.WriteLine($"  Datendatei:      {current.TxtFileName} ({(current.SaveAsDocx ? ".docx" : ".txt")})");
                Console.WriteLine($"  Inhalt-Spalten:  {string.Join(", ", current.TxtContentColumnIndices.Select(GetExcelColumnLetter))}");

                if (activeRules.Count > 0)
                {
                    for (int g = 0; g < activeRules.Count; g++)
                    {
                        var r = activeRules[g];
                        string ruleSummary = string.Join(", ", r.GroupValueToFolderMap.Select(kv => $"'{kv.Key}' -> '{kv.Value}'"));
                        Console.WriteLine($"  Gruppierung {g + 1}:   Spalte {GetExcelColumnLetter(r.GroupByColumnIndex)} ({ruleSummary})");
                    }
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
                        return (PickerOutcome.Selected, templates[selectedIndex].Template);

                    case ConsoleKey.D:
                        File.Delete(templates[selectedIndex].FilePath);
                        templates.RemoveAt(selectedIndex);
                        if (templates.Count == 0)
                            return (PickerOutcome.Cancelled, null);
                        if (selectedIndex >= templates.Count)
                            selectedIndex = templates.Count - 1;
                        break;

                    case ConsoleKey.Backspace:
                        return (PickerOutcome.Back, null);

                    case ConsoleKey.Escape:
                        return (PickerOutcome.Cancelled, null);
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