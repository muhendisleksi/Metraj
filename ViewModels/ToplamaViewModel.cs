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
    public class ToplamaViewModel : ViewModelBase
    {
        private double _toplam;
        private string _onEkFiltre = "";
        private string _sonEkFiltre = "";
        private string _durumMesaji = "Text/MText nesneleri seçin ve Topla'ya tıklayın.";
        private int _gecerliSayisi;
        private int _gecersizSayisi;

        public ObservableCollection<ToplamaOgesi> Ogeler { get; } = new ObservableCollection<ToplamaOgesi>();

        public double Toplam
        {
            get => _toplam;
            set => SetProperty(ref _toplam, value);
        }

        public string OnEkFiltre
        {
            get => _onEkFiltre;
            set => SetProperty(ref _onEkFiltre, value);
        }

        public string SonEkFiltre
        {
            get => _sonEkFiltre;
            set => SetProperty(ref _sonEkFiltre, value);
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }

        public int GecerliSayisi
        {
            get => _gecerliSayisi;
            set => SetProperty(ref _gecerliSayisi, value);
        }

        public int GecersizSayisi
        {
            get => _gecersizSayisi;
            set => SetProperty(ref _gecersizSayisi, value);
        }

        public ICommand MetinSecVeToplaCommand { get; }
        public ICommand TemizleCommand { get; }

        public ToplamaViewModel()
        {
            MetinSecVeToplaCommand = new RelayCommand(MetinSecVeTopla);
            TemizleCommand = new RelayCommand(Temizle);
        }

        private void MetinSecVeTopla()
        {
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();
                var selResult = editorService.GetSelection("\nToplanacak Text/MText nesnelerini seçin: ");

                if (selResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    DurumMesaji = "Seçim iptal edildi.";
                    return;
                }

                var toplamaService = ServiceContainer.GetRequiredService<IToplamaService>();
                var ogeler = toplamaService.ToplaMetinleri(selResult.Value, OnEkFiltre, SonEkFiltre);

                Ogeler.Clear();
                foreach (var oge in ogeler)
                    Ogeler.Add(oge);

                Toplam = toplamaService.ToplamDeger(ogeler);
                GecerliSayisi = ogeler.Count(o => o.GecerliSayi);
                GecersizSayisi = ogeler.Count(o => !o.GecerliSayi);

                DurumMesaji = $"{ogeler.Count} metin okundu ({GecerliSayisi} geçerli, {GecersizSayisi} geçersiz). Toplam: {Toplam:F2}";

                LoggingService.Info("Toplama hesaplandı: {Count} metin, toplam {Toplam:F2}", ogeler.Count, Toplam);
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Toplama hatası", ex);
            }
        }

        private void Temizle()
        {
            Ogeler.Clear();
            Toplam = 0;
            GecerliSayisi = 0;
            GecersizSayisi = 0;
            DurumMesaji = "Text/MText nesneleri seçin ve Topla'ya tıklayın.";
        }
    }
}
