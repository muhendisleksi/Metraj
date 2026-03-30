using System.Windows;
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

                    // Kiyas tablosu satir secimi → onizlemede rol bazli highlight+zoom + bilgi metni
                    if (args.PropertyName == nameof(vm.SecilenKiyas))
                    {
                        if (vm.SecilenKiyas != null)
                        {
                            var k = vm.SecilenKiyas;
                            OnizlemeControl.HighlightRol(k.UstCizgiRolu, k.AltCizgiRolu);

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
            // Loaded sirasinda Canvas henuz boyutlanmamis olabilir.
            // ContentRendered'da layout kesinlikle tamamlanmistir — ilk kesiti tekrar ciz.
            if (DataContext is KesitDogrulamaViewModel vm && vm.AktifKesit != null)
                OnizlemeControl.CizgileriYukle(vm.AktifKesit);
        }
    }
}
