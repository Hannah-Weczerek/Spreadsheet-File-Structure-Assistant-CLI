namespace SpreadsheetFileStructureAssistantCLI;

public enum PickerOutcome
{
    Selected,
    Back,
    Cancelled
}

public sealed record ColumnSelectionResult(PickerOutcome Outcome, List<int> SelectedIndices);

public static class ConsoleColumnSelector
{
    public static ColumnSelectionResult SelectColumns(
        Action drawBanner,
        string stepIndicator,
        string title,
        string subtitle,
        IReadOnlyList<string> headers,
        IReadOnlyList<string>? sampleRow = null,
        string previewSeparator = "_",
        bool allowSelectAll = false,
        IReadOnlyList<int>? initialSelection = null)
    {
        if (headers.Count == 0)
            return new ColumnSelectionResult(PickerOutcome.Cancelled, []);

        int cursorIndex = 0;
        var selectedOrder = initialSelection != null
            ? new List<int>(initialSelection.Where(i => i >= 0 && i < headers.Count))
            : new List<int>();

        const int columnsPerRow = 3;

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

                Console.WriteLine(title);
                Console.WriteLine(subtitle);

                string controls = allowSelectAll
                    ? "Steuerung: [Pfeile] Bewegen | [Space] Wählen | [A] Alle | [Enter] Weiter | [Backspace] Zurück | [Esc] Abbruch"
                    : "Steuerung: [Pfeile] Bewegen | [Space] Wählen | [Enter] Weiter | [Backspace] Zurück | [Esc] Abbruch";

                Console.WriteLine(controls);
                Console.WriteLine(new string('-', Math.Max(60, Math.Min(100, Console.WindowWidth - 1))));

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

                Console.WriteLine(new string('-', Math.Max(60, Math.Min(100, Console.WindowWidth - 1))));

                if (selectedOrder.Count == 0)
                {
                    Console.WriteLine("Vorschau: (Noch nichts ausgewählt - mindestens 1 Spalte wählen)");
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
                            return new ColumnSelectionResult(PickerOutcome.Selected, selectedOrder);
                        break;

                    case ConsoleKey.Backspace:
                        return new ColumnSelectionResult(PickerOutcome.Back, []);

                    case ConsoleKey.Escape:
                        return new ColumnSelectionResult(PickerOutcome.Cancelled, []);
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