using System.Text.RegularExpressions;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;

namespace GetSpecifications.WorkVolume.Table8
{
    /// <summary>
    /// Читает «Спецификацию элементов покрытия эстакады» (таблица №7) и извлекает
    /// значения «Кол.» из строк-заголовков блоков. Заголовок распознаётся по пустой «Поз.»
    /// и вхождению ключа раздела в декодированный текст ячейки «Наименование».
    /// </summary>
    public static class WorkVolumeCoverCalculator
    {
        private const string EXPECTED_TITLE = "Спецификация элементов покрытия эстакады";
        private const int COL_POZ = 0;
        private const int COL_NAIM = 2;
        private const int COL_KOL = 3;

        // Кириллица в RTF-содержимом ячейки (fmtUnderline) хранится как \uNNNN? —
        // декодируем в символы для сравнения с ключами каталога.
        private static readonly Regex RtfUnicodeRegex =
            new(@"\\u(?<code>\d+)\?", RegexOptions.Compiled);

        public static bool IsCoverSpecificationTable(McTable table)
        {
            string title = table.GetCellText(SpecificationTableLayout.TITLE_ROW, 0) ?? "";
            return string.Equals(title.Trim(), EXPECTED_TITLE, StringComparison.Ordinal);
        }

        /// <summary>
        /// Возвращает словарь «ключ раздела → значение Кол.» только для заголовков
        /// с заполненным количеством. Точка в значении заменяется на запятую.
        /// </summary>
        public static Dictionary<string, string> ReadSectionQuantities(
            McTable table7,
            IReadOnlyList<Table8SectionTemplate> sections)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            int lastRow = table7.GetRowCountAbs() - 1;

            for (int row = SpecificationTableLayout.FIRST_DATA_ROW; row <= lastRow; row++)
            {
                if ((table7.GetCellText(row, COL_POZ) ?? "").Trim().Length > 0)
                    continue;

                string name = DecodeRtfUnicode(table7.GetCellText(row, COL_NAIM) ?? "");
                if (name.Length == 0)
                    continue;

                foreach (Table8SectionTemplate section in sections)
                {
                    if (!name.Contains(section.Key, StringComparison.Ordinal))
                        continue;

                    string qty = (table7.GetCellText(row, COL_KOL) ?? "").Trim();
                    if (qty.Length > 0)
                        result[section.Key] = qty.Replace('.', ',');
                    break;
                }
            }

            return result;
        }

        private static string DecodeRtfUnicode(string raw)
            => RtfUnicodeRegex.Replace(raw, m => ((char)int.Parse(m.Groups["code"].Value)).ToString());
    }
}