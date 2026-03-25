using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metraj.Models;
using Metraj.Services.Interfaces;
using Newtonsoft.Json;

namespace Metraj.Services
{
    public class KatmanEslestirmeService : IKatmanEslestirmeService
    {
        private KatmanEslestirmeAyarlari _ayarlar;

        public KatmanEslestirmeService()
        {
            _ayarlar = AyarlariYukle();
        }

        public KatmanEslestirme LayerEslestir(string layerAdi)
        {
            if (string.IsNullOrWhiteSpace(layerAdi))
                return null;

            // JSON config'ten eşleştir
            var eslestirme = _ayarlar.Eslestirmeler
                .Where(e => e.Aktif)
                .FirstOrDefault(e => PatternEslesiyor(layerAdi, e.LayerPattern));

            if (eslestirme != null)
                return eslestirme;

            // Fallback: hardcoded eşleştirme (EnKesitAlanService.MalzemeAdiCikar mantığı)
            var fallback = HardcodedEslestir(layerAdi);
            return fallback;
        }

        public KatmanEslestirmeAyarlari AyarlariYukle()
        {
            string dosyaYolu = AyarDosyaYolunuBul();

            if (!string.IsNullOrEmpty(dosyaYolu) && File.Exists(dosyaYolu))
            {
                try
                {
                    string json = File.ReadAllText(dosyaYolu);
                    var ayarlar = JsonConvert.DeserializeObject<KatmanEslestirmeAyarlari>(json);
                    if (ayarlar != null && ayarlar.Eslestirmeler.Count > 0)
                    {
                        _ayarlar = ayarlar;
                        LoggingService.Info("Katman eşleştirme ayarları yüklendi: {DosyaYolu}, {Adet} kural",
                            dosyaYolu, ayarlar.Eslestirmeler.Count);
                        return ayarlar;
                    }
                }
                catch (System.Exception ex)
                {
                    LoggingService.Warning("Katman eşleştirme ayarları okunamadı: {Hata}", ex);
                }
            }

            var varsayilan = KatmanEslestirmeAyarlari.VarsayilanOlustur();
            _ayarlar = varsayilan;
            return varsayilan;
        }

        public void AyarlariKaydet(KatmanEslestirmeAyarlari ayarlar)
        {
            if (ayarlar == null) return;

            _ayarlar = ayarlar;
            string dosyaYolu = AyarDosyaYolunuBul() ?? VarsayilanAyarDosyaYolu();

            try
            {
                string klasor = Path.GetDirectoryName(dosyaYolu);
                if (!Directory.Exists(klasor))
                    Directory.CreateDirectory(klasor);

                string json = JsonConvert.SerializeObject(ayarlar, Formatting.Indented);
                File.WriteAllText(dosyaYolu, json);
                LoggingService.Info("Katman eşleştirme ayarları kaydedildi: {DosyaYolu}", dosyaYolu);
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Katman eşleştirme ayarları kaydedilemedi: {Hata}", ex);
            }
        }

        public bool PatternEslesiyor(string layerAdi, string pattern)
        {
            if (string.IsNullOrEmpty(layerAdi) || string.IsNullOrEmpty(pattern))
                return false;

            string upperLayer = layerAdi.ToUpperInvariant();
            string upperPattern = pattern.ToUpperInvariant();

            // Tam eşleşme
            if (!upperPattern.Contains("*"))
                return upperLayer.Equals(upperPattern, StringComparison.OrdinalIgnoreCase);

            // Wildcard pattern: "KAZI*" → StartsWith, "*KAZI" → EndsWith, "*KAZI*" → Contains
            // "BIT*TEMEL*" gibi ortada * olan durumlar
            string[] parcalar = upperPattern.Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries);

            if (parcalar.Length == 0)
                return true; // Pattern sadece "*"

            if (parcalar.Length == 1)
            {
                if (upperPattern.StartsWith("*") && upperPattern.EndsWith("*"))
                    return upperLayer.Contains(parcalar[0]);
                if (upperPattern.StartsWith("*"))
                    return upperLayer.EndsWith(parcalar[0]);
                if (upperPattern.EndsWith("*"))
                    return upperLayer.StartsWith(parcalar[0]);
                return upperLayer.Equals(parcalar[0]);
            }

            // Birden fazla parça: hepsinin sırasıyla bulunması gerekir
            int aramaBaslangic = 0;
            for (int i = 0; i < parcalar.Length; i++)
            {
                int bulunanIndex = upperLayer.IndexOf(parcalar[i], aramaBaslangic, StringComparison.OrdinalIgnoreCase);
                if (bulunanIndex < 0) return false;

                // İlk parça pattern başında * yoksa baştan eşleşmeli
                if (i == 0 && !upperPattern.StartsWith("*") && bulunanIndex != 0)
                    return false;

                aramaBaslangic = bulunanIndex + parcalar[i].Length;
            }

            // Son parça pattern sonunda * yoksa sonda eşleşmeli
            if (!upperPattern.EndsWith("*") && aramaBaslangic != upperLayer.Length)
                return false;

            return true;
        }

        private KatmanEslestirme HardcodedEslestir(string layerAdi)
        {
            if (string.IsNullOrWhiteSpace(layerAdi)) return null;

            string upper = layerAdi.ToUpperInvariant()
                .Replace("\u0130", "I").Replace("\u015E", "S").Replace("\u00C7", "C")
                .Replace("\u011E", "G").Replace("\u00DC", "U").Replace("\u00D6", "O");

            if (upper.Contains("ASINMA") || upper.Contains("BSK"))
                return new KatmanEslestirme { MalzemeAdi = "A\u015F\u0131nma", Kategori = MalzemeKategorisi.Ustyapi };
            if (upper.Contains("BINDER"))
                return new KatmanEslestirme { MalzemeAdi = "Binder", Kategori = MalzemeKategorisi.Ustyapi };
            if (upper.Contains("BITUM"))
                return new KatmanEslestirme { MalzemeAdi = "Bit\u00FCml\u00FC Temel", Kategori = MalzemeKategorisi.Ustyapi };
            if (upper.Contains("PLENT") || upper.Contains("PMT"))
                return new KatmanEslestirme { MalzemeAdi = "Plentmiks Temel", Kategori = MalzemeKategorisi.Alttemel };
            if (upper.Contains("KIRMATAS") || upper.Contains("KMT"))
                return new KatmanEslestirme { MalzemeAdi = "K\u0131rmata\u015F Temel", Kategori = MalzemeKategorisi.Alttemel };
            if (upper.Contains("STAB"))
                return new KatmanEslestirme { MalzemeAdi = "Stabilize", Kategori = MalzemeKategorisi.Alttemel };
            if (upper.Contains("YARMA") || upper.Contains("KAZI"))
                return new KatmanEslestirme { MalzemeAdi = "Kaz\u0131", Kategori = MalzemeKategorisi.ToprakIsleri };
            if (upper.Contains("DOLGU"))
                return new KatmanEslestirme { MalzemeAdi = "Dolgu", Kategori = MalzemeKategorisi.ToprakIsleri };
            if (upper.Contains("SEV"))
                return new KatmanEslestirme { MalzemeAdi = "\u015Eev", Kategori = MalzemeKategorisi.ToprakIsleri };
            if (upper.Contains("BANKET"))
                return new KatmanEslestirme { MalzemeAdi = "Banket", Kategori = MalzemeKategorisi.Ozel };
            if (upper.Contains("HENDEK"))
                return new KatmanEslestirme { MalzemeAdi = "Hendek", Kategori = MalzemeKategorisi.Ozel };

            return null;
        }

        private string AyarDosyaYolunuBul()
        {
            // Önce DLL'in yanında ara
            try
            {
                string dllKlasor = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string dosya = Path.Combine(dllKlasor, Constants.KatmanEslestirmeDosyaAdi);
                if (File.Exists(dosya)) return dosya;
            }
            catch { }

            // AppData altında ara
            string appDataYolu = VarsayilanAyarDosyaYolu();
            if (File.Exists(appDataYolu)) return appDataYolu;

            return null;
        }

        private string VarsayilanAyarDosyaYolu()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "MetrajAsistani", Constants.KatmanEslestirmeDosyaAdi);
        }
    }
}
