using System.Collections.Generic;

namespace Metraj.Models.IhaleKontrol
{
    /// <summary>
    /// Geometrik hesaptan elde edilen tek bir kesitin verileri.
    /// </summary>
    public class GeometrikKesitVerisi
    {
        public double Istasyon { get; set; }
        public string IstasyonMetni { get; set; }
        public double KaziAlani { get; set; }
        public double DolguAlani { get; set; }
        public double UstyapiToplamAlani { get; set; }
        public List<TabakaAlani> TabakaAlanlari { get; set; } = new List<TabakaAlani>();
        public bool TabakaCizgisiKullanildi { get; set; }
    }

    public class TabakaAlani
    {
        public string MalzemeAdi { get; set; }
        public double Alan { get; set; }
        public bool Tahmini { get; set; }
    }
}
