using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    public class TabloOkumaService : ITabloOkumaService
    {
        private readonly IDocumentContext _documentContext;
        private const double UyumToleransYuzde = 2.0;
        private const double UyariToleransYuzde = 5.0;

        private static readonly Dictionary<string, string[]> MalzemeAnahtarlar = new Dictionary<string, string[]>
        {
            { "Yarma", new[] { "YARMA", "KAZI", "YARMASI" } },
            { "Dolgu", new[] { "DOLGU", "DOLGUSU" } },
            { "Sıyırma", new[] { "SIYIRMA", "SIYIRMASI", "BİTKİSEL" } },
            { "Aşınma", new[] { "AŞINMA", "ASINMA" } },
            { "Binder", new[] { "BİNDER", "BINDER" } },
            { "Bitümlü Temel", new[] { "BİTÜMLÜ", "BITUMLU" } },
            { "Plentmiks", new[] { "PLENTMİKS", "PLENTMIKS" } },
            { "Alttemel", new[] { "ALTTEMEL", "ALT TEMEL" } },
            { "Kırmataş", new[] { "KIRMATAŞ", "KIRMATAS" } },
            { "B.T. Yerine Konan", new[] { "YERİNE KONAN", "YERINE KONAN" } },
            { "B.T. Yerine Konmayan", new[] { "YERİNE KONMAYAN", "YERINE KONMAYAN" } },
        };

        public TabloOkumaService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public Dictionary<string, double> TabloOku(KesitGrubu kesit)
        {
            var degerler = new Dictionary<string, double>();

            if (kesit.TextObjeler == null || kesit.TextObjeler.Count == 0)
                return degerler;

            var textler = TextleriOku(kesit.TextObjeler);

            foreach (var (metin, _) in textler)
            {
                foreach (var (malzeme, anahtarlar) in MalzemeAnahtarlar)
                {
                    if (degerler.ContainsKey(malzeme)) continue;

                    string upper = metin.ToUpperInvariant();
                    if (anahtarlar.Any(a => upper.Contains(a)))
                    {
                        double? deger = SayiCikar(metin);
                        if (deger.HasValue)
                        {
                            degerler[malzeme] = deger.Value;
                            break;
                        }
                    }
                }
            }

            return degerler;
        }

        public List<TabloKiyasSonucu> Kiyasla(KesitGrubu kesit)
        {
            var tabloDegerleri = TabloOku(kesit);
            var sonuclar = new List<TabloKiyasSonucu>();

            if (kesit.HesaplananAlanlar == null) return sonuclar;

            foreach (var hesap in kesit.HesaplananAlanlar)
            {
                if (tabloDegerleri.TryGetValue(hesap.MalzemeAdi, out double tabloAlani))
                {
                    double fark = Math.Abs(hesap.Alan - tabloAlani);
                    double farkYuzde = tabloAlani > 0 ? (fark / tabloAlani) * 100 : 0;

                    sonuclar.Add(new TabloKiyasSonucu
                    {
                        MalzemeAdi = hesap.MalzemeAdi,
                        TabloAlani = tabloAlani,
                        HesaplananAlan = hesap.Alan,
                        Fark = fark,
                        FarkYuzde = farkYuzde,
                        Uyumlu = farkYuzde <= UyumToleransYuzde
                    });
                }
            }

            kesit.TabloKiyaslari = sonuclar;

            bool hepsiUyumlu = sonuclar.All(s => s.Uyumlu);
            bool sorunluVar = sonuclar.Any(s => s.FarkYuzde > UyariToleransYuzde);

            if (sorunluVar)
                kesit.Durum = DogrulamaDurumu.Sorunlu;
            else if (hepsiUyumlu && kesit.Durum == DogrulamaDurumu.Bekliyor)
                kesit.Durum = DogrulamaDurumu.Onaylandi;

            return sonuclar;
        }

        public void TopluKiyasla(List<KesitGrubu> kesitler)
        {
            foreach (var kesit in kesitler)
                Kiyasla(kesit);

            int uyumlu = kesitler.Count(k => k.Durum == DogrulamaDurumu.Onaylandi);
            int sorunlu = kesitler.Count(k => k.Durum == DogrulamaDurumu.Sorunlu);
            LoggingService.Info($"Toplu kıyaslama: {uyumlu} uyumlu, {sorunlu} sorunlu");
        }

        private List<(string metin, ObjectId id)> TextleriOku(List<ObjectId> textIds)
        {
            var sonuc = new List<(string, ObjectId)>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in textIds)
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);
                    string metin = null;

                    if (obj is DBText dbText)
                        metin = dbText.TextString;
                    else if (obj is MText mText)
                        metin = mText.Contents;

                    if (!string.IsNullOrWhiteSpace(metin))
                        sonuc.Add((metin.Trim(), id));
                }

                tr.Commit();
            }

            return sonuc;
        }

        private double? SayiCikar(string metin)
        {
            var match = Regex.Match(metin, @"[=:]\s*([\d.,]+)");
            if (!match.Success)
                match = Regex.Match(metin, @"([\d]+[.,][\d]+)");

            if (match.Success)
            {
                string sayi = match.Groups[1].Value.Replace(',', '.');
                if (double.TryParse(sayi, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double deger))
                    return deger;
            }

            return null;
        }
    }
}
