using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GetSpecifications.Specifications.Table2
{
    /// <summary>Корневой объект каталога Table2Catalog.json.</summary>
    public sealed class Table2CatalogRoot
    {
        public List<RackTypeTemplate> RackTypes { get; set; } = new();
    }

    /// <summary>Шаблон одного типа стойки: содержит упорядоченный список строк таблицы.</summary>
    public sealed class RackTypeTemplate
    {
        public string TipStoiki { get; set; } = "";
        public List<RackRowTemplate> Rows { get; set; } = new();
    }

    /// <summary>
    /// Шаблон одной строки таблицы.
    /// </summary>
    public sealed class RackRowTemplate
    {
        public string Kind { get; set; } = "";
        public string Position { get; set; } = "";

        [JsonPropertyName("nameTemplate")]
        public string Name { get; set; } = "";

        public string Top { get; set; } = "";
        public string BottomTemplate { get; set; } = "";

        [JsonPropertyName("quantityTemplate")]
        public string Quantity { get; set; } = "";

        public string Mass { get; set; } = "";
        public string Note { get; set; } = "";
        public string WorkVolumeName { get; set; } = "";
        public double FerCoefficient { get; set; }
    }

    /// <summary>Агрегат по одному типу стойки: суммарное количество и длина.</summary>
    public sealed class Table2SourceAggregate
    {
        public string TipStoiki { get; set; } = "";
        public int Count { get; set; }
        public double TotalLen { get; set; }
    }

    /// <summary>Финальная строка таблицы после подстановки токенов и сборки формул.</summary>
    public sealed class Table2Row
    {
        public string Position { get; set; } = "";
        public string Obz { get; set; } = "";
        public string Name { get; set; } = "";
        public string Quantity { get; set; } = "";
        public string Mass { get; set; } = "";
        public string Note { get; set; } = "";
    }

    /// <summary>
    /// Загружает каталог из embedded resource Table2Catalog.json.
    /// Каталог загружается один раз при старте через static readonly в SpecificationsRacksTable2Builder.
    /// </summary>
    public static class Table2Catalog
    {
        public static Table2CatalogRoot Load()
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            string? resourceName = asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Table2Catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new InvalidOperationException("Не найден embedded resource Table2Catalog.json.");

            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException("Не удалось открыть stream для Table2Catalog.json.");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            var root = JsonSerializer.Deserialize<Table2CatalogRoot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (root == null || root.RackTypes == null || root.RackTypes.Count == 0)
                throw new InvalidOperationException(
                    "Каталог Table2Catalog.json пустой или имеет неверную структуру.");

            return root;
        }
    }
}