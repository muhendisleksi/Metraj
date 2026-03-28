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
            { "Yarma", new[] { "YARMA", "KAZI" } },
            { "Dolgu", new[] { "DOLGU" } },
            { "Siyirma", new[] { "SIYIRMA", "SİYIRMA", "BİTKİSEL", "BITKISEL" } },
            { "Asinma", new[] { "AŞINMA", "ASINMA" } },
            { "Binder", new[] { "BİNDER", "BINDER" } },
            { "Bitumlu Temel", new[] { "BİTÜMLÜ", "BITUMLU", "BITUMEN", "BİTÜMEN" } },
            { "Plentmiks", new[] { "PLENTMİKS", "PLENTMIKS", "PLENTMIS" } },
            { "Alttemel", new[] { "ALTTEMEL", "ALT TEMEL", "GRANULER", "GRANÜLER" } },
            { "Kirmatas", new[] { "KIRMATAŞ", "KIRMATAS" } },
            { "B.T. Yerine Konan", new[] { "YERİNE KONAN", "YERINE KONAN", "BT KONAN" } },
            { "B.T. Yerine Konmayan", new[] { "YERİNE KONMAYAN", "YERINE KONMAYAN", "BT KONMAYAN" } },
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

            // Yontem 1: Ayni text icerisinde anahtar kelime + sayi (orn: "Yarma = 16.27" veya "Yarma | 16.27")
            foreach (var (metin, x, y) in textler)
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

            // Yontem 2: Ayri text objeleri (malzeme adi ayri, deger ayri — yakin konumda)
            // Malzeme adi text'i ile yanindaki/altindaki sayi text'ini eslesir
            if (degerler.Count == 0 && textler.Count >= 2)
            {
                var malzemeTextleri = new List<(string malzeme, double x, double y)>();
                var sayiTextleri = new List<(double deger, double x, double y)>();

                foreach (var (metin, x, y) in textler)
                {
                    string upper = metin.ToUpperInvariant();

                    // Bu text bir malzeme adi mi?
                    foreach (var (malzeme, anahtarlar) in MalzemeAnahtarlar)
                    {
                        if (anahtarlar.Any(a => upper.Contains(a)))
                        {
                            malzemeTextleri.Add((malzeme, x, y));
                            break;
                        }
                    }

                    // Bu text bir sayi mi?
                    if (double.TryParse(metin.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double sayi))
                    {
                        sayiTextleri.Add((sayi, x, y));
                    }
                }

                // Yakin konumdaki malzeme-sayi ciftlerini eslestir
                foreach (var (malzeme, mx, my) in malzemeTextleri)
                {
                    if (degerler.ContainsKey(malzeme)) continue;

                    // En yakin sayi text'ini bul (oncelik: saga yakin, sonra asagi yakin)
                    var enYakin = sayiTextleri
                        .Where(s => Math.Abs(s.y - my) < 3.0) // ayni satir (Y farki < 3 birim)
                        .OrderBy(s => Math.Abs(s.x - mx))
                        .FirstOrDefault();

                    if (enYakin.deger > 0 || (enYakin.x != 0 && enYakin.y != 0))
                    {
                        degerler[malzeme] = enYakin.deger;
                    }
                    else
                    {
                        // Altindaki sayi text'ini dene
                        enYakin = sayiTextleri
                            .Where(s => Math.Abs(s.x - mx) < 5.0 && s.y < my)
                            .OrderByDescending(s => s.y)
                            .FirstOrDefault();

                        if (enYakin.deger > 0 || (enYakin.x != 0 && enYakin.y != 0))
                            degerler[malzeme] = enYakin.deger;
                    }
                }
            }

            if (degerler.Count > 0)
                LoggingService.Info($"Tablo okuma {kesit.Anchor?.IstasyonMetni}: {degerler.Count} malzeme — {string.Join(", ", degerler.Select(d => $"{d.Key}={d.Value:F2}"))}");

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

            bool hepsiUyumlu = sonuclar.Count > 0 && sonuclar.All(s => s.Uyumlu);
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
            LoggingService.Info($"Toplu kiyaslama: {uyumlu} uyumlu, {sorunlu} sorunlu");
        }

        private List<(string metin, double x, double y)> TextleriOku(List<ObjectId> textIds)
        {
            var sonuc = new List<(string, double, double)>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in textIds)
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);
                    string metin = null;
                    double x = 0, y = 0;

                    if (obj is DBText dbText)
                    {
                        metin = dbText.TextString;
                        x = dbText.Position.X;
                        y = dbText.Position.Y;
                    }
                    else if (obj is MText mText)
                    {
                        metin = mText.Contents;
                        x = mText.Location.X;
                        y = mText.Location.Y;
                    }

                    if (!string.IsNullOrWhiteSpace(metin))
                        sonuc.Add((metin.Trim(), x, y));
                }

                tr.Commit();
            }

            return sonuc;
        }

        private double? SayiCikar(string metin)
        {
            // "Yarma = 16.27" veya "Yarma | 16.27" veya "Yarma : 16.27"
            var match = Regex.Match(metin, @"[=:|]\s*([\d.,]+)");
            if (!match.Success)
            {
                // "Yarma  16.27" (boslukla ayrilmis)
                match = Regex.Match(metin, @"\s+([\d]+[.,]\d+)\s*$");
            }
            if (!match.Success)
            {
                // Sadece sonundaki sayi
                match = Regex.Match(metin, @"([\d]+[.,]\d+)$");
            }

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
