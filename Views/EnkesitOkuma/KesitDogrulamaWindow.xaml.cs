using System.Linq;
using System.Windows;
using Metraj.Models.YolEnkesit;
using Metraj.ViewModels.EnkesitOkuma;

namespace Metraj.Views.EnkesitOkuma
{
    public partial class KesitDogrulamaWindow : Window
    {
        public KesitDogrulamaWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            ContentRendered += OnContentRendered;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is KesitDogrulamaViewModel vm)
            {
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(vm.AktifKesit) && vm.AktifKesit != null)
                        OnizlemeControl.CizgileriYukle(vm.AktifKesit);

                    // Kiyas tablosu satir secimi → kalibrasyon ile ayni HighlightLayer
                    if (args.PropertyName == nameof(vm.SecilenKiyas))
                    {
                        if (vm.SecilenKiyas != null)
                        {
                            var k = vm.SecilenKiyas;

                            OnizlemeControl.HighlightTemizle();

                            // MalzemeAdi'ndan direkt rol belirle (UstCizgiRolu/AltCizgiRolu'na guvenme)
                            var cizgiler = vm.AktifKesit?.Cizgiler;
                            if (cizgiler != null)
                            {
                                var fokusRol = MalzemedenRol(k.MalzemeAdi);
                                if (fokusRol != CizgiRolu.Tanimsiz)
                                {
                                    var fokusCizgiler = cizgiler
                                        .Where(c => c.Rol == fokusRol).ToList();
                                    if (fokusCizgiler.Count > 0)
                                        OnizlemeControl.HighlightCizgiler(fokusCizgiler);
                                }
                            }

                            string bilgi = k.TabloAlani > 0
                                ? $"{k.MalzemeAdi} fokus \u2014 Hesap: {k.HesaplananAlan:F2} m\u00B2 | Tablo: {k.TabloAlani:F2} m\u00B2 | Fark: %{k.FarkYuzde:F1}"
                                : $"{k.MalzemeAdi} fokus \u2014 Hesap: {k.HesaplananAlan:F2} m\u00B2";
                            OnizlemeControl.BilgiMetniAyarla(bilgi);
                        }
                        else
                        {
                            OnizlemeControl.HighlightTemizle();
                            OnizlemeControl.BilgiMetniAyarla("");
                        }
                    }
                };

                if (vm.AktifKesit != null)
                    OnizlemeControl.CizgileriYukle(vm.AktifKesit);

                OnizlemeControl.CizgiSecildi += (s, cizgi) => vm.SecilenCizgi = cizgi;
                OnizlemeControl.CokluSecimDegisti += (s, liste) => vm.SecilenCizgiler = liste;
            }
        }

        private void OnContentRendered(object sender, System.EventArgs e)
        {
            if (DataContext is KesitDogrulamaViewModel vm && vm.AktifKesit != null)
                OnizlemeControl.CizgileriYukle(vm.AktifKesit);
        }

        private static CizgiRolu MalzemedenRol(string malzemeAdi)
        {
            switch (malzemeAdi)
            {
                case "Siyirma": return CizgiRolu.Siyirma;
                case "Yarma": return CizgiRolu.Yarma;
                case "Dolgu": return CizgiRolu.Dolgu;
                case "Asinma": return CizgiRolu.Asinma;
                case "Binder": return CizgiRolu.Binder;
                case "Bitumlu Temel": return CizgiRolu.BitumluTemel;
                case "Plentmiks": return CizgiRolu.Plentmiks;
                case "Alttemel": return CizgiRolu.AltTemel;
                case "B.T. Yerine Konan": return CizgiRolu.BTYerineKonan;
                case "B.T. Yerine Konmayan": return CizgiRolu.BTYerineKonmayan;
                default: return CizgiRolu.Tanimsiz;
            }
        }
    }
}
