using System.IO;
using System.Reflection;
using System.Text.Json;

namespace GetSpecifications.WorkVolume.Table5
{
    /// <summary>Корневой объект каталога Table5Catalog.json.</summary>
    public sealed class Table5CatalogRoot
    {
        public List<Table5RowTemplate> Rows { get; set; } = new();
    }

    /// <summary>
    /// Шаблон одной статичной строки ведомости объёмов работ (поз. 101–106).
    /// Формула расчёта не хранится как код — Formula задаёт метку, которую
    /// WorkVolumeBeamsBuilder распознаёт через switch и вызывает
    /// соответствующий заранее закреплённый расчёт.
    /// </summary>
    public sealed class Table5RowTemplate
    {
        public string Position { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Formula { get; set; } = "";
    }

    /// <summary>
    /// Загружает каталог из embedded resource Table5Catalog.json.
    /// Каталог загружается один раз при старте через static readonly в WorkVolumeBeamsBuilder.
    /// </summary>
    public static class BeamsTable5Catalog
    {
        public static Table5CatalogRoot Load()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string? resourceName = asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Table5Catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new InvalidOperationException("Не найден embedded resource Table5Catalog.json.");

            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException("Не удалось открыть stream для Table5Catalog.json.");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            var root = JsonSerializer.Deserialize<Table5CatalogRoot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (root == null || root.Rows == null || root.Rows.Count == 0)
                throw new InvalidOperationException(
                    "Каталог Table5Catalog.json пустой или имеет неверную структуру.");

            return root;
        }
    }
}