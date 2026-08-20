using GetSpecifications.Specifications.Common;
using GetSpecifications.Specifications.Table3;
using GetSpecifications.Specifications.Table4;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using Multicad;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Runtime;

namespace GetSpecifications.Commands
{
    /// <summary>
    /// Команда CreateSpecificationsBeams — формирует таблицы по выбранным объектам балок:
    /// Таблица №3 — «Таблица сечений балок кабельной эстакады».
    /// Таблица №4 — «Спецификация элементов балок эстакады».
    /// </summary>
    public class CreateSpecificationsBeamsCommand
    {
        private static Editor Ed => Application.DocumentManager.MdiActiveDocument.Editor;
        private static void Msg(string text) => Ed.WriteMessage(text);

        [CommandMethod("CreateSpecificationsBeams", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void CreateSpecificationsBeams()
        {
            List<McParametricObject>? selected = SelectBeamObjects();
            if (selected == null)
                return;

            // === Таблица №3 ===

            var pt3Opts = new PromptPointOptions(
                "\nУкажите точку вставки таблицы сечений балок кабельной эстакады: ");
            PromptPointResult pt3Res = Ed.GetPoint(pt3Opts);
            if (pt3Res.Status != PromptStatus.OK)
                return;

            var insertPt3 = new Point3d(pt3Res.Value.X, pt3Res.Value.Y, pt3Res.Value.Z);

            var table3Builder = new SpecificationsBeamsTable3Builder();
            table3Builder.TryBuild(selected, insertPt3, out string table3Message);
            Msg("\n" + table3Message);

            // === Таблица №4 ===

            string? steelGrade = PromptSteelGrade();
            if (steelGrade == null)
                return;

            var pt4Opts = new PromptPointOptions(
                "\nУкажите точку вставки спецификации элементов балок эстакады: ");
            PromptPointResult pt4Res = Ed.GetPoint(pt4Opts);
            if (pt4Res.Status != PromptStatus.OK)
                return;

            var insertPt4 = new Point3d(pt4Res.Value.X, pt4Res.Value.Y, pt4Res.Value.Z);

            var table4Builder = new SpecificationsBeamsTable4Builder();
            table4Builder.TryBuild(selected, insertPt4, steelGrade, out string table4Message);
            Msg("\n" + table4Message);
        }

        // Пустая строка ввода означает согласие с маркой по умолчанию «245-4».
        private string? PromptSteelGrade()
        {
            var opts = new PromptStringOptions(
                "\nУкажите марку стали по ГОСТ 27772-2021 для металлических конструкций <245-4>: ")
            {
                AllowSpaces = true
            };

            PromptResult res = Ed.GetString(opts);

            if (res.Status != PromptStatus.OK)
                return null;

            string value = res.StringResult?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(value))
                value = "245-4";

            return value;
        }

        // Цикл повторяет запрос выбора до тех пор, пока в наборе не окажется хотя бы один
        // объект балки. Escape прерывает команду; пустой или нерелевантный набор — повтор.
        private List<McParametricObject>? SelectBeamObjects()
        {
            var opts = new PromptSelectionOptions
            {
                MessageForAdding = "\nВыберите объекты балок (рамкой или поштучно): ",
                MessageForRemoval = "\nУберите объекты из набора: "
            };

            while (true)
            {
                PromptSelectionResult res = Ed.GetSelection(opts);

                if (res.Status == PromptStatus.Cancel)
                    return null;

                if (res.Status != PromptStatus.OK || res.Value == null || res.Value.Count == 0)
                {
                    Msg("\nНеобходимо выбрать параметрические объекты балок");
                    continue;
                }

                var result = new List<McParametricObject>();

                foreach (var oid in res.Value.GetObjectIds())
                {
                    var mcId = new McObjectId(oid.OldIdPtr);
                    if (mcId.IsNull) continue;

                    if (mcId.GetObject() is McParametricObject p && IsBeamObject(p))
                        result.Add(p);
                }

                if (result.Count == 0)
                {
                    Msg($"\nВ выборке нет объектов с именем, начинающимся на" +
                        $" «{SpecificationObjectConstants.BALKA_CLASS_PREFIX}N-N»." +
                        $"\nПопробуйте ещё раз.");
                    continue;
                }

                return result;
            }
        }

        // Фильтр: McParametricObject.Name соответствует шаблону «Балка N-N»,
        // где N — целое число от 1 до 10.
        private static bool IsBeamObject(McParametricObject p)
        {
            string name = p.Name ?? "";
            if (!name.StartsWith(SpecificationObjectConstants.BALKA_CLASS_PREFIX, StringComparison.Ordinal))
                return false;

            string suffix = name.Substring(SpecificationObjectConstants.BALKA_CLASS_PREFIX.Length);
            int dashIdx = suffix.IndexOf('-');
            if (dashIdx <= 0) return false;

            string left = suffix.Substring(0, dashIdx);
            string right = suffix.Substring(dashIdx + 1);

            return string.Equals(left, right, StringComparison.Ordinal)
                && int.TryParse(left, out int n)
                && n >= 1 && n <= 10;
        }
    }
}