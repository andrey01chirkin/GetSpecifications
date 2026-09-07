namespace GetSpecifications.Specifications.Table3
{
    public static class SpecificationsBeamsTable3Layout
    {
        // === Структура столбцов ===
        public const int TABLE_COLS = 9;

        public const double COL_W_NOMER = 15.0; // 0: Номер разреза
        public const double COL_W_KOL_BALOK = 15.0; // 1: Кол-во балок
        public const double COL_W_DLINA = 20.0; // 2: Длина, м
        public const double COL_W_NORM = 25.0; // 3: q норм. нагрузка
        public const double COL_W_RASCH = 25.0; // 4: q расч. нагрузка
        public const double COL_W_SHVELLER = 20.0; // 5: Швеллер поз.
        public const double COL_W_KOL_SHV = 20.0; // 6: Кол-во швеллеров
        public const double COL_W_PLASTINA = 20.0; // 7: Пластина поз.
        public const double COL_W_POKRYTIE = 20.0; // 8: Покрытие эстакады

        // === Высоты строк ===
        public const double ROW_H_TITLE = 10.0;
        public const double ROW_H_HDR1 = 15.0;
        public const double ROW_H_HDR2 = 10.0;
        public const double ROW_H_DATA = 7.0;

        // === Индексы строк (глобальные) ===
        // Схема секций: HeaderFirst(0, 3) → строки 0 (заголовок), 1 (шапка1), 2 (шапка2);
        // None(3, N) → строка 3 служебная, данные начинаются с FIRST_DATA_ROW = 4.
        public const int TITLE_ROW = 0;
        public const int HEADER_ROW_1 = 1;
        public const int HEADER_ROW_2 = 2;
        public const int FIRST_DATA_ROW = 4;

        // === Индексы столбцов, читаемых при подсчёте длины покрытия (Table7) ===
        public const int COL_DLINA = 2;
        public const int COL_POKRYTIE = 8;
    }
}