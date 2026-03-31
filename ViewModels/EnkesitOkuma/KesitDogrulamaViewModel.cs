using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Metraj.Models.YolEnkesit;
using Metraj.Services;

namespace Metraj.ViewModels.EnkesitOkuma
{
    public class KesitDogrulamaViewModel : ViewModelBase
    {
        private List<KesitGrubu> _tumKesitler;
        private List<KesitGrubu> _filtrelenmisKesitler;
        private KesitGrubu _aktifKesit;
        private int _aktifIndex = -1;
        private bool _sadeceSorunluGoster;
        private CizgiTanimi _secilenCizgi;
        private List<CizgiTanimi> _secilenCizgiler = new List<CizgiTanimi>();
        private CizgiRolu _yeniRol;
        private TabloKiyasSonucu _secilenKiyas;
        private List<TabloKiyasSonucu> _aktifKesitKiyaslar;

        public KesitDogrulamaViewModel()
        {
            OncekiCommand = new RelayCommand(Onceki, () => AktifIndex > 0);
            SonrakiCommand = new RelayCommand(Sonraki, () => _filtrelenmisKesitler != null && AktifIndex < _filtrelenmisKesitler.Count - 1);
            OnaylaCommand = new RelayCommand(Onayla, () => AktifKesit != null);
            SorunluIsaretle = new RelayCommand(SorunluOlarakIsaretle, () => AktifKesit != null);
            CizgiDuzeltCommand = new RelayCommand(CizgiDuzelt, () => SecilenCizgi != null || (_secilenCizgiler != null && _secilenCizgiler.Count > 0));
            TumunuOnaylaCommand = new RelayCommand(TumunuOnayla);
            TabloKabulCommand = new RelayCommand<TabloKiyasSonucu>(TabloKabulEt);
            HesapKabulCommand = new RelayCommand<TabloKiyasSonucu>(HesapKabulEt);
            TopluTabloKabulCommand = new RelayCommand(TopluTabloKabul, () => AktifKesit != null);
            TopluHesapKabulCommand = new RelayCommand(TopluHesapKabul, () => AktifKesit != null);

            MevcutRoller = new ObservableCollection<CizgiRolu>(
                Enum.GetValues(typeof(CizgiRolu)).Cast<CizgiRolu>());
        }

        public KesitGrubu AktifKesit
        {
            get => _aktifKesit;
            set
            {
                if (SetProperty(ref _aktifKesit, value))
                {
                    KiyaslarGuncelle();
                    SecilenKiyas = null;
                    OnPropertiesChanged(nameof(AktifKesitBilgi), nameof(AktifKesitAlanlar), nameof(AktifKesitCizgiler));
                }
            }
        }

        public int AktifIndex
        {
            get => _aktifIndex;
            set
            {
                if (SetProperty(ref _aktifIndex, value) && _filtrelenmisKesitler != null && value >= 0 && value < _filtrelenmisKesitler.Count)
                    AktifKesit = _filtrelenmisKesitler[value];
            }
        }

        public bool SadeceSorunluGoster
        {
            get => _sadeceSorunluGoster;
            set
            {
                if (SetProperty(ref _sadeceSorunluGoster, value))
                {
                    FiltreUygula();
                    OnPropertiesChanged(nameof(ToplamGosterilen));
                }
            }
        }

        public CizgiTanimi SecilenCizgi { get => _secilenCizgi; set => SetProperty(ref _secilenCizgi, value); }
        public List<CizgiTanimi> SecilenCizgiler { get => _secilenCizgiler; set => SetProperty(ref _secilenCizgiler, value); }
        public CizgiRolu YeniRol { get => _yeniRol; set => SetProperty(ref _yeniRol, value); }
        public TabloKiyasSonucu SecilenKiyas { get => _secilenKiyas; set => SetProperty(ref _secilenKiyas, value); }
        public ObservableCollection<CizgiRolu> MevcutRoller { get; }

        public string AktifKesitBilgi => AktifKesit?.Anchor != null
            ? $"Km {YolKesitService.IstasyonFormatla(AktifKesit.Anchor.Istasyon)} — {AktifKesit.Durum}"
            : "";

        public List<AlanHesapSonucu> AktifKesitAlanlar => AktifKesit?.HesaplananAlanlar;
        public List<CizgiTanimi> AktifKesitCizgiler => AktifKesit?.Cizgiler;

        /// <summary>
        /// Cache'li kiyas listesi. AktifKesit degistiginde bir kez hesaplanir.
        /// DataGrid ItemsSource buna baglanir — her getter'da yeni liste olusturmaz.
        /// </summary>
        public List<TabloKiyasSonucu> AktifKesitKiyaslar
        {
            get => _aktifKesitKiyaslar;
            private set => SetProperty(ref _aktifKesitKiyaslar, value);
        }

        /// <summary>AktifKesit degistiginde kiyas listesini bir kez hesapla ve cache'le.</summary>
        private void KiyaslarGuncelle()
        {
            if (AktifKesit == null)
            {
                AktifKesitKiyaslar = null;
                return;
            }

            // Kiyas sonuclari varsa dogrudan kullan
            if (AktifKesit.TabloKiyaslari != null && AktifKesit.TabloKiyaslari.Count > 0)
            {
                AktifKesitKiyaslar = AktifKesit.TabloKiyaslari;
                return;
            }

            // Kiyas yoksa hesaplanan alanlari tablo formatina donustur
            if (AktifKesit.HesaplananAlanlar != null && AktifKesit.HesaplananAlanlar.Count > 0)
            {
                AktifKesitKiyaslar = AktifKesit.HesaplananAlanlar.Select(a =>
                {
                    var s = new TabloKiyasSonucu
                    {
                        MalzemeAdi = a.MalzemeAdi,
                        HesaplananAlan = a.Alan,
                        TabloAlani = 0,
                        Fark = 0,
                        FarkYuzde = 0,
                        Uyumlu = false,
                        UstCizgiRolu = a.UstCizgiRolu,
                        AltCizgiRolu = a.AltCizgiRolu
                    };
                    s.FokusBilgisiAyarla(AktifKesit.Cizgiler);
                    return s;
                }).ToList();
                return;
            }

            AktifKesitKiyaslar = null;
        }

        public string ToplamGosterilen => _filtrelenmisKesitler != null
            ? $"{AktifIndex + 1} / {_filtrelenmisKesitler.Count}"
            : "0 / 0";

        public int OnayliSayisi => _tumKesitler?.Count(k => k.Durum == DogrulamaDurumu.Onaylandi) ?? 0;
        public int UyariSayisi => _tumKesitler?.Count(k => k.Durum == DogrulamaDurumu.Bekliyor) ?? 0;
        public int SorunluSayisi => _tumKesitler?.Count(k => k.Durum == DogrulamaDurumu.Sorunlu) ?? 0;

        public ICommand OncekiCommand { get; }
        public ICommand SonrakiCommand { get; }
        public ICommand OnaylaCommand { get; }
        public ICommand SorunluIsaretle { get; }
        public ICommand CizgiDuzeltCommand { get; }
        public ICommand TumunuOnaylaCommand { get; }
        public ICommand TabloKabulCommand { get; }
        public ICommand HesapKabulCommand { get; }
        public ICommand TopluTabloKabulCommand { get; }
        public ICommand TopluHesapKabulCommand { get; }

        public void Yukle(List<KesitGrubu> kesitler)
        {
            // Ayni veri zaten yuklu ise state'i koru (AktifIndex, SadeceSorunluGoster, Karar)
            if (_tumKesitler == kesitler && _filtrelenmisKesitler != null && _filtrelenmisKesitler.Count > 0)
            {
                // Kiyas listesini guncelle (Hesapla sonrasi yeni degerler olabilir)
                KiyaslarGuncelle();
                DurumGuncelle();
                return;
            }

            _tumKesitler = kesitler;
            FiltreUygula();
            if (_filtrelenmisKesitler.Count > 0)
                AktifIndex = 0;
            DurumGuncelle();
        }

        private void FiltreUygula()
        {
            if (SadeceSorunluGoster)
                _filtrelenmisKesitler = _tumKesitler.Where(k => k.Durum == DogrulamaDurumu.Sorunlu || k.Durum == DogrulamaDurumu.Bekliyor).ToList();
            else
                _filtrelenmisKesitler = _tumKesitler.ToList();

            AktifIndex = _filtrelenmisKesitler.Count > 0 ? 0 : -1;
        }

        private void Onceki() { if (AktifIndex > 0) AktifIndex--; OnPropertyChanged(nameof(ToplamGosterilen)); }
        private void Sonraki() { if (AktifIndex < _filtrelenmisKesitler.Count - 1) AktifIndex++; OnPropertyChanged(nameof(ToplamGosterilen)); }

        private void Onayla()
        {
            if (AktifKesit == null) return;
            KararsizKiyaslariCoz(_aktifKesitKiyaslar);
            AktifKesit.Durum = DogrulamaDurumu.Onaylandi;
            DurumGuncelle();

            // Son kesitteyse sadece UI guncelle, degilse sonrakine gec
            if (_filtrelenmisKesitler != null && AktifIndex < _filtrelenmisKesitler.Count - 1)
                Sonraki();
            else
                OnPropertiesChanged(nameof(AktifKesitBilgi), nameof(ToplamGosterilen));
        }

        private void SorunluOlarakIsaretle()
        {
            if (AktifKesit == null) return;
            AktifKesit.Durum = DogrulamaDurumu.Sorunlu;
            DurumGuncelle();
        }

        private void CizgiDuzelt()
        {
            // Coklu secim varsa hepsine ayni rolu ata
            var hedefler = _secilenCizgiler != null && _secilenCizgiler.Count > 0
                ? _secilenCizgiler
                : (SecilenCizgi != null ? new List<CizgiTanimi> { SecilenCizgi } : null);

            if (hedefler == null || hedefler.Count == 0) return;

            foreach (var cizgi in hedefler)
            {
                cizgi.Rol = YeniRol;
                cizgi.OtomatikAtanmis = false;
            }

            AktifKesit.Durum = DogrulamaDurumu.Duzeltildi;
            OnPropertyChanged(nameof(AktifKesitCizgiler));
            DurumGuncelle();
        }

        private void TumunuOnayla()
        {
            foreach (var kesit in _tumKesitler)
            {
                if (kesit.Durum == DogrulamaDurumu.Bekliyor || kesit.Durum == DogrulamaDurumu.Sorunlu)
                {
                    KararsizKiyaslariCoz(kesit.TabloKiyaslari);
                    kesit.Durum = DogrulamaDurumu.Onaylandi;
                }
            }

            // Aktif kesitin kiyaslarini da guncelle (ekrandakiler)
            KararsizKiyaslariCoz(_aktifKesitKiyaslar);
            if (AktifKesit != null && AktifKesit.Durum != DogrulamaDurumu.Onaylandi)
                AktifKesit.Durum = DogrulamaDurumu.Onaylandi;

            // Filtre ve UI guncelle
            DurumGuncelle();
            KiyaslarGuncelle();
            OnPropertiesChanged(nameof(AktifKesitKiyaslar), nameof(AktifKesitBilgi));
        }

        private void TabloKabulEt(TabloKiyasSonucu kiyas)
        {
            if (kiyas == null) return;
            kiyas.Karar = KararDurumu.TabloKabul;
            KararSonrasiKontrol();
        }

        private void HesapKabulEt(TabloKiyasSonucu kiyas)
        {
            if (kiyas == null) return;
            kiyas.Karar = KararDurumu.HesapKabul;
            KararSonrasiKontrol();
        }

        private void TopluTabloKabul()
        {
            if (_aktifKesitKiyaslar == null) return;
            foreach (var k in _aktifKesitKiyaslar)
                if (k.Karar == KararDurumu.Bekliyor) k.Karar = KararDurumu.TabloKabul;
            KararSonrasiKontrol();
        }

        private void TopluHesapKabul()
        {
            if (_aktifKesitKiyaslar == null) return;
            foreach (var k in _aktifKesitKiyaslar)
                if (k.Karar == KararDurumu.Bekliyor) k.Karar = KararDurumu.HesapKabul;
            KararSonrasiKontrol();
        }

        /// <summary>Tum malzemelerin karari verilmisse kesiti otomatik onayla.</summary>
        private void KararSonrasiKontrol()
        {
            if (_aktifKesitKiyaslar == null || AktifKesit == null) return;

            bool hepsiKararli = _aktifKesitKiyaslar.All(k => k.Karar != KararDurumu.Bekliyor);
            if (hepsiKararli && AktifKesit.Durum != DogrulamaDurumu.Onaylandi && AktifKesit.Durum != DogrulamaDurumu.Duzeltildi)
            {
                AktifKesit.Durum = DogrulamaDurumu.Onaylandi;
                DurumGuncelle();

                // Son kesitteyse sadece UI guncelle, degilse sonrakine gec
                if (_filtrelenmisKesitler != null && AktifIndex < _filtrelenmisKesitler.Count - 1)
                    Sonraki();
                else
                    OnPropertiesChanged(nameof(AktifKesitBilgi), nameof(ToplamGosterilen));
            }
        }

        /// <summary>Bekleyen kiyaslari otomatik karara baglar. TabloAlani > 0 ise TabloKabul, degilse HesapKabul.</summary>
        private static void KararsizKiyaslariCoz(List<TabloKiyasSonucu> kiyaslar)
        {
            if (kiyaslar == null) return;
            foreach (var k in kiyaslar)
            {
                if (k.Karar == KararDurumu.Bekliyor)
                    k.Karar = k.TabloAlani > 0 ? KararDurumu.TabloKabul : KararDurumu.HesapKabul;
            }
        }

        private void DurumGuncelle()
        {
            OnPropertiesChanged(nameof(OnayliSayisi), nameof(UyariSayisi), nameof(SorunluSayisi),
                nameof(AktifKesitBilgi), nameof(ToplamGosterilen));
        }
    }
}
