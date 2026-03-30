using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.YolEnkesit;
using Metraj.Services;
using Metraj.Services.YolEnkesit;
using Newtonsoft.Json;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Metraj.ViewModels.EnkesitOkuma
{
    public class EnkesitOkumaMainViewModel : ViewModelBase
    {
        private readonly IAnchorTaramaService _anchorService;
        private readonly IKesitGruplamaService _gruplamaService;
        private readonly ICizgiRolAtamaService _rolAtamaService;
        private readonly IKesitAlanHesapService _alanHesapService;
        private readonly ITabloOkumaService _tabloService;
        private readonly IEditorService _editorService;
        private readonly EntityCacheService _cacheService;

        private string _durumMesaji = "Hazır";
        private List<ObjectId> _secilenEntityler;
        private List<AnchorNokta> _anchorlar;
        private KesitPenceresi _pencere;
        private List<KesitGrubu> _kesitler;
        private ReferansKesitSablonu _sablon;
        private TopluTaramaSonucu _taramaSonucu;
        private int _ilerlemeYuzde;
        private bool _taramaDevamEdiyor;
        private bool _iptalIstendi;
        private string _ilerlemeDetay = "";

        public EnkesitOkumaMainViewModel(
            IAnchorTaramaService anchorService,
            IKesitGruplamaService gruplamaService,
            ICizgiRolAtamaService rolAtamaService,
            IKesitAlanHesapService alanHesapService,
            ITabloOkumaService tabloService,
            IEditorService editorService,
            EntityCacheService cacheService)
        {
            _anchorService = anchorService;
            _gruplamaService = gruplamaService;
            _rolAtamaService = rolAtamaService;
            _alanHesapService = alanHesapService;
            _tabloService = tabloService;
            _editorService = editorService;
            _cacheService = cacheService;

            EntitySecCommand = new RelayCommand(EntitySec, () => !_taramaDevamEdiyor);
            PencereBelirleCommand = new RelayCommand(PencereBelirle, () => _anchorlar != null && !_taramaDevamEdiyor);
            KalibrasyonAcCommand = new RelayCommand(KalibrasyonAc, () => _anchorlar != null && _pencere != null && !_taramaDevamEdiyor);
            SablonYukleCommand = new RelayCommand(SablonYukle, () => _anchorlar != null && _pencere != null && !_taramaDevamEdiyor);
            DogrulamaAcCommand = new RelayCommand(DogrulamaAc, () => _kesitler != null && _kesitler.Count > 0 && !_taramaDevamEdiyor);
            ExcelAktarCommand = new RelayCommand(ExcelAktar, () => _kesitler != null && _kesitler.Count > 0 && !_taramaDevamEdiyor);
            JsonKaydetCommand = new RelayCommand(JsonKaydet, () => !_taramaDevamEdiyor);
            HesaplaCommand = new RelayCommand(Hesapla, () => _kesitler != null && _kesitler.Count > 0 && !_taramaDevamEdiyor);
            IptalCommand = new RelayCommand(IptalEt, () => _taramaDevamEdiyor);
        }

        /// <summary>Secim oncesi panel gizleme, sonrasi gosterme icin.</summary>
        public event Action PanelGizle;
        public event Action PanelGoster;

        public string DurumMesaji { get => _durumMesaji; set => SetProperty(ref _durumMesaji, value); }
        public int IlerlemeYuzde { get => _ilerlemeYuzde; set => SetProperty(ref _ilerlemeYuzde, value); }
        public string IlerlemeDetay { get => _ilerlemeDetay; set => SetProperty(ref _ilerlemeDetay, value); }
        public bool TaramaDevamEdiyor { get => _taramaDevamEdiyor; set { if (SetProperty(ref _taramaDevamEdiyor, value)) OnPropertyChanged(nameof(IptalGorunur)); } }
        public bool IptalGorunur => _taramaDevamEdiyor;

        public List<AnchorNokta> Anchorlar { get => _anchorlar; set => SetProperty(ref _anchorlar, value); }
        public KesitPenceresi Pencere { get => _pencere; set => SetProperty(ref _pencere, value); }
        public List<KesitGrubu> Kesitler { get => _kesitler; set => SetProperty(ref _kesitler, value); }
        public ReferansKesitSablonu Sablon { get => _sablon; set => SetProperty(ref _sablon, value); }
        public TopluTaramaSonucu TaramaSonucu { get => _taramaSonucu; set => SetProperty(ref _taramaSonucu, value); }

        public string EntitySayisi => _secilenEntityler != null ? $"{_secilenEntityler.Count:N0} obje seçildi" : "";
        public string AnchorSayisi => _anchorlar != null ? $"{_anchorlar.Count} istasyon bulundu" : "";
        public string PencereBilgisi => _pencere != null ? $"Pencere: {_pencere.Genislik:F1} x {_pencere.Yukseklik:F1}" : "";
        public string KesitSayisi => _kesitler != null ? $"{_kesitler.Count} kesit" : "";
        public string SonucBilgisi
        {
            get
            {
                if (_kesitler == null) return "";
                int onayli = _kesitler.Count(k => k.Durum == DogrulamaDurumu.Onaylandi);
                int duzeltildi = _kesitler.Count(k => k.Durum == DogrulamaDurumu.Duzeltildi);
                return $"{_kesitler.Count} kesit -- {onayli} onay, {duzeltildi} düzeltme";
            }
        }

        public string KesitOzeti => _kesitler != null ? $"Kesit: {_kesitler.Count}" : "Kesit: —";

        public string IstasyonAraligi
        {
            get
            {
                if (_anchorlar == null || _anchorlar.Count == 0) return "İstasyon: —";
                string ilk = YolKesitService.IstasyonFormatla(_anchorlar.First().Istasyon);
                string son = YolKesitService.IstasyonFormatla(_anchorlar.Last().Istasyon);
                return $"{ilk} ... {son}";
            }
        }

        public ICommand EntitySecCommand { get; }
        public ICommand PencereBelirleCommand { get; }
        public ICommand KalibrasyonAcCommand { get; }
        public ICommand SablonYukleCommand { get; }
        public ICommand DogrulamaAcCommand { get; }
        public ICommand ExcelAktarCommand { get; }
        public ICommand JsonKaydetCommand { get; }
        public ICommand HesaplaCommand { get; }
        public ICommand IptalCommand { get; }

        /// <summary>
        /// UI thread'ine nefes aldirir — bekleyen render/input event'lerini isler.
        /// AutoCAD API ana thread gerektirdiginden async kullanilamaz, bunun yerine
        /// her N kesit isleminden sonra bu metod cagrilir.
        /// </summary>
        private void UIGuncelle()
        {
            try
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
            catch { }
        }

        private void ButonDurumGuncelle()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void IptalEt()
        {
            _iptalIstendi = true;
            DurumMesaji = "İptal ediliyor...";
        }

        private void EntitySec()
        {
            try
            {
                PanelGizle?.Invoke();

                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var result = ed.GetSelection();

                PanelGoster?.Invoke();

                if (result.Status != PromptStatus.OK) return;

                _secilenEntityler = new List<ObjectId>(result.Value.GetObjectIds());
                DurumMesaji = $"{_secilenEntityler.Count:N0} obje seçildi";
                OnPropertyChanged(nameof(EntitySayisi));
                LoggingService.Info(DurumMesaji);

                AnchorTara();
            }
            catch (System.Exception ex)
            {
                PanelGoster?.Invoke();
                DurumMesaji = "Seçim hatası: " + ex.Message;
                LoggingService.Error("Entity secim hatasi", ex);
            }
        }

        private void AnchorTara()
        {
            try
            {
                Anchorlar = _anchorService.AnchorTara(_secilenEntityler);
                if (Anchorlar.Count > 0)
                {
                    string ilk = YolKesitService.IstasyonFormatla(Anchorlar.First().Istasyon);
                    string son = YolKesitService.IstasyonFormatla(Anchorlar.Last().Istasyon);

                    // CL bazli otomatik pencere tespiti
                    double platformYariGenislik = _anchorService.PlatformGenisligiTespit(Anchorlar, _secilenEntityler);
                    var ilkAnchor = Anchorlar[0];
                    Pencere = KesitPenceresi.CL_Bazli(ilkAnchor.CL_MinY, ilkAnchor.CL_MaxY, platformYariGenislik);

                    DurumMesaji = $"{Anchorlar.Count} kesit bulundu ({ilk} ... {son})";
                    OnPropertiesChanged(nameof(AnchorSayisi), nameof(PencereBilgisi), nameof(IstasyonAraligi));
                }
                else
                {
                    DurumMesaji = "CL+Km eşleşmesi bulunamadı";
                }
                OnPropertyChanged(nameof(AnchorSayisi));
                ButonDurumGuncelle();
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Anchor tarama hatası: " + ex.Message;
                LoggingService.Error("Anchor tarama hatasi", ex);
            }
        }

        private void PencereBelirle()
        {
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;

                var pt1Result = ed.GetPoint("\nPencere sol-alt kosesini tiklayin: ");
                if (pt1Result.Status != PromptStatus.OK) return;

                var pt2Result = ed.GetCorner("\nPencere sag-ust kosesini tiklayin: ", pt1Result.Value);
                if (pt2Result.Status != PromptStatus.OK) return;

                var pt1 = pt1Result.Value;
                var pt2 = pt2Result.Value;

                var ilkAnchor = Anchorlar[0];

                Pencere = new KesitPenceresi
                {
                    Genislik = Math.Abs(pt2.X - pt1.X),
                    Yukseklik = Math.Abs(pt2.Y - pt1.Y),
                    OffsetSolX = ilkAnchor.X - Math.Min(pt1.X, pt2.X),
                    OffsetSagX = Math.Max(pt1.X, pt2.X) - ilkAnchor.X,
                    OffsetAltY = ilkAnchor.Y - Math.Min(pt1.Y, pt2.Y),
                    OffsetUstY = Math.Max(pt1.Y, pt2.Y) - ilkAnchor.Y
                };

                DurumMesaji = $"Pencere: {Pencere.Genislik:F1} x {Pencere.Yukseklik:F1} birim";
                OnPropertyChanged(nameof(PencereBilgisi));
                ButonDurumGuncelle();
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Pencere belirleme hatası: " + ex.Message;
                LoggingService.Error("Pencere belirleme hatasi", ex);
            }
        }

        private void KalibrasyonAc()
        {
            try
            {
                if (_anchorlar == null || _pencere == null || _secilenEntityler == null)
                {
                    DurumMesaji = "Önce hazırlık adımını tamamlayın";
                    return;
                }

                // Cache yoksa olustur (kalibrasyon TopluTara'dan once calisir)
                if (_cacheService.Cache == null)
                    _cacheService.CacheOlustur(_secilenEntityler);

                // Kesit secim dialogu
                int? secilenIdx = KesitSecimDialoguGoster();
                if (!secilenIdx.HasValue) return;

                var secilenAnchor = _anchorlar[secilenIdx.Value];
                var secilenKesitler = _gruplamaService.KesitGrupla(
                    new List<AnchorNokta> { secilenAnchor }, _pencere, _secilenEntityler);

                if (secilenKesitler.Count == 0)
                {
                    DurumMesaji = "Seçilen kesitte çizgi bulunamadı";
                    return;
                }

                var refKesit = secilenKesitler[0];
                var vm = ServiceContainer.GetRequiredService<ReferansKesitViewModel>();
                vm.Yukle(refKesit);

                // Kesit navigasyonu: tum anchor listesini ver, kalibrasyon ekraninda kesit degistirebilsin
                vm.NavigasyonYukle(_anchorlar, _pencere, _secilenEntityler, _gruplamaService, secilenIdx.Value);

                var window = new Views.EnkesitOkuma.ReferansKesitWindow();
                window.DataContext = vm;

                try
                {
                    var acadWin = AcadApp.MainWindow;
                    if (acadWin != null)
                    {
                        var helper = new System.Windows.Interop.WindowInteropHelper(window);
                        helper.Owner = acadWin.Handle;
                    }
                }
                catch { }

                if (window.ShowDialog() == true)
                {
                    Sablon = vm.OlusturulanSablon;
                    DurumMesaji = $"{Sablon.Kurallar.Count} çizgi rolü tanımlandı";

                    TopluTara();
                }
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Kalibrasyon hatası: " + ex.Message;
                LoggingService.Error("Kalibrasyon hatasi", ex);
            }
        }

        private void SablonYukle()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON Dosyası|*.json",
                    Title = "Sablon Yukle"
                };

                if (dialog.ShowDialog() == true)
                {
                    string json = System.IO.File.ReadAllText(dialog.FileName);
                    Sablon = JsonConvert.DeserializeObject<ReferansKesitSablonu>(json);
                    DurumMesaji = $"Şablon yüklendi: {Sablon.Kurallar.Count} kural";

                    TopluTara();
                }
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Şablon yükleme hatası: " + ex.Message;
                LoggingService.Error("Sablon yukleme hatasi", ex);
            }
        }

        private void TopluTara()
        {
            try
            {
                TaramaDevamEdiyor = true;
                _iptalIstendi = false;
                IlerlemeYuzde = 0;
                IlerlemeDetay = "Entity'ler okunuyor...";
                DurumMesaji = "Tarama basliyor...";
                ButonDurumGuncelle();
                UIGuncelle();

                // Faz 0: Entity cache olusturma (%0 → %40)
                _cacheService.CacheOlustur(_secilenEntityler, (okunan, toplam) =>
                {
                    if (_iptalIstendi) return false;
                    int yuzde = (int)((double)okunan / toplam * 40);
                    IlerlemeYuzde = yuzde;
                    IlerlemeDetay = $"Entity okuma: {okunan:N0} / {toplam:N0}";
                    UIGuncelle();
                    return true;
                });

                if (_iptalIstendi) { TaramaBitir("Iptal edildi"); return; }

                // Faz 1: Kesit gruplama (%40 → %55)
                IlerlemeYuzde = 40;
                IlerlemeDetay = "Kesit gruplama...";
                UIGuncelle();

                Kesitler = _gruplamaService.KesitGrupla(_anchorlar, _pencere, _secilenEntityler);
                int toplam2 = Kesitler.Count;

                if (_iptalIstendi) { TaramaBitir("Iptal edildi"); return; }

                IlerlemeYuzde = 55;
                IlerlemeDetay = $"{toplam2} kesit bulundu";
                UIGuncelle();

                // Faz 2: Rol atama (%55 → %70)
                for (int i = 0; i < toplam2; i++)
                {
                    if (_iptalIstendi) { TaramaBitir($"Iptal edildi ({i}/{toplam2} kesit islendi)"); return; }

                    _rolAtamaService.OtomatikRolAta(Kesitler[i], _sablon);

                    if ((i + 1) % 10 == 0 || i == toplam2 - 1)
                    {
                        int yuzde = 55 + (int)((double)(i + 1) / toplam2 * 15);
                        IlerlemeYuzde = yuzde;
                        IlerlemeDetay = $"Rol atama: {i + 1} / {toplam2}";
                        UIGuncelle();
                    }
                }

                // Faz 3: Alan hesabi (%70 → %85)
                for (int i = 0; i < toplam2; i++)
                {
                    if (_iptalIstendi) { TaramaBitir($"Iptal edildi ({i}/{toplam2} alan hesabi)"); return; }

                    _alanHesapService.AlanHesapla(Kesitler[i]);

                    if ((i + 1) % 10 == 0 || i == toplam2 - 1)
                    {
                        int yuzde = 70 + (int)((double)(i + 1) / toplam2 * 15);
                        IlerlemeYuzde = yuzde;
                        IlerlemeDetay = $"Alan hesabi: {i + 1} / {toplam2}";
                        UIGuncelle();
                    }
                }

                // Faz 4: Tablo kiyaslama (%85 → %100)
                for (int i = 0; i < toplam2; i++)
                {
                    if (_iptalIstendi) { TaramaBitir($"Iptal edildi ({i}/{toplam2} kiyas)"); return; }

                    _tabloService.Kiyasla(Kesitler[i]);

                    if ((i + 1) % 20 == 0 || i == toplam2 - 1)
                    {
                        int yuzde = 85 + (int)((double)(i + 1) / toplam2 * 15);
                        IlerlemeYuzde = yuzde;
                        IlerlemeDetay = $"Tablo kiyasi: {i + 1} / {toplam2}";
                        UIGuncelle();
                    }
                }

                IlerlemeYuzde = 100;

                int uyumlu = Kesitler.Count(k => k.Durum == DogrulamaDurumu.Onaylandi);
                int uyari = Kesitler.Count(k => k.Durum == DogrulamaDurumu.Bekliyor);
                int sorunlu = Kesitler.Count(k => k.Durum == DogrulamaDurumu.Sorunlu);

                TaramaSonucu = new TopluTaramaSonucu
                {
                    Kesitler = Kesitler,
                    Sablon = Sablon,
                    TaramaTarihi = DateTime.Now,
                    ToplamKesit = toplam2,
                    OnayliKesit = uyumlu,
                    UyariKesit = uyari,
                    SorunluKesit = sorunlu
                };

                // NOT: TanilamaRaporuYaz buradan kaldirildi — TraceBoundary cagrisi
                // UI'i dakikalarca kilitliyordu. Rapor gerektiginde ayri tetiklenir.

                TaramaBitir($"{toplam2} kesit tarandi -- {uyumlu} uyumlu, {uyari} uyari, {sorunlu} sorunlu");
                OnPropertiesChanged(nameof(KesitSayisi), nameof(SonucBilgisi), nameof(KesitOzeti));
            }
            catch (System.Exception ex)
            {
                TaramaBitir("Toplu tarama hatasi: " + ex.Message);
                LoggingService.Error("Toplu tarama hatasi", ex);
            }
        }

        private void TaramaBitir(string mesaj)
        {
            TaramaDevamEdiyor = false;
            _iptalIstendi = false;
            DurumMesaji = mesaj;
            IlerlemeDetay = "";
            ButonDurumGuncelle();
        }

        private void DogrulamaAc()
        {
            try
            {
                var vm = ServiceContainer.GetRequiredService<KesitDogrulamaViewModel>();
                vm.Yukle(Kesitler);

                var window = new Views.EnkesitOkuma.KesitDogrulamaWindow();
                window.DataContext = vm;

                try
                {
                    var acadWin = AcadApp.MainWindow;
                    if (acadWin != null)
                    {
                        var helper = new System.Windows.Interop.WindowInteropHelper(window);
                        helper.Owner = acadWin.Handle;
                    }
                }
                catch { }

                window.ShowDialog();

                int onayli = Kesitler.Count(k => k.Durum == DogrulamaDurumu.Onaylandi);
                int duzeltildi = Kesitler.Count(k => k.Durum == DogrulamaDurumu.Duzeltildi);
                DurumMesaji = $"{Kesitler.Count} kesit doğrulandı ({onayli} onay, {duzeltildi} düzeltme)";
                OnPropertyChanged(nameof(SonucBilgisi));
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Doğrulama hatası: " + ex.Message;
                LoggingService.Error("Dogrulama hatasi", ex);
            }
        }

        private void Hesapla()
        {
            if (_kesitler == null) return;
            _alanHesapService.TopluAlanHesapla(_kesitler);
            _tabloService.TopluKiyasla(_kesitler);
            DurumMesaji = "Hesaplama tamamlandı";
            OnPropertyChanged(nameof(SonucBilgisi));
        }

        private void ExcelAktar()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Dosyası|*.xlsx",
                    FileName = "YolEnkesitOkuma_Sonuc.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    var exportService = ServiceContainer.GetRequiredService<Services.Interfaces.IExcelExportService>();
                    exportService.EnkesitOkumaExport(Kesitler, dialog.FileName);
                    DurumMesaji = "Excel dosyası kaydedildi";
                }
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Excel aktarım hatası: " + ex.Message;
                LoggingService.Error("Excel aktarim hatasi", ex);
            }
        }

        private void JsonKaydet()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON Dosyası|*.json",
                    FileName = "YolEnkesitOkuma_Veri.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    string json = JsonConvert.SerializeObject(TaramaSonucu, Formatting.Indented,
                        new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    DurumMesaji = "Proje verisi kaydedildi";
                }
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "JSON kayıt hatası: " + ex.Message;
                LoggingService.Error("JSON kayit hatasi", ex);
            }
        }

        private int? KesitSecimDialoguGoster()
        {
            var ogeler = _anchorlar.Select((a, i) => new Models.YolEnkesit.KesitSecimOgesi
            {
                Index = i,
                IstasyonMetni = YolKesitService.IstasyonFormatla(a.Istasyon),
                Aciklama = i == 0 ? " (ilk)" : i == _anchorlar.Count - 1 ? " (son)" : ""
            }).ToList();

            int varsayilanIndex = _anchorlar.Count / 3;

            var window = new Views.EnkesitOkuma.KesitSecimWindow();
            window.Yukle(ogeler, varsayilanIndex);

            try
            {
                var acadWin = AcadApp.MainWindow;
                if (acadWin != null)
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(window);
                    helper.Owner = acadWin.Handle;
                }
            }
            catch { }

            if (window.ShowDialog() == true)
                return window.SecilenIndex;
            return null;
        }
    }
}
