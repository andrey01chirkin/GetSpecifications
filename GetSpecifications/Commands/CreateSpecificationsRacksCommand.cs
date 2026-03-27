using Multicad;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Runtime;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using GetSpecifications.Specifications.Common;
using GetSpecifications.Specifications.Table1;
using GetSpecifications.Specifications.Table2;

namespace GetSpecifications.Commands
{
    /// <summary>
    /// Команда CreateSpecificationsRacks — формирует две спецификации по выбранным объектам:
    /// 1. «Спецификация элементов эстакады» (сводная по типам стоек, фундаментам, траверсам).
    /// 2. «Спецификация элементов стоек эстакады» (детальная по составу каждого типа стойки).
    /// Марка стали запрашивается между вставками таблиц, поскольку используется только в таблице №2.
    /// </summary>
    public class CreateSpecificationsRacksCommand
    {
        private static Editor Ed => Application.DocumentManager.MdiActiveDocument.Editor;
        private static void Msg(string text) => Ed.WriteMessage(text);

        [CommandMethod("CreateSpecificationsRacks", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void CreateSpecificationsRacks()
        {
            List<McParametricObject>? selected = SelectParametricObjects();
            if (selected == null)
                return;

            var pt1Opts = new PromptPointOptions(
                "\nУкажите точку вставки Спецификации элементов эстакады: ");
            PromptPointResult pt1Res = Ed.GetPoint(pt1Opts);
            if (pt1Res.Status != PromptStatus.OK)
                return;

            var insertPt1 = new Point3d(pt1Res.Value.X, pt1Res.Value.Y, pt1Res.Value.Z);

            var table1Builder = new SpecificationsRacksTable1Builder();
            if (table1Builder.TryBuild(selected, insertPt1, out string table1Message))
                Msg("\n" + table1Message);
            else
            {
                Msg("\n" + table1Message);
                return;
            }

            string? steelGrade = PromptSteelGrade();
            if (steelGrade == null)
                return;

            var pt2Opts = new PromptPointOptions(
                "\nУкажите точку вставки Спецификации элементов стоек эстакады: ");
            PromptPointResult pt2Res = Ed.GetPoint(pt2Opts);
            if (pt2Res.Status != PromptStatus.OK)
                return;

            var insertPt2 = new Point3d(pt2Res.Value.X, pt2Res.Value.Y, pt2Res.Value.Z);

            var table2Builder = new SpecificationsRacksTable2Builder();
            if (table2Builder.TryBuild(selected, insertPt2, steelGrade, out string table2Message))
                Msg("\n" + table2Message);
            else
                Msg("\n" + table2Message);
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
        // целевой объект. Escape прерывает команду; пустой или нерелевантный набор — повтор.
        private List<McParametricObject>? SelectParametricObjects()
        {
            var opts = new PromptSelectionOptions
            {
                MessageForAdding = "\nВыберите объекты (рамкой или поштучно): ",
                MessageForRemoval = "\nУберите объекты из набора: "
            };

            while (true)
            {
                PromptSelectionResult res = Ed.GetSelection(opts);

                if (res.Status == PromptStatus.Cancel)
                    return null;

                if (res.Status != PromptStatus.OK || res.Value == null || res.Value.Count == 0)
                {
                    Msg("\nНеобходимо выбрать параметрические объекты");
                    continue;
                }

                var result = new List<McParametricObject>();

                foreach (var oid in res.Value.GetObjectIds())
                {
                    var mcId = new McObjectId(oid.OldIdPtr);
                    if (mcId.IsNull) continue;

                    if (mcId.GetObject() is McParametricObject p && MatchesTargetClass(p))
                        result.Add(p);
                }

                if (result.Count == 0)
                {
                    Msg($"\nВ выборке нет объектов «{SpecificationObjectConstants.STOIKA_CLASS}»" +
                        $" или «{SpecificationObjectConstants.TRAVERSA_CLASS}»." +
                        $"\nПопробуйте ещё раз.");
                    continue;
                }

                return result;
            }
        }

        // Идентификация по p.Name: это имя конкретного экземпляра, совпадающее с ClassName
        // для СПДС-объектов данного проекта.
        private static bool MatchesTargetClass(McParametricObject p)
        {
            string name = p.Name ?? "";

            return string.Equals(name, SpecificationObjectConstants.STOIKA_CLASS, StringComparison.Ordinal) ||
                   string.Equals(name, SpecificationObjectConstants.TRAVERSA_CLASS, StringComparison.Ordinal);
        }
    }
}