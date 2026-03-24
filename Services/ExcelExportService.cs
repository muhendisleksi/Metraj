using System;
using System.Linq;
using ClosedXML.Excel;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public partial class ExcelExportService : IExcelExportService
    {
        public ExportResult Export(MetrajRaporu rapor, string dosyaYolu)
        {
            try
            {
                if (rapor == null)
                    return new ExportResult { Basarili = false, HataMesaji = "Rapor verisi bos." };

                using (var workbook = new XLWorkbook())
                {
                    // Uzunluk sayfasi
                    if (rapor.UzunlukSonuclari != null && rapor.UzunlukSonuclari.Count > 0)
                        UzunlukSayfasiOlustur(workbook, rapor);

                    // Alan sayfasi
                    if (rapor.AlanSonuclari != null && rapor.AlanSonuclari.Count > 0)
                        AlanSayfasiOlustur(workbook, rapor);

                    // Hacim sayfasi
                    if (rapor.HacimSonucu != null && rapor.HacimSonucu.Segmentler.Count > 0)
                        HacimSayfasiOlustur(workbook, rapor);

                    // Toplama sayfasi
                    if (rapor.ToplamaSonuclari != null && rapor.ToplamaSonuclari.Count > 0)
                        ToplamaSayfasiOlustur(workbook, rapor);

                    // Ozet sayfasi
                    OzetSayfasiOlustur(workbook, rapor);

                    if (workbook.Worksheets.Count == 0)
                        return new ExportResult { Basarili = false, HataMesaji = "Disa aktarilacak veri yok." };

                    workbook.SaveAs(dosyaYolu);
                }

                LoggingService.Info("Excel disa aktarma basarili: {DosyaYolu}", dosyaYolu);
                return new ExportResult { Basarili = true, DosyaYolu = dosyaYolu };
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Excel disa aktarma hatasi", ex);
                return new ExportResult { Basarili = false, HataMesaji = ex.Message };
            }
        }

        private void UzunlukSayfasiOlustur(XLWorkbook workbook, MetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Uzunluk");
            ws.Cell(1, 1).Value = "UZUNLUK METRAJ CETVELI";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 4).Merge();

            ws.Cell(2, 1).Value = "Tarih: " + rapor.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm");
            ws.Range(2, 1, 2, 4).Merge();

            int row = 4;
            ws.Cell(row, 1).Value = "No";
            ws.Cell(row, 2).Value = "Nesne Tipi";
            ws.Cell(row, 3).Value = "Katman";
            ws.Cell(row, 4).Value = "Uzunluk (m)";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGray;

            int no = 1;
            foreach (var s in rapor.UzunlukSonuclari)
            {
                row++;
                ws.Cell(row, 1).Value = no++;
                ws.Cell(row, 2).Value = s.NesneTipi;
                ws.Cell(row, 3).Value = s.KatmanAdi;
                ws.Cell(row, 4).Value = s.Uzunluk;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            }

            row++;
            ws.Cell(row, 3).Value = "TOPLAM:";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = rapor.UzunlukSonuclari.Sum(s => s.Uzunluk);
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";

            ws.Columns().AdjustToContents();
        }

        private void AlanSayfasiOlustur(XLWorkbook workbook, MetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Alan");
            ws.Cell(1, 1).Value = "ALAN METRAJ CETVELI";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 5).Merge();

            ws.Cell(2, 1).Value = "Tarih: " + rapor.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm");

            int row = 4;
            ws.Cell(row, 1).Value = "No";
            ws.Cell(row, 2).Value = "Nesne Tipi";
            ws.Cell(row, 3).Value = "Katman";
            ws.Cell(row, 4).Value = "Alan (m\u00B2)";
            ws.Cell(row, 5).Value = "Cevre (m)";
            ws.Range(row, 1, row, 5).Style.Font.Bold = true;
            ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightGray;

            int no = 1;
            foreach (var s in rapor.AlanSonuclari)
            {
                row++;
                ws.Cell(row, 1).Value = no++;
                ws.Cell(row, 2).Value = s.NesneTipi;
                ws.Cell(row, 3).Value = s.KatmanAdi;
                ws.Cell(row, 4).Value = s.Alan;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 5).Value = s.Cevre;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            }

            row++;
            ws.Cell(row, 3).Value = "TOPLAM:";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = rapor.AlanSonuclari.Sum(s => s.Alan);
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";

            ws.Columns().AdjustToContents();
        }

        private void HacimSayfasiOlustur(XLWorkbook workbook, MetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Hacim");
            ws.Cell(1, 1).Value = "HACIM HESAP CETVELI";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 6).Merge();

            ws.Cell(2, 1).Value = "Metot: " + (rapor.HacimSonucu.Metot == HacimMetodu.OrtalamaAlan ? "Ortalama Alan" : "Prismoidal");
            ws.Cell(3, 1).Value = "Tarih: " + rapor.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm");

            int row = 5;
            ws.Cell(row, 1).Value = "No";
            ws.Cell(row, 2).Value = "Ist. 1";
            ws.Cell(row, 3).Value = "Ist. 2";
            ws.Cell(row, 4).Value = "Alan 1 (m\u00B2)";
            ws.Cell(row, 5).Value = "Alan 2 (m\u00B2)";
            ws.Cell(row, 6).Value = "Hacim (m\u00B3)";
            ws.Range(row, 1, row, 6).Style.Font.Bold = true;
            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.LightGray;

            int no = 1;
            foreach (var seg in rapor.HacimSonucu.Segmentler)
            {
                row++;
                ws.Cell(row, 1).Value = no++;
                ws.Cell(row, 2).Value = seg.Istasyon1;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(row, 3).Value = seg.Istasyon2;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(row, 4).Value = seg.Alan1;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 5).Value = seg.Alan2;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 6).Value = seg.Hacim;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            }

            row++;
            ws.Cell(row, 5).Value = "TOPLAM:";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 6).Value = rapor.HacimSonucu.ToplamHacim;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";

            // Bruckner data
            if (rapor.HacimSonucu.BrucknerVerisi != null && rapor.HacimSonucu.BrucknerVerisi.Count > 0)
            {
                row += 3;
                ws.Cell(row, 1).Value = "BRUCKNER DIYAGRAMI VERISI";
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;
                ws.Cell(row, 1).Value = "Istasyon";
                ws.Cell(row, 2).Value = "Kumulatif Hacim (m\u00B3)";
                ws.Range(row, 1, row, 2).Style.Font.Bold = true;
                ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.LightGray;

                foreach (var b in rapor.HacimSonucu.BrucknerVerisi)
                {
                    row++;
                    ws.Cell(row, 1).Value = b.Istasyon;
                    ws.Cell(row, 1).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(row, 2).Value = b.KumulatifHacim;
                    ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                }
            }

            ws.Columns().AdjustToContents();
        }

        private void ToplamaSayfasiOlustur(XLWorkbook workbook, MetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Toplama");
            ws.Cell(1, 1).Value = "METIN TOPLAMA CETVELI";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 4).Merge();

            int row = 3;
            ws.Cell(row, 1).Value = "No";
            ws.Cell(row, 2).Value = "Metin Degeri";
            ws.Cell(row, 3).Value = "Sayisal Deger";
            ws.Cell(row, 4).Value = "Katman";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGray;

            int no = 1;
            foreach (var oge in rapor.ToplamaSonuclari)
            {
                row++;
                ws.Cell(row, 1).Value = no++;
                ws.Cell(row, 2).Value = oge.MetinDegeri;
                if (oge.GecerliSayi)
                {
                    ws.Cell(row, 3).Value = oge.SayisalDeger;
                    ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                }
                else
                {
                    ws.Cell(row, 3).Value = "Gecersiz";
                    ws.Cell(row, 3).Style.Font.FontColor = XLColor.Red;
                }
                ws.Cell(row, 4).Value = oge.KatmanAdi;
            }

            row++;
            ws.Cell(row, 2).Value = "TOPLAM:";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = rapor.ToplamaSonuclari.Where(o => o.GecerliSayi).Sum(o => o.SayisalDeger);
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";

            ws.Columns().AdjustToContents();
        }

        private void OzetSayfasiOlustur(XLWorkbook workbook, MetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Ozet");
            ws.Cell(1, 1).Value = "METRAJ OZET RAPORU";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 3).Merge();

            ws.Cell(2, 1).Value = "Tarih: " + rapor.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm");

            int row = 4;
            ws.Cell(row, 1).Value = "Olcum Tipi";
            ws.Cell(row, 2).Value = "Adet";
            ws.Cell(row, 3).Value = "Toplam";
            ws.Range(row, 1, row, 3).Style.Font.Bold = true;
            ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;

            if (rapor.UzunlukSonuclari != null && rapor.UzunlukSonuclari.Count > 0)
            {
                row++;
                ws.Cell(row, 1).Value = "Uzunluk";
                ws.Cell(row, 2).Value = rapor.UzunlukSonuclari.Count;
                ws.Cell(row, 3).Value = rapor.UzunlukSonuclari.Sum(s => s.Uzunluk).ToString("F2") + " m";
            }

            if (rapor.AlanSonuclari != null && rapor.AlanSonuclari.Count > 0)
            {
                row++;
                ws.Cell(row, 1).Value = "Alan";
                ws.Cell(row, 2).Value = rapor.AlanSonuclari.Count;
                ws.Cell(row, 3).Value = rapor.AlanSonuclari.Sum(s => s.Alan).ToString("F2") + " m\u00B2";
            }

            if (rapor.HacimSonucu != null && rapor.HacimSonucu.Segmentler != null && rapor.HacimSonucu.Segmentler.Count > 0)
            {
                row++;
                ws.Cell(row, 1).Value = "Hacim";
                ws.Cell(row, 2).Value = rapor.HacimSonucu.Segmentler.Count + " segment";
                ws.Cell(row, 3).Value = rapor.HacimSonucu.ToplamHacim.ToString("F2") + " m\u00B3";
            }

            if (rapor.ToplamaSonuclari != null && rapor.ToplamaSonuclari.Count > 0)
            {
                row++;
                ws.Cell(row, 1).Value = "Toplama";
                ws.Cell(row, 2).Value = rapor.ToplamaSonuclari.Count;
                ws.Cell(row, 3).Value = rapor.ToplamaSonuclari.Where(o => o.GecerliSayi).Sum(o => o.SayisalDeger).ToString("F2");
            }

            ws.Columns().AdjustToContents();
        }
    }
}
