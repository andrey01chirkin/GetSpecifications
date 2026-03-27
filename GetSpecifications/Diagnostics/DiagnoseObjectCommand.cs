using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using Multicad;
using Multicad.DatabaseServices;
using Multicad.Objects;
using Multicad.Runtime;

namespace GetSpecifications.Diagnostics
{
    /// <summary>
    /// Команда DiagnoseObject — выводит в командную строку полный тип объекта
    /// и все параметры скрипта СПДС из ObjectProperties.
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
    }
}