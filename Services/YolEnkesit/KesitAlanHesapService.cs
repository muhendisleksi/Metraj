using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models.YolEnkesit;
using Metraj.Services.Interfaces;

namespace Metraj.Services.YolEnkesit
{
    public partial class KesitAlanHesapService : IKesitAlanHesapService
    {
        private readonly IEnKesitAlanService _enKesitAlanService;
        private int _logSayaci;

        // Dikey cizgi esigi: Y/X orani bundan buyukse dikey kabul edilir
        private const double DikeyOranEsigi = 5.0;
        // Nokta birlesme esigi: bu mesafeden yakin noktalar tek noktaya dusurulur
        private const double NoktaBirlesmeToleransi = 0.01;

        public KesitAlanHesapService(IEnKesitAlanService enKesitAlanService)
        {
            _enKesitAlanService = enKesitAlanService;
        }

        // =============== CIZGI BIRLESTIRME ===============

        /// <summary>
        /// Ayni roldeki tum yatay cizgilerin noktalarini birlestirerek tek sureli cizgi olusturur.
        /// Turk yol enkesitlerinde tabaka cizgileri CL'de ikiye bolunmus cizilir.
        /// Bu metot sol ve sag yarilari birlestirip tam platform genisliginde cizgi dondurur.
        /// Dikey cizgiler (hendek kenari vb.) filtrelenir.
        /// </summary>
        private List<Point2d> RolNoktalariniAl(KesitGrubu kesit, CizgiRolu rol)
        {
            var cizgiler = kesit.Cizgiler.Where(c => c.Rol == rol).ToList();
            if (cizgiler.Count == 0) return null;
            if (cizgiler.Count == 1) return cizgiler[0].Noktalar;

            // Dikey cizgileri filtrele (Y/X orani > 5 = dikey eleman, tabaka degil)
            var yataylar = cizgiler.Where(c =>
            {
                if (c.Noktalar.Count < 2) return false;
                double xRange = c.Noktalar.Max(p => p.X) - c.Noktalar.Min(p => p.X);
                if (xRange < 0.01) return false;
                double yRange = c.Noktalar.Max(p => p.Y) - c.Noktalar.Min(p => p.Y);
                return yRange / xRange <= DikeyOranEsigi;
            }).ToList();

            // Hepsi dikey ise, en genis X araligina sahip olani don
            if (yataylar.Count == 0)
                return cizgiler.OrderByDescending(c => c.Noktalar.Max(p => p.X) - c.Noktalar.Min(p => p.X)).First().Noktalar;

            if (yataylar.Count == 1) return yataylar[0].Noktalar;

            // Birden fazla yatay cizgi: noktalarini birlestir
            int oncekiToplamNokta = yataylar.Sum(c => c.Noktalar.Count);
            var sonuc = NoktalariMerge(yataylar);

            // Tanilama logu — birlesme oncesi/sonrasi nokta farki
            if (_logSayaci < 3)
            {
                LoggingService.Info($"  [MERGE-TANILAMA] {rol}: {cizgiler.Count} parca ({yataylar.Count} yatay + {cizgiler.Count - yataylar.Count} dikey)");
                foreach (var c in yataylar)
                    LoggingService.Info($"    parca: {c.Noktalar.Count} pnt, X=[{c.Noktalar.Min(p => p.X):F2}..{c.Noktalar.Max(p => p.X):F2}]");
                LoggingService.Info($"    MERGE: {oncekiToplamNokta} -> {sonuc.Count} pnt, X=[{sonuc.Min(p => p.X):F2}..{sonuc.Max(p => p.X):F2}]");
                if (oncekiToplamNokta - sonuc.Count > 2)
                    LoggingService.Warning($"    UYARI: {oncekiToplamNokta - sonuc.Count} nokta kayboldu!");
            }

            return sonuc;
        }

        /// <summary>
        /// Birden fazla cizginin noktalarini X'e gore siralayip yakin noktalari birlestirir.
        /// CL'de uc uca eklenen sol/sag yarilari tek surekli cizgiye donusturur.
        /// </summary>
        private List<Point2d> NoktalariMerge(List<CizgiTanimi> cizgiler)
        {
            var tumNoktalar = cizgiler.SelectMany(c => c.Noktalar).OrderBy(p => p.X).ToList();

            var birlesik = new List<Point2d>(tumNoktalar.Count);
            birlesik.Add(tumNoktalar[0]);

            for (int i = 1; i < tumNoktalar.Count; i++)
            {
                var son = birlesik[birlesik.Count - 1];
                var yeni = tumNoktalar[i];

                if (Math.Abs(yeni.X - son.X) < NoktaBirlesmeToleransi)
                {
                    // Ayni X — Y degerlerini ortala (CL bilesim noktasi)
                    birlesik[birlesik.Count - 1] = new Point2d(
                        (son.X + yeni.X) / 2,
                        (son.Y + yeni.Y) / 2);
                    continue;
                }

                birlesik.Add(yeni);
            }

            return birlesik;
        }

        // =============== ALAN HESABI ===============

        /// <summary>
        /// Shoelace (polygon) yontemiyle alan hesabi — TraceBoundary basarisiz oldugunda fallback.
        /// </summary>
        private List<AlanHesapSonucu> ShoelaceAlanHesapla(KesitGrubu kesit)
        {
            var sonuclar = new List<AlanHesapSonucu>();
            bool detayliLog = _logSayaci < 3;

            if (detayliLog)
            {
                LoggingService.Info($"=== ALAN HESAP DETAY: {kesit.Anchor?.IstasyonMetni} ===");
                foreach (var c in kesit.Cizgiler.Where(c => c.Rol != CizgiRolu.Tanimsiz && c.Rol != CizgiRolu.CerceveCizgisi && c.Rol != CizgiRolu.GridCizgisi))
                {
                    double cMinX = c.Noktalar.Min(p => p.X);
                    double cMaxX = c.Noktalar.Max(p => p.X);
                    LoggingService.Info($"  {c.Rol}: {c.LayerAdi}, {c.Noktalar.Count} pnt, X=[{cMinX:F2}..{cMaxX:F2}]");
                }
            }

            // Birlesik noktalar — ayni roldeki parcalari merge et
            var zeminNkt = RolNoktalariniAl(kesit, CizgiRolu.Zemin);
            var siyirmaNkt = RolNoktalariniAl(kesit, CizgiRolu.SiyirmaTaban);
            var ustyapiAltNkt = RolNoktalariniAl(kesit, CizgiRolu.UstyapiAltKotu);

            // UstyapiAltKotu fallback: yoksa en alt tabaka cizgisini kullan
            // Yol platformunun alt siniri = en alttaki ustyapi tabakasi
            if (ustyapiAltNkt == null)
            {
                var fallbackSirasi = new[]
                {
                    CizgiRolu.KirmatasTaban, CizgiRolu.AltTemelTaban, CizgiRolu.PlentmiksTaban,
                    CizgiRolu.BitumluTemelTaban, CizgiRolu.BinderTaban, CizgiRolu.AsinmaTaban
                };
                foreach (var fb in fallbackSirasi)
                {
                    ustyapiAltNkt = RolNoktalariniAl(kesit, fb);
                    if (ustyapiAltNkt != null)
                    {
                        if (detayliLog) LoggingService.Info($"  UstyapiAltKotu YOK -> fallback: {fb}");
                        break;
                    }
                }
            }

            if (detayliLog)
            {
                LogBirlesim(kesit, CizgiRolu.Zemin, zeminNkt);
                LogBirlesim(kesit, CizgiRolu.SiyirmaTaban, siyirmaNkt);
                LogBirlesim(kesit, CizgiRolu.UstyapiAltKotu, ustyapiAltNkt);
            }

            // Siyirma
            if (zeminNkt != null && siyirmaNkt != null)
            {
                double siyirmaAlani = IkiCizgiArasiAlanHesapla(zeminNkt, siyirmaNkt, "Siyirma");
                if (detayliLog) LoggingService.Info($"  Siyirma alani: {siyirmaAlani:F4} m2");
                sonuclar.Add(new AlanHesapSonucu
                {
                    MalzemeAdi = "Siyirma",
                    Alan = siyirmaAlani,
                    UstCizgiRolu = CizgiRolu.Zemin,
                    AltCizgiRolu = CizgiRolu.SiyirmaTaban,
                    Aciklama = "Zemin - Siyirma tabani arasi"
                });
            }

            // Yarma / Dolgu
            if (siyirmaNkt != null && ustyapiAltNkt != null)
                HesaplaYarmaDolgu(siyirmaNkt, ustyapiAltNkt, sonuclar, detayliLog);

            // Ustyapi tabakalari
            HesaplaUstyapiTabakalari(kesit, sonuclar, detayliLog);

            kesit.HesaplananAlanlar = sonuclar;

            if (detayliLog)
            {
                LoggingService.Info($"  TOPLAM: {sonuclar.Count} malzeme hesaplandi");
                foreach (var s in sonuclar)
                    LoggingService.Info($"    {s.MalzemeAdi} = {s.Alan:F4} m2");
            }

            _logSayaci++;
            return sonuclar;
        }

        public void TopluAlanHesapla(List<KesitGrubu> kesitler)
        {
            _logSayaci = 0;
            foreach (var kesit in kesitler)
                AlanHesapla(kesit);

            LoggingService.Info($"Toplu alan hesabi: {kesitler.Count} kesit hesaplandi");
        }

        private void LogBirlesim(KesitGrubu kesit, CizgiRolu rol, List<Point2d> birlesik)
        {
            int parca = kesit.Cizgiler.Count(c => c.Rol == rol);
            if (parca <= 1 || birlesik == null) return;
            LoggingService.Info($"  {rol}: {parca} parca birlesti -> {birlesik.Count} nokta, X=[{birlesik.Min(p => p.X):F2}..{birlesik.Max(p => p.X):F2}]");
        }

        private void HesaplaYarmaDolgu(List<Point2d> siyirmaNkt, List<Point2d> ustyapiAltNkt, List<AlanHesapSonucu> sonuclar, bool detayliLog)
        {
            double minX = Math.Max(siyirmaNkt.Min(p => p.X), ustyapiAltNkt.Min(p => p.X));
            double maxX = Math.Min(siyirmaNkt.Max(p => p.X), ustyapiAltNkt.Max(p => p.X));

            if (detayliLog) LoggingService.Info($"  Yarma/Dolgu X araligi: [{minX:F2}..{maxX:F2}]");

            double? kesisimX = KesisimXBul(siyirmaNkt, ustyapiAltNkt, minX, maxX);

            if (kesisimX.HasValue)
            {
                if (detayliLog) LoggingService.Info($"  Kesisim X: {kesisimX:F2}");

                double yarmaAlani = BolgeAlanHesapla(siyirmaNkt, ustyapiAltNkt, minX, kesisimX.Value);
                double dolguAlani = BolgeAlanHesapla(ustyapiAltNkt, siyirmaNkt, kesisimX.Value, maxX);

                if (yarmaAlani > 0.0001)
                    sonuclar.Add(new AlanHesapSonucu { MalzemeAdi = "Yarma", Alan = yarmaAlani, UstCizgiRolu = CizgiRolu.SiyirmaTaban, AltCizgiRolu = CizgiRolu.UstyapiAltKotu, Aciklama = "Siyirma tabani > Ustyapi alt kotu bolgesi" });

                if (dolguAlani > 0.0001)
                    sonuclar.Add(new AlanHesapSonucu { MalzemeAdi = "Dolgu", Alan = dolguAlani, UstCizgiRolu = CizgiRolu.UstyapiAltKotu, AltCizgiRolu = CizgiRolu.SiyirmaTaban, Aciklama = "Ustyapi alt kotu > Siyirma tabani bolgesi" });
            }
            else
            {
                double siyirmaOrtY = siyirmaNkt.Average(p => p.Y);
                double ustyapiOrtY = ustyapiAltNkt.Average(p => p.Y);
                string malzeme = siyirmaOrtY > ustyapiOrtY ? "Yarma" : "Dolgu";
                double tamAlan = IkiCizgiArasiAlanHesapla(siyirmaNkt, ustyapiAltNkt, malzeme);

                if (tamAlan > 0.0001)
                    sonuclar.Add(new AlanHesapSonucu { MalzemeAdi = malzeme, Alan = tamAlan, UstCizgiRolu = siyirmaOrtY > ustyapiOrtY ? CizgiRolu.SiyirmaTaban : CizgiRolu.UstyapiAltKotu, AltCizgiRolu = siyirmaOrtY > ustyapiOrtY ? CizgiRolu.UstyapiAltKotu : CizgiRolu.SiyirmaTaban, Aciklama = $"{malzeme} - tam bolge" });
            }
        }

        private void HesaplaUstyapiTabakalari(KesitGrubu kesit, List<AlanHesapSonucu> sonuclar, bool detayliLog)
        {
            var tabakalar = new[]
            {
                (ust: CizgiRolu.ProjeKotu, alt: CizgiRolu.AsinmaTaban, ad: "Asinma"),
                (ust: CizgiRolu.AsinmaTaban, alt: CizgiRolu.BinderTaban, ad: "Binder"),
                (ust: CizgiRolu.BinderTaban, alt: CizgiRolu.BitumluTemelTaban, ad: "Bitumlu Temel"),
                (ust: CizgiRolu.BitumluTemelTaban, alt: CizgiRolu.PlentmiksTaban, ad: "Plentmiks"),
                (ust: CizgiRolu.PlentmiksTaban, alt: CizgiRolu.AltTemelTaban, ad: "Alttemel"),
                (ust: CizgiRolu.AltTemelTaban, alt: CizgiRolu.KirmatasTaban, ad: "Kirmatas"),
            };

            foreach (var (ust, alt, ad) in tabakalar)
            {
                var ustNkt = RolNoktalariniAl(kesit, ust);
                var altNkt = RolNoktalariniAl(kesit, alt);

                if (ustNkt == null || altNkt == null) continue;

                if (detayliLog)
                {
                    LogBirlesim(kesit, ust, ustNkt);
                    LogBirlesim(kesit, alt, altNkt);
                }

                double alan = IkiCizgiArasiAlanHesapla(ustNkt, altNkt, ad);
                if (detayliLog) LoggingService.Info($"  {ad}: {alan:F4} m2 ({ust} -> {alt})");

                if (alan > 0.0001)
                {
                    sonuclar.Add(new AlanHesapSonucu
                    {
                        MalzemeAdi = ad,
                        Alan = alan,
                        UstCizgiRolu = ust,
                        AltCizgiRolu = alt,
                        Aciklama = $"{ad} tabakasi"
                    });
                }
            }
        }

        private double IkiCizgiArasiAlanHesapla(List<Point2d> ustNoktalar, List<Point2d> altNoktalar, string malzemeAdi = null)
        {
            double minX = Math.Max(ustNoktalar.Min(p => p.X), altNoktalar.Min(p => p.X));
            double maxX = Math.Min(ustNoktalar.Max(p => p.X), altNoktalar.Max(p => p.X));

            if (maxX <= minX) return 0;

            var ustKesik = _enKesitAlanService.ClipToXRange(ustNoktalar, minX, maxX);
            var altKesik = _enKesitAlanService.ClipToXRange(altNoktalar, minX, maxX);

            var polygon = new List<Point2d>();
            polygon.AddRange(ustKesik.OrderBy(p => p.X));
            polygon.AddRange(altKesik.OrderByDescending(p => p.X));

            double alan = _enKesitAlanService.ShoelaceAlan(polygon);

            // Tanilama logu — birlesme oncesi/sonrasi nokta kaybi ve polygon bilgisi
            if (_logSayaci < 3 && malzemeAdi != null)
            {
                LoggingService.Info($"  [SH-TANILAMA] {malzemeAdi}:");
                LoggingService.Info($"    Ust girdi: {ustNoktalar.Count} pnt, X=[{ustNoktalar.Min(p => p.X):F2}..{ustNoktalar.Max(p => p.X):F2}]");
                LoggingService.Info($"    Alt girdi: {altNoktalar.Count} pnt, X=[{altNoktalar.Min(p => p.X):F2}..{altNoktalar.Max(p => p.X):F2}]");
                LoggingService.Info($"    Clip X: [{minX:F2}..{maxX:F2}], genislik={maxX - minX:F2}");
                LoggingService.Info($"    Clip sonrasi: ust={ustKesik.Count} pnt, alt={altKesik.Count} pnt");
                LoggingService.Info($"    Polygon: {polygon.Count} pnt, X=[{polygon.Min(p => p.X):F2}..{polygon.Max(p => p.X):F2}], Y=[{polygon.Min(p => p.Y):F2}..{polygon.Max(p => p.Y):F2}]");
                LoggingService.Info($"    Shoelace alan: {alan:F4} m2");
            }

            return alan;
        }

        private double? KesisimXBul(List<Point2d> cizgi1, List<Point2d> cizgi2, double minX, double maxX)
        {
            int adimSayisi = 100;
            double adim = (maxX - minX) / adimSayisi;

            double oncekiFark = 0;
            bool ilk = true;

            for (double x = minX; x <= maxX; x += adim)
            {
                double y1 = _enKesitAlanService.InterpolateY(cizgi1, x);
                double y2 = _enKesitAlanService.InterpolateY(cizgi2, x);
                double fark = y1 - y2;

                if (!ilk && oncekiFark * fark < 0)
                    return IkiliAramaKesisim(cizgi1, cizgi2, x - adim, x);

                oncekiFark = fark;
                ilk = false;
            }

            return null;
        }

        private double IkiliAramaKesisim(List<Point2d> cizgi1, List<Point2d> cizgi2, double solX, double sagX)
        {
            for (int i = 0; i < 50; i++)
            {
                double ortaX = (solX + sagX) / 2;
                double fark = _enKesitAlanService.InterpolateY(cizgi1, ortaX) - _enKesitAlanService.InterpolateY(cizgi2, ortaX);

                if (Math.Abs(fark) < 1e-6) return ortaX;

                double solFark = _enKesitAlanService.InterpolateY(cizgi1, solX) - _enKesitAlanService.InterpolateY(cizgi2, solX);
                if (solFark * fark < 0) sagX = ortaX;
                else solX = ortaX;
            }

            return (solX + sagX) / 2;
        }

        private double BolgeAlanHesapla(List<Point2d> ustNoktalar, List<Point2d> altNoktalar, double minX, double maxX)
        {
            if (maxX <= minX) return 0;

            var ustKesik = _enKesitAlanService.ClipToXRange(ustNoktalar, minX, maxX);
            var altKesik = _enKesitAlanService.ClipToXRange(altNoktalar, minX, maxX);

            var polygon = new List<Point2d>();
            polygon.AddRange(ustKesik.OrderBy(p => p.X));
            polygon.AddRange(altKesik.OrderByDescending(p => p.X));

            return _enKesitAlanService.ShoelaceAlan(polygon);
        }

        // =============== TANILAMA RAPORU ===============

        public string TanilamaRaporuYaz(List<KesitGrubu> kesitler)
        {
            if (kesitler == null || kesitler.Count == 0) return null;

            // 5 orneklem sec: bas, 1/4, 1/2, 3/4, son
            var orneklemIdx = OrneklemIndeksleri(kesitler.Count);
            var sb = new StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
            sb.AppendLine("║        ALAN HESAP TANILAMA RAPORU                       ║");
            sb.AppendLine($"║  Tarih: {DateTime.Now:yyyy-MM-dd HH:mm}                             ║");
            sb.AppendLine($"║  Toplam kesit: {kesitler.Count}                                      ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            foreach (int idx in orneklemIdx)
            {
                var kesit = kesitler[idx];
                sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine($"KESIT #{idx + 1}: {kesit.Anchor?.IstasyonMetni ?? "?"}");
                sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                TanilamaKesitYaz(sb, kesit);
                sb.AppendLine();
            }

            // Dosyaya yaz
            string dosyaAdi = $"AlanHesap_Tanilama_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string klasor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Metraj_Tanilama");
            Directory.CreateDirectory(klasor);
            string tam = Path.Combine(klasor, dosyaAdi);

            File.WriteAllText(tam, sb.ToString(), Encoding.UTF8);
            LoggingService.Info($"Tanilama raporu yazildi: {tam}");
            return tam;
        }

        private List<int> OrneklemIndeksleri(int toplam)
        {
            var idx = new HashSet<int> { 0, toplam - 1 };
            if (toplam > 2) idx.Add(toplam / 4);
            if (toplam > 3) idx.Add(toplam / 2);
            if (toplam > 4) idx.Add(toplam * 3 / 4);
            return idx.OrderBy(i => i).ToList();
        }

        private void TanilamaKesitYaz(StringBuilder sb, KesitGrubu kesit)
        {
            // 1. Tum cizgiler
            sb.AppendLine();
            sb.AppendLine("  [1] CIZGILER");
            sb.AppendLine($"  {"Rol",-20} {"Layer",-25} {"Pnt",4} {"X min",10} {"X max",10} {"Y min",10} {"Y max",10} {"Ort Y",10}");
            sb.AppendLine($"  {new string('-', 99)}");

            var anlamliCizgiler = kesit.Cizgiler
                .Where(c => c.Rol != CizgiRolu.CerceveCizgisi && c.Rol != CizgiRolu.GridCizgisi)
                .OrderByDescending(c => c.OrtalamaY)
                .ToList();

            foreach (var c in anlamliCizgiler)
            {
                double cMinX = c.Noktalar.Count > 0 ? c.Noktalar.Min(p => p.X) : 0;
                double cMaxX = c.Noktalar.Count > 0 ? c.Noktalar.Max(p => p.X) : 0;
                double cMinY = c.Noktalar.Count > 0 ? c.Noktalar.Min(p => p.Y) : 0;
                double cMaxY = c.Noktalar.Count > 0 ? c.Noktalar.Max(p => p.Y) : 0;
                double xAraligi = cMaxX - cMinX;
                double yAraligi = cMaxY - cMinY;
                string tip = (xAraligi < 0.01 || yAraligi / xAraligi > DikeyOranEsigi) ? " [DIKEY]" : "";
                string oto = c.OtomatikAtanmis ? "" : " [MANUEL]";
                sb.AppendLine($"  {c.Rol,-20} {(c.LayerAdi ?? "?"),-25} {c.Noktalar.Count,4} {cMinX,10:F2} {cMaxX,10:F2} {cMinY,10:F2} {cMaxY,10:F2} {c.OrtalamaY,10:F2}{oto}{tip}");
            }

            int cerceveSayisi = kesit.Cizgiler.Count(c => c.Rol == CizgiRolu.CerceveCizgisi || c.Rol == CizgiRolu.GridCizgisi);
            int tanimsizSayisi = kesit.Cizgiler.Count(c => c.Rol == CizgiRolu.Tanimsiz);
            sb.AppendLine($"  (+ {cerceveSayisi} cerceve/grid, {tanimsizSayisi} tanimsiz cizgi gizlendi)");

            // 2. Rol durumu + birlesme bilgisi
            sb.AppendLine();
            sb.AppendLine("  [2] ROL DURUMU (birlestirme)");
            var tumRoller = new[]
            {
                CizgiRolu.Zemin, CizgiRolu.SiyirmaTaban, CizgiRolu.ProjeKotu, CizgiRolu.UstyapiAltKotu,
                CizgiRolu.AsinmaTaban, CizgiRolu.BinderTaban, CizgiRolu.BitumluTemelTaban,
                CizgiRolu.PlentmiksTaban, CizgiRolu.AltTemelTaban, CizgiRolu.KirmatasTaban
            };
            foreach (var rol in tumRoller)
            {
                var parcalar = kesit.Cizgiler.Where(c => c.Rol == rol).ToList();
                if (parcalar.Count == 0)
                {
                    sb.AppendLine($"  {rol,-20}: YOK");
                    continue;
                }

                var birlesik = RolNoktalariniAl(kesit, rol);
                int yataySayisi = parcalar.Count(c =>
                {
                    if (c.Noktalar.Count < 2) return false;
                    double xr = c.Noktalar.Max(p => p.X) - c.Noktalar.Min(p => p.X);
                    if (xr < 0.01) return false;
                    double yr = c.Noktalar.Max(p => p.Y) - c.Noktalar.Min(p => p.Y);
                    return yr / xr <= DikeyOranEsigi;
                });
                int dikeySayisi = parcalar.Count - yataySayisi;

                if (parcalar.Count == 1)
                {
                    sb.AppendLine($"  {rol,-20}: 1 cizgi, {birlesik.Count} pnt, X=[{birlesik.Min(p => p.X):F2}..{birlesik.Max(p => p.X):F2}]");
                }
                else
                {
                    sb.AppendLine($"  {rol,-20}: {parcalar.Count} parca ({yataySayisi} yatay + {dikeySayisi} dikey) -> BIRLESIK {birlesik.Count} pnt, X=[{birlesik.Min(p => p.X):F2}..{birlesik.Max(p => p.X):F2}]");
                }
            }

            // 3. Malzeme hesaplari — birlesik noktalar kullanilarak
            sb.AppendLine();
            sb.AppendLine("  [3] MALZEME HESAPLARI");

            var zeminNkt = RolNoktalariniAl(kesit, CizgiRolu.Zemin);
            var siyirmaNkt = RolNoktalariniAl(kesit, CizgiRolu.SiyirmaTaban);
            var ustyapiAltNkt = RolNoktalariniAl(kesit, CizgiRolu.UstyapiAltKotu);

            // UstyapiAltKotu fallback (tanilama icin)
            if (ustyapiAltNkt == null)
            {
                var fbSirasi = new[] { CizgiRolu.KirmatasTaban, CizgiRolu.AltTemelTaban, CizgiRolu.PlentmiksTaban,
                    CizgiRolu.BitumluTemelTaban, CizgiRolu.BinderTaban, CizgiRolu.AsinmaTaban };
                foreach (var fb in fbSirasi)
                {
                    ustyapiAltNkt = RolNoktalariniAl(kesit, fb);
                    if (ustyapiAltNkt != null)
                    {
                        sb.AppendLine($"  ** UstyapiAltKotu YOK -> fallback: {fb}");
                        break;
                    }
                }
            }

            // Siyirma
            CiftHesapLog(sb, "Siyirma", zeminNkt, siyirmaNkt, CizgiRolu.Zemin, CizgiRolu.SiyirmaTaban, kesit);

            // Yarma/Dolgu
            if (siyirmaNkt != null && ustyapiAltNkt != null)
            {
                double minX = Math.Max(siyirmaNkt.Min(p => p.X), ustyapiAltNkt.Min(p => p.X));
                double maxX = Math.Min(siyirmaNkt.Max(p => p.X), ustyapiAltNkt.Max(p => p.X));
                double overlap = maxX - minX;

                sb.AppendLine($"  --- Yarma/Dolgu ---");
                sb.AppendLine($"      Ust: {CizgiRolu.SiyirmaTaban} X=[{siyirmaNkt.Min(p => p.X):F2}..{siyirmaNkt.Max(p => p.X):F2}]");
                sb.AppendLine($"      Alt: {CizgiRolu.UstyapiAltKotu} X=[{ustyapiAltNkt.Min(p => p.X):F2}..{ustyapiAltNkt.Max(p => p.X):F2}]");
                sb.AppendLine($"      X overlap: [{minX:F2}..{maxX:F2}] = {overlap:F2} birim");

                if (overlap <= 0)
                {
                    sb.AppendLine($"      SONUC: X overlap yok! Alan hesaplanamadi.");
                }
                else
                {
                    double? kesisimX = KesisimXBul(siyirmaNkt, ustyapiAltNkt, minX, maxX);
                    if (kesisimX.HasValue)
                    {
                        double yarma = BolgeAlanHesapla(siyirmaNkt, ustyapiAltNkt, minX, kesisimX.Value);
                        double dolgu = BolgeAlanHesapla(ustyapiAltNkt, siyirmaNkt, kesisimX.Value, maxX);
                        sb.AppendLine($"      Kesisim X: {kesisimX.Value:F2}");
                        sb.AppendLine($"      Yarma alani: {yarma:F4} m2 (X=[{minX:F2}..{kesisimX.Value:F2}])");
                        sb.AppendLine($"      Dolgu alani: {dolgu:F4} m2 (X=[{kesisimX.Value:F2}..{maxX:F2}])");
                    }
                    else
                    {
                        double tamAlan = IkiCizgiArasiAlanHesapla(siyirmaNkt, ustyapiAltNkt);
                        string tip = siyirmaNkt.Average(p => p.Y) > ustyapiAltNkt.Average(p => p.Y) ? "Yarma" : "Dolgu";
                        sb.AppendLine($"      Kesisim yok — tam {tip}: {tamAlan:F4} m2");
                    }
                }
            }
            else
            {
                sb.AppendLine($"  --- Yarma/Dolgu ---");
                sb.AppendLine($"      SONUC: {(siyirmaNkt == null ? "SiyirmaTaban" : "UstyapiAltKotu")} eksik, hesap yapilamadi.");
            }

            // Ustyapi tabakalari
            var tabakalar = new[]
            {
                (ust: CizgiRolu.ProjeKotu, alt: CizgiRolu.AsinmaTaban, ad: "Asinma"),
                (ust: CizgiRolu.AsinmaTaban, alt: CizgiRolu.BinderTaban, ad: "Binder"),
                (ust: CizgiRolu.BinderTaban, alt: CizgiRolu.BitumluTemelTaban, ad: "Bitumlu Temel"),
                (ust: CizgiRolu.BitumluTemelTaban, alt: CizgiRolu.PlentmiksTaban, ad: "Plentmiks"),
                (ust: CizgiRolu.PlentmiksTaban, alt: CizgiRolu.AltTemelTaban, ad: "Alttemel"),
                (ust: CizgiRolu.AltTemelTaban, alt: CizgiRolu.KirmatasTaban, ad: "Kirmatas"),
            };

            foreach (var (ust, alt, ad) in tabakalar)
            {
                var ustNkt = RolNoktalariniAl(kesit, ust);
                var altNkt = RolNoktalariniAl(kesit, alt);
                CiftHesapLog(sb, ad, ustNkt, altNkt, ust, alt, kesit);
            }

            // 3b. TraceBoundary detay
            TBTanilamaKesitYaz(sb, kesit);

            // 4. Tablo text durumu
            sb.AppendLine();
            sb.AppendLine("  [4] TABLO TEXT DURUMU");
            sb.AppendLine($"      Text objesi sayisi: {kesit.TextObjeler?.Count ?? 0}");
            if (kesit.TextObjeler == null || kesit.TextObjeler.Count == 0)
                sb.AppendLine($"      UYARI: Hic text bulunamadi — tablo okuma calismaz!");

            // 5. Hesap sonuclari vs tablo
            if (kesit.HesaplananAlanlar != null && kesit.HesaplananAlanlar.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  [5] HESAP SONUCLARI");
                sb.AppendLine($"  {"Malzeme",-20} {"Hesap (m2)",12} {"Tablo (m2)",12} {"Fark",8} {"Fark %",8}");
                sb.AppendLine($"  {new string('-', 64)}");

                foreach (var h in kesit.HesaplananAlanlar)
                {
                    var kiyas = kesit.TabloKiyaslari?.FirstOrDefault(k => k.MalzemeAdi == h.MalzemeAdi);
                    string tabloStr = kiyas != null ? $"{kiyas.TabloAlani,12:F2}" : $"{"---",12}";
                    string farkStr = kiyas != null ? $"{kiyas.Fark,8:F2}" : $"{"",8}";
                    string yuzdeStr = kiyas != null ? $"{kiyas.FarkYuzde,7:F1}%" : $"{"",8}";
                    sb.AppendLine($"  {h.MalzemeAdi,-20} {h.Alan,12:F2} {tabloStr} {farkStr} {yuzdeStr}");
                }
            }
        }

        private void CiftHesapLog(StringBuilder sb, string ad, List<Point2d> ustNkt, List<Point2d> altNkt, CizgiRolu ustRol, CizgiRolu altRol, KesitGrubu kesit = null)
        {
            sb.AppendLine($"  --- {ad} ---");
            if (ustNkt == null || altNkt == null)
            {
                string eksik = ustNkt == null ? ustRol.ToString() : altRol.ToString();
                sb.AppendLine($"      SONUC: {eksik} eksik, hesap yapilamadi.");
                return;
            }

            // Birlesme oncesi parca bilgisi
            if (kesit != null)
            {
                var ustParcalar = kesit.Cizgiler.Where(c => c.Rol == ustRol).ToList();
                var altParcalar = kesit.Cizgiler.Where(c => c.Rol == altRol).ToList();
                int ustHamNokta = ustParcalar.Sum(c => c.Noktalar.Count);
                int altHamNokta = altParcalar.Sum(c => c.Noktalar.Count);
                sb.AppendLine($"      Ust ham: {ustParcalar.Count} parca, {ustHamNokta} pnt -> birlesik {ustNkt.Count} pnt (kayip: {ustHamNokta - ustNkt.Count})");
                sb.AppendLine($"      Alt ham: {altParcalar.Count} parca, {altHamNokta} pnt -> birlesik {altNkt.Count} pnt (kayip: {altHamNokta - altNkt.Count})");
            }

            double ustMinX = ustNkt.Min(p => p.X), ustMaxX = ustNkt.Max(p => p.X);
            double altMinX = altNkt.Min(p => p.X), altMaxX = altNkt.Max(p => p.X);
            double minX = Math.Max(ustMinX, altMinX);
            double maxX = Math.Min(ustMaxX, altMaxX);
            double overlap = maxX - minX;

            sb.AppendLine($"      Ust: {ustRol} ({ustNkt.Count} pnt) X=[{ustMinX:F2}..{ustMaxX:F2}]");
            sb.AppendLine($"      Alt: {altRol} ({altNkt.Count} pnt) X=[{altMinX:F2}..{altMaxX:F2}]");
            sb.AppendLine($"      X overlap: [{minX:F2}..{maxX:F2}] = {overlap:F2} birim");

            if (overlap <= 0)
            {
                sb.AppendLine($"      SONUC: X overlap yok! Alan = 0");
                return;
            }

            var ustKesik = _enKesitAlanService.ClipToXRange(ustNkt, minX, maxX);
            var altKesik = _enKesitAlanService.ClipToXRange(altNkt, minX, maxX);
            sb.AppendLine($"      Clip sonrasi: ust={ustKesik.Count} pnt, alt={altKesik.Count} pnt");

            var polygon = new List<Point2d>();
            polygon.AddRange(ustKesik.OrderBy(p => p.X));
            polygon.AddRange(altKesik.OrderByDescending(p => p.X));
            sb.AppendLine($"      Polygon: {polygon.Count} pnt, X=[{polygon.Min(p => p.X):F2}..{polygon.Max(p => p.X):F2}], Y=[{polygon.Min(p => p.Y):F2}..{polygon.Max(p => p.Y):F2}]");

            double alan = _enKesitAlanService.ShoelaceAlan(polygon);
            double ortYFark = Math.Abs(ustNkt.Average(p => p.Y) - altNkt.Average(p => p.Y));
            sb.AppendLine($"      Y farki (ortalama): {ortYFark:F4} birim");
            sb.AppendLine($"      SONUC: {alan:F4} m2");

            if (alan < 0.001 && ortYFark > 0.01)
                sb.AppendLine($"      UYARI: Y farki var ama alan ~0 — clip/polygon sorunu olabilir!");
        }
    }
}
