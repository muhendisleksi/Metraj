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

                string upper = (cizgi.LayerAdi ?? "").ToUpperInvariant();
                if (upper.Contains("ZEMİN") || upper.Contains("ZEMIN") || upper.Contains("SIYAH"))
                    cizgi.Rol = CizgiRolu.Zemin;
                else if (upper.Contains("SIYIRMA") || upper.Contains("SİYIRMA"))
                    cizgi.Rol = CizgiRolu.SiyirmaTaban;
                else if (upper.Contains("PROJE") || upper.Contains("KIRMIZI"))
                    cizgi.Rol = CizgiRolu.ProjeKotu;
                else if (upper.Contains("EKSEN") || upper.Contains("CL") || upper.Contains("AXIS"))
                    cizgi.Rol = CizgiRolu.EksenCizgisi;
                else if (upper.Contains("CERCEVE") || upper.Contains("ÇERÇEVE") || upper.Contains("FRAME"))
                    cizgi.Rol = CizgiRolu.CerceveCizgisi;
                else if (upper.Contains("GRID") || upper.Contains("OLCEK"))
                    cizgi.Rol = CizgiRolu.GridCizgisi;
                else if (upper.Contains("HENDEK"))
                    cizgi.Rol = CizgiRolu.HendekCizgisi;
                else if (upper.Contains("SEV") || upper.Contains("ŞEV"))
                    cizgi.Rol = CizgiRolu.SevCizgisi;
                else if (upper.Contains("BANKET"))
                    cizgi.Rol = CizgiRolu.BanketCizgisi;
                else if (upper.Contains("ASINMA") || upper.Contains("AŞINMA"))
                    cizgi.Rol = CizgiRolu.AsinmaTaban;
                else if (upper.Contains("BINDER"))
                    cizgi.Rol = CizgiRolu.BinderTaban;

                if (cizgi.Rol != CizgiRolu.Tanimsiz)
                    cizgi.OtomatikAtanmis = true;
            }

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
                Filter = "JSON Dosyası|*.json",
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
