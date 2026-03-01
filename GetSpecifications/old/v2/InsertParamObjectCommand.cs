using Multicad;
using Multicad.AplicationServices;
using Multicad.CustomObjectBase;
using Multicad.DatabaseServices;
using Multicad.DatabaseServices.StandardObjects;
using Multicad.Geometry;
using Multicad.Runtime;
using System;
using System.Collections.Generic;

namespace GetSpecifications
{
    public class InsertParamObjectCommand
    {
        // Имя атрибута отметки земли
        private const string GROUND_LEVEL_ATTR = "rOtmetkaZemli";

        [CommandMethod("InsertParamObject", CommandFlags.NoCheck | CommandFlags.NoPrefix)]
        public void InsertParamObject()
        {
            var doc = McDocumentsManager.GetActiveDoc();
            if (doc == null) return;

            // --- Шаг 1: Выбор параметрического объекта ---
            McObjectId paramObjId = SelectParametricObject(doc);
            if (paramObjId.IsNull) return;

            // --- Шаг 2: Выбор незамкнутой полилинии ---
            Polyline3d polyline = SelectPolyline(doc);
            if (polyline == null) return;

            // --- Шаг 3: Получить все вершины полилинии ---
            List<Point3d> vertices = GetPolylineVertices(polyline);
            if (vertices == null || vertices.Count == 0)
            {
                McDocumentsManager.GetActiveDoc().Editor.PutMessage(
                    "\nПолилиния не содержит вершин.");
                return;
            }

            // --- Шаг 4: Определить отметку земли из оригинального объекта ---
            double? groundLevel = GetGroundLevelAttribute(paramObjId);

            // --- Шаг 5: Вставить копии на каждую вершину ---
            InsertCopiesAtVertices(paramObjId, vertices, groundLevel, doc);

            doc.Editor.PutMessage(
                $"\nВставлено {vertices.Count} объектов вдоль полилинии.");
        }

        /// <summary>
        /// Запрашивает пользователя выбрать параметрический объект.
        /// Повторяет запрос если выбранный объект не параметрический.
        /// </summary>
        private McObjectId SelectParametricObject(IMcDocument doc)
        {
            while (true)
            {
                doc.Editor.PutMessage(
                    "\nВыберите параметрический объект: ");

                McObjectId id = doc.Editor.SelectObject();

                if (id.IsNull)
                {
                    doc.Editor.PutMessage(
                        "\nОбъект не выбран. Команда отменена.");
                    return McObjectId.Null;
                }

                // Проверка: является ли объект параметрическим (McCustomBase)
                McObject obj = id.GetObject();
                if (obj is McCustomBase customObj &&
                    IsParametricObject(customObj))
                {
                    return id;
                }

                doc.Editor.PutMessage(
                    "\nНеобходимо выбрать параметрический объект");
                // Продолжаем цикл
            }
        }

        /// <summary>
        /// Проверяет, является ли объект параметрическим (имеет переопределяемые параметры).
        /// </summary>
        private bool IsParametricObject(McCustomBase obj)
        {
            try
            {
                // Параметрический объект должен реализовывать IMcParameterRedefinitions
                // или иметь атрибуты СПДС
                if (obj is IMcParameterRedefinitions paramObj)
                {
                    return true;
                }

                // Дополнительная проверка: наличие DbObject с атрибутами
                var dbObj = obj.DbObject;
                if (dbObj != null)
                {
                    // Попытка получить атрибуты объекта СПДС
                    var attrs = dbObj.GetDynProperties();
                    return attrs != null && attrs.Count > 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Запрашивает пользователя выбрать незамкнутую полилинию.
        /// Повторяет запрос при неверном выборе.
        /// </summary>
        private Polyline3d SelectPolyline(IMcDocument doc)
        {
            while (true)
            {
                doc.Editor.PutMessage(
                    "\nВыберите полилинию для простановки параметрического объекта: ");

                McObjectId id = doc.Editor.SelectObject();

                if (id.IsNull)
                {
                    doc.Editor.PutMessage(
                        "\nОбъект не выбран. Команда отменена.");
                    return null;
                }

                McObject obj = id.GetObject();

                // Проверка: является ли объект полилинией DbPolyline
                if (obj is DbPolyline dbPoly)
                {
                    Polyline3d pline = dbPoly.Polyline;

                    // Проверка замкнутости
                    if (pline != null && pline.ClosedLogically)
                    {
                        doc.Editor.PutMessage(
                            "\nНеобходимо выбрать незамкнутую полилинию");
                        continue;
                    }

                    return pline;
                }
                else
                {
                    doc.Editor.PutMessage(
                        "\nНеобходимо выбрать полилинию для простановки параметрического объекта");
                    // Продолжаем цикл
                }
            }
        }

        /// <summary>
        /// Получает список вершин полилинии как Point3d.
        /// </summary>
        private List<Point3d> GetPolylineVertices(Polyline3d polyline)
        {
            var result = new List<Point3d>();

            try
            {
                // Метод 1: Через Points (PointDataOfPolyline)
                var points = polyline.Points;
                if (points != null && points.Count > 0)
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        result.Add(points[i]);
                    }
                    return result;
                }

                // Метод 2: Через Vertices (VertexDataOfPolyline)
                var vertices = polyline.Vertices;
                if (vertices != null && vertices.Count > 0)
                {
                    var vertList = vertices.GetVertices();
                    if (vertList != null)
                    {
                        foreach (var vert in vertList)
                        {
                            if (vert != null && vert.IsPoint())
                            {
                                result.Add(vert.Point);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                McDocumentsManager.GetActiveDoc()?.Editor
                    .PutMessage($"\nОшибка получения вершин: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Получает значение атрибута отметки земли из параметрического объекта.
        /// Возвращает null если атрибут отсутствует.
        /// </summary>
        private double? GetGroundLevelAttribute(McObjectId paramObjId)
        {
            try
            {
                McObject obj = paramObjId.GetObject();
                if (obj == null) return null;

                var dbObj = obj.DbObject;
                if (dbObj == null) return null;

                // Получаем динамические свойства (атрибуты СПДС)
                var props = dbObj.GetDynProperties();
                if (props == null) return null;

                foreach (var prop in props)
                {
                    if (string.Equals(prop.Name, GROUND_LEVEL_ATTR,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value is double dVal)
                            return dVal;

                        if (prop.Value != null &&
                            double.TryParse(prop.Value.ToString(),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out double parsed))
                        {
                            return parsed;
                        }

                        return null;
                    }
                }
            }
            catch
            {
                // Атрибут отсутствует — это нормально
            }

            return null;
        }

        /// <summary>
        /// Вставляет копии параметрического объекта в каждую вершину полилинии.
        /// </summary>
        private void InsertCopiesAtVertices(
            McObjectId sourceId,
            List<Point3d> vertices,
            double? groundLevel,
            IMcDocument doc)
        {
            McObject sourceObj = sourceId.GetObject();
            if (sourceObj == null) return;

            foreach (Point3d vertex in vertices)
            {
                try
                {
                    // Определяем целевую точку вставки
                    Point3d insertPt = CalculateInsertPoint(vertex, groundLevel);

                    // Создаём копию объекта
                    McCustomBase copy = CloneParametricObject(sourceObj, doc);
                    if (copy == null) continue;

                    // Позиционируем копию
                    PositionObject(copy, insertPt);
                }
                catch (Exception ex)
                {
                    doc.Editor.PutMessage(
                        $"\nОшибка при вставке объекта в точку {vertex}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Вычисляет точку вставки с учётом отметки земли.
        /// </summary>
        private Point3d CalculateInsertPoint(Point3d vertex, double? groundLevel)
        {
            if (!groundLevel.HasValue)
            {
                // Атрибут отсутствует — вставляем точно в координаты вершины
                return vertex;
            }
            else
            {
                // Атрибут присутствует — X совпадает с вершиной,
                // Y смещается на значение rOtmetkaZemli
                return new Point3d(
                    vertex.X,
                    vertex.Y + groundLevel.Value,
                    vertex.Z
                );
            }
        }

        /// <summary>
        /// Создаёт клон параметрического объекта и добавляет его в документ.
        /// </summary>
        private McCustomBase CloneParametricObject(McObject sourceObj, IMcDocument doc)
        {
            try
            {
                if (sourceObj is McCustomBase customBase)
                {
                    // Клонируем объект через БД документа
                    McCustomBase clone = (McCustomBase)customBase.Clone();
                    if (clone == null) return null;

                    // Добавляем в БД текущего документа
                    doc.Database.AppendObject(clone.DbObject);
                    return clone;
                }
            }
            catch (Exception ex)
            {
                McDocumentsManager.GetActiveDoc()?.Editor
                    .PutMessage($"\nОшибка клонирования: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Позиционирует объект в указанную точку через матрицу трансляции.
        /// </summary>
        private void PositionObject(McCustomBase obj, Point3d targetPoint)
        {
            // Создаём вектор смещения от начала координат до целевой точки
            Vector3d displacement = targetPoint - Point3d.Origin;

            // Создаём матрицу трансляции
            Matrix3d translationMatrix = Matrix3d.Displacement(displacement);

            // Применяем трансформацию
            obj.DbEntity.TransformBy(translationMatrix);
        }
    }
}