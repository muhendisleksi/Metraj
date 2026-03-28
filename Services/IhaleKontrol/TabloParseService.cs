using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.IhaleKontrol;
using Metraj.Services.IhaleKontrol.Interfaces;

namespace Metraj.Services.IhaleKontrol
{
    public class TabloParseService : ITabloParseService
    {
        private readonly IDocumentContext _documentContext;

        private static readonly Regex KmRegex = new Regex(
            @"(?:KM\s*[:=]?\s*)?(\d+)\+(\d+(?:[.,]\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Sayı olabilecek text'ler için regex
        private static readonly Regex SayiRegex = new Regex(
            @"^\s*-?\d+[.,]?\d*\s*$", RegexOptions.Compiled);

        // Satır gruplama toleransı (Y ekseni, aynı satırdaki text'ler)
        private const double SatirToleransi = 2.0;
        // Sütun gruplama toleransı (X ekseni, sol/sağ sütun ayırımı)
        private const double SutunToleransi = 5.0;

        private static readonly Dictionary<string, string[]> NormalizasyonTablosu = new Dictionary<string, string[]>
        {
            ["Aşınma"] = new[] { "ASINMA", "AŞINMA", "BSK", "ASINMA TABAKASI" },
            ["Binder"] = new[] { "BINDER", "BİNDER" },
            ["Bitümen"] = new[] { "BITUMEN", "BİTÜMEN", "BTM", "BIT.TEMEL", "BITUMLU", "BİTÜMLÜ", "BITUMLU TEMEL", "BİTÜMLÜ TEMEL" },
            ["Plentmiks"] = new[] { "PLENTMISK", "PLENTMİKS", "PLENTMIKS", "PMT", "PLENT" },
            ["AltTemel"] = new[] { "ALTTEMEL", "ALT TEMEL", "KMT", "KIRMATAS", "KIRMATAŞ" },
            ["Sıyırma"] = new[] { "SIYIRMA", "SIYIRMA", "SIYIRMA TABAKASI" },
            ["Yarma"] = new[] { "YARMA", "KAZI", "KAZIM" },
            ["Dolgu"] = new[] { "DOLGU" },
            ["B.T. Yerine Konan"] = new[] { "B.T. YERINE KONAN", "BT YERINE KONAN", "YERINE KONAN", "YERİNE KONAN" },
            ["B.T. Yerine Konmayan"] = new[] { "B.T. YERINE KONMAYAN", "BT YERINE KONMAYAN", "YERINE KONMAYAN", "YERİNE KONMAYAN" },
            ["Stabilize"] = new[] { "STABILIZE", "STABİLİZE" },
            ["Banket"] = new[] { "BANKET" },
            ["Hendek"] = new[] { "HENDEK" }
        };

        public TabloParseService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public List<TabloKesitVerisi> TumTablolariOku()
        {
            var tumTextler = new List<TextBilgisi>();

            using (var tr = _documentContext.BeginTransaction())
            {
                var db = _documentContext.Database;
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;
                    var textBilgi = TextBilgisiOku(entity);
                    if (textBilgi != null)
                        tumTextler.Add(textBilgi);
                }

                tr.Commit();
            }

            if (tumTextler.Count == 0)
            {
                LoggingService.Warning("DWG'de hiç text objesi bulunamadı.", null);
                return new List<TabloKesitVerisi>();
            }

            // KM text'lerini bul — her biri bir tablo başlangıcı
            var kmTextler = tumTextler
                .Where(t => KmRegex.IsMatch(t.Metin))
                .ToList();

            if (kmTextler.Count == 0)
            {
                LoggingService.Warning("DWG'de KM değeri içeren text bulunamadı.", null);
                return new List<TabloKesitVerisi>();
            }

            // Her KM text'inin etrafındaki text'leri grupla → tablo reconstruct
            var sonuclar = new List<TabloKesitVerisi>();

            foreach (var kmText in kmTextler)
            {
                var veri = TabloReconstructEt(kmText, tumTextler);
                if (veri != null)
                    sonuclar.Add(veri);
            }

            sonuclar.Sort((a, b) => a.Istasyon.CompareTo(b.Istasyon));
            LoggingService.Info("Tablo parse tamamlandı: {Adet} kesit (text-bazlı)", sonuclar.Count);
            return sonuclar;
        }

        private TabloKesitVerisi TabloReconstructEt(TextBilgisi kmText, List<TextBilgisi> tumTextler)
        {
            try
            {
                double istasyon = KmParseEt(kmText.Metin);
                if (istasyon < 0) return null;

                var veri = new TabloKesitVerisi
                {
                    Istasyon = istasyon,
                    IstasyonMetni = kmText.Metin.Trim(),
                    TabloX = kmText.X,
                    TabloY = kmText.Y
                };

                // KM text'inin altındaki text'leri bul (aynı X bölgesinde, Y azalan)
                // Tablo genişliği: KM text'inin X'ine göre ±arama genişliği
                double aramaGenisligi = 150;  // birim
                double aramaDerinligi = 200;  // KM'nin altına kaç birim bakacağız

                var yakinTextler = tumTextler
                    .Where(t => t != kmText &&
                                Math.Abs(t.X - kmText.X) < aramaGenisligi &&
                                t.Y < kmText.Y &&
                                t.Y > kmText.Y - aramaDerinligi)
                    .ToList();

                if (yakinTextler.Count < 2)
                    return null;

                // Text'leri satır satır grupla (Y değerine göre)
                var satirlar = SatirlaraGrupla(yakinTextler);

                // Her satırda sol sütun (malzeme adı) ve sağ sütun (alan değeri) ayır
                foreach (var satir in satirlar)
                {
                    if (satir.Count < 2) continue;

                    // X'e göre sırala — en soldaki malzeme adı, en sağdaki alan değeri
                    var sirali = satir.OrderBy(t => t.X).ToList();
                    string solMetin = sirali.First().Metin.Trim();
                    string sagMetin = sirali.Last().Metin.Trim();

                    // Başlık satırını atla
                    string solUpper = solMetin.ToUpperInvariant();
                    if (solUpper.Contains("MALZEME") || solUpper.Contains("ISMI") ||
                        solUpper.Contains("İSMİ") || solUpper.Contains("ADI") ||
                        solUpper.Contains("KM") || KmRegex.IsMatch(solMetin))
                        continue;

                    // Sağ taraf sayı olmalı
                    if (!SayiIcerir(sagMetin))
                        continue;

                    double alan = 0;
                    NumberParserHelper.TryParse(sagMetin, out alan);

                    veri.MalzemeAlanlari.Add(new TabloMalzemeAlani
                    {
                        HamMalzemeAdi = solMetin,
                        NormalizeMalzemeAdi = MalzemeAdiNormalize(solMetin),
                        Alan = alan
                    });
                }

                return veri.MalzemeAlanlari.Count > 0 ? veri : null;
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("Tablo reconstruct hatası: {Hata}", ex, ex.Message);
                return null;
            }
        }

        private List<List<TextBilgisi>> SatirlaraGrupla(List<TextBilgisi> textler)
        {
            // Y'ye göre azalan sırala (üstten alta)
            var sirali = textler.OrderByDescending(t => t.Y).ToList();
            var satirlar = new List<List<TextBilgisi>>();

            foreach (var text in sirali)
            {
                bool satirBulundu = false;
                foreach (var satir in satirlar)
                {
                    if (Math.Abs(satir[0].Y - text.Y) < SatirToleransi)
                    {
                        satir.Add(text);
                        satirBulundu = true;
                        break;
                    }
                }
                if (!satirBulundu)
                    satirlar.Add(new List<TextBilgisi> { text });
            }

            return satirlar;
        }

        private bool SayiIcerir(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return false;
            string temiz = metin.Trim();
            // Nokta veya virgüllü sayılar: "16.27", "0,00", "1234"
            return SayiRegex.IsMatch(temiz) || NumberParserHelper.TryParse(temiz, out _);
        }

        private TextBilgisi TextBilgisiOku(Entity entity)
        {
            if (entity is DBText dbText)
            {
                string metin = dbText.TextString;
                if (string.IsNullOrWhiteSpace(metin)) return null;
                return new TextBilgisi
                {
                    Metin = metin,
                    X = dbText.Position.X,
                    Y = dbText.Position.Y
                };
            }

            if (entity is MText mText)
            {
                string metin = mText.Contents;
                if (string.IsNullOrWhiteSpace(metin)) return null;
                // MText format kodlarını temizle
                metin = MTextTemizle(metin);
                if (string.IsNullOrWhiteSpace(metin)) return null;
                return new TextBilgisi
                {
                    Metin = metin,
                    X = mText.Location.X,
                    Y = mText.Location.Y
                };
            }

            return null;
        }

        private string MTextTemizle(string metin)
        {
            if (string.IsNullOrEmpty(metin)) return metin;
            // MText format kodlarını temizle: {\fArial|b1|i0|...; gibi
            metin = Regex.Replace(metin, @"\\[A-Za-z][^;]*;", "");
            metin = Regex.Replace(metin, @"\{|\}", "");
            metin = Regex.Replace(metin, @"\\P", " ");
            return metin.Trim();
        }

        private double KmParseEt(string metin)
        {
            var match = KmRegex.Match(metin);
            if (!match.Success)
                return -1;

            if (int.TryParse(match.Groups[1].Value, out int km))
            {
                string metreStr = match.Groups[2].Value.Replace(',', '.');
                if (double.TryParse(metreStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double metre))
                {
                    return km * 1000.0 + metre;
                }
            }
            return -1;
        }

        public string MalzemeAdiNormalize(string hamAd)
        {
            if (string.IsNullOrWhiteSpace(hamAd))
                return hamAd;

            string temiz = hamAd.Trim();
            string upper = TurkceUpperNormalize(temiz);

            foreach (var kvp in NormalizasyonTablosu)
            {
                foreach (string varyant in kvp.Value)
                {
                    string normalVaryant = TurkceUpperNormalize(varyant);
                    if (upper.Equals(normalVaryant, StringComparison.OrdinalIgnoreCase))
                        return kvp.Key;
                }
            }

            return temiz;
        }

        private static string TurkceUpperNormalize(string metin)
        {
            return metin.ToUpperInvariant()
                .Replace("İ", "I").Replace("Ş", "S").Replace("Ç", "C")
                .Replace("Ğ", "G").Replace("Ü", "U").Replace("Ö", "O")
                .Replace("ı", "I").Replace("ş", "S").Replace("ç", "C")
                .Replace("ğ", "G").Replace("ü", "U").Replace("ö", "O");
        }

        private class TextBilgisi
        {
            public string Metin { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }
    }
}
