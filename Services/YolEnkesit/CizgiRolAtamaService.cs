using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    public class CizgiRolAtamaService : ICizgiRolAtamaService
    {
        private const int MaxTanimsizEsik = 5;

        public ReferansKesitSablonu KalibrasyonOlustur(KesitGrubu referansKesit, List<CizgiTanimi> rolAtanmisCizgiler)
        {
            var sablon = new ReferansKesitSablonu
            {
                OlusturmaTarihi = DateTime.Now,
                Kurallar = new List<RolEslestirmeKurali>()
            };

            double clY = BulEksenY(rolAtanmisCizgiler);

            foreach (var cizgi in rolAtanmisCizgiler)
            {
                if (cizgi.Rol == CizgiRolu.Tanimsiz || cizgi.Rol == CizgiRolu.Diger) continue;

                sablon.Kurallar.Add(new RolEslestirmeKurali
                {
                    Rol = cizgi.Rol,
                    LayerPattern = LayerPatternOlustur(cizgi.LayerAdi),
                    RenkIndex = cizgi.RenkIndex,
                    BagintiliYPozisyonu = clY != 0 ? cizgi.OrtalamaY - clY : (double?)null
                });
            }

            return sablon;
        }

        public void OtomatikRolAta(KesitGrubu kesit, ReferansKesitSablonu sablon)
        {
            foreach (var cizgi in kesit.Cizgiler)
            {
                if (cizgi.Rol != CizgiRolu.Tanimsiz) continue;

                var eslesen = LayerIleEslestir(cizgi, sablon.Kurallar);
                if (eslesen != null)
                {
                    cizgi.Rol = eslesen.Rol;
                    cizgi.OtomatikAtanmis = true;
                    continue;
                }

                eslesen = RenkIleEslestir(cizgi, sablon.Kurallar);
                if (eslesen != null)
                {
                    cizgi.Rol = eslesen.Rol;
                    cizgi.OtomatikAtanmis = true;
                    continue;
                }

                eslesen = PozisyonIleEslestir(cizgi, sablon.Kurallar, kesit);
                if (eslesen != null)
                {
                    cizgi.Rol = eslesen.Rol;
                    cizgi.OtomatikAtanmis = true;
                }
            }

            int tanimsizSayisi = kesit.Cizgiler.Count(c => c.Rol == CizgiRolu.Tanimsiz);
            if (tanimsizSayisi > MaxTanimsizEsik)
                kesit.Durum = DogrulamaDurumu.Sorunlu;
        }

        public void TopluRolAta(List<KesitGrubu> kesitler, ReferansKesitSablonu sablon)
        {
            foreach (var kesit in kesitler)
            {
                OtomatikRolAta(kesit, sablon);
            }

            int sorunlu = kesitler.Count(k => k.Durum == DogrulamaDurumu.Sorunlu);
            LoggingService.Info($"Toplu rol atama: {kesitler.Count} kesit, {sorunlu} sorunlu");
        }

        private RolEslestirmeKurali LayerIleEslestir(CizgiTanimi cizgi, List<RolEslestirmeKurali> kurallar)
        {
            foreach (var kural in kurallar)
            {
                if (string.IsNullOrEmpty(kural.LayerPattern)) continue;
                if (LayerPatternUyuyor(cizgi.LayerAdi, kural.LayerPattern))
                    return kural;
            }
            return null;
        }

        private RolEslestirmeKurali RenkIleEslestir(CizgiTanimi cizgi, List<RolEslestirmeKurali> kurallar)
        {
            var renkEslesenler = kurallar.Where(k => k.RenkIndex.HasValue && k.RenkIndex.Value == cizgi.RenkIndex).ToList();
            return renkEslesenler.Count == 1 ? renkEslesenler[0] : null;
        }

        private RolEslestirmeKurali PozisyonIleEslestir(CizgiTanimi cizgi, List<RolEslestirmeKurali> kurallar, KesitGrubu kesit)
        {
            var eksenCizgi = kesit.Cizgiler.FirstOrDefault(c => c.Rol == CizgiRolu.EksenCizgisi);
            if (eksenCizgi == null) return null;

            double clY = eksenCizgi.OrtalamaY;
            double bagintiliY = cizgi.OrtalamaY - clY;

            RolEslestirmeKurali enYakin = null;
            double enKucukFark = double.MaxValue;

            foreach (var kural in kurallar)
            {
                if (!kural.BagintiliYPozisyonu.HasValue) continue;
                double fark = Math.Abs(bagintiliY - kural.BagintiliYPozisyonu.Value);
                if (fark < enKucukFark)
                {
                    enKucukFark = fark;
                    enYakin = kural;
                }
            }

            return enKucukFark < 2.0 ? enYakin : null;
        }

        private double BulEksenY(List<CizgiTanimi> cizgiler)
        {
            var eksen = cizgiler.FirstOrDefault(c => c.Rol == CizgiRolu.EksenCizgisi);
            return eksen?.OrtalamaY ?? 0;
        }

        private string LayerPatternOlustur(string layerAdi)
        {
            if (string.IsNullOrEmpty(layerAdi)) return "";
            int dashIdx = layerAdi.LastIndexOf('-');
            int underIdx = layerAdi.LastIndexOf('_');
            int sepIdx = Math.Max(dashIdx, underIdx);
            if (sepIdx > 0)
                return layerAdi.Substring(0, sepIdx + 1) + "*";
            return layerAdi + "*";
        }

        private bool LayerPatternUyuyor(string layerAdi, string pattern)
        {
            if (string.IsNullOrEmpty(layerAdi) || string.IsNullOrEmpty(pattern)) return false;
            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(layerAdi, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}
