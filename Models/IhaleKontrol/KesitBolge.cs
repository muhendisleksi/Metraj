using System.Collections.Generic;

namespace Metraj.Models.IhaleKontrol
{
    /// <summary>
    /// DWG'de tespit edilen bir kesitin spatial bilgileri.
    /// </summary>
    public class KesitBolge
    {
        public double Istasyon { get; set; }
        public string IstasyonMetni { get; set; }

        // Bounding box
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        // CL çizgisi X koordinatı
        public double CLX { get; set; }

        // İlişkilendirilmiş tablo
        public TabloKesitVerisi TabloVerisi { get; set; }

        // Grid konumu
        public int Satir { get; set; }
        public int Sutun { get; set; }
    }
}
