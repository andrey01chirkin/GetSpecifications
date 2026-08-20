using GetSpecifications.WorkVolume.Table5;
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
    /// Команда CreateBeamsWorkVolume — строит «Ведомость объёмов работ элементов балок
    /// эстакады» по выбранной пользователем таблице «Спецификация элементов балок
    /// эстакады» (таблица №4, результат команды CreateSpecificationsBeams).
    /// </summary>
    public class CreateWorkVolumeBeamsCommand
    {
        private static Editor Ed => Application.DocumentManager.MdiActiveDocument.Editor;
        private static void Msg(string text) => Ed.WriteMessage(text);

        [CommandMethod("CreateWorkVolumeBeams", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void CreateBeamsWorkVolume()
        {
            McTable? table4 = SelectBeamsSpecificationTable();
            if (table4 == null)
                return;

            var pointOpts = new PromptPointOptions(
                "\nУкажите точку вставки ведомости объёмов работ: ");
            PromptPointResult pointRes = Ed.GetPoint(pointOpts);
            if (pointRes.Status != PromptStatus.OK)
                return;

            var insertPt = new Point3d(pointRes.Value.X, pointRes.Value.Y, pointRes.Value.Z);

            var builder = new WorkVolumeBeamsBuilder();
            builder.TryBuild(table4, insertPt, out string message);
            Msg("\n" + message);
        }

        // Цикл повторяет запрос выбора, пока пользователь не выберет корректную таблицу
        // или не нажмёт Esc. Аналогично SelectBeamObjects из CreateSpecificationsBeamsCommand.
        private McTable? SelectBeamsSpecificationTable()
        {
            while (true)
            {
                McObjectId id = McObjectManager.SelectObject(
                    "\nВыберите таблицу «Спецификация элементов балок эстакады»: ");

                if (id.IsNull)
                    return null;

                McObject obj = id.GetObject();

                if (obj is not McTable table)
                {
                    Msg("\nВыбранный объект не является таблицей. Попробуйте ещё раз.");
                    continue;
                }

                if (!WorkVolumeBeamsCalculator.IsBeamsSpecificationTable(table))
                {
                    Msg("\nВыбранная таблица не является «Спецификацией элементов балок эстакады». Попробуйте ещё раз.");
                    continue;
                }

                return table;
            }
        }
    }
}