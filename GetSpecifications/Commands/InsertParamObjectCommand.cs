using Multicad;
using Multicad.DatabaseServices;
using Multicad.DatabaseServices.StandardObjects;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Runtime;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;

namespace GetSpecifications.Commands
{
    /// <summary>
    /// Команда InsertParamObject — вставка копий параметрического объекта СПДС вдоль вершин полилинии.
    ///
    /// Алгоритм выравнивания по уровню земли (при наличии атрибута rOtmetkaZemli):
    /// все копии вставляются на общей базовой отметке Ycommon = Ymin − R0,
    /// а отметка земли каждой копии устанавливается равной Yi − Ycommon.
    ///
    /// Алгоритм простановки шага (при наличии обоих атрибутов rOtmetkaZemli и rStep):
    /// каждой стойке задаётся шаг, равный горизонтальному расстоянию по оси X до следующей стойки.
    /// Последней стойке шаг не изменяется — остаётся унаследованным от клона исходного объекта.
    /// </summary>
    public class InsertParamObjectCommand
    {
        private const string GROUND_LEVEL_ATTR = "rOtmetkaZemli";
        private const string STEP_ATTR = "rStep";

        private static Editor Ed
            => Application.DocumentManager.MdiActiveDocument.Editor;

        private static void Msg(string text)
            => Ed.WriteMessage(text);

        [CommandMethod("InsertParamObject", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void InsertParamObject()
        {
            McParametricObject? srcParam = SelectParametricObject();
            if (srcParam == null) return;

            DbPolyline? dbPoly = SelectPolyline();
            if (dbPoly == null) return;

            Polyline3d pline = dbPoly.Polyline;
            if (pline.ClosedLogically)
            {
                Msg("\nНеобходимо выбрать незамкнутую полилинию");
                return;
            }

            List<Point3d> vertices = GetPolylineVertices(pline);
            if (vertices.Count == 0)
            {
                Msg("\nПолилиния не содержит вершин");
                return;
            }

            double? r0 = GetGroundLevelAttribute(srcParam);

            // rStep задаётся только если объект является стойкой —
            // то есть имеет оба атрибута rOtmetkaZemli и rStep.
            bool hasStep = r0.HasValue && HasAttribute(srcParam, STEP_ATTR);

#if DEBUG
            Msg($"\n[ДИАГНОСТИКА] R0 = {(r0.HasValue ? r0.Value.ToString("F3") : "null (параметр не найден)")}");
            Msg($"\n[ДИАГНОСТИКА] hasStep = {hasStep}");
#endif

            int insertedCount = r0.HasValue
                ? InsertWithGroundLevel(srcParam, vertices, r0.Value, hasStep)
                : InsertAtVertices(srcParam, vertices);

            Msg($"\nВставлено {insertedCount} объектов вдоль полилинии.");
        }

        /// <summary>
        /// Запрашивает у пользователя параметрический объект СПДС.
        /// Повторяет запрос до корректного выбора или отмены (Escape).
        /// </summary>
        private McParametricObject? SelectParametricObject()
        {
            var opts = new PromptEntityOptions("\nВыберите параметрический объект: ");
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

                McObject obj = McObjectManager.GetObject(
                    new McObjectId(res.ObjectId.OldIdPtr));

                if (obj is McParametricObject paramObj)
                    return paramObj;

                Msg($"\nВыбранный объект не является параметрическим СПДС" +
                    $" ({obj.GetType().Name}). Попробуйте ещё раз.");
            }
        }

        /// <summary>
        /// Запрашивает у пользователя полилинию.
        /// Повторяет запрос до корректного выбора или отмены (Escape).
        /// </summary>
        private DbPolyline? SelectPolyline()
        {
            var opts = new PromptEntityOptions(
                "\nВыберите полилинию для простановки параметрического объекта: ");
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

                McObject obj = McObjectManager.GetObject(
                    new McObjectId(res.ObjectId.OldIdPtr));

                if (obj is DbPolyline dbPoly)
                    return dbPoly;

                Msg($"\nВыбранный объект не является полилинией" +
                    $" ({obj.GetType().Name}). Попробуйте ещё раз.");
            }
        }

        private List<Point3d> GetPolylineVertices(Polyline3d polyline)
        {
            var result = new List<Point3d>();
            var points = polyline.Points;
            for (int i = 0; i < points.Count; i++)
                result.Add(points[i]);
            return result;
        }

        /// <summary>
        /// Возвращает значение атрибута rOtmetkaZemli исходного объекта,
        /// или null если атрибут отсутствует.
        /// </summary>
        private double? GetGroundLevelAttribute(McParametricObject paramObj)
        {
            foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
            {
                if (prop.Name != GROUND_LEVEL_ATTR) continue;
                return (double)prop.GetValue(paramObj);
            }
            return null;
        }

        /// <summary>
        /// Проверяет наличие атрибута с заданным именем у параметрического объекта.
        /// </summary>
        private bool HasAttribute(McParametricObject paramObj, string attrName)
        {
            foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
                if (prop.Name == attrName) return true;
            return false;
        }

        /// <summary>
        /// Задаёт атрибут rOtmetkaZemli через Change() вместо McProperty.SetValue(),
        /// чтобы гарантировать пересчёт скрипта при совпадении нового значения со старым.
        /// </summary>
        private void SetGroundLevelViaChange(McParametricObject paramObj, double value)
        {
            using var ev = new ExValue();
            ev.Name = GROUND_LEVEL_ATTR;
            ev.Value = value;
            paramObj.Change(ev, false);
        }

        private McParametricObject? CloneParametricObject(McParametricObject source)
        {
            McDbObject? cloned = source.DbObject.Clone() as McDbObject;
            if (cloned == null) return null;

            cloned.AddToCurrentDocument();
            return cloned.Entity as McParametricObject;
        }

        private void MoveObject(McParametricObject paramObj, Point3d targetOrigin)
        {
            Vector3d displacement = targetOrigin - paramObj.Origin;
            Matrix3d moveMatrix = Matrix3d.Displacement(displacement);
            paramObj.DbEntity.Transform(moveMatrix);
        }

        /// <summary>
        /// Вставляет копии объекта по алгоритму выравнивания по уровню земли.
        ///
        /// Ycommon = Ymin − R0 — единая базовая отметка вставки для всех стоек.
        /// rOtmetkaZemli каждой стойки = Yi − Ycommon.
        ///
        /// Если hasStep == true, каждой стойке (кроме последней) задаётся rStep
        /// равный горизонтальному расстоянию по X до следующей стойки.
        /// </summary>
        private int InsertWithGroundLevel(
            McParametricObject source,
            List<Point3d> vertices,
            double r0,
            bool hasStep)
        {
            double yMin = double.MaxValue;
            foreach (var v in vertices)
                if (v.Y < yMin) yMin = v.Y;

            double yCommon = yMin - r0;

#if DEBUG
            Msg($"\n[ДИАГНОСТИКА] R0={r0:F3}, Ymin={yMin:F3}, Ycommon={yCommon:F3}");
            Msg($"\n[ДИАГНОСТИКА] Вершины ({vertices.Count} шт.):");
            foreach (var v in vertices)
                Msg($"\n  X={v.X:F3} Y={v.Y:F3} → Origin.Y={yCommon:F3}, rOtm={v.Y - yCommon:F3}");
#endif

            int count = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Point3d vertex = vertices[i];
                try
                {
                    McParametricObject? copy = CloneParametricObject(source);
                    if (copy == null) continue;

                    var targetOrigin = new Point3d(vertex.X, yCommon, 0);
                    double rValue = vertex.Y - yCommon;

                    MoveObject(copy, targetOrigin);
                    SetGroundLevelViaChange(copy, rValue);

                    // Шаг задаётся всем стойкам кроме последней.
                    // Последняя стойка сохраняет значение rStep от клона исходного объекта.
                    if (hasStep && i < vertices.Count - 1)
                    {
                        double step = Math.Abs(vertices[i + 1].X - vertex.X);
                        using var ev = new ExValue();
                        ev.Name = STEP_ATTR;
                        ev.Value = step;
                        copy.Change(ev, false);
                    }

                    copy.DbObject.Update();

#if DEBUG
                    Msg($"\n[ДИАГНОСТИКА] Вставлен: Origin=({copy.Origin.X:F3}, {copy.Origin.Y:F3}), rOtm={rValue:F3}");
#endif
                    count++;
                }
                catch (Exception ex)
                {
                    Msg($"\nОшибка вставки в точку {vertex}: {ex.Message}");
                }
            }
            return count;
        }

        /// <summary>
        /// Вставляет копии объекта напрямую в вершины полилинии без изменения атрибутов.
        /// Используется когда объект не имеет атрибута rOtmetkaZemli.
        /// </summary>
        private int InsertAtVertices(McParametricObject source, List<Point3d> vertices)
        {
            int count = 0;
            foreach (Point3d vertex in vertices)
            {
                try
                {
                    McParametricObject? copy = CloneParametricObject(source);
                    if (copy == null) continue;

                    MoveObject(copy, vertex);
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
    }
}