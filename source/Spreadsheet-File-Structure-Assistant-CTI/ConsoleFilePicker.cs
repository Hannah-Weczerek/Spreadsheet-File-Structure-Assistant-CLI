namespace SpreadsheetFileStructureAssistantCTI;

public static class ConsoleFilePicker
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".ods",
        ".xls",
        ".fods"
    };

    private sealed class TreeNode
    {
        public string Name { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public bool IsDirectory { get; init; }
        public int Depth { get; init; }
        public bool IsExpanded { get; set; }
    }

    public static string? PickSpreadsheet(Action drawBanner)
    {
        var nodes = BuildInitialRoots();
        if (nodes.Count == 0)
            return null;

        int selectedIndex = 0;
        int scrollOffset = 0;

        Console.CursorVisible = false;
        try
        {
            while (true)
            {
                Console.Clear();
                drawBanner();

                Console.WriteLine("Spreadsheet auswählen (.xlsx / .ods / .fods / .xls)");
                Console.WriteLine("Steuerung: [Up/Down] Bewegen  |  [Enter] Öffnen/Schließen/Speichern  |  [Esc] Abbrechen");
                Console.WriteLine(new string('-', 76));

                int visibleRows = Math.Max(5, Console.WindowHeight - 10);

                if (selectedIndex < scrollOffset)
                    scrollOffset = selectedIndex;
                else if (selectedIndex >= scrollOffset + visibleRows)
                    scrollOffset = selectedIndex - visibleRows + 1;

                int endIndex = Math.Min(nodes.Count, scrollOffset + visibleRows);

                for (int i = scrollOffset; i < endIndex; i++)
                {
                    var node = nodes[i];
                    string indent = new(' ', node.Depth * 3);
                    string icon = node.IsDirectory
                        ? (node.IsExpanded ? "[-] " : "[+] ")
                        : "    ";

                    string line = $"{indent}{icon}{node.Name}";

                    // Visueller Tweak
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
                        Console.ForegroundColor = node.IsDirectory ? ConsoleColor.Cyan : ConsoleColor.Gray;
                        Console.WriteLine(line);
                        Console.ResetColor();
                    }
                }

                Console.WriteLine(new string('-', 76));
                Console.WriteLine($"Path: {nodes[selectedIndex].FullPath}");

                var key = Console.ReadKey(intercept: true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        if (selectedIndex > 0)
                            selectedIndex--;
                        break;

                    case ConsoleKey.DownArrow:
                        if (selectedIndex < nodes.Count - 1)
                            selectedIndex++;
                        break;

                    case ConsoleKey.Enter:
                        var current = nodes[selectedIndex];
                        if (!current.IsDirectory)
                        {
                            return current.FullPath;
                        }

                        if (current.IsExpanded)
                        {
                            CollapseNode(nodes, selectedIndex);
                        }
                        else
                        {
                            ExpandNode(nodes, selectedIndex);
                        }
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

    private static List<TreeNode> BuildInitialRoots()
    {
        var roots = new List<TreeNode>();
        var seenPaths = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var candidates = new (string Label, string Path)[]
        {
            ("Home", userHome),
            ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("Downloads", Path.Combine(userHome, "Downloads")),
            ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            ("Current Folder", Directory.GetCurrentDirectory())
        };

        foreach (var (label, path) in candidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && seenPaths.Add(path))
            {
                roots.Add(new TreeNode
                {
                    Name = $"{label} ({Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))})",
                    FullPath = path,
                    IsDirectory = true,
                    Depth = 0,
                    IsExpanded = false
                });
            }
        }

        return roots;
    }

    private static void ExpandNode(List<TreeNode> nodes, int index)
    {
        var parent = nodes[index];
        var children = new List<TreeNode>();

        try
        {
            var dirInfo = new DirectoryInfo(parent.FullPath);

            // 1. Überspringt z.B. .git oder .config)
            foreach (var dir in dirInfo.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (dir.Name.StartsWith('.') || (dir.Attributes & FileAttributes.Hidden) != 0)
                    continue;

                children.Add(new TreeNode
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    Depth = parent.Depth + 1,
                    IsExpanded = false
                });
            }

            // 2. Nur erlaubte Dateien anzeigen (.xlsx, .ods, etc.)
            foreach (var file in dirInfo.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (file.Name.StartsWith('.') || (file.Attributes & FileAttributes.Hidden) != 0)
                    continue;

                if (AllowedExtensions.Contains(file.Extension))
                {
                    children.Add(new TreeNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        Depth = parent.Depth + 1
                    });
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Gesperrte Ordner werden ignoriert
        }
        catch (IOException)
        {
            // Unlesbare Verzeichnisse werden still ignoriert
        }

        parent.IsExpanded = true;
        if (children.Count > 0)
        {
            nodes.InsertRange(index + 1, children);
        }
    }

    private static void CollapseNode(List<TreeNode> nodes, int index)
    {
        var parent = nodes[index];
        parent.IsExpanded = false;

        int removeStart = index + 1;
        int removeCount = 0;

        while (removeStart + removeCount < nodes.Count && nodes[removeStart + removeCount].Depth > parent.Depth)
        {
            removeCount++;
        }

        if (removeCount > 0)
        {
            nodes.RemoveRange(removeStart, removeCount);
        }
    }
}
