using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Metraj.Models.IhaleKontrol;
using Metraj.Services;
using Metraj.Services.Interfaces;
using Metraj.Services.IhaleKontrol.Interfaces;

namespace Metraj.ViewModels
{
    public class IhaleKontrolViewModel : ViewModelBase
    {
        private readonly ITabloParseService _tabloService;
        private readonly IReferansKesitService _referansService;
        private readonly IKesitTespitService _kesitTespitService;
        private readonly IGeometrikAlanService _geometrikService;
        private readonly IKarsilastirmaService _karsilastirmaService;
        private readonly IExcelExportService _excelService;

        // Durum mesajları
        private string _tabloParseStatus = "Henüz çalıştırılmadı";
        private string _referansKesitStatus = "Tanımsız";
        private string _geometrikHesapStatus = "Henüz çalıştırılmadı";
        private string _karsilastirmaStatus = "Henüz çalıştırılmadı";
        private string _durumMesaji = "";

        // Veri
        private ReferansKesitAyarlari _referansAyarlari;
        private List<TabloKesitVerisi> _tabloVerileri;
        private List<KesitBolge> _kesitBolgeleri;
        private List<GeometrikKesitVerisi> _geometrikVeriler;
        private IhaleKontrolRaporu _rapor;

        // Tolerans
        private double _uyariTolerans = 3.0;
        private double _hataTolerans = 10.0;

        // Profil
        private string _seciliProfil;
        private ObservableCollection<string> _kayitliProfiller = new ObservableCollection<string>();

        // Karşılaştırma sonuç listesi
        private ObservableCollection<KesitKarsilastirma> _karsilastirmalar = new ObservableCollection<KesitKarsilastirma>();
        private KesitKarsilastirma _seciliKarsilastirma;

        public IhaleKontrolViewModel(
            ITabloParseService tabloService,
            IReferansKesitService referansService,
            IKesitTespitService kesitTespitService,
            IGeometrikAlanService geometrikService,
            IKarsilastirmaService karsilastirmaService,
            IExcelExportService excelService)
        {
            _tabloService = tabloService;
            _referansService = referansService;
            _kesitTespitService = kesitTespitService;
            _geometrikService = geometrikService;
            _karsilastirmaService = karsilastirmaService;
            _excelService = excelService;

            TabloOkuCommand = new RelayCommand(TabloOku);
            ReferansKesitTanimlaCommand = new RelayCommand(ReferansKesitTanimla);
            ReferansKesitYukleCommand = new RelayCommand(ReferansKesitYukle, () => !string.IsNullOrEmpty(SeciliProfil));
            GeometrikHesapCommand = new RelayCommand(GeometrikHesap,
                () => _tabloVerileri != null && _referansAyarlari != null);
            KarsilastirCommand = new RelayCommand(Karsilastir,
                () => _tabloVerileri != null && _geometrikVeriler != null);
            ExcelRaporCommand = new RelayCommand(ExcelRapor, () => _rapor != null);
            TemizleCommand = new RelayCommand(Temizle);

            ProfilleriYukle();
        }

        // Commands
        public ICommand TabloOkuCommand { get; }
        public ICommand ReferansKesitTanimlaCommand { get; }
        public ICommand ReferansKesitYukleCommand { get; }
        public ICommand GeometrikHesapCommand { get; }
        public ICommand KarsilastirCommand { get; }
        public ICommand ExcelRaporCommand { get; }
        public ICommand TemizleCommand { get; }

        // Properties
        public string TabloParseStatus
        {
            get => _tabloParseStatus;
            set => SetProperty(ref _tabloParseStatus, value);
        }

        public string ReferansKesitStatus
        {
            get => _referansKesitStatus;
            set => SetProperty(ref _referansKesitStatus, value);
        }

        public string GeometrikHesapStatus
        {
            get => _geometrikHesapStatus;
            set => SetProperty(ref _geometrikHesapStatus, value);
        }

        public string KarsilastirmaStatus
        {
            get => _karsilastirmaStatus;
            set => SetProperty(ref _karsilastirmaStatus, value);
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }

        public double UyariTolerans
        {
            get => _uyariTolerans;
            set => SetProperty(ref _uyariTolerans, value);
        }

        public double HataTolerans
        {
            get => _hataTolerans;
            set => SetProperty(ref _hataTolerans, value);
        }

        public string SeciliProfil
        {
            get => _seciliProfil;
            set { SetProperty(ref _seciliProfil, value); CommandManager.InvalidateRequerySuggested(); }
        }

        public ObservableCollection<string> KayitliProfiller
        {
            get => _kayitliProfiller;
            set => SetProperty(ref _kayitliProfiller, value);
        }

        public ObservableCollection<KesitKarsilastirma> Karsilastirmalar
        {
            get => _karsilastirmalar;
            set => SetProperty(ref _karsilastirmalar, value);
        }

        public KesitKarsilastirma SeciliKarsilastirma
        {
            get => _seciliKarsilastirma;
            set => SetProperty(ref _seciliKarsilastirma, value);
        }

        // Özet istatistikler
        public int ToplamKesit => _rapor?.ToplamKesit ?? 0;
        public int SorunsuzKesit => _rapor?.SorunsuzKesit ?? 0;
        public int UyariKesit => _rapor?.UyariKesit ?? 0;
        public int HataliKesit => _rapor?.HataliKesit ?? 0;

        // Referans kesit bilgisi
        public string ReferansAraziInfo => _referansAyarlari?.AraziCizgisi != null
            ? $"Layer={_referansAyarlari.AraziCizgisi.LayerAdi}, Renk={_referansAyarlari.AraziCizgisi.RenkIndex}"
            : "-";
        public string ReferansProjeInfo => _referansAyarlari?.ProjeHatti != null
            ? $"Layer={_referansAyarlari.ProjeHatti.LayerAdi}, Renk={_referansAyarlari.ProjeHatti.RenkIndex}"
            : "-";
        public string ReferansSiyahKotInfo => _referansAyarlari?.SiyahKot != null
            ? $"Layer={_referansAyarlari.SiyahKot.LayerAdi}, Renk={_referansAyarlari.SiyahKot.RenkIndex}"
            : "-";

        // Command implementations
        private void TabloOku()
        {
            try
            {
                _tabloVerileri = _tabloService.TumTablolariOku();
                TabloParseStatus = $"{_tabloVerileri.Count} tablo okundu";
                DurumMesaji = $"Tablo parse tamamlandı: {_tabloVerileri.Count} kesit";
                CommandManager.InvalidateRequerySuggested();
            }
            catch (System.Exception ex)
            {
                TabloParseStatus = "Hata!";
                DurumMesaji = $"Tablo okuma hatası: {ex.Message}";
                LoggingService.Error("TabloOku hatası", ex);
            }
        }

        private void ReferansKesitTanimla()
        {
            try
            {
                var ayarlar = _referansService.ReferansKesitTanimla();
                if (ayarlar == null) return;

                _referansAyarlari = ayarlar;
                _referansService.AyarlariKaydet(ayarlar);

                ReferansKesitStatus = $"Tanımlı ({ayarlar.ProjeAdi})";
                DurumMesaji = $"Referans kesit tanımlandı ve kaydedildi: {ayarlar.ProjeAdi}";

                ProfilleriYukle();
                ReferansBilgileriniGuncelle();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (System.Exception ex)
            {
                ReferansKesitStatus = "Hata!";
                DurumMesaji = $"Referans kesit hatası: {ex.Message}";
                LoggingService.Error("ReferansKesitTanimla hatası", ex);
            }
        }

        private void ReferansKesitYukle()
        {
            try
            {
                if (string.IsNullOrEmpty(SeciliProfil)) return;

                var ayarlar = _referansService.AyarlariYukle(SeciliProfil);
                if (ayarlar == null)
                {
                    DurumMesaji = "Profil yüklenemedi.";
                    return;
                }

                _referansAyarlari = ayarlar;
                ReferansKesitStatus = $"Yüklendi ({ayarlar.ProjeAdi})";
                DurumMesaji = $"Referans kesit yüklendi: {ayarlar.ProjeAdi}";

                ReferansBilgileriniGuncelle();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (System.Exception ex)
            {
                DurumMesaji = $"Profil yükleme hatası: {ex.Message}";
                LoggingService.Error("ReferansKesitYukle hatası", ex);
            }
        }

        private void GeometrikHesap()
        {
            try
            {
                _kesitBolgeleri = _kesitTespitService.KesitleriBelirle(_tabloVerileri, _referansAyarlari);
                _geometrikVeriler = _geometrikService.TumKesitleriHesapla(_kesitBolgeleri, _referansAyarlari);

                GeometrikHesapStatus = $"{_geometrikVeriler.Count} kesit hesaplandı";
                DurumMesaji = $"Geometrik hesap tamamlandı: {_geometrikVeriler.Count} kesit";
                CommandManager.InvalidateRequerySuggested();
            }
            catch (System.Exception ex)
            {
                GeometrikHesapStatus = "Hata!";
                DurumMesaji = $"Geometrik hesap hatası: {ex.Message}";
                LoggingService.Error("GeometrikHesap hatası", ex);
            }
        }

        private void Karsilastir()
        {
            try
            {
                var karsilastirmalar = _karsilastirmaService.Karsilastir(
                    _tabloVerileri, _geometrikVeriler,
                    UyariTolerans, HataTolerans);

                var kubaj = _karsilastirmaService.KubajKarsilastir(_tabloVerileri, _geometrikVeriler);

                _rapor = new IhaleKontrolRaporu
                {
                    TabloVerileri = _tabloVerileri,
                    GeometrikVeriler = _geometrikVeriler,
                    Karsilastirmalar = karsilastirmalar,
                    KubajSonucu = kubaj,
                    UyariToleransYuzde = UyariTolerans,
                    HataToleransYuzde = HataTolerans
                };

                Karsilastirmalar = new ObservableCollection<KesitKarsilastirma>(karsilastirmalar);

                int hata = _rapor.HataliKesit;
                int uyari = _rapor.UyariKesit;
                KarsilastirmaStatus = $"{hata} hata, {uyari} uyarı";
                DurumMesaji = $"Karşılaştırma tamamlandı: {_rapor.ToplamKesit} kesit, {hata} hata, {uyari} uyarı";

                OzetBilgileriniGuncelle();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (System.Exception ex)
            {
                KarsilastirmaStatus = "Hata!";
                DurumMesaji = $"Karşılaştırma hatası: {ex.Message}";
                LoggingService.Error("Karsilastir hatası", ex);
            }
        }

        private void ExcelRapor()
        {
            try
            {
                string dosyaYolu = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "IhaleKontrol_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");

                var result = _excelService.IhaleKontrolExport(_rapor, dosyaYolu);

                DurumMesaji = result.Basarili
                    ? $"Excel raporu oluşturuldu: {result.DosyaYolu}"
                    : $"Excel hatası: {result.HataMesaji}";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = $"Excel hatası: {ex.Message}";
                LoggingService.Error("ExcelRapor hatası", ex);
            }
        }

        private void Temizle()
        {
            _tabloVerileri = null;
            _geometrikVeriler = null;
            _kesitBolgeleri = null;
            _rapor = null;

            Karsilastirmalar.Clear();
            TabloParseStatus = "Henüz çalıştırılmadı";
            GeometrikHesapStatus = "Henüz çalıştırılmadı";
            KarsilastirmaStatus = "Henüz çalıştırılmadı";
            DurumMesaji = "Temizlendi.";

            OzetBilgileriniGuncelle();
            CommandManager.InvalidateRequerySuggested();
        }

        private void ProfilleriYukle()
        {
            var profiller = _referansService.KayitliProfilleriListele();
            KayitliProfiller = new ObservableCollection<string>(profiller);
        }

        private void ReferansBilgileriniGuncelle()
        {
            OnPropertiesChanged(
                nameof(ReferansAraziInfo),
                nameof(ReferansProjeInfo),
                nameof(ReferansSiyahKotInfo));
        }

        private void OzetBilgileriniGuncelle()
        {
            OnPropertiesChanged(
                nameof(ToplamKesit),
                nameof(SorunsuzKesit),
                nameof(UyariKesit),
                nameof(HataliKesit));
        }
    }
}
