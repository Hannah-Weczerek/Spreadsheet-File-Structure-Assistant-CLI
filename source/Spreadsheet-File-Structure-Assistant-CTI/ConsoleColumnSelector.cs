namespace SpreadsheetFileStructureAssistantCTI;

public static class ConsoleColumnSelector
{
    public static List<int>? SelectColumns(
        Action drawBanner,
        string title,
        string subtitle,
        IReadOnlyList<string> headers,
        IReadOnlyList<string>? sampleRow = null,
        string previewSeparator = "_",
        bool allowSelectAll = false)
    {
        if (headers.Count == 0)
            return null;

        int cursorIndex = 0;
        var selectedOrder = new List<int>();
        const int columnsPerRow = 3;

        Console.CursorVisible = false;
        try
        {
            while (true)
            {
                Console.Clear();
                drawBanner();

                Console.WriteLine(title);
                Console.WriteLine(subtitle);

                string controls = allowSelectAll
                    ? "Steuerung: [Pfeiltasten] Bewegen | [Space] Wählen | [A] Alle auswählen | [Enter] OK | [Esc] Abbruch"
                    : "Steuerung: [Pfeiltasten] Bewegen | [Space] Wählen | [Enter] OK | [Esc] Abbruch";

                Console.WriteLine(controls);
                Console.WriteLine(new string('-', 92));

                int totalRows = (int)Math.Ceiling(headers.Count / (double)columnsPerRow);
                int colWidth = Math.Max(22, (Console.WindowWidth - 6) / columnsPerRow);

                for (int r = 0; r < totalRows; r++)
                {
                    for (int c = 0; c < columnsPerRow; c++)
                    {
                        int idx = r * columnsPerRow + c;
                        if (idx >= headers.Count)
                        {
                            Console.Write(new string(' ', colWidth));
                            continue;
                        }

                        int orderPos = selectedOrder.IndexOf(idx);
                        bool isSelected = orderPos >= 0;
                        string checkMark = isSelected ? $"[{orderPos + 1}]" : "[ ]";
                        string label = $"{checkMark} {headers[idx]}";

                        if (label.Length > colWidth - 2)
                            label = label[..(colWidth - 5)] + "...";
                        else
                            label = label.PadRight(colWidth - 2);

                        if (idx == cursorIndex)
                        {
                            Console.BackgroundColor = ConsoleColor.White;
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.Write(label);
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = isSelected ? ConsoleColor.Green : ConsoleColor.Gray;
                            Console.Write(label);
                            Console.ResetColor();
                        }

                        if (c < columnsPerRow - 1)
                            Console.Write(" | ");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine(new string('-', 92));

                if (selectedOrder.Count == 0)
                {
                    Console.WriteLine("Vorschau: (Noch nichts ausgewhält - mindestens 1 Spalte wählen)");
                }
                else
                {
                    string templatePreview = string.Join(previewSeparator, selectedOrder.Select(i => headers[i]));
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"Vorschau: {templatePreview}");
                    Console.ResetColor();

                    if (sampleRow != null)
                    {
                        string examplePreview = string.Join(previewSeparator, selectedOrder.Select(i =>
                            i < sampleRow.Count && !string.IsNullOrWhiteSpace(sampleRow[i])
                                ? sampleRow[i].Trim()
                                : "LEER"));
                        Console.WriteLine($"Beispiel (Zeile 1): {examplePreview}");
                    }
                }

                var key = Console.ReadKey(intercept: true).Key;

                switch (key)
                {
                    case ConsoleKey.LeftArrow:
                        if (cursorIndex > 0)
                            cursorIndex--;
                        break;

                    case ConsoleKey.RightArrow:
                        if (cursorIndex < headers.Count - 1)
                            cursorIndex++;
                        break;

                    case ConsoleKey.UpArrow:
                        if (cursorIndex - columnsPerRow >= 0)
                            cursorIndex -= columnsPerRow;
                        break;

                    case ConsoleKey.DownArrow:
                        if (cursorIndex + columnsPerRow < headers.Count)
                            cursorIndex += columnsPerRow;
                        break;

                    case ConsoleKey.Spacebar:
                        if (selectedOrder.Contains(cursorIndex))
                            selectedOrder.Remove(cursorIndex);
                        else
                            selectedOrder.Add(cursorIndex);
                        break;

                    case ConsoleKey.A when allowSelectAll:
                        if (selectedOrder.Count == headers.Count)
                        {
                            selectedOrder.Clear();
                        }
                        else
                        {
                            selectedOrder.Clear();
                            selectedOrder.AddRange(Enumerable.Range(0, headers.Count));
                        }
                        break;

                    case ConsoleKey.Enter:
                        if (selectedOrder.Count > 0)
                            return selectedOrder;
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
}
