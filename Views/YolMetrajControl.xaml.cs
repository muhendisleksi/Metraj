using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Metraj.Models;
using Metraj.ViewModels;

namespace Metraj.Views
{
    public partial class YolMetrajControl : UserControl
    {
        public YolMetrajControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is YolMetrajViewModel oldVm)
                oldVm.SeciliKolonIstasyonlari.CollectionChanged -= OnIstasyonlarChanged;

            if (e.NewValue is YolMetrajViewModel newVm)
                newVm.SeciliKolonIstasyonlari.CollectionChanged += OnIstasyonlarChanged;
        }

        private void OnIstasyonlarChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SutunlariGuncelle();
        }

        private void SutunlariGuncelle()
        {
            if (!(DataContext is YolMetrajViewModel vm)) return;

            var istasyonlar = vm.SeciliKolonIstasyonlari;
            IstasyonlarGrid.Columns.Clear();

            // Sabit: Istasyon sutunu
            IstasyonlarGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "\u0130stasyon",
                Binding = new Binding("IstasyonMetni"),
                Width = new DataGridLength(70)
            });

            // Dinamik: tum malzeme adlarini topla
            var malzemeler = istasyonlar
                .SelectMany(i => i.KatmanAlanlari)
                .Select(k => k.MalzemeAdi)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var malzeme in malzemeler)
            {
                var col = new DataGridTextColumn
                {
                    Header = malzeme,
                    Binding = new Binding
                    {
                        Path = new PropertyPath("."),
                        Converter = new MalzemeAlanConverter(),
                        ConverterParameter = malzeme,
                        StringFormat = "F2"
                    },
                    Width = new DataGridLength(60)
                };
                col.ElementStyle = SagaYaslaStil();
                IstasyonlarGrid.Columns.Add(col);
            }

            // Sabit: Kalem sayisi
            var kalemCol = new DataGridTextColumn
            {
                Header = "Kalem",
                Binding = new Binding("KatmanAlanlari.Count"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            kalemCol.ElementStyle = OrtalaStil();
            IstasyonlarGrid.Columns.Add(kalemCol);
        }

        private Style SagaYaslaStil()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 4, 0)));
            return style;
        }

        private Style OrtalaStil()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            return style;
        }

        private void IstasyonlarGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(DataContext is YolMetrajViewModel vm)) return;

            // Tiklanan hucrenin DataGridCell'ini bul
            DependencyObject dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is DataGridCell))
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridCell cell && cell.Column != null)
            {
                string header = cell.Column.Header as string;
                if (header != null && header != "\u0130stasyon" && header != "Kalem")
                {
                    vm.SagTikMalzemeAdi = header;

                    // Tiklanan satiri da sec
                    DependencyObject rowDep = dep;
                    while (rowDep != null && !(rowDep is DataGridRow))
                        rowDep = VisualTreeHelper.GetParent(rowDep);
                    if (rowDep is DataGridRow row)
                        IstasyonlarGrid.SelectedItem = row.Item;

                    return;
                }
            }
            vm.SagTikMalzemeAdi = null;
        }

        private void MetotCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is YolMetrajViewModel vm && MetotCombo.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag as string;
                switch (tag)
                {
                    case "Prismoidal":
                        vm.SeciliMetot = HacimMetodu.Prismoidal;
                        break;
                    default:
                        vm.SeciliMetot = HacimMetodu.OrtalamaAlan;
                        break;
                }
            }
        }
    }

    public class MalzemeAlanConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is YolKesitVerisi kesit && parameter is string malzemeAdi)
            {
                double alan = kesit.MalzemeAlaniGetir(malzemeAdi);
                return alan.ToString("F2");
            }
            return "0.00";
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
