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
        private int _aktifIndex;
        private bool _sadeceSorunluGoster;
        private CizgiTanimi _secilenCizgi;
        private CizgiRolu _yeniRol;

        public KesitDogrulamaViewModel()
        {
            OncekiCommand = new RelayCommand(Onceki, () => AktifIndex > 0);
            SonrakiCommand = new RelayCommand(Sonraki, () => _filtrelenmisKesitler != null && AktifIndex < _filtrelenmisKesitler.Count - 1);
            OnaylaCommand = new RelayCommand(Onayla, () => AktifKesit != null);
            SorunluIsaretle = new RelayCommand(SorunluOlarakIsaretle, () => AktifKesit != null);
            CizgiDuzeltCommand = new RelayCommand(CizgiDuzelt, () => SecilenCizgi != null);
            TumunuOnaylaCommand = new RelayCommand(TumunuOnayla);

            MevcutRoller = new ObservableCollection<CizgiRolu>(
                Enum.GetValues(typeof(CizgiRolu)).Cast<CizgiRolu>());
        }

        public KesitGrubu AktifKesit
        {
            get => _aktifKesit;
            set
            {
                if (SetProperty(ref _aktifKesit, value))
                    OnPropertiesChanged(nameof(AktifKesitBilgi), nameof(AktifKesitAlanlar), nameof(AktifKesitCizgiler), nameof(AktifKesitKiyaslar));
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
        public CizgiRolu YeniRol { get => _yeniRol; set => SetProperty(ref _yeniRol, value); }
        public ObservableCollection<CizgiRolu> MevcutRoller { get; }

        public string AktifKesitBilgi => AktifKesit?.Anchor != null
            ? $"Km {YolKesitService.IstasyonFormatla(AktifKesit.Anchor.Istasyon)} — {AktifKesit.Durum}"
            : "";

        public List<AlanHesapSonucu> AktifKesitAlanlar => AktifKesit?.HesaplananAlanlar;
        public List<CizgiTanimi> AktifKesitCizgiler => AktifKesit?.Cizgiler;

        /// <summary>
        /// Kiyas sonuclari varsa onlari, yoksa hesaplanan alanlari TabloKiyasSonucu formatinda dondurur.
        /// Boylece DataGrid her zaman dolu gorulur.
        /// </summary>
        public List<TabloKiyasSonucu> AktifKesitKiyaslar
        {
            get
            {
                if (AktifKesit == null) return null;

                // Kiyas sonuclari varsa dogrudan dondur
                if (AktifKesit.TabloKiyaslari != null && AktifKesit.TabloKiyaslari.Count > 0)
                    return AktifKesit.TabloKiyaslari;

                // Kiyas yoksa hesaplanan alanlari tablo formatina donustur
                if (AktifKesit.HesaplananAlanlar != null && AktifKesit.HesaplananAlanlar.Count > 0)
                {
                    return AktifKesit.HesaplananAlanlar.Select(a => new TabloKiyasSonucu
                    {
                        MalzemeAdi = a.MalzemeAdi,
                        HesaplananAlan = a.Alan,
                        TabloAlani = 0,
                        Fark = 0,
                        FarkYuzde = 0,
                        Uyumlu = false
                    }).ToList();
                }

                return null;
            }
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

        public void Yukle(List<KesitGrubu> kesitler)
        {
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
            AktifKesit.Durum = DogrulamaDurumu.Onaylandi;
            DurumGuncelle();
            Sonraki();
        }

        private void SorunluOlarakIsaretle()
        {
            if (AktifKesit == null) return;
            AktifKesit.Durum = DogrulamaDurumu.Sorunlu;
            DurumGuncelle();
        }

        private void CizgiDuzelt()
        {
            if (SecilenCizgi == null) return;
            SecilenCizgi.Rol = YeniRol;
            SecilenCizgi.OtomatikAtanmis = false;
            AktifKesit.Durum = DogrulamaDurumu.Duzeltildi;
            OnPropertyChanged(nameof(AktifKesitCizgiler));
            DurumGuncelle();
        }

        private void TumunuOnayla()
        {
            foreach (var kesit in _tumKesitler)
            {
                if (kesit.Durum == DogrulamaDurumu.Bekliyor)
                    kesit.Durum = DogrulamaDurumu.Onaylandi;
            }
            DurumGuncelle();
        }

        private void DurumGuncelle()
        {
            OnPropertiesChanged(nameof(OnayliSayisi), nameof(UyariSayisi), nameof(SorunluSayisi),
                nameof(AktifKesitBilgi), nameof(ToplamGosterilen));
        }
    }
}
