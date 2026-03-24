using System.Windows.Controls;
using Metraj.Models;
using Metraj.ViewModels;

namespace Metraj.Views
{
    public partial class HacimControl : UserControl
    {
        public HacimControl()
        {
            InitializeComponent();
        }

        private void MetotCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is HacimViewModel vm && MetotCombo.SelectedItem is ComboBoxItem item)
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
}
