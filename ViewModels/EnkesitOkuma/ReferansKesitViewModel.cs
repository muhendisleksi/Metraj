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
    public class LayerRenkGrubu : INotifyPropertyChanged
    {
        private CizgiRolu _atananRol;
        private bool _onaylandi;

        public string LayerAdi { get; set; }
        public short RenkIndex { get; set; }
        public int CizgiSayisi { get; set; }
        public string XAraligi { get; set; }
        public bool KapaliVar { get; set; }
        public string OrnekBilgi { get; set; }

        public CizgiRolu AtananRol
        {
            get => _atananRol;
            set
            {
                if (_atananRol == value) return;
                _atananRol = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AtananRol)));
                // Kullanici ComboBox'tan secim yaptiginda otomatik onayla
                Onaylandi = true;
                RolDegisti?.Invoke(this);
            }
        }

        public bool Onaylandi
        {
            get => _onaylandi;
            set
            {
                if (_onaylandi == value) return;
                _onaylandi = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Onaylandi)));
                OnayDegisti?.Invoke();
            }
        }

        /// <summary>ViewModel'in onay sayacini guncellemesi icin callback.</summary>
        public Action OnayDegisti { get; set; }

        /// <summary>Rol degistiginde ViewModel'in cizgileri aninda guncellemesi icin callback.</summary>
        public Action<LayerRenkGrubu> RolDegisti { get; set; }

        public System.Windows.Media.Color WpfRenk => AcadRenkCevir(RenkIndex);

        public static System.Windows.Media.Color AcadRenkCevir(short idx)
        {
            switch (idx)
            {
                case 1: return System.Windows.Media.Color.FromRgb(0xFF, 0x00, 0x00); // Kirmizi
                case 2: return System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0x00); // Sari
                case 3: return System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x00); // Yesil
                case 4: return System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0xFF); // Cyan
                case 5: return System.Windows.Media.Color.FromRgb(0x00, 0x00, 0xFF); // Mavi
                case 6: return System.Windows.Media.Color.FromRgb(0xFF, 0x00, 0xFF); // Magenta
                case 7: return System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF); // Beyaz
                case 8: return System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80); // Gri
                case 9: return System.Windows.Media.Color.FromRgb(0xC0, 0xC0, 0xC0); // Acik gri
                default: return System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

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
        private LayerRenkGrubu _secilenGrup;

        // Kesit navigasyonu
        private List<AnchorNokta> _tumAnchorlar;
        private KesitPenceresi _pencere;
        private List<ObjectId> _tumEntityler;
        private IKesitGruplamaService _gruplamaService;
        private int _aktifKesitIndex;
        private KesitSecimOgesi _secilenKesitOgesi;

        private static readonly HashSet<string> VarsayilanKapaliLayerler = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase) { "Defpoints" };

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
            TopluRolAtaCommand = new RelayCommand(TopluRolAtaUygula, () => TopluTumOnaylandi);
            TumunuOnaylaCommand = new RelayCommand(TumunuOnayla);

            MevcutRoller = new ObservableCollection<CizgiRolu>(
                Enum.GetValues(typeof(CizgiRolu)).Cast<CizgiRolu>());
        }

        public ObservableCollection<CizgiTanimi> Cizgiler { get; } = new ObservableCollection<CizgiTanimi>();
        public ObservableCollection<CizgiTanimi> GoruntulenenCizgiler { get; } = new ObservableCollection<CizgiTanimi>();
        public ObservableCollection<LayerFiltreOgesi> LayerFiltresi { get; } = new ObservableCollection<LayerFiltreOgesi>();
        public ObservableCollection<LayerRenkGrubu> LayerRenkGruplari { get; } = new ObservableCollection<LayerRenkGrubu>();
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
        public ICommand TopluRolAtaCommand { get; }
        public ICommand TumunuOnaylaCommand { get; }

        public bool TopluTumOnaylandi => LayerRenkGruplari.Count > 0 && LayerRenkGruplari.All(g => g.Onaylandi);

        public string TopluOnayDurumu
        {
            get
            {
                int toplam = LayerRenkGruplari.Count;
                if (toplam == 0) return "";
                int onaylanan = LayerRenkGruplari.Count(g => g.Onaylandi);
                return $"{onaylanan}/{toplam} onaylandi";
            }
        }

        public string KaydetButonMetni
        {
            get
            {
                int toplam = LayerRenkGruplari.Count;
                if (toplam == 0) return "Kaydet";
                int onaylanan = LayerRenkGruplari.Count(g => g.Onaylandi);
                return $"Kaydet ({onaylanan}/{toplam})";
            }
        }

        public LayerRenkGrubu SecilenGrup
        {
            get => _secilenGrup;
            set
            {
                if (SetProperty(ref _secilenGrup, value))
                    FiltreDegisti?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Yukle(KesitGrubu kesit)
        {
            // Mevcut atamalari kaydet (kesit navigasyonunda korunmasi icin)
            var oncekiAtamalar = new Dictionary<string, (CizgiRolu rol, bool onay)>();
            foreach (var g in LayerRenkGruplari)
                oncekiAtamalar[$"{g.LayerAdi}|{g.RenkIndex}"] = (g.AtananRol, g.Onaylandi);

            _referansKesit = kesit;
            Cizgiler.Clear();
            foreach (var c in kesit.Cizgiler.OrderByDescending(c => c.OrtalamaY))
                Cizgiler.Add(c);

            // OtomatikAta cizgilere roller atar, tablo bu sonuclardan doldurulur
            OtomatikAta();
            LayerFiltresiOlustur();
            LayerRenkTablosuOlustur();

            // Onceki atamalari yeni tabloya uygula
            if (oncekiAtamalar.Count > 0)
                OncekiAtamalariUygula(oncekiAtamalar);

            FiltreUygula();
        }

        private void OncekiAtamalariUygula(Dictionary<string, (CizgiRolu rol, bool onay)> oncekiAtamalar)
        {
            foreach (var g in LayerRenkGruplari)
            {
                string anahtar = $"{g.LayerAdi}|{g.RenkIndex}";
                if (oncekiAtamalar.TryGetValue(anahtar, out var onceki))
                {
                    // AtananRol setter'i GrupRolDegisti + Onaylandi=true tetikler
                    // Bunu gecici olarak devre disi birak, toplu uygulayalim
                    g.RolDegisti = null;
                    g.OnayDegisti = null;

                    g.AtananRol = onceki.rol;
                    g.Onaylandi = onceki.onay;

                    // Callback'leri geri bagla
                    g.RolDegisti = GrupRolDegisti;
                    g.OnayDegisti = OnayDurumuGuncelle;

                    // Cizgilere uygula
                    foreach (var c in Cizgiler)
                    {
                        if ((c.LayerAdi ?? "(bos)") == g.LayerAdi && c.RenkIndex == g.RenkIndex)
                            c.Rol = g.AtananRol;
                    }
                }
            }

            OnayDurumuGuncelle();
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
            GoruntulenenCizgiler.Clear();
            foreach (var c in Cizgiler)
            {
                // Sadece Cerceve/Grid gizlenebilir, diger tum layer'lar her zaman gorunur
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

                string upper = TurkceNormalize((cizgi.LayerAdi ?? "").ToUpperInvariant());

                if (upper.Contains("ZEMIN") || upper.Contains("SIYAH") || upper.Contains("ARAZI"))
                { cizgi.Rol = CizgiRolu.Zemin; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("SIYIRMA") || upper.Contains("SIYRIM"))
                { cizgi.Rol = CizgiRolu.Siyirma; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("DESIGN") || upper.Contains("TASARIM"))
                { cizgi.Rol = CizgiRolu.Diger; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("PROJE") || upper.Contains("KIRMIZI"))
                { cizgi.Rol = CizgiRolu.ProjeKotu; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("EKSEN") || upper.Contains("CL") || upper.Contains("AXIS"))
                { cizgi.Rol = CizgiRolu.Diger; cizgi.OtomatikAtanmis = true; continue; }

                // Cerceve / Palye / Pafta / EnKesit pafta → hepsi cerceve
                if (upper.Contains("CERCEVE") || upper.Contains("FRAME") || upper.Contains("PALYE")
                    || upper.Contains("PAFTA") || upper.Contains("ENKESIT") || upper.Contains("EN KESIT"))
                { cizgi.Rol = CizgiRolu.CerceveCizgisi; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("GRID") || upper.Contains("OLCEK"))
                { cizgi.Rol = CizgiRolu.GridCizgisi; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("HENDEK"))
                { cizgi.Rol = CizgiRolu.Diger; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("SEV"))
                { cizgi.Rol = CizgiRolu.Diger; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("BANKET") || upper.Contains("BORDUR"))
                { cizgi.Rol = CizgiRolu.Diger; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("ASINMA"))
                { cizgi.Rol = CizgiRolu.Asinma; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("BINDER"))
                { cizgi.Rol = CizgiRolu.Binder; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("BITUMEN") || upper.Contains("BITUMLU") || upper.Contains("BITUM"))
                { cizgi.Rol = CizgiRolu.BitumluTemel; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("GRANULER") || upper.Contains("ALTTEMEL") || upper.Contains("ALT TEMEL"))
                { cizgi.Rol = CizgiRolu.AltTemel; cizgi.OtomatikAtanmis = true; continue; }

                if (upper.Contains("PLENT"))
                { cizgi.Rol = CizgiRolu.Plentmiks; cizgi.OtomatikAtanmis = true; continue; }

                // Renk bazli
                switch (cizgi.RenkIndex)
                {
                    case 3: cizgi.Rol = CizgiRolu.Zemin; cizgi.OtomatikAtanmis = true; break;
                    case 5: cizgi.Rol = CizgiRolu.Siyirma; cizgi.OtomatikAtanmis = true; break;
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
                CizgiRolu.Asinma, CizgiRolu.Binder, CizgiRolu.BitumluTemel,
                CizgiRolu.Plentmiks, CizgiRolu.AltTemel
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
            // (ornek: "Granuler" layerindaki 3 cizgi hepsi AltTemel olarak atandi)
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

                // Merkez rolunden yayil: 3 cizgi + AltTemel(4) -> [3,4,5]
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

        /// <summary>
        /// Turkce ozel karakterleri ASCII karsiliklarina donusturur.
        /// DWG layer adlarinda İ/Ş/Ü/Ö/Ç/Ğ kullanilabilir — ToUpperInvariant bunlari normalize etmez.
        /// </summary>
        private static string TurkceNormalize(string s)
        {
            return s.Replace('\u0130', 'I')  // İ -> I
                    .Replace('\u0131', 'I')  // ı -> I
                    .Replace('\u015E', 'S')  // Ş -> S
                    .Replace('\u015F', 'S')  // ş -> S
                    .Replace('\u00DC', 'U')  // Ü -> U
                    .Replace('\u00FC', 'U')  // ü -> U
                    .Replace('\u00D6', 'O')  // Ö -> O
                    .Replace('\u00F6', 'O')  // ö -> O
                    .Replace('\u00C7', 'C')  // Ç -> C
                    .Replace('\u00E7', 'C')  // ç -> C
                    .Replace('\u011E', 'G')  // Ğ -> G
                    .Replace('\u011F', 'G'); // ğ -> G
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

        private void OnayDurumuGuncelle()
        {
            OnPropertiesChanged(nameof(TopluTumOnaylandi), nameof(TopluOnayDurumu), nameof(KaydetButonMetni));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void GrupRolDegisti(LayerRenkGrubu grup)
        {
            // Bu layer+renk grubundaki tum cizgilere rolu aninda ata
            foreach (var c in Cizgiler)
            {
                if ((c.LayerAdi ?? "(bos)") == grup.LayerAdi && c.RenkIndex == grup.RenkIndex)
                    c.Rol = grup.AtananRol;
            }
            FiltreUygula();
        }

        private void TumunuOnayla()
        {
            foreach (var g in LayerRenkGruplari)
                g.Onaylandi = true;
        }

        private void LayerRenkTablosuOlustur()
        {
            LayerRenkGruplari.Clear();

            var gruplar = Cizgiler
                .Where(c => c.Rol != CizgiRolu.CerceveCizgisi && c.Rol != CizgiRolu.GridCizgisi)
                .GroupBy(c => new { Layer = c.LayerAdi ?? "(bos)", c.RenkIndex })
                .OrderBy(g => g.Key.Layer)
                .ThenBy(g => g.Key.RenkIndex);

            foreach (var grup in gruplar)
            {
                double minX = grup.SelectMany(c => c.Noktalar).Min(p => p.X);
                double maxX = grup.SelectMany(c => c.Noktalar).Max(p => p.X);
                int kapaliSayisi = grup.Count(c => c.KapaliMi);
                int acikSayisi = grup.Count(c => !c.KapaliMi);

                // Mevcut OtomatikAta sonuclarindan onerilen rolu al
                var onerilen = grup.First().Rol;

                // Genel layer'lara otomatik rol onerme — kullanici secsin
                bool genelLayer = VarsayilanKapaliLayerler.Contains(grup.Key.Layer);
                if (genelLayer)
                    onerilen = CizgiRolu.Tanimsiz;

                var oge = new LayerRenkGrubu
                {
                    LayerAdi = grup.Key.Layer,
                    RenkIndex = grup.Key.RenkIndex,
                    CizgiSayisi = grup.Count(),
                    XAraligi = $"{minX:F2}..{maxX:F2}",
                    KapaliVar = kapaliSayisi > 0,
                    OrnekBilgi = kapaliSayisi > 0 && acikSayisi > 0
                        ? $"{kapaliSayisi} kapali, {acikSayisi} acik"
                        : kapaliSayisi > 0 ? $"{kapaliSayisi} kapali" : $"{acikSayisi} acik",
                    AtananRol = onerilen,
                    OnayDegisti = OnayDurumuGuncelle,
                    RolDegisti = GrupRolDegisti
                };
                // Genel layer'lar hicbir zaman otomatik onayli gelmesin
                // AtananRol setter'i Onaylandi=true yapar, bunu geri al
                oge.Onaylandi = false;

                LayerRenkGruplari.Add(oge);
            }

            OnayDurumuGuncelle();
        }

        private void TopluRolAtaUygula()
        {
            var tabakaSirasi = new[]
            {
                CizgiRolu.Asinma, CizgiRolu.Binder, CizgiRolu.BitumluTemel,
                CizgiRolu.Plentmiks, CizgiRolu.AltTemel
            };

            foreach (var grup in LayerRenkGruplari)
            {
                if (grup.AtananRol == CizgiRolu.Tanimsiz) continue;

                var eslesen = Cizgiler
                    .Where(c => (c.LayerAdi ?? "(bos)") == grup.LayerAdi && c.RenkIndex == grup.RenkIndex)
                    .ToList();

                foreach (var c in eslesen)
                {
                    c.Rol = grup.AtananRol;
                    c.OtomatikAtanmis = false;
                }
            }

            // Coklu tabaka duzeltmesi
            var tabakaSirasiArr = new[]
            {
                CizgiRolu.Asinma, CizgiRolu.Binder, CizgiRolu.BitumluTemel,
                CizgiRolu.Plentmiks, CizgiRolu.AltTemel
            };
            CokluTabakaDuzelt(tabakaSirasiArr);

            // Listeyi yenile
            var temp = Cizgiler.ToList();
            Cizgiler.Clear();
            foreach (var c in temp) Cizgiler.Add(c);
            FiltreUygula();
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
