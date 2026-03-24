using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Metraj.Infrastructure;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services;
using Metraj.Services.Interfaces;

namespace Metraj.ViewModels
{
    public class AlanViewModel : ViewModelBase
    {
        private double _toplamAlan;
        private BirimTipi _seciliBirim = BirimTipi.Metrekare;
        private string _durumMesaji = "Kapalı nesne seçin ve Hesapla'ya tıklayın.";

        public ObservableCollection<AlanOlcumu> Sonuclar { get; } = new ObservableCollection<AlanOlcumu>();

        public double ToplamAlan
        {
            get => _toplamAlan;
            set => SetProperty(ref _toplamAlan, value);
        }

        public BirimTipi SeciliBirim
        {
            get => _seciliBirim;
            set
            {
                if (SetProperty(ref _seciliBirim, value))
                    BirimGuncelle();
            }
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }

        public string BirimEtiketi
        {
            get
            {
                switch (SeciliBirim)
                {
                    case BirimTipi.Hektar: return "ha";
                    case BirimTipi.Donum: return "dönüm";
                    default: return "m\u00B2";
                }
            }
        }

        public ICommand NesneSecVeHesaplaCommand { get; }
        public ICommand TemizleCommand { get; }

        public AlanViewModel()
        {
            NesneSecVeHesaplaCommand = new RelayCommand(NesneSecVeHesapla);
            TemizleCommand = new RelayCommand(Temizle);
        }

        private void NesneSecVeHesapla()
        {
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();
                var selResult = editorService.GetSelection("\nAlan hesaplamak için kapalı nesneleri seçin: ");

                if (selResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    DurumMesaji = "Seçim iptal edildi.";
                    return;
                }

                var alanService = ServiceContainer.GetRequiredService<IAlanHesapService>();
                var sonuclar = alanService.Hesapla(selResult.Value);

                Sonuclar.Clear();
                foreach (var s in sonuclar)
                {
                    s.BirimAlan = alanService.BirimDonustur(s.Alan, SeciliBirim);
                    Sonuclar.Add(s);
                }

                ToplamAlan = sonuclar.Sum(s => alanService.BirimDonustur(s.Alan, SeciliBirim));
                DurumMesaji = $"{sonuclar.Count} nesne ölçüldü. Toplam: {ToplamAlan:F2} {BirimEtiketi}";

                LoggingService.Info("Alan hesaplandı: {Count} nesne, toplam {Toplam:F2} m\u00B2", sonuclar.Count, sonuclar.Sum(s => s.Alan));
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Alan hesaplama hatası", ex);
            }
        }

        private void BirimGuncelle()
        {
            if (Sonuclar.Count == 0) return;

            var alanService = ServiceContainer.GetRequiredService<IAlanHesapService>();
            foreach (var s in Sonuclar)
            {
                s.BirimAlan = alanService.BirimDonustur(s.Alan, SeciliBirim);
            }
            ToplamAlan = Sonuclar.Sum(s => s.BirimAlan);
            OnPropertyChanged(nameof(BirimEtiketi));
        }

        private void Temizle()
        {
            Sonuclar.Clear();
            ToplamAlan = 0;
            DurumMesaji = "Kapalı nesne seçin ve Hesapla'ya tıklayın.";
        }
    }
}
