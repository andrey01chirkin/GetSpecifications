using System.Globalization;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;

namespace GetSpecifications.Specifications.Table4
{
    /// <summary>
    /// Строит таблицу «Спецификация элементов балок эстакады» (таблица №4).
    /// Форма 7 ГОСТ 21.101.2020: стандартные 6 колонок, те же размеры, что у таблиц №1 и №2.
    /// Строки с нулевым количеством в таблицу не включаются.
    /// </summary>
    public sealed class SpecificationsBeamsTable4Builder
    {
        // Таблица В ТЗ: длина поз. «а» в метрах по номеру сечения (индекс = beamNumber).
        // Сечения 1, 2, 10 не имеют поз. «а», их значения равны 0.
        private static readonly double[] LenA = { 0, 0, 0, 0.84, 1.01, 1.21, 1.41, 1.61, 1.81, 2.01, 0 };

        // Таблица Г ТЗ: количество уголков на метр по номеру сечения (индекс = beamNumber).
        // Сечение 10 не имеет поз. «б», его значение равно 0.
        private static readonly int[] CntB = { 0, 2, 2, 3, 3, 4, 4, 4, 4, 4, 0 };

        private static readonly Table4CatalogRoot _catalog = BeamsTable4Catalog.Load();
        private readonly BeamObjectPropertyReader _reader = new BeamObjectPropertyReader();

        private sealed class BeamRecord
        {
            public string SName { get; set; } = "";
            public int BeamNumber { get; set; }
            public int LetterIndex { get; set; }
            public int Count { get; set; }
            public double TotalLenM { get; set; }
            // Атрибут rDlinaTraversy присутствует только у Балки 10-10 — нужен для расчёта поз. «г».
            public double DlinaTraversyM { get; set; }
        }

        private sealed class Table4Row
        {
            public string Position { get; set; } = "";
            public string Name { get; set; } = "";
            public string Quantity { get; set; } = "";
            public string Mass { get; set; } = "";
            public string Note { get; set; } = "";
        }

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

            Dictionary<string, BeamRecord> dict = AggregateBeams(selectedObjects);
            if (dict.Count == 0)
            {
                message = "В выборке нет балок с распознанным атрибутом «sName».";
                return false;
            }

            List<BeamRecord> records = dict.Values
                .OrderBy(r => r.BeamNumber)
                .ThenBy(r => r.LetterIndex)
                .ToList();

            // Метраж швеллеров: TotalLenM × ChannelCount, суммируется по одному буквенному индексу.
            double[] channelQty = new double[12];
            // Количество торцевых пластин: Count × ChannelCount, суммируется аналогично.
            int[] plateQty = new int[12];

            foreach (BeamRecord rec in records)
            {
                int ch = BeamSectionHelper.GetChannelCount(rec.BeamNumber);
                channelQty[rec.LetterIndex] += rec.TotalLenM * ch;
                plateQty[rec.LetterIndex] += rec.Count * ch;
            }

            double totalA = 0;
            int totalB = 0;
            double totalG = 0;

            foreach (BeamRecord rec in records)
            {
                int bn = rec.BeamNumber;
                // K = ceil(TotalLenM) — шаг установки элементов равен 1 м.
                int k = (int)Math.Ceiling(rec.TotalLenM);

                if (bn >= 3 && bn <= 9)
                    totalA += k * 2 * LenA[bn];

                if (bn >= 1 && bn <= 9)
                    totalB += k * CntB[bn];

                if (bn == 10)
                    totalG += k * (rec.DlinaTraversyM + 0.2);
            }

            int totalV = totalB * 2;

            List<Table4Row> rows = BuildRows(channelQty, plateQty, totalA, totalB, totalV, totalG, steelGrade);

            if (rows.Count == 0)
            {
                message = "Нет данных для формирования таблицы №4.";
                return false;
            }

            SpecificationTableHelper.EnsureDrawingResources();

            McTable table = SpecificationTableHelper.CreateStandardTable(
                "Спецификация элементов балок эстакады",
                rows.Count);

            int rowIndex = SpecificationTableLayout.FIRST_DATA_ROW;
            foreach (Table4Row row in rows)
            {
                FillDataRow(table, rowIndex, row);
                rowIndex++;
            }

            SpecificationTableHelper.ApplyStandardBorders(table, rows.Count);
            SpecificationTableHelper.AddTableToDocument(table, insertPt);

            message = "Таблица «Спецификация элементов балок эстакады» сформирована.";
            return true;
        }

        // === Агрегация ===

        private Dictionary<string, BeamRecord> AggregateBeams(List<McParametricObject> objects)
        {
            var dict = new Dictionary<string, BeamRecord>(StringComparer.Ordinal);

            foreach (McParametricObject obj in objects)
            {
                BeamObjectData data = _reader.Read(obj);

                if (string.IsNullOrWhiteSpace(data.SName)) continue;
                if (!BeamSectionHelper.TryParseSectionName(data.SName, out int beamNumber, out int letterIndex)) continue;

                if (dict.TryGetValue(data.SName, out BeamRecord? rec))
                {
                    rec.Count++;
                    rec.TotalLenM += data.RL / 10.0;
                }
                else
                {
                    dict[data.SName] = new BeamRecord
                    {
                        SName = data.SName,
                        BeamNumber = beamNumber,
                        LetterIndex = letterIndex,
                        Count = 1,
                        TotalLenM = data.RL / 10.0,
                        // DlinaTraversyM берётся от первого объекта в группе — внутри группы значение одинаково.
                        DlinaTraversyM = data.DlinaTraversyM
                    };
                }
            }

            return dict;
        }

        // === Сборка строк ===

        private List<Table4Row> BuildRows(
            double[] channelQty,
            int[] plateQty,
            double totalA,
            int totalB,
            int totalV,
            double totalG,
            string steelGrade)
        {
            var result = new List<Table4Row>();

            foreach (Table4RowTemplate catalogRow in _catalog.Rows)
            {
                string pos = catalogRow.Position;
                string name = BuildNameFormula(catalogRow, steelGrade);
                string mass = WrapAsRawTextFormula(catalogRow.Mass);

                if (int.TryParse(pos, out int posNum) && posNum >= 1 && posNum <= 12)
                {
                    double qty = channelQty[posNum - 1];
                    if (qty <= 0) continue;

                    result.Add(new Table4Row { Position = pos, Name = name, Quantity = FormatDecimal2(qty), Mass = mass, Note = catalogRow.Note });
                    continue;
                }

                if (pos.StartsWith("п-", StringComparison.Ordinal) &&
                    int.TryParse(pos.Substring(2), out int pNum) && pNum >= 1 && pNum <= 12)
                {
                    int qty = plateQty[pNum - 1];
                    if (qty <= 0) continue;

                    result.Add(new Table4Row { Position = pos, Name = name, Quantity = qty.ToString(CultureInfo.InvariantCulture), Mass = mass, Note = catalogRow.Note });
                    continue;
                }

                switch (pos)
                {
                    case "а":
                        if (totalA <= 0) break;
                        result.Add(new Table4Row { Position = pos, Name = name, Quantity = FormatDecimal2(totalA), Mass = mass, Note = catalogRow.Note });
                        break;

                    case "б":
                        if (totalB <= 0) break;
                        result.Add(new Table4Row { Position = pos, Name = name, Quantity = totalB.ToString(CultureInfo.InvariantCulture), Mass = mass, Note = catalogRow.Note });
                        break;

                    case "в":
                        if (totalV <= 0) break;
                        result.Add(new Table4Row { Position = pos, Name = name, Quantity = totalV.ToString(CultureInfo.InvariantCulture), Mass = mass, Note = catalogRow.Note });
                        break;

                    case "г":
                        if (totalG <= 0) break;
                        result.Add(new Table4Row { Position = pos, Name = name, Quantity = FormatDecimal2(totalG), Mass = mass, Note = catalogRow.Note });
                        break;
                }
            }

            return result;
        }

        private void FillDataRow(McTable table, int row, Table4Row data)
        {
            SpecificationTableHelper.SetStandardCell(table, row, 0, data.Position, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 1, "", HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 2, data.Name, HorizTextAlign.Left, indent: 1.0);
            SpecificationTableHelper.SetStandardCell(table, row, 3, data.Quantity, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 4, data.Mass, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 5, data.Note, HorizTextAlign.Center);
        }

        // === Построение формулы наименования ===

        // Аналог BuildFractionNameText из Table2Builder.
        // suffix непустой только у поз. «б» и «г» — добавляется третьим аргументом в fmtText.
        private string BuildNameFormula(Table4RowTemplate catalogRow, string steelGrade)
        {
            string prefix = EscapeFormulaText(catalogRow.Prefix);
            string top = EscapeFormulaText(catalogRow.Top);
            string bottom = EscapeFormulaText("С" + steelGrade + " ГОСТ 27772-2021");

            if (string.IsNullOrEmpty(catalogRow.Suffix))
                return $"=fmtText(\"{prefix}\"; fmtDiv(\"{top}\"; \"{bottom}\"; True))";

            string suffix = EscapeFormulaText(catalogRow.Suffix);
            return $"=fmtText(\"{prefix}\"; fmtDiv(\"{top}\"; \"{bottom}\"; True); \"{suffix}\")";
        }

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

        private string FormatDecimal2(double value)
            => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}