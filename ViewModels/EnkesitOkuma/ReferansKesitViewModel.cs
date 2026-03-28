using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Models.YolEnkesit;
using Metraj.Services;
using Metraj.Services.YolEnkesit;
using Newtonsoft.Json;

namespace Metraj.ViewModels.EnkesitOkuma
{
    public class LayerFiltreOgesi : INotifyPropertyChanged
    {
        private bool _gorunur = true;
        public string LayerAdi { get; set; }
        public int CizgiSayisi { get; set; }
        public bool Gorunur
        {
            get => _gorunur;
            set
            {
                if (_gorunur == value) return;
                _gorunur = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gorunur)));
                GorunurlukDegisti?.Invoke();
            }
        }
        public Action GorunurlukDegisti { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class ReferansKesitViewModel : ViewModelBase
    {
        private readonly ICizgiRolAtamaService _rolAtamaService;
        private KesitGrubu _referansKesit;
        private CizgiTanimi _secilenCizgi;
        private CizgiRolu _secilenRol;
        private ReferansKesitSablonu _olusturulanSablon;
        private bool _cerceveGoster;

        // Kesit navigasyonu
        private List<AnchorNokta> _tumAnchorlar;
        private KesitPenceresi _pencere;
        private List<ObjectId> _tumEntityler;
        private IKesitGruplamaService _gruplamaService;
        private int _aktifKesitIndex;
        private KesitSecimOgesi _secilenKesitOgesi;

        private static readonly HashSet<string> VarsayilanKapaliLayerler = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase) { "0", "1", "Defpoints" };

        public ReferansKesitViewModel(ICizgiRolAtamaService rolAtamaService)
        {
            _rolAtamaService = rolAtamaService;

            RolAta = new RelayCommand(RolAtaUygula, () => SecilenCizgi != null);
            TumunuOtomatikAta = new RelayCommand(OtomatikAta);
            Kaydet = new RelayCommand(SablonKaydet);
            SablonDosyayaKaydet = new RelayCommand(SablonDosyayaKaydetUygula);
            TumLayerAc = new RelayCommand(() => { foreach (var l in LayerFiltresi) l.Gorunur = true; });
            TumLayerKapat = new RelayCommand(() => { foreach (var l in LayerFiltresi) l.Gorunur = false; });
            OncekiKesitCommand = new RelayCommand(OncekiKesit, () => NavigasyonAktif && _aktifKesitIndex > 0);
            SonrakiKesitCommand = new RelayCommand(SonrakiKesit, () => NavigasyonAktif && _aktifKesitIndex < (_tumAnchorlar?.Count ?? 0) - 1);

            MevcutRoller = new ObservableCollection<CizgiRolu>(
                Enum.GetValues(typeof(CizgiRolu)).Cast<CizgiRolu>());
        }

        public ObservableCollection<CizgiTanimi> Cizgiler { get; } = new ObservableCollection<CizgiTanimi>();
        public ObservableCollection<CizgiTanimi> GoruntulenenCizgiler { get; } = new ObservableCollection<CizgiTanimi>();
        public ObservableCollection<LayerFiltreOgesi> LayerFiltresi { get; } = new ObservableCollection<LayerFiltreOgesi>();
        public ObservableCollection<CizgiRolu> MevcutRoller { get; }

        public event EventHandler FiltreDegisti;

        public CizgiTanimi SecilenCizgi
        {
            get => _secilenCizgi;
            set
            {
                if (SetProperty(ref _secilenCizgi, value))
                {
                    SecilenRol = value?.Rol ?? CizgiRolu.Tanimsiz;
                    OnPropertiesChanged(nameof(SecilenCizgiBilgi));
                }
            }
        }

        public CizgiRolu SecilenRol
        {
            get => _secilenRol;
            set => SetProperty(ref _secilenRol, value);
        }

        public bool CerceveGoster
        {
            get => _cerceveGoster;
            set { if (SetProperty(ref _cerceveGoster, value)) FiltreUygula(); }
        }

        public string SecilenCizgiBilgi => SecilenCizgi != null
            ? $"Layer: {SecilenCizgi.LayerAdi} | Renk: {SecilenCizgi.RenkIndex} | Y: {SecilenCizgi.OrtalamaY:F2}"
            : "";

        public ReferansKesitSablonu OlusturulanSablon => _olusturulanSablon;

        // Kesit navigasyon property'leri
        public ObservableCollection<KesitSecimOgesi> KesitSecenekleri { get; } = new ObservableCollection<KesitSecimOgesi>();
        public bool NavigasyonAktif => _tumAnchorlar != null && _tumAnchorlar.Count > 1;

        public KesitSecimOgesi SecilenKesitOgesi
        {
            get => _secilenKesitOgesi;
            set
            {
                if (SetProperty(ref _secilenKesitOgesi, value) && value != null && value.Index != _aktifKesitIndex)
                    KesitDegistir(value.Index);
            }
        }

        public string AktifKesitBilgi => _tumAnchorlar != null && _aktifKesitIndex < _tumAnchorlar.Count
            ? $"Kesit {_aktifKesitIndex + 1} / {_tumAnchorlar.Count}"
            : "";

        public event EventHandler KesitDegistirildi;

        public ICommand RolAta { get; }
        public ICommand TumunuOtomatikAta { get; }
        public ICommand Kaydet { get; }
        public ICommand SablonDosyayaKaydet { get; }
        public ICommand TumLayerAc { get; }
        public ICommand TumLayerKapat { get; }
        public ICommand OncekiKesitCommand { get; }
        public ICommand SonrakiKesitCommand { get; }

        public void Yukle(KesitGrubu kesit)
        {
            _referansKesit = kesit;
            Cizgiler.Clear();
            foreach (var c in kesit.Cizgiler.OrderByDescending(c => c.OrtalamaY))
                Cizgiler.Add(c);

            OtomatikAta();
            LayerFiltresiOlustur();
            FiltreUygula();
        }

        public void NavigasyonYukle(List<AnchorNokta> anchorlar, KesitPenceresi pencere,
            List<ObjectId> entityler, IKesitGruplamaService gruplamaService, int baslangicIndex)
        {
            _tumAnchorlar = anchorlar;
            _pencere = pencere;
            _tumEntityler = entityler;
            _gruplamaService = gruplamaService;

            KesitSecenekleri.Clear();
            for (int i = 0; i < anchorlar.Count; i++)
            {
                KesitSecenekleri.Add(new KesitSecimOgesi
                {
                    Index = i,
                    IstasyonMetni = YolKesitService.IstasyonFormatla(anchorlar[i].Istasyon),
                    Aciklama = i == 0 ? " (ilk)" : i == anchorlar.Count - 1 ? " (son)" : ""
                });
            }

            _aktifKesitIndex = baslangicIndex;
            _secilenKesitOgesi = KesitSecenekleri.Count > baslangicIndex ? KesitSecenekleri[baslangicIndex] : null;
            OnPropertiesChanged(nameof(NavigasyonAktif), nameof(AktifKesitBilgi), nameof(SecilenKesitOgesi));
        }

        private void KesitDegistir(int yeniIndex)
        {
            if (_tumAnchorlar == null || yeniIndex < 0 || yeniIndex >= _tumAnchorlar.Count) return;
            _aktifKesitIndex = yeniIndex;

            var anchor = _tumAnchorlar[yeniIndex];
            var kesitler = _gruplamaService.KesitGrupla(
                new List<AnchorNokta> { anchor }, _pencere, _tumEntityler);

            if (kesitler.Count > 0)
                Yukle(kesitler[0]);

            _secilenKesitOgesi = KesitSecenekleri.Count > yeniIndex ? KesitSecenekleri[yeniIndex] : null;
            OnPropertiesChanged(nameof(AktifKesitBilgi), nameof(SecilenKesitOgesi));
            KesitDegistirildi?.Invoke(this, EventArgs.Empty);
        }

        private void OncekiKesit()
        {
            if (_aktifKesitIndex > 0) KesitDegistir(_aktifKesitIndex - 1);
        }

        private void SonrakiKesit()
        {
            if (_aktifKesitIndex < (_tumAnchorlar?.Count ?? 0) - 1) KesitDegistir(_aktifKesitIndex + 1);
        }

        private void LayerFiltresiOlustur()
        {
            LayerFiltresi.Clear();
            var layerGruplari = Cizgiler.GroupBy(c => c.LayerAdi ?? "(bos)")
                .OrderBy(g => g.Key);

            foreach (var grup in layerGruplari)
            {
                var oge = new LayerFiltreOgesi
                {
                    LayerAdi = grup.Key,
                    CizgiSayisi = grup.Count(),
                    Gorunur = !VarsayilanKapaliLayerler.Contains(grup.Key)
                };
                oge.GorunurlukDegisti = FiltreUygula;
                LayerFiltresi.Add(oge);
            }
        }

        private void FiltreUygula()
        {
            var gorunurLayerler = new HashSet<string>(
                LayerFiltresi.Where(l => l.Gorunur).Select(l => l.LayerAdi));

            GoruntulenenCizgiler.Clear();
            foreach (var c in Cizgiler)
            {
                if (!gorunurLayerler.Contains(c.LayerAdi ?? "(bos)")) continue;
                if (!CerceveGoster && (c.Rol == CizgiRolu.CerceveCizgisi || c.Rol == CizgiRolu.GridCizgisi)) continue;
                GoruntulenenCizgiler.Add(c);
            }

            OnPropertyChanged(nameof(GoruntulenenCizgiler));
            FiltreDegisti?.Invoke(this, EventArgs.Empty);
        }

        private void RolAtaUygula()
        {
            if (SecilenCizgi == null) return;
            SecilenCizgi.Rol = SecilenRol;
            SecilenCizgi.OtomatikAtanmis = false;

            int idx = Cizgiler.IndexOf(SecilenCizgi);
            if (idx >= 0)
            {
                Cizgiler.RemoveAt(idx);
                Cizgiler.Insert(idx, SecilenCizgi);
            }
            FiltreUygula();
        }

        private void OtomatikAta()
        {
            foreach (var cizgi in Cizgiler)
            {
                if (cizgi.Rol != CizgiRolu.Tanimsiz) continue;

                string upper = (cizgi.LayerAdi ?? "").ToUpperInvariant();

                if (upper.Contains("ZEMIN") || upper.Contains("SIYAH") || upper.Contains("ARAZI"))
                { cizgi.Rol = CizgiRolu.Zemin; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("SIYIRMA") || upper.Contains("SIYRIM"))
                { cizgi.Rol = CizgiRolu.SiyirmaTaban; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("PROJE") || upper.Contains("KIRMIZI") || upper.Contains("DESIGN") || upper.Contains("TASARIM"))
                { cizgi.Rol = CizgiRolu.ProjeKotu; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("EKSEN") || upper.Contains("CL") || upper.Contains("AXIS"))
                { cizgi.Rol = CizgiRolu.EksenCizgisi; cizgi.OtomatikAtanmis = true; continue; }

                // Cerceve / Palye (kesin cerceve)
                if (upper.Contains("CERCEVE") || upper.Contains("FRAME") || upper.Contains("PALYE"))
                { cizgi.Rol = CizgiRolu.CerceveCizgisi; cizgi.OtomatikAtanmis = true; continue; }

                // PAFTA: akilli ayirim — kisa/dikey cizgiler cerceve, uzun yatay cizgiler tanimsiz birak
                if (upper.Contains("PAFTA"))
                {
                    if (CizgiKisaMi(cizgi))
                    { cizgi.Rol = CizgiRolu.CerceveCizgisi; cizgi.OtomatikAtanmis = true; }
                    continue;
                }

                if (upper.Contains("GRID") || upper.Contains("OLCEK"))
                { cizgi.Rol = CizgiRolu.GridCizgisi; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("HENDEK"))
                { cizgi.Rol = CizgiRolu.HendekCizgisi; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("SEV"))
                { cizgi.Rol = CizgiRolu.SevCizgisi; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("BANKET") || upper.Contains("BORDUR"))
                { cizgi.Rol = CizgiRolu.BanketCizgisi; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("ASINMA"))
                { cizgi.Rol = CizgiRolu.AsinmaTaban; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("BINDER"))
                { cizgi.Rol = CizgiRolu.BinderTaban; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("BITUMEN") || upper.Contains("BITUMLU") || upper.Contains("BITUM"))
                { cizgi.Rol = CizgiRolu.BitumluTemelTaban; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("GRANULER") || upper.Contains("ALTTEMEL") || upper.Contains("ALT TEMEL"))
                { cizgi.Rol = CizgiRolu.AltTemelTaban; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("PLENT"))
                { cizgi.Rol = CizgiRolu.PlentmiksTaban; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("KIRMA"))
                { cizgi.Rol = CizgiRolu.KirmatasTaban; cizgi.OtomatikAtanmis = true; continue; }

                // Renk bazli
                switch (cizgi.RenkIndex)
                {
                    case 3: cizgi.Rol = CizgiRolu.Zemin; cizgi.OtomatikAtanmis = true; break;
                    case 5: cizgi.Rol = CizgiRolu.SiyirmaTaban; cizgi.OtomatikAtanmis = true; break;
                    case 1: cizgi.Rol = CizgiRolu.ProjeKotu; cizgi.OtomatikAtanmis = true; break;
                }
            }

            // Y pozisyonuna gore ustyapi tabakalari
            var gizliTabakalar = Cizgiler
                .Where(c => c.Rol == CizgiRolu.Tanimsiz && (c.RenkIndex == 7 || c.RenkIndex == 8 || c.RenkIndex == 9))
                .OrderByDescending(c => c.OrtalamaY)
                .ToList();

            var tabakaSirasi = new[]
            {
                CizgiRolu.AsinmaTaban, CizgiRolu.BinderTaban, CizgiRolu.BitumluTemelTaban,
                CizgiRolu.PlentmiksTaban, CizgiRolu.AltTemelTaban, CizgiRolu.KirmatasTaban
            };

            for (int i = 0; i < Math.Min(gizliTabakalar.Count, tabakaSirasi.Length); i++)
            {
                if (Cizgiler.Any(c => c.Rol == tabakaSirasi[i])) continue;
                gizliTabakalar[i].Rol = tabakaSirasi[i];
                gizliTabakalar[i].OtomatikAtanmis = true;
            }

            // Coklu tabaka duzeltmesi: ayni layer'da birden fazla cizgi ayni role atandiysa
            // Y pozisyonuna gore siralayip farkli tabakalara yay
            CokluTabakaDuzelt(tabakaSirasi);

            var temp = Cizgiler.ToList();
            Cizgiler.Clear();
            foreach (var c in temp) Cizgiler.Add(c);
        }

        private void CokluTabakaDuzelt(CizgiRolu[] tabakaSirasi)
        {
            // Ayni layer'da birden fazla cizgi ayni tabaka rolune atanmis olabilir
            // (ornek: "Granuler" layerindaki 3 cizgi hepsi AltTemelTaban olarak atandi)
            // Bunlari Y pozisyonuna gore siralayip farkli rollere yay
            var cokluGruplar = Cizgiler
                .Where(c => c.OtomatikAtanmis && Array.IndexOf(tabakaSirasi, c.Rol) >= 0)
                .GroupBy(c => c.LayerAdi)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var grup in cokluGruplar)
            {
                var cizgilerSirali = grup.OrderByDescending(c => c.OrtalamaY).ToList();
                var atananRol = cizgilerSirali[0].Rol;
                int rolIdx = Array.IndexOf(tabakaSirasi, atananRol);

                // Merkez rolunden yayil: 3 cizgi + AltTemelTaban(4) -> [3,4,5]
                int baslangic = Math.Max(0, rolIdx - (cizgilerSirali.Count - 1) / 2);

                // Baska layer'a atanmis rolleri atla
                var digerAtananlar = new HashSet<CizgiRolu>(
                    Cizgiler.Where(c => !grup.Contains(c) && c.OtomatikAtanmis
                        && Array.IndexOf(tabakaSirasi, c.Rol) >= 0)
                    .Select(c => c.Rol));

                int atanan = 0;
                for (int ri = baslangic; ri < tabakaSirasi.Length && atanan < cizgilerSirali.Count; ri++)
                {
                    if (!digerAtananlar.Contains(tabakaSirasi[ri]))
                    {
                        cizgilerSirali[atanan].Rol = tabakaSirasi[ri];
                        atanan++;
                    }
                }
            }
        }

        private bool CizgiKisaMi(CizgiTanimi cizgi)
        {
            if (cizgi.Noktalar.Count < 2) return true;
            double xMin = cizgi.Noktalar.Min(p => p.X);
            double xMax = cizgi.Noktalar.Max(p => p.X);
            double yMin = cizgi.Noktalar.Min(p => p.Y);
            double yMax = cizgi.Noktalar.Max(p => p.Y);
            double xAraligi = xMax - xMin;
            double yAraligi = yMax - yMin;

            if (xAraligi < 2.0) return true;  // dikey cerceve kenari
            if (xAraligi > 5.0 && yAraligi < 1.0) return false;  // uzun yatay -> ustyapi olabilir
            return true;
        }

        private void SablonKaydet()
        {
            _olusturulanSablon = _rolAtamaService.KalibrasyonOlustur(_referansKesit, Cizgiler.ToList());
        }

        private void SablonDosyayaKaydetUygula()
        {
            if (_olusturulanSablon == null) SablonKaydet();

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Dosyasi|*.json",
                FileName = "EnkesitSablon.json"
            };

            if (dialog.ShowDialog() == true)
            {
                string json = JsonConvert.SerializeObject(_olusturulanSablon, Formatting.Indented);
                System.IO.File.WriteAllText(dialog.FileName, json);
            }
        }
    }
}
