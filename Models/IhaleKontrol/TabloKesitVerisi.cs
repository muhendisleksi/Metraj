using System.Collections.Generic;

namespace Metraj.Models.IhaleKontrol
{
    /// <summary>
    /// AutoCAD Table objesinden okunan tek bir kesitin verileri.
    /// </summary>
    public class TabloKesitVerisi
    {
        public double Istasyon { get; set; }
        public string IstasyonMetni { get; set; }
        public List<TabloMalzemeAlani> MalzemeAlanlari { get; set; } = new List<TabloMalzemeAlani>();

        // Table objesinin DWG'deki konumu (kesit eşleştirmesi için)
        public double TabloX { get; set; }
        public double TabloY { get; set; }
    }

    public class TabloMalzemeAlani
    {
        public string HamMalzemeAdi { get; set; }
        public string NormalizeMalzemeAdi { get; set; }
        public double Alan { get; set; }
    }
}
