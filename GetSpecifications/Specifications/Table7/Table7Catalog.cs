using System.IO;
using System.Reflection;
using System.Text.Json;

namespace GetSpecifications.Specifications.Table7
{
    /// <summary>Корневой объект каталога Table7Catalog.json.</summary>
    public sealed class Table7CatalogRoot
    {
        public List<Table7RowTemplate> Rows { get; set; } = new();
    }

    /// <summary>
    /// Шаблон одной строки «Спецификации элементов покрытия эстакады».
    /// Kind: "title" (подчёркнутый заголовок блока), "text" (обычный текст),
    /// "fraction" (Prefix + дробь Top/Bottom [+ Suffix]), "empty" (пустая строка).
    /// Токены: {steel} — в Bottom; {coverLen} — в Quantity.
    /// </summary>
    public sealed class Table7RowTemplate
    {
        public string Kind { get; set; } = "";
        public string Position { get; set; } = "";
        public string Name { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string Top { get; set; } = "";
        public string Bottom { get; set; } = "";
        public string Suffix { get; set; } = "";
        public string Quantity { get; set; } = "";
        public string Mass { get; set; } = "";
        public string Note { get; set; } = "";

        // Двухстрочное примечание (без "\n"): верх и низ выводятся через fmtDiv без черты.
        public string NoteTop { get; set; } = "";
        public string NoteBottom { get; set; } = "";
    }

    /// <summary>
    /// Загружает каталог из embedded resource Table7Catalog.json.
    /// Каталог загружается один раз при старте через static readonly в SpecificationsCoverTable7Builder.
    /// </summary>
    public static class CoverTable7Catalog
    {
        public static Table7CatalogRoot Load()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string? resourceName = asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Table7Catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new InvalidOperationException("Не найден embedded resource Table7Catalog.json.");

            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException("Не удалось открыть stream для Table7Catalog.json.");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            var root = JsonSerializer.Deserialize<Table7CatalogRoot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (root == null || root.Rows == null || root.Rows.Count == 0)
                throw new InvalidOperationException(
                    "Каталог Table7Catalog.json пустой или имеет неверную структуру.");

            return root;
        }
    }
}