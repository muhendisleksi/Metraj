using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public partial class ExcelExportService
    {
        public ExportResult YolMetrajExport(YolMetrajRaporu rapor, string dosyaYolu)
        {
            try
            {
                if (rapor == null || rapor.Kesitler == null || rapor.Kesitler.Count == 0)
                    return new ExportResult { Basarili = false, HataMesaji = "Yol metraj verisi bo\u015F." };

                using (var workbook = new XLWorkbook())
                {
                    KesitAlanlariSayfasi(workbook, rapor);

                    if (rapor.KubajSonucu != null && rapor.KubajSonucu.MalzemeOzetleri.Count > 0)
                    {
                        KubajTablosuSayfasi(workbook, rapor);
                        KaziDolguOzetSayfasi(workbook, rapor);

                        if (rapor.KubajSonucu.BrucknerVerisi != null && rapor.KubajSonucu.BrucknerVerisi.Count > 0)
                            BrucknerSayfasi(workbook, rapor);
                    }

                    workbook.SaveAs(dosyaYolu);
                }

                LoggingService.Info("Yol metraj Excel olu\u015Fturuldu: {DosyaYolu}", dosyaYolu);
                return new ExportResult { Basarili = true, DosyaYolu = dosyaYolu };
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Yol metraj Excel hatas\u0131", ex);
                return new ExportResult { Basarili = false, HataMesaji = ex.Message };
            }
        }

        private void KesitAlanlariSayfasi(XLWorkbook workbook, YolMetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Kesit Alanlar\u0131");

            // Dinamik sütunlar: kesitlerde hangi malzemeler varsa
            var malzemeler = rapor.Kesitler
                .SelectMany(k => k.KatmanAlanlari)
                .Select(k => k.MalzemeAdi)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Başlık
            int col = 1;
            ws.Cell(1, col).Value = "No";
            ws.Cell(1, col + 1).Value = "\u0130stasyon";
            col = 3;
            foreach (var m in malzemeler)
            {
                ws.Cell(1, col).Value = m + " (m\u00B2)";
                col++;
            }
            ws.Cell(1, col).Value = "Kaz\u0131 (m\u00B2)";
            ws.Cell(1, col + 1).Value = "Dolgu (m\u00B2)";

            // Başlık stil
            int sonSutun = col + 1;
            var baslikRange = ws.Range(1, 1, 1, sonSutun);
            baslikRange.Style.Font.Bold = true;
            baslikRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            baslikRange.Style.Font.FontColor = XLColor.White;
            baslikRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Veri satırları
            for (int i = 0; i < rapor.Kesitler.Count; i++)
            {
                var kesit = rapor.Kesitler[i];
                int satir = i + 2;
                col = 1;

                ws.Cell(satir, col).Value = i + 1;
                ws.Cell(satir, col + 1).Value = kesit.IstasyonMetni ?? $"{kesit.Istasyon:F2}";
                col = 3;

                foreach (var m in malzemeler)
                {
                    double alan = kesit.MalzemeAlaniGetir(m);
                    ws.Cell(satir, col).Value = alan;
                    ws.Cell(satir, col).Style.NumberFormat.Format = "#,##0.00";
                    col++;
                }

                ws.Cell(satir, col).Value = kesit.ToplamKaziAlani;
                ws.Cell(satir, col).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(satir, col + 1).Value = kesit.ToplamDolguAlani;
                ws.Cell(satir, col + 1).Style.NumberFormat.Format = "#,##0.00";
            }

            ws.Columns().AdjustToContents();
        }

        private void KubajTablosuSayfasi(XLWorkbook workbook, YolMetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("K\u00FCbaj Tablosu");
            int satir = 1;

            foreach (var ozet in rapor.KubajSonucu.MalzemeOzetleri)
            {
                // Malzeme başlık
                ws.Cell(satir, 1).Value = "Malzeme: " + ozet.MalzemeAdi;
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 1).Style.Font.FontSize = 12;
                satir++;

                // Tablo başlık
                ws.Cell(satir, 1).Value = "Ist.1";
                ws.Cell(satir, 2).Value = "Ist.2";
                ws.Cell(satir, 3).Value = "Alan1 (m\u00B2)";
                ws.Cell(satir, 4).Value = "Alan2 (m\u00B2)";
                ws.Cell(satir, 5).Value = "Mesafe (m)";
                ws.Cell(satir, 6).Value = "Hacim (m\u00B3)";

                var baslikRange = ws.Range(satir, 1, satir, 6);
                baslikRange.Style.Font.Bold = true;
                baslikRange.Style.Fill.BackgroundColor = XLColor.DarkGray;
                baslikRange.Style.Font.FontColor = XLColor.White;
                satir++;

                foreach (var seg in ozet.Segmentler)
                {
                    ws.Cell(satir, 1).Value = seg.Istasyon1;
                    ws.Cell(satir, 2).Value = seg.Istasyon2;
                    ws.Cell(satir, 3).Value = seg.Alan1;
                    ws.Cell(satir, 4).Value = seg.Alan2;
                    ws.Cell(satir, 5).Value = seg.Mesafe;
                    ws.Cell(satir, 6).Value = seg.Hacim;

                    for (int c = 1; c <= 6; c++)
                        ws.Cell(satir, c).Style.NumberFormat.Format = "#,##0.00";
                    satir++;
                }

                // Toplam
                ws.Cell(satir, 5).Value = "TOPLAM:";
                ws.Cell(satir, 5).Style.Font.Bold = true;
                ws.Cell(satir, 6).Value = ozet.ToplamHacim;
                ws.Cell(satir, 6).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(satir, 6).Style.Font.Bold = true;
                satir += 2;
            }

            ws.Columns().AdjustToContents();
        }

        private void KaziDolguOzetSayfasi(XLWorkbook workbook, YolMetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Kaz\u0131-Dolgu \u00D6zet");
            int satir = 1;

            ws.Cell(satir, 1).Value = "KAZI-DOLGU METRAJ \u00D6ZET\u0130";
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 1).Style.Font.FontSize = 14;
            satir += 2;

            ws.Cell(satir, 1).Value = "Proje:";
            ws.Cell(satir, 2).Value = rapor.ProjeAdi ?? "-";
            satir++;
            ws.Cell(satir, 1).Value = "Tarih:";
            ws.Cell(satir, 2).Value = rapor.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm");
            satir++;
            ws.Cell(satir, 1).Value = "Metot:";
            ws.Cell(satir, 2).Value = rapor.KubajSonucu.Metot == HacimMetodu.Prismoidal ? "Prismoidal" : "Ortalama Alan";
            satir += 2;

            ws.Cell(satir, 1).Value = "Toplam Kaz\u0131 Hacmi:";
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 2).Value = rapor.KubajSonucu.ToplamKaziHacmi;
            ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(satir, 3).Value = "m\u00B3";
            satir++;
            ws.Cell(satir, 1).Value = "Toplam Dolgu Hacmi:";
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 2).Value = rapor.KubajSonucu.ToplamDolguHacmi;
            ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(satir, 3).Value = "m\u00B3";
            satir++;
            ws.Cell(satir, 1).Value = "Net Hacim:";
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 2).Value = rapor.KubajSonucu.NetHacim;
            ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(satir, 3).Value = "m\u00B3";
            satir += 2;

            // Malzeme bazlı özet
            ws.Cell(satir, 1).Value = "MALZEME BAZLI \u00D6ZET";
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 1).Style.Font.FontSize = 12;
            satir++;

            ws.Cell(satir, 1).Value = "Malzeme";
            ws.Cell(satir, 2).Value = "Kategori";
            ws.Cell(satir, 3).Value = "Toplam Hacim (m\u00B3)";
            var baslikRange = ws.Range(satir, 1, satir, 3);
            baslikRange.Style.Font.Bold = true;
            baslikRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            baslikRange.Style.Font.FontColor = XLColor.White;
            satir++;

            foreach (var ozet in rapor.KubajSonucu.MalzemeOzetleri)
            {
                ws.Cell(satir, 1).Value = ozet.MalzemeAdi;
                ws.Cell(satir, 2).Value = ozet.Kategori.ToString();
                ws.Cell(satir, 3).Value = ozet.ToplamHacim;
                ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.00";
                satir++;
            }

            ws.Columns().AdjustToContents();
        }

        private void BrucknerSayfasi(XLWorkbook workbook, YolMetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Bruckner");

            ws.Cell(1, 1).Value = "\u0130stasyon (m)";
            ws.Cell(1, 2).Value = "K\u00FCm\u00FClatif Hacim (m\u00B3)";
            var baslikRange = ws.Range(1, 1, 1, 2);
            baslikRange.Style.Font.Bold = true;
            baslikRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            baslikRange.Style.Font.FontColor = XLColor.White;

            for (int i = 0; i < rapor.KubajSonucu.BrucknerVerisi.Count; i++)
            {
                var nokta = rapor.KubajSonucu.BrucknerVerisi[i];
                ws.Cell(i + 2, 1).Value = nokta.Istasyon;
                ws.Cell(i + 2, 1).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(i + 2, 2).Value = nokta.KumulatifHacim;
                ws.Cell(i + 2, 2).Style.NumberFormat.Format = "#,##0.00";
            }

            ws.Columns().AdjustToContents();
        }
    }
}
