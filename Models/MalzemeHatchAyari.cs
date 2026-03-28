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
                    // ACI: 1=kirmizi 2=sari 3=yesil 4=cyan 5=mavi 6=magenta 7=beyaz
                    // 10=pembe 30=turuncu 80=koyu-yesil 140=acik-mor 200=mor 210=koyu-pembe
                    new MalzemeHatchAyari { MalzemeAdi = "Yarma",     RenkIndex = 1,   HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-YARMA",    EtiketLayerAdi = "METRAJ-YARMA-ETIKET",    KisaKod = "Y" },   // Kirmizi
                    new MalzemeHatchAyari { MalzemeAdi = "Dolgu",     RenkIndex = 5,   HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-DOLGU",    EtiketLayerAdi = "METRAJ-DOLGU-ETIKET",    KisaKod = "D" },   // Mavi
                    new MalzemeHatchAyari { MalzemeAdi = "A\u015F\u0131nma",    RenkIndex = 30,  HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-ASINMA",   EtiketLayerAdi = "METRAJ-ASINMA-ETIKET",   KisaKod = "As" },  // Turuncu
                    new MalzemeHatchAyari { MalzemeAdi = "Binder",    RenkIndex = 3,   HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-BINDER",   EtiketLayerAdi = "METRAJ-BINDER-ETIKET",   KisaKod = "Bi" },  // Yesil
                    new MalzemeHatchAyari { MalzemeAdi = "Bit\u00FCmen",   RenkIndex = 200, HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-BITUMEN",  EtiketLayerAdi = "METRAJ-BITUMEN-ETIKET",  KisaKod = "Bt" },  // Mor
                    new MalzemeHatchAyari { MalzemeAdi = "Plentmiks", RenkIndex = 2,   HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-PLENTMIKS",EtiketLayerAdi = "METRAJ-PLENTMIKS-ETIKET",KisaKod = "Pm" },  // Sari
                    new MalzemeHatchAyari { MalzemeAdi = "AltTemel",  RenkIndex = 4,   HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-ALTTEMEL", EtiketLayerAdi = "METRAJ-ALTTEMEL-ETIKET", KisaKod = "At" },  // Cyan
                    new MalzemeHatchAyari { MalzemeAdi = "S\u0131y\u0131rma",   RenkIndex = 6,   HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-SIYIRMA",  EtiketLayerAdi = "METRAJ-SIYIRMA-ETIKET",  KisaKod = "Si" },  // Magenta
                    new MalzemeHatchAyari { MalzemeAdi = "B.T. Yerine Konan",    RenkIndex = 10,  HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-BTKONAN",  EtiketLayerAdi = "METRAJ-BTKONAN-ETIKET",  KisaKod = "BK" },  // Pembe
                    new MalzemeHatchAyari { MalzemeAdi = "B.T. Yerine Konmayan", RenkIndex = 80,  HatchPattern = "SOLID", Seffaflik = 0.85, LayerAdi = "METRAJ-BTKONMAYAN",EtiketLayerAdi = "METRAJ-BTKONMAYAN-ETIKET",KisaKod = "BN" },  // Koyu Yesil
                }
            };
        }
    }
}
