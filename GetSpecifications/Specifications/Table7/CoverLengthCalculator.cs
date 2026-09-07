using System.Globalization;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Table3;

namespace GetSpecifications.Specifications.Table7
{
    /// <summary>
    /// Читает «Таблицу сечений балок кабельной эстакады» (таблица №3) и считает суммарную
    /// длину покрытия: сумма столбца «Длина, м» по строкам, где «Покрытие эстакады» = «выполняется».
    /// </summary>
    public static class CoverLengthCalculator
    {
        private const string EXPECTED_TITLE = "Таблица сечений балок кабельной эстакады";
        private const string COVERED = "выполняется";

        public static bool IsBeamsSectionTable(McTable table)
        {
            string title = table.GetCellText(0, 0) ?? "";
            return string.Equals(title.Trim(), EXPECTED_TITLE, StringComparison.Ordinal);
        }

        public static double Calculate(McTable table)
        {
            double total = 0.0;
            int lastRow = table.GetRowCountAbs() - 1;

            for (int row = SpecificationsBeamsTable3Layout.FIRST_DATA_ROW; row <= lastRow; row++)
            {
                string coverage = (table.GetCellText(row, SpecificationsBeamsTable3Layout.COL_POKRYTIE) ?? "").Trim();
                if (!string.Equals(coverage, COVERED, StringComparison.Ordinal))
                    continue;

                string raw = (table.GetCellText(row, SpecificationsBeamsTable3Layout.COL_DLINA) ?? "")
                    .Trim().Replace(',', '.');

                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double len))
                    total += len;
            }

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }
    }
}