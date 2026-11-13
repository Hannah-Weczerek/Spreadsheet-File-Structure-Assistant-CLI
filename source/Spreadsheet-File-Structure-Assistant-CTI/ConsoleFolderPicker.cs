namespace SpreadsheetFileStructureAssistantCTI;

public static class ConsoleFolderPicker
{
    private sealed class FolderNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public int Depth { get; init; }
        public bool IsExpanded { get; set; }
    }

    public static string? PickDestinationFolder(Action drawBanner)
    {
        var nodes = BuildInitialRoots();
        if (nodes.Count == 0)
            return null;

        int selectedIndex = 0;
        int scrollOffset = 0;
        string statusMessage = string.Empty;

        Console.CursorVisible = false;
        try
        {
            while (true)
            {
                Console.Clear();
                drawBanner();

                Console.WriteLine("Schritt 2: Zielordner für die Dateistruktur auswählen");
                Console.WriteLine("Steuerung: [Hoch/Runter] Navigieren | [Enter] Auf-/Zuklappen | [Space] Auswählen");
                Console.WriteLine("           [N] Neuer Ordner         | [R] Umbenennen         | [Esc] Abbrechen");
                Console.WriteLine(new string('-', 80));

                int visibleRows = Math.Max(5, Console.WindowHeight - 12);

                if (selectedIndex < scrollOffset)
                    scrollOffset = selectedIndex;
                else if (selectedIndex >= scrollOffset + visibleRows)
                    scrollOffset = selectedIndex - visibleRows + 1;

                int endIndex = Math.Min(nodes.Count, scrollOffset + visibleRows);

                for (int i = scrollOffset; i < endIndex; i++)
                {
                    var node = nodes[i];
                    string indent = new(' ', node.Depth * 3);
                    string icon = node.IsExpanded ? "[-] " : "[+] ";
                    string line = $"{indent}{icon}{node.Name}";

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

                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"Aktueller Pfad: {nodes[selectedIndex].FullPath}");
                if (!string.IsNullOrEmpty(statusMessage))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(statusMessage);
                    Console.ResetColor();
                }

                var key = Console.ReadKey(intercept: true).Key;
                statusMessage = string.Empty;

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
                        if (nodes[selectedIndex].IsExpanded)
                            CollapseNode(nodes, selectedIndex);
                        else
                            ExpandNode(nodes, selectedIndex);
                        break;

                    case ConsoleKey.Spacebar:
                        return nodes[selectedIndex].FullPath;

                    case ConsoleKey.N:
                        statusMessage = CreateNewSubfolder(nodes, ref selectedIndex);
                        break;

                    case ConsoleKey.R:
                        statusMessage = RenameFolder(nodes, selectedIndex);
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

    private static string CreateNewSubfolder(List<FolderNode> nodes, ref int selectedIndex)
    {
        var parent = nodes[selectedIndex];
        Console.CursorVisible = true;
        Console.Write($"\nName für neuen Unterordner in '{parent.Name}': ");
        string? newName = Console.ReadLine()?.Trim();
        Console.CursorVisible = false;

        if (string.IsNullOrWhiteSpace(newName))
            return "Erstellen abgebrochen (leerer Name).";

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Fehler: Ungültige Zeichen im Ordnernamen.";

        string newFullPath = Path.Combine(parent.FullPath, newName);
        if (Directory.Exists(newFullPath))
            return $"Fehler: '{newName}' existiert bereits.";

        try
        {
            Directory.CreateDirectory(newFullPath);

            // Überordner frisch aufklappen, damit der neue Ordner sofort sichtbar ist
            if (parent.IsExpanded)
                CollapseNode(nodes, selectedIndex);

            ExpandNode(nodes, selectedIndex);

            int newIdx = nodes.FindIndex(n => n.FullPath == newFullPath);
            if (newIdx >= 0)
                selectedIndex = newIdx;

            return $"Ordner '{newName}' erstellt.";
        }
        catch (Exception ex)
        {
            return $"Fehler beim Erstellen: {ex.Message}";
        }
    }

    private static string RenameFolder(List<FolderNode> nodes, int selectedIndex)
    {
        var target = nodes[selectedIndex];
        if (target.Depth == 0)
            return "Hinweis: Haupt-Einstiegsordner (Home, Documents, ...) können nicht umbenannt werden.";

        string? parentDir = Path.GetDirectoryName(target.FullPath);
        if (string.IsNullOrEmpty(parentDir))
            return "Fehler: Übergeordneter Pfad nicht gefunden.";

        Console.CursorVisible = true;
        Console.Write($"\nNeuer Name für '{target.Name}': ");
        string? newName = Console.ReadLine()?.Trim();
        Console.CursorVisible = false;

        if (string.IsNullOrWhiteSpace(newName) || newName == target.Name)
            return "Umbenennen abgebrochen.";

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Fehler: Ungültige Zeichen im Ordnernamen.";

        string newFullPath = Path.Combine(parentDir, newName);
        if (Directory.Exists(newFullPath))
            return $"Fehler: Ein Ordner namens '{newName}' existiert bereits.";

        try
        {
            bool wasExpanded = target.IsExpanded;
            if (wasExpanded)
                CollapseNode(nodes, selectedIndex);

            Directory.Move(target.FullPath, newFullPath);
            target.Name = newName;
            target.FullPath = newFullPath;

            if (wasExpanded)
                ExpandNode(nodes, selectedIndex);

            return $"Umbenannt zu '{newName}'.";
        }
        catch (Exception ex)
        {
            return $"Fehler beim Umbenennen: {ex.Message}";
        }
    }

    private static List<FolderNode> BuildInitialRoots()
    {
        var roots = new List<FolderNode>();
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
                roots.Add(new FolderNode
                {
                    Name = $"{label} ({Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))})",
                    FullPath = path,
                    Depth = 0,
                    IsExpanded = false
                });
            }
        }

        return roots;
    }

    private static void ExpandNode(List<FolderNode> nodes, int index)
    {
        var parent = nodes[index];
        var children = new List<FolderNode>();

        try
        {
            var dirInfo = new DirectoryInfo(parent.FullPath);
            foreach (var dir in dirInfo.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (dir.Name.StartsWith('.') || (dir.Attributes & FileAttributes.Hidden) != 0)
                    continue;

                children.Add(new FolderNode
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    Depth = parent.Depth + 1,
                    IsExpanded = false
                });
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        parent.IsExpanded = true;
        if (children.Count > 0)
        {
            nodes.InsertRange(index + 1, children);
        }
    }

    private static void CollapseNode(List<FolderNode> nodes, int index)
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