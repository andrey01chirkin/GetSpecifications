using System;
using System.Collections.Generic;
using System.Linq;

// --- Классический .NET API nanoCAD ---
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;

// --- MultiCAD.NET API ---
using Multicad;
using Multicad.DatabaseServices;
using Multicad.Geometry;

[assembly: CommandClass(typeof(NanoCAD.InsertParamObject.InsertParamObjectCommand))]

namespace NanoCAD.InsertParamObject
{
    /// <summary>
    /// Команда InsertParamObject.
    /// Расставляет параметрический объект СПДС по вершинам полилинии.
    ///
    /// Алгоритм вставки:
    ///   6.1 — объект без rOtmetkaZemli: точка вставки = вершина полилинии.
    ///   6.2 — объект с rOtmetkaZemli:
    ///         Ycommon = Ymin ? R0
    ///         Origin_i = (Xi, Ycommon, 0)
    ///         rOtmetkaZemli_i = Yi ? Ycommon
    /// </summary>
    public class InsertParamObjectCommand
    {
        // Минимально допустимое значение rOtmetkaZemli (задано в объекте, зашито в требованиях)
        private const double MinOtmetka = 0.1;

        [CommandMethod("InsertParamObject", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            ed.WriteMessage("\n=== InsertParamObject: расстановка объектов по вершинам полилинии ===\n");

            // ??? Шаг 1: выбор параметрического объекта (шаблона) ????????????????????
            ObjectId templateId = SelectParamObject(ed);
            if (templateId == ObjectId.Null) return;

            // ??? Шаг 2: выбор полилинии ??????????????????????????????????????????????
            ObjectId polylineId = SelectPolyline(ed);
            if (polylineId == ObjectId.Null) return;

            // ??? Шаг 3: выполнить расстановку ????????????????????????????????????????
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Получаем вершины полилинии
                    var vertices = GetPolylineVertices(tr, polylineId);
                    if (vertices.Count == 0)
                    {
                        ed.WriteMessage("\nОшибка: полилиния не содержит вершин.\n");
                        tr.Abort();
                        return;
                    }

                    // Читаем шаблонный объект
                    DBObject templateObj = tr.GetObject(templateId, OpenMode.ForRead);

                    // Определяем наличие атрибута rOtmetkaZemli и его значение
                    double? r0 = GetRoOtmetkaZemli(templateId);

                    if (r0.HasValue)
                    {
                        // Алгоритм 6.2: объект имеет rOtmetkaZemli
                        ed.WriteMessage($"\nРежим 6.2: атрибут rOtmetkaZemli найден (R0 = {r0.Value}).\n");
                        PlaceWithOtmetka(tr, db, templateId, vertices, r0.Value, ed);
                    }
                    else
                    {
                        // Алгоритм 6.1: объект без rOtmetkaZemli
                        ed.WriteMessage("\nРежим 6.1: атрибут rOtmetkaZemli отсутствует.\n");
                        PlaceSimple(tr, db, templateId, vertices, ed);
                    }

                    tr.Commit();
                    ed.WriteMessage($"\nГотово: вставлено {vertices.Count} объектов.\n");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nОшибка при расстановке: {ex.Message}\n");
                    tr.Abort();
                }
            }
        }

        // ????????????????????????????????????????????????????????????????????????????
        // ВЫБОР ПАРАМЕТРИЧЕСКОГО ОБЪЕКТА (с повторным запросом при неверном выборе)
        // ????????????????????????????????????????????????????????????????????????????

        private ObjectId SelectParamObject(Editor ed)
        {
            while (true)
            {
                var opts = new PromptEntityOptions(
                    "\nВыберите параметрический объект СПДС и нажмите Enter: ");
                opts.SetRejectMessage(
                    "\nНеобходимо выбрать параметрический объект");
                // Допускаем любой объект — тип проверим сами (СПДС-объект не имеет
                // стандартного DXF-имени, которое можно указать в AllowedClasses)
                opts.AllowNone = true;

                PromptEntityResult result = ed.GetEntity(opts);

                if (result.Status == PromptStatus.None ||
                    result.Status == PromptStatus.Cancel)
                {
                    ed.WriteMessage("\nКоманда отменена.\n");
                    return ObjectId.Null;
                }

                if (result.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nНеобходимо выбрать параметрический объект\n");
                    continue;
                }

                // Проверяем, что выбранный объект является параметрическим объектом СПДС
                if (!IsParamObject(result.ObjectId))
                {
                    ed.WriteMessage("\nНеобходимо выбрать параметрический объект\n");
                    continue;
                }

                return result.ObjectId;
            }
        }

        // ????????????????????????????????????????????????????????????????????????????
        // ВЫБОР ПОЛИЛИНИИ (с повторным запросом при неверном выборе)
        // ????????????????????????????????????????????????????????????????????????????

        private ObjectId SelectPolyline(Editor ed)
        {
            while (true)
            {
                var opts = new PromptEntityOptions(
                    "\nВыберите полилинию для простановки объектов и нажмите Enter: ");
                opts.SetRejectMessage(
                    "\nНеобходимо выбрать полилинию для простановки параметрического объекта");
                opts.AddAllowedClass(typeof(Polyline), exactMatch: true);
                opts.AddAllowedClass(typeof(Polyline2d), exactMatch: true);
                opts.AddAllowedClass(typeof(Polyline3d), exactMatch: true);
                opts.AllowNone = true;

                PromptEntityResult result = ed.GetEntity(opts);

                if (result.Status == PromptStatus.None ||
                    result.Status == PromptStatus.Cancel)
                {
                    ed.WriteMessage("\nКоманда отменена.\n");
                    return ObjectId.Null;
                }

                if (result.Status != PromptStatus.OK)
                {
                    ed.WriteMessage(
                        "\nНеобходимо выбрать полилинию для простановки параметрического объекта\n");
                    continue;
                }

                // Дополнительная проверка типа (на случай если фильтр не сработал)
                using (var tr = result.ObjectId.Database.TransactionManager.StartTransaction())
                {
                    DBObject obj = tr.GetObject(result.ObjectId, OpenMode.ForRead);
                    tr.Commit();
                    if (obj is Polyline || obj is Polyline2d || obj is Polyline3d)
                        return result.ObjectId;
                }

                ed.WriteMessage(
                    "\nНеобходимо выбрать полилинию для простановки параметрического объекта\n");
            }
        }

        // ????????????????????????????????????????????????????????????????????????????
        // АЛГОРИТМ 6.1 — вставка без rOtmetkaZemli: точки вставки = вершины
        // ????????????????????????????????????????????????????????????????????????????

        private void PlaceSimple(
            Transaction tr,
            Database db,
            ObjectId templateId,
            List<Point3d> vertices,
            Editor ed)
        {
            // Точка вставки шаблона (исходная)
            Point3d templateOrigin = GetInsertionPoint(tr, templateId);

            foreach (Point3d vertex in vertices)
            {
                // Вектор смещения: новая точка вставки — вершина полилинии
                Vector3d displacement = vertex - templateOrigin;
                ObjectId newId = CopyAndDisplace(tr, db, templateId, displacement);

                ed.WriteMessage($"  Вставлен объект в ({vertex.X:F3}, {vertex.Y:F3})\n");
            }
        }

        // ????????????????????????????????????????????????????????????????????????????
        // АЛГОРИТМ 6.2 — вставка с rOtmetkaZemli
        // ????????????????????????????????????????????????????????????????????????????

        private void PlaceWithOtmetka(
            Transaction tr,
            Database db,
            ObjectId templateId,
            List<Point3d> vertices,
            double r0,
            Editor ed)
        {
            // Шаг 1: Ymin по всем вершинам
            double yMin = vertices.Min(v => v.Y);

            // Шаг 2: общий уровень вставки
            // Ycommon = Ymin ? R0
            double yCommon = yMin - r0;

            ed.WriteMessage($"  Ymin = {yMin:F3}, R0 = {r0:F3}, Ycommon = {yCommon:F3}\n");

            // Точка вставки шаблона (исходная)
            Point3d templateOrigin = GetInsertionPoint(tr, templateId);

            foreach (Point3d vertex in vertices)
            {
                // Новая точка вставки: Xi — из вершины, Y = Ycommon
                Point3d newOrigin = new Point3d(vertex.X, yCommon, 0.0);
                Vector3d displacement = newOrigin - templateOrigin;

                // Значение rOtmetkaZemli для этой копии
                // rOtmetkaZemli_i = Yi ? Ycommon  (= R0 + (Yi ? Ymin))
                double otmetkaValue = vertex.Y - yCommon;

                // Применяем минимальный порог
                if (otmetkaValue < MinOtmetka)
                    otmetkaValue = MinOtmetka;

                // Копируем и смещаем
                ObjectId newId = CopyAndDisplace(tr, db, templateId, displacement);

                // Устанавливаем значение rOtmetkaZemli для копии
                SetRoOtmetkaZemli(newId, otmetkaValue);

                ed.WriteMessage(
                    $"  Вставлен объект в ({newOrigin.X:F3}, {newOrigin.Y:F3}), " +
                    $"rOtmetkaZemli = {otmetkaValue:F3}\n");
            }
        }

        // ????????????????????????????????????????????????????????????????????????????
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ????????????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Определяет, является ли объект параметрическим объектом СПДС.
        ///
        /// Параметрические объекты СПДС из базы элементов в чертеже представлены
        /// как McCustomObject. В DXF они имеют имя, начинающееся с "MCAD_" или "SP_STD",
        /// и в свойствах содержат кастомные параметры (CustomProperties/ObjectProperties).
        ///
        /// Метод использует два способа проверки:
        ///   1. MultiCAD API: McObjectId ? McDbEntity ? TypeName
        ///   2. Классический API: проверка DxfName объекта
        ///
        /// ВАЖНО: Точное значение DxfName или TypeName для СПДС-объектов зависит
        /// от версии nanoCAD и СПДС. Уточните через командную строку nanoCAD:
        ///   (entget (car (entsel))) — посмотрите поле "0" (тип объекта)
        /// или через Intellisense в отладчике.
        /// </summary>
        private bool IsParamObject(ObjectId objId)
        {
            // ?? Способ 1: MultiCAD API ??????????????????????????????????????????????
            try
            {
                McObjectId mcId = McObjectId.FromOldId((long)objId.OldIdPtr);
                if (mcId.IsValid)
                {
                    McDbEntity mcEntity = mcId.GetObject() as McDbEntity;
                    if (mcEntity != null)
                    {
                        string typeName = mcEntity.GetType().Name;
                        // Параметрические объекты СПДС: тип содержит "StdItem",
                        // "CustomObject" или "SpItem" — уточните по вашей версии SDK
                        if (typeName.IndexOf("StdItem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            typeName.IndexOf("SpStd", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            typeName.IndexOf("SpItem", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }

                        // Проверка через наличие ключевых свойств СПДС
                        var props = mcEntity.ObjectProperties;
                        if (props != null && props.Count > 0)
                        {
                            // СПДС-объекты имеют специфические системные свойства
                            // Если среди них есть "ID" или другие служебные — это СПДС
                            foreach (McObjectProperty prop in props)
                            {
                                string pName = prop.Name;
                                if (pName == "ID" || pName == "Name" ||
                                    pName.StartsWith("r") || pName.StartsWith("n") ||
                                    pName.StartsWith("s"))
                                {
                                    // Дополнительно: СПДС-объекты НЕ являются
                                    // обычными примитивами (линия, дуга и т.д.)
                                    if (!(mcEntity is McDbPolyline) &&
                                        !(mcEntity is McDbLine) &&
                                        !(mcEntity is McDbArc) &&
                                        !(mcEntity is McDbCircle) &&
                                        !(mcEntity is McDbText))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* игнорируем — пробуем классический API */ }

            // ?? Способ 2: Классический API (Teigha) ????????????????????????????????
            try
            {
                using (var tr = objId.Database.TransactionManager.StartTransaction())
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForRead);
                    tr.Commit();

                    string dxfName = obj.GetRXClass()?.DxfName ?? string.Empty;

                    // Параметрические объекты СПДС в DXG выглядят как кастомные объекты
                    // Типичные DXF-имена: "MCAD_DBOBJ", "SpStdItem", "NC_SPDS_ITEM" и т.п.
                    // Уточните через: Application.Invoke("(entget (car (entsel)))")
                    if (dxfName.IndexOf("MCAD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dxfName.IndexOf("SpStd", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dxfName.IndexOf("SPDS", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    // Если объект не является стандартным примитивом — возможно СПДС
                    bool isStandardPrimitive =
                        obj is Polyline || obj is Polyline2d || obj is Polyline3d ||
                        obj is Line || obj is Arc || obj is Circle ||
                        obj is DBText || obj is MText || obj is BlockReference ||
                        obj is Hatch || obj is Dimension;

                    if (!isStandardPrimitive)
                    {
                        // Проверяем XData — СПДС-объекты хранят параметры в XData
                        ResultBuffer xdata = obj.GetXDataForApplication(null);
                        if (xdata != null)
                        {
                            var typedValues = xdata.AsArray();
                            xdata.Dispose();
                            // Если есть XData — скорее всего кастомный СПДС-объект
                            return typedValues.Length > 0;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Возвращает значение атрибута rOtmetkaZemli через MultiCAD API.
        /// Если атрибут отсутствует — возвращает null.
        /// </summary>
        private double? GetRoOtmetkaZemli(ObjectId objId)
        {
            // ?? MultiCAD API ????????????????????????????????????????????????????????
            try
            {
                McObjectId mcId = McObjectId.FromOldId((long)objId.OldIdPtr);
                if (mcId.IsValid)
                {
                    McDbEntity mcEntity = mcId.GetObject() as McDbEntity;
                    if (mcEntity != null)
                    {
                        McObjectPropertyCollection props = mcEntity.ObjectProperties;
                        if (props != null)
                        {
                            foreach (McObjectProperty prop in props)
                            {
                                if (string.Equals(prop.Name, "rOtmetkaZemli",
                                    StringComparison.OrdinalIgnoreCase))
                                {
                                    object val = prop.Value;
                                    if (val != null && double.TryParse(
                                        val.ToString(),
                                        System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out double dVal))
                                    {
                                        return dVal;
                                    }
                                    // Атрибут есть, но значение не число — возвращаем 0
                                    return 0.0;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // ?? Классический API: XData / ExtensionDictionary ???????????????????????
            try
            {
                using (var tr = objId.Database.TransactionManager.StartTransaction())
                {
                    DBObject obj = tr.GetObject(objId, OpenMode.ForRead);

                    // Попытка 1: XData
                    ResultBuffer xdata = obj.GetXDataForApplication("SPDS");
                    if (xdata != null)
                    {
                        foreach (TypedValue tv in xdata)
                        {
                            if (tv.TypeCode == (short)DxfCode.ExtendedDataReal)
                            {
                                tr.Commit();
                                return (double)tv.Value;
                            }
                        }
                        xdata.Dispose();
                    }

                    // Попытка 2: ExtensionDictionary
                    if (!obj.ExtensionDictionary.IsNull)
                    {
                        DBDictionary dict =
                            tr.GetObject(obj.ExtensionDictionary, OpenMode.ForRead)
                            as DBDictionary;
                        if (dict != null && dict.Contains("rOtmetkaZemli"))
                        {
                            DBObject entry = tr.GetObject(
                                dict.GetAt("rOtmetkaZemli"), OpenMode.ForRead);
                            if (entry is Xrecord xrec)
                            {
                                foreach (TypedValue tv in xrec.Data)
                                {
                                    if (tv.TypeCode == (short)DxfCode.Real)
                                    {
                                        tr.Commit();
                                        return (double)tv.Value;
                                    }
                                }
                            }
                        }
                    }

                    tr.Commit();
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Устанавливает значение атрибута rOtmetkaZemli для объекта.
        /// Обновление производится через MultiCAD API (ObjectProperties)
        /// или через XData в классическом API.
        /// </summary>
        private void SetRoOtmetkaZemli(ObjectId objId, double value)
        {
            // ?? MultiCAD API ????????????????????????????????????????????????????????
            try
            {
                McObjectId mcId = McObjectId.FromOldId((long)objId.OldIdPtr);
                if (mcId.IsValid)
                {
                    McDbEntity mcEntity = mcId.GetObject() as McDbEntity;
                    if (mcEntity != null && !mcEntity.IsReadOnly)
                    {
                        McObjectPropertyCollection props = mcEntity.ObjectProperties;
                        if (props != null)
                        {
                            foreach (McObjectProperty prop in props)
                            {
                                if (string.Equals(prop.Name, "rOtmetkaZemli",
                                    StringComparison.OrdinalIgnoreCase))
                                {
                                    prop.Value = value;
                                    mcEntity.DbEntity.Update();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // ?? Классический API: XData ?????????????????????????????????????????????
            try
            {
                Database db = objId.Database;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // Регистрируем appName если нужно
                    RegAppTable rat =
                        tr.GetObject(db.RegAppTableId, OpenMode.ForWrite) as RegAppTable;
                    if (rat != null && !rat.Has("SPDS_OTMETKA"))
                    {
                        var record = new RegAppTableRecord { Name = "SPDS_OTMETKA" };
                        rat.Add(record);
                        tr.AddNewlyCreatedDBObject(record, true);
                    }

                    DBObject obj = tr.GetObject(objId, OpenMode.ForWrite);
                    using (var rb = new ResultBuffer(
                        new TypedValue((int)DxfCode.ExtendedDataRegAppName, "SPDS_OTMETKA"),
                        new TypedValue((int)DxfCode.ExtendedDataAsciiString, "rOtmetkaZemli"),
                        new TypedValue((int)DxfCode.ExtendedDataReal, value)))
                    {
                        obj.XData = rb;
                    }

                    tr.Commit();
                }
            }
            catch { }
        }

        /// <summary>
        /// Возвращает точку вставки параметрического объекта.
        /// Для СПДС-объектов с Origin/Position использует McGePoint3d,
        /// для остальных — GeometricExtents.MinPoint.
        /// </summary>
        private Point3d GetInsertionPoint(Transaction tr, ObjectId objId)
        {
            // ?? MultiCAD API ????????????????????????????????????????????????????????
            try
            {
                McObjectId mcId = McObjectId.FromOldId((long)objId.OldIdPtr);
                if (mcId.IsValid)
                {
                    McDbEntity mcEntity = mcId.GetObject() as McDbEntity;
                    if (mcEntity != null)
                    {
                        // Пробуем получить Origin через reflection (разные версии API)
                        var originProp = mcEntity.GetType().GetProperty("Origin")
                                      ?? mcEntity.GetType().GetProperty("Position")
                                      ?? mcEntity.GetType().GetProperty("InsertPoint");
                        if (originProp != null)
                        {
                            object originVal = originProp.GetValue(mcEntity);
                            if (originVal is McGePoint3d mcPt)
                                return new Point3d(mcPt.X, mcPt.Y, mcPt.Z);
                        }

                        // Через ObjectProperties
                        var props = mcEntity.ObjectProperties;
                        if (props != null)
                        {
                            foreach (McObjectProperty prop in props)
                            {
                                if (prop.Name == "Origin" || prop.Name == "Position")
                                {
                                    if (prop.Value is McGePoint3d pt)
                                        return new Point3d(pt.X, pt.Y, pt.Z);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // ?? Классический API ????????????????????????????????????????????????????
            DBObject obj = tr.GetObject(objId, OpenMode.ForRead);
            try
            {
                Extents3d ext = obj.GeometricExtents;
                // Для СПДС объектов точка вставки ? MinPoint extents
                return ext.MinPoint;
            }
            catch
            {
                return Point3d.Origin;
            }
        }

        /// <summary>
        /// Получает вершины полилинии (Polyline, Polyline2d, Polyline3d).
        /// </summary>
        private List<Point3d> GetPolylineVertices(Transaction tr, ObjectId polylineId)
        {
            var result = new List<Point3d>();
            DBObject obj = tr.GetObject(polylineId, OpenMode.ForRead);

            if (obj is Polyline pl)
            {
                for (int i = 0; i < pl.NumberOfVertices; i++)
                    result.Add(pl.GetPoint3dAt(i));
            }
            else if (obj is Polyline2d pl2d)
            {
                foreach (ObjectId vertId in pl2d)
                {
                    var vert = tr.GetObject(vertId, OpenMode.ForRead) as Vertex2d;
                    if (vert != null)
                        result.Add(new Point3d(vert.Position.X, vert.Position.Y, 0));
                }
            }
            else if (obj is Polyline3d pl3d)
            {
                foreach (ObjectId vertId in pl3d)
                {
                    var vert = tr.GetObject(vertId, OpenMode.ForRead) as PolylineVertex3d;
                    if (vert != null)
                        result.Add(vert.Position);
                }
            }

            return result;
        }

        /// <summary>
        /// Копирует объект и смещает копию на заданный вектор.
        ///
        /// Предпочтительный метод — MultiCAD Clone() + Transform.
        /// Резервный — классический DeepCloneObjects + TransformBy.
        /// </summary>
        private ObjectId CopyAndDisplace(
            Transaction tr,
            Database db,
            ObjectId sourceId,
            Vector3d displacement)
        {
            // ?? MultiCAD API ????????????????????????????????????????????????????????
            try
            {
                McObjectId mcSrcId = McObjectId.FromOldId((long)sourceId.OldIdPtr);
                if (mcSrcId.IsValid)
                {
                    McDbEntity srcEntity = mcSrcId.GetObject() as McDbEntity;
                    if (srcEntity != null)
                    {
                        // Клонируем
                        McDbEntity clone = srcEntity.Clone() as McDbEntity;
                        if (clone != null)
                        {
                            // Смещение через матрицу трансформации
                            McGeMatrix3d mx = McGeMatrix3d.Translation(
                                new McGeVector3d(displacement.X, displacement.Y, displacement.Z));
                            clone.DbEntity.TransformBy(mx);

                            // Добавляем в текущий документ
                            clone.DbEntity.AddToCurrentDocument();

                            // Возвращаем ObjectId через McObjectId
                            long newIdPtr = clone.DbEntity.ObjectId.Ptr;
                            return new ObjectId(new IntPtr(newIdPtr));
                        }
                    }
                }
            }
            catch { }

            // ?? Классический API ????????????????????????????????????????????????????
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord mspace =
                    tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite)
                    as BlockTableRecord;

                // DeepClone одного объекта в пространство модели
                var idCollection = new ObjectIdCollection { sourceId };
                var mapping = new IdMapping();
                db.DeepCloneObjects(idCollection, mspace.ObjectId, mapping, false);

                // Находим id клона
                IdPair pair = mapping[sourceId];
                ObjectId cloneId = pair.Value;

                // Применяем смещение
                Entity cloneEnt = tr.GetObject(cloneId, OpenMode.ForWrite) as Entity;
                if (cloneEnt != null)
                {
                    cloneEnt.TransformBy(Matrix3d.Displacement(displacement));
                }

                return cloneId;
            }
        }
    }
}