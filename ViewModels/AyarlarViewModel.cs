using System;
using System.IO;
using System.Windows.Input;
using Newtonsoft.Json;
using Metraj.Models;
using Metraj.Services;

namespace Metraj.ViewModels
{
    public class AyarlarViewModel : ViewModelBase
    {
        private double _textYuksekligi = 2.5;
        private int _ondalikSayisi = 2;
        private bool _leaderGoster;
        private string _durumMesaji = "";

        private static readonly string AyarlarDosyaYolu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Metraj", "ayarlar.json");

        public double TextYuksekligi
        {
            get => _textYuksekligi;
            set => SetProperty(ref _textYuksekligi, value);
        }

        public int OndalikSayisi
        {
            get => _ondalikSayisi;
            set => SetProperty(ref _ondalikSayisi, value);
        }

        public bool LeaderGoster
        {
            get => _leaderGoster;
            set => SetProperty(ref _leaderGoster, value);
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }

        public ICommand KaydetCommand { get; }
        public ICommand VarsayilanlarCommand { get; }

        public AyarlarViewModel()
        {
            KaydetCommand = new RelayCommand(Kaydet);
            VarsayilanlarCommand = new RelayCommand(Varsayilanlara);
            Yukle();
        }

        private void Kaydet()
        {
            try
            {
                var ayarlar = new
                {
                    TextYuksekligi,
                    OndalikSayisi,
                    LeaderGoster
                };

                var dir = Path.GetDirectoryName(AyarlarDosyaYolu);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(AyarlarDosyaYolu, JsonConvert.SerializeObject(ayarlar, Formatting.Indented));
                DurumMesaji = "Ayarlar kaydedildi.";
                LoggingService.Info("Ayarlar kaydedildi");
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Kaydetme hatasi: " + ex.Message;
                LoggingService.Error("Ayar kaydetme hatasi", ex);
            }
        }

        private void Yukle()
        {
            try
            {
                if (!File.Exists(AyarlarDosyaYolu)) return;

                var json = File.ReadAllText(AyarlarDosyaYolu);
                var ayarlar = JsonConvert.DeserializeAnonymousType(json, new
                {
                    TextYuksekligi = 2.5,
                    OndalikSayisi = 2,
                    LeaderGoster = false
                });

                if (ayarlar != null)
                {
                    TextYuksekligi = ayarlar.TextYuksekligi;
                    OndalikSayisi = ayarlar.OndalikSayisi;
                    LeaderGoster = ayarlar.LeaderGoster;
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("Ayar yukleme hatasi", ex);
            }
        }

        private void Varsayilanlara()
        {
            TextYuksekligi = 2.5;
            OndalikSayisi = 2;
            LeaderGoster = false;
            DurumMesaji = "Varsayilan ayarlar yuklendi.";
        }

        public AnnotationAyarlari GetAnnotationAyarlari(string katmanAdi = null)
        {
            return new AnnotationAyarlari
            {
                TextYuksekligi = TextYuksekligi,
                KatmanAdi = katmanAdi ?? Constants.LayerEtiket,
                Format = "{0:F" + OndalikSayisi + "}",
                LeaderGoster = LeaderGoster
            };
        }
    }
}
