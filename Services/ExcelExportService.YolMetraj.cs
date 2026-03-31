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
                    OzetSayfasi(workbook, rapor);
                    KesitAlanlariSayfasi(workbook, rapor);

                    if (rapor.KubajSonucu != null && rapor.KubajSonucu.MalzemeOzetleri.Count > 0)
                    {
                        KubajCetveliSayfasi(workbook, rapor);

                        if (rapor.Kesitler.Count >= 2)
                            BrucknerCetveliSayfasi(workbook, rapor);
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

        // ═══════════════════════════════════════════════════════════════════
        //  1) ÖZET SAYFASI
        // ═══════════════════════════════════════════════════════════════════

        private void OzetSayfasi(XLWorkbook workbook, YolMetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("\u00D6zet");

            ApplySheetTitle(ws, 1, 1, 4, "YOL METRAJ \u00D6ZET\u0130");

            // ── Proje Bilgileri ──
            int satir = 3;
            ApplySectionTitle(ws, satir, 1, 4, "PROJE B\u0130LG\u0130LER\u0130");
            satir++;

            WriteParamRow(ws, ref satir, "Proje:", rapor.ProjeAdi ?? "-");
            WriteParamRow(ws, ref satir, "Tarih:", rapor.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm"));

            if (rapor.KubajSonucu != null)
                WriteParamRow(ws, ref satir, "Hesap Metodu:", rapor.KubajSonucu.Metot == HacimMetodu.Prismoidal ? "Prismoidal Form\u00FCl" : "Ortalama Alan Metodu");

            WriteParamRow(ws, ref satir, "Kesit Say\u0131s\u0131:", rapor.Kesitler.Count.ToString());

            if (rapor.Kesitler.Count >= 2)
            {
                var sirali = rapor.Kesitler.OrderBy(k => k.Istasyon).ToList();
                double toplamUzunluk = sirali.Last().Istasyon - sirali.First().Istasyon;
                WriteParamRow(ws, ref satir, "Toplam G\u00FCzergah:", toplamUzunluk, "m", "#,##0.00");
                WriteParamRow(ws, ref satir, "Ba\u015Flang\u0131\u00E7 \u0130st.:", sirali.First().IstasyonMetni ?? YolKesitService.IstasyonFormatla(sirali.First().Istasyon));
                WriteParamRow(ws, ref satir, "Biti\u015F \u0130st.:", sirali.Last().IstasyonMetni ?? YolKesitService.IstasyonFormatla(sirali.Last().Istasyon));
            }

            ApplyDataBorders(ws.Range(4, 1, satir - 1, 2));
            satir++;

            // ── Kazı-Dolgu Dengesi ──
            if (rapor.KubajSonucu != null)
            {
                ApplySectionTitle(ws, satir, 1, 4, "KAZI-DOLGU DENGES\u0130");
                satir++;

                int dengeBaslangic = satir;

                ws.Cell(satir, 1).Value = "Toplam Kaz\u0131 Hacmi";
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 2).Value = rapor.KubajSonucu.ToplamKaziHacmi;
                ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 2).Style.Font.Bold = true;
                ws.Cell(satir, 3).Value = "m\u00B3";
                ws.Cell(satir, 3).Style.Font.FontColor = XLColor.FromArgb(155, 168, 182);
                ws.Range(satir, 1, satir, 3).Style.Fill.BackgroundColor = KaziBgColor;
                satir++;

                ws.Cell(satir, 1).Value = "Toplam Dolgu Hacmi";
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 2).Value = rapor.KubajSonucu.ToplamDolguHacmi;
                ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 2).Style.Font.Bold = true;
                ws.Cell(satir, 3).Value = "m\u00B3";
                ws.Cell(satir, 3).Style.Font.FontColor = XLColor.FromArgb(155, 168, 182);
                ws.Range(satir, 1, satir, 3).Style.Fill.BackgroundColor = DolguBgColor;
                satir++;

                ws.Cell(satir, 1).Value = "Net Hacim (Kaz\u0131 \u2212 Dolgu)";
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 2).Value = rapor.KubajSonucu.NetHacim;
                ws.Cell(satir, 2).Style.NumberFormat.Format = "+#,##0.000;\u2212#,##0.000;0.000";
                ws.Cell(satir, 3).Value = "m\u00B3";
                ws.Cell(satir, 3).Style.Font.FontColor = XLColor.FromArgb(155, 168, 182);
                ApplyGrandTotalRow(ws.Range(satir, 1, satir, 3));

                // Net hacim rengini belirle
                if (rapor.KubajSonucu.NetHacim > 0)
                    ws.Cell(satir, 2).Style.Font.FontColor = SuccessColor;
                else if (rapor.KubajSonucu.NetHacim < 0)
                    ws.Cell(satir, 2).Style.Font.FontColor = ErrorColor;

                ApplyDataBorders(ws.Range(dengeBaslangic, 1, satir, 3));
                satir += 2;

                // ── Malzeme Bazlı Hacim Tablosu ──
                ApplySectionTitle(ws, satir, 1, 4, "MALZEME BAZLI HAC\u0130M \u00D6ZET\u0130");
                satir++;

                ws.Cell(satir, 1).Value = "Malzeme";
                ws.Cell(satir, 2).Value = "Kategori";
                ws.Cell(satir, 3).Value = "Hacim (m\u00B3)";
                ws.Cell(satir, 4).Value = "Oran (%)";
                ApplyHeaderStyle(ws.Range(satir, 1, satir, 4));
                satir++;

                double toplamHacim = rapor.KubajSonucu.MalzemeOzetleri.Sum(m => m.ToplamHacim);
                int malzDataStart = satir;

                // Kategoriye gore gruplama
                var kategoriler = rapor.KubajSonucu.MalzemeOzetleri
                    .GroupBy(m => m.Kategori)
                    .OrderBy(g => KategoriSirasi(g.Key));

                foreach (var grup in kategoriler)
                {
                    foreach (var ozet in grup.OrderByDescending(m => m.ToplamHacim))
                    {
                        ws.Cell(satir, 1).Value = ozet.MalzemeAdi;
                        ws.Cell(satir, 2).Value = KategoriMetni(ozet.Kategori);
                        ws.Cell(satir, 3).Value = ozet.ToplamHacim;
                        ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.000";
                        ws.Cell(satir, 4).Value = toplamHacim > 0 ? (ozet.ToplamHacim / toplamHacim * 100) : 0;
                        ws.Cell(satir, 4).Style.NumberFormat.Format = "#,##0.0";

                        // Kategori renk aksanı
                        ws.Cell(satir, 1).Style.Border.LeftBorder = XLBorderStyleValues.Thick;
                        ws.Cell(satir, 1).Style.Border.LeftBorderColor = KategoriRengi(ozet.Kategori);
                        satir++;
                    }

                    // Kategori ara toplam
                    double katToplamHacim = grup.Sum(m => m.ToplamHacim);
                    ws.Cell(satir, 1).Value = KategoriMetni(grup.Key) + " Toplam\u0131";
                    ws.Cell(satir, 3).Value = katToplamHacim;
                    ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(satir, 4).Value = toplamHacim > 0 ? (katToplamHacim / toplamHacim * 100) : 0;
                    ws.Cell(satir, 4).Style.NumberFormat.Format = "#,##0.0";
                    ApplySubtotalRow(ws.Range(satir, 1, satir, 4));
                    satir++;
                }

                int malzDataEnd = satir - 1;
                if (malzDataEnd >= malzDataStart)
                {
                    ApplyAltRowShading(ws, malzDataStart, malzDataEnd, 1, 4);
                    ApplyDataBorders(ws.Range(malzDataStart - 1, 1, malzDataEnd, 4));
                }

                satir++;

                // ── İstatistikler ──
                ApplySectionTitle(ws, satir, 1, 4, "\u0130STAT\u0130ST\u0130KLER");
                satir++;

                var siraliK = rapor.Kesitler.OrderBy(k => k.Istasyon).ToList();
                double avgKaziAlan = siraliK.Average(k => k.ToplamKaziAlani);
                double avgDolguAlan = siraliK.Average(k => k.ToplamDolguAlani);
                double maxKaziAlan = siraliK.Max(k => k.ToplamKaziAlani);
                double maxDolguAlan = siraliK.Max(k => k.ToplamDolguAlani);
                double minKaziAlan = siraliK.Min(k => k.ToplamKaziAlani);
                double minDolguAlan = siraliK.Min(k => k.ToplamDolguAlani);

                int istatBaslangic = satir;
                WriteParamRow(ws, ref satir, "Ort. Kaz\u0131 Alan\u0131:", avgKaziAlan, "m\u00B2", "#,##0.00");
                WriteParamRow(ws, ref satir, "Ort. Dolgu Alan\u0131:", avgDolguAlan, "m\u00B2", "#,##0.00");
                WriteParamRow(ws, ref satir, "Maks. Kaz\u0131 Alan\u0131:", maxKaziAlan, "m\u00B2", "#,##0.00");
                WriteParamRow(ws, ref satir, "Maks. Dolgu Alan\u0131:", maxDolguAlan, "m\u00B2", "#,##0.00");
                WriteParamRow(ws, ref satir, "Min. Kaz\u0131 Alan\u0131:", minKaziAlan, "m\u00B2", "#,##0.00");
                WriteParamRow(ws, ref satir, "Min. Dolgu Alan\u0131:", minDolguAlan, "m\u00B2", "#,##0.00");

                // Brückner istatistikleri
                if (rapor.KubajSonucu.BrucknerVerisi != null && rapor.KubajSonucu.BrucknerVerisi.Count > 0)
                {
                    var bruckner = rapor.KubajSonucu.BrucknerVerisi;
                    double maxBr = bruckner.Max(b => b.KumulatifHacim);
                    double minBr = bruckner.Min(b => b.KumulatifHacim);
                    var maxBrNokta = bruckner.First(b => b.KumulatifHacim == maxBr);
                    var minBrNokta = bruckner.First(b => b.KumulatifHacim == minBr);

                    WriteParamRow(ws, ref satir, "Br\u00FCckner Maks.:", maxBr, "m\u00B3", "+#,##0.000;\u2212#,##0.000;0.000");
                    WriteParamRow(ws, ref satir, "  (istasyon):", YolKesitService.IstasyonFormatla(maxBrNokta.Istasyon));
                    WriteParamRow(ws, ref satir, "Br\u00FCckner Min.:", minBr, "m\u00B3", "+#,##0.000;\u2212#,##0.000;0.000");
                    WriteParamRow(ws, ref satir, "  (istasyon):", YolKesitService.IstasyonFormatla(minBrNokta.Istasyon));
                }

                ApplyDataBorders(ws.Range(istatBaslangic, 1, satir - 1, 3));
            }

            ws.Column(1).Width = 28;
            ws.Column(2).Width = 18;
            ws.Column(3).Width = 12;
            ws.Column(4).Width = 12;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  2) KESİT ALANLARI SAYFASI
        // ═══════════════════════════════════════════════════════════════════

        private void KesitAlanlariSayfasi(XLWorkbook workbook, YolMetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Kesit Alanlar\u0131");

            var malzemeler = rapor.Kesitler
                .SelectMany(k => k.KatmanAlanlari)
                .Select(k => k.MalzemeAdi)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int sonSutun = 3 + malzemeler.Count + 1;

            ApplySheetTitle(ws, 1, 1, sonSutun, "KES\u0130T ALANLARI TABLOSU");

            int col = 1;
            ws.Cell(2, col).Value = "No";
            ws.Cell(2, col + 1).Value = "\u0130stasyon";
            col = 3;
            foreach (var m in malzemeler)
            {
                ws.Cell(2, col).Value = m + " (m\u00B2)";
                col++;
            }
            int kaziCol = col;
            int dolguCol = col + 1;
            ws.Cell(2, kaziCol).Value = "Kaz\u0131 (m\u00B2)";
            ws.Cell(2, dolguCol).Value = "Dolgu (m\u00B2)";

            ApplyHeaderStyle(ws.Range(2, 1, 2, sonSutun));
            ws.SheetView.FreezeRows(2);

            int dataStartRow = 3;
            for (int i = 0; i < rapor.Kesitler.Count; i++)
            {
                var kesit = rapor.Kesitler[i];
                int satir = dataStartRow + i;
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

                ws.Cell(satir, kaziCol).Value = kesit.ToplamKaziAlani;
                ws.Cell(satir, kaziCol).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(satir, kaziCol).Style.Fill.BackgroundColor = KaziBgColor;

                ws.Cell(satir, dolguCol).Value = kesit.ToplamDolguAlani;
                ws.Cell(satir, dolguCol).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(satir, dolguCol).Style.Fill.BackgroundColor = DolguBgColor;
            }

            int dataEndRow = dataStartRow + rapor.Kesitler.Count - 1;
            ApplyAltRowShading(ws, dataStartRow, dataEndRow, 1, kaziCol - 1);

            // Toplam satiri
            int toplamRow = dataEndRow + 1;
            ws.Cell(toplamRow, 2).Value = "TOPLAM";
            for (int c = 3; c <= sonSutun; c++)
            {
                double toplam = 0;
                for (int r = dataStartRow; r <= dataEndRow; r++)
                {
                    var val = ws.Cell(r, c).Value;
                    if (val.IsNumber) toplam += val.GetNumber();
                }
                ws.Cell(toplamRow, c).Value = toplam;
                ws.Cell(toplamRow, c).Style.NumberFormat.Format = "#,##0.00";
            }
            ApplyGrandTotalRow(ws.Range(toplamRow, 1, toplamRow, sonSutun));
            ApplyDataBorders(ws.Range(2, 1, toplamRow, sonSutun));

            FinalizeSheet(ws);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  3) KÜBAJ CETVELİ SAYFASI (detaylı, kümülatif hacimli)
        // ═══════════════════════════════════════════════════════════════════

        private void KubajCetveliSayfasi(XLWorkbook workbook, YolMetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("K\u00FCbaj Cetveli");
            int sonSutun = 9;

            ApplySheetTitle(ws, 1, 1, sonSutun, "K\u00DCBAJ CETVEL\u0130");
            ws.SheetView.FreezeRows(1);

            int satir = 3;
            double genelToplam = 0;

            foreach (var ozet in rapor.KubajSonucu.MalzemeOzetleri)
            {
                // Malzeme section basligi
                string baslik = ozet.MalzemeAdi + "  \u2502  " + KategoriMetni(ozet.Kategori) + "  \u2502  Toplam: " + ozet.ToplamHacim.ToString("#,##0.000") + " m\u00B3";
                ApplySectionTitle(ws, satir, 1, sonSutun, baslik);
                satir++;

                // Tablo basliklari
                ws.Cell(satir, 1).Value = "No";
                ws.Cell(satir, 2).Value = "\u0130st. Ba\u015Fl.";
                ws.Cell(satir, 3).Value = "\u0130st. Biti\u015F";
                ws.Cell(satir, 4).Value = "Alan\u2081 (m\u00B2)";
                ws.Cell(satir, 5).Value = "Alan\u2082 (m\u00B2)";
                ws.Cell(satir, 6).Value = "Mesafe (m)";
                ws.Cell(satir, 7).Value = "Tatbik M. (m)";
                ws.Cell(satir, 8).Value = "Hacim (m\u00B3)";
                ws.Cell(satir, 9).Value = "K\u00FCm. Hacim (m\u00B3)";
                ApplySubHeaderStyle(ws.Range(satir, 1, satir, sonSutun));
                satir++;

                int veriBaslangic = satir;
                double kumulatif = 0;

                for (int i = 0; i < ozet.Segmentler.Count; i++)
                {
                    var seg = ozet.Segmentler[i];
                    kumulatif += seg.Hacim;

                    ws.Cell(satir, 1).Value = i + 1;
                    ws.Cell(satir, 2).Value = IstasyonKisaFormat(seg.Istasyon1);
                    ws.Cell(satir, 3).Value = IstasyonKisaFormat(seg.Istasyon2);
                    ws.Cell(satir, 4).Value = seg.Alan1;
                    ws.Cell(satir, 4).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(satir, 5).Value = seg.Alan2;
                    ws.Cell(satir, 5).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(satir, 6).Value = seg.Mesafe;
                    ws.Cell(satir, 6).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(satir, 7).Value = seg.TatbikMesafesi;
                    ws.Cell(satir, 7).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(satir, 8).Value = seg.Hacim;
                    ws.Cell(satir, 8).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(satir, 9).Value = kumulatif;
                    ws.Cell(satir, 9).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(satir, 9).Style.Font.FontColor = XLColor.FromArgb(75, 85, 99);
                    satir++;
                }

                int veriBitis = satir - 1;
                if (veriBitis >= veriBaslangic)
                    ApplyAltRowShading(ws, veriBaslangic, veriBitis, 1, sonSutun);

                // Ara toplam
                ws.Cell(satir, 1).Value = "TOPLAM";
                ws.Cell(satir, 8).Value = ozet.ToplamHacim;
                ws.Cell(satir, 8).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 9).Value = kumulatif;
                ws.Cell(satir, 9).Style.NumberFormat.Format = "#,##0.000";
                ApplySubtotalRow(ws.Range(satir, 1, satir, sonSutun));

                // Section border
                ApplyDataBorders(ws.Range(veriBaslangic - 1, 1, satir, sonSutun));

                genelToplam += ozet.ToplamHacim;
                satir += 2;
            }

            // ── Genel Toplam ──
            ws.Cell(satir, 1).Value = "GENEL TOPLAM";
            ws.Cell(satir, 8).Value = genelToplam;
            ws.Cell(satir, 8).Style.NumberFormat.Format = "#,##0.000";
            ApplyGrandTotalRow(ws.Range(satir, 1, satir, sonSutun));
            satir += 2;

            // ── Kategori Bazlı Toplam Tablosu ──
            ApplySectionTitle(ws, satir, 1, sonSutun, "KATEGOR\u0130 BAZLI TOPLAM");
            satir++;

            ws.Cell(satir, 1).Value = "Kategori";
            ws.Cell(satir, 2).Value = "Malzeme Say\u0131s\u0131";
            ws.Cell(satir, 3).Value = "Toplam Hacim (m\u00B3)";
            ApplySubHeaderStyle(ws.Range(satir, 1, satir, 3));
            satir++;

            int katDataStart = satir;
            var katGruplar = rapor.KubajSonucu.MalzemeOzetleri
                .GroupBy(m => m.Kategori)
                .OrderBy(g => KategoriSirasi(g.Key));

            foreach (var g in katGruplar)
            {
                ws.Cell(satir, 1).Value = KategoriMetni(g.Key);
                ws.Cell(satir, 1).Style.Border.LeftBorder = XLBorderStyleValues.Thick;
                ws.Cell(satir, 1).Style.Border.LeftBorderColor = KategoriRengi(g.Key);
                ws.Cell(satir, 2).Value = g.Count();
                ws.Cell(satir, 3).Value = g.Sum(m => m.ToplamHacim);
                ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.000";
                satir++;
            }
            int katDataEnd = satir - 1;

            if (katDataEnd >= katDataStart)
            {
                ApplyAltRowShading(ws, katDataStart, katDataEnd, 1, 3);
                ApplyDataBorders(ws.Range(katDataStart - 1, 1, katDataEnd, 3));
            }

            FinalizeSheet(ws);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  4) BRÜCKNER CETVELİ SAYFASI
        // ═══════════════════════════════════════════════════════════════════

        private void BrucknerCetveliSayfasi(XLWorkbook workbook, YolMetrajRaporu rapor)
        {
            var ws = workbook.Worksheets.Add("Br\u00FCckner Cetveli");
            int sonSutun = 9;

            // Baslik
            ApplySheetTitle(ws, 1, 1, sonSutun, "BR\u00DCCKNER CETVEL\u0130 \u2014 KAZI / DOLGU K\u00DCTLE DENGES\u0130");

            // 2 satirlik header
            // Satir 3: ust basliklar (merged)
            ws.Cell(3, 1).Value = "Kilometre";
            ws.Range(3, 1, 4, 1).Merge();
            ws.Cell(3, 2).Value = "Ara Uzak.\n(m)";
            ws.Range(3, 2, 4, 2).Merge();

            ws.Cell(3, 3).Value = "ALAN (m\u00B2)";
            ws.Range(3, 3, 3, 4).Merge();
            ws.Cell(4, 3).Value = "KAZI";
            ws.Cell(4, 4).Value = "DOLGU";

            ws.Cell(3, 5).Value = "HAC\u0130M (m\u00B3)";
            ws.Range(3, 5, 3, 6).Merge();
            ws.Cell(4, 5).Value = "KAZI";
            ws.Cell(4, 6).Value = "DOLGU";

            ws.Cell(3, 7).Value = "K\u00DCMLAT\u0130F HAC\u0130M (m\u00B3)";
            ws.Range(3, 7, 3, 8).Merge();
            ws.Cell(4, 7).Value = "KAZI";
            ws.Cell(4, 8).Value = "DOLGU";

            ws.Cell(3, 9).Value = "BR\u00DCCKNER\nDE\u011EER\u0130 (m\u00B3)";
            ws.Range(3, 9, 4, 9).Merge();

            // Header stil
            ApplyHeaderStyle(ws.Range(3, 1, 4, sonSutun));
            ws.SheetView.FreezeRows(4);

            // Veri
            var sirali = rapor.Kesitler.OrderBy(k => k.Istasyon).ToList();
            int satir = 5;
            double cumKazi = 0;
            double cumDolgu = 0;

            // Ilk istasyon satiri
            var ilk = sirali[0];
            ws.Cell(satir, 1).Value = ilk.IstasyonMetni ?? YolKesitService.IstasyonFormatla(ilk.Istasyon);
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 3).Value = ilk.ToplamKaziAlani;
            ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 4).Value = ilk.ToplamDolguAlani;
            ws.Cell(satir, 4).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 7).Value = 0;
            ws.Cell(satir, 7).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 8).Value = 0;
            ws.Cell(satir, 8).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 9).Value = 0;
            ws.Cell(satir, 9).Style.NumberFormat.Format = "+#,##0.000;\u2212#,##0.000;0.000";
            satir++;

            for (int i = 0; i < sirali.Count - 1; i++)
            {
                var k1 = sirali[i];
                var k2 = sirali[i + 1];
                double mesafe = k2.Istasyon - k1.Istasyon;
                if (mesafe <= 0) continue;

                // Hacim hesabi (ayni metod ile)
                var metot = rapor.KubajSonucu?.Metot ?? HacimMetodu.OrtalamaAlan;
                double kaziHacim = HacimFormulleri.Hesapla(k1.ToplamKaziAlani, k2.ToplamKaziAlani, mesafe, metot);
                double dolguHacim = HacimFormulleri.Hesapla(k1.ToplamDolguAlani, k2.ToplamDolguAlani, mesafe, metot);

                cumKazi += kaziHacim;
                cumDolgu += dolguHacim;
                double bruckner = cumKazi - cumDolgu;

                // Ara mesafe satiri (interval row)
                ws.Cell(satir, 2).Value = mesafe;
                ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(satir, 5).Value = kaziHacim;
                ws.Cell(satir, 5).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 6).Value = dolguHacim;
                ws.Cell(satir, 6).Style.NumberFormat.Format = "#,##0.000";
                ws.Range(satir, 1, satir, sonSutun).Style.Fill.BackgroundColor = AltRowColor;
                ws.Range(satir, 1, satir, sonSutun).Style.Font.FontColor = XLColor.FromArgb(107, 114, 128);
                satir++;

                // Istasyon satiri
                ws.Cell(satir, 1).Value = k2.IstasyonMetni ?? YolKesitService.IstasyonFormatla(k2.Istasyon);
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 3).Value = k2.ToplamKaziAlani;
                ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 4).Value = k2.ToplamDolguAlani;
                ws.Cell(satir, 4).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 7).Value = cumKazi;
                ws.Cell(satir, 7).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 8).Value = cumDolgu;
                ws.Cell(satir, 8).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 9).Value = bruckner;
                ws.Cell(satir, 9).Style.NumberFormat.Format = "+#,##0.000;\u2212#,##0.000;0.000";

                // Bruckner degerine gore renk
                if (bruckner > 0)
                    ws.Cell(satir, 9).Style.Font.FontColor = SuccessColor;
                else if (bruckner < 0)
                    ws.Cell(satir, 9).Style.Font.FontColor = ErrorColor;

                ws.Cell(satir, 9).Style.Font.Bold = true;

                // Sifir gecisi vurgula (isaret degisimi)
                if (i > 0)
                {
                    double oncekiBr = cumKazi - kaziHacim - (cumDolgu - dolguHacim);
                    if ((oncekiBr >= 0 && bruckner < 0) || (oncekiBr <= 0 && bruckner > 0))
                    {
                        ws.Range(satir, 1, satir, sonSutun).Style.Border.TopBorder = XLBorderStyleValues.Medium;
                        ws.Range(satir, 1, satir, sonSutun).Style.Border.TopBorderColor = WarningColor;
                    }
                }

                // Ince alt border
                ws.Range(satir, 1, satir, sonSutun).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                ws.Range(satir, 1, satir, sonSutun).Style.Border.BottomBorderColor = XLColor.FromArgb(209, 213, 219);
                satir++;
            }

            // ── Toplam Satiri ──
            int toplamRow = satir;
            ws.Cell(toplamRow, 1).Value = "TOPLAM";

            // Toplam hacimleri hesapla
            double topKaziHacim = cumKazi;
            double topDolguHacim = cumDolgu;

            ws.Cell(toplamRow, 5).Value = topKaziHacim;
            ws.Cell(toplamRow, 5).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(toplamRow, 6).Value = topDolguHacim;
            ws.Cell(toplamRow, 6).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(toplamRow, 7).Value = cumKazi;
            ws.Cell(toplamRow, 7).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(toplamRow, 8).Value = cumDolgu;
            ws.Cell(toplamRow, 8).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(toplamRow, 9).Value = cumKazi - cumDolgu;
            ws.Cell(toplamRow, 9).Style.NumberFormat.Format = "+#,##0.000;\u2212#,##0.000;0.000";
            ApplyGrandTotalRow(ws.Range(toplamRow, 1, toplamRow, sonSutun));

            // Bruckner toplam rengi
            if (cumKazi - cumDolgu > 0)
                ws.Cell(toplamRow, 9).Style.Font.FontColor = SuccessColor;
            else if (cumKazi - cumDolgu < 0)
                ws.Cell(toplamRow, 9).Style.Font.FontColor = ErrorColor;

            // Dis border
            ApplyDataBorders(ws.Range(3, 1, toplamRow, sonSutun));

            FinalizeSheet(ws);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Yardımcı Metotlar
        // ═══════════════════════════════════════════════════════════════════

        private static string IstasyonKisaFormat(double istasyon)
        {
            return YolKesitService.IstasyonFormatla(istasyon);
        }

        private static string KategoriMetni(MalzemeKategorisi kategori)
        {
            switch (kategori)
            {
                case MalzemeKategorisi.Ustyapi: return "\u00DCstyap\u0131";
                case MalzemeKategorisi.Alttemel: return "Alttemel";
                case MalzemeKategorisi.ToprakIsleri: return "Toprak \u0130\u015Fleri";
                case MalzemeKategorisi.Ozel: return "\u00D6zel";
                default: return kategori.ToString();
            }
        }

        private static int KategoriSirasi(MalzemeKategorisi kategori)
        {
            switch (kategori)
            {
                case MalzemeKategorisi.ToprakIsleri: return 0;
                case MalzemeKategorisi.Ustyapi: return 1;
                case MalzemeKategorisi.Alttemel: return 2;
                case MalzemeKategorisi.Ozel: return 3;
                default: return 4;
            }
        }

        private static XLColor KategoriRengi(MalzemeKategorisi kategori)
        {
            switch (kategori)
            {
                case MalzemeKategorisi.ToprakIsleri: return XLColor.FromArgb(180, 130, 70);
                case MalzemeKategorisi.Ustyapi: return XLColor.FromArgb(55, 65, 81);
                case MalzemeKategorisi.Alttemel: return XLColor.FromArgb(107, 114, 128);
                case MalzemeKategorisi.Ozel: return XLColor.FromArgb(202, 138, 4);
                default: return XLColor.FromArgb(156, 163, 175);
            }
        }
    }
}
