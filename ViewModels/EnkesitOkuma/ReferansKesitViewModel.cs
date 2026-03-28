using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Metraj.Models.YolEnkesit;
using Metraj.Services.YolEnkesit;
using Newtonsoft.Json;

namespace Metraj.ViewModels.EnkesitOkuma
{
    public class ReferansKesitViewModel : ViewModelBase
    {
        private readonly ICizgiRolAtamaService _rolAtamaService;
        private KesitGrubu _referansKesit;
        private CizgiTanimi _secilenCizgi;
        private CizgiRolu _secilenRol;
        private ReferansKesitSablonu _olusturulanSablon;

        public ReferansKesitViewModel(ICizgiRolAtamaService rolAtamaService)
        {
            _rolAtamaService = rolAtamaService;

            RolAta = new RelayCommand(RolAtaUygula, () => SecilenCizgi != null);
            TumunuOtomatikAta = new RelayCommand(OtomatikAta);
            Kaydet = new RelayCommand(SablonKaydet);
            SablonDosyayaKaydet = new RelayCommand(SablonDosyayaKaydetUygula);

            MevcutRoller = new ObservableCollection<CizgiRolu>(
                Enum.GetValues(typeof(CizgiRolu)).Cast<CizgiRolu>());
        }

        public ObservableCollection<CizgiTanimi> Cizgiler { get; } = new ObservableCollection<CizgiTanimi>();
        public ObservableCollection<CizgiRolu> MevcutRoller { get; }

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

        public string SecilenCizgiBilgi => SecilenCizgi != null
            ? $"Layer: {SecilenCizgi.LayerAdi} | Renk: {SecilenCizgi.RenkIndex} | Y: {SecilenCizgi.OrtalamaY:F2}"
            : "";

        public ReferansKesitSablonu OlusturulanSablon => _olusturulanSablon;

        public ICommand RolAta { get; }
        public ICommand TumunuOtomatikAta { get; }
        public ICommand Kaydet { get; }
        public ICommand SablonDosyayaKaydet { get; }

        public void Yukle(KesitGrubu kesit)
        {
            _referansKesit = kesit;
            Cizgiler.Clear();
            foreach (var c in kesit.Cizgiler.OrderByDescending(c => c.OrtalamaY))
                Cizgiler.Add(c);
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
        }

        private void OtomatikAta()
        {
            foreach (var cizgi in Cizgiler)
            {
                if (cizgi.Rol != CizgiRolu.Tanimsiz) continue;

                // 1. Layer adindan dene
                string upper = (cizgi.LayerAdi ?? "").ToUpperInvariant();
                if (upper.Contains("ZEMIN") || upper.Contains("SIYAH"))
                { cizgi.Rol = CizgiRolu.Zemin; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("SIYIRMA"))
                { cizgi.Rol = CizgiRolu.SiyirmaTaban; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("PROJE") || upper.Contains("KIRMIZI"))
                { cizgi.Rol = CizgiRolu.ProjeKotu; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("EKSEN") || upper.Contains("CL") || upper.Contains("AXIS"))
                { cizgi.Rol = CizgiRolu.EksenCizgisi; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("CERCEVE") || upper.Contains("FRAME"))
                { cizgi.Rol = CizgiRolu.CerceveCizgisi; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("GRID") || upper.Contains("OLCEK"))
                { cizgi.Rol = CizgiRolu.GridCizgisi; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("HENDEK"))
                { cizgi.Rol = CizgiRolu.HendekCizgisi; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("SEV"))
                { cizgi.Rol = CizgiRolu.SevCizgisi; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("BANKET"))
                { cizgi.Rol = CizgiRolu.BanketCizgisi; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("ASINMA"))
                { cizgi.Rol = CizgiRolu.AsinmaTaban; cizgi.OtomatikAtanmis = true; continue; }
                if (upper.Contains("BINDER"))
                { cizgi.Rol = CizgiRolu.BinderTaban; cizgi.OtomatikAtanmis = true; continue; }

                // 2. Layer eslesmedi -> renk bazli dene
                switch (cizgi.RenkIndex)
                {
                    case 3: // Yesil -> Zemin
                        cizgi.Rol = CizgiRolu.Zemin;
                        cizgi.OtomatikAtanmis = true;
                        break;
                    case 5: // Mavi -> Siyirma
                        cizgi.Rol = CizgiRolu.SiyirmaTaban;
                        cizgi.OtomatikAtanmis = true;
                        break;
                    case 1: // Kirmizi -> Proje kotu
                        cizgi.Rol = CizgiRolu.ProjeKotu;
                        cizgi.OtomatikAtanmis = true;
                        break;
                }
            }

            // 3. Renk 7/8/9 (beyaz/gri) tanimsiz cizgileri Y pozisyonuna gore ustyapi tabakalari olarak ata
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
                gizliTabakalar[i].Rol = tabakaSirasi[i];
                gizliTabakalar[i].OtomatikAtanmis = true;
            }

            // UI guncelle
            var temp = Cizgiler.ToList();
            Cizgiler.Clear();
            foreach (var c in temp) Cizgiler.Add(c);
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
