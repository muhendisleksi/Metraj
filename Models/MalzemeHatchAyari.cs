using System;
using System.Collections.Generic;
using System.Linq;

namespace Metraj.Models
{
    public class MalzemeHatchAyari
    {
        public string MalzemeAdi { get; set; }
        public short RenkIndex { get; set; }
        public string HatchPattern { get; set; } = "SOLID";
        public double Seffaflik { get; set; } = 0.60;
        public string LayerAdi { get; set; }
        public string EtiketLayerAdi { get; set; }
        public string KisaKod { get; set; }
    }

    public class MalzemeHatchAyarlari
    {
        public List<MalzemeHatchAyari> Ayarlar { get; set; } = new List<MalzemeHatchAyari>();

        public MalzemeHatchAyari AyarGetir(string malzemeAdi)
        {
            if (string.IsNullOrEmpty(malzemeAdi)) return null;
            var ayar = Ayarlar.FirstOrDefault(a =>
                a.MalzemeAdi.Equals(malzemeAdi, StringComparison.OrdinalIgnoreCase));
            if (ayar != null) return ayar;

            // Varsayılan ayar oluştur
            return new MalzemeHatchAyari
            {
                MalzemeAdi = malzemeAdi,
                RenkIndex = 7,
                HatchPattern = "SOLID",
                Seffaflik = 0.50,
                LayerAdi = "METRAJ-" + malzemeAdi.ToUpperInvariant().Replace(" ", "-").Replace(".", ""),
                EtiketLayerAdi = "METRAJ-" + malzemeAdi.ToUpperInvariant().Replace(" ", "-").Replace(".", "") + "-ETIKET",
                KisaKod = malzemeAdi.Length >= 2 ? malzemeAdi.Substring(0, 2) : malzemeAdi
            };
        }

        public static MalzemeHatchAyarlari VarsayilanOlustur()
        {
            return new MalzemeHatchAyarlari
            {
                Ayarlar = new List<MalzemeHatchAyari>
                {
                    new MalzemeHatchAyari { MalzemeAdi = "Yarma",     RenkIndex = 3,   HatchPattern = "SOLID",  Seffaflik = 0.60, LayerAdi = "METRAJ-YARMA",    EtiketLayerAdi = "METRAJ-YARMA-ETIKET",    KisaKod = "Y" },
                    new MalzemeHatchAyari { MalzemeAdi = "Dolgu",     RenkIndex = 4,   HatchPattern = "SOLID",  Seffaflik = 0.60, LayerAdi = "METRAJ-DOLGU",    EtiketLayerAdi = "METRAJ-DOLGU-ETIKET",    KisaKod = "D" },
                    new MalzemeHatchAyari { MalzemeAdi = "A\u015F\u0131nma",    RenkIndex = 1,   HatchPattern = "SOLID", Seffaflik = 0.50, LayerAdi = "METRAJ-ASINMA",   EtiketLayerAdi = "METRAJ-ASINMA-ETIKET",   KisaKod = "As" },
                    new MalzemeHatchAyari { MalzemeAdi = "Binder",    RenkIndex = 30,  HatchPattern = "SOLID", Seffaflik = 0.50, LayerAdi = "METRAJ-BINDER",   EtiketLayerAdi = "METRAJ-BINDER-ETIKET",   KisaKod = "Bi" },
                    new MalzemeHatchAyari { MalzemeAdi = "Bit\u00FCmen",   RenkIndex = 210, HatchPattern = "SOLID", Seffaflik = 0.55, LayerAdi = "METRAJ-BITUMEN",  EtiketLayerAdi = "METRAJ-BITUMEN-ETIKET",  KisaKod = "Bt" },
                    new MalzemeHatchAyari { MalzemeAdi = "Plentmiks", RenkIndex = 6,   HatchPattern = "SOLID", Seffaflik = 0.50, LayerAdi = "METRAJ-PLENTMIKS",EtiketLayerAdi = "METRAJ-PLENTMIKS-ETIKET",KisaKod = "Pm" },
                    new MalzemeHatchAyari { MalzemeAdi = "AltTemel",  RenkIndex = 50,  HatchPattern = "SOLID", Seffaflik = 0.50, LayerAdi = "METRAJ-ALTTEMEL", EtiketLayerAdi = "METRAJ-ALTTEMEL-ETIKET", KisaKod = "At" },
                    new MalzemeHatchAyari { MalzemeAdi = "S\u0131y\u0131rma",   RenkIndex = 90,  HatchPattern = "SOLID", Seffaflik = 0.50, LayerAdi = "METRAJ-SIYIRMA",  EtiketLayerAdi = "METRAJ-SIYIRMA-ETIKET",  KisaKod = "Si" },
                    new MalzemeHatchAyari { MalzemeAdi = "B.T. Yerine Konan",    RenkIndex = 170, HatchPattern = "SOLID", Seffaflik = 0.55, LayerAdi = "METRAJ-BTKONAN",  EtiketLayerAdi = "METRAJ-BTKONAN-ETIKET",  KisaKod = "BK" },
                    new MalzemeHatchAyari { MalzemeAdi = "B.T. Yerine Konmayan", RenkIndex = 40,  HatchPattern = "SOLID", Seffaflik = 0.55, LayerAdi = "METRAJ-BTKONMAYAN",EtiketLayerAdi = "METRAJ-BTKONMAYAN-ETIKET",KisaKod = "BN" },
                }
            };
        }
    }
}
