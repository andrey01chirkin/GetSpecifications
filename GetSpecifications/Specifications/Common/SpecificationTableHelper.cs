using Multicad.Geometry;
using Multicad.Symbols.Tables;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using Teigha.DatabaseServices;

namespace GetSpecifications.Specifications.Common
{
    public static class SpecificationTableHelper
    {
        private static Editor Ed => Application.DocumentManager.MdiActiveDocument.Editor;

        private static void Msg(string text)
            => Ed.WriteMessage(text);

        /// <summary>
        /// Гарантирует наличие в чертеже слоя и текстового стиля для таблиц спецификаций.
        /// Вызывается перед созданием любой таблицы.
        /// </summary>
        public static void EnsureDrawingResources()
        {
            EnsureLayerExists(SpecificationObjectConstants.LAYER_NAME);
            EnsureTextStyleExists(
                SpecificationObjectConstants.TEXT_STYLE,
                SpecificationObjectConstants.TEXT_FONT_FILE);
        }

        /// <summary>
        /// Создаёт McTable со стандартной структурой секций и шапкой.
        /// Схема секций: HeaderFirst(0, 2) — строки 0 (заголовок) и 1 (шапка);
        /// None(2, dataRows) — строка 2 служебная, рабочие данные начинаются с FIRST_DATA_ROW = 3.
        /// </summary>
        public static McTable CreateStandardTable(string title, int dataRows)
        {
            var table = new McTable();

            table.DefaultCell.HorizontalTextAlign = HorizTextAlign.Center;
            table.DefaultCell.VerticalTextAlign = VertTextAlign.Center;

            table.Columns.AddRange(0, SpecificationTableLayout.TABLE_COLS);
            table.Columns[0].Width = SpecificationTableLayout.COL_W_POZ;
            table.Columns[1].Width = SpecificationTableLayout.COL_W_OBZ;
            table.Columns[2].Width = SpecificationTableLayout.COL_W_NAIM;
            table.Columns[3].Width = SpecificationTableLayout.COL_W_KOL;
            table.Columns[4].Width = SpecificationTableLayout.COL_W_MASS;
            table.Columns[5].Width = SpecificationTableLayout.COL_W_PRIM;

            table.Rows.InsertSection(SectionTypeEnum.HeaderFirst, 0, 2);
            table.Rows.InsertSection(SectionTypeEnum.None, 2, dataRows);

            int lastDataRow = SpecificationTableLayout.FIRST_DATA_ROW + dataRows - 1;

            table.Rows[SpecificationTableLayout.TITLE_ROW].Height = SpecificationTableLayout.ROW_H_TITLE;
            table.Rows[SpecificationTableLayout.HEADER_ROW].Height = SpecificationTableLayout.ROW_H_HDR;

            for (int r = SpecificationTableLayout.FIRST_DATA_ROW; r <= lastDataRow; r++)
                table.Rows[r].Height = SpecificationTableLayout.ROW_H_DATA;

            FillTitleRow(table, title);
            FillHeaderRow(table);

            return table;
        }

        /// <summary>Добавляет таблицу в документ, задаёт слой и смещает в точку вставки.</summary>
        public static void AddTableToDocument(McTable table, Point3d insertPt)
        {
            table.DbObject.AddToCurrentDocument();
            table.DbEntity.Layer = SpecificationObjectConstants.LAYER_NAME;
            table.DbEntity.Transform(
                Matrix3d.Displacement(new Vector3d(insertPt.X, insertPt.Y, insertPt.Z)));
            table.DbObject.Update();
        }

        /// <summary>
        /// Устанавливает текст, выравнивание, высоту и стиль ячейки данных.
        /// Параметр indent задаёт отступ текста слева (используется для колонки «Наименование»).
        /// </summary>
        public static void SetStandardCell(
            McTable table,
            int row,
            int col,
            string text,
            HorizTextAlign hAlign,
            double indent = 0.0)
        {
            table.SetCellText(row, col, text ?? "");
            table.SetCellHorizAlign(hAlign, row, col);
            table.SetCellVertAlign(VertTextAlign.Center, row, col);
            table.SetCellTextHeight(SpecificationTableLayout.TEXT_H_DATA, row, col);
            table.SetCellTextScale(SpecificationTableLayout.TEXT_SCALE, row, col);
            table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, row, col);

            if (indent > 0.0)
                table.SetCellTextIndent(indent, row, col);
        }

        /// <summary>
        /// Применяет границы таблицы по ТЗ: тонкие внутренние горизонтали в строках данных,
        /// толстые вертикали во всех строках, толстая рамка снаружи и вокруг шапки.
        /// </summary>
        public static void ApplyStandardBorders(McTable table, int dataRows)
        {
            int headerRow = SpecificationTableLayout.HEADER_ROW;
            int firstDataRow = SpecificationTableLayout.FIRST_DATA_ROW;
            int lastDataRow = firstDataRow + dataRows - 1;

            // Тонкие границы всех ячеек данных — базовый слой.
            for (int r = firstDataRow; r <= lastDataRow; r++)
                for (int c = 0; c < SpecificationTableLayout.TABLE_COLS; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.All, r, c, SpecificationTableLayout.LW_THIN);

            // Толстые вертикали в шапке и строках данных.
            for (int c = 0; c < SpecificationTableLayout.TABLE_COLS; c++)
                table.SetCellBorderAbs(BorderTypesEnum.Vertical, headerRow, c, SpecificationTableLayout.LW_THICK);

            for (int r = firstDataRow; r <= lastDataRow; r++)
                for (int c = 0; c < SpecificationTableLayout.TABLE_COLS; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.Vertical, r, c, SpecificationTableLayout.LW_THICK);

            // Толстая рамка вокруг шапки.
            for (int c = 0; c < SpecificationTableLayout.TABLE_COLS; c++)
                table.SetCellBorderAbs(BorderTypesEnum.All, headerRow, c, SpecificationTableLayout.LW_THICK);

            // Толстые внешние горизонтали: верх шапки и низ последней строки данных.
            for (int c = 0; c < SpecificationTableLayout.TABLE_COLS; c++)
            {
                table.SetCellBorderAbs(BorderTypesEnum.Top, headerRow, c, SpecificationTableLayout.LW_THICK);
                table.SetCellBorderAbs(BorderTypesEnum.Bottom, lastDataRow, c, SpecificationTableLayout.LW_THICK);
            }

            // Толстые вертикальные внешние края шапки и строк данных.
            table.SetCellBorderAbs(BorderTypesEnum.Left, headerRow, 0, SpecificationTableLayout.LW_THICK);
            table.SetCellBorderAbs(BorderTypesEnum.Right, headerRow, SpecificationTableLayout.TABLE_COLS - 1, SpecificationTableLayout.LW_THICK);

            for (int r = firstDataRow; r <= lastDataRow; r++)
            {
                table.SetCellBorderAbs(BorderTypesEnum.Left, r, 0, SpecificationTableLayout.LW_THICK);
                table.SetCellBorderAbs(BorderTypesEnum.Right, r, SpecificationTableLayout.TABLE_COLS - 1, SpecificationTableLayout.LW_THICK);
            }

            // Строка заголовка (row 0): убрать все границы кроме нижней через глобальный индексатор.
            for (int c = 0; c < SpecificationTableLayout.TABLE_COLS; c++)
            {
                table[0, c].SetBorderLineweight(BorderTypesEnum.ButBottom, -9); // -9 = невидимая линия
                table[0, c].SetBorderLineweight(BorderTypesEnum.Bottom, SpecificationTableLayout.LW_THICK);
            }
        }

        private static void FillTitleRow(McTable table, string title)
        {
            table.Merge(new McTable.RectRange(0, 0, SpecificationTableLayout.TABLE_COLS - 1, 0));

            table.SetCellText(row: 0, col: 0, title);
            table.SetCellHorizAlign(HorizTextAlign.Center, 0, 0);
            table.SetCellVertAlign(VertTextAlign.Center, 0, 0);
            table.SetCellTextHeight(SpecificationTableLayout.TEXT_H_TITLE, 0, 0);
            table.SetCellTextScale(SpecificationTableLayout.TEXT_SCALE, 0, 0);
            table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, 0, 0);
        }

        private static void FillHeaderRow(McTable table)
        {
            string[] headers =
            {
                "Поз.",
                "Обозначение",
                "Наименование",
                "Кол.",
                "Масса\nед., кг",
                "Приме-\nчание"
            };

            for (int c = 0; c < SpecificationTableLayout.TABLE_COLS; c++)
            {
                table.SetCellText(SpecificationTableLayout.HEADER_ROW, c, headers[c]);
                table.SetCellHorizAlign(HorizTextAlign.Center, SpecificationTableLayout.HEADER_ROW, c);
                table.SetCellVertAlign(VertTextAlign.Center, SpecificationTableLayout.HEADER_ROW, c);
                table.SetCellTextHeight(SpecificationTableLayout.TEXT_H_DATA, SpecificationTableLayout.HEADER_ROW, c);
                table.SetCellTextScale(SpecificationTableLayout.TEXT_SCALE, SpecificationTableLayout.HEADER_ROW, c);
                table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, SpecificationTableLayout.HEADER_ROW, c);
            }
        }

        private static void EnsureLayerExists(string layerName)
        {
            try
            {
                var db = Application.DocumentManager.MdiActiveDocument.Database;
                using var tr = db.TransactionManager.StartTransaction();

                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                if (!lt.Has(layerName))
                {
                    lt.UpgradeOpen();
                    var ltr = new LayerTableRecord { Name = layerName };
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }

                tr.Commit();
            }
            catch (Exception ex)
            {
                Msg($"\n[ПРЕДУПРЕЖДЕНИЕ] Не удалось создать слой '{layerName}': {ex.Message}");
            }
        }

        private static void EnsureTextStyleExists(string styleName, string fontFileName)
        {
            try
            {
                var db = Application.DocumentManager.MdiActiveDocument.Database;
                using var tr = db.TransactionManager.StartTransaction();

                var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);

                if (!tst.Has(styleName))
                {
                    tst.UpgradeOpen();
                    var ts = new TextStyleTableRecord
                    {
                        Name = styleName,
                        FileName = fontFileName,
                        TextSize = 0.0
                    };
                    tst.Add(ts);
                    tr.AddNewlyCreatedDBObject(ts, true);
                }
                else
                {
                    // Обновляем шрифт на случай, если стиль был создан с другим файлом.
                    var ts = (TextStyleTableRecord)tr.GetObject(tst[styleName], OpenMode.ForWrite);
                    ts.FileName = fontFileName;
                }

                tr.Commit();
            }
            catch (Exception ex)
            {
                Msg($"\n[ПРЕДУПРЕЖДЕНИЕ] Не удалось создать/обновить текстовый стиль '{styleName}': {ex.Message}");
            }
        }
    }
}