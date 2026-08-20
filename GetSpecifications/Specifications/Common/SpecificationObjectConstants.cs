namespace GetSpecifications.Specifications.Common
{
    public static class SpecificationObjectConstants
    {
        // === Имена классов СПДС-объектов (McParametricObject.Name) ===
        public const string STOIKA_CLASS = "Стойка_универсальная_ГТП";
        public const string TRAVERSA_CLASS = "Траверса_ГТП";

        // Стойки с этим исполнением не учитываются в спецификациях (существующие объекты).
        public const string EXCLUDED_IMPL = "Стойка существующая";

        // === Имена параметров скрипта СПДС (McProperty.Name) ===
        public const string PARAM_IMPL = "strImplementation";
        public const string PARAM_TIP_STOIKI = "strTipStoiki";
        public const string PARAM_TIP_FUND = "strTipFundamenta";
        public const string PARAM_DLINA = "rDlinaStoikiM";
        public const string PARAM_TIP_TRAV = "strTipTraversy";

        // === Ресурсы чертежа ===
        public const string LAYER_NAME = "СПДС_ТАБЛИЦЫ";
        public const string TEXT_STYLE = "Txt";
        public const string TEXT_FONT_FILE = "txt.shx";

        // === Балки ===
        // Все классы балок имеют формат «Балка N-N» — фильтрация по префиксу.
        public const string BALKA_CLASS_PREFIX = "Балка ";

        public const string PARAM_SNAME = "sName";
        public const string PARAM_RL = "rL";
        public const string PARAM_NORM_NAGRUZKA = "rNormNagruzka";
        public const string PARAM_RASCH_NAGRUZKA = "rRaschNagruzka";
        public const string PARAM_POKRYTIE = "strPokrytieEstakady";
        public const string PARAM_DLINA_TRAVERSY = "rDlinaTraversy";
    }
}