using System.IO;
using System.Reflection;
using System.Text.Json;

namespace GetSpecifications.Specifications.Table4
{
    /// <summary>Корневой объект каталога Table4Catalog.json.</summary>
    public sealed class Table4CatalogRoot
    {
        public List<Table4RowTemplate> Rows { get; set; } = new();
    }

    /// <summary>
    /// Шаблон одной строки таблицы №4.
    /// Наименование строится как: fmtText(prefix; fmtDiv(top; "С{steel} ГОСТ 27772-2021"; True)[; suffix])
    /// Суффикс suffix пустой для большинства строк; для «б» и «г» содержит " L=600".
    /// Поля WorkVolumeName/FerCoefficient/IsBoxSection используются только при построении
    /// таблицы №5 (ведомость объёмов работ) — не влияют на построение таблицы №4.
    /// </summary>
    public sealed class Table4RowTemplate
    {
        public string Position { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string Top { get; set; } = "";
        public string Suffix { get; set; } = "";
        public string Mass { get; set; } = "";
        public string Note { get; set; } = "";

        // === Поля для таблицы №5 (ведомость объёмов работ) ===

        // Ключ группировки материалов в ВР: строки с одинаковым WorkVolumeName
        // суммируются в одну позицию (например, поз. «1» и поз. «а» — обе «Швеллер 12У»).
        public string WorkVolumeName { get; set; } = "";

        // Коэффициент площади (м² на 1 тонну) из ФЕР 81-02-13-2001, разделы 2.3.1/2.3.3/2.3.4.
        public double FerCoefficient { get; set; }

        // true только для швеллеров, идущих на коробчатое сечение балки —
        // площадь окраски делится на 2 (красится только внешняя сторона).
        public bool IsBoxSection { get; set; }
    }

    /// <summary>
    /// Загружает каталог из embedded resource Table4Catalog.json.
    /// Каталог загружается один раз при старте через static readonly в SpecificationsBeamsTable4Builder.
    /// </summary>
    public static class BeamsTable4Catalog
    {
        public static Table4CatalogRoot Load()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string? resourceName = asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Table4Catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new InvalidOperationException("Не найден embedded resource Table4Catalog.json.");

            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException("Не удалось открыть stream для Table4Catalog.json.");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            var root = JsonSerializer.Deserialize<Table4CatalogRoot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (root == null || root.Rows == null || root.Rows.Count == 0)
                throw new InvalidOperationException(
                    "Каталог Table4Catalog.json пустой или имеет неверную структуру.");

            return root;
        }
    }
}