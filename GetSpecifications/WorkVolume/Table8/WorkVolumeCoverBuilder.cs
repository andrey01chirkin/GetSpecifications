using Multicad.Geometry;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;
using GetSpecifications.WorkVolume.Common;

namespace GetSpecifications.WorkVolume.Table8
{
    /// <summary>
    /// Строит «Ведомость объемов работ элементов покрытия эстакады» по выбранной таблице №7.
    /// Содержимое разделов статично (Table8Catalog.json); из спецификации берутся только
    /// значения «Кол.» заголовков блоков. Раздел с пустым значением не выводится.
    /// Нумерация строк сквозная по выведенным разделам. Оформление — как у таблицы №5.
    /// </summary>
    public sealed class WorkVolumeCoverBuilder
    {
        private const string TABLE_TITLE = "Ведомость объемов работ элементов покрытия эстакады";

        private static readonly Table8CatalogRoot _catalog = CoverTable8Catalog.Load();

        private sealed class RowData
        {
            public string Position { get; set; } = "";
            public string Name { get; set; } = "";
            public string Unit { get; set; } = "";
            public string Qty { get; set; } = "";
            public bool IsSectionHeader { get; set; }
        }

        public bool TryBuild(McTable table7, Point3d insertPt, out string message)
        {
            Dictionary<string, string> quantities =
                WorkVolumeCoverCalculator.ReadSectionQuantities(table7, _catalog.Sections);

            if (quantities.Count == 0)
            {
                message = "В спецификации не заполнено значение «Кол.» ни в одном заголовке раздела — ведомость не сформирована.";
                return false;
            }

            List<RowData> rows = BuildRows(quantities);

            SpecificationTableHelper.EnsureDrawingResources();

            McTable table = CreateTable(rows.Count);

            int rowIndex = WorkVolumeBeamsTableLayout.FIRST_DATA_ROW;
            foreach (RowData row in rows)
            {
                FillDataRow(table, rowIndex, row);
                rowIndex++;
            }

            ApplyBorders(table, rows.Count);
            SpecificationTableHelper.AddTableToDocument(table, insertPt);

            message = "Таблица «Ведомость объемов работ элементов покрытия эстакады» сформирована.";
            return true;
        }

        // === Построение строк ===

        private List<RowData> BuildRows(Dictionary<string, string> quantities)
        {
            var rows = new List<RowData>();
            int number = 1;

            foreach (Table8SectionTemplate section in _catalog.Sections)
            {
                if (!quantities.TryGetValue(section.Key, out string? qty))
                    continue;

                // Пустая строка-разделитель только между выведенными разделами.
                if (rows.Count > 0)
                    rows.Add(new RowData());

                rows.Add(new RowData
                {
                    Name = section.Name,
                    Unit = section.Unit,
                    Qty = WrapAsRawTextFormula(qty),
                    IsSectionHeader = true
                });

                foreach (Table8RowTemplate t in section.Rows)
                {
                    rows.Add(new RowData
                    {
                        Position = number.ToString(),
                        Name = t.Name,
                        Unit = t.Unit,
                        Qty = WrapAsRawTextFormula(t.Quantity)
                    });
                    number++;
                }
            }

            return rows;
        }

        // === Построение структуры таблицы ===

        private McTable CreateTable(int dataRows)
        {
            var table = new McTable();

            table.DefaultCell.HorizontalTextAlign = HorizTextAlign.Center;
            table.DefaultCell.VerticalTextAlign = VertTextAlign.Center;

            table.Columns.AddRange(0, WorkVolumeBeamsTableLayout.TABLE_COLS);
            table.Columns[0].Width = WorkVolumeBeamsTableLayout.COL_W_NUM;
            table.Columns[1].Width = WorkVolumeBeamsTableLayout.COL_W_NAME;
            table.Columns[2].Width = WorkVolumeBeamsTableLayout.COL_W_UNIT;
            table.Columns[3].Width = WorkVolumeBeamsTableLayout.COL_W_QTY;

            table.Rows.InsertSection(SectionTypeEnum.HeaderFirst, 0, 2);
            table.Rows.InsertSection(SectionTypeEnum.None, 2, dataRows);

            int lastDataRow = WorkVolumeBeamsTableLayout.FIRST_DATA_ROW + dataRows - 1;

            table.Rows[WorkVolumeBeamsTableLayout.TITLE_ROW].Height = WorkVolumeBeamsTableLayout.ROW_H_TITLE;
            table.Rows[WorkVolumeBeamsTableLayout.HEADER_ROW].Height = WorkVolumeBeamsTableLayout.ROW_H_HDR;

            for (int r = WorkVolumeBeamsTableLayout.FIRST_DATA_ROW; r <= lastDataRow; r++)
                table.Rows[r].Height = WorkVolumeBeamsTableLayout.ROW_H_DATA;

            FillTitleRow(table);
            FillHeaderRow(table);

            return table;
        }

        private void FillTitleRow(McTable table)
        {
            table.Merge(new McTable.RectRange(0, 0, WorkVolumeBeamsTableLayout.TABLE_COLS - 1, 0));

            table.SetCellText(0, 0, TABLE_TITLE);
            table.SetCellHorizAlign(HorizTextAlign.Center, 0, 0);
            table.SetCellVertAlign(VertTextAlign.Center, 0, 0);
            table.SetCellTextHeight(WorkVolumeBeamsTableLayout.TEXT_H_TITLE, 0, 0);
            table.SetCellTextScale(WorkVolumeBeamsTableLayout.TEXT_SCALE, 0, 0);
            table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, 0, 0);
        }

        private void FillHeaderRow(McTable table)
        {
            string[] headers = { "N\nп/п", "Наименование работ", "Ед.\nизм.", "Кол-\nво" };

            for (int c = 0; c < WorkVolumeBeamsTableLayout.TABLE_COLS; c++)
            {
                table.SetCellText(WorkVolumeBeamsTableLayout.HEADER_ROW, c, headers[c]);
                table.SetCellHorizAlign(HorizTextAlign.Center, WorkVolumeBeamsTableLayout.HEADER_ROW, c);
                table.SetCellVertAlign(VertTextAlign.Center, WorkVolumeBeamsTableLayout.HEADER_ROW, c);
                table.SetCellTextHeight(WorkVolumeBeamsTableLayout.TEXT_H_DATA, WorkVolumeBeamsTableLayout.HEADER_ROW, c);
                table.SetCellTextScale(WorkVolumeBeamsTableLayout.TEXT_SCALE, WorkVolumeBeamsTableLayout.HEADER_ROW, c);
                table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, WorkVolumeBeamsTableLayout.HEADER_ROW, c);
            }
        }

        private void FillDataRow(McTable table, int row, RowData data)
        {
            SetCell(table, row, 0, data.Position, HorizTextAlign.Center);

            if (data.IsSectionHeader)
                SetCell(table, row, 1, data.Name, HorizTextAlign.Center);
            else
                SetCell(table, row, 1, data.Name, HorizTextAlign.Left, indent: 1.0);

            SetCell(table, row, 2, data.Unit, HorizTextAlign.Center);
            SetCell(table, row, 3, data.Qty, HorizTextAlign.Center);
        }

        private void SetCell(McTable table, int row, int col, string text, HorizTextAlign hAlign, double indent = 0.0)
        {
            table.SetCellText(row, col, text ?? "");
            table.SetCellHorizAlign(hAlign, row, col);
            table.SetCellVertAlign(VertTextAlign.Center, row, col);
            table.SetCellTextHeight(WorkVolumeBeamsTableLayout.TEXT_H_DATA, row, col);
            table.SetCellTextScale(WorkVolumeBeamsTableLayout.TEXT_SCALE, row, col);
            table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, row, col);

            if (indent > 0.0)
                table.SetCellTextIndent(indent, row, col);
        }

        private void ApplyBorders(McTable table, int dataRows)
        {
            int headerRow = WorkVolumeBeamsTableLayout.HEADER_ROW;
            int firstDataRow = WorkVolumeBeamsTableLayout.FIRST_DATA_ROW;
            int lastDataRow = firstDataRow + dataRows - 1;
            int cols = WorkVolumeBeamsTableLayout.TABLE_COLS;

            for (int r = firstDataRow; r <= lastDataRow; r++)
                for (int c = 0; c < cols; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.All, r, c, WorkVolumeBeamsTableLayout.LW_THIN);

            for (int c = 0; c < cols; c++)
                table.SetCellBorderAbs(BorderTypesEnum.All, headerRow, c, WorkVolumeBeamsTableLayout.LW_THICK);

            for (int c = 0; c < cols; c++)
            {
                table.SetCellBorderAbs(BorderTypesEnum.Top, headerRow, c, WorkVolumeBeamsTableLayout.LW_THICK);
                table.SetCellBorderAbs(BorderTypesEnum.Bottom, lastDataRow, c, WorkVolumeBeamsTableLayout.LW_THICK);
            }

            for (int r = firstDataRow; r <= lastDataRow; r++)
                for (int c = 0; c < cols; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.Vertical, r, c, WorkVolumeBeamsTableLayout.LW_THICK);

            table.SetCellBorderAbs(BorderTypesEnum.Left, headerRow, 0, WorkVolumeBeamsTableLayout.LW_THICK);
            table.SetCellBorderAbs(BorderTypesEnum.Right, headerRow, cols - 1, WorkVolumeBeamsTableLayout.LW_THICK);

            for (int r = firstDataRow; r <= lastDataRow; r++)
            {
                table.SetCellBorderAbs(BorderTypesEnum.Left, r, 0, WorkVolumeBeamsTableLayout.LW_THICK);
                table.SetCellBorderAbs(BorderTypesEnum.Right, r, cols - 1, WorkVolumeBeamsTableLayout.LW_THICK);
            }

            for (int c = 0; c < cols; c++)
            {
                table[0, c].SetBorderLineweight(BorderTypesEnum.ButBottom, -9);
                table[0, c].SetBorderLineweight(BorderTypesEnum.Bottom, WorkVolumeBeamsTableLayout.LW_THICK);
            }
        }

        // fmtRaw запрещает таблице усекать конечные нули (1,0 → 1; 2,60 → 2,6).
        private static string WrapAsRawTextFormula(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return "=fmtRaw(\"" + value.Replace("\"", "\"\"") + "\")";
        }

    }
}