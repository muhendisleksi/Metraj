using System;
using System.Collections.Generic;

namespace Metraj.Models.IhaleKontrol
{
    public class ReferansKesitAyarlari
    {
        public CizgiTanimi AraziCizgisi { get; set; }
        public CizgiTanimi ProjeHatti { get; set; }
        public CizgiTanimi SiyahKot { get; set; }
        public CizgiTanimi CLCizgisi { get; set; }
        public List<CizgiTanimi> TabakaCizgileri { get; set; } = new List<CizgiTanimi>();
        public UstyapiKalinliklari Kalinliklar { get; set; } = new UstyapiKalinliklari();

        public string ProjeAdi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string DosyaYolu { get; set; }

        public bool Gecerli =>
            AraziCizgisi != null &&
            ProjeHatti != null &&
            CLCizgisi != null;
    }

    public class CizgiTanimi
    {
        public string LayerAdi { get; set; }
        public short RenkIndex { get; set; }
        public string NesneTipi { get; set; }
        public CizgiRolu Rol { get; set; }
        public string Aciklama { get; set; }
    }

    public enum CizgiRolu
    {
        Arazi,
        ProjeHatti,
        SiyahKot,
        TabakaSiniri,
        SevCizgisi,
        CL
    }

    public class UstyapiKalinliklari
    {
        public double AsinmaKalinligi { get; set; } = 0.05;
        public double BinderKalinligi { get; set; } = 0.08;
        public double BitumenKalinligi { get; set; } = 0.12;
        public double PlentmiksKalinligi { get; set; } = 0.20;
        public double AltTemelKalinligi { get; set; } = 0.25;

        public double ToplamKalinlik =>
            AsinmaKalinligi + BinderKalinligi +
            BitumenKalinligi + PlentmiksKalinligi + AltTemelKalinligi;

        public Dictionary<string, double> TabakaOranlari()
        {
            double toplam = ToplamKalinlik;
            if (toplam <= 0) return new Dictionary<string, double>();

            return new Dictionary<string, double>
            {
                ["Aşınma"] = AsinmaKalinligi / toplam,
                ["Binder"] = BinderKalinligi / toplam,
                ["Bitümen"] = BitumenKalinligi / toplam,
                ["Plentmiks"] = PlentmiksKalinligi / toplam,
                ["AltTemel"] = AltTemelKalinligi / toplam
            };
        }
    }
}
