using System.IO;
using System.Reflection;
using System.Text.Json;

namespace GetSpecifications.WorkVolume.Table8
{
    /// <summary>Корневой объект каталога Table8Catalog.json.</summary>
    public sealed class Table8CatalogRoot
    {
        public List<Table8SectionTemplate> Sections { get; set; } = new();
    }

    /// <summary>
    /// Раздел ведомости объёмов работ покрытия. Key — фрагмент текста заголовка блока
    /// в таблице №7, по которому раздел сопоставляется со спецификацией.
    /// </summary>
    public sealed class Table8SectionTemplate
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public List<Table8RowTemplate> Rows { get; set; } = new();
    }

    /// <summary>Статичная строка раздела: значения заданы на единицу и не пересчитываются.</summary>
    public sealed class Table8RowTemplate
    {
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Quantity { get; set; } = "";
    }

    /// <summary>
    /// Загружает каталог из embedded resource Table8Catalog.json.
    /// Каталог загружается один раз при старте через static readonly в WorkVolumeCoverBuilder.
    /// </summary>
    public static class CoverTable8Catalog
    {
        public static Table8CatalogRoot Load()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string? resourceName = asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Table8Catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new InvalidOperationException("Не найден embedded resource Table8Catalog.json.");

            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException("Не удалось открыть stream для Table8Catalog.json.");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            var root = JsonSerializer.Deserialize<Table8CatalogRoot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (root == null || root.Sections == null || root.Sections.Count == 0)
                throw new InvalidOperationException(
                    "Каталог Table8Catalog.json пустой или имеет неверную структуру.");

            return root;
        }
    }
}