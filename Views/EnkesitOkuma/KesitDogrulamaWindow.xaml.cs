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
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is KesitDogrulamaViewModel vm)
            {
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(vm.AktifKesit) && vm.AktifKesit != null)
                        OnizlemeControl.CizgileriYukle(vm.AktifKesit.Cizgiler);
                };

                if (vm.AktifKesit != null)
                    OnizlemeControl.CizgileriYukle(vm.AktifKesit.Cizgiler);

                OnizlemeControl.CizgiSecildi += (s, cizgi) => vm.SecilenCizgi = cizgi;
            }
        }
    }
}
