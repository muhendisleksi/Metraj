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
    public class HacimViewModel : ViewModelBase
    {
        private HacimMetodu _seciliMetot = HacimMetodu.OrtalamaAlan;
        private double _toplamHacim;
        private string _durumMesaji = "En kesit verilerini girin ve Hesapla'ya tıklayın.";
        private HacimHesapSonucu _sonuc;

        public ObservableCollection<EnKesitVerisi> Enkesitler { get; } = new ObservableCollection<EnKesitVerisi>();
        public ObservableCollection<HacimSegmenti> Segmentler { get; } = new ObservableCollection<HacimSegmenti>();
        public ObservableCollection<BrucknerNoktasi> BrucknerVerisi { get; } = new ObservableCollection<BrucknerNoktasi>();

        public HacimMetodu SeciliMetot
        {
            get => _seciliMetot;
            set => SetProperty(ref _seciliMetot, value);
        }

        public double ToplamHacim
        {
            get => _toplamHacim;
            set => SetProperty(ref _toplamHacim, value);
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }

        public HacimHesapSonucu Sonuc
        {
            get => _sonuc;
            set => SetProperty(ref _sonuc, value);
        }

        public ICommand EnkesitEkleCommand { get; }
        public ICommand HesaplaCommand { get; }
        public ICommand TemizleCommand { get; }
        public ICommand PolylinedenAlanAlCommand { get; }

        public HacimViewModel()
        {
            EnkesitEkleCommand = new RelayCommand(EnkesitEkle);
            HesaplaCommand = new RelayCommand(Hesapla, () => Enkesitler.Count >= 2);
            TemizleCommand = new RelayCommand(Temizle);
            PolylinedenAlanAlCommand = new RelayCommand(PolylinedenAlanAl);
        }

        private void EnkesitEkle()
        {
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();

                // İstasyon değerini sor
                var istasyonResult = editorService.GetDouble("\nİstasyon değerini girin: ");
                if (istasyonResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

                // Alan değerini sor
                var alanResult = editorService.GetDouble("\nEn kesit alanını girin (m²): ");
                if (alanResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

                Enkesitler.Add(new EnKesitVerisi
                {
                    Istasyon = istasyonResult.Value,
                    ToplamAlan = alanResult.Value,
                    Aciklama = $"Km {istasyonResult.Value:F3}"
                });

                DurumMesaji = $"{Enkesitler.Count} en kesit girildi.";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("En kesit ekleme hatası", ex);
            }
        }

        private void PolylinedenAlanAl()
        {
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();
                var docContext = ServiceContainer.GetRequiredService<IDocumentContext>();

                // İstasyon değerini sor
                var istasyonResult = editorService.GetDouble("\nİstasyon değerini girin: ");
                if (istasyonResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

                // Kapalı polyline seç
                var entityResult = editorService.GetEntity("\nKapalı polyline seçin: ");
                if (entityResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

                using (var tr = docContext.BeginTransaction())
                {
                    var entity = tr.GetObject(entityResult.ObjectId, OpenMode.ForRead) as Entity;
                    double alan = 0;

                    if (entity is Polyline pl && pl.Closed)
                    {
                        alan = pl.Area;
                    }
                    else if (entity is Circle circle)
                    {
                        alan = Math.PI * circle.Radius * circle.Radius;
                    }
                    else
                    {
                        DurumMesaji = "Seçilen nesne kapalı bir polyline veya circle olmalı.";
                        tr.Commit();
                        return;
                    }

                    Enkesitler.Add(new EnKesitVerisi
                    {
                        Istasyon = istasyonResult.Value,
                        ToplamAlan = alan,
                        Aciklama = $"Km {istasyonResult.Value:F3} (polyline)"
                    });

                    tr.Commit();
                }

                DurumMesaji = $"{Enkesitler.Count} en kesit girildi.";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Polyline alan alma hatası", ex);
            }
        }

        private void Hesapla()
        {
            try
            {
                if (Enkesitler.Count < 2)
                {
                    DurumMesaji = "En az 2 en kesit gerekli.";
                    return;
                }

                var hacimService = ServiceContainer.GetRequiredService<IHacimHesapService>();
                Sonuc = hacimService.HesaplaEnkesittenHacim(
                    new System.Collections.Generic.List<EnKesitVerisi>(Enkesitler), SeciliMetot);

                Segmentler.Clear();
                foreach (var seg in Sonuc.Segmentler)
                    Segmentler.Add(seg);

                BrucknerVerisi.Clear();
                foreach (var b in Sonuc.BrucknerVerisi)
                    BrucknerVerisi.Add(b);

                ToplamHacim = Sonuc.ToplamHacim;
                DurumMesaji = $"Hesaplandı: {Sonuc.Segmentler.Count} segment, toplam {ToplamHacim:F2} m³";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hesaplama hatası: " + ex.Message;
                LoggingService.Error("Hacim hesaplama hatası", ex);
            }
        }

        private void Temizle()
        {
            Enkesitler.Clear();
            Segmentler.Clear();
            BrucknerVerisi.Clear();
            ToplamHacim = 0;
            Sonuc = null;
            DurumMesaji = "En kesit verilerini girin ve Hesapla'ya tıklayın.";
        }
    }
}
