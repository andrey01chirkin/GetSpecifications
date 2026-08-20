using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using Multicad;
using Multicad.DatabaseServices;
using Multicad.Objects;
using Multicad.Runtime;
using Multicad.Symbols.Tables;

namespace GetSpecifications.Diagnostics
{
    /// <summary>
    /// Команда DiagnoseObject — выводит в командную строку полный тип объекта
    /// и все параметры скрипта СПДС из ObjectProperties.
    /// Команда DiagnoseTable — выводит сырой GetCellText по всем ячейкам выбранной
    /// таблицы McTable, без интерпретации формул/RTF. Нужна для разбора реального
    /// формата содержимого ячеек (например, после fmtUnderline/fmtDiv) перед тем,
    /// как писать regex для чтения таблицы программно.
    /// </summary>
    public class DiagnoseObjectCommand
    {
        private static Editor Ed => Application.DocumentManager.MdiActiveDocument.Editor;
        private static void Msg(string text) => Ed.WriteMessage(text);

        [CommandMethod("DiagnoseObject", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void DiagnoseObject()
        {
            McObjectId id = McObjectManager.SelectObject("\nВыберите объект для диагностики: ");
            if (id.IsNull) return;

            McObject obj = id.GetObject();

            Msg($"\nGetType().FullName : {obj.GetType().FullName}");

            if (obj is McParametricObject p)
            {
                Msg($"\nClassName    = \"{p.ClassName}\"");
                Msg($"\nCommonName   = \"{p.CommonName}\"");
                Msg($"\nName         = \"{p.Name}\"");
                Msg($"\nClassType    = \"{p.ClassType}\"");
                Msg($"\nClassSubType = \"{p.ClassSubType}\"");
                Msg("\n--- ObjectProperties ---");

                // GetValue принимает сам McParametricObject как component — не DbEntity и не DbObject.
                foreach (McProperty prop in p.DbEntity.ObjectProperties)
                    Msg($"\n  [{prop.PropertyType.Name}] {prop.Name} = {prop.GetValue(p)}");
            }
            else
            {
                Msg("\nОбъект не является McParametricObject");
            }
        }

        [CommandMethod("DiagnoseTable", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void DiagnoseTable()
        {
            McObjectId id = McObjectManager.SelectObject("\nВыберите таблицу для диагностики: ");
            if (id.IsNull) return;

            McObject obj = id.GetObject();

            if (obj is not McTable table)
            {
                Msg("\nВыбранный объект не является McTable.");
                return;
            }

            int rowCount = table.GetRowCountAbs();
            int colCount = table.Columns.Count;

            Msg($"\n--- DiagnoseTable: строк = {rowCount}, столбцов = {colCount} ---");

            for (int row = 0; row < rowCount; row++)
            {
                for (int col = 0; col < colCount; col++)
                {
                    string raw = table.GetCellText(row, col) ?? "";
                    // Экранируем переносы строк, чтобы видеть их явно в командной строке,
                    // а не разрывать вывод.
                    string visible = raw.Replace("\r\n", "\\r\\n").Replace("\n", "\\n").Replace("\r", "\\r");
                    Msg($"\n[{row},{col}] = \"{visible}\"");
                }
            }

            Msg("\n--- Конец DiagnoseTable ---");
        }
    }
}