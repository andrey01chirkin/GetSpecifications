using System.Globalization;
using Multicad.Geometry;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;
using GetSpecifications.WorkVolume.Common;

namespace GetSpecifications.WorkVolume.Table6
{
    /// <summary>
    /// Строит таблицу «Ведомость объёмов работ элементов стоек эстакады» (таблица №6)
    /// по данным, рассчитанным WorkVolumeRacksCalculator из выбранной таблицы №2.
    /// Структура — 5 колонок: № п/п / Наименование работ / Ед. изм. / Кол-во / Примечание.
    /// </summary>
    public sealed class WorkVolumeRacksBuilder
    {
        private const string TABLE_TITLE = "Ведомость объемов работ элементов стоек эстакады";

        private static readonly Table6CatalogRoot _catalog = RacksTable6Catalog.Load();

        private sealed class RowData
        {
            public string Position { get; set; } = "";
            public string Name { get; set; } = "";
            public string Unit { get; set; } = "";
            public string Qty { get; set; } = "";
            public string Note { get; set; } = "";
            public bool IsSectionHeader { get; set; }
        }

        public bool TryBuild(McTable table2, Point3d insertPt, out string message)
        {
            if (!WorkVolumeRacksCalculator.IsRacksSpecificationTable(table2))
            {
                message = "Выбранная таблица не является «Спецификацией элементов стоек эстакады».";
                return false;
            }

            if (!WorkVolumeRacksCalculator.TryCalculate(table2, out WorkVolumeRacksResult? result, out string calcMessage)
                || result == null)
            {
                message = calcMessage;
                return false;
            }

            List<RowData> rows = BuildRows(result);

            SpecificationTableHelper.EnsureDrawingResources();

            McTable table = CreateTable(rows.Count);

            int rowIndex = WorkVolumeRacksTableLayout.FIRST_DATA_ROW;
            foreach (RowData row in rows)
            {
                FillDataRow(table, rowIndex, row);
                rowIndex++;
            }

            ApplyBorders(table, rows.Count);
            SpecificationTableHelper.AddTableToDocument(table, insertPt);

            message = "Таблица «Ведомость объёмов работ элементов стоек эстакады» сформирована.\n" + calcMessage;
            return true;
        }

        // === Построение строк ===

        private List<RowData> BuildRows(WorkVolumeRacksResult result)
        {
            var rows = new List<RowData>
            {
                new RowData
                {
                    Position = "",
                    Name = "Элементы стоек (объемы общие)",
                    Unit = "",
                    Qty = "",
                    Note = "",
                    IsSectionHeader = true
                }
            };

            foreach (Table6RowTemplate catRow in _catalog.Rows)
            {
                if (catRow.Position == "95")
                {
                    rows.Add(new RowData
                    {
                        Position = "95",
                        Name = catRow.Name,
                        Unit = catRow.Unit,
                        Qty = FormatDecimal2(result.TotalMassKg),
                        Note = ""
                    });

                    foreach (RackMaterialAggregate mat in result.Materials)
                    {
                        rows.Add(new RowData
                        {
                            Position = "",
                            Name = mat.IsPipe
                                ? $"-{mat.WorkVolumeName}, С{result.SteelGrade} (m = {FormatDecimal2(mat.UnitMassPerM)} кг/м)"
                                : $"-{mat.WorkVolumeName}, С{result.SteelGrade}",
                            Unit = "кг",
                            Qty = FormatDecimal2(mat.MassKg),
                            Note = mat.IsPipe ? $"Lобщ={FormatDecimal2(mat.TotalLenM)}" : ""
                        });
                    }

                    continue;
                }

                double value = catRow.Formula switch
                {
                    "PaintArea" => result.TotalPaintAreaM2,
                    "ReducedArea" => result.ReducedAreaM2,
                    "WeldVisual" => result.WeldVisualM,
                    "WeldUzk" => result.WeldUzkM,
                    _ => 0.0
                };

                rows.Add(new RowData
                {
                    Position = catRow.Position,
                    Name = catRow.Name,
                    Unit = catRow.Unit,
                    Qty = FormatDecimal2(value),
                    Note = ""
                });
            }

            return rows;
        }

        // === Построение структуры таблицы ===

        private McTable CreateTable(int dataRows)
        {
            var table = new McTable();

            table.DefaultCell.HorizontalTextAlign = HorizTextAlign.Center;
            table.DefaultCell.VerticalTextAlign = VertTextAlign.Center;

            table.Columns.AddRange(0, WorkVolumeRacksTableLayout.TABLE_COLS);
            table.Columns[0].Width = WorkVolumeRacksTableLayout.COL_W_NUM;
            table.Columns[1].Width = WorkVolumeRacksTableLayout.COL_W_NAME;
            table.Columns[2].Width = WorkVolumeRacksTableLayout.COL_W_UNIT;
            table.Columns[3].Width = WorkVolumeRacksTableLayout.COL_W_QTY;
            table.Columns[4].Width = WorkVolumeRacksTableLayout.COL_W_PRIM;

            table.Rows.InsertSection(SectionTypeEnum.HeaderFirst, 0, 2);
            table.Rows.InsertSection(SectionTypeEnum.None, 2, dataRows);

            int lastDataRow = WorkVolumeRacksTableLayout.FIRST_DATA_ROW + dataRows - 1;

            table.Rows[WorkVolumeRacksTableLayout.TITLE_ROW].Height = WorkVolumeRacksTableLayout.ROW_H_TITLE;
            table.Rows[WorkVolumeRacksTableLayout.HEADER_ROW].Height = WorkVolumeRacksTableLayout.ROW_H_HDR;

            for (int r = WorkVolumeRacksTableLayout.FIRST_DATA_ROW; r <= lastDataRow; r++)
                table.Rows[r].Height = WorkVolumeRacksTableLayout.ROW_H_DATA;

            FillTitleRow(table);
            FillHeaderRow(table);

            return table;
        }

        private void FillTitleRow(McTable table)
        {
            table.Merge(new McTable.RectRange(0, 0, WorkVolumeRacksTableLayout.TABLE_COLS - 1, 0));

            table.SetCellText(0, 0, TABLE_TITLE);
            table.SetCellHorizAlign(HorizTextAlign.Center, 0, 0);
            table.SetCellVertAlign(VertTextAlign.Center, 0, 0);
            table.SetCellTextHeight(WorkVolumeRacksTableLayout.TEXT_H_TITLE, 0, 0);
            table.SetCellTextScale(WorkVolumeRacksTableLayout.TEXT_SCALE, 0, 0);
            table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, 0, 0);
        }

        private void FillHeaderRow(McTable table)
        {
            string[] headers = { "N\nп/п", "Наименование работ", "Ед.\nизм.", "Кол-\nво", "Приме-\nчание" };

            for (int c = 0; c < WorkVolumeRacksTableLayout.TABLE_COLS; c++)
            {
                table.SetCellText(WorkVolumeRacksTableLayout.HEADER_ROW, c, headers[c]);
                table.SetCellHorizAlign(HorizTextAlign.Center, WorkVolumeRacksTableLayout.HEADER_ROW, c);
                table.SetCellVertAlign(VertTextAlign.Center, WorkVolumeRacksTableLayout.HEADER_ROW, c);
                table.SetCellTextHeight(WorkVolumeRacksTableLayout.TEXT_H_DATA, WorkVolumeRacksTableLayout.HEADER_ROW, c);
                table.SetCellTextScale(WorkVolumeRacksTableLayout.TEXT_SCALE, WorkVolumeRacksTableLayout.HEADER_ROW, c);
                table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, WorkVolumeRacksTableLayout.HEADER_ROW, c);
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
            SetCell(table, row, 4, data.Note, HorizTextAlign.Center);
        }

        private void SetCell(McTable table, int row, int col, string text, HorizTextAlign hAlign, double indent = 0.0)
        {
            table.SetCellText(row, col, text ?? "");
            table.SetCellHorizAlign(hAlign, row, col);
            table.SetCellVertAlign(VertTextAlign.Center, row, col);
            table.SetCellTextHeight(WorkVolumeRacksTableLayout.TEXT_H_DATA, row, col);
            table.SetCellTextScale(WorkVolumeRacksTableLayout.TEXT_SCALE, row, col);
            table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, row, col);

            if (indent > 0.0)
                table.SetCellTextIndent(indent, row, col);
        }

        private void ApplyBorders(McTable table, int dataRows)
        {
            int headerRow = WorkVolumeRacksTableLayout.HEADER_ROW;
            int firstDataRow = WorkVolumeRacksTableLayout.FIRST_DATA_ROW;
            int lastDataRow = firstDataRow + dataRows - 1;

            for (int r = firstDataRow; r <= lastDataRow; r++)
                for (int c = 0; c < WorkVolumeRacksTableLayout.TABLE_COLS; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.All, r, c, WorkVolumeRacksTableLayout.LW_THIN);

            for (int c = 0; c < WorkVolumeRacksTableLayout.TABLE_COLS; c++)
                table.SetCellBorderAbs(BorderTypesEnum.All, headerRow, c, WorkVolumeRacksTableLayout.LW_THICK);

            for (int c = 0; c < WorkVolumeRacksTableLayout.TABLE_COLS; c++)
            {
                table.SetCellBorderAbs(BorderTypesEnum.Top, headerRow, c, WorkVolumeRacksTableLayout.LW_THICK);
                table.SetCellBorderAbs(BorderTypesEnum.Bottom, lastDataRow, c, WorkVolumeRacksTableLayout.LW_THICK);
            }

            for (int r = firstDataRow; r <= lastDataRow; r++)
                for (int c = 0; c < WorkVolumeRacksTableLayout.TABLE_COLS; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.Vertical, r, c, WorkVolumeRacksTableLayout.LW_THICK);

            table.SetCellBorderAbs(BorderTypesEnum.Left, headerRow, 0, WorkVolumeRacksTableLayout.LW_THICK);
            table.SetCellBorderAbs(BorderTypesEnum.Right, headerRow, WorkVolumeRacksTableLayout.TABLE_COLS - 1, WorkVolumeRacksTableLayout.LW_THICK);

            for (int r = firstDataRow; r <= lastDataRow; r++)
            {
                table.SetCellBorderAbs(BorderTypesEnum.Left, r, 0, WorkVolumeRacksTableLayout.LW_THICK);
                table.SetCellBorderAbs(BorderTypesEnum.Right, r, WorkVolumeRacksTableLayout.TABLE_COLS - 1, WorkVolumeRacksTableLayout.LW_THICK);
            }

            for (int c = 0; c < WorkVolumeRacksTableLayout.TABLE_COLS; c++)
            {
                table[0, c].SetBorderLineweight(BorderTypesEnum.ButBottom, -9);
                table[0, c].SetBorderLineweight(BorderTypesEnum.Bottom, WorkVolumeRacksTableLayout.LW_THICK);
            }
        }

        private static string FormatDecimal2(double value)
            => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}