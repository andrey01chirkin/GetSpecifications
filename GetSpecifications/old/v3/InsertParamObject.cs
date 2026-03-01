using Multicad;                                          // McObjectId, McObject
using Multicad.DatabaseServices;                        // McObjectManager (static), McDbObject
using Multicad.DatabaseServices.StandardObjects;        // DbPolyline, Polyline3d
using Multicad.Geometry;                                // Point3d, Vector3d, Matrix3d
using Multicad.Objects;                                 // McParametricObject
using Multicad.Runtime;                                 // CommandMethod, CommandFlags
using HostMgd.ApplicationServices;                      // Application

namespace GetSpecifications
{
    public class InsertParamObjectCommand
    {
        private const string GROUND_LEVEL_ATTR = "rOtmetkaZemli";

        private static void Msg(string text)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(text);
        }

        [CommandMethod("InsertParamObject", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void InsertParamObject()
        {
            McObjectId paramObjId = SelectParametricObject();
            if (paramObjId.IsNull) return;

            Polyline3d polyline = SelectPolyline();
            if (polyline == null) return;

            List<Point3d> vertices = GetPolylineVertices(polyline);
            if (vertices == null || vertices.Count == 0)
            {
                Msg("\nПолилиния не содержит вершин.");
                return;
            }

            double? groundLevel = GetGroundLevelAttribute(paramObjId);

            int insertedCount = InsertCopiesAtVertices(paramObjId, vertices, groundLevel);
            Msg($"\nВставлено {insertedCount} объектов вдоль полилинии.");
        }

        private McObjectId SelectParametricObject()
        {
            while (true)
            {
                string prompt = "\nВыберите параметрический объект: ";
                McObjectId id = McObjectManager.SelectObject(prompt);

                if (id.IsNull)
                {
                    Msg("\nОбъект не выбран. Команда отменена.");
                    return McObjectId.Null;
                }

                McObject obj = id.GetObject();
                if (obj is McParametricObject)
                    return id;

                Msg($"\nНеобходимо выбрать параметрический объект СПДС" +
                    $" (выбран тип: {obj?.GetType().FullName ?? "null"})");
            }
        }

        private Polyline3d SelectPolyline()
        {
            while (true)
            {
                string prompt = "\nВыберите полилинию для простановки параметрического объекта: ";
                McObjectId id = McObjectManager.SelectObject(prompt);

                if (id.IsNull)
                {
                    Msg("\nОбъект не выбран. Команда отменена.");
                    return null;
                }

                McObject obj = id.GetObject();

                if (obj is DbPolyline dbPoly)
                {
                    Polyline3d pline = dbPoly.Polyline;
                    if (pline != null && pline.ClosedLogically)
                    {
                        Msg("\nНеобходимо выбрать незамкнутую полилинию");
                        continue;
                    }
                    return pline;
                }

                Msg("\nНеобходимо выбрать полилинию для простановки параметрического объекта");
            }
        }

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

        private double? GetGroundLevelAttribute(McObjectId paramObjId)
        {
            try
            {
                McObject obj = paramObjId.GetObject();
                if (!(obj is McParametricObject paramObj)) return null;

                McDbObject dbObj = paramObj.DbObject;
                if (dbObj == null) return null;

                try
                {
                    var props = dbObj.CustomProperties;
                    if (props != null)
                    {
                        double? v = ExtractDoubleByName(props, GROUND_LEVEL_ATTR);
                        if (v.HasValue) return v;
                    }
                }
                catch { }

                try
                {
                    var props = dbObj.ObjectProperties;
                    if (props != null)
                    {
                        double? v = ExtractDoubleByName(props, GROUND_LEVEL_ATTR);
                        if (v.HasValue) return v;
                    }
                }
                catch { }
            }
            catch { }

            return null;
        }

        private double? ExtractDoubleByName(dynamic props, string name)
        {
            try
            {
                foreach (var prop in props)
                {
                    string propName = prop.Name as string ?? prop.Name?.ToString();
                    if (string.Equals(propName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        object val = prop.Value;
                        if (val is double d) return d;
                        if (val != null && double.TryParse(val.ToString(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double parsed))
                            return parsed;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Вставляет копии объекта во все вершины полилинии.
        /// Возвращает количество реально вставленных объектов.
        /// </summary>
        private int InsertCopiesAtVertices(
            McObjectId sourceId,
            List<Point3d> vertices,
            double? groundLevel)
        {
            McObject sourceObj = sourceId.GetObject();
            if (!(sourceObj is McParametricObject sourceParam))
            {
                Msg("\nОшибка: исходный объект не является McParametricObject");
                return 0;
            }

            int count = 0;
            foreach (Point3d vertex in vertices)
            {
                try
                {
                    Point3d insertPt = CalculateInsertPoint(vertex, groundLevel);
                    McParametricObject copy = CloneParametricObject(sourceParam);
                    if (copy == null)
                    {
                        Msg($"\nОшибка: не удалось клонировать объект для точки {vertex}");
                        continue;
                    }

                    // Позиционируем через свойство Origin (задокументировано в McParametricObject)
                    // Origin — "Положение первого штриха", т.е. точка вставки объекта
                    copy.Origin = insertPt;

                    // Обновляем объект после изменения позиции
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

        private Point3d CalculateInsertPoint(Point3d vertex, double? groundLevel)
        {
            if (!groundLevel.HasValue) return vertex;
            return new Point3d(vertex.X, vertex.Y + groundLevel.Value, vertex.Z);
        }

        /// <summary>
        /// Клонирует McParametricObject и добавляет в текущий документ.
        /// </summary>
        private McParametricObject CloneParametricObject(McParametricObject source)
        {
            try
            {
                McDbObject srcDbObj = source.DbObject;
                if (srcDbObj == null) return null;

                // Clone() — метод McDbObject
                McDbObject cloned = srcDbObj.Clone() as McDbObject;
                if (cloned == null)
                {
                    Msg("\nОшибка: Clone() вернул null");
                    return null;
                }

                // AddToCurrentDocument() — метод McDbObject,
                // добавляет объект в активный документ
                cloned.AddToCurrentDocument();

                // Entity — свойство McDbObject, возвращает бизнес-объект
                McParametricObject result = cloned.Entity as McParametricObject;
                if (result == null)
                    Msg($"\nОшибка: Entity вернул {cloned.Entity?.GetType().FullName ?? "null"}, ожидался McParametricObject");

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
