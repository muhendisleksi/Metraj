using System;
using ClosedXML.Excel;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public partial class ExcelExportService : IExcelExportService
    {
        #region Stil Sabitleri

        private static readonly XLColor HeaderBgColor = XLColor.FromArgb(55, 65, 81);
        private static readonly XLColor SectionBgColor = XLColor.FromArgb(120, 85, 55);
        private static readonly XLColor SubHeaderBgColor = XLColor.FromArgb(107, 114, 128);
        private static readonly XLColor SubtotalBgColor = XLColor.FromArgb(229, 231, 235);
        private static readonly XLColor GrandTotalBgColor = XLColor.FromArgb(243, 234, 220);
        private static readonly XLColor AltRowColor = XLColor.FromArgb(249, 250, 251);
        private static readonly XLColor KaziBgColor = XLColor.FromArgb(254, 243, 199);
        private static readonly XLColor DolguBgColor = XLColor.FromArgb(209, 250, 229);
        private static readonly XLColor SuccessBgColor = XLColor.FromArgb(220, 252, 231);
        private static readonly XLColor WarningBgColor = XLColor.FromArgb(254, 249, 195);
        private static readonly XLColor ErrorBgColor = XLColor.FromArgb(254, 226, 226);
        private static readonly XLColor SuccessColor = XLColor.FromArgb(22, 163, 74);
        private static readonly XLColor WarningColor = XLColor.FromArgb(202, 138, 4);
        private static readonly XLColor ErrorColor = XLColor.FromArgb(220, 38, 38);

        #endregion

        #region Yardımcı Metodlar

        private static void ApplyHeaderStyle(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = HeaderBgColor;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 10;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.WrapText = true;
        }

        private static void ApplySubHeaderStyle(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = SubHeaderBgColor;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Font.Bold = true;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static void ApplySectionTitle(IXLWorksheet ws, int row, int colStart, int colEnd, string text)
        {
            ws.Cell(row, colStart).Value = text;
            var range = ws.Range(row, colStart, row, colEnd);
            range.Merge();
            range.Style.Fill.BackgroundColor = SectionBgColor;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 12;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        private static void ApplySheetTitle(IXLWorksheet ws, int row, int colStart, int colEnd, string text)
        {
            ws.Cell(row, colStart).Value = text;
            var range = ws.Range(row, colStart, row, colEnd);
            range.Merge();
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 14;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            range.Style.Border.BottomBorderColor = HeaderBgColor;
        }

        private static void ApplySubtotalRow(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = SubtotalBgColor;
            range.Style.Font.Bold = true;
            range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            range.Style.Border.TopBorderColor = XLColor.FromArgb(156, 163, 175);
        }

        private static void ApplyGrandTotalRow(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = GrandTotalBgColor;
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 11;
            range.Style.Border.TopBorder = XLBorderStyleValues.Thick;
            range.Style.Border.TopBorderColor = HeaderBgColor;
        }

        private static void ApplyAltRowShading(IXLWorksheet ws, int startRow, int endRow, int startCol, int endCol)
        {
            for (int r = startRow; r <= endRow; r++)
            {
                if ((r - startRow) % 2 == 1)
                    ws.Range(r, startCol, r, endCol).Style.Fill.BackgroundColor = AltRowColor;
            }
        }

        private static void ApplyDataBorders(IXLRange range)
        {
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorderColor = XLColor.FromArgb(209, 213, 219);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = XLColor.FromArgb(156, 163, 175);
        }

        private static void WriteParamRow(IXLWorksheet ws, ref int row, string label, string value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromArgb(85, 85, 85);
            ws.Cell(row, 2).Value = value;
            row++;
        }

        private static void WriteParamRow(IXLWorksheet ws, ref int row, string label, double value, string unit, string format = "#,##0.00")
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromArgb(85, 85, 85);
            ws.Cell(row, 2).Value = value;
            ws.Cell(row, 2).Style.NumberFormat.Format = format;
            ws.Cell(row, 2).Style.Font.Bold = true;
            if (!string.IsNullOrEmpty(unit))
            {
                ws.Cell(row, 3).Value = unit;
                ws.Cell(row, 3).Style.Font.FontColor = XLColor.FromArgb(155, 168, 182);
            }
            row++;
        }

        private static void FinalizeSheet(IXLWorksheet ws)
        {
            ws.Columns().AdjustToContents();
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
        }

        #endregion
    }
}
