namespace GetSpecifications.Specifications.Common
{
    public static class SpecificationTableLayout
    {
        // === Структура столбцов ===
        public const int TABLE_COLS = 6;
        public const double COL_W_POZ = 15.0;
        public const double COL_W_OBZ = 60.0;
        public const double COL_W_NAIM = 65.0;
        public const double COL_W_KOL = 10.0;
        public const double COL_W_MASS = 15.0;
        public const double COL_W_PRIM = 20.0;

        // === Высоты строк (мм на чертеже) ===
        public const double ROW_H_TITLE = 10.0;
        public const double ROW_H_HDR = 15.0;
        public const double ROW_H_DATA = 8.0;

        // === Параметры текста ===
        public const double TEXT_H_TITLE = 5.0;
        public const double TEXT_H_DATA = 3.0;
        public const double TEXT_SCALE = 0.7; // горизонтальное сжатие

        // === Веса линий границ (единицы McTable — сотые доли мм, 50 ≈ 0.5 мм) ===
        public const int LW_THICK = 50;
        public const int LW_THIN = 25;

        // === Индексы строк в глобальной адресации McTable ===
        // Схема секций: HeaderFirst(0,2) → строки 0..1; None(2, N) → строка 2 служебная, данные с 3.
        public const int TITLE_ROW = 0; // объединённая строка заголовка таблицы
        public const int HEADER_ROW = 1; // строка шапки (наименования колонок)
        public const int FIRST_DATA_ROW = 3; // строка 2 — служебная строка секции None
    }
}