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
    /// Если объект имеет атрибут rOtmetkaZemli, применяется алгоритм выравнивания по уровню земли:
    /// все копии вставляются на общей базовой отметке Ycommon = Ymin − R0,
    /// а отметка земли каждой копии устанавливается равной Yi − Ycommon.
    /// </summary>
    public class InsertParamObjectCommand
    {
        private const string GROUND_LEVEL_ATTR = "rOtmetkaZemli";

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
#if DEBUG
            Msg($"\n[ДИАГНОСТИКА] R0 = {(r0.HasValue ? r0.Value.ToString("F3") : "null (параметр не найден)")}");
#endif
            int insertedCount = r0.HasValue
                ? InsertWithGroundLevel(srcParam, vertices, r0.Value)
                : InsertAtVertices(srcParam, vertices);

            Msg($"\nВставлено {insertedCount} объектов вдоль полилинии.");
        }

        // Цикл повторяет запрос до корректного выбора McParametricObject или Escape.
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

        // Возвращает null, если объект не имеет атрибута rOtmetkaZemli
        private double? GetGroundLevelAttribute(McParametricObject paramObj)
        {
            foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
            {
                if (prop.Name != GROUND_LEVEL_ATTR) continue;
                return (double)prop.GetValue(paramObj);
            }
            return null;
        }

        // Change() используется вместо McProperty.SetValue(), поскольку SetValue()
        // не вызывает пересчёт скрипта, если новое значение совпадает со старым.
        private void SetGroundLevelViaChange(McParametricObject paramObj, double value)
        {
            var ev = new ExValue();
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

        // === Алгоритм выравнивания по уровню земли ===
        // Все стойки должны иметь одну базовую отметку вставки (Ycommon),
        // а индивидуальная высота над землёй задаётся через rOtmetkaZemli.
        // Ycommon = Ymin − R0, где R0 — значение атрибута у исходного объекта.
        private int InsertWithGroundLevel(
            McParametricObject source,
            List<Point3d> vertices,
            double r0)
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
            foreach (Point3d vertex in vertices)
            {
                try
                {
                    McParametricObject? copy = CloneParametricObject(source);
                    if (copy == null) continue;

                    var targetOrigin = new Point3d(vertex.X, yCommon, 0);
                    double rValue = vertex.Y - yCommon;

                    MoveObject(copy, targetOrigin);
                    SetGroundLevelViaChange(copy, rValue);
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