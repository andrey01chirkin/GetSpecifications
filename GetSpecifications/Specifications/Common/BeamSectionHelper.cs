namespace GetSpecifications.Specifications.Common
{
    /// <summary>
    /// Общие вспомогательные методы для работы с сечениями балок кабельной эстакады.
    /// Используется в Table3Builder и Table4Builder — выделено во избежание дублирования.
    /// </summary>
    internal static class BeamSectionHelper
    {
        // Алфавит буквенных индексов разрезов: а=0, б=1, в=2, г=3, д=4, е=5, ж=6, и=7, к=8, л=9, м=10, н=11.
        // Буквы «з» и «й» в технических обозначениях ГОСТ не применяются.
        public const string LetterSequence = "абвгдежиклмн";

        /// <summary>
        /// Количество швеллеров в поперечном сечении балки определяется конструкцией эстакады:
        /// однорядные балки (1–3 и 10) несут 2 швеллера, двухрядные (4–9) — 4.
        /// </summary>
        public static int GetChannelCount(int beamNumber)
            => (beamNumber == 1 || beamNumber == 2 || beamNumber == 3 || beamNumber == 10) ? 2 : 4;

        /// <summary>
        /// Разбирает обозначение разреза вида «10б-10б» в номер балки и буквенный индекс.
        /// Формат: {число}{буква}-{число}{буква}, обе части должны быть идентичны.
        /// Возвращает false если формат не распознан или номер вне диапазона 1–10.
        /// </summary>
        public static bool TryParseSectionName(string sName, out int beamNumber, out int letterIndex)
        {
            beamNumber = 0;
            letterIndex = 0;

            int dashIdx = sName.IndexOf('-');
            if (dashIdx <= 0) return false;

            string left = sName.Substring(0, dashIdx);

            int i = 0;
            while (i < left.Length && char.IsDigit(left[i])) i++;
            if (i == 0 || i >= left.Length) return false;

            if (!int.TryParse(left.Substring(0, i), out beamNumber)) return false;
            if (beamNumber < 1 || beamNumber > 10) return false;

            int idx = LetterSequence.IndexOf(left[i]);
            if (idx < 0) return false;

            letterIndex = idx;
            return true;
        }
    }
}