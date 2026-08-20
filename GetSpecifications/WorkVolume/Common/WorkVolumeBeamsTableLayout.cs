namespace GetSpecifications.WorkVolume.Common
{
    public static class WorkVolumeBeamsTableLayout
    {
        // === Структура столбцов ===
        public const int TABLE_COLS = 4;
        public const double COL_W_NUM = 15.0;
        public const double COL_W_NAME = 110.0;
        public const double COL_W_UNIT = 20.0;
        public const double COL_W_QTY = 20.0;

        // === Высоты строк (мм на чертеже) ===
        public const double ROW_H_TITLE = 10.0;
        public const double ROW_H_HDR = 15.0;
        public const double ROW_H_DATA = 8.0;

        // === Параметры текста ===
        public const double TEXT_H_TITLE = 5.0;
        public const double TEXT_H_DATA = 3.0;
        public const double TEXT_SCALE = 0.7;

        // === Веса линий границ (единицы McTable — сотые доли мм, 50 ≈ 0.5 мм) ===
        public const int LW_THICK = 50;
        public const int LW_THIN = 25;

        // === Индексы строк в глобальной адресации McTable ===
        public const int TITLE_ROW = 0;
        public const int HEADER_ROW = 1;
        public const int FIRST_DATA_ROW = 3;
    }
}