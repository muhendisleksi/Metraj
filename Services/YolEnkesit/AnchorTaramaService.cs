using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.YolEnkesit;
using Metraj.Services;

namespace Metraj.Services.YolEnkesit
{
    public class AnchorTaramaService : IAnchorTaramaService
    {
        private readonly IDocumentContext _documentContext;

        public AnchorTaramaService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public List<AnchorNokta> AnchorTara(IEnumerable<ObjectId> entityIds)
        {
            var adaylar = new List<AnchorNokta>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in entityIds)
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
                    else
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(metin)) continue;

                    double istasyon = IstasyonParseKati(metin);
                    if (istasyon < 0) continue;

                    adaylar.Add(new AnchorNokta
                    {
                        Istasyon = istasyon,
                        IstasyonMetni = metin.Trim(),
                        X = x,
                        Y = y,
                        TextId = id
                    });
                }

                tr.Commit();
            }

            LoggingService.Info($"Anchor aday sayisi: {adaylar.Count}");

            // Duplike temizle (ayni km'de birden fazla text)
            var temiz = DuplikeTemizle(adaylar);

            // Aralik kontrolu: mantikli aralikta olanlari filtrele
            var sonuc = AralikFiltrele(temiz);

            LoggingService.Info($"Anchor tarama sonuc: {adaylar.Count} aday -> {sonuc.Count} istasyon");
            return sonuc;
        }

        /// <summary>
        /// Kati istasyon parse: sadece "0+000", "0+010", "1+200" gibi tam istasyon formatlarini kabul eder.
        /// "0+000.39", "122.44", "-2.00%" gibi degerler REDDEDILIR.
        /// </summary>
        private double IstasyonParseKati(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return -1;
            string temiz = metin.Trim();

            // Yuzde, birim, ozel karakter iceren metinleri hemen reddet
            if (temiz.Contains("%") || temiz.Contains("=") || temiz.Contains(":") ||
                temiz.Contains("m2") || temiz.Contains("m²") || temiz.Contains("m³"))
                return -1;

            // Onekleri temizle
            temiz = Regex.Replace(temiz, @"^(Km|KM|km|Ist|IST|ist|İst|İST)[:\s]*", "", RegexOptions.IgnoreCase);

            // Kati format: N+NNN veya N+NNN.00 (ondalik kisim yoksa veya .00 ise kabul)
            var match = Regex.Match(temiz, @"^(\d+)\+(\d{3})(\.(\d+))?$");
            if (!match.Success) return -1;

            // Ondalik kontrolu: .00 veya ondalik yok kabul, diger (.39, .52 vb.) reddet
            if (match.Groups[4].Success)
            {
                string ondalik = match.Groups[4].Value;
                // Sadece .0, .00, .000 kabul
                if (ondalik.TrimEnd('0').Length > 0)
                    return -1;
            }

            double km = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            double m = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            return km * 1000 + m;
        }

        private List<AnchorNokta> DuplikeTemizle(List<AnchorNokta> anchorlar)
        {
            if (anchorlar.Count <= 1) return anchorlar.OrderBy(a => a.Istasyon).ToList();

            var sonuc = new List<AnchorNokta>();
            // Ayni istasyon degerine sahip anchor'lari grupla (1m tolerans)
            var sirali = anchorlar.OrderBy(a => a.Istasyon).ToList();
            var gruplar = new List<List<AnchorNokta>>();
            var mevcutGrup = new List<AnchorNokta> { sirali[0] };

            for (int i = 1; i < sirali.Count; i++)
            {
                if (Math.Abs(sirali[i].Istasyon - mevcutGrup[0].Istasyon) < 1.0)
                {
                    mevcutGrup.Add(sirali[i]);
                }
                else
                {
                    gruplar.Add(mevcutGrup);
                    mevcutGrup = new List<AnchorNokta> { sirali[i] };
                }
            }
            gruplar.Add(mevcutGrup);

            foreach (var grup in gruplar)
            {
                if (grup.Count == 1)
                {
                    sonuc.Add(grup[0]);
                }
                else
                {
                    // Grup icerisinden en belirgin olani sec (en kisa metin = en temiz istasyon)
                    var enIyi = grup.OrderBy(a => a.IstasyonMetni.Length).First();
                    sonuc.Add(enIyi);
                }
            }

            return sonuc.OrderBy(a => a.Istasyon).ToList();
        }

        /// <summary>
        /// Mantikli aralikta olan istasyonlari filtreler.
        /// Gercek istasyonlar genelde 10m veya 20m araliklidir.
        /// 0.39m, 0.52m gibi araliklar gercek degildir.
        /// </summary>
        private List<AnchorNokta> AralikFiltrele(List<AnchorNokta> anchorlar)
        {
            if (anchorlar.Count <= 3) return anchorlar;

            // En sik aralik degerini bul (dominant aralik)
            var araliklar = new List<double>();
            for (int i = 1; i < anchorlar.Count; i++)
            {
                double aralik = anchorlar[i].Istasyon - anchorlar[i - 1].Istasyon;
                if (aralik > 0.5) // cok kucuk araliklari say
                    araliklar.Add(Math.Round(aralik));
            }

            if (araliklar.Count == 0) return anchorlar;

            // En sik gorülen aralik
            double dominantAralik = araliklar.GroupBy(a => a)
                .OrderByDescending(g => g.Count())
                .First().Key;

            LoggingService.Info($"Dominant istasyon araligi: {dominantAralik}m");

            // Dominant aralik 5m'den kucukse veri hatali, filtreleme yapma
            if (dominantAralik < 5) return anchorlar;

            // Dominant araligin katlarina uyan istasyonlari filtrele
            // Tolerans: dominant araligin %30'u
            double tolerans = dominantAralik * 0.3;
            var sonuc = new List<AnchorNokta> { anchorlar[0] };

            for (int i = 1; i < anchorlar.Count; i++)
            {
                double aralik = anchorlar[i].Istasyon - sonuc.Last().Istasyon;
                double katSayisi = Math.Round(aralik / dominantAralik);

                if (katSayisi >= 1 && Math.Abs(aralik - katSayisi * dominantAralik) < tolerans)
                {
                    sonuc.Add(anchorlar[i]);
                }
                else
                {
                    LoggingService.Warning($"Istasyon filtrelendi: {anchorlar[i].IstasyonMetni} (aralik: {aralik:F1}m)");
                }
            }

            return sonuc;
        }
    }
}
