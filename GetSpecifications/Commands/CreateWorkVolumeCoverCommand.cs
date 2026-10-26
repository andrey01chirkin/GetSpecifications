using GetSpecifications.WorkVolume.Table8;
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
    /// Команда CreateWorkVolumeCover — строит «Ведомость объемов работ элементов покрытия
    /// эстакады» по выбранной таблице «Спецификация элементов покрытия эстакады»
    /// (таблица №7, результат команды CreateSpecificationsCover).
    /// </summary>
    public class CreateWorkVolumeCoverCommand
    {
        private static Editor Ed => Application.DocumentManager.MdiActiveDocument.Editor;
        private static void Msg(string text) => Ed.WriteMessage(text);

        [CommandMethod("CreateWorkVolumeCover", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void CreateWorkVolumeCover()
        {
            McTable? table7 = SelectCoverSpecificationTable();
            if (table7 == null)
                return;

            var pointOpts = new PromptPointOptions(
                "\nУкажите точку вставки ведомости объёмов работ: ");
            PromptPointResult pointRes = Ed.GetPoint(pointOpts);
            if (pointRes.Status != PromptStatus.OK)
                return;

            var insertPt = new Point3d(pointRes.Value.X, pointRes.Value.Y, pointRes.Value.Z);

            new WorkVolumeCoverBuilder().TryBuild(table7, insertPt, out string message);
            Msg("\n" + message);
        }

        // Цикл повторяет запрос выбора, пока пользователь не выберет корректную таблицу
        // или не нажмёт Esc. Аналогично SelectBeamsSpecificationTable из CreateWorkVolumeBeamsCommand.
        private McTable? SelectCoverSpecificationTable()
        {
            while (true)
            {
                McObjectId id = McObjectManager.SelectObject(
                    "\nВыберите таблицу «Спецификация элементов покрытия эстакады»: ");
                if (id.IsNull)
                    return null;

                McObject obj = id.GetObject();

                if (obj is not McTable table)
                {
                    Msg("\nВыбранный объект не является таблицей. Попробуйте ещё раз.");
                    continue;
                }

                if (!WorkVolumeCoverCalculator.IsCoverSpecificationTable(table))
                {
                    Msg("\nВыбранная таблица не является «Спецификацией элементов покрытия эстакады». Попробуйте ещё раз.");
                    continue;
                }

                return table;
            }
        }
    }
}