using Multicad;                              
using Multicad.DatabaseServices;
using Multicad.DatabaseServices.StandardObjects;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Runtime;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;

namespace GetSpecifications
{
    /// <summary>
    /// Команда InsertParamObject — вставка параметрических объектов СПДС вдоль полилинии.
    /// Если объект имеет атрибут rOtmetkaZemli, применяется алгоритм привязки к уровню земли.
    /// </summary>
    public class InsertParamObjectCommand
    {
        // Системное имя атрибута отметки земли в скрипте параметрического объекта СПДС
        private const string GROUND_LEVEL_ATTR = "rOtmetkaZemli";

        private static Editor Ed
            => Application.DocumentManager.MdiActiveDocument.Editor;

        private static void Msg(string text)
            => Ed.WriteMessage(text);

        [CommandMethod("InsertParamObject", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void InsertParamObject()
        {
            // --- Шаг 1: Выбор параметрического объекта (с повтором при промахе) ---
            McParametricObject? srcParam = SelectParametricObject();
            if (srcParam == null) return;

            // --- Шаг 2: Выбор незамкнутой полилинии (с повтором при промахе) ---
            DbPolyline? dbPoly = SelectPolyline();
            if (dbPoly == null) return;

            Polyline3d pline = dbPoly.Polyline;
            if (pline.ClosedLogically)
            {
                Msg("\nНеобходимо выбрать незамкнутую полилинию");
                return;
            }

            // --- Шаг 3: Получить все вершины полилинии ---
            List<Point3d> vertices = GetPolylineVertices(pline);
            if (vertices.Count == 0)
            {
                Msg("\nПолилиния не содержит вершин");
                return;
            }

            // --- Шаг 4: Считать R0 = rOtmetkaZemli из шаблона ---
            double? r0 = GetGroundLevelAttribute(srcParam);
            Msg($"\n[ДИАГНОСТИКА] R0 = {(r0.HasValue ? r0.Value.ToString("F3") : "null (параметр не найден)")}");

            // --- Шаг 5: Вставить копии ---
            int insertedCount = r0.HasValue
                ? InsertWithGroundLevel(srcParam, vertices, r0.Value)
                : InsertAtVertices(srcParam, vertices);

            Msg($"\nВставлено {insertedCount} объектов вдоль полилинии.");
        }

        // ---------------------------------------------------------------
        //  Выбор объектов с повтором при промахе.
        //  PromptStatus.Cancel — Escape → возвращаем null, команда завершается.
        //  PromptStatus.OK     — объект выбран → проверяем тип, при несоответствии просим снова.
        //  Иначе               — промах мимо объекта → повторяем запрос.
        // ---------------------------------------------------------------

        private McParametricObject? SelectParametricObject()
        {
            var opts = new PromptEntityOptions("\nВыберите параметрический объект или [?]: ");
            opts.AllowNone = false;

            while (true)
            {
                PromptEntityResult res = Ed.GetEntity(opts);

                if (res.Status == PromptStatus.Cancel)
                    return null;

                if (res.Status != PromptStatus.OK)
                {
                    Msg("\nПромах. Укажите параметрический объект СПДС.");
                    continue;
                }

                // HostMgd возвращает платформенный ObjectId — преобразуем в McObjectId через OldIdPtr
                McObject obj = McObjectManager.GetObject(
                    new McObjectId(res.ObjectId.OldIdPtr));

                if (obj is McParametricObject paramObj)
                    return paramObj;

                Msg($"\nВыбранный объект не является параметрическим СПДС" +
                    $" ({obj.GetType().Name}). Попробуйте ещё раз.");
            }
        }

        private DbPolyline? SelectPolyline()
        {
            var opts = new PromptEntityOptions(
                "\nВыберите полилинию для простановки параметрического объекта или [?]: ");
            opts.AllowNone = false;

            while (true)
            {
                PromptEntityResult res = Ed.GetEntity(opts);

                if (res.Status == PromptStatus.Cancel)
                    return null;

                if (res.Status != PromptStatus.OK)
                {
                    Msg("\nПромах. Укажите полилинию.");
                    continue;
                }

                // HostMgd возвращает платформенный ObjectId — преобразуем в McObjectId через OldIdPtr
                McObject obj = McObjectManager.GetObject(
                    new McObjectId(res.ObjectId.OldIdPtr));

                if (obj is DbPolyline dbPoly)
                    return dbPoly;

                Msg($"\nВыбранный объект не является полилинией" +
                    $" ({obj.GetType().Name}). Попробуйте ещё раз.");
            }
        }

        // ---------------------------------------------------------------
        //  Возвращает список вершин полилинии через коллекцию Points.
        // ---------------------------------------------------------------
        private List<Point3d> GetPolylineVertices(Polyline3d polyline)
        {
            var result = new List<Point3d>();
            var points = polyline.Points;
            for (int i = 0; i < points.Count; i++)
                result.Add(points[i]);
            return result;
        }

        // ---------------------------------------------------------------
        //  Чтение и запись атрибута rOtmetkaZemli параметрического объекта.
        //
        //  Атрибуты скрипта СПДС хранятся в DbEntity.ObjectProperties (коллекция McProperty).
        //  McProperty наследует PropertyDescriptor (.NET):
        //    GetValue(component) / SetValue(component, val) — component = McParametricObject.
        // ---------------------------------------------------------------

        // Возвращает значение rOtmetkaZemli или null если атрибут отсутствует у данного типа объекта
        private double? GetGroundLevelAttribute(McParametricObject paramObj)
        {
            foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
            {
                if (prop.Name != GROUND_LEVEL_ATTR) continue;
                return (double)prop.GetValue(paramObj);
            }

            return null;
        }

        private void SetGroundLevelAttribute(McParametricObject paramObj, double value)
        {
            foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
            {
                if (prop.Name != GROUND_LEVEL_ATTR) continue;
                prop.SetValue(paramObj, value);
                return;
            }
        }

        // ---------------------------------------------------------------
        //  Вставка с учётом атрибута rOtmetkaZemli.
        //
        //  Все объекты вставляются на одну общую горизонтальную отметку Ycommon,
        //  а индивидуальная отметка земли каждого задаётся через rOtmetkaZemli.
        //
        //  Алгоритм:
        //    Ymin    = min(Y всех вершин полилинии)
        //    Ycommon = Ymin - R0          — общая отметка вставки (ниже самой низкой вершины на R0)
        //    Для каждой вершины (Xi, Yi):
        //      Origin        = (Xi, Ycommon, 0)
        //      rOtmetkaZemli = Yi - Ycommon    (= R0 + (Yi - Ymin))
        // ---------------------------------------------------------------
        private int InsertWithGroundLevel(
            McParametricObject source,
            List<Point3d> vertices,
            double r0)
        {
            double yMin = double.MaxValue;
            foreach (var v in vertices)
                if (v.Y < yMin) yMin = v.Y;

            double yCommon = yMin - r0;

            Msg($"\n[ДИАГНОСТИКА] R0={r0:F3}, Ymin={yMin:F3}, Ycommon={yCommon:F3}");
            Msg($"\n[ДИАГНОСТИКА] Вершины ({vertices.Count} шт.):");
            foreach (var v in vertices)
                Msg($"\n  X={v.X:F3} Y={v.Y:F3} → Origin.Y={yCommon:F3}, rOtm={v.Y - yCommon:F3}");

            int count = 0;
            foreach (Point3d vertex in vertices)
            {
                try
                {
                    double rValue = vertex.Y - yCommon;

                    McParametricObject? copy = CloneParametricObject(source);
                    if (copy == null) continue;

                    copy.Origin = new Point3d(vertex.X, yCommon, 0);
                    SetGroundLevelAttribute(copy, rValue);
                    copy.DbObject.Update();
                    Msg($"\n[ДИАГНОСТИКА] Вставлен: Origin=({copy.Origin.X:F3}, {copy.Origin.Y:F3}), rOtm={rValue:F3}");
                    count++;
                }
                catch (Exception ex)
                {
                    Msg($"\nОшибка вставки в точку {vertex}: {ex.Message}");
                }
            }
            return count;
        }

        // ---------------------------------------------------------------
        //  Вставка без учёта rOtmetkaZemli — объекты размещаются точно в вершины полилинии.
        // ---------------------------------------------------------------
        private int InsertAtVertices(McParametricObject source, List<Point3d> vertices)
        {
            int count = 0;
            foreach (Point3d vertex in vertices)
            {
                try
                {
                    McParametricObject? copy = CloneParametricObject(source);
                    if (copy == null) continue;

                    copy.Origin = vertex;
                    copy.DbObject.Update();
                    count++;
                }
                catch (Exception ex)
                {
                    Msg($"\nОшибка вставки в точку {vertex}: {ex.Message}");
                }
            }
            return count;
        }

        // ---------------------------------------------------------------
        //  Клонирует параметрический объект и добавляет копию в текущий документ.
        //  Clone() возвращает object → приводим через as McDbObject.
        //  Объект извлекается через McDbObject.Entity.
        // ---------------------------------------------------------------
        private McParametricObject? CloneParametricObject(McParametricObject source)
        {
            McDbObject? cloned = source.DbObject.Clone() as McDbObject;
            if (cloned == null) return null;

            cloned.AddToCurrentDocument();
            return cloned.Entity as McParametricObject;
        }
    }
}
