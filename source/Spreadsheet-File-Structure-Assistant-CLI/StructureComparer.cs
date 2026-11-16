namespace SpreadsheetFileStructureAssistantCLI;

public static class StructureComparer
{
    private sealed record IndexedFile(
        string FullPath,
        string RelativePath,
        string FileNameWithoutExt,
        string ParentFolderName,
        string Extension);

    private sealed record DuplicateCandidate(
        IndexedFile FileA,
        IndexedFile FileB,
        double Similarity,
        string MatchReason);

    public static void RunCompareFlow(Action drawBanner)
    {
        const int totalSteps = 3;
        int step = 1;

        string? folderA = null;
        string? folderB = null;

        while (step <= totalSteps)
        {
            if (step == 1)
            {
                var res = ConsoleFolderPicker.PickDestinationFolder(
                    drawBanner,
                    $"Schritt 1 / {totalSteps}: Erste Dateistruktur auswählen",
                    "Wählen Sie den Hauptordner der ERSTEN Dateistruktur aus:");

                if (res.Outcome == PickerOutcome.Back || res.Outcome == PickerOutcome.Cancelled || res.FolderPath is null)
                    return;

                folderA = res.FolderPath;
                step = 2;
            }
            else if (step == 2)
            {
                var res = ConsoleFolderPicker.PickDestinationFolder(
                    drawBanner,
                    $"Schritt 2 / {totalSteps}: Zweite Dateistruktur auswählen",
                    "Wählen Sie den Hauptordner der ZWEITEN Dateistruktur aus:");

                if (res.Outcome == PickerOutcome.Back)
                {
                    step = 1;
                    continue;
                }
                if (res.Outcome == PickerOutcome.Cancelled || res.FolderPath is null)
                    return;

                if (string.Equals(Path.GetFullPath(folderA!), Path.GetFullPath(res.FolderPath), StringComparison.OrdinalIgnoreCase))
                {
                    Console.Clear();
                    drawBanner();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Fehler: Sie haben zweimal denselben Ordner ausgewählt.");
                    Console.ResetColor();
                    Console.WriteLine("Drücken Sie eine Taste, um einen anderen zweiten Ordner zu wählen...");
                    Console.ReadKey(intercept: true);
                    continue;
                }

                folderB = res.FolderPath;
                step = 3;
            }
            else if (step == 3)
            {
                bool backRequested = ExecuteComparisonAndReview(drawBanner, folderA!, folderB!, totalSteps);
                if (backRequested)
                {
                    step = 2;
                    continue;
                }
                break;
            }
        }
    }

    private static bool ExecuteComparisonAndReview(Action drawBanner, string rootA, string rootB, int totalSteps)
    {
        Console.Clear();
        drawBanner();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Schritt 3 / {totalSteps}: Analyse und Duplikat-Prüfung");
        Console.ResetColor();
        Console.WriteLine("Scanne Verzeichnisse und vergleiche .txt / .docx Dateien...");

        var dirsA = GetRelativeDirectories(rootA);
        var dirsB = GetRelativeDirectories(rootB);

        var filesA = IndexDataFiles(rootA);
        var filesB = IndexDataFiles(rootB);

        int matchingDirCount = dirsA.Intersect(dirsB, StringComparer.OrdinalIgnoreCase).Count();
        var candidates = FindDuplicateCandidates(filesA, filesB);

        Console.Clear();
        drawBanner();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Schritt 3 / {totalSteps}: Vergleichsergebnis");
        Console.ResetColor();
        Console.WriteLine(new string('-', 86));
        Console.WriteLine($"Struktur A: {rootA}");
        Console.WriteLine($"            {dirsA.Count} Unterordner | {filesA.Count} Datendateien (.txt/.docx)");
        Console.WriteLine($"Struktur B: {rootB}");
        Console.WriteLine($"            {dirsB.Count} Unterordner | {filesB.Count} Datendateien (.txt/.docx)");
        Console.WriteLine(new string('-', 86));
        Console.WriteLine($"Identische relative Ordnerpfade: {matchingDirCount}");
        Console.ForegroundColor = candidates.Count > 0 ? ConsoleColor.Cyan : ConsoleColor.Green;
        Console.WriteLine($"Gefundene potenzielle Datei-Duplikate: {candidates.Count}");
        Console.ResetColor();
        Console.WriteLine(new string('-', 86));

        if (candidates.Count == 0)
        {
            Console.WriteLine("\nKeine übereinstimmenden oder sehr ähnlichen Dateien gefunden.");
            Console.WriteLine("Steuerung: [Backspace] Zurück zur Ordnerauswahl | [Beliebige andere Taste] Fertig");
            var key = Console.ReadKey(intercept: true).Key;
            return key == ConsoleKey.Backspace;
        }

        Console.WriteLine("\nMöchten Sie die potenziellen Duplikate jetzt Side-by-Side prüfen und bereinigen?");
        Console.WriteLine("[Y] Prüfung starten | [N] Beenden ohne Änderungen | [Backspace] Zurück");

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (keyInfo.Key == ConsoleKey.Backspace)
                return true;

            char ch = char.ToUpperInvariant(keyInfo.KeyChar);
            if (ch == 'N')
                return false;
            if (ch is 'Y' or 'J')
                break;
        }

        int resolvedCount = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var cand = candidates[i];
            if (!File.Exists(cand.FileA.FullPath) || !File.Exists(cand.FileB.FullPath))
                continue;

            int action = ShowSideBySideReview(drawBanner, cand, i + 1, candidates.Count);
            if (action == -1)
                break;

            if (action > 0)
                resolvedCount++;
        }

        Console.Clear();
        drawBanner();
        Console.WriteLine($"Überprüfung abgeschlossen. {resolvedCount} Konflikt(e) wurden bereinigt/zusammengeführt.");
        return false;
    }

    private static int ShowSideBySideReview(Action drawBanner, DuplicateCandidate cand, int currentIdx, int totalCount)
    {
        var linesA = DocumentHelper.ReadDocumentLines(cand.FileA.FullPath);
        var linesB = DocumentHelper.ReadDocumentLines(cand.FileB.FullPath);

        while (true)
        {
            Console.Clear();
            drawBanner();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Duplikat-Prüfung {currentIdx} / {totalCount}  |  Ähnlichkeit: {cand.Similarity:P0} ({cand.MatchReason})");
            Console.ResetColor();

            int totalWidth = Math.Max(76, Console.WindowWidth - 2);
            int halfWidth = (totalWidth - 3) / 2;

            string headerA = TruncatePad($"[A] {cand.FileA.ParentFolderName}/{Path.GetFileName(cand.FileA.FullPath)}", halfWidth);
            string headerB = TruncatePad($"[B] {cand.FileB.ParentFolderName}/{Path.GetFileName(cand.FileB.FullPath)}", halfWidth);

            Console.WriteLine(new string('=', totalWidth));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{headerA} | {headerB}");
            Console.ResetColor();
            Console.WriteLine(new string('-', totalWidth));

            int maxLines = Math.Min(18, Math.Max(linesA.Count, linesB.Count));
            if (maxLines == 0)
            {
                Console.WriteLine($"{TruncatePad("(Leere Datei)", halfWidth)} | {TruncatePad("(Leere Datei)", halfWidth)}");
            }
            else
            {
                for (int r = 0; r < maxLines; r++)
                {
                    string rawA = r < linesA.Count ? linesA[r] : string.Empty;
                    string rawB = r < linesB.Count ? linesB[r] : string.Empty;
                    bool isDiff = !string.Equals(rawA.Trim(), rawB.Trim(), StringComparison.Ordinal);

                    string colA = TruncatePad(rawA, halfWidth);
                    string colB = TruncatePad(rawB, halfWidth);

                    if (isDiff)
                        Console.ForegroundColor = ConsoleColor.Yellow;

                    Console.WriteLine($"{colA} | {colB}");

                    if (isDiff)
                        Console.ResetColor();
                }

                if (linesA.Count > 18 || linesB.Count > 18)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"... weitere Zeilen ausgeblendet (A: {linesA.Count} Zeilen, B: {linesB.Count} Zeilen) ...");
                    Console.ResetColor();
                }
            }

            Console.WriteLine(new string('=', totalWidth));
            Console.WriteLine("Aktion wählen:");
            Console.WriteLine("  1. Nur Datei A behalten (Datei B löschen)");
            Console.WriteLine("  2. Nur Datei B behalten (Datei A löschen)");
            Console.WriteLine("  3. Zusammenführen (Merge) in Datei A (Datei B löschen)");
            Console.WriteLine("  4. Zusammenführen (Merge) in Datei B (Datei A löschen)");
            Console.WriteLine("  5. Beide getrennt behalten (Überspringen)");
            Console.WriteLine("  [Esc] Prüfung abbrechen");
            Console.Write("\nAuswahl: ");

            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape)
                return -1;

            switch (key.KeyChar)
            {
                case '1':
                    SafeDeleteFileAndEmptyParent(cand.FileB.FullPath);
                    return 1;

                case '2':
                    SafeDeleteFileAndEmptyParent(cand.FileA.FullPath);
                    return 2;

                case '3':
                    MergeDocuments(cand.FileA.FullPath, linesA, linesB);
                    SafeDeleteFileAndEmptyParent(cand.FileB.FullPath);
                    return 3;

                case '4':
                    MergeDocuments(cand.FileB.FullPath, linesB, linesA);
                    SafeDeleteFileAndEmptyParent(cand.FileA.FullPath);
                    return 4;

                case '5':
                    return 0;
            }
        }
    }

    private static void MergeDocuments(string targetPath, List<string> primaryLines, List<string> secondaryLines)
    {
        var merged = new List<string>(primaryLines);
        var existingSet = new HashSet<string>(primaryLines.Select(l => l.Trim()), StringComparer.OrdinalIgnoreCase);

        var extraLines = secondaryLines
            .Where(l => !string.IsNullOrWhiteSpace(l) && !existingSet.Contains(l.Trim()))
            .ToList();

        if (extraLines.Count > 0)
        {
            merged.Add(string.Empty);
            merged.Add("--- Zusammengeführte Daten ---");
            merged.AddRange(extraLines);
        }

        bool isDocx = Path.GetExtension(targetPath).Equals(".docx", StringComparison.OrdinalIgnoreCase);
        DocumentHelper.WriteDocument(targetPath, merged, isDocx);
    }

    private static void SafeDeleteFileAndEmptyParent(string filePath)
    {
        try
        {
            string? parentDir = Path.GetDirectoryName(filePath);
            File.Delete(filePath);

            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
            {
                if (!Directory.EnumerateFileSystemEntries(parentDir).Any())
                {
                    Directory.Delete(parentDir);
                }
            }
        }
        catch
        {
        }
    }

    private static HashSet<string> GetRelativeDirectories(string rootPath)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string dir in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories))
            {
                set.Add(Path.GetRelativePath(rootPath, dir));
            }
        }
        catch
        {
        }
        return set;
    }

    private static List<IndexedFile> IndexDataFiles(string rootPath)
    {
        var list = new List<IndexedFile>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not (".txt" or ".docx"))
                    continue;

                string parentDir = Path.GetDirectoryName(file) ?? rootPath;
                string parentName = new DirectoryInfo(parentDir).Name;

                list.Add(new IndexedFile(
                    FullPath: file,
                    RelativePath: Path.GetRelativePath(rootPath, file),
                    FileNameWithoutExt: Path.GetFileNameWithoutExtension(file),
                    ParentFolderName: parentName,
                    Extension: ext));
            }
        }
        catch
        {
        }
        return list;
    }

    private static List<DuplicateCandidate> FindDuplicateCandidates(List<IndexedFile> filesA, List<IndexedFile> filesB)
    {
        var results = new List<DuplicateCandidate>();
        var matchedB = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in filesA)
        {
            DuplicateCandidate? bestMatch = null;

            foreach (var b in filesB)
            {
                if (matchedB.Contains(b.FullPath))
                    continue;

                double folderSim = CalculateSimilarity(a.ParentFolderName, b.ParentFolderName);
                double fileSim = CalculateSimilarity(a.FileNameWithoutExt, b.FileNameWithoutExt);

                double combinedScore = a.FileNameWithoutExt.Equals(b.FileNameWithoutExt, StringComparison.OrdinalIgnoreCase)
                    ? (folderSim * 0.75 + fileSim * 0.25)
                    : Math.Max(fileSim, (folderSim + fileSim) / 2.0);

                if (combinedScore >= 0.80)
                {
                    if (bestMatch is null || combinedScore > bestMatch.Similarity)
                    {
                        string reason = combinedScore >= 0.99
                            ? "Exakte Übereinstimmung"
                            : $"Ähnlicher Ordner/Dateiname ({folderSim:P0} / {fileSim:P0})";
                        bestMatch = new DuplicateCandidate(a, b, combinedScore, reason);
                    }
                }
            }

            if (bestMatch != null)
            {
                matchedB.Add(bestMatch.FileB.FullPath);
                results.Add(bestMatch);
            }
        }

        return results.OrderByDescending(r => r.Similarity).ToList();
    }

    private static double CalculateSimilarity(string s1, string s2)
    {
        string a = NormalizeToken(s1);
        string b = NormalizeToken(s2);

        if (a.Length == 0 && b.Length == 0)
            return 1.0;
        if (a.Length == 0 || b.Length == 0)
            return 0.0;
        if (a == b)
            return 1.0;

        int distance = LevenshteinDistance(a, b);
        int maxLen = Math.Max(a.Length, b.Length);
        return 1.0 - (double)distance / maxLen;
    }

    private static string NormalizeToken(string input)
    {
        return new string(input.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private static string TruncatePad(string text, int width)
    {
        string clean = text.Replace("\t", "    ").Replace("\r", "").Replace("\n", " ");
        if (clean.Length > width)
            return clean[..Math.Max(0, width - 3)] + "...";
        return clean.PadRight(width);
    }
}