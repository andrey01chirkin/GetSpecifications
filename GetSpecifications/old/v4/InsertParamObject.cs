using System;
using System.Collections.Generic;
using Multicad;                                          // McObjectId, McObject, ExValue                                          // McObjectId, McObject, ExValue
using Multicad.DatabaseServices;
using Multicad.DatabaseServices.StandardObjects;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Runtime;
using HostMgd.ApplicationServices;

namespace GetSpecifications
{
    public class InsertParamObjectCommand
    {
        private const string GROUND_LEVEL_ATTR = "rOtmetkaZemli";

        private static void Msg(string text)
            => Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(text);

        [CommandMethod("InsertParamObject", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void InsertParamObject()
        {
            // --- Шаг 1: Выбор параметрического объекта ---
            string p1 = "\nВыберите параметрический объект: ";
            McObjectId srcId = McObjectManager.SelectObject(p1);
            if (srcId.IsNull) return;

            McObject srcObj = srcId.GetObject();
            if (!(srcObj is McParametricObject srcParam))
            {
                Msg($"\nНеобходимо выбрать параметрический объект СПДС" +
                    $" (выбран: {srcObj?.GetType().FullName ?? "null"})");
                return;
            }

            // --- Шаг 2: Выбор незамкнутой полилинии ---
            string p2 = "\nВыберите полилинию для простановки параметрического объекта: ";
            McObjectId plineId = McObjectManager.SelectObject(p2);
            if (plineId.IsNull) return;

            McObject plineObj = plineId.GetObject();
            if (!(plineObj is DbPolyline dbPoly))
            {
                Msg("\nНеобходимо выбрать полилинию");
                return;
            }

            Polyline3d pline = dbPoly.Polyline;
            if (pline == null || pline.ClosedLogically)
            {
                Msg("\nНеобходимо выбрать незамкнутую полилинию");
                return;
            }

            // --- Шаг 3: Получить все вершины полилинии ---
            List<Point3d> vertices = GetPolylineVertices(pline);
            if (vertices == null || vertices.Count == 0)
            {
                Msg("\nПолилиния не содержит вершин.");
                return;
            }

            // --- Шаг 4: Считать R0 = rOtmetkaZemli из шаблона ---
            double? r0 = GetGroundLevelAttribute(srcParam);

            // --- Шаг 5: Вставить копии ---
            int insertedCount;
            if (r0.HasValue)
            {
                // Алгоритм с rOtmetkaZemli:
                // Y0 = Y первой вершины, Ycommon = Y0 - R0
                // Для каждой вершины (Xi, Yi):
                //   Origin = (Xi, Ycommon, 0)
                //   rOtmetkaZemli = Yi - Ycommon
                insertedCount = InsertWithGroundLevel(srcParam, vertices, r0.Value);
            }
            else
            {
                // Атрибут отсутствует — вставляем точно в координаты вершин
                insertedCount = InsertAtVertices(srcParam, vertices);
            }

            Msg($"\nВставлено {insertedCount} объектов вдоль полилинии.");
        }

        // ---------------------------------------------------------------
        //  Получение вершин полилинии
        // ---------------------------------------------------------------
        private List<Point3d> GetPolylineVertices(Polyline3d polyline)
        {
            var result = new List<Point3d>();
            try
            {
                var points = polyline.Points;
                if (points != null && points.Count > 0)
                {
                    for (int i = 0; i < points.Count; i++)
                        result.Add(points[i]);
                    return result;
                }

                var vertices = polyline.Vertices;
                if (vertices != null && vertices.Count > 0)
                {
                    var vertList = vertices.GetVertices();
                    if (vertList != null)
                        foreach (var vert in vertList)
                            if (vert != null && vert.IsPoint())
                                result.Add(vert.Point);
                }
            }
            catch (Exception ex)
            {
                Msg($"\nОшибка получения вершин: {ex.Message}");
            }
            return result;
        }

        // ---------------------------------------------------------------
        //  Чтение и запись параметра rOtmetkaZemli
        //
        //  ExValue (Multicad) — контейнер "имя + значение" параметрических объектов.
        //  Свойства: Name (string), AsNumber (double), AsString, IsUndefined.
        //  GetRedefined() — коллекция ExValue переопределённых параметров.
        //  Change()       — задокументированный метод McParametricObject для смены параметров.
        // ---------------------------------------------------------------

        private double? GetGroundLevelAttribute(McParametricObject paramObj)
        {
            try
            {
                // GetParamLocalName — проверяем наличие параметра у данного типа объекта
                if (string.IsNullOrWhiteSpace(paramObj.GetParamLocalName(GROUND_LEVEL_ATTR)))
                    return null;

                // McProperty наследует PropertyDescriptor.
                // GetValue(component) — component = сам McParametricObject.
                foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
                {
                    if (!string.Equals(prop.Name, GROUND_LEVEL_ATTR,
                        StringComparison.OrdinalIgnoreCase)) continue;

                    object val = prop.GetValue(paramObj);
                    if (val is double d) return d;
                    if (val != null && double.TryParse(val.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double parsed))
                        return parsed;
                }
            }
            catch { }

            return null;
        }

        private void SetGroundLevelAttribute(McParametricObject paramObj, double value)
        {
            try
            {
                // McProperty.SetValue(component, value) — component = McParametricObject
                foreach (McProperty prop in paramObj.DbEntity.ObjectProperties)
                {
                    if (!string.Equals(prop.Name, GROUND_LEVEL_ATTR,
                        StringComparison.OrdinalIgnoreCase)) continue;

                    prop.SetValue(paramObj, value);
                    return;
                }
            }
            catch (Exception ex)
            {
                Msg($"\nОшибка установки {GROUND_LEVEL_ATTR}={value:F3}: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------
        //  Вставка С атрибутом rOtmetkaZemli
        //
        //  Алгоритм:
        //    Ymin    = min(Y всех вершин полилинии)
        //    Ycommon = Ymin - R0
        //    Для каждой вершины (Xi, Yi):
        //      Origin = (Xi, Ycommon, 0)
        //      rOtmetkaZemli = Yi - Ycommon
        // ---------------------------------------------------------------
        private int InsertWithGroundLevel(
            McParametricObject source,
            List<Point3d> vertices,
            double r0)
        {
            // Ymin = минимальная Y среди всех вершин полилинии
            double yMin = double.MaxValue;
            foreach (var v in vertices)
                if (v.Y < yMin) yMin = v.Y;

            // Ycommon = Ymin - R0
            // Объект на вершине с Ymin ставится ниже её ровно на R0
            double yCommon = yMin - r0;

            int count = 0;
            foreach (Point3d vertex in vertices)
            {
                try
                {
                    // Точка вставки: X из вершины, Y = Ycommon, Z = 0
                    Point3d insertPt = new Point3d(vertex.X, yCommon, 0);

                    // Значение rOtmetkaZemli для этой копии
                    double rValue = vertex.Y - yCommon;

                    McParametricObject copy = CloneParametricObject(source);
                    if (copy == null) continue;

                    // Устанавливаем позицию
                    copy.Origin = insertPt;

                    // Устанавливаем rOtmetkaZemli = Yi - Ycommon
                    SetGroundLevelAttribute(copy, rValue);

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
        //  Вставка БЕЗ атрибута rOtmetkaZemli — точно в вершины
        // ---------------------------------------------------------------
        private int InsertAtVertices(McParametricObject source, List<Point3d> vertices)
        {
            int count = 0;
            foreach (Point3d vertex in vertices)
            {
                try
                {
                    McParametricObject copy = CloneParametricObject(source);
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
        //  Клонирование
        // ---------------------------------------------------------------
        private McParametricObject CloneParametricObject(McParametricObject source)
        {
            try
            {
                McDbObject cloned = source.DbObject.Clone() as McDbObject;
                if (cloned == null)
                {
                    Msg("\nОшибка: Clone() вернул null");
                    return null;
                }

                cloned.AddToCurrentDocument();

                McParametricObject result = cloned.Entity as McParametricObject;
                if (result == null)
                    Msg($"\nОшибка: Entity вернул {cloned.Entity?.GetType().FullName ?? "null"}");

                return result;
            }
            catch (Exception ex)
            {
                Msg($"\nОшибка клонирования: {ex.Message}");
                return null;
            }
        }
    }
}
