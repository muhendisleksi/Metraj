using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Metraj.Infrastructure;
using Metraj.Models;
using Metraj.Services;
using Metraj.Services.Interfaces;

namespace Metraj.ViewModels
{
    public class YolMetrajViewModel : ViewModelBase
    {
        private HacimMetodu _seciliMetot = HacimMetodu.OrtalamaAlan;
        private string _durumMesaji = "Kesit eklemek i\u00E7in 'Kesit Ekle' butonuna t\u0131klay\u0131n.";
        private YolKubajSonucu _kubajSonucu;
        private YolKesitVerisi _seciliKesit;

        public ObservableCollection<YolKesitVerisi> Kesitler { get; }
            = new ObservableCollection<YolKesitVerisi>();

        public ObservableCollection<KatmanAlanBilgisi> SeciliKesitKatmanlari { get; }
            = new ObservableCollection<KatmanAlanBilgisi>();

        public ObservableCollection<MalzemeHacimOzeti> MalzemeOzetleri { get; }
            = new ObservableCollection<MalzemeHacimOzeti>();

        public HacimMetodu SeciliMetot
        {
            get => _seciliMetot;
            set => SetProperty(ref _seciliMetot, value);
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }

        public YolKubajSonucu KubajSonucu
        {
            get => _kubajSonucu;
            set
            {
                if (SetProperty(ref _kubajSonucu, value))
                    OnPropertiesChanged("ToplamKaziHacmi", "ToplamDolguHacmi", "NetHacim");
            }
        }

        public YolKesitVerisi SeciliKesit
        {
            get => _seciliKesit;
            set
            {
                if (SetProperty(ref _seciliKesit, value))
                    SeciliKesitDegisti();
            }
        }

        public double ToplamKaziHacmi => _kubajSonucu?.ToplamKaziHacmi ?? 0;
        public double ToplamDolguHacmi => _kubajSonucu?.ToplamDolguHacmi ?? 0;
        public double NetHacim => _kubajSonucu?.NetHacim ?? 0;

        public ICommand KesitEkleCommand { get; }
        public ICommand KesitSilCommand { get; }
        public ICommand HesaplaCommand { get; }
        public ICommand TemizleCommand { get; }
        public ICommand ExcelAktarCommand { get; }

        public YolMetrajViewModel()
        {
            KesitEkleCommand = new RelayCommand(KesitEkle);
            KesitSilCommand = new RelayCommand(KesitSil, () => SeciliKesit != null);
            HesaplaCommand = new RelayCommand(Hesapla, () => Kesitler.Count >= 2);
            TemizleCommand = new RelayCommand(Temizle);
            ExcelAktarCommand = new RelayCommand(ExcelAktar, () => Kesitler.Count > 0);
        }

        private void KesitEkle()
        {
            try
            {
                var kesitService = ServiceContainer.GetRequiredService<IYolKesitService>();
                var kesit = kesitService.TekKesitOku();

                if (kesit == null)
                {
                    DurumMesaji = "\u0130\u015Flem iptal edildi.";
                    return;
                }

                if (kesit.KatmanAlanlari.Count == 0)
                {
                    DurumMesaji = "Kesitte hi\u00E7bir katman alan\u0131 bulunamad\u0131.";
                    return;
                }

                Kesitler.Add(kesit);
                DurumMesaji = $"Kesit eklendi: Km {kesit.IstasyonMetni}, {kesit.KatmanAlanlari.Count} katman";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Kesit ekleme hatas\u0131", ex);
            }
        }

        private void KesitSil()
        {
            if (SeciliKesit == null) return;
            Kesitler.Remove(SeciliKesit);
            SeciliKesit = null;
            DurumMesaji = $"{Kesitler.Count} kesit mevcut.";
        }

        private void Hesapla()
        {
            try
            {
                if (Kesitler.Count < 2)
                {
                    DurumMesaji = "En az 2 kesit gerekli.";
                    return;
                }

                var kubajService = ServiceContainer.GetRequiredService<IYolKubajService>();
                KubajSonucu = kubajService.KubajHesapla(
                    new System.Collections.Generic.List<YolKesitVerisi>(Kesitler), SeciliMetot);

                MalzemeOzetleri.Clear();
                foreach (var ozet in KubajSonucu.MalzemeOzetleri)
                    MalzemeOzetleri.Add(ozet);

                DurumMesaji = $"K\u00FCbaj hesapland\u0131: {Kesitler.Count} kesit, {MalzemeOzetleri.Count} malzeme, " +
                              $"kaz\u0131: {ToplamKaziHacmi:F2} m\u00B3, dolgu: {ToplamDolguHacmi:F2} m\u00B3";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hesaplama hatas\u0131: " + ex.Message;
                LoggingService.Error("Yol k\u00FCbaj hesaplama hatas\u0131", ex);
            }
        }

        private void Temizle()
        {
            Kesitler.Clear();
            SeciliKesitKatmanlari.Clear();
            MalzemeOzetleri.Clear();
            KubajSonucu = null;
            SeciliKesit = null;
            DurumMesaji = "Kesit eklemek i\u00E7in 'Kesit Ekle' butonuna t\u0131klay\u0131n.";
        }

        private void ExcelAktar()
        {
            try
            {
                var excelService = ServiceContainer.GetRequiredService<IExcelExportService>() as ExcelExportService;
                if (excelService == null)
                {
                    DurumMesaji = "Excel servis hatas\u0131.";
                    return;
                }

                var rapor = new YolMetrajRaporu
                {
                    Kesitler = new List<YolKesitVerisi>(Kesitler),
                    KubajSonucu = _kubajSonucu,
                    ProjeAdi = "Yol Metraj",
                    OlusturmaTarihi = DateTime.Now
                };

                var dosyaYolu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "YolMetraj_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");

                var result = excelService.YolMetrajExport(rapor, dosyaYolu);

                if (result.Basarili)
                    DurumMesaji = "Excel olu\u015Fturuldu: " + result.DosyaYolu;
                else
                    DurumMesaji = "Excel hatas\u0131: " + result.HataMesaji;
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Excel export hatas\u0131: " + ex.Message;
                LoggingService.Error("Yol metraj Excel export hatas\u0131", ex);
            }
        }

        private void SeciliKesitDegisti()
        {
            SeciliKesitKatmanlari.Clear();
            if (_seciliKesit == null) return;

            foreach (var katman in _seciliKesit.KatmanAlanlari)
                SeciliKesitKatmanlari.Add(katman);
        }
    }
}
