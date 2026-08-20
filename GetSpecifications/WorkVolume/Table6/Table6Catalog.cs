using System.IO;
using System.Reflection;
using System.Text.Json;

namespace GetSpecifications.WorkVolume.Table6
{
    /// <summary>Корневой объект каталога Table6Catalog.json.</summary>
    public sealed class Table6CatalogRoot
    {
        public List<Table6RowTemplate> Rows { get; set; } = new();
    }

    /// <summary>
    /// Шаблон одной статичной строки ведомости объёмов работ по стойкам (поз. 95–100).
    /// Формула расчёта не хранится как код — Formula задаёт метку, которую
    /// WorkVolumeRacksBuilder распознаёт через switch, аналогично Table5Catalog для балок.
    /// </summary>
    public sealed class Table6RowTemplate
    {
        public string Position { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Formula { get; set; } = "";
    }

    /// <summary>
    /// Загружает каталог из embedded resource Table6Catalog.json.
    /// Каталог загружается один раз при старте через static readonly в WorkVolumeRacksBuilder.
    /// </summary>
    public static class RacksTable6Catalog
    {
        public static Table6CatalogRoot Load()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string? resourceName = asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Table6Catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new InvalidOperationException("Не найден embedded resource Table6Catalog.json.");

            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException("Не удалось открыть stream для Table6Catalog.json.");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            var root = JsonSerializer.Deserialize<Table6CatalogRoot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (root == null || root.Rows == null || root.Rows.Count == 0)
                throw new InvalidOperationException(
                    "Каталог Table6Catalog.json пустой или имеет неверную структуру.");

            return root;
        }
    }
}