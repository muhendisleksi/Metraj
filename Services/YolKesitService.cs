using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
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
        private readonly IEnKesitAlanService _enkesitService;
        private readonly IKatmanEslestirmeService _eslestirmeService;

        public YolKesitService(
            IDocumentContext documentContext,
            IEditorService editorService,
            IEnKesitAlanService enkesitService,
            IKatmanEslestirmeService eslestirmeService)
        {
            _documentContext = documentContext;
            _editorService = editorService;
            _enkesitService = enkesitService;
            _eslestirmeService = eslestirmeService;
        }

        public YolKesitVerisi TekKesitOku()
        {
            // 1. İstasyon bilgisini al
            var (istasyon, istasyonMetni) = IstasyonAl();
            if (istasyon < 0)
                return null;

            var kesit = new YolKesitVerisi
            {
                Istasyon = istasyon,
                IstasyonMetni = istasyonMetni
            };

            _editorService.WriteMessage($"\n--- Km {istasyonMetni} i\u00E7in nesne se\u00E7imi ---\n");
            _editorService.WriteMessage("Nesne tipi se\u00E7in. \u00C7\u0131kmak i\u00E7in 'Bitti' se\u00E7in.\n");

            // 2. Döngüde nesne seçtir
            while (true)
            {
                var tipResult = _editorService.GetKeywords(
                    "\nNesne tipi [KapaliNesne/IkiCizgi/Bitti]: ",
                    new[] { "KapaliNesne", "IkiCizgi", "Bitti" },
                    "KapaliNesne");

                if (tipResult.Status != PromptStatus.OK)
                    break;

                string secim = tipResult.StringResult;
                if (string.IsNullOrEmpty(secim) || secim.Equals("Bitti", StringComparison.OrdinalIgnoreCase))
                    break;

                KatmanAlanBilgisi bilgi = null;

                if (secim.Equals("KapaliNesne", StringComparison.OrdinalIgnoreCase))
                {
                    bilgi = KapaliNesneOku();
                }
                else if (secim.Equals("IkiCizgi", StringComparison.OrdinalIgnoreCase))
                {
                    bilgi = IkiCizgiOku();
                }

                if (bilgi != null && bilgi.Alan > Constants.AlanToleransi)
                {
                    kesit.KatmanAlanlari.Add(bilgi);
                    _editorService.WriteMessage($"\n  {bilgi.MalzemeAdi}: {bilgi.Alan:F2} m\u00B2 eklendi.\n");
                }
            }

            // 3. Toplam kazı/dolgu hesapla
            foreach (var katman in kesit.KatmanAlanlari)
            {
                if (katman.Kategori == MalzemeKategorisi.ToprakIsleri)
                {
                    if (katman.MalzemeAdi.Equals("Kaz\u0131", StringComparison.OrdinalIgnoreCase) ||
                        katman.MalzemeAdi.Equals("Yarma", StringComparison.OrdinalIgnoreCase))
                        kesit.ToplamKaziAlani += katman.Alan;
                    else if (katman.MalzemeAdi.Equals("Dolgu", StringComparison.OrdinalIgnoreCase))
                        kesit.ToplamDolguAlani += katman.Alan;
                }
            }

            LoggingService.Info("Yol kesit okundu: Km {Ist}, {Count} katman, kaz\u0131={Kazi:F2} m\u00B2, dolgu={Dolgu:F2} m\u00B2",
                istasyonMetni, kesit.KatmanAlanlari.Count, kesit.ToplamKaziAlani, kesit.ToplamDolguAlani);

            return kesit;
        }

        public double IstasyonParse(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin))
                return -1;

            // Temizle: "Km 0+100", "KM:0+050", "Ist: 0+200" gibi önek kaldır
            string temiz = metin.Trim();
            temiz = Regex.Replace(temiz, @"^(Km|KM|km|Ist|IST|ist)[:\s]*", "", RegexOptions.IgnoreCase);

            // Pattern: "0+020", "1+234.567", "0+020.00"
            var match = Regex.Match(temiz, @"(\d+)\+(\d+\.?\d*)");
            if (match.Success)
            {
                double km = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                double m = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                return km * 1000 + m;
            }

            // Sadece sayı girilmişse direkt al
            if (double.TryParse(temiz, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double direkt))
                return direkt;

            return -1;
        }

        private (double istasyon, string metin) IstasyonAl()
        {
            var yontemResult = _editorService.GetKeywords(
                "\n\u0130stasyon giri\u015F y\u00F6ntemi [TextSec/ManuelGir]: ",
                new[] { "TextSec", "ManuelGir" },
                "ManuelGir");

            if (yontemResult.Status != PromptStatus.OK)
                return (-1, null);

            string yontem = yontemResult.StringResult;

            if (yontem != null && yontem.Equals("TextSec", StringComparison.OrdinalIgnoreCase))
            {
                return IstasyonTexttenOku();
            }
            else
            {
                return IstasyonManuelAl();
            }
        }

        private (double istasyon, string metin) IstasyonTexttenOku()
        {
            var entityResult = _editorService.GetEntity("\n\u0130stasyon text/mtext nesnesini se\u00E7in: ");
            if (entityResult.Status != PromptStatus.OK)
                return (-1, null);

            using (var tr = _documentContext.BeginTransaction())
            {
                var obj = tr.GetObject(entityResult.ObjectId, OpenMode.ForRead);
                string metin = null;

                if (obj is DBText dbText)
                    metin = dbText.TextString;
                else if (obj is MText mText)
                    metin = mText.Contents;

                tr.Commit();

                if (string.IsNullOrWhiteSpace(metin))
                {
                    _editorService.WriteMessage("\nSe\u00E7ilen nesne text de\u011Fil veya bo\u015F.\n");
                    return (-1, null);
                }

                double istasyon = IstasyonParse(metin);
                if (istasyon < 0)
                {
                    _editorService.WriteMessage($"\n'{metin}' istasyon format\u0131na uymuyor.\n");
                    return (-1, null);
                }

                string formatli = IstasyonFormatla(istasyon);
                _editorService.WriteMessage($"\n\u0130stasyon: {formatli} ({istasyon:F2} m)\n");
                return (istasyon, formatli);
            }
        }

        private (double istasyon, string metin) IstasyonManuelAl()
        {
            var strResult = _editorService.GetString("\n\u0130stasyon de\u011Ferini girin (ör: 0+020 veya 20): ");
            if (strResult.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(strResult.StringResult))
                return (-1, null);

            double istasyon = IstasyonParse(strResult.StringResult);
            if (istasyon < 0)
            {
                _editorService.WriteMessage($"\n'{strResult.StringResult}' ge\u00E7erli bir istasyon de\u011Feri de\u011Fil.\n");
                return (-1, null);
            }

            string formatli = IstasyonFormatla(istasyon);
            _editorService.WriteMessage($"\n\u0130stasyon: {formatli} ({istasyon:F2} m)\n");
            return (istasyon, formatli);
        }

        private KatmanAlanBilgisi KapaliNesneOku()
        {
            var entityResult = _editorService.GetEntity("\nKapal\u0131 nesne se\u00E7in (polyline/hatch): ");
            if (entityResult.Status != PromptStatus.OK)
                return null;

            double alan = _enkesitService.KapaliNesneAlan(entityResult.ObjectId);
            if (alan <= Constants.AlanToleransi)
            {
                _editorService.WriteMessage("\nGe\u00E7erli alan hesaplanamad\u0131.\n");
                return null;
            }

            // Layer adını al ve malzeme eşleştir
            string layerAdi = LayerAdiAl(entityResult.ObjectId);
            return MalzemeBilgisiOlustur(layerAdi, alan);
        }

        private KatmanAlanBilgisi IkiCizgiOku()
        {
            var ustResult = _editorService.GetEntity("\n\u00DCst \u00E7izgiyi se\u00E7in: ");
            if (ustResult.Status != PromptStatus.OK) return null;

            var altResult = _editorService.GetEntity("\nAlt \u00E7izgiyi se\u00E7in: ");
            if (altResult.Status != PromptStatus.OK) return null;

            double alan = _enkesitService.IkiCizgiArasiAlan(ustResult.ObjectId, altResult.ObjectId);
            if (alan <= Constants.AlanToleransi)
            {
                _editorService.WriteMessage("\nGe\u00E7erli alan hesaplanamad\u0131.\n");
                return null;
            }

            string layerAdi = LayerAdiAl(ustResult.ObjectId);
            return MalzemeBilgisiOlustur(layerAdi, alan);
        }

        private KatmanAlanBilgisi MalzemeBilgisiOlustur(string layerAdi, double alan)
        {
            var eslestirme = _eslestirmeService.LayerEslestir(layerAdi);

            string malzemeAdi;
            MalzemeKategorisi kategori;

            if (eslestirme != null)
            {
                malzemeAdi = eslestirme.MalzemeAdi;
                kategori = eslestirme.Kategori;
                _editorService.WriteMessage($"\n  Otomatik e\u015Fle\u015Fme: {layerAdi} \u2192 {malzemeAdi}\n");
            }
            else
            {
                // Kullanıcıdan sor
                var adResult = _editorService.GetString($"\nMalzeme ad\u0131 girin (layer: {layerAdi}): ", layerAdi);
                malzemeAdi = (adResult.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(adResult.StringResult))
                    ? adResult.StringResult
                    : layerAdi;

                var katResult = _editorService.GetKeywords(
                    "\nKategori [Ustyapi/Alttemel/ToprakIsleri/Ozel]: ",
                    new[] { "Ustyapi", "Alttemel", "ToprakIsleri", "Ozel" },
                    "Ozel");

                kategori = MalzemeKategorisi.Ozel;
                if (katResult.Status == PromptStatus.OK && !string.IsNullOrEmpty(katResult.StringResult))
                {
                    switch (katResult.StringResult)
                    {
                        case "Ustyapi": kategori = MalzemeKategorisi.Ustyapi; break;
                        case "Alttemel": kategori = MalzemeKategorisi.Alttemel; break;
                        case "ToprakIsleri": kategori = MalzemeKategorisi.ToprakIsleri; break;
                    }
                }
            }

            return new KatmanAlanBilgisi
            {
                MalzemeAdi = malzemeAdi,
                Kategori = kategori,
                Alan = alan,
                KaynakLayerAdi = layerAdi
            };
        }

        private string LayerAdiAl(ObjectId entityId)
        {
            try
            {
                using (var tr = _documentContext.BeginTransaction())
                {
                    var entity = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
                    string layer = entity?.Layer ?? "";
                    tr.Commit();
                    return layer;
                }
            }
            catch
            {
                return "";
            }
        }

        private string IstasyonFormatla(double istasyon)
        {
            int km = (int)(istasyon / 1000);
            double m = istasyon % 1000;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}+{1:000.00}", km, m);
        }
    }
}
