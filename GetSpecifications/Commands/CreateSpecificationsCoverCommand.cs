using GetSpecifications.Specifications.Common;
using GetSpecifications.Specifications.Table7;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using Multicad;
using Multicad.DatabaseServices;
using Multicad.Geometry;
using Multicad.Runtime;
using Multicad.Symbols.Tables;

namespace GetSpecifications.Commands
{
    /// <summary>
    /// Команда CreateSpecificationsCover — формирует «Спецификацию элементов покрытия эстакады»
    /// (таблица №7) по выбранной «Таблице сечений балок кабельной эстакады» (таблица №3,
    /// результат команды CreateSpecificationsBeams).
    /// </summary>
    public class CreateSpecificationsCoverCommand
    {
        private static Editor Ed => Application.DocumentManager.MdiActiveDocument.Editor;
        private static void Msg(string text) => Ed.WriteMessage(text);

        [CommandMethod("CreateSpecificationsCover", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void CreateSpecificationsCover()
        {
            McTable? table3 = SelectBeamsSectionTable();
            if (table3 == null)
                return;

            double coverLen = CoverLengthCalculator.Calculate(table3);
            if (coverLen <= 0)
            {
                Msg("\nНет балок с покрытием «выполняется» в выбранной таблице сечений — спецификация не сформирована.");
                return;
            }

            string? steelGrade = SpecificationTableHelper.PromptSteelGrade(Ed);
            if (steelGrade == null)
                return;

            var pointOpts = new PromptPointOptions(
                "\nУкажите точку вставки Спецификации элементов покрытия эстакады: ");
            PromptPointResult pointRes = Ed.GetPoint(pointOpts);
            if (pointRes.Status != PromptStatus.OK)
                return;

            var insertPt = new Point3d(pointRes.Value.X, pointRes.Value.Y, pointRes.Value.Z);

            new SpecificationsCoverTable7Builder().Build(coverLen, insertPt, steelGrade);
            Msg("\nТаблица «Спецификация элементов покрытия эстакады» сформирована.");
        }

        // Цикл повторяет запрос выбора, пока пользователь не выберет корректную таблицу
        // или не нажмёт Esc. Аналогично SelectBeamsSpecificationTable из CreateWorkVolumeBeamsCommand.
        private McTable? SelectBeamsSectionTable()
        {
            while (true)
            {
                McObjectId id = McObjectManager.SelectObject(
                    "\nВыберите \"Таблица сечений балок кабельной эстакады\": ");
                if (id.IsNull)
                    return null;

                McObject obj = id.GetObject();

                if (obj is not McTable table)
                {
                    Msg("\nВыбранный объект не является таблицей. Попробуйте ещё раз.");
                    continue;
                }

                if (!CoverLengthCalculator.IsBeamsSectionTable(table))
                {
                    Msg("\nВыбранная таблица не является «Таблицей сечений балок кабельной эстакады». Попробуйте ещё раз.");
                    continue;
                }

                return table;
            }
        }
    }
}