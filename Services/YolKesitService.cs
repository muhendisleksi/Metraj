using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public class YolKesitService : IYolKesitService
    {
        private readonly IDocumentContext _documentContext;
        private readonly IEditorService _editorService;
        private readonly IHatchOlusturmaService _hatchService;
        private readonly IMalzemeHatchAyarService _ayarService;

        public YolKesitService(
            IDocumentContext documentContext,
            IEditorService editorService,
            IHatchOlusturmaService hatchService,
            IMalzemeHatchAyarService ayarService)
        {
            _documentContext = documentContext;
            _editorService = editorService;
            _hatchService = hatchService;
            _ayarService = ayarService;
        }

        public YolKesitVerisi TiklaIsaretleKesitOku(string kolonHarfi)
        {
            var (istasyon, istasyonMetni) = IstasyonAl();
            if (istasyon < 0) return null;

            var kesit = new YolKesitVerisi
            {
                Istasyon = istasyon,
                IstasyonMetni = istasyonMetni,
                KolonHarfi = kolonHarfi
            };

            // Aktif malzeme (varsay\u0131lan: Yarma)
            string aktifMalzeme = "Yarma";
            MalzemeHatchAyari aktifAyar = _ayarService.MalzemeAyariGetir(aktifMalzeme);

            _editorService.WriteMessage($"\n--- Kolon {kolonHarfi} | Km {istasyonMetni} ---\n");
            _editorService.WriteMessage($"Aktif malzeme: {aktifMalzeme}. 'Kategori' ile de\u011Fi\u015Ftirin.\n");

            var isaretler = new List<(ObjectId hatchId, KatmanAlanBilgisi bilgi)>();

            while (true)
            {
                var result = _editorService.GetPointWithKeywords(
                    $"\n[{aktifMalzeme.ToUpperInvariant()}] T\u0131klay\u0131n veya [NesneSec/Kategori/Yarma/Dolgu/GeriAl/Bitti]: ",
                    new[] { "NesneSec", "Kategori", "Yarma", "Dolgu", "GeriAl", "Bitti" });

                if (result.Status == PromptStatus.Cancel)
                    break;

                if (result.Status == PromptStatus.Keyword)
                {
                    string kw = result.StringResult;

                    if (kw.Equals("Yarma", StringComparison.OrdinalIgnoreCase))
                    {
                        aktifMalzeme = "Yarma";
                        aktifAyar = _ayarService.MalzemeAyariGetir(aktifMalzeme);
                        _editorService.WriteMessage($"\nAktif malzeme: Yarma\n");
                    }
                    else if (kw.Equals("Dolgu", StringComparison.OrdinalIgnoreCase))
                    {
                        aktifMalzeme = "Dolgu";
                        aktifAyar = _ayarService.MalzemeAyariGetir(aktifMalzeme);
                        _editorService.WriteMessage($"\nAktif malzeme: Dolgu\n");
                    }
                    else if (kw.Equals("Kategori", StringComparison.OrdinalIgnoreCase))
                    {
                        var secilen = KategoridenMalzemeSec();
                        if (secilen != null)
                        {
                            aktifMalzeme = secilen;
                            aktifAyar = _ayarService.MalzemeAyariGetir(aktifMalzeme);
                            _editorService.WriteMessage($"\nAktif malzeme: {aktifMalzeme}\n");
                        }
                    }
                    else if (kw.Equals("NesneSec", StringComparison.OrdinalIgnoreCase))
                    {
                        var nesneResult = NesneSecVeAlanAl(aktifMalzeme, aktifAyar, kolonHarfi);
                        if (nesneResult.HasValue)
                            isaretler.Add(nesneResult.Value);
                    }
                    else if (kw.Equals("GeriAl", StringComparison.OrdinalIgnoreCase))
                    {
                        GeriAl(isaretler);
                    }
                    else if (kw.Equals("Bitti", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    continue;
                }

                if (result.Status == PromptStatus.OK)
                {
                    var tiklaNoktasi = result.Value;
                    var (hatchId, alan) = _hatchService.HatchOlustur(tiklaNoktasi, aktifAyar);

                    if (hatchId.IsNull || alan <= Constants.AlanToleransi)
                    {
                        _editorService.WriteMessage("\nKapal\u0131 alan bulunamad\u0131. Ba\u015Fka noktaya veya 'NesneSec' deneyin.\n");
                        continue;
                    }

                    var bilgi = new KatmanAlanBilgisi
                    {
                        MalzemeAdi = aktifMalzeme,
                        Kategori = KategoriBelirleMalzeme(aktifMalzeme),
                        Alan = alan,
                        KaynakLayerAdi = aktifAyar.LayerAdi,
                        TiklamaNoktalari = new System.Collections.Generic.List<double[]>
                            { new[] { tiklaNoktasi.X, tiklaNoktasi.Y } }
                    };

                    isaretler.Add((hatchId, bilgi));
                    _editorService.WriteMessage($"\n  {aktifMalzeme}: {alan:F2} m\u00B2\n");
                }
            }

            // "Bitti" sonras\u0131: her malzeme i\u00E7in toplam\u0131 hesaplay\u0131p tek etiket at
            EtiketleriTopluOlustur(isaretler, kolonHarfi);

            // Ayn\u0131 malzemeleri birle\u015Ftirerek sonu\u00E7 olu\u015Ftur
            var gruplu = isaretler
                .GroupBy(x => x.bilgi.MalzemeAdi, StringComparer.OrdinalIgnoreCase);
            foreach (var grup in gruplu)
            {
                var birlesik = new KatmanAlanBilgisi
                {
                    MalzemeAdi = grup.First().bilgi.MalzemeAdi,
                    Kategori = grup.First().bilgi.Kategori,
                    Alan = grup.Sum(x => x.bilgi.Alan),
                    KaynakLayerAdi = grup.First().bilgi.KaynakLayerAdi
                };
                foreach (var item in grup)
                    birlesik.TiklamaNoktalari.AddRange(item.bilgi.TiklamaNoktalari);
                kesit.KatmanAlanlari.Add(birlesik);
            }

            kesit.ToplamKaziAlani = kesit.KatmanAlanlari
                .Where(k => k.MalzemeAdi.Equals("Yarma", StringComparison.OrdinalIgnoreCase))
                .Sum(k => k.Alan);
            kesit.ToplamDolguAlani = kesit.KatmanAlanlari
                .Where(k => k.MalzemeAdi.Equals("Dolgu", StringComparison.OrdinalIgnoreCase))
                .Sum(k => k.Alan);

            _editorService.WriteMessage($"\nKolon {kolonHarfi} | Km {istasyonMetni}: {isaretler.Count} kalem i\u015Faretlendi.\n");

            LoggingService.Info("Kesit: Kolon {K}, Km {I}, {A} kalem",
                kolonHarfi, istasyonMetni, isaretler.Count);

            return kesit;
        }

        public List<KatmanAlanBilgisi> KalemEkle(string kolonHarfi)
        {
            string aktifMalzeme = "Yarma";
            MalzemeHatchAyari aktifAyar = _ayarService.MalzemeAyariGetir(aktifMalzeme);

            _editorService.WriteMessage($"\n--- Kolon {kolonHarfi}: Kalem ekleme ---\n");

            var isaretler = new List<(ObjectId hatchId, KatmanAlanBilgisi bilgi)>();

            while (true)
            {
                var result = _editorService.GetPointWithKeywords(
                    $"\n[{aktifMalzeme.ToUpperInvariant()}] T\u0131klay\u0131n [NesneSec/Kategori/Yarma/Dolgu/GeriAl/Bitti]: ",
                    new[] { "NesneSec", "Kategori", "Yarma", "Dolgu", "GeriAl", "Bitti" });

                if (result.Status == PromptStatus.Cancel) break;

                if (result.Status == PromptStatus.Keyword)
                {
                    string kw = result.StringResult;
                    if (kw.Equals("Yarma", StringComparison.OrdinalIgnoreCase))
                    { aktifMalzeme = "Yarma"; aktifAyar = _ayarService.MalzemeAyariGetir(aktifMalzeme); _editorService.WriteMessage($"\nAktif: Yarma\n"); }
                    else if (kw.Equals("Dolgu", StringComparison.OrdinalIgnoreCase))
                    { aktifMalzeme = "Dolgu"; aktifAyar = _ayarService.MalzemeAyariGetir(aktifMalzeme); _editorService.WriteMessage($"\nAktif: Dolgu\n"); }
                    else if (kw.Equals("Kategori", StringComparison.OrdinalIgnoreCase))
                    { var s = KategoridenMalzemeSec(); if (s != null) { aktifMalzeme = s; aktifAyar = _ayarService.MalzemeAyariGetir(aktifMalzeme); _editorService.WriteMessage($"\nAktif: {aktifMalzeme}\n"); } }
                    else if (kw.Equals("NesneSec", StringComparison.OrdinalIgnoreCase))
                    { var r = NesneSecVeAlanAl(aktifMalzeme, aktifAyar, kolonHarfi); if (r.HasValue) isaretler.Add(r.Value); }
                    else if (kw.Equals("GeriAl", StringComparison.OrdinalIgnoreCase))
                    { GeriAl(isaretler); }
                    else if (kw.Equals("Bitti", StringComparison.OrdinalIgnoreCase))
                    { break; }
                    continue;
                }

                if (result.Status == PromptStatus.OK)
                {
                    var tikPt = result.Value;
                    var (hatchId, alan) = _hatchService.HatchOlustur(tikPt, aktifAyar);
                    if (hatchId.IsNull || alan <= Constants.AlanToleransi)
                    { _editorService.WriteMessage("\nAlan bulunamad\u0131. 'NesneSec' deneyin.\n"); continue; }

                    isaretler.Add((hatchId, new KatmanAlanBilgisi
                    {
                        MalzemeAdi = aktifMalzeme,
                        Kategori = KategoriBelirleMalzeme(aktifMalzeme),
                        Alan = alan,
                        KaynakLayerAdi = aktifAyar.LayerAdi,
                        TiklamaNoktalari = new System.Collections.Generic.List<double[]>
                            { new[] { tikPt.X, tikPt.Y } }
                    }));
                    _editorService.WriteMessage($"\n  {aktifMalzeme}: {alan:F2} m\u00B2\n");
                }
            }

            EtiketleriTopluOlustur(isaretler, kolonHarfi);

            // Birle\u015Ftir
            var sonuc = new List<KatmanAlanBilgisi>();
            var gruplu = isaretler.GroupBy(x => x.bilgi.MalzemeAdi, StringComparer.OrdinalIgnoreCase);
            foreach (var grup in gruplu)
            {
                var birlesik = new KatmanAlanBilgisi
                {
                    MalzemeAdi = grup.First().bilgi.MalzemeAdi,
                    Kategori = grup.First().bilgi.Kategori,
                    Alan = grup.Sum(x => x.bilgi.Alan),
                    KaynakLayerAdi = grup.First().bilgi.KaynakLayerAdi
                };
                foreach (var item in grup)
                    birlesik.TiklamaNoktalari.AddRange(item.bilgi.TiklamaNoktalari);
                sonuc.Add(birlesik);
            }
            return sonuc;
        }

        private string KategoridenMalzemeSec()
        {
            var katResult = _editorService.GetKeywords(
                "\nKategori [Ustyapi/Alttemel/ToprakIsleri/Ozel]: ",
                new[] { "Ustyapi", "Alttemel", "ToprakIsleri", "Ozel" },
                "Ustyapi");

            if (katResult.Status != PromptStatus.OK || string.IsNullOrEmpty(katResult.StringResult))
                return null;

            string[] malzemeler;
            switch (katResult.StringResult)
            {
                case "Ustyapi":
                    malzemeler = new[] { "Asinma", "Binder", "Bitumen" };
                    break;
                case "Alttemel":
                    malzemeler = new[] { "Plentmiks", "AltTemel", "Siyirma" };
                    break;
                case "ToprakIsleri":
                    malzemeler = new[] { "Yarma", "Dolgu" };
                    break;
                case "Ozel":
                    malzemeler = new[] { "BTYerineKonan", "BTYerineKonmayan" };
                    break;
                default:
                    return null;
            }

            var malResult = _editorService.GetKeywords(
                $"\nMalzeme [{string.Join("/", malzemeler)}]: ",
                malzemeler,
                malzemeler[0]);

            if (malResult.Status != PromptStatus.OK || string.IsNullOrEmpty(malResult.StringResult))
                return null;

            // Keyword'den ger\u00E7ek malzeme ad\u0131na d\u00F6n\u00FC\u015Ft\u00FCr
            return KeywordToMalzemeAdi(malResult.StringResult);
        }

        private string KeywordToMalzemeAdi(string keyword)
        {
            switch (keyword)
            {
                case "Asinma": return "A\u015F\u0131nma";
                case "Binder": return "Binder";
                case "Bitumen": return "Bit\u00FCmen";
                case "Plentmiks": return "Plentmiks";
                case "AltTemel": return "AltTemel";
                case "Siyirma": return "S\u0131y\u0131rma";
                case "Yarma": return "Yarma";
                case "Dolgu": return "Dolgu";
                case "BTYerineKonan": return "B.T. Yerine Konan";
                case "BTYerineKonmayan": return "B.T. Yerine Konmayan";
                default: return keyword;
            }
        }

        private MalzemeKategorisi KategoriBelirleMalzeme(string malzemeAdi)
        {
            if (malzemeAdi == "Yarma" || malzemeAdi == "Dolgu")
                return MalzemeKategorisi.ToprakIsleri;
            if (malzemeAdi == "A\u015F\u0131nma" || malzemeAdi == "Binder" || malzemeAdi == "Bit\u00FCmen")
                return MalzemeKategorisi.Ustyapi;
            if (malzemeAdi == "Plentmiks" || malzemeAdi == "AltTemel" || malzemeAdi == "S\u0131y\u0131rma")
                return MalzemeKategorisi.Alttemel;
            return MalzemeKategorisi.Ozel;
        }

        public string KolonHarfiUret(int sira)
        {
            string harf = "";
            int n = sira;
            do
            {
                harf = (char)('A' + n % 26) + harf;
                n = n / 26 - 1;
            } while (n >= 0);
            return harf;
        }

        public double IstasyonParse(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return -1;
            string temiz = metin.Trim();
            temiz = Regex.Replace(temiz, @"^(Km|KM|km|Ist|IST|ist)[:\s]*", "", RegexOptions.IgnoreCase);
            var match = Regex.Match(temiz, @"(\d+)\+(\d+\.?\d*)");
            if (match.Success)
            {
                double km = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                double m = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                return km * 1000 + m;
            }
            if (double.TryParse(temiz, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double direkt))
                return direkt;
            return -1;
        }

        private (double istasyon, string metin) IstasyonAl()
        {
            var yontemResult = _editorService.GetKeywords(
                "\n\u0130stasyon [TextSec/ManuelGir]: ",
                new[] { "TextSec", "ManuelGir" }, "ManuelGir");
            if (yontemResult.Status != PromptStatus.OK) return (-1, null);

            if (yontemResult.StringResult != null &&
                yontemResult.StringResult.Equals("TextSec", StringComparison.OrdinalIgnoreCase))
                return IstasyonTexttenOku();
            return IstasyonManuelAl();
        }

        private (double istasyon, string metin) IstasyonTexttenOku()
        {
            var entityResult = _editorService.GetEntity("\n\u0130stasyon text se\u00E7in: ");
            if (entityResult.Status != PromptStatus.OK) return (-1, null);

            using (var tr = _documentContext.BeginTransaction())
            {
                var obj = tr.GetObject(entityResult.ObjectId, OpenMode.ForRead);
                string metin = null;
                if (obj is DBText dbText) metin = dbText.TextString;
                else if (obj is MText mText) metin = mText.Contents;
                tr.Commit();

                if (string.IsNullOrWhiteSpace(metin)) return (-1, null);
                double istasyon = IstasyonParse(metin);
                if (istasyon < 0) return (-1, null);
                string formatli = IstasyonFormatla(istasyon);
                return (istasyon, formatli);
            }
        }

        private (double istasyon, string metin) IstasyonManuelAl()
        {
            var strResult = _editorService.GetString("\n\u0130stasyon girin (\u00F6r: 0+020): ");
            if (strResult.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(strResult.StringResult))
                return (-1, null);
            double istasyon = IstasyonParse(strResult.StringResult);
            if (istasyon < 0) return (-1, null);
            string formatli = IstasyonFormatla(istasyon);
            return (istasyon, formatli);
        }

        private (ObjectId hatchId, KatmanAlanBilgisi bilgi)? NesneSecVeAlanAl(
            string aktifMalzeme, MalzemeHatchAyari aktifAyar, string kolonHarfi)
        {
            var entityResult = _editorService.GetEntity("\nPolyline, hatch veya circle se\u00E7in: ");
            if (entityResult.Status != PromptStatus.OK)
                return null;

            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                double alan = 0;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var entity = tr.GetObject(entityResult.ObjectId, OpenMode.ForRead) as Entity;

                    if (entity is Polyline pl && pl.Closed)
                        alan = pl.Area;
                    else if (entity is Hatch h)
                    {
                        try { alan = h.Area; } catch { }
                    }
                    else if (entity is Circle c)
                        alan = Math.PI * c.Radius * c.Radius;
                    else
                    {
                        _editorService.WriteMessage("\nKapal\u0131 polyline, hatch veya circle se\u00E7in.\n");
                        tr.Commit();
                        return null;
                    }
                    tr.Commit();
                }

                if (alan <= Constants.AlanToleransi)
                {
                    _editorService.WriteMessage("\nGe\u00E7erli alan hesaplanamad\u0131.\n");
                    return null;
                }

                var bilgi = new KatmanAlanBilgisi
                {
                    MalzemeAdi = aktifMalzeme,
                    Kategori = KategoriBelirleMalzeme(aktifMalzeme),
                    Alan = alan,
                    KaynakLayerAdi = aktifAyar.LayerAdi
                };

                _editorService.WriteMessage($"\n  {aktifMalzeme}: {alan:F2} m\u00B2 (nesne se\u00E7im)\n");
                return (entityResult.ObjectId, bilgi);
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("Nesne se\u00E7im hatas\u0131", ex);
                return null;
            }
        }

        private void EtiketleriTopluOlustur(
            List<(ObjectId hatchId, KatmanAlanBilgisi bilgi)> isaretler, string kolonHarfi)
        {
            // Her malzeme i\u00E7in son hatch'in merkezine tek etiket at
            var gruplar = isaretler
                .GroupBy(x => x.bilgi.MalzemeAdi, StringComparer.OrdinalIgnoreCase);

            foreach (var grup in gruplar)
            {
                // Grubun son hatch'ine etiket at (en b\u00FCy\u00FCk alan olan\u0131 tercih et)
                var enBuyuk = grup.OrderByDescending(x => x.bilgi.Alan).First();
                if (!enBuyuk.hatchId.IsNull)
                {
                    var ayar = _ayarService.MalzemeAyariGetir(grup.Key);
                    _hatchService.EtiketYaz(enBuyuk.hatchId, kolonHarfi, ayar);
                }
            }
        }

        private void GeriAl(List<(ObjectId hatchId, KatmanAlanBilgisi bilgi)> isaretler)
        {
            if (isaretler.Count == 0)
            {
                _editorService.WriteMessage("\nGeri al\u0131nacak i\u015Faret yok.\n");
                return;
            }
            var son = isaretler[isaretler.Count - 1];
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    if (!son.hatchId.IsNull)
                    {
                        var hatch = tr.GetObject(son.hatchId, OpenMode.ForWrite) as Entity;
                        hatch?.Erase();
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("Geri alma hatas\u0131", ex);
            }
            isaretler.RemoveAt(isaretler.Count - 1);
            _editorService.WriteMessage("\nSon i\u015Faret geri al\u0131nd\u0131.\n");
        }

        private string IstasyonFormatla(double istasyon)
        {
            int km = (int)(istasyon / 1000);
            double m = istasyon % 1000;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}+{1:000.00}", km, m);
        }
    }
}
