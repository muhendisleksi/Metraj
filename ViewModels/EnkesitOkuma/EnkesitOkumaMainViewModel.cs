using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
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

        private int _aktifAdim = 1;
        private string _durumMesaji = "Hazir";
        private List<ObjectId> _secilenEntityler;
        private List<AnchorNokta> _anchorlar;
        private KesitPenceresi _pencere;
        private List<KesitGrubu> _kesitler;
        private ReferansKesitSablonu _sablon;
        private TopluTaramaSonucu _taramaSonucu;
        private int _ilerlemeYuzde;

        public EnkesitOkumaMainViewModel(
            IAnchorTaramaService anchorService,
            IKesitGruplamaService gruplamaService,
            ICizgiRolAtamaService rolAtamaService,
            IKesitAlanHesapService alanHesapService,
            ITabloOkumaService tabloService,
            IEditorService editorService)
        {
            _anchorService = anchorService;
            _gruplamaService = gruplamaService;
            _rolAtamaService = rolAtamaService;
            _alanHesapService = alanHesapService;
            _tabloService = tabloService;
            _editorService = editorService;

            EntitySecCommand = new RelayCommand(EntitySec, () => AktifAdim == 1);
            PencereBelirleCommand = new RelayCommand(PencereBelirle, () => AktifAdim == 1 && _anchorlar != null);
            KalibrasyonAcCommand = new RelayCommand(KalibrasyonAc, () => AktifAdim == 2);
            SablonYukleCommand = new RelayCommand(SablonYukle, () => AktifAdim == 2);
            DogrulamaAcCommand = new RelayCommand(DogrulamaAc, () => AktifAdim == 3 && _kesitler != null);
            ExcelAktarCommand = new RelayCommand(ExcelAktar, () => AktifAdim == 3 && _kesitler != null);
            JsonKaydetCommand = new RelayCommand(JsonKaydet, () => AktifAdim == 3);
            HesaplaCommand = new RelayCommand(Hesapla, () => AktifAdim == 3 && _kesitler != null);
            IleriCommand = new RelayCommand(AdimIleri, () => AktifAdim < 3);
            GeriCommand = new RelayCommand(AdimGeri, () => AktifAdim > 1);
        }

        public int AktifAdim
        {
            get => _aktifAdim;
            set
            {
                if (SetProperty(ref _aktifAdim, value))
                    OnPropertiesChanged(nameof(Adim1Aktif), nameof(Adim2Aktif), nameof(Adim3Aktif));
            }
        }

        public string DurumMesaji { get => _durumMesaji; set => SetProperty(ref _durumMesaji, value); }
        public int IlerlemeYuzde { get => _ilerlemeYuzde; set => SetProperty(ref _ilerlemeYuzde, value); }
        public List<AnchorNokta> Anchorlar { get => _anchorlar; set => SetProperty(ref _anchorlar, value); }
        public KesitPenceresi Pencere { get => _pencere; set => SetProperty(ref _pencere, value); }
        public List<KesitGrubu> Kesitler { get => _kesitler; set => SetProperty(ref _kesitler, value); }
        public ReferansKesitSablonu Sablon { get => _sablon; set => SetProperty(ref _sablon, value); }
        public TopluTaramaSonucu TaramaSonucu { get => _taramaSonucu; set => SetProperty(ref _taramaSonucu, value); }

        // 3 adim
        public bool Adim1Aktif => AktifAdim == 1;
        public bool Adim2Aktif => AktifAdim == 2;
        public bool Adim3Aktif => AktifAdim == 3;

        public string EntitySayisi => _secilenEntityler != null ? $"{_secilenEntityler.Count:N0} entity secildi" : "";
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
                return $"{_kesitler.Count} kesit -- {onayli} onay, {duzeltildi} duzeltme";
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
        public ICommand IleriCommand { get; }
        public ICommand GeriCommand { get; }

        private void EntitySec()
        {
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var result = ed.GetSelection();
                if (result.Status != PromptStatus.OK) return;

                _secilenEntityler = new List<ObjectId>(result.Value.GetObjectIds());
                DurumMesaji = $"{_secilenEntityler.Count:N0} entity secildi";
                OnPropertyChanged(nameof(EntitySayisi));
                LoggingService.Info(DurumMesaji);

                // Otomatik anchor tara
                AnchorTara();
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Entity secim hatasi: " + ex.Message;
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
                    DurumMesaji = $"{Anchorlar.Count} istasyon bulundu ({ilk} ... {son})";
                }
                else
                {
                    DurumMesaji = "Istasyon text'i bulunamadi";
                }
                OnPropertyChanged(nameof(AnchorSayisi));
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Anchor tarama hatasi: " + ex.Message;
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
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Pencere belirleme hatasi: " + ex.Message;
                LoggingService.Error("Pencere belirleme hatasi", ex);
            }
        }

        private void KalibrasyonAc()
        {
            try
            {
                if (_anchorlar == null || _pencere == null || _secilenEntityler == null)
                {
                    DurumMesaji = "Once hazirlik adimini tamamlayin";
                    return;
                }

                var ilkKesitler = _gruplamaService.KesitGrupla(
                    new List<AnchorNokta> { _anchorlar[0] }, _pencere, _secilenEntityler);

                if (ilkKesitler.Count == 0)
                {
                    DurumMesaji = "Referans kesitte cizgi bulunamadi";
                    return;
                }

                var refKesit = ilkKesitler[0];
                var vm = ServiceContainer.GetRequiredService<ReferansKesitViewModel>();
                vm.Yukle(refKesit);

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
                    DurumMesaji = $"{Sablon.Kurallar.Count} cizgi rolu tanimlandi";

                    // Otomatik toplu tarama baslat
                    TopluTara();
                }
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Kalibrasyon hatasi: " + ex.Message;
                LoggingService.Error("Kalibrasyon hatasi", ex);
            }
        }

        private void SablonYukle()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON Dosyasi|*.json",
                    Title = "Sablon Yukle"
                };

                if (dialog.ShowDialog() == true)
                {
                    string json = System.IO.File.ReadAllText(dialog.FileName);
                    Sablon = JsonConvert.DeserializeObject<ReferansKesitSablonu>(json);
                    DurumMesaji = $"Sablon yuklendi: {Sablon.Kurallar.Count} kural";

                    // Otomatik toplu tarama baslat
                    TopluTara();
                }
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Sablon yukleme hatasi: " + ex.Message;
                LoggingService.Error("Sablon yukleme hatasi", ex);
            }
        }

        private void TopluTara()
        {
            try
            {
                DurumMesaji = "Tarama basliyor...";
                IlerlemeYuzde = 0;

                Kesitler = _gruplamaService.KesitGrupla(_anchorlar, _pencere, _secilenEntityler);
                IlerlemeYuzde = 30;

                _rolAtamaService.TopluRolAta(Kesitler, _sablon);
                IlerlemeYuzde = 60;

                _alanHesapService.TopluAlanHesapla(Kesitler);
                IlerlemeYuzde = 80;

                _tabloService.TopluKiyasla(Kesitler);
                IlerlemeYuzde = 100;

                int uyumlu = Kesitler.Count(k => k.Durum == DogrulamaDurumu.Onaylandi);
                int uyari = Kesitler.Count(k => k.Durum == DogrulamaDurumu.Bekliyor);
                int sorunlu = Kesitler.Count(k => k.Durum == DogrulamaDurumu.Sorunlu);

                TaramaSonucu = new TopluTaramaSonucu
                {
                    Kesitler = Kesitler,
                    Sablon = Sablon,
                    TaramaTarihi = DateTime.Now,
                    ToplamKesit = Kesitler.Count,
                    OnayliKesit = uyumlu,
                    UyariKesit = uyari,
                    SorunluKesit = sorunlu
                };

                DurumMesaji = $"{Kesitler.Count} kesit tarandi -- {uyumlu} uyumlu, {uyari} uyari, {sorunlu} sorunlu";
                OnPropertiesChanged(nameof(KesitSayisi), nameof(SonucBilgisi));

                // Otomatik sonuc adimina gec
                AktifAdim = 3;
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Toplu tarama hatasi: " + ex.Message;
                LoggingService.Error("Toplu tarama hatasi", ex);
            }
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
                DurumMesaji = $"{Kesitler.Count} kesit dogrulandi ({onayli} onay, {duzeltildi} duzeltme)";
                OnPropertyChanged(nameof(SonucBilgisi));
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Dogrulama hatasi: " + ex.Message;
                LoggingService.Error("Dogrulama hatasi", ex);
            }
        }

        private void Hesapla()
        {
            if (_kesitler == null) return;
            _alanHesapService.TopluAlanHesapla(_kesitler);
            _tabloService.TopluKiyasla(_kesitler);
            DurumMesaji = "Hesaplama tamamlandi";
            OnPropertyChanged(nameof(SonucBilgisi));
        }

        private void ExcelAktar()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Dosyasi|*.xlsx",
                    FileName = "YolEnkesitOkuma_Sonuc.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    var exportService = ServiceContainer.GetRequiredService<Services.Interfaces.IExcelExportService>();
                    exportService.EnkesitOkumaExport(Kesitler, dialog.FileName);
                    DurumMesaji = "Excel dosyasi kaydedildi";
                }
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Excel aktarim hatasi: " + ex.Message;
                LoggingService.Error("Excel aktarim hatasi", ex);
            }
        }

        private void JsonKaydet()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON Dosyasi|*.json",
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
                DurumMesaji = "JSON kayit hatasi: " + ex.Message;
                LoggingService.Error("JSON kayit hatasi", ex);
            }
        }

        private void AdimIleri()
        {
            if (AktifAdim < 3) AktifAdim++;
        }

        private void AdimGeri()
        {
            if (AktifAdim > 1) AktifAdim--;
        }
    }
}
