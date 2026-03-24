using System;

namespace Metraj.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private int _seciliTabIndex;
        private string _durumMesaji = "Hazır";

        public int SeciliTabIndex
        {
            get => _seciliTabIndex;
            set => SetProperty(ref _seciliTabIndex, value);
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }
    }
}
