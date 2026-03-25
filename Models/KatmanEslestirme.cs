using System.Collections.Generic;

namespace Metraj.Models
{
    public class KatmanEslestirme
    {
        public string LayerPattern { get; set; }        // "ASINMA*", "C-KAZI", vb.
        public string MalzemeAdi { get; set; }           // "Aşınma Tabakası"
        public MalzemeKategorisi Kategori { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class KatmanEslestirmeAyarlari
    {
        public List<KatmanEslestirme> Eslestirmeler { get; set; } = new List<KatmanEslestirme>();

        public static KatmanEslestirmeAyarlari VarsayilanOlustur()
        {
            return new KatmanEslestirmeAyarlari
            {
                Eslestirmeler = new List<KatmanEslestirme>
                {
                    new KatmanEslestirme { LayerPattern = "ASINMA*",    MalzemeAdi = "A\u015F\u0131nma",          Kategori = MalzemeKategorisi.Ustyapi },
                    new KatmanEslestirme { LayerPattern = "BSK*",       MalzemeAdi = "A\u015F\u0131nma",          Kategori = MalzemeKategorisi.Ustyapi },
                    new KatmanEslestirme { LayerPattern = "BINDER*",    MalzemeAdi = "Binder",          Kategori = MalzemeKategorisi.Ustyapi },
                    new KatmanEslestirme { LayerPattern = "BIT*TEMEL*", MalzemeAdi = "Bit\u00FCml\u00FC Temel",   Kategori = MalzemeKategorisi.Ustyapi },
                    new KatmanEslestirme { LayerPattern = "BTM*",       MalzemeAdi = "Bit\u00FCml\u00FC Temel",   Kategori = MalzemeKategorisi.Ustyapi },
                    new KatmanEslestirme { LayerPattern = "PLENT*",     MalzemeAdi = "Plentmiks Temel", Kategori = MalzemeKategorisi.Alttemel },
                    new KatmanEslestirme { LayerPattern = "PMT*",       MalzemeAdi = "Plentmiks Temel", Kategori = MalzemeKategorisi.Alttemel },
                    new KatmanEslestirme { LayerPattern = "KIRMATAS*",  MalzemeAdi = "K\u0131rmata\u015F Temel",  Kategori = MalzemeKategorisi.Alttemel },
                    new KatmanEslestirme { LayerPattern = "KMT*",       MalzemeAdi = "K\u0131rmata\u015F Temel",  Kategori = MalzemeKategorisi.Alttemel },
                    new KatmanEslestirme { LayerPattern = "STAB*",      MalzemeAdi = "Stabilize",       Kategori = MalzemeKategorisi.Alttemel },
                    new KatmanEslestirme { LayerPattern = "KAZI*",      MalzemeAdi = "Kaz\u0131",            Kategori = MalzemeKategorisi.ToprakIsleri },
                    new KatmanEslestirme { LayerPattern = "YARMA*",     MalzemeAdi = "Kaz\u0131",            Kategori = MalzemeKategorisi.ToprakIsleri },
                    new KatmanEslestirme { LayerPattern = "DOLGU*",     MalzemeAdi = "Dolgu",           Kategori = MalzemeKategorisi.ToprakIsleri },
                    new KatmanEslestirme { LayerPattern = "SEV*",       MalzemeAdi = "\u015Eev",             Kategori = MalzemeKategorisi.ToprakIsleri },
                    new KatmanEslestirme { LayerPattern = "BANKET*",    MalzemeAdi = "Banket",          Kategori = MalzemeKategorisi.Ozel },
                    new KatmanEslestirme { LayerPattern = "HENDEK*",    MalzemeAdi = "Hendek",          Kategori = MalzemeKategorisi.Ozel },
                }
            };
        }
    }
}
