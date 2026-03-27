using System.Globalization;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;

namespace GetSpecifications.Specifications.Table2
{
    /// <summary>
    /// Строит таблицу «Спецификация элементов стоек эстакады» (таблица №2).
    /// Для каждого типа стойки из каталога Table2Catalog.json формируется группа строк:
    /// строка-заголовок с подчёркнутым названием, строки материалов с дробями и текстом.
    /// Стойки без шаблона в каталоге пропускаются.
    /// </summary>
    
    public sealed class SpecificationsRacksTable2Builder
    {
        private static readonly Table2CatalogRoot _catalog = Table2Catalog.Load();
        private readonly ParametricObjectPropertyReader _reader = new ParametricObjectPropertyReader();

        public bool TryBuild(
            List<McParametricObject> selectedObjects,
            Point3d insertPt,
            string steelGrade,
            out string message)
        {
            message = "";

            if (selectedObjects == null || selectedObjects.Count == 0)
            {
                message = "Не переданы выбранные параметрические объекты.";
                return false;
            }

            Dictionary<string, Table2SourceAggregate> aggregates = AggregateStoiki(selectedObjects);
            if (aggregates.Count == 0)
            {
                message = "В выборке нет стоек для формирования таблицы №2.";
                return false;
            }

            Dictionary<string, RackTypeTemplate> templates = _catalog.RackTypes
                .ToDictionary(x => x.TipStoiki, x => x, StringComparer.Ordinal);

            var rows = new List<Table2Row>();

            foreach (Table2SourceAggregate aggregate in aggregates.Values
                .OrderBy(x => x.TipStoiki, StringComparer.CurrentCulture))
            {
                if (!templates.TryGetValue(aggregate.TipStoiki, out RackTypeTemplate? template))
                    continue;

                rows.AddRange(BuildRowsForRackType(template, aggregate, steelGrade));
            }

            if (rows.Count == 0)
            {
                message = "В выборке нет стоек, для которых есть шаблоны в Table2Catalog.json.";
                return false;
            }

            SpecificationTableHelper.EnsureDrawingResources();

            McTable table = SpecificationTableHelper.CreateStandardTable(
                "Спецификация элементов стоек эстакады",
                rows.Count);

            int rowIndex = SpecificationTableLayout.FIRST_DATA_ROW;
            foreach (Table2Row row in rows)
            {
                FillDataRow(table, rowIndex, row);
                rowIndex++;
            }

            SpecificationTableHelper.ApplyStandardBorders(table, rows.Count);
            SpecificationTableHelper.AddTableToDocument(table, insertPt);

            message = "Таблица «Спецификация элементов стоек эстакады» сформирована.";
            return true;
        }

        private Dictionary<string, Table2SourceAggregate> AggregateStoiki(
            List<McParametricObject> selectedObjects)
        {
            var dict = new Dictionary<string, Table2SourceAggregate>(StringComparer.Ordinal);

            foreach (McParametricObject obj in selectedObjects)
            {
                ParametricObjectData data = _reader.Read(obj);

                if (!string.Equals(data.ObjectName, SpecificationObjectConstants.STOIKA_CLASS, StringComparison.Ordinal))
                    continue;

                if (string.Equals(data.Implementation, SpecificationObjectConstants.EXCLUDED_IMPL, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrWhiteSpace(data.TipStoiki))
                    continue;

                if (dict.TryGetValue(data.TipStoiki, out Table2SourceAggregate? rec))
                {
                    rec.Count++;
                    rec.TotalLen += data.DlinaStoikiM;
                }
                else
                {
                    dict[data.TipStoiki] = new Table2SourceAggregate
                    {
                        TipStoiki = data.TipStoiki,
                        Count = 1,
                        TotalLen = data.DlinaStoikiM
                    };
                }
            }

            return dict;
        }

        private List<Table2Row> BuildRowsForRackType(
            RackTypeTemplate template,
            Table2SourceAggregate aggregate,
            string steelGrade)
        {
            var result = new List<Table2Row>();

            foreach (RackRowTemplate templateRow in template.Rows)
            {
                switch (templateRow.Kind)
                {
                    case "title":
                        // Количество стоек выводится в колонке «Примечание», не в «Количество».
                        result.Add(new Table2Row
                        {
                            Position = "",
                            Obz = "",
                            Name = "=fmtUnderline(\"Стойка типа " + EscapeFormulaText(aggregate.TipStoiki) + "\")",
                            Quantity = "",
                            Mass = "",
                            Note = aggregate.Count.ToString(CultureInfo.InvariantCulture) + " шт."
                        });
                        break;

                    case "text":
                        result.Add(new Table2Row
                        {
                            Position = templateRow.Position,
                            Obz = "",
                            Name = ReplaceTokens(templateRow.Name, aggregate, steelGrade),
                            Quantity = ReplaceTokens(templateRow.Quantity, aggregate, steelGrade),
                            Mass = templateRow.Mass,
                            Note = templateRow.Note
                        });
                        break;

                    case "fraction":
                        result.Add(new Table2Row
                        {
                            Position = templateRow.Position,
                            Obz = "",
                            Name = BuildFractionNameText(
                                           ReplaceTokens(templateRow.Top, aggregate, steelGrade),
                                           ReplaceTokens(templateRow.BottomTemplate, aggregate, steelGrade)),
                            Quantity = ReplaceTokens(templateRow.Quantity, aggregate, steelGrade),
                            Mass = templateRow.Mass,
                            Note = templateRow.Note
                        });
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Неизвестный kind '{templateRow.Kind}' для стойки '{template.TipStoiki}'.");
                }
            }

            return result;
        }

        private void FillDataRow(McTable table, int row, Table2Row data)
        {
            SpecificationTableHelper.SetStandardCell(table, row, 0, data.Position, HorizTextAlign.Center);
            // Колонка «Обозначение» (col 1) намеренно оставляется пустой в таблице №2 по ТЗ.
            SpecificationTableHelper.SetStandardCell(table, row, 1, data.Obz, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 2, data.Name, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 3, data.Quantity, HorizTextAlign.Center);
            // Масса оборачивается в fmtRaw, чтобы таблица не обрезала конечный ноль (47,20 → 47,2).
            SpecificationTableHelper.SetStandardCell(table, row, 4, WrapAsRawTextFormula(data.Mass), HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 5, data.Note, HorizTextAlign.Center);
        }

        private string ReplaceTokens(
            string value,
            Table2SourceAggregate aggregate,
            string steelGrade)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value
                .Replace("{steel}", steelGrade)
                .Replace("{tipStoiki}", aggregate.TipStoiki)
                .Replace("{count}", aggregate.Count.ToString(CultureInfo.InvariantCulture))
                .Replace("{totalLen}", FormatDecimal2(aggregate.TotalLen));
        }

        // Формирует формулу таблицы вида: =fmtText("Лист "; fmtDiv("верх"; "низ"; True))
        private string BuildFractionNameText(string top, string bottom)
        {
            top = EscapeFormulaText(top);
            bottom = EscapeFormulaText(bottom);
            return "=fmtText(\"Лист \"; fmtDiv(\"" + top + "\"; \"" + bottom + "\"; True))";
        }

        // fmtRaw запрещает таблице интерпретировать содержимое как формулу или усекать нули.
        private string WrapAsRawTextFormula(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return "=fmtRaw(\"" + EscapeFormulaText(value) + "\")";
        }

        // Двойные кавычки экранируются удвоением согласно синтаксису формул таблицы nanoCAD.
        private string EscapeFormulaText(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Replace("\"", "\"\"");
        }

        private string FormatDecimal2(double value)
            => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}