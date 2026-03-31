using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
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
                    var wsAlan = wb.Worksheets.Add("Alan Hesapları");
                    AlanSayfasiOlustur(wsAlan, kesitler);

                    var wsKiyas = wb.Worksheets.Add("Tablo Kıyası");
                    KiyasSayfasiOlustur(wsKiyas, kesitler);

                    wb.SaveAs(dosyaYolu);
                }

                return new ExportResult { Basarili = true, DosyaYolu = dosyaYolu };
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Enkesit okuma Excel export hatası", ex);
                return new ExportResult { Basarili = false, HataMesaji = ex.Message };
            }
        }

        private void AlanSayfasiOlustur(IXLWorksheet ws, List<KesitGrubu> kesitler)
        {
            var malzemeler = kesitler
                .Where(k => k.HesaplananAlanlar != null)
                .SelectMany(k => k.HesaplananAlanlar.Select(a => a.MalzemeAdi))
                .Distinct().ToList();

            ws.Cell(1, 1).Value = "İstasyon";
            ws.Cell(1, 2).Value = "Durum";
            for (int i = 0; i < malzemeler.Count; i++)
                ws.Cell(1, 3 + i).Value = malzemeler[i];

            ws.Row(1).Style.Font.Bold = true;

            int satir = 2;
            foreach (var kesit in kesitler.OrderBy(k => k.Anchor?.Istasyon ?? 0))
            {
                ws.Cell(satir, 1).Value = kesit.Anchor != null
                    ? YolKesitService.IstasyonFormatla(kesit.Anchor.Istasyon) : "";
                ws.Cell(satir, 2).Value = kesit.Durum.ToString();

                if (kesit.HesaplananAlanlar != null)
                {
                    for (int i = 0; i < malzemeler.Count; i++)
                    {
                        var alan = kesit.HesaplananAlanlar.FirstOrDefault(a => a.MalzemeAdi == malzemeler[i]);
                        if (alan == null) continue;

                        // Karar verilmisse kabul edilen degeri kullan
                        var kiyas = kesit.TabloKiyaslari?.FirstOrDefault(k => k.MalzemeAdi == malzemeler[i]);
                        double deger = (kiyas != null && kiyas.KabulEdilenAlan > 0)
                            ? kiyas.KabulEdilenAlan
                            : alan.Alan;
                        ws.Cell(satir, 3 + i).Value = deger;
                    }
                }
                satir++;
            }

            ws.Columns().AdjustToContents();
        }

        private void KiyasSayfasiOlustur(IXLWorksheet ws, List<KesitGrubu> kesitler)
        {
            ws.Cell(1, 1).Value = "Istasyon";
            ws.Cell(1, 2).Value = "Malzeme";
            ws.Cell(1, 3).Value = "Hesaplanan";
            ws.Cell(1, 4).Value = "Tablo";
            ws.Cell(1, 5).Value = "Fark";
            ws.Cell(1, 6).Value = "Fark %";
            ws.Cell(1, 7).Value = "Uyumlu";
            ws.Cell(1, 8).Value = "Karar";
            ws.Cell(1, 9).Value = "Kabul Edilen";
            ws.Row(1).Style.Font.Bold = true;

            int satir = 2;
            foreach (var kesit in kesitler.OrderBy(k => k.Anchor?.Istasyon ?? 0))
            {
                if (kesit.TabloKiyaslari == null) continue;
                foreach (var kiyas in kesit.TabloKiyaslari)
                {
                    ws.Cell(satir, 1).Value = kesit.Anchor != null
                        ? YolKesitService.IstasyonFormatla(kesit.Anchor.Istasyon) : "";
                    ws.Cell(satir, 2).Value = kiyas.MalzemeAdi;
                    ws.Cell(satir, 3).Value = kiyas.HesaplananAlan;
                    ws.Cell(satir, 4).Value = kiyas.TabloAlani;
                    ws.Cell(satir, 5).Value = kiyas.Fark;
                    ws.Cell(satir, 6).Value = kiyas.FarkYuzde;
                    ws.Cell(satir, 7).Value = kiyas.Uyumlu ? "Evet" : "Hayir";
                    ws.Cell(satir, 8).Value = KararMetni(kiyas.Karar);
                    ws.Cell(satir, 9).Value = kiyas.KabulEdilenAlan;

                    if (!kiyas.Uyumlu && kiyas.Karar == KararDurumu.Bekliyor)
                        ws.Row(satir).Style.Font.FontColor = XLColor.Red;

                    satir++;
                }
            }

            ws.Columns().AdjustToContents();
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
    }
}
