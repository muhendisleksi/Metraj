using System.Windows.Media;

namespace Metraj.Models.YolEnkesit
{
    public class TabloKiyasSonucu
    {
        public string MalzemeAdi { get; set; }
        public double TabloAlani { get; set; }
        public double HesaplananAlan { get; set; }
        public double Fark { get; set; }
        public double FarkYuzde { get; set; }
        public bool Uyumlu { get; set; }

        /// <summary>Bu malzemenin ust sinir cizgi rolu (AlanHesapSonucu'ndan).</summary>
        public CizgiRolu UstCizgiRolu { get; set; }
        /// <summary>Bu malzemenin alt sinir cizgi rolu (AlanHesapSonucu'ndan).</summary>
        public CizgiRolu AltCizgiRolu { get; set; }

        /// <summary>Renk kodu: Yesil (≤%2), Sari (%2-5), Kirmizi (>%5), Gri (tablo yok)</summary>
        public Brush DurumRengi
        {
            get
            {
                if (TabloAlani <= 0) return new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66));
                if (FarkYuzde <= 2.0) return new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
                if (FarkYuzde <= 5.0) return new SolidColorBrush(Color.FromRgb(0xEF, 0xB8, 0x4E));
                return new SolidColorBrush(Color.FromRgb(0xE2, 0x4B, 0x4A));
            }
        }
    }
}
