using System.Globalization;
using System.Text.RegularExpressions;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;
using GetSpecifications.Specifications.Table4;
using GetSpecifications.WorkVolume.Common;

namespace GetSpecifications.WorkVolume.Table5
{
    /// <summary>Агрегированные данные по одному материалу (строка поз. 101 в ВР).</summary>
    public sealed class MaterialAggregate
    {
        public string WorkVolumeName { get; set; } = "";
        public double MassKg { get; set; }
        public double PaintAreaM2 { get; set; }
    }

    /// <summary>Результат расчёта ведомости объёмов работ по таблице №4.</summary>
    public sealed class WorkVolumeResult
    {
        public string SteelGrade { get; set; } = "";
        public List<MaterialAggregate> Materials { get; set; } = new();
        public double TotalMassKg { get; set; }
        public double TotalPaintAreaM2 { get; set; }
        public double ReducedAreaM2 { get; set; }
        public double WeldVisualM { get; set; }
        public double WeldUzkM { get; set; }
    }

    /// <summary>
    /// Читает таблицу «Спецификация элементов балок эстакады» (таблица №4) и рассчитывает
    /// ведомость объёмов работ согласно алгоритму из ТЗ:
    /// поз.101 — масса по материалам (группировка по Table4RowTemplate.WorkVolumeName);
    /// поз.102 — площадь АКП (масса×коэф.ФЕР/1000, /2 для швеллеров коробчатого сечения);
    /// поз.103 — приведённая площадь (102×2,1);
    /// поз.104 — визуальный контроль швов (общая масса×0,025);
    /// поз.105 — УЗК (104×0,005);
    /// поз.106 — огнезащита (= поз.102).
    /// Округление до сотых применяется на каждом шаге, как в примерах ТЗ (округлённое
    /// значение массы материала используется в дальнейших расчётах площади и т.д.).
    /// </summary>
    public static class WorkVolumeBeamsCalculator
    {
        private const string EXPECTED_TITLE = "Спецификация элементов балок эстакады";

        // Марка стали спрятана в RTF-содержимом ячейки «Наименование»: между литералом марки
        // и текстом "27772-2021" всегда стоят ровно 4 RTF-эскейпа кириллицы — буквы «Г»,«О»,«С»,«Т».
        private static readonly Regex SteelGradeRegex =
            new(@"(?<grade>\d+(?:-\d+)?)\s*(?:\\u\d+\??\s*){4}27772-2021", RegexOptions.Compiled);

        private static readonly Table4CatalogRoot _catalog = BeamsTable4Catalog.Load();

        /// <summary>
        /// Проверяет, что выбранный объект — это таблица «Спецификация элементов балок эстакады».
        /// Идентификация по тексту заголовка (Вариант А) — без изменений в Table4Builder.
        /// </summary>
        public static bool IsBeamsSpecificationTable(McTable table)
        {
            string title = table.GetCellText(SpecificationTableLayout.TITLE_ROW, 0) ?? "";
            return string.Equals(title.Trim(), EXPECTED_TITLE, StringComparison.Ordinal);
        }

        public static bool TryCalculate(McTable table4, out WorkVolumeResult? result, out string message)
        {
            result = null;
            var warnings = new List<string>();

            Dictionary<string, Table4RowTemplate> byPosition =
                _catalog.Rows.ToDictionary(r => r.Position, StringComparer.Ordinal);

            int lastDataRow = table4.GetRowCountAbs() - 1;

            var massByName = new Dictionary<string, double>(StringComparer.Ordinal);
            var roundedAreaSumByName = new Dictionary<string, double>(StringComparer.Ordinal);
            var ferByName = new Dictionary<string, (double coef, bool isBox)>(StringComparer.Ordinal);
            var conflictByName = new Dictionary<string, bool>(StringComparer.Ordinal);
            var order = new List<string>();
            string steelGrade = "";

            for (int row = SpecificationTableLayout.FIRST_DATA_ROW; row <= lastDataRow; row++)
            {
                string pos = (table4.GetCellText(row, 0) ?? "").Trim();
                if (string.IsNullOrEmpty(pos))
                    continue;

                if (!byPosition.TryGetValue(pos, out Table4RowTemplate? catRow))
                {
                    warnings.Add($"Позиция «{pos}» (строка {row}) не найдена в каталоге — пропущена.");
                    continue;
                }

                string qtyRaw = table4.GetCellText(row, 3) ?? "";
                if (!TryParseDecimal(qtyRaw, out double qty))
                {
                    warnings.Add($"Не удалось разобрать количество «{qtyRaw}» в позиции «{pos}» — пропущена.");
                    continue;
                }

                if (!TryParseDecimal(catRow.Mass, out double unitMass))
                {
                    warnings.Add($"В каталоге для позиции «{pos}» некорректная масса «{catRow.Mass}» — пропущена.");
                    continue;
                }

                if (string.IsNullOrEmpty(steelGrade))
                {
                    string naimRaw = table4.GetCellText(row, 2) ?? "";
                    Match m = SteelGradeRegex.Match(naimRaw);
                    if (m.Success)
                        steelGrade = m.Groups["grade"].Value;
                }

                string key = catRow.WorkVolumeName;
                double rowMass = qty * unitMass;

                // Площадь позиции округляется сразу — согласно примеру ТЗ (поз. «1»: 71,22/2=35,61;
                // поз. «а»: 450,43), а не после суммирования сырых значений по группе материала.
                double rowArea = rowMass * catRow.FerCoefficient / 1000.0;
                if (catRow.IsBoxSection)
                    rowArea /= 2.0;
                rowArea = Math.Round(rowArea, 2, MidpointRounding.AwayFromZero);

                if (!massByName.ContainsKey(key))
                {
                    massByName[key] = 0.0;
                    roundedAreaSumByName[key] = 0.0;
                    ferByName[key] = (catRow.FerCoefficient, catRow.IsBoxSection);
                    conflictByName[key] = false;
                    order.Add(key);
                }
                else if (ferByName[key].isBox != catRow.IsBoxSection)
                {
                    conflictByName[key] = true;
                }

                massByName[key] += rowMass;
                roundedAreaSumByName[key] += rowArea;
            }

            if (order.Count == 0)
            {
                message = "Не удалось прочитать ни одной позиции из выбранной таблицы.";
                return false;
            }

            List<string> sortedOrder = MaterialSortHelper.SortByProfile(order);

            var materials = new List<MaterialAggregate>();
            double totalMass = 0.0;
            double totalArea = 0.0;

            foreach (string name in sortedOrder)
            {
                double massKg = Math.Round(massByName[name], 2, MidpointRounding.AwayFromZero);
                double area;

                if (conflictByName[name])
                {
                    area = Math.Round(roundedAreaSumByName[name], 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    (double coef, bool isBox) = ferByName[name];
                    area = massKg * coef / 1000.0;
                    if (isBox) area /= 2.0;
                    area = Math.Round(area, 2, MidpointRounding.AwayFromZero);
                }

                materials.Add(new MaterialAggregate
                {
                    WorkVolumeName = name,
                    MassKg = massKg,
                    PaintAreaM2 = area
                });

                totalMass += massKg;
                totalArea += area;
            }

            totalMass = Math.Round(totalMass, 2, MidpointRounding.AwayFromZero);
            totalArea = Math.Round(totalArea, 2, MidpointRounding.AwayFromZero);

            double reducedArea = Math.Round(totalArea * 2.1, 2, MidpointRounding.AwayFromZero);
            double weldVisual = Math.Round(totalMass * 0.025, 2, MidpointRounding.AwayFromZero);
            double weldUzk = Math.Round(weldVisual * 0.005, 2, MidpointRounding.AwayFromZero);

            result = new WorkVolumeResult
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

        private static bool TryParseDecimal(string raw, out double value)
        {
            string normalized = (raw ?? "").Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}