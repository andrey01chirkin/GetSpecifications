using System.Globalization;
using Multicad.Geometry;
using Multicad.Objects;
using Multicad.Symbols.Tables;
using GetSpecifications.Specifications.Common;

namespace GetSpecifications.Specifications.Table3
{
    /// <summary>
    /// Строит таблицу «Таблица сечений балок кабельной эстакады» (таблица №3).
    /// Строки группируются по атрибуту sName, сортируются по (номер балки, буква).
    /// Длина каждого объекта = rL / 10 м, суммируется по группе.
    /// Количество швеллеров: 2 шт. для балок 1–3 и 10; 4 шт. для 4–9.
    /// </summary>
    public sealed class SpecificationsBeamsTable3Builder
    {
        private sealed class BeamSectionRecord
        {
            public string SName { get; set; } = "";
            public int BeamNumber { get; set; }
            public int LetterIndex { get; set; }
            public int Count { get; set; }
            public double TotalLenM { get; set; }
            public string NormNagruzka { get; set; } = "";
            public string RaschNagruzka { get; set; } = "";
            public string PokrytieEstakady { get; set; } = "";
        }

        private readonly BeamObjectPropertyReader _reader = new BeamObjectPropertyReader();

        public bool TryBuild(
            List<McParametricObject> selectedObjects,
            Point3d insertPt,
            out string message)
        {
            message = "";

            if (selectedObjects == null || selectedObjects.Count == 0)
            {
                message = "Не переданы выбранные параметрические объекты.";
                return false;
            }

            Dictionary<(string, string), BeamSectionRecord> dict = AggregateBeams(selectedObjects);

            if (dict.Count == 0)
            {
                message = "В выборке нет балок с распознанным атрибутом «sName».";
                return false;
            }

            List<BeamSectionRecord> sorted = dict.Values
                .OrderBy(r => r.BeamNumber)
                .ThenBy(r => r.LetterIndex)
                .ThenBy(r => r.PokrytieEstakady, StringComparer.CurrentCulture)
                .ToList();

            SpecificationTableHelper.EnsureDrawingResources();

            McTable table = BuildTable(sorted);

            SpecificationTableHelper.AddTableToDocument(table, insertPt);

            message = "Таблица «Таблица сечений балок кабельной эстакады» сформирована.";
            return true;
        }

        // === Агрегация ===

        private Dictionary<(string sName, string pokrytie), BeamSectionRecord> AggregateBeams(
    List<McParametricObject> objects)
        {
            var dict = new Dictionary<(string, string), BeamSectionRecord>();

            foreach (McParametricObject obj in objects)
            {
                BeamObjectData data = _reader.Read(obj);

                if (string.IsNullOrWhiteSpace(data.SName)) continue;
                if (!BeamSectionHelper.TryParseSectionName(data.SName, out int beamNumber, out int letterIndex))
                    continue;

                var key = (data.SName, data.PokrytieEstakady);

                if (dict.TryGetValue(key, out BeamSectionRecord? rec))
                {
                    rec.Count++;
                    rec.TotalLenM += data.RL / 10.0;
                }
                else
                {
                    dict[key] = new BeamSectionRecord
                    {
                        SName = data.SName,
                        BeamNumber = beamNumber,
                        LetterIndex = letterIndex,
                        Count = 1,
                        TotalLenM = data.RL / 10.0,
                        NormNagruzka = FormatLoad(data.NormNagruzka),
                        RaschNagruzka = FormatLoad(data.RaschNagruzka),
                        PokrytieEstakady = data.PokrytieEstakady
                    };
                }
            }

            return dict;
        }

        // === Построение McTable ===

        private McTable BuildTable(List<BeamSectionRecord> records)
        {
            int dataRows = records.Count;
            var table = new McTable();

            table.DefaultCell.HorizontalTextAlign = HorizTextAlign.Center;
            table.DefaultCell.VerticalTextAlign = VertTextAlign.Center;

            SetColumnWidths(table);

            // HeaderFirst(0, 3): строки 0 (заголовок), 1 (шапка1), 2 (шапка2).
            // None(3, dataRows): строка 3 — служебная, данные начинаются с индекса 4.
            table.Rows.InsertSection(SectionTypeEnum.HeaderFirst, 0, 3);
            table.Rows.InsertSection(SectionTypeEnum.None, 3, dataRows);

            SetRowHeights(table, dataRows);

            FillTitleRow(table);
            FillHeaderRows(table);
            FillDataRows(table, records);
            ApplyTable3Borders(table, dataRows);

            return table;
        }

        private static void SetColumnWidths(McTable table)
        {
            table.Columns.AddRange(0, SpecificationsBeamsTable3Layout.TABLE_COLS);
            table.Columns[0].Width = SpecificationsBeamsTable3Layout.COL_W_NOMER;
            table.Columns[1].Width = SpecificationsBeamsTable3Layout.COL_W_KOL_BALOK;
            table.Columns[2].Width = SpecificationsBeamsTable3Layout.COL_W_DLINA;
            table.Columns[3].Width = SpecificationsBeamsTable3Layout.COL_W_NORM;
            table.Columns[4].Width = SpecificationsBeamsTable3Layout.COL_W_RASCH;
            table.Columns[5].Width = SpecificationsBeamsTable3Layout.COL_W_SHVELLER;
            table.Columns[6].Width = SpecificationsBeamsTable3Layout.COL_W_KOL_SHV;
            table.Columns[7].Width = SpecificationsBeamsTable3Layout.COL_W_PLASTINA;
            table.Columns[8].Width = SpecificationsBeamsTable3Layout.COL_W_POKRYTIE;
        }

        private static void SetRowHeights(McTable table, int dataRows)
        {
            table.Rows[SpecificationsBeamsTable3Layout.TITLE_ROW].Height = SpecificationsBeamsTable3Layout.ROW_H_TITLE;
            table.Rows[SpecificationsBeamsTable3Layout.HEADER_ROW_1].Height = SpecificationsBeamsTable3Layout.ROW_H_HDR1;
            table.Rows[SpecificationsBeamsTable3Layout.HEADER_ROW_2].Height = SpecificationsBeamsTable3Layout.ROW_H_HDR2;

            int lastDataRow = SpecificationsBeamsTable3Layout.FIRST_DATA_ROW + dataRows - 1;
            for (int r = SpecificationsBeamsTable3Layout.FIRST_DATA_ROW; r <= lastDataRow; r++)
                table.Rows[r].Height = SpecificationsBeamsTable3Layout.ROW_H_DATA;
        }

        // === Заполнение строк ===

        private static void FillTitleRow(McTable table)
        {
            int cols = SpecificationsBeamsTable3Layout.TABLE_COLS;
            table.Merge(new McTable.RectRange(0, 0, cols - 1, 0));
            table.SetCellText(0, 0, "Таблица сечений балок кабельной эстакады");
            table.SetCellHorizAlign(HorizTextAlign.Center, 0, 0);
            table.SetCellVertAlign(VertTextAlign.Center, 0, 0);
            table.SetCellTextHeight(SpecificationTableLayout.TEXT_H_TITLE, 0, 0);
            table.SetCellTextScale(SpecificationTableLayout.TEXT_SCALE, 0, 0);
            table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, 0, 0);
        }

        private static void FillHeaderRows(McTable table)
        {
            // Вертикальное объединение (строки 1–2) для колонок без подраздела.
            table.Merge(new McTable.RectRange(0, 1, 0, 2)); // Номер разреза
            table.Merge(new McTable.RectRange(1, 1, 1, 2)); // Кол-во балок
            table.Merge(new McTable.RectRange(2, 1, 2, 2)); // Длина, м
            table.Merge(new McTable.RectRange(5, 1, 5, 2)); // Швеллер поз.
            table.Merge(new McTable.RectRange(6, 1, 6, 2)); // Кол-во швеллеров
            table.Merge(new McTable.RectRange(7, 1, 7, 2)); // Пластина поз.
            table.Merge(new McTable.RectRange(8, 1, 8, 2)); // Покрытие эстакады

            // Горизонтальное объединение «q, т/м» над колонками 3–4 в строке 1.
            table.Merge(new McTable.RectRange(3, 1, 4, 1));

            // Строка 1: основные заголовки.
            SetHeaderCell(table, 1, 0, "Номер\nразреза");
            SetHeaderCell(table, 1, 1, "Кол-во\nбалок");
            SetHeaderCell(table, 1, 2, "Длина, м");
            SetHeaderCell(table, 1, 3, "q, т/м");
            SetHeaderCell(table, 1, 5, "Швеллер,\nпоз.\nв спец.\nэлементов");
            SetHeaderCell(table, 1, 6, "Кол-во\nшвеллеров");
            SetHeaderCell(table, 1, 7, "Пластина\nдля заделки\nторцов балок,\nпоз. в спец.\nэлементов");
            SetHeaderCell(table, 1, 8, "Покрытие\nэстакады");

            // Строка 2: подзаголовки нагрузок.
            SetHeaderCell(table, 2, 3, "Норм.\nнагрузка");
            SetHeaderCell(table, 2, 4, "Расч.\nнагрузка");
        }

        private static void SetHeaderCell(McTable table, int row, int col, string text)
        {
            table.SetCellText(row, col, text);
            table.SetCellHorizAlign(HorizTextAlign.Center, row, col);
            table.SetCellVertAlign(VertTextAlign.Center, row, col);
            table.SetCellTextHeight(SpecificationTableLayout.TEXT_H_DATA, row, col);
            table.SetCellTextScale(SpecificationTableLayout.TEXT_SCALE, row, col);
            table.SetCellTextStyle(SpecificationObjectConstants.TEXT_STYLE, row, col);
        }

        private void FillDataRows(McTable table, List<BeamSectionRecord> records)
        {
            int row = SpecificationsBeamsTable3Layout.FIRST_DATA_ROW;

            foreach (BeamSectionRecord rec in records)
            {
                int pos = rec.LetterIndex + 1;

                SpecificationTableHelper.SetStandardCell(table, row, 0, rec.SName, HorizTextAlign.Center);
                SpecificationTableHelper.SetStandardCell(table, row, 1, rec.Count.ToString(CultureInfo.InvariantCulture), HorizTextAlign.Center);
                SpecificationTableHelper.SetStandardCell(table, row, 2, FormatDecimal2(rec.TotalLenM), HorizTextAlign.Center);
                SpecificationTableHelper.SetStandardCell(table, row, 3, rec.NormNagruzka, HorizTextAlign.Center);
                SpecificationTableHelper.SetStandardCell(table, row, 4, rec.RaschNagruzka, HorizTextAlign.Center);
                SpecificationTableHelper.SetStandardCell(table, row, 5, $"поз. {pos}", HorizTextAlign.Center);
                SpecificationTableHelper.SetStandardCell(table, row, 6, $"{BeamSectionHelper.GetChannelCount(rec.BeamNumber)} шт.", HorizTextAlign.Center);
                SpecificationTableHelper.SetStandardCell(table, row, 7, $"п-{pos}", HorizTextAlign.Center);
                SpecificationTableHelper.SetStandardCell(table, row, 8, rec.PokrytieEstakady, HorizTextAlign.Center);

                row++;
            }
        }

        // === Границы ===

        private static void ApplyTable3Borders(McTable table, int dataRows)
        {
            int cols = SpecificationsBeamsTable3Layout.TABLE_COLS;
            int firstDataRow = SpecificationsBeamsTable3Layout.FIRST_DATA_ROW;
            int lastDataRow = firstDataRow + dataRows - 1;

            // Строка заголовка (0): только нижняя граница.
            // SetBorderLineweight через глобальный индексатор — единственный способ управлять
            // границами строки HeaderFirst без конфликта с секционной адресацией.
            for (int c = 0; c < cols; c++)
            {
                table[0, c].SetBorderLineweight(BorderTypesEnum.ButBottom, -9);
                table[0, c].SetBorderLineweight(BorderTypesEnum.Bottom, SpecificationTableLayout.LW_THICK);
            }

            // Строки шапки (1, 2): все границы толстые.
            for (int r = 1; r <= 2; r++)
                for (int c = 0; c < cols; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.All, r, c, SpecificationTableLayout.LW_THICK);

            for (int r = firstDataRow; r <= lastDataRow; r++)
                for (int c = 0; c < cols; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.All, r, c, SpecificationTableLayout.LW_THIN);

            // Перекрыть толстыми вертикалями.
            for (int r = firstDataRow; r <= lastDataRow; r++)
                for (int c = 0; c < cols; c++)
                    table.SetCellBorderAbs(BorderTypesEnum.Vertical, r, c, SpecificationTableLayout.LW_THICK);

            for (int c = 0; c < cols; c++)
                table.SetCellBorderAbs(BorderTypesEnum.Bottom, lastDataRow, c, SpecificationTableLayout.LW_THICK);

            for (int r = firstDataRow; r <= lastDataRow; r++)
            {
                table.SetCellBorderAbs(BorderTypesEnum.Left, r, 0, SpecificationTableLayout.LW_THICK);
                table.SetCellBorderAbs(BorderTypesEnum.Right, r, cols - 1, SpecificationTableLayout.LW_THICK);
            }
        }

        // === Вспомогательные методы ===

        private static string FormatLoad(double value)
            => value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

        private static string FormatDecimal2(double value)
            => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}