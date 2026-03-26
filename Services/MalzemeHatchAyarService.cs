using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metraj.Models;
using Metraj.Services.Interfaces;
using Newtonsoft.Json;

namespace Metraj.Services
{
    public class MalzemeHatchAyarService : IMalzemeHatchAyarService
    {
        private MalzemeHatchAyarlari _ayarlar;

        public MalzemeHatchAyarService()
        {
            _ayarlar = AyarlariYukle();
        }

        public MalzemeHatchAyarlari AyarlariYukle()
        {
            string dosyaYolu = DosyaYolunuBul();

            if (!string.IsNullOrEmpty(dosyaYolu) && File.Exists(dosyaYolu))
            {
                try
                {
                    string json = File.ReadAllText(dosyaYolu);
                    var ayarlar = JsonConvert.DeserializeObject<MalzemeHatchAyarlari>(json);
                    if (ayarlar != null && ayarlar.Ayarlar.Count > 0)
                    {
                        _ayarlar = ayarlar;
                        LoggingService.Info("Hatch ayarlar\u0131 y\u00FCklendi: {Adet} malzeme", ayarlar.Ayarlar.Count);
                        return ayarlar;
                    }
                }
                catch (System.Exception ex)
                {
                    LoggingService.Warning("Hatch ayarlar\u0131 okunamad\u0131: {Hata}", ex);
                }
            }

            var varsayilan = MalzemeHatchAyarlari.VarsayilanOlustur();
            _ayarlar = varsayilan;
            return varsayilan;
        }

        public void AyarlariKaydet(MalzemeHatchAyarlari ayarlar)
        {
            if (ayarlar == null) return;
            _ayarlar = ayarlar;

            string dosyaYolu = DosyaYolunuBul() ?? VarsayilanDosyaYolu();
            try
            {
                string klasor = Path.GetDirectoryName(dosyaYolu);
                if (!Directory.Exists(klasor))
                    Directory.CreateDirectory(klasor);

                string json = JsonConvert.SerializeObject(ayarlar, Formatting.Indented);
                File.WriteAllText(dosyaYolu, json);
                LoggingService.Info("Hatch ayarlar\u0131 kaydedildi: {DosyaYolu}", dosyaYolu);
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Hatch ayarlar\u0131 kaydedilemedi", ex);
            }
        }

        public MalzemeHatchAyari MalzemeAyariGetir(string malzemeAdi)
        {
            return _ayarlar.AyarGetir(malzemeAdi);
        }

        public List<string> TumMalzemeAdlari()
        {
            return _ayarlar.Ayarlar.Select(a => a.MalzemeAdi).ToList();
        }

        private string DosyaYolunuBul()
        {
            try
            {
                string dllKlasor = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string dosya = Path.Combine(dllKlasor, Constants.HatchAyarlariDosyaAdi);
                if (File.Exists(dosya)) return dosya;
            }
            catch { }

            string appDataYolu = VarsayilanDosyaYolu();
            if (File.Exists(appDataYolu)) return appDataYolu;

            return null;
        }

        private string VarsayilanDosyaYolu()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "MetrajAsistani", Constants.HatchAyarlariDosyaAdi);
        }
    }
}
