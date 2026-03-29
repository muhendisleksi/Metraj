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
                };

                if (vm.AktifKesit != null)
                    OnizlemeControl.CizgileriYukle(vm.AktifKesit);

                OnizlemeControl.CizgiSecildi += (s, cizgi) => vm.SecilenCizgi = cizgi;
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
