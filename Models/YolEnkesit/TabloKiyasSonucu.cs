using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Metraj.Models.YolEnkesit
{
    public enum KararDurumu
    {
        Bekliyor,
        TabloKabul,
        HesapKabul,
        OtomatikOnay
    }

    public class TabloKiyasSonucu : INotifyPropertyChanged
    {
        private KararDurumu _karar = KararDurumu.Bekliyor;
        private string _kararAciklamasi;

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

        /// <summary>Kullanici veya otomatik karar.</summary>
        public KararDurumu Karar
        {
            get => _karar;
            set
            {
                if (_karar != value)
                {
                    _karar = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(KabulEdilenAlan));
                    OnPropertyChanged(nameof(DurumRengi));
                    OnPropertyChanged(nameof(SatirArkaPlan));
                }
            }
        }

        /// <summary>Kullanicinin opsiyonel notu.</summary>
        public string KararAciklamasi
        {
            get => _kararAciklamasi;
            set { _kararAciklamasi = value; OnPropertyChanged(); }
        }

        /// <summary>Karara gore kabul edilen alan degeri.</summary>
        public double KabulEdilenAlan
        {
            get
            {
                switch (Karar)
                {
                    case KararDurumu.TabloKabul: return TabloAlani;
                    case KararDurumu.HesapKabul: return HesaplananAlan;
                    case KararDurumu.OtomatikOnay: return FarkYuzde <= 2.0 ? TabloAlani : HesaplananAlan;
                    default: return 0;
                }
            }
        }

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

        /// <summary>Karar durumuna gore satir arka plan rengi.</summary>
        public Brush SatirArkaPlan
        {
            get
            {
                switch (Karar)
                {
                    case KararDurumu.TabloKabul: return new SolidColorBrush(Color.FromArgb(0x30, 0x4E, 0xC9, 0xB0));
                    case KararDurumu.HesapKabul: return new SolidColorBrush(Color.FromArgb(0x30, 0x37, 0x8A, 0xDD));
                    case KararDurumu.OtomatikOnay: return new SolidColorBrush(Color.FromArgb(0x18, 0x4E, 0xC9, 0xB0));
                    default: return Brushes.Transparent;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
