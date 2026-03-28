using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Metraj.Models.YolEnkesit;

namespace Metraj.Views.EnkesitOkuma
{
    public partial class KesitSecimWindow : Window
    {
        public int SecilenIndex { get; private set; } = -1;

        public KesitSecimWindow()
        {
            InitializeComponent();
        }

        public void Yukle(List<KesitSecimOgesi> ogeler, int varsayilanIndex)
        {
            KesitListBox.ItemsSource = ogeler;
            if (varsayilanIndex >= 0 && varsayilanIndex < ogeler.Count)
            {
                KesitListBox.SelectedIndex = varsayilanIndex;
                KesitListBox.ScrollIntoView(ogeler[varsayilanIndex]);
            }
        }

        private void BtnSec_Click(object sender, RoutedEventArgs e)
        {
            if (KesitListBox.SelectedItem is KesitSecimOgesi oge)
            {
                SecilenIndex = oge.Index;
                DialogResult = true;
                Close();
            }
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ListBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            BtnSec_Click(sender, e);
        }
    }
}
