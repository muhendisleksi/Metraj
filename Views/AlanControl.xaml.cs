using System.Windows.Controls;
using Metraj.Models;
using Metraj.ViewModels;

namespace Metraj.Views
{
    public partial class AlanControl : UserControl
    {
        public AlanControl()
        {
            InitializeComponent();
        }

        private void BirimComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is AlanViewModel vm && BirimComboBox.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag as string;
                switch (tag)
                {
                    case "Hektar":
                        vm.SeciliBirim = BirimTipi.Hektar;
                        break;
                    case "Donum":
                        vm.SeciliBirim = BirimTipi.Donum;
                        break;
                    default:
                        vm.SeciliBirim = BirimTipi.Metrekare;
                        break;
                }
            }
        }
    }
}
