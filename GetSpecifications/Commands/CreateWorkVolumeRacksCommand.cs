using GetSpecifications.WorkVolume.Table6;
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
    /// Команда CreateWorkVolumeRacks — строит «Ведомость объёмов работ элементов стоек
    /// эстакады» по выбранной пользователем таблице «Спецификация элементов стоек
    /// эстакады» (таблица №2, результат команды CreateSpecificationsRacks).
    /// </summary>
    public class CreateWorkVolumeRacksCommand
    {
        private static Editor Ed => Application.DocumentManager.MdiActiveDocument.Editor;
        private static void Msg(string text) => Ed.WriteMessage(text);

        [CommandMethod("CreateWorkVolumeRacks", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void CreateWorkVolumeRacks()
        {
            McTable? table2 = SelectRacksSpecificationTable();
            if (table2 == null)
                return;

            var pointOpts = new PromptPointOptions(
                "\nУкажите точку вставки ведомости объёмов работ: ");
            PromptPointResult pointRes = Ed.GetPoint(pointOpts);
            if (pointRes.Status != PromptStatus.OK)
                return;

            var insertPt = new Point3d(pointRes.Value.X, pointRes.Value.Y, pointRes.Value.Z);

            var builder = new WorkVolumeRacksBuilder();
            builder.TryBuild(table2, insertPt, out string message);
            Msg("\n" + message);
        }

        // Цикл повторяет запрос выбора, пока пользователь не выберет корректную таблицу
        // или не нажмёт Esc. Аналогично SelectBeamsSpecificationTable из CreateWorkVolumeBeamsCommand.
        private McTable? SelectRacksSpecificationTable()
        {
            while (true)
            {
                McObjectId id = McObjectManager.SelectObject(
                    "\nВыберите таблицу «Спецификация элементов стоек эстакады»: ");
                if (id.IsNull)
                    return null;

                McObject obj = id.GetObject();

                if (obj is not McTable table)
                {
                    Msg("\nВыбранный объект не является таблицей. Попробуйте ещё раз.");
                    continue;
                }

                if (!WorkVolumeRacksCalculator.IsRacksSpecificationTable(table))
                {
                    Msg("\nВыбранная таблица не является «Спецификацией элементов стоек эстакады». Попробуйте ещё раз.");
                    continue;
                }

                return table;
            }
        }
    }
}