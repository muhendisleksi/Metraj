using System.Collections.Generic;
using System.Windows;
using Metraj.Models.YolEnkesit;
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

            // Kalibrasyon: hibrit mod — rol atanmis cizgiler rol rengi, atanmamislar orijinal
            OnizlemeControl.RenkModu = OnizlemeRenkModu.HibritRenk;

            // Ilk yukleme: tum cizgileri Canvas'a gonder (dogrulama ile ayni veri)
            OnizlemeControl.CizgileriYukle(new List<CizgiTanimi>(vm.Cizgiler));

            // Canvas -> ViewModel senkronizasyonu
            OnizlemeControl.CizgiSecildi += (s, cizgi) => vm.SecilenCizgi = cizgi;

            // Rol degistiginde onizlemeyi guncelle — zoom/pan/highlight korunur
            vm.FiltreDegisti += (s, ev) =>
            {
                OnizlemeControl.CizgileriGuncelle(new List<CizgiTanimi>(vm.Cizgiler));
            };

            // Tablo satir secimi → onizlemede zoom + highlight
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(vm.SecilenGrup))
                {
                    if (vm.SecilenGrup != null)
                        OnizlemeControl.HighlightLayer(vm.SecilenGrup.LayerAdi, vm.SecilenGrup.RenkIndex);
                    else
                        OnizlemeControl.HighlightTemizle();
                }
            };

            // Kesit degistirildiginde Canvas'i yeniden yukle
            vm.KesitDegistirildi += (s, ev) =>
            {
                OnizlemeControl.CizgileriYukle(new List<CizgiTanimi>(vm.Cizgiler));
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
