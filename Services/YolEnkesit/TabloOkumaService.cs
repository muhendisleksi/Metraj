using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    public class TabloOkumaService : ITabloOkumaService
    {
        private readonly IDocumentContext _documentContext;
        private readonly EntityCacheService _cacheService;
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

        public TabloOkumaService(IDocumentContext documentContext, EntityCacheService cacheService)
        {
            _documentContext = documentContext;
            _cacheService = cacheService;
        }

        private int _logSayaci;

        public Dictionary<string, double> TabloOku(KesitGrubu kesit)
        {
            var degerler = new Dictionary<string, double>();

            if (kesit.TextObjeler == null || kesit.TextObjeler.Count == 0)
            {
                if (_logSayaci < 3) LoggingService.Warning($"TabloOku: {kesit.Anchor?.IstasyonMetni} — TextObjeler bos!");
                return degerler;
            }

            var textler = TextleriOku(kesit.TextObjeler);
            bool tanilama = _logSayaci < 3;

            // ===== DETAYLI TANILAMA =====
            if (tanilama && textler.Count > 0)
            {
                LoggingService.Info($"=== TABLO TEXT TANILAMA: {kesit.Anchor?.IstasyonMetni} ({textler.Count} text, {kesit.TextObjeler.Count} ObjectId) ===");

                // Tum text'leri listele (50'ye kadar)
                int gosterilecek = Math.Min(textler.Count, 50);
                for (int i = 0; i < gosterilecek; i++)
                {
                    var (metin, x, y) = textler[i];
                    string kisa = metin.Length > 60 ? metin.Substring(0, 60) + "..." : metin;
                    LoggingService.Info($"  [{i,2}] X={x:F1} Y={y:F1} \"{kisa}\"");
                }
                if (textler.Count > 50)
                    LoggingService.Info($"  ... ve {textler.Count - 50} text daha");

                // Anahtar kelime eslesmesi tanilama
                LoggingService.Info($"  --- ANAHTAR KELIME TARAMASI ---");
                int eslesenText = 0;
                foreach (var (metin, x, y) in textler)
                {
                    string upper = metin.ToUpperInvariant();
                    foreach (var (malzeme, anahtarlar) in MalzemeAnahtarlar)
                    {
                        string bulunanAnahtar = anahtarlar.FirstOrDefault(a => upper.Contains(a));
                        if (bulunanAnahtar != null)
                        {
                            double? deger = SayiCikar(metin);
                            LoggingService.Info($"    ESLESTI: \"{metin}\" -> {malzeme} (anahtar:{bulunanAnahtar}), sayi={deger?.ToString("F2") ?? "YOK"}");
                            eslesenText++;
                            break;
                        }
                    }
                }
                if (eslesenText == 0)
                    LoggingService.Warning($"    HICBIR TEXT ANAHTAR KELIME ICERMIYOR!");

                // Saf sayi text'leri
                var sayilar = new List<(string metin, double deger, double x, double y)>();
                foreach (var (metin, x, y) in textler)
                {
                    if (double.TryParse(metin.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double sayi))
                    {
                        sayilar.Add((metin, sayi, x, y));
                    }
                }
                LoggingService.Info($"  --- SAF SAYI TEXT'LERI: {sayilar.Count} ---");
                foreach (var (metin, deger, x, y) in sayilar.Take(20))
                    LoggingService.Info($"    X={x:F1} Y={y:F1} deger={deger:F4} (\"{metin}\")");

                _logSayaci++;
            }

            // ===== YONTEM 1: Ayni text'te anahtar + sayi =====
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

            if (tanilama)
                LoggingService.Info($"  Yontem 1 sonuc: {degerler.Count} malzeme — {string.Join(", ", degerler.Select(d => $"{d.Key}={d.Value:F2}"))}");

            // ===== YONTEM 2: Ayri text objeleri (malzeme adi + yakin sayi) =====
            // Yontem 1'de bulunamayan malzemeler icin de calistir
            {
                var malzemeTextleri = new List<(string malzeme, double x, double y)>();
                var sayiTextleri = new List<(double deger, double x, double y)>();

                foreach (var (metin, x, y) in textler)
                {
                    string upper = metin.ToUpperInvariant();

                    foreach (var (malzeme, anahtarlar) in MalzemeAnahtarlar)
                    {
                        if (anahtarlar.Any(a => upper.Contains(a)))
                        {
                            malzemeTextleri.Add((malzeme, x, y));
                            break;
                        }
                    }

                    // Saf sayi veya birimli sayi: "16.27", "16,27", "0.00", "16.27 m²"
                    string temizMetin = Regex.Replace(metin, @"\s*m[²2]?\s*$", "").Trim();
                    if (double.TryParse(temizMetin.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double sayi))
                    {
                        sayiTextleri.Add((sayi, x, y));
                    }
                }

                if (tanilama)
                    LoggingService.Info($"  Yontem 2 adaylari: {malzemeTextleri.Count} malzeme text, {sayiTextleri.Count} sayi text");

                foreach (var (malzeme, mx, my) in malzemeTextleri)
                {
                    if (degerler.ContainsKey(malzeme)) continue;

                    // Ayni satirda (Y ±0.3) ve malzemenin SAGINDA olan en yakin sayiyi al
                    // Grid degerleri (140.00, 142.00) malzemenin solundadir — eslesmemeleri icin s.x > mx
                    var enYakin = sayiTextleri
                        .Where(s => Math.Abs(s.y - my) < 0.3 && s.x > mx)
                        .OrderBy(s => s.x - mx)
                        .FirstOrDefault();

                    if (enYakin.deger > 0 || (enYakin.x != 0 && enYakin.y != 0))
                    {
                        degerler[malzeme] = enYakin.deger;
                        if (tanilama) LoggingService.Info($"    Y2 eslesti: {malzeme} = {enYakin.deger:F2} (sag, dx={enYakin.x - mx:F1})");
                    }
                    else
                    {
                        // Fallback: genis Y toleransi (±1.5) ile saga bak
                        enYakin = sayiTextleri
                            .Where(s => Math.Abs(s.y - my) < 1.5 && s.x > mx && s.x - mx < 10)
                            .OrderBy(s => Math.Abs(s.y - my))
                            .FirstOrDefault();

                        if (enYakin.deger > 0 || (enYakin.x != 0 && enYakin.y != 0))
                        {
                            degerler[malzeme] = enYakin.deger;
                            if (tanilama) LoggingService.Info($"    Y2 eslesti: {malzeme} = {enYakin.deger:F2} (genis, dx={enYakin.x - mx:F1}, dy={Math.Abs(enYakin.y - my):F1})");
                        }
                        else if (tanilama)
                        {
                            LoggingService.Warning($"    Y2 BASARISIZ: {malzeme} pos=({mx:F1},{my:F1}) — sagda sayi bulunamadi");
                        }
                    }
                }
            }

            if (degerler.Count > 0)
                LoggingService.Info($"Tablo okuma {kesit.Anchor?.IstasyonMetni}: {degerler.Count} malzeme — {string.Join(", ", degerler.Select(d => $"{d.Key}={d.Value:F2}"))}");
            else if (tanilama)
                LoggingService.Warning($"Tablo okuma {kesit.Anchor?.IstasyonMetni}: HICBIR MALZEME BULUNAMADI ({textler.Count} text taranmasina ragmen)");

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
                        Uyumlu = farkYuzde <= UyumToleransYuzde,
                        UstCizgiRolu = hesap.UstCizgiRolu,
                        AltCizgiRolu = hesap.AltCizgiRolu,
                        Karar = farkYuzde <= UyumToleransYuzde ? KararDurumu.OtomatikOnay : KararDurumu.Bekliyor
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
            _logSayaci = 0;
            foreach (var kesit in kesitler)
                Kiyasla(kesit);

            int uyumlu = kesitler.Count(k => k.Durum == DogrulamaDurumu.Onaylandi);
            int sorunlu = kesitler.Count(k => k.Durum == DogrulamaDurumu.Sorunlu);
            LoggingService.Info($"Toplu kiyaslama: {uyumlu} uyumlu, {sorunlu} sorunlu");
        }

        private List<(string metin, double x, double y)> TextleriOku(List<ObjectId> textIds)
        {
            var sonuc = new List<(string, double, double)>();
            var cache = _cacheService.Cache;

            // Cache varsa cache'den oku (Transaction yok)
            if (cache != null)
            {
                foreach (var id in textIds)
                {
                    if (cache.TryGetValue(id.Handle.Value, out var cached) && cached.Textler != null)
                        sonuc.AddRange(cached.Textler);
                }
                return sonuc;
            }

            // Fallback: cache yoksa eski yontem (kalibrasyon ekranindan cagrildiginda)
            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in textIds)
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);

                    if (obj is DBText dbText)
                    {
                        if (!string.IsNullOrWhiteSpace(dbText.TextString))
                            sonuc.Add((dbText.TextString.Trim(), dbText.Position.X, dbText.Position.Y));
                    }
                    else if (obj is MText mText)
                    {
                        if (!string.IsNullOrWhiteSpace(mText.Contents))
                        {
                            string temiz = MTextIcerikTemizle(mText.Contents);
                            if (!string.IsNullOrWhiteSpace(temiz))
                                sonuc.Add((temiz, mText.Location.X, mText.Location.Y));
                        }
                    }
                    else if (obj is Table table)
                    {
                        TabloHucreleriniOku(table, sonuc);
                    }
                    else if (obj is BlockReference blkRef)
                    {
                        BlokIciTextleriOku(blkRef, tr, sonuc);
                    }
                }

                tr.Commit();
            }

            return sonuc;
        }

        /// <summary>
        /// BlockReference icindeki tum text entity'lerini okur.
        /// MVS, MVS_1 gibi malzeme tablosu bloklarinin icerigini cikarir.
        /// Blok tanimindaki entity'ler + attribute'lar okunur.
        /// Pozisyonlar blok insert noktasina gore WCS'ye donusturulur.
        /// </summary>
        private void BlokIciTextleriOku(BlockReference blkRef, Transaction tr, List<(string, double, double)> sonuc)
        {
            try
            {
                // 1. Attribute'lari oku (varsa)
                foreach (ObjectId attId in blkRef.AttributeCollection)
                {
                    var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (att != null && !string.IsNullOrWhiteSpace(att.TextString))
                        sonuc.Add((att.TextString.Trim(), att.Position.X, att.Position.Y));
                }

                // 2. Blok tanimindaki entity'leri oku
                var btr = tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return;

                // Blok transform: blok icindeki yerel koordinatlari WCS'ye cevir
                var xform = blkRef.BlockTransform;

                foreach (ObjectId entId in btr)
                {
                    var ent = tr.GetObject(entId, OpenMode.ForRead);

                    string metin = null;
                    Point3d pos = Point3d.Origin;

                    if (ent is DBText dt)
                    {
                        metin = dt.TextString;
                        pos = dt.Position;
                    }
                    else if (ent is MText mt)
                    {
                        metin = MTextIcerikTemizle(mt.Contents);
                        pos = mt.Location;
                    }
                    else if (ent is AttributeDefinition attDef)
                    {
                        // Attribute definition'lar blok taniminda durur, deger attribute reference'ta
                        // Zaten yukarida AttributeCollection'dan okundular, atla
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(metin)) continue;

                    // Yerel koordinati WCS'ye donustur
                    var wcsPt = pos.TransformBy(xform);
                    sonuc.Add((metin.Trim(), wcsPt.X, wcsPt.Y));
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning($"Blok ici okuma hatasi ({blkRef.Name}): {ex.Message}");
            }
        }

        /// <summary>AutoCAD Table entity'sinin tum hucrelerini text olarak cikarir.</summary>
        private void TabloHucreleriniOku(Table table, List<(string, double, double)> sonuc)
        {
            try
            {
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    for (int col = 0; col < table.Columns.Count; col++)
                    {
                        string metin = null;
                        try { metin = table.GetTextString(row, col, 0); }
                        catch { continue; }

                        if (string.IsNullOrWhiteSpace(metin)) continue;

                        // Hucre pozisyonu tahmini: tablo pozisyonu bazli
                        double x = table.Position.X + col * 5.0;
                        double y = table.Position.Y - row * 1.5;

                        sonuc.Add((metin.Trim(), x, y));
                    }
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning($"Table okuma hatasi: {ex.Message}");
            }
        }

        private double? SayiCikar(string metin)
        {
            // "Yarma = 16.27" veya "Yarma | 16.27" veya "Yarma : 16.27"
            var match = Regex.Match(metin, @"[=:]\s*([\d.,]+)");
            if (!match.Success)
            {
                // "Yarma  16.27" (boslukla ayrilmis)
                match = Regex.Match(metin, @"\s+([\d]+[.,]\d+)\s*[m²]*\s*$");
            }
            if (!match.Success)
            {
                // Sadece sonundaki sayi (birim olabilir)
                match = Regex.Match(metin, @"([\d]+[.,]\d+)\s*[m²]*\s*$");
            }
            if (!match.Success)
            {
                // Tam sayi (ondalik yok)
                match = Regex.Match(metin, @"\s+(\d+)\s*[m²]*\s*$");
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

        /// <summary>
        /// MText formatlama kodlarini temizleyerek duz metin dondurur.
        /// MText.Contents formatlar icerir: {\fArial|b0|i0;text}, \A1;, \P vb.
        /// </summary>
        internal static string MTextIcerikTemizle(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            // {\fFont|b0|i0;icerik} bloklarindan sadece icerigi al
            var s = Regex.Replace(raw, @"\{\\[^;]+;([^}]*)\}", "$1");
            // Standalone format kodlari: \A1; \P \~ vb.
            s = Regex.Replace(s, @"\\[APHLOQTWpf][^;]*;", "");
            s = s.Replace("\\~", " ");
            s = s.Replace("\\P", " ");
            // Kalan escape'leri temizle
            s = Regex.Replace(s, @"\\.", "");
            s = s.Replace("{", "").Replace("}", "");
            return s.Trim();
        }

        /// <summary>
        /// Tanilama raporuna text detaylarini yazar.
        /// Anahtar kelime iceren ve sayisal deger iceren text'leri listeler.
        /// Hicbiri yoksa ilk 20 text'i gosterir.
        /// </summary>
        public void TabloTanilamaYaz(KesitGrubu kesit, StringBuilder sb)
        {
            if (kesit.TextObjeler == null || kesit.TextObjeler.Count == 0)
            {
                sb.AppendLine("      UYARI: Hic text bulunamadi — tablo okuma calismaz!");
                return;
            }

            var textler = TextleriOku(kesit.TextObjeler);
            sb.AppendLine($"      ObjectId sayisi: {kesit.TextObjeler.Count}, okunan text: {textler.Count}");

            if (textler.Count == 0)
            {
                sb.AppendLine("      UYARI: ObjectId'ler var ama icerik okunamadi!");
                return;
            }

            // Anahtar kelime listesi (malzeme + genel tablo kelimeleri)
            var genelAnahtarlar = new[] { "KM", "ALAN", "MALZEME", "B.T.", "M2", "M²" };

            // Filtreli text'ler: anahtar kelime iceren veya sayisal deger iceren
            var filtreliler = new List<(string metin, double x, double y, string neden)>();
            foreach (var (metin, x, y) in textler)
            {
                string upper = metin.ToUpperInvariant();

                // Malzeme anahtar kelime kontrolu
                foreach (var (malzeme, anahtarlar) in MalzemeAnahtarlar)
                {
                    string bulunan = anahtarlar.FirstOrDefault(a => upper.Contains(a));
                    if (bulunan != null)
                    {
                        filtreliler.Add((metin, x, y, $"malzeme:{malzeme}"));
                        break;
                    }
                }

                // Genel anahtar kelimeler
                if (!filtreliler.Any(f => f.metin == metin && f.x == x))
                {
                    string genelBulunan = genelAnahtarlar.FirstOrDefault(a => upper.Contains(a));
                    if (genelBulunan != null)
                        filtreliler.Add((metin, x, y, $"anahtar:{genelBulunan}"));
                }

                // Sayisal deger kontrolu
                if (!filtreliler.Any(f => f.metin == metin && f.x == x))
                {
                    string temiz = Regex.Replace(metin, @"\s*m[²2]?\s*$", "").Trim();
                    if (double.TryParse(temiz.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                    {
                        filtreliler.Add((metin, x, y, "sayi"));
                    }
                }
            }

            if (filtreliler.Count > 0)
            {
                sb.AppendLine($"      Filtreli text'ler ({filtreliler.Count}/{textler.Count}):");
                foreach (var (metin, x, y, neden) in filtreliler)
                {
                    string kisa = metin.Length > 40 ? metin.Substring(0, 40) + "..." : metin;
                    sb.AppendLine($"        X={x:F1} Y={y:F1} [{neden}] \"{kisa}\"");
                }
            }
            else
            {
                sb.AppendLine($"      UYARI: Hicbir text anahtar kelime veya sayi icermiyor!");
                sb.AppendLine($"      Ilk {Math.Min(textler.Count, 20)} text (debug):");
                foreach (var (metin, x, y) in textler.Take(20))
                {
                    string kisa = metin.Length > 50 ? metin.Substring(0, 50) + "..." : metin;
                    sb.AppendLine($"        X={x:F1} Y={y:F1} \"{kisa}\"");
                }
            }

            // Kiyaslama sonucu ozeti
            if (kesit.TabloKiyaslari != null && kesit.TabloKiyaslari.Count > 0)
            {
                sb.AppendLine($"      Parse sonucu: {kesit.TabloKiyaslari.Count} malzeme eslestirildi");
            }
            else
            {
                sb.AppendLine($"      Parse sonucu: HICBIR MALZEME ESLESTIRILEMEDI");
            }

            // Entity tipi dagilimi — cache'den veya Transaction'dan
            sb.AppendLine($"      --- ENTITY TIP DAGILIMI ---");
            try
            {
                var tipSayilari = new Dictionary<string, int>();
                int malzemeIcerenSayisi = 0;
                var cache = _cacheService.Cache;

                foreach (var id in kesit.TextObjeler)
                {
                    string tipAdi;
                    string icerik = null;

                    if (cache != null && cache.TryGetValue(id.Handle.Value, out var cached))
                    {
                        // Cache'den tip bilgisi
                        switch (cached.Kategori)
                        {
                            case EntityKategori.Text: tipAdi = "Text"; break;
                            case EntityKategori.Tablo:
                                tipAdi = "Table";
                                sb.AppendLine($"        TABLE: Pos=({cached.MinX:F1},{cached.MinY:F1})");
                                break;
                            case EntityKategori.Blok:
                                tipAdi = "BlockReference";
                                sb.AppendLine($"        BLOCK: Pos=({cached.MinX:F1},{cached.MinY:F1})");
                                break;
                            default: tipAdi = "Diger"; break;
                        }

                        // Icerik kontrolu — text'lerin birlesimi
                        if (cached.Textler != null && cached.Textler.Count > 0)
                            icerik = string.Join(" ", cached.Textler.ConvertAll(t => t.metin));
                    }
                    else
                    {
                        tipAdi = "Bilinmiyor";
                    }

                    if (!tipSayilari.ContainsKey(tipAdi)) tipSayilari[tipAdi] = 0;
                    tipSayilari[tipAdi]++;

                    if (icerik != null)
                    {
                        string upper = icerik.ToUpperInvariant();
                        bool malzemeVar = upper.Contains("YARMA") || upper.Contains("DOLGU")
                            || upper.Contains("ASINMA") || upper.Contains("BINDER")
                            || upper.Contains("BITUM") || upper.Contains("PLENT")
                            || upper.Contains("ALTTEMEL") || upper.Contains("GRANU")
                            || upper.Contains("KIRMA") || upper.Contains("SIYIR")
                            || upper.Contains("MALZEME") || upper.Contains("ALAN");
                        if (malzemeVar)
                        {
                            malzemeIcerenSayisi++;
                            string kisa = icerik.Length > 50 ? icerik.Substring(0, 50) + "..." : icerik;
                            sb.AppendLine($"        ** {tipAdi}: \"{kisa}\"");
                        }
                    }
                }

                foreach (var (tip, sayi) in tipSayilari)
                    sb.AppendLine($"      {tip}: {sayi}");
                sb.AppendLine($"      Malzeme kelimesi iceren: {malzemeIcerenSayisi}");

                if (malzemeIcerenSayisi == 0)
                    sb.AppendLine($"      UYARI: Hicbir text entity'si malzeme kelimesi icermiyor — tablo bu pencerede yok olabilir!");
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"      Entity tip taramasi hatasi: {ex.Message}");
            }
        }
    }
}
