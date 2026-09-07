using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace GetSpecifications.WorkVolume.Common
{
    /// <summary>
    /// Сортировка материалов ведомостей объёмов работ (таблицы №5, №6) по профилю проката:
    /// сначала профиль (швеллер/труба) по возрастанию типоразмера, затем лист по возрастанию
    /// толщины, затем уголок. Ключ сортировки полностью выводится из WorkVolumeName —
    /// отдельного поля категории в каталогах нет и не требуется.
    /// </summary>
    public static class MaterialSortHelper
    {
        private static readonly Regex FirstNumberRegex = new(@"\d+", RegexOptions.Compiled);

        /// <summary>Возвращает имена материалов, отсортированные по профилю и возрастанию типоразмера.</summary>
        public static List<string> SortByProfile(IEnumerable<string> workVolumeNames)
            => workVolumeNames
                .OrderBy(GetCategory)
                .ThenBy(GetSize)
                .ToList();

        // 0 — швеллер/труба, 1 — лист, 2 — уголок, 3 — нераспознанный профиль (в конец списка).
        private static int GetCategory(string workVolumeName)
        {
            if (workVolumeName.StartsWith("Швеллер", StringComparison.Ordinal)) return 0;
            if (workVolumeName.StartsWith("Труба", StringComparison.Ordinal)) return 0;
            if (workVolumeName.StartsWith("Лист", StringComparison.Ordinal)) return 1;
            if (workVolumeName.StartsWith("Уголок", StringComparison.Ordinal)) return 2;
            return 3;
        }

        // Типоразмер — первое число в названии: типоразмер швеллера, диаметр трубы,
        // толщина листа или первая размерность уголка.
        private static double GetSize(string workVolumeName)
        {
            Match m = FirstNumberRegex.Match(workVolumeName);
            return m.Success ? double.Parse(m.Value, CultureInfo.InvariantCulture) : 0.0;
        }
    }
}