using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.YolEnkesit;
using Metraj.Services;
using Metraj.Services.Interfaces;

namespace Metraj.Services.YolEnkesit
{
    public class AnchorTaramaService : IAnchorTaramaService
    {
        private readonly IDocumentContext _documentContext;
        private readonly IEnKesitAlanService _enKesitAlanService;
        private const double CL_TextEslesmeMesafesi = 8.0;

        public AnchorTaramaService(IDocumentContext documentContext, IEnKesitAlanService enKesitAlanService)
        {
            _documentContext = documentContext;
            _enKesitAlanService = enKesitAlanService;
        }

        public List<AnchorNokta> AnchorTara(IEnumerable<ObjectId> entityIds)
        {
            var dikeyCizgiler = new List<(ObjectId id, double x, double minY, double maxY)>();
            var kmTextleri = new List<(ObjectId id, double istasyon, string metin, double x, double y)>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in entityIds)
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);

                    // Text mi?
                    if (obj is DBText dbText)
                    {
                        double ist = IstasyonParseKati(dbText.TextString);
                        if (ist >= 0)
                            kmTextleri.Add((id, ist, dbText.TextString.Trim(), dbText.Position.X, dbText.Position.Y));
                    }
                    else if (obj is MText mText)
                    {
                        double ist = IstasyonParseKati(mText.Contents);
                        if (ist >= 0)
                            kmTextleri.Add((id, ist, mText.Contents.Trim(), mText.Location.X, mText.Location.Y));
                    }
                    // Dikey cizgi mi?
                    else if (obj is Entity ent && (ent is Polyline || ent is Polyline2d || ent is Polyline3d || ent is Line))
                    {
                        var noktalar = _enKesitAlanService.PolylineNoktalariniAl(id);
                        if (noktalar != null && noktalar.Count >= 2)
                        {
                            double xMin = noktalar.Min(p => p.X);
                            double xMax = noktalar.Max(p => p.X);
                            double yMin = noktalar.Min(p => p.Y);
                            double yMax = noktalar.Max(p => p.Y);
                            double xAraligi = xMax - xMin;
                            double yAraligi = yMax - yMin;

                            // Dikey cizgi: X araligi < 2, Y araligi > 5, Y/X > 5
                            if (xAraligi < 2.0 && yAraligi > 5.0 && (xAraligi < 0.01 || yAraligi / xAraligi > 5))
                            {
                                double ortaX = (xMin + xMax) / 2;
                                dikeyCizgiler.Add((id, ortaX, yMin, yMax));
                            }
                        }
                    }
                }

                tr.Commit();
            }

            LoggingService.Info($"CL aday sayisi: {dikeyCizgiler.Count}, Km text sayisi: {kmTextleri.Count}");

            // Duplike km text temizle
            kmTextleri = DuplikeKmTemizle(kmTextleri);

            // CL + Km text eslestirmesi
            var anchorlar = CL_KmEslestir(dikeyCizgiler, kmTextleri);

            // Aralik filtrele
            anchorlar = AralikFiltrele(anchorlar);

            LoggingService.Info($"Anchor sonuc: {anchorlar.Count} kesit (CL+Km eslesmesi)");
            return anchorlar;
        }

        /// <summary>Platform yari genisligini otomatik tespit eder (ilk kesitin CL yakinindaki yatay cizgilerin genisligi).</summary>
        public double PlatformGenisligiTespit(List<AnchorNokta> anchorlar, IEnumerable<ObjectId> entityIds)
        {
            if (anchorlar.Count == 0) return 30; // varsayilan

            var ilkAnchor = anchorlar[0];
            double maxGenislik = 0;

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in entityIds)
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);
                    if (!(obj is Entity ent)) continue;
                    if (!(ent is Polyline || ent is Line)) continue;

                    var noktalar = _enKesitAlanService.PolylineNoktalariniAl(id);
                    if (noktalar == null || noktalar.Count < 2) continue;

                    double xMin = noktalar.Min(p => p.X);
                    double xMax = noktalar.Max(p => p.X);
                    double yMin = noktalar.Min(p => p.Y);
                    double yMax = noktalar.Max(p => p.Y);

                    // CL'ye yakin yatay cizgiler (Y araligi dar, CL Y araligi icinde)
                    double genislik = xMax - xMin;
                    if (genislik < 3 || genislik > 100) continue; // cok kisa veya cok uzun
                    if (yMax - yMin > genislik * 0.5) continue; // dikey agirlikli, yatay degil

                    // CL'nin X'ine yakin mi?
                    if (Math.Abs((xMin + xMax) / 2 - ilkAnchor.CL_X) > genislik) continue;

                    // CL'nin Y araligi icinde mi?
                    double ortaY = (yMin + yMax) / 2;
                    if (ortaY < ilkAnchor.CL_MinY || ortaY > ilkAnchor.CL_MaxY) continue;

                    if (genislik > maxGenislik)
                        maxGenislik = genislik;
                }

                tr.Commit();
            }

            double yariGenislik = maxGenislik > 0 ? (maxGenislik / 2 + 5) : 30;
            LoggingService.Info($"Platform yari genisligi: {yariGenislik:F1} (max yatay cizgi: {maxGenislik:F1})");
            return yariGenislik;
        }

        private List<AnchorNokta> CL_KmEslestir(
            List<(ObjectId id, double x, double minY, double maxY)> dikeyCizgiler,
            List<(ObjectId id, double istasyon, string metin, double x, double y)> kmTextleri)
        {
            var sonuc = new List<AnchorNokta>();
            var kullanilanCL = new HashSet<long>();

            // Her km text'i icin en yakin CL'yi bul
            foreach (var km in kmTextleri.OrderBy(k => k.istasyon))
            {
                (ObjectId id, double x, double minY, double maxY) enIyiCL = default;
                double enKucukFark = double.MaxValue;

                foreach (var cl in dikeyCizgiler)
                {
                    if (kullanilanCL.Contains(cl.id.Handle.Value)) continue;

                    double xFark = Math.Abs(cl.x - km.x);
                    if (xFark > CL_TextEslesmeMesafesi) continue;

                    // Text CL'nin altinda veya ustunde olmali
                    // (Y farki cok buyukse farkli kesitten geliyordur)
                    double yFark = Math.Min(Math.Abs(km.y - cl.minY), Math.Abs(km.y - cl.maxY));
                    if (yFark > cl.maxY - cl.minY) continue; // Y farki CL yuksekliginden buyukse reddet

                    if (xFark < enKucukFark)
                    {
                        enKucukFark = xFark;
                        enIyiCL = cl;
                    }
                }

                if (enIyiCL.id.IsValid)
                {
                    kullanilanCL.Add(enIyiCL.id.Handle.Value);
                    sonuc.Add(new AnchorNokta
                    {
                        Istasyon = km.istasyon,
                        IstasyonMetni = km.metin,
                        X = enIyiCL.x, // CL'nin X'ini kullan (anchor pozisyonu = CL)
                        Y = (enIyiCL.minY + enIyiCL.maxY) / 2,
                        TextId = km.id,
                        CL_X = enIyiCL.x,
                        CL_MinY = enIyiCL.minY,
                        CL_MaxY = enIyiCL.maxY,
                        CL_EntityId = enIyiCL.id,
                        CL_Dogrulandi = true
                    });
                }
            }

            return sonuc.OrderBy(a => a.Istasyon).ToList();
        }

        private double IstasyonParseKati(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return -1;
            string temiz = metin.Trim();

            if (temiz.Contains("%") || temiz.Contains("=") || temiz.Contains(":") ||
                temiz.Contains("m2") || temiz.Contains("m\u00B2") || temiz.Contains("m\u00B3"))
                return -1;

            temiz = Regex.Replace(temiz, @"^(Km|KM|km|Ist|IST|ist|İst|İST)[:\s]*", "", RegexOptions.IgnoreCase);

            var match = Regex.Match(temiz, @"^(\d+)\+(\d{3})(\.(\d+))?$");
            if (!match.Success) return -1;

            if (match.Groups[4].Success)
            {
                string ondalik = match.Groups[4].Value;
                if (ondalik.TrimEnd('0').Length > 0)
                    return -1;
            }

            double km = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            double m = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            return km * 1000 + m;
        }

        private List<(ObjectId id, double istasyon, string metin, double x, double y)> DuplikeKmTemizle(
            List<(ObjectId id, double istasyon, string metin, double x, double y)> kmTextleri)
        {
            var gruplar = kmTextleri.GroupBy(k => Math.Round(k.istasyon));
            var sonuc = new List<(ObjectId id, double istasyon, string metin, double x, double y)>();

            foreach (var grup in gruplar)
            {
                var enIyi = grup.OrderBy(k => k.metin.Length).First();
                sonuc.Add(enIyi);
            }

            return sonuc.OrderBy(k => k.istasyon).ToList();
        }

        private List<AnchorNokta> AralikFiltrele(List<AnchorNokta> anchorlar)
        {
            if (anchorlar.Count <= 3) return anchorlar;

            var araliklar = new List<double>();
            for (int i = 1; i < anchorlar.Count; i++)
            {
                double aralik = anchorlar[i].Istasyon - anchorlar[i - 1].Istasyon;
                if (aralik > 0.5)
                    araliklar.Add(Math.Round(aralik));
            }

            if (araliklar.Count == 0) return anchorlar;

            double dominantAralik = araliklar.GroupBy(a => a)
                .OrderByDescending(g => g.Count())
                .First().Key;

            LoggingService.Info($"Dominant istasyon araligi: {dominantAralik}m");

            if (dominantAralik < 5) return anchorlar;

            double tolerans = dominantAralik * 0.3;
            var sonuc = new List<AnchorNokta> { anchorlar[0] };

            for (int i = 1; i < anchorlar.Count; i++)
            {
                double aralik = anchorlar[i].Istasyon - sonuc.Last().Istasyon;
                double katSayisi = Math.Round(aralik / dominantAralik);

                if (katSayisi >= 1 && Math.Abs(aralik - katSayisi * dominantAralik) < tolerans)
                    sonuc.Add(anchorlar[i]);
            }

            return sonuc;
        }
    }
}
