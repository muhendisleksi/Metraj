using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using Metraj.Models;
using Metraj.Models.YolEnkesit;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public partial class ExcelExportService
    {
        public ExportResult EnkesitOkumaExport(List<KesitGrubu> kesitler, string dosyaYolu)
        {
            try
            {
                using (var wb = new XLWorkbook())
                {
                    var sirali = kesitler.Where(k => k.Anchor != null).OrderBy(k => k.Anchor.Istasyon).ToList();
                    var malzemeler = MalzemeListesiOlustur(sirali);
                    bool kubajVar = sirali.Count >= 2 && malzemeler.Count > 0;

                    if (kubajVar)
                        EnkesitOzetSayfasi(wb, sirali, malzemeler);

                    var wsAlan = wb.Worksheets.Add("Alan Hesaplar\u0131");
                    AlanSayfasiOlustur(wsAlan, kesitler);

                    var wsKiyas = wb.Worksheets.Add("Tablo K\u0131yas\u0131");
                    KiyasSayfasiOlustur(wsKiyas, kesitler);

                    if (kubajVar)
                    {
                        EnkesitKubajCetveliSayfasi(wb, sirali, malzemeler);
                        EnkesitBrucknerSayfasi(wb, sirali);
                    }

                    wb.SaveAs(dosyaYolu);
                }

                return new ExportResult { Basarili = true, DosyaYolu = dosyaYolu };
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Enkesit okuma Excel export hatas\u0131", ex);
                return new ExportResult { Basarili = false, HataMesaji = ex.Message };
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ENKESİT ÖZET SAYFASI (m³ hesaplı)
        // ═══════════════════════════════════════════════════════════════════

        private void EnkesitOzetSayfasi(XLWorkbook wb, List<KesitGrubu> sirali, List<string> malzemeler)
        {
            var ws = wb.Worksheets.Add("\u00D6zet");
            ApplySheetTitle(ws, 1, 1, 4, "ENKES\u0130T OKUMA \u2014 HAC\u0130M \u00D6ZET\u0130");

            int satir = 3;
            ApplySectionTitle(ws, satir, 1, 4, "G\u00DCNCEL B\u0130LG\u0130LER");
            satir++;

            WriteParamRow(ws, ref satir, "Kesit Say\u0131s\u0131:", sirali.Count.ToString());
            double toplamUzunluk = sirali.Last().Anchor.Istasyon - sirali.First().Anchor.Istasyon;
            WriteParamRow(ws, ref satir, "Toplam G\u00FCzergah:", toplamUzunluk, "m", "#,##0.00");
            WriteParamRow(ws, ref satir, "Ba\u015Flang\u0131\u00E7:", YolKesitService.IstasyonFormatla(sirali.First().Anchor.Istasyon));
            WriteParamRow(ws, ref satir, "Biti\u015F:", YolKesitService.IstasyonFormatla(sirali.Last().Anchor.Istasyon));
            WriteParamRow(ws, ref satir, "Hesap Metodu:", "Ortalama Alan");
            ApplyDataBorders(ws.Range(4, 1, satir - 1, 2));
            satir++;

            // ── Malzeme Hacim Tablosu ──
            ApplySectionTitle(ws, satir, 1, 4, "MALZEME HAC\u0130MLER\u0130 (m\u00B3)");
            satir++;

            ws.Cell(satir, 1).Value = "Malzeme";
            ws.Cell(satir, 2).Value = "Toplam Hacim (m\u00B3)";
            ws.Cell(satir, 3).Value = "Oran (%)";
            ApplyHeaderStyle(ws.Range(satir, 1, satir, 3));
            satir++;

            // Hacimleri hesapla
            var malzemeHacimleri = new Dictionary<string, double>();
            double genelToplam = 0;

            foreach (var m in malzemeler)
            {
                double topHacim = 0;
                for (int i = 0; i < sirali.Count - 1; i++)
                {
                    double a1 = KesitMalzemeAlani(sirali[i], m);
                    double a2 = KesitMalzemeAlani(sirali[i + 1], m);
                    double mesafe = sirali[i + 1].Anchor.Istasyon - sirali[i].Anchor.Istasyon;
                    if (mesafe > 0)
                        topHacim += HacimFormulleri.Hesapla(a1, a2, mesafe, HacimMetodu.OrtalamaAlan);
                }
                malzemeHacimleri[m] = topHacim;
                genelToplam += topHacim;
            }

            int malzBaslangic = satir;
            foreach (var m in malzemeler.OrderByDescending(m => malzemeHacimleri[m]))
            {
                ws.Cell(satir, 1).Value = m;
                ws.Cell(satir, 2).Value = malzemeHacimleri[m];
                ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 3).Value = genelToplam > 0 ? (malzemeHacimleri[m] / genelToplam * 100) : 0;
                ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.0";
                satir++;
            }

            // Genel toplam
            ws.Cell(satir, 1).Value = "GENEL TOPLAM";
            ws.Cell(satir, 2).Value = genelToplam;
            ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.000";
            ApplyGrandTotalRow(ws.Range(satir, 1, satir, 3));

            ApplyAltRowShading(ws, malzBaslangic, satir - 1, 1, 3);
            ApplyDataBorders(ws.Range(malzBaslangic - 1, 1, satir, 3));
            satir += 2;

            // ── Kazı-Dolgu Dengesi ──
            double kaziHacmi = malzemeHacimleri.Where(kv => KaziMalzemesiMi(kv.Key)).Sum(kv => kv.Value);
            double dolguHacmi = malzemeHacimleri.Where(kv => DolguMalzemesiMi(kv.Key)).Sum(kv => kv.Value);
            double netHacim = kaziHacmi - dolguHacmi;

            if (kaziHacmi > 0 || dolguHacmi > 0)
            {
                ApplySectionTitle(ws, satir, 1, 4, "KAZI-DOLGU DENGES\u0130");
                satir++;

                int dengeBaslangic = satir;
                ws.Cell(satir, 1).Value = "Toplam Kaz\u0131 (Yarma)";
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 2).Value = kaziHacmi;
                ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 2).Style.Font.Bold = true;
                ws.Cell(satir, 3).Value = "m\u00B3";
                ws.Cell(satir, 3).Style.Font.FontColor = XLColor.FromArgb(155, 168, 182);
                ws.Range(satir, 1, satir, 3).Style.Fill.BackgroundColor = KaziBgColor;
                satir++;

                ws.Cell(satir, 1).Value = "Toplam Dolgu";
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 2).Value = dolguHacmi;
                ws.Cell(satir, 2).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 2).Style.Font.Bold = true;
                ws.Cell(satir, 3).Value = "m\u00B3";
                ws.Cell(satir, 3).Style.Font.FontColor = XLColor.FromArgb(155, 168, 182);
                ws.Range(satir, 1, satir, 3).Style.Fill.BackgroundColor = DolguBgColor;
                satir++;

                ws.Cell(satir, 1).Value = "Net (Kaz\u0131 \u2212 Dolgu)";
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 2).Value = netHacim;
                ws.Cell(satir, 2).Style.NumberFormat.Format = "+#,##0.000;\u2212#,##0.000;0.000";
                ws.Cell(satir, 3).Value = "m\u00B3";
                ws.Cell(satir, 3).Style.Font.FontColor = XLColor.FromArgb(155, 168, 182);
                ApplyGrandTotalRow(ws.Range(satir, 1, satir, 3));
                if (netHacim > 0) ws.Cell(satir, 2).Style.Font.FontColor = SuccessColor;
                else if (netHacim < 0) ws.Cell(satir, 2).Style.Font.FontColor = ErrorColor;

                ApplyDataBorders(ws.Range(dengeBaslangic, 1, satir, 3));
            }

            ws.Column(1).Width = 28;
            ws.Column(2).Width = 22;
            ws.Column(3).Width = 12;
            ws.Column(4).Width = 12;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ENKESİT KÜBAJ CETVELİ (malzeme bazlı m³ detay)
        // ═══════════════════════════════════════════════════════════════════

        private void EnkesitKubajCetveliSayfasi(XLWorkbook wb, List<KesitGrubu> sirali, List<string> malzemeler)
        {
            var ws = wb.Worksheets.Add("K\u00FCbaj Cetveli");
            int sonSutun = 7;

            ApplySheetTitle(ws, 1, 1, sonSutun, "K\u00DCBAJ CETVEL\u0130 \u2014 MALZEME BAZLI HAC\u0130M DETAYI");
            ws.SheetView.FreezeRows(1);

            int satir = 3;
            double genelToplam = 0;

            foreach (var malzeme in malzemeler)
            {
                // Malzeme hacimleri hesapla
                double malzToplam = 0;
                var segmentler = new List<(string ist1, string ist2, double a1, double a2, double mesafe, double hacim)>();

                for (int i = 0; i < sirali.Count - 1; i++)
                {
                    double a1 = KesitMalzemeAlani(sirali[i], malzeme);
                    double a2 = KesitMalzemeAlani(sirali[i + 1], malzeme);
                    double mesafe = sirali[i + 1].Anchor.Istasyon - sirali[i].Anchor.Istasyon;
                    if (mesafe <= 0) continue;

                    double hacim = HacimFormulleri.Hesapla(a1, a2, mesafe, HacimMetodu.OrtalamaAlan);
                    malzToplam += hacim;

                    string ist1 = YolKesitService.IstasyonFormatla(sirali[i].Anchor.Istasyon);
                    string ist2 = YolKesitService.IstasyonFormatla(sirali[i + 1].Anchor.Istasyon);
                    segmentler.Add((ist1, ist2, a1, a2, mesafe, hacim));
                }

                if (segmentler.Count == 0) continue;

                // Section basligi
                string baslik = malzeme + "  \u2502  Toplam: " + malzToplam.ToString("#,##0.000") + " m\u00B3";
                ApplySectionTitle(ws, satir, 1, sonSutun, baslik);
                satir++;

                // Tablo basliklari
                ws.Cell(satir, 1).Value = "No";
                ws.Cell(satir, 2).Value = "\u0130st. Ba\u015Fl.";
                ws.Cell(satir, 3).Value = "\u0130st. Biti\u015F";
                ws.Cell(satir, 4).Value = "Alan\u2081 (m\u00B2)";
                ws.Cell(satir, 5).Value = "Alan\u2082 (m\u00B2)";
                ws.Cell(satir, 6).Value = "Mesafe (m)";
                ws.Cell(satir, 7).Value = "Hacim (m\u00B3)";
                ApplySubHeaderStyle(ws.Range(satir, 1, satir, sonSutun));
                satir++;

                int veriBaslangic = satir;
                double kumulatif = 0;

                for (int i = 0; i < segmentler.Count; i++)
                {
                    var seg = segmentler[i];
                    kumulatif += seg.hacim;

                    ws.Cell(satir, 1).Value = i + 1;
                    ws.Cell(satir, 2).Value = seg.ist1;
                    ws.Cell(satir, 3).Value = seg.ist2;
                    ws.Cell(satir, 4).Value = seg.a1;
                    ws.Cell(satir, 4).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(satir, 5).Value = seg.a2;
                    ws.Cell(satir, 5).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(satir, 6).Value = seg.mesafe;
                    ws.Cell(satir, 6).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(satir, 7).Value = seg.hacim;
                    ws.Cell(satir, 7).Style.NumberFormat.Format = "#,##0.000";
                    satir++;
                }

                int veriBitis = satir - 1;
                if (veriBitis >= veriBaslangic)
                    ApplyAltRowShading(ws, veriBaslangic, veriBitis, 1, sonSutun);

                // Ara toplam
                ws.Cell(satir, 1).Value = "TOPLAM";
                ws.Cell(satir, 7).Value = malzToplam;
                ws.Cell(satir, 7).Style.NumberFormat.Format = "#,##0.000";
                ApplySubtotalRow(ws.Range(satir, 1, satir, sonSutun));
                ApplyDataBorders(ws.Range(veriBaslangic - 1, 1, satir, sonSutun));

                genelToplam += malzToplam;
                satir += 2;
            }

            // Genel toplam
            ws.Cell(satir, 1).Value = "T\u00DCM MALZEMELER GENEL TOPLAM";
            ws.Cell(satir, 7).Value = genelToplam;
            ws.Cell(satir, 7).Style.NumberFormat.Format = "#,##0.000";
            ApplyGrandTotalRow(ws.Range(satir, 1, satir, sonSutun));

            FinalizeSheet(ws);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ENKESİT BRÜCKNER CETVELİ (kazı/dolgu kütle dengesi)
        // ═══════════════════════════════════════════════════════════════════

        private void EnkesitBrucknerSayfasi(XLWorkbook wb, List<KesitGrubu> sirali)
        {
            // Kazi ve dolgu malzemesi var mi kontrol et
            bool kaziVar = false, dolguVar = false;
            foreach (var k in sirali)
            {
                if (KesitKaziAlani(k) > 0) kaziVar = true;
                if (KesitDolguAlani(k) > 0) dolguVar = true;
                if (kaziVar || dolguVar) break;
            }
            if (!kaziVar && !dolguVar) return;

            var ws = wb.Worksheets.Add("Br\u00FCckner Cetveli");
            int sonSutun = 9;

            ApplySheetTitle(ws, 1, 1, sonSutun, "BR\u00DCCKNER CETVEL\u0130 \u2014 KAZI / DOLGU K\u00DCTLE DENGES\u0130");

            // 2 satirlik header
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

            ApplyHeaderStyle(ws.Range(3, 1, 4, sonSutun));
            ws.SheetView.FreezeRows(4);

            // Ilk istasyon
            int satir = 5;
            double cumKazi = 0, cumDolgu = 0;
            var ilk = sirali[0];

            ws.Cell(satir, 1).Value = YolKesitService.IstasyonFormatla(ilk.Anchor.Istasyon);
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 3).Value = KesitKaziAlani(ilk);
            ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 4).Value = KesitDolguAlani(ilk);
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
                double mesafe = k2.Anchor.Istasyon - k1.Anchor.Istasyon;
                if (mesafe <= 0) continue;

                double kaziA1 = KesitKaziAlani(k1);
                double kaziA2 = KesitKaziAlani(k2);
                double dolguA1 = KesitDolguAlani(k1);
                double dolguA2 = KesitDolguAlani(k2);

                double kaziHacim = HacimFormulleri.Hesapla(kaziA1, kaziA2, mesafe, HacimMetodu.OrtalamaAlan);
                double dolguHacim = HacimFormulleri.Hesapla(dolguA1, dolguA2, mesafe, HacimMetodu.OrtalamaAlan);

                cumKazi += kaziHacim;
                cumDolgu += dolguHacim;
                double bruckner = cumKazi - cumDolgu;

                // Ara mesafe satiri
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
                ws.Cell(satir, 1).Value = YolKesitService.IstasyonFormatla(k2.Anchor.Istasyon);
                ws.Cell(satir, 1).Style.Font.Bold = true;
                ws.Cell(satir, 3).Value = kaziA2;
                ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 4).Value = dolguA2;
                ws.Cell(satir, 4).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 7).Value = cumKazi;
                ws.Cell(satir, 7).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 8).Value = cumDolgu;
                ws.Cell(satir, 8).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(satir, 9).Value = bruckner;
                ws.Cell(satir, 9).Style.NumberFormat.Format = "+#,##0.000;\u2212#,##0.000;0.000";

                if (bruckner > 0) ws.Cell(satir, 9).Style.Font.FontColor = SuccessColor;
                else if (bruckner < 0) ws.Cell(satir, 9).Style.Font.FontColor = ErrorColor;
                ws.Cell(satir, 9).Style.Font.Bold = true;

                // Sifir gecisi
                if (i > 0)
                {
                    double oncekiBr = (cumKazi - kaziHacim) - (cumDolgu - dolguHacim);
                    if ((oncekiBr > 0 && bruckner < 0) || (oncekiBr < 0 && bruckner > 0))
                    {
                        ws.Range(satir, 1, satir, sonSutun).Style.Border.TopBorder = XLBorderStyleValues.Medium;
                        ws.Range(satir, 1, satir, sonSutun).Style.Border.TopBorderColor = WarningColor;
                    }
                }

                ws.Range(satir, 1, satir, sonSutun).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                ws.Range(satir, 1, satir, sonSutun).Style.Border.BottomBorderColor = XLColor.FromArgb(209, 213, 219);
                satir++;
            }

            // Toplam
            ws.Cell(satir, 1).Value = "TOPLAM";
            ws.Cell(satir, 5).Value = cumKazi;
            ws.Cell(satir, 5).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 6).Value = cumDolgu;
            ws.Cell(satir, 6).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 7).Value = cumKazi;
            ws.Cell(satir, 7).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 8).Value = cumDolgu;
            ws.Cell(satir, 8).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(satir, 9).Value = cumKazi - cumDolgu;
            ws.Cell(satir, 9).Style.NumberFormat.Format = "+#,##0.000;\u2212#,##0.000;0.000";
            ApplyGrandTotalRow(ws.Range(satir, 1, satir, sonSutun));

            if (cumKazi - cumDolgu > 0) ws.Cell(satir, 9).Style.Font.FontColor = SuccessColor;
            else if (cumKazi - cumDolgu < 0) ws.Cell(satir, 9).Style.Font.FontColor = ErrorColor;

            ApplyDataBorders(ws.Range(3, 1, satir, sonSutun));
            FinalizeSheet(ws);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ALAN HESAPLARI SAYFASI (mevcut)
        // ═══════════════════════════════════════════════════════════════════

        private void AlanSayfasiOlustur(IXLWorksheet ws, List<KesitGrubu> kesitler)
        {
            var malzemeler = kesitler
                .Where(k => k.HesaplananAlanlar != null)
                .SelectMany(k => k.HesaplananAlanlar.Select(a => a.MalzemeAdi))
                .Distinct().ToList();

            int sonSutun = 2 + malzemeler.Count;
            if (sonSutun < 2) sonSutun = 2;

            ApplySheetTitle(ws, 1, 1, sonSutun, "ENKES\u0130T ALAN HESAPLARI");

            ws.Cell(2, 1).Value = "\u0130stasyon";
            ws.Cell(2, 2).Value = "Durum";
            for (int i = 0; i < malzemeler.Count; i++)
                ws.Cell(2, 3 + i).Value = malzemeler[i] + " (m\u00B2)";

            ApplyHeaderStyle(ws.Range(2, 1, 2, sonSutun));
            ws.SheetView.FreezeRows(2);

            int dataStartRow = 3;
            int satir = dataStartRow;
            foreach (var kesit in kesitler.OrderBy(k => k.Anchor?.Istasyon ?? 0))
            {
                ws.Cell(satir, 1).Value = kesit.Anchor != null
                    ? YolKesitService.IstasyonFormatla(kesit.Anchor.Istasyon) : "";

                ws.Cell(satir, 2).Value = kesit.Durum.ToString();

                var durumCell = ws.Cell(satir, 2);
                switch (kesit.Durum)
                {
                    case DogrulamaDurumu.Bekliyor:
                        durumCell.Style.Fill.BackgroundColor = WarningBgColor;
                        durumCell.Style.Font.FontColor = WarningColor;
                        break;
                    case DogrulamaDurumu.Onaylandi:
                        durumCell.Style.Fill.BackgroundColor = SuccessBgColor;
                        durumCell.Style.Font.FontColor = SuccessColor;
                        break;
                    case DogrulamaDurumu.Duzeltildi:
                        durumCell.Style.Fill.BackgroundColor = XLColor.FromArgb(219, 234, 254);
                        durumCell.Style.Font.FontColor = XLColor.FromArgb(37, 99, 235);
                        break;
                    case DogrulamaDurumu.Sorunlu:
                        durumCell.Style.Fill.BackgroundColor = ErrorBgColor;
                        durumCell.Style.Font.FontColor = ErrorColor;
                        break;
                }

                if (kesit.HesaplananAlanlar != null)
                {
                    for (int i = 0; i < malzemeler.Count; i++)
                    {
                        var alan = kesit.HesaplananAlanlar.FirstOrDefault(a => a.MalzemeAdi == malzemeler[i]);
                        if (alan == null) continue;

                        var kiyas = kesit.TabloKiyaslari?.FirstOrDefault(k => k.MalzemeAdi == malzemeler[i]);
                        double deger = (kiyas != null && kiyas.KabulEdilenAlan > 0)
                            ? kiyas.KabulEdilenAlan
                            : alan.Alan;
                        ws.Cell(satir, 3 + i).Value = deger;
                        ws.Cell(satir, 3 + i).Style.NumberFormat.Format = "#,##0.00";
                    }
                }
                satir++;
            }

            int dataEndRow = satir - 1;

            if (dataEndRow >= dataStartRow)
            {
                ApplyAltRowShading(ws, dataStartRow, dataEndRow, 1, sonSutun);

                int toplamRow = dataEndRow + 1;
                ws.Cell(toplamRow, 1).Value = "TOPLAM";
                for (int c = 3; c <= sonSutun; c++)
                {
                    double toplam = 0;
                    for (int r = dataStartRow; r <= dataEndRow; r++)
                    {
                        var val = ws.Cell(r, c).Value;
                        if (val.IsNumber)
                            toplam += val.GetNumber();
                    }
                    ws.Cell(toplamRow, c).Value = toplam;
                    ws.Cell(toplamRow, c).Style.NumberFormat.Format = "#,##0.00";
                }
                ApplyGrandTotalRow(ws.Range(toplamRow, 1, toplamRow, sonSutun));
                ApplyDataBorders(ws.Range(2, 1, toplamRow, sonSutun));
            }

            FinalizeSheet(ws);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  TABLO KIYASI SAYFASI (mevcut)
        // ═══════════════════════════════════════════════════════════════════

        private void KiyasSayfasiOlustur(IXLWorksheet ws, List<KesitGrubu> kesitler)
        {
            int sonSutun = 9;

            ApplySheetTitle(ws, 1, 1, sonSutun, "TABLO KIYASI RAPORU");

            ws.Cell(2, 1).Value = "\u0130stasyon";
            ws.Cell(2, 2).Value = "Malzeme";
            ws.Cell(2, 3).Value = "Hesaplanan";
            ws.Cell(2, 4).Value = "Tablo";
            ws.Cell(2, 5).Value = "Fark";
            ws.Cell(2, 6).Value = "Fark %";
            ws.Cell(2, 7).Value = "Uyumlu";
            ws.Cell(2, 8).Value = "Karar";
            ws.Cell(2, 9).Value = "Kabul Edilen";
            ApplyHeaderStyle(ws.Range(2, 1, 2, sonSutun));
            ws.SheetView.FreezeRows(2);

            int dataStartRow = 3;
            int satir = dataStartRow;
            string sonIstasyon = "";

            int uyumluSayisi = 0;
            int uyumsuzKararli = 0;
            int uyumsuzBekliyor = 0;
            int toplamKayit = 0;

            foreach (var kesit in kesitler.OrderBy(k => k.Anchor?.Istasyon ?? 0))
            {
                if (kesit.TabloKiyaslari == null) continue;

                string istasyon = kesit.Anchor != null
                    ? YolKesitService.IstasyonFormatla(kesit.Anchor.Istasyon) : "";

                if (!string.IsNullOrEmpty(sonIstasyon) && istasyon != sonIstasyon && satir > dataStartRow)
                {
                    ws.Range(satir, 1, satir, sonSutun).Style.Border.TopBorder = XLBorderStyleValues.Medium;
                    ws.Range(satir, 1, satir, sonSutun).Style.Border.TopBorderColor = XLColor.FromArgb(156, 163, 175);
                }
                sonIstasyon = istasyon;

                foreach (var kiyas in kesit.TabloKiyaslari)
                {
                    ws.Cell(satir, 1).Value = istasyon;
                    ws.Cell(satir, 2).Value = kiyas.MalzemeAdi;
                    ws.Cell(satir, 3).Value = kiyas.HesaplananAlan;
                    ws.Cell(satir, 3).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(satir, 4).Value = kiyas.TabloAlani;
                    ws.Cell(satir, 4).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(satir, 5).Value = kiyas.Fark;
                    ws.Cell(satir, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(satir, 6).Value = kiyas.FarkYuzde;
                    ws.Cell(satir, 6).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(satir, 9).Value = kiyas.KabulEdilenAlan;
                    ws.Cell(satir, 9).Style.NumberFormat.Format = "#,##0.00";

                    ws.Cell(satir, 7).Value = kiyas.Uyumlu ? "Evet" : "Hay\u0131r";
                    if (kiyas.Uyumlu)
                        ws.Cell(satir, 7).Style.Font.FontColor = SuccessColor;
                    else
                    {
                        ws.Cell(satir, 7).Style.Font.FontColor = ErrorColor;
                        ws.Cell(satir, 7).Style.Font.Bold = true;
                    }

                    string kararText = KararMetni(kiyas.Karar);
                    ws.Cell(satir, 8).Value = kararText;
                    switch (kiyas.Karar)
                    {
                        case KararDurumu.OtomatikOnay:
                            ws.Cell(satir, 8).Style.Font.FontColor = SuccessColor;
                            break;
                        case KararDurumu.TabloKabul:
                        case KararDurumu.HesapKabul:
                            ws.Cell(satir, 8).Style.Font.FontColor = WarningColor;
                            break;
                        case KararDurumu.Bekliyor:
                            ws.Cell(satir, 8).Style.Font.FontColor = ErrorColor;
                            ws.Cell(satir, 8).Style.Font.Italic = true;
                            break;
                    }

                    double farkAbs = Math.Abs(kiyas.FarkYuzde);
                    if (farkAbs <= 2)
                        ws.Cell(satir, 6).Style.Font.FontColor = SuccessColor;
                    else if (farkAbs <= 5)
                        ws.Cell(satir, 6).Style.Font.FontColor = WarningColor;
                    else
                    {
                        ws.Cell(satir, 6).Style.Font.FontColor = ErrorColor;
                        ws.Cell(satir, 6).Style.Font.Bold = true;
                    }

                    if (kiyas.Uyumlu)
                    {
                        ws.Range(satir, 1, satir, sonSutun).Style.Fill.BackgroundColor = SuccessBgColor;
                        uyumluSayisi++;
                    }
                    else if (kiyas.Karar == KararDurumu.TabloKabul || kiyas.Karar == KararDurumu.HesapKabul)
                    {
                        ws.Range(satir, 1, satir, sonSutun).Style.Fill.BackgroundColor = WarningBgColor;
                        uyumsuzKararli++;
                    }
                    else
                    {
                        ws.Range(satir, 1, satir, sonSutun).Style.Fill.BackgroundColor = ErrorBgColor;
                        uyumsuzBekliyor++;
                    }

                    toplamKayit++;
                    satir++;
                }
            }

            int dataEndRow = satir - 1;

            if (dataEndRow >= dataStartRow)
                ApplyDataBorders(ws.Range(2, 1, dataEndRow, sonSutun));

            satir += 1;
            ws.Cell(satir, 1).Value = "\u00D6ZET";
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 1).Style.Font.FontSize = 11;
            satir++;

            ws.Cell(satir, 1).Value = "Toplam Kay\u0131t:";
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 2).Value = toplamKayit;
            ws.Cell(satir, 2).Style.Font.Bold = true;
            satir++;

            ws.Cell(satir, 1).Value = "Uyumlu:";
            ws.Cell(satir, 1).Style.Font.FontColor = SuccessColor;
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 2).Value = uyumluSayisi;
            ws.Cell(satir, 2).Style.Font.FontColor = SuccessColor;
            ws.Cell(satir, 2).Style.Font.Bold = true;
            if (toplamKayit > 0)
            {
                ws.Cell(satir, 3).Value = $"(%{(uyumluSayisi * 100.0 / toplamKayit):F1})";
                ws.Cell(satir, 3).Style.Font.FontColor = SuccessColor;
            }
            satir++;

            ws.Cell(satir, 1).Value = "Uyumsuz - Karar\u0131 Verilmi\u015F:";
            ws.Cell(satir, 1).Style.Font.FontColor = WarningColor;
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 2).Value = uyumsuzKararli;
            ws.Cell(satir, 2).Style.Font.FontColor = WarningColor;
            ws.Cell(satir, 2).Style.Font.Bold = true;
            satir++;

            ws.Cell(satir, 1).Value = "Uyumsuz - Bekliyor:";
            ws.Cell(satir, 1).Style.Font.FontColor = ErrorColor;
            ws.Cell(satir, 1).Style.Font.Bold = true;
            ws.Cell(satir, 2).Value = uyumsuzBekliyor;
            ws.Cell(satir, 2).Style.Font.FontColor = ErrorColor;
            ws.Cell(satir, 2).Style.Font.Bold = true;

            ApplyDataBorders(ws.Range(satir - 4, 1, satir, 3));

            FinalizeSheet(ws);
        }

        private static string KararMetni(KararDurumu karar)
        {
            switch (karar)
            {
                case KararDurumu.TabloKabul: return "Tablo Kabul";
                case KararDurumu.HesapKabul: return "Hesap Kabul";
                case KararDurumu.OtomatikOnay: return "Otomatik Onay";
                default: return "Bekliyor";
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Enkesit Yardımcı Metodlar
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Kesitteki bir malzemenin kabul edilen alanini dondurur.</summary>
        private static double KesitMalzemeAlani(KesitGrubu kesit, string malzemeAdi)
        {
            var kiyas = kesit.TabloKiyaslari?.FirstOrDefault(k =>
                k.MalzemeAdi.Equals(malzemeAdi, StringComparison.OrdinalIgnoreCase));
            if (kiyas != null && kiyas.KabulEdilenAlan > 0)
                return kiyas.KabulEdilenAlan;

            var alan = kesit.HesaplananAlanlar?.FirstOrDefault(a =>
                a.MalzemeAdi.Equals(malzemeAdi, StringComparison.OrdinalIgnoreCase));
            return alan?.Alan ?? 0;
        }

        /// <summary>Kazi (Yarma) malzemelerinin toplam alanini dondurur.</summary>
        private static double KesitKaziAlani(KesitGrubu kesit)
        {
            double toplam = 0;
            if (kesit.TabloKiyaslari != null)
            {
                foreach (var k in kesit.TabloKiyaslari)
                    if (KaziMalzemesiMi(k.MalzemeAdi))
                        toplam += k.KabulEdilenAlan > 0 ? k.KabulEdilenAlan : k.HesaplananAlan;
            }
            else if (kesit.HesaplananAlanlar != null)
            {
                foreach (var a in kesit.HesaplananAlanlar)
                    if (KaziMalzemesiMi(a.MalzemeAdi))
                        toplam += a.Alan;
            }
            return toplam;
        }

        /// <summary>Dolgu malzemelerinin toplam alanini dondurur.</summary>
        private static double KesitDolguAlani(KesitGrubu kesit)
        {
            double toplam = 0;
            if (kesit.TabloKiyaslari != null)
            {
                foreach (var k in kesit.TabloKiyaslari)
                    if (DolguMalzemesiMi(k.MalzemeAdi))
                        toplam += k.KabulEdilenAlan > 0 ? k.KabulEdilenAlan : k.HesaplananAlan;
            }
            else if (kesit.HesaplananAlanlar != null)
            {
                foreach (var a in kesit.HesaplananAlanlar)
                    if (DolguMalzemesiMi(a.MalzemeAdi))
                        toplam += a.Alan;
            }
            return toplam;
        }

        private static bool KaziMalzemesiMi(string malzemeAdi)
        {
            if (string.IsNullOrEmpty(malzemeAdi)) return false;
            var lower = malzemeAdi.ToLowerInvariant();
            return lower.Contains("yarma") || lower.Contains("kaz\u0131") || lower.Contains("kazi");
        }

        private static bool DolguMalzemesiMi(string malzemeAdi)
        {
            if (string.IsNullOrEmpty(malzemeAdi)) return false;
            return malzemeAdi.ToLowerInvariant().Contains("dolgu");
        }

        private static List<string> MalzemeListesiOlustur(List<KesitGrubu> sirali)
        {
            return sirali
                .Where(k => k.HesaplananAlanlar != null)
                .SelectMany(k => k.HesaplananAlanlar.Select(a => a.MalzemeAdi))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
