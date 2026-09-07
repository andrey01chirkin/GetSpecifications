using System.Globalization;
using Multicad.Geometry;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;

namespace GetSpecifications.Specifications.Table7
{
    /// <summary>
    /// Строит таблицу «Спецификация элементов покрытия эстакады» (таблица №7).
    /// Все строки фиксированы и берутся из Table7Catalog.json; переменны только марка стали
    /// (токен {steel}) и суммарная длина покрытия (токен {coverLen}).
    /// Столбец «Обозначение» и ячейки «Кол.» заголовков блоков без значения заполняются пользователем вручную.
    /// </summary>
    public sealed class SpecificationsCoverTable7Builder
    {
        private const double COL_W_NAIM_WIDE = 80.0;
        private static readonly Table7CatalogRoot _catalog = CoverTable7Catalog.Load();

        private sealed class Table7Row
        {
            public string Position { get; set; } = "";
            public string Name { get; set; } = "";
            public string Quantity { get; set; } = "";
            public string Mass { get; set; } = "";
            public string Note { get; set; } = "";
            public bool IsTitle { get; set; }
        }

        public void Build(double coverLenM, Point3d insertPt, string steelGrade)
        {
            List<Table7Row> rows = BuildRows(coverLenM, steelGrade);

            SpecificationTableHelper.EnsureDrawingResources();

            McTable table = SpecificationTableHelper.CreateStandardTable(
                "Спецификация элементов покрытия эстакады",
                rows.Count);

            // Столбец 2 — «Наименование». Ширина задана только для этой таблицы,
            // общая константа COL_W_NAIM у таблиц №1, №2, №4 не меняется.
            table.Columns[2].Width = COL_W_NAIM_WIDE;

            int rowIndex = SpecificationTableLayout.FIRST_DATA_ROW;
            foreach (Table7Row row in rows)
            {
                FillDataRow(table, rowIndex, row);
                rowIndex++;
            }

            SpecificationTableHelper.ApplyStandardBorders(table, rows.Count);
            SpecificationTableHelper.AddTableToDocument(table, insertPt);
        }

        private List<Table7Row> BuildRows(double coverLenM, string steelGrade)
        {
            string coverLen = coverLenM.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
            var result = new List<Table7Row>();

            foreach (Table7RowTemplate t in _catalog.Rows)
            {
                string quantity = t.Quantity.Replace("{coverLen}", coverLen);
                string note = BuildNoteText(t);

                switch (t.Kind)
                {
                    case "empty":
                        result.Add(new Table7Row());
                        break;

                    case "title":
                        result.Add(new Table7Row
                        {
                            Name = "=fmtUnderline(\"" + EscapeFormulaText(t.Name) + "\")",
                            Quantity = quantity,
                            Mass = WrapAsRawTextFormula(t.Mass),
                            Note = note,
                            IsTitle = true
                        });
                        break;

                    case "text":
                        result.Add(new Table7Row
                        {
                            Position = t.Position,
                            Name = t.Name,
                            Quantity = quantity,
                            Mass = WrapAsRawTextFormula(t.Mass),
                            Note = note
                        });
                        break;

                    case "fraction":
                        result.Add(new Table7Row
                        {
                            Position = t.Position,
                            Name = BuildFractionNameText(t, steelGrade),
                            Quantity = quantity,
                            Mass = WrapAsRawTextFormula(t.Mass),
                            Note = note
                        });
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Неизвестный kind '{t.Kind}' в Table7Catalog.json.");
                }
            }

            return result;
        }

        // Двухстрочное примечание: верх и низ дроби без черты — =fmtDiv("верх"; "низ"; False).
        // Двухстрочное примечание: верх и низ дроби без черты — =fmtDiv("верх"; "низ"; False).
        private string BuildNoteText(Table7RowTemplate t)
        {
            if (string.IsNullOrEmpty(t.NoteBottom))
                return t.Note;

            return $"=fmtDiv(\"{EscapeFormulaText(t.NoteTop)}\"; \"{EscapeFormulaText(t.NoteBottom)}\"; False)";
        }

        private void FillDataRow(McTable table, int row, Table7Row data)
        {
            SpecificationTableHelper.SetStandardCell(table, row, 0, data.Position, HorizTextAlign.Center);
            // Колонка «Обозначение» в автоматическом режиме не заполняется (ТЗ п. 2.2.2).
            SpecificationTableHelper.SetStandardCell(table, row, 1, "", HorizTextAlign.Center);

            if (data.IsTitle)
                SpecificationTableHelper.SetStandardCell(table, row, 2, data.Name, HorizTextAlign.Center);
            else
                SpecificationTableHelper.SetStandardCell(table, row, 2, data.Name, HorizTextAlign.Left, indent: 1.0);

            SpecificationTableHelper.SetStandardCell(table, row, 3, data.Quantity, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 4, data.Mass, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 5, data.Note, HorizTextAlign.Center);
        }

        // =fmtText("Prefix"; fmtDiv("top"; "bottom"; True)[; "suffix"])
        private string BuildFractionNameText(Table7RowTemplate t, string steelGrade)
        {
            string prefix = EscapeFormulaText(t.Prefix);
            string top = EscapeFormulaText(t.Top);
            string bottom = EscapeFormulaText(t.Bottom.Replace("{steel}", steelGrade));

            if (string.IsNullOrEmpty(t.Suffix))
                return $"=fmtText(\"{prefix}\"; fmtDiv(\"{top}\"; \"{bottom}\"; True))";

            string suffix = EscapeFormulaText(t.Suffix);
            return $"=fmtText(\"{prefix}\"; fmtDiv(\"{top}\"; \"{bottom}\"; True); \"{suffix}\")";
        }

        // fmtRaw запрещает таблице усекать конечные нули (2,20 → 2,2).
        private string WrapAsRawTextFormula(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return "=fmtRaw(\"" + EscapeFormulaText(value) + "\")";
        }

        private string EscapeFormulaText(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Replace("\"", "\"\"");
        }
    }
}