using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using Metraj.Models.IhaleKontrol;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public partial class ExcelExportService
    {
        public ExportResult IhaleKontrolExport(IhaleKontrolRaporu rapor, string dosyaYolu)
        {
            try
            {
                if (rapor == null || rapor.Karsilastirmalar.Count == 0)
                    return new ExportResult { Basarili = false, HataMesaji = "İhale kontrol verisi boş." };

                using (var workbook = new XLWorkbook())
                {
                    TabloDegerleriSayfasi(workbook, rapor);
                    GeometrikHesapSayfasi(workbook, rapor);
                    FarkAnaliziSayfasi(workbook, rapor);

                    if (rapor.KubajSonucu != null && rapor.KubajSonucu.MalzemeKubajlari.Count > 0)
                        KubajKarsilastirmaSayfasi(workbook, rapor);

                    UyarilarSayfasi(workbook, rapor);

                    workbook.SaveAs(dosyaYolu);
                }

                LoggingService.Info("İhale kontrol Excel oluşturuldu: {DosyaYolu}", dosyaYolu);
                return new ExportResult { Basarili = true, DosyaYolu = dosyaYolu };
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("İhale kontrol Excel hatası", ex);
                return new ExportResult { Basarili = false, HataMesaji = ex.Message };
            }
        }

        private void TabloDegerleriSayfasi(XLWorkbook workbook, IhaleKontrolRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Tablo Değerleri");

            // Tüm malzeme adlarını topla
            var malzemeAdlari = rapor.TabloVerileri
                .SelectMany(t => t.MalzemeAlanlari.Select(m => m.NormalizeMalzemeAdi))
                .Distinct().ToList();

            // Başlık satırı
            ws.Cell(1, 1).Value = "KM";
            for (int i = 0; i < malzemeAdlari.Count; i++)
                ws.Cell(1, i + 2).Value = malzemeAdlari[i];

            BaslikStiliUygula(ws.Range(1, 1, 1, malzemeAdlari.Count + 1));

            // Veri satırları
            int row = 2;
            foreach (var tablo in rapor.TabloVerileri.OrderBy(t => t.Istasyon))
            {
                ws.Cell(row, 1).Value = tablo.IstasyonMetni;
                for (int i = 0; i < malzemeAdlari.Count; i++)
                {
                    var malzeme = tablo.MalzemeAlanlari
                        .FirstOrDefault(m => m.NormalizeMalzemeAdi == malzemeAdlari[i]);
                    ws.Cell(row, i + 2).Value = malzeme?.Alan ?? 0;
                    ws.Cell(row, i + 2).Style.NumberFormat.Format = "0.00";
                }
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void GeometrikHesapSayfasi(XLWorkbook workbook, IhaleKontrolRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Geometrik Hesap");

            var malzemeAdlari = rapor.GeometrikVeriler
                .SelectMany(g => g.TabakaAlanlari.Select(t => t.MalzemeAdi))
                .Distinct().ToList();

            ws.Cell(1, 1).Value = "KM";
            for (int i = 0; i < malzemeAdlari.Count; i++)
                ws.Cell(1, i + 2).Value = malzemeAdlari[i];

            BaslikStiliUygula(ws.Range(1, 1, 1, malzemeAdlari.Count + 1));

            int row = 2;
            foreach (var geo in rapor.GeometrikVeriler.OrderBy(g => g.Istasyon))
            {
                ws.Cell(row, 1).Value = geo.IstasyonMetni;
                for (int i = 0; i < malzemeAdlari.Count; i++)
                {
                    var tabaka = geo.TabakaAlanlari
                        .FirstOrDefault(t => t.MalzemeAdi == malzemeAdlari[i]);
                    if (tabaka != null)
                    {
                        ws.Cell(row, i + 2).Value = tabaka.Alan;
                        ws.Cell(row, i + 2).Style.NumberFormat.Format = "0.00";
                        if (tabaka.Tahmini)
                            ws.Cell(row, i + 2).Style.Font.Italic = true;
                    }
                }
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void FarkAnaliziSayfasi(XLWorkbook workbook, IhaleKontrolRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Fark Analizi");

            ws.Cell(1, 1).Value = "KM";
            ws.Cell(1, 2).Value = "Malzeme";
            ws.Cell(1, 3).Value = "İhale (m²)";
            ws.Cell(1, 4).Value = "Hesap (m²)";
            ws.Cell(1, 5).Value = "Fark (m²)";
            ws.Cell(1, 6).Value = "%";
            ws.Cell(1, 7).Value = "Durum";
            BaslikStiliUygula(ws.Range(1, 1, 1, 7));

            int row = 2;
            foreach (var karsilastirma in rapor.Karsilastirmalar.OrderBy(k => k.Istasyon))
            {
                foreach (var malzeme in karsilastirma.Malzemeler)
                {
                    ws.Cell(row, 1).Value = karsilastirma.IstasyonMetni;
                    ws.Cell(row, 2).Value = malzeme.MalzemeAdi;
                    ws.Cell(row, 3).Value = malzeme.TabloDegeri;
                    ws.Cell(row, 3).Style.NumberFormat.Format = "0.00";

                    if (malzeme.GeometrikHesapYapildi)
                    {
                        ws.Cell(row, 4).Value = malzeme.GeometrikDeger;
                        ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
                        ws.Cell(row, 5).Value = malzeme.Fark;
                        ws.Cell(row, 5).Style.NumberFormat.Format = "0.00";
                        ws.Cell(row, 6).Value = malzeme.FarkYuzde;
                        ws.Cell(row, 6).Style.NumberFormat.Format = "0.0";
                    }
                    else
                    {
                        ws.Cell(row, 4).Value = "-";
                        ws.Cell(row, 5).Value = "-";
                        ws.Cell(row, 6).Value = "-";
                    }

                    ws.Cell(row, 7).Value = DurumMetni(malzeme.Durum);
                    DurumRengiUygula(ws.Cell(row, 7), malzeme.Durum);

                    // Satır renklendirme
                    if (malzeme.Durum == KontrolDurumu.Hata)
                        ws.Range(row, 1, row, 7).Style.Font.FontColor = XLColor.Red;
                    else if (malzeme.Durum == KontrolDurumu.Uyari)
                        ws.Range(row, 1, row, 7).Style.Font.FontColor = XLColor.DarkGoldenrod;

                    row++;
                }
            }

            ws.Columns().AdjustToContents();
        }

        private void KubajKarsilastirmaSayfasi(XLWorkbook workbook, IhaleKontrolRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Kübaj Karşılaştırması");

            ws.Cell(1, 1).Value = "Malzeme";
            ws.Cell(1, 2).Value = "İhale (m³)";
            ws.Cell(1, 3).Value = "Hesap (m³)";
            ws.Cell(1, 4).Value = "Fark (m³)";
            ws.Cell(1, 5).Value = "%";
            ws.Cell(1, 6).Value = "Durum";
            BaslikStiliUygula(ws.Range(1, 1, 1, 6));

            int row = 2;
            foreach (var kubaj in rapor.KubajSonucu.MalzemeKubajlari)
            {
                ws.Cell(row, 1).Value = kubaj.MalzemeAdi;
                ws.Cell(row, 2).Value = kubaj.IhaleHacmi;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 3).Value = kubaj.HesapHacmi;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 4).Value = kubaj.Fark;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 5).Value = kubaj.FarkYuzde;
                ws.Cell(row, 5).Style.NumberFormat.Format = "0.0";
                ws.Cell(row, 6).Value = DurumMetni(kubaj.Durum);
                DurumRengiUygula(ws.Cell(row, 6), kubaj.Durum);
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void UyarilarSayfasi(XLWorkbook workbook, IhaleKontrolRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Uyarılar");

            ws.Cell(1, 1).Value = "KM";
            ws.Cell(1, 2).Value = "Malzeme";
            ws.Cell(1, 3).Value = "Durum";
            ws.Cell(1, 4).Value = "Fark (m²)";
            ws.Cell(1, 5).Value = "Fark (%)";
            ws.Cell(1, 6).Value = "Açıklama";
            BaslikStiliUygula(ws.Range(1, 1, 1, 6));

            int row = 2;
            foreach (var karsilastirma in rapor.Karsilastirmalar.OrderBy(k => k.Istasyon))
            {
                foreach (var malzeme in karsilastirma.Malzemeler
                    .Where(m => m.Durum == KontrolDurumu.Hata || m.Durum == KontrolDurumu.Uyari))
                {
                    ws.Cell(row, 1).Value = karsilastirma.IstasyonMetni;
                    ws.Cell(row, 2).Value = malzeme.MalzemeAdi;
                    ws.Cell(row, 3).Value = DurumMetni(malzeme.Durum);
                    DurumRengiUygula(ws.Cell(row, 3), malzeme.Durum);
                    ws.Cell(row, 4).Value = malzeme.Fark;
                    ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
                    ws.Cell(row, 5).Value = malzeme.FarkYuzde;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "0.0";
                    ws.Cell(row, 6).Value = malzeme.Tahmini ? "Tahmini değer (tabaka çizgisi yok)" : "";
                    row++;
                }
            }

            // Ardışık kesit tutarlılık uyarıları
            row++;
            ws.Cell(row, 1).Value = "ARDİŞİK KESİT TUTARSIZLIKLARI";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            var siraliTablolar = rapor.TabloVerileri.OrderBy(t => t.Istasyon).ToList();
            for (int i = 0; i < siraliTablolar.Count - 1; i++)
            {
                var mevcut = siraliTablolar[i];
                var sonraki = siraliTablolar[i + 1];

                foreach (var malzeme in mevcut.MalzemeAlanlari)
                {
                    var sonrakiMalzeme = sonraki.MalzemeAlanlari
                        .FirstOrDefault(m => m.NormalizeMalzemeAdi == malzeme.NormalizeMalzemeAdi);

                    if (sonrakiMalzeme != null && malzeme.Alan > 0)
                    {
                        double degisim = Math.Abs(sonrakiMalzeme.Alan - malzeme.Alan) / malzeme.Alan * 100;
                        if (degisim > 50)
                        {
                            ws.Cell(row, 1).Value = $"{mevcut.IstasyonMetni} → {sonraki.IstasyonMetni}";
                            ws.Cell(row, 2).Value = malzeme.NormalizeMalzemeAdi;
                            ws.Cell(row, 3).Value = "UYARI";
                            ws.Cell(row, 3).Style.Font.FontColor = XLColor.DarkGoldenrod;
                            ws.Cell(row, 4).Value = sonrakiMalzeme.Alan - malzeme.Alan;
                            ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
                            ws.Cell(row, 5).Value = degisim;
                            ws.Cell(row, 5).Style.NumberFormat.Format = "0.0";
                            ws.Cell(row, 6).Value = $"Ardışık kesitlerde >%50 alan sıçraması ({malzeme.Alan:F2} → {sonrakiMalzeme.Alan:F2})";
                            row++;
                        }
                    }
                }
            }

            ws.Columns().AdjustToContents();
        }

        // --- Yardımcı metodlar ---

        private void BaslikStiliUygula(IXLRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.DarkSlateGray;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private string DurumMetni(KontrolDurumu durum)
        {
            switch (durum)
            {
                case KontrolDurumu.OK: return "OK";
                case KontrolDurumu.Uyari: return "UYARI";
                case KontrolDurumu.Hata: return "HATA";
                case KontrolDurumu.DogrulamaYok: return "Doğrulama Yok";
                default: return "-";
            }
        }

        private void DurumRengiUygula(IXLCell cell, KontrolDurumu durum)
        {
            switch (durum)
            {
                case KontrolDurumu.OK:
                    cell.Style.Font.FontColor = XLColor.DarkGreen;
                    break;
                case KontrolDurumu.Uyari:
                    cell.Style.Font.FontColor = XLColor.DarkGoldenrod;
                    cell.Style.Font.Bold = true;
                    break;
                case KontrolDurumu.Hata:
                    cell.Style.Font.FontColor = XLColor.Red;
                    cell.Style.Font.Bold = true;
                    break;
                case KontrolDurumu.DogrulamaYok:
                    cell.Style.Font.FontColor = XLColor.Gray;
                    cell.Style.Font.Italic = true;
                    break;
            }
        }
    }
}
