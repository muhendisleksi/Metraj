using System.Windows;
using Metraj.ViewModels.EnkesitOkuma;

namespace Metraj.Views.EnkesitOkuma
{
    public partial class ReferansKesitWindow : Window
    {
        public ReferansKesitWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ReferansKesitViewModel;
            if (vm == null) return;

            OnizlemeControl.CizgileriYukle(new System.Collections.Generic.List<Models.YolEnkesit.CizgiTanimi>(vm.Cizgiler));

            // Canvas -> Liste senkronizasyonu
            OnizlemeControl.CizgiSecildi += (s, cizgi) =>
            {
                vm.SecilenCizgi = cizgi;
            };

            // Liste -> Canvas senkronizasyonu
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(vm.SecilenCizgi) && vm.SecilenCizgi != null)
                {
                    OnizlemeControl.VurgulaCizgi(vm.SecilenCizgi);
                }
            };
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReferansKesitViewModel vm)
            {
                vm.Kaydet.Execute(null);
                DialogResult = true;
                Close();
            }
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
