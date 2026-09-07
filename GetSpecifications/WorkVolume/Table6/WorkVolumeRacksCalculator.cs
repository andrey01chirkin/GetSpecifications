using System.Globalization;
using System.Text.RegularExpressions;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;
using GetSpecifications.Specifications.Table2;
using GetSpecifications.WorkVolume.Common;

namespace GetSpecifications.WorkVolume.Table6
{
    /// <summary>Агрегированные данные по одному материалу (труба или лист) для строки поз. 95.</summary>
    public sealed class RackMaterialAggregate
    {
        public string WorkVolumeName { get; set; } = "";
        public double MassKg { get; set; }
        public double PaintAreaM2 { get; set; }
        // Заполняется только для труб (IsPipe == true) — суммарная длина по всем найденным
        // позициям типоразмера, для «Примечание» в формате «Lобщ=…».
        public bool IsPipe { get; set; }
        public double TotalLenM { get; set; }
        public double UnitMassPerM { get; set; }
    }

    /// <summary>Результат расчёта ведомости объёмов работ по таблице №2 (стойки).</summary>
    public sealed class WorkVolumeRacksResult
    {
        public string SteelGrade { get; set; } = "";
        public List<RackMaterialAggregate> Materials { get; set; } = new();
        public double TotalMassKg { get; set; }
        public double TotalPaintAreaM2 { get; set; }
        public double ReducedAreaM2 { get; set; }
        public double WeldVisualM { get; set; }
        public double WeldUzkM { get; set; }
    }

    /// <summary>
    /// Читает таблицу «Спецификация элементов стоек эстакады» (таблица №2) и рассчитывает
    /// ведомость объёмов работ. В отличие от WorkVolumeBeamsCalculator, разбор ведётся не
    /// плоским словарём по номеру позиции, а блочно: строка-заголовок секции распознаётся
    /// структурно (пустая ячейка «Поз.»), тип стойки и количество извлекаются из неё, после
    /// чего следующие N строк читаются позиционно по шаблону RackTypeTemplate.Rows.
    /// Формулы расчёта площади/сварки идентичны WorkVolumeBeamsCalculator (без деления
    /// площади на 2 — IsBoxSection для стоек не применяется).
    /// </summary>
    public static class WorkVolumeRacksCalculator
    {
        private const string EXPECTED_TITLE = "Спецификация элементов стоек эстакады";

        // Марка стали закодирована в RTF-содержимом ячейки «Наименование» ровно так же,
        // как у балок: между числом марки и «27772-2021» стоят 4 RTF-эскейпа букв «Г,О,С,Т».
        private static readonly Regex SteelGradeRegex =
            new(@"(?<grade>\d+(?:-\d+)?)\s*(?:\\u\d+\??\s*){4}27772-2021", RegexOptions.Compiled);

        // Тип стойки закодирован в заголовке секции через fmtUnderline("Стойка типа С-N[П]").
        // Уникальная точка привязки — «С» вплотную к дефису перед номером
        private static readonly Regex RackTypeRegex =
            new(@"\\u1057\?-(?<num>\d+)(?<pIndex>\\u1055\?)?", RegexOptions.Compiled);

        private static readonly Table2CatalogRoot _catalog = Table2Catalog.Load();

        public static bool IsRacksSpecificationTable(McTable table)
        {
            string title = table.GetCellText(SpecificationTableLayout.TITLE_ROW, 0) ?? "";
            return string.Equals(title.Trim(), EXPECTED_TITLE, StringComparison.Ordinal);
        }

        public static bool TryCalculate(McTable table2, out WorkVolumeRacksResult? result, out string message)
        {
            result = null;
            var warnings = new List<string>();

            Dictionary<string, RackTypeTemplate> templatesByTip = _catalog.RackTypes
                .ToDictionary(x => x.TipStoiki, x => x, StringComparer.Ordinal);

            var massByName = new Dictionary<string, double>(StringComparer.Ordinal);
            var lenByName = new Dictionary<string, double>(StringComparer.Ordinal);
            var ferByName = new Dictionary<string, (double coef, bool isPipe)>(StringComparer.Ordinal);
            var unitMassByName = new Dictionary<string, double>(StringComparer.Ordinal);
            var order = new List<string>();
            string steelGrade = "";

            int lastRow = table2.GetRowCountAbs() - 1;
            int row = SpecificationTableLayout.FIRST_DATA_ROW;

            while (row <= lastRow)
            {
                string pos = (table2.GetCellText(row, 0) ?? "").Trim();

                if (pos.Length > 0)
                {
                    row++;
                    continue;
                }

                string rawTitle = table2.GetCellText(row, 2) ?? "";
                string tipStoiki = ParseTipStoiki(rawTitle);

                string rawNote = (table2.GetCellText(row, 5) ?? "").Trim();
                int count = ParseCount(rawNote);

                int contentStart = row + 1;
                int contentEnd = contentStart;
                while (contentEnd <= lastRow && (table2.GetCellText(contentEnd, 0) ?? "").Trim().Length > 0)
                    contentEnd++;

                int actualRows = contentEnd - contentStart;

                if (!templatesByTip.TryGetValue(tipStoiki, out RackTypeTemplate? template))
                {
                    warnings.Add($"Тип стойки «{tipStoiki}» (строка {row}) не найден в каталоге — пропущен.");
                    row = contentEnd;
                    continue;
                }

                List<RackRowTemplate> contentTemplates = template.Rows.Skip(1).ToList();

                if (contentTemplates.Count != actualRows)
                    warnings.Add(
                        $"Тип стойки «{tipStoiki}»: количество строк в таблице ({actualRows})" +
                        $" не совпадает с каталогом ({contentTemplates.Count}).");

                int n = Math.Min(contentTemplates.Count, actualRows);

                for (int i = 0; i < n; i++)
                {
                    int dataRow = contentStart + i;
                    RackRowTemplate templateRow = contentTemplates[i];

                    string qtyRaw = table2.GetCellText(dataRow, 3) ?? "";
                    if (!TryParseDecimal(qtyRaw, out double qty))
                    {
                        warnings.Add($"Не удалось разобрать «Кол.» = «{qtyRaw}» в строке {dataRow} — пропущена.");
                        continue;
                    }

                    if (!TryParseDecimal(templateRow.Mass, out double unitMass))
                    {
                        warnings.Add($"В каталоге для позиции «{templateRow.Position}» ({tipStoiki})" +
                                     $" некорректная масса «{templateRow.Mass}» — пропущена.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(steelGrade))
                    {
                        string naimRaw = table2.GetCellText(dataRow, 2) ?? "";
                        Match m = SteelGradeRegex.Match(naimRaw);
                        if (m.Success)
                            steelGrade = m.Groups["grade"].Value;
                    }

                    bool isPipe = templateRow.Kind == "text";
                    string key = templateRow.WorkVolumeName;

                    if (!massByName.ContainsKey(key))
                    {
                        massByName[key] = 0.0;
                        lenByName[key] = 0.0;
                        ferByName[key] = (templateRow.FerCoefficient, isPipe);
                        unitMassByName[key] = unitMass;
                        order.Add(key);
                    }

                    if (isPipe)
                    {
                        massByName[key] += qty * unitMass;
                        lenByName[key] += qty;
                    }
                    else
                    {
                        massByName[key] += qty * unitMass * count;
                    }
                }

                row = contentEnd;
            }

            if (order.Count == 0)
            {
                message = "Не удалось прочитать ни одной позиции из выбранной таблицы.";
                return false;
            }

            List<string> sortedOrder = MaterialSortHelper.SortByProfile(order);

            var materials = new List<RackMaterialAggregate>();
            double totalMass = 0.0;
            double totalArea = 0.0;

            foreach (string name in sortedOrder)
            {
                double massKg = Math.Round(massByName[name], 2, MidpointRounding.AwayFromZero);
                (double coef, bool isPipe) = ferByName[name];

                double area = Math.Round(massKg * coef / 1000.0, 2, MidpointRounding.AwayFromZero);

                materials.Add(new RackMaterialAggregate
                {
                    WorkVolumeName = name,
                    MassKg = massKg,
                    PaintAreaM2 = area,
                    IsPipe = isPipe,
                    TotalLenM = isPipe ? Math.Round(lenByName[name], 2, MidpointRounding.AwayFromZero) : 0.0,
                    UnitMassPerM = isPipe ? unitMassByName[name] : 0.0
                });

                totalMass += massKg;
                totalArea += area;
            }

            totalMass = Math.Round(totalMass, 2, MidpointRounding.AwayFromZero);
            totalArea = Math.Round(totalArea, 2, MidpointRounding.AwayFromZero);

            double reducedArea = Math.Round(totalArea * 2.1, 2, MidpointRounding.AwayFromZero);
            double weldVisual = Math.Round(totalMass * 0.025, 2, MidpointRounding.AwayFromZero);
            double weldUzk = Math.Round(weldVisual * 0.005, 2, MidpointRounding.AwayFromZero);

            result = new WorkVolumeRacksResult
            {
                SteelGrade = string.IsNullOrEmpty(steelGrade) ? "245-4" : steelGrade,
                Materials = materials,
                TotalMassKg = totalMass,
                TotalPaintAreaM2 = totalArea,
                ReducedAreaM2 = reducedArea,
                WeldVisualM = weldVisual,
                WeldUzkM = weldUzk
            };

            message = warnings.Count == 0
                ? "Расчёт ведомости объёмов работ выполнен без замечаний."
                : "Расчёт выполнен с предупреждениями:\n" + string.Join("\n", warnings);

            return true;
        }

        private static string ParseTipStoiki(string rawRtf)
        {
            Match m = RackTypeRegex.Match(rawRtf);
            if (!m.Success)
                return "";

            string suffix = m.Groups["pIndex"].Success ? "П" : "";
            return "С-" + m.Groups["num"].Value + suffix;
        }

        private static int ParseCount(string rawNote)
        {
            int spaceIdx = rawNote.IndexOf(' ');
            string numPart = spaceIdx > 0 ? rawNote.Substring(0, spaceIdx) : rawNote;
            return int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                ? count
                : 0;
        }

        private static bool TryParseDecimal(string raw, out double value)
        {
            string normalized = (raw ?? "").Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}