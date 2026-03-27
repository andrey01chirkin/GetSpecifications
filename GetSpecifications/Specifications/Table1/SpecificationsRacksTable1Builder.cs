using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;

namespace GetSpecifications.Specifications.Table1
{
    /// <summary>
    /// Строит таблицу «Спецификация элементов эстакады» (таблица №1).
    /// Таблица содержит три группы строк: стойки, фундаменты, траверсы —
    /// каждая агрегирована по типу и отсортирована.
    /// Стойки с исполнением EXCLUDED_IMPL в таблицу не включаются.
    /// </summary>
    public sealed class SpecificationsRacksTable1Builder
    {
        private sealed class StoikaRecord
        {
            public string TipStoiki { get; set; } = "";
            public int Count { get; set; }
            public double TotalLen { get; set; }
        }

        private readonly ParametricObjectPropertyReader _reader = new ParametricObjectPropertyReader();

        public bool TryBuild(
            List<McParametricObject> selectedObjects,
            Point3d insertPt,
            out string message)
        {
            message = "";

            if (selectedObjects == null || selectedObjects.Count == 0)
            {
                message = "Не переданы выбранные параметрические объекты.";
                return false;
            }

            // Три независимых словаря: стойки суммируют длину, фундаменты и траверсы — только количество.
            var stoikiDict = new Dictionary<string, StoikaRecord>(StringComparer.Ordinal);
            var fundDict = new Dictionary<string, int>(StringComparer.Ordinal);
            var travDict = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var obj in selectedObjects)
            {
                ParametricObjectData data = _reader.Read(obj);

                if (string.Equals(data.ObjectName, SpecificationObjectConstants.STOIKA_CLASS, StringComparison.Ordinal))
                {
                    if (string.Equals(data.Implementation, SpecificationObjectConstants.EXCLUDED_IMPL, StringComparison.Ordinal))
                        continue;

                    if (!string.IsNullOrWhiteSpace(data.TipStoiki))
                    {
                        if (stoikiDict.TryGetValue(data.TipStoiki, out var rec))
                        {
                            rec.Count++;
                            rec.TotalLen += data.DlinaStoikiM;
                        }
                        else
                        {
                            stoikiDict[data.TipStoiki] = new StoikaRecord
                            {
                                TipStoiki = data.TipStoiki,
                                Count = 1,
                                TotalLen = data.DlinaStoikiM
                            };
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(data.TipFundamenta))
                        fundDict[data.TipFundamenta] = fundDict.GetValueOrDefault(data.TipFundamenta) + 1;
                }
                else if (string.Equals(data.ObjectName, SpecificationObjectConstants.TRAVERSA_CLASS, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(data.TipTraversy))
                        travDict[data.TipTraversy] = travDict.GetValueOrDefault(data.TipTraversy) + 1;
                }
            }

            List<StoikaRecord> sortedStoiki = stoikiDict.Values
                .OrderBy(r => r.TipStoiki, StringComparer.CurrentCulture).ToList();
            List<KeyValuePair<string, int>> sortedFund = fundDict
                .OrderBy(kv => kv.Key, StringComparer.CurrentCulture).ToList();
            List<KeyValuePair<string, int>> sortedTrav = travDict
                .OrderBy(kv => kv.Key, StringComparer.CurrentCulture).ToList();

            int dataRows = sortedStoiki.Count + sortedFund.Count + sortedTrav.Count;
            if (dataRows == 0)
            {
                message = "Нет данных для формирования таблицы №1.";
                return false;
            }

            SpecificationTableHelper.EnsureDrawingResources();

            McTable table = SpecificationTableHelper.CreateStandardTable(
                "Спецификация элементов эстакады",
                dataRows);

            int row = SpecificationTableLayout.FIRST_DATA_ROW;

            foreach (var s in sortedStoiki)
            {
                // Суммарная длина стоек выводится в колонке «Примечание» в формате L=X,XX.
                string prim = $"L={s.TotalLen:F2}".Replace('.', ',');

                FillDataRow(table, row, s.TipStoiki, $"Стойка {s.TipStoiki}", s.Count.ToString(), "");
                SpecificationTableHelper.SetStandardCell(table, row, 5, prim, HorizTextAlign.Center);
                row++;
            }

            foreach (var kv in sortedFund)
            {
                FillDataRow(table, row, kv.Key, $"Фундамент {kv.Key}", kv.Value.ToString(), "");
                row++;
            }

            foreach (var kv in sortedTrav)
            {
                FillDataRow(table, row, kv.Key, $"Траверса {kv.Key}", kv.Value.ToString(), "");
                row++;
            }

            SpecificationTableHelper.ApplyStandardBorders(table, dataRows);
            SpecificationTableHelper.AddTableToDocument(table, insertPt);

            message = "Таблица «Спецификация элементов эстакады» сформирована.";
            return true;
        }

        private void FillDataRow(
            McTable table,
            int row,
            string poz,
            string naim,
            string kol,
            string primechanie)
        {
            SpecificationTableHelper.SetStandardCell(table, row, 0, poz, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 1, "", HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 2, naim, HorizTextAlign.Left, indent: 1.0);
            SpecificationTableHelper.SetStandardCell(table, row, 3, kol, HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 4, "", HorizTextAlign.Center);
            SpecificationTableHelper.SetStandardCell(table, row, 5, primechanie, HorizTextAlign.Center);
        }
    }
}