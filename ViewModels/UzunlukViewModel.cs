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
    public class UzunlukViewModel : ViewModelBase
    {
        private double _toplamUzunluk;
        private GruplamaTipi _seciliGruplama = GruplamaTipi.Yok;
        private string _durumMesaji = "Nesne seçin ve Hesapla'ya tıklayın.";

        public ObservableCollection<UzunlukOlcumu> Sonuclar { get; } = new ObservableCollection<UzunlukOlcumu>();

        public double ToplamUzunluk
        {
            get => _toplamUzunluk;
            set => SetProperty(ref _toplamUzunluk, value);
        }

        public GruplamaTipi SeciliGruplama
        {
            get => _seciliGruplama;
            set => SetProperty(ref _seciliGruplama, value);
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }

        public ICommand NesneSecVeHesaplaCommand { get; }
        public ICommand TemizleCommand { get; }

        public UzunlukViewModel()
        {
            NesneSecVeHesaplaCommand = new RelayCommand(NesneSecVeHesapla);
            TemizleCommand = new RelayCommand(Temizle);
        }

        private void NesneSecVeHesapla()
        {
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();
                var selResult = editorService.GetSelection("\nUzunluk hesaplamak için nesneleri seçin: ");

                if (selResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    DurumMesaji = "Seçim iptal edildi.";
                    return;
                }

                var uzunlukService = ServiceContainer.GetRequiredService<IUzunlukHesapService>();
                var sonuclar = uzunlukService.Hesapla(selResult.Value);

                Sonuclar.Clear();
                foreach (var s in sonuclar)
                {
                    Sonuclar.Add(s);
                }

                ToplamUzunluk = sonuclar.Sum(s => s.Uzunluk);
                DurumMesaji = $"{sonuclar.Count} nesne ölçüldü. Toplam: {ToplamUzunluk:F2} m";

                LoggingService.Info("Uzunluk hesaplandı: {Count} nesne, toplam {Toplam:F2} m", sonuclar.Count, ToplamUzunluk);
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Uzunluk hesaplama hatası", ex);
            }
        }

        private void Temizle()
        {
            Sonuclar.Clear();
            ToplamUzunluk = 0;
            DurumMesaji = "Nesne seçin ve Hesapla'ya tıklayın.";
        }
    }
}
