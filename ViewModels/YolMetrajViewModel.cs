using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Metraj.Infrastructure;
using Metraj.Models;
using Metraj.Services;
using Metraj.Services.Interfaces;
using Newtonsoft.Json;

namespace Metraj.ViewModels
{
    public class YolMetrajViewModel : ViewModelBase
    {
        private HacimMetodu _seciliMetot = HacimMetodu.OrtalamaAlan;
        private string _durumMesaji = "Kolon ekleyip istasyonlar\u0131 girin.";
        private YolKolonu _seciliKolon;
        private YolKesitVerisi _seciliIstasyon;
        private int _kolonSayaci = 0;
        private string _sagTikMalzemeAdi;

        public ObservableCollection<YolKolonu> Kolonlar { get; } = new ObservableCollection<YolKolonu>();
        public ObservableCollection<YolKesitVerisi> SeciliKolonIstasyonlari { get; } = new ObservableCollection<YolKesitVerisi>();
        public ObservableCollection<KatmanAlanBilgisi> SeciliIstasyonKatmanlari { get; } = new ObservableCollection<KatmanAlanBilgisi>();
        public ObservableCollection<MalzemeHacimOzeti> MalzemeOzetleri { get; } = new ObservableCollection<MalzemeHacimOzeti>();

        public HacimMetodu SeciliMetot { get => _seciliMetot; set => SetProperty(ref _seciliMetot, value); }
        public string DurumMesaji { get => _durumMesaji; set => SetProperty(ref _durumMesaji, value); }
        public string SagTikMalzemeAdi
        {
            get => _sagTikMalzemeAdi;
            set { if (SetProperty(ref _sagTikMalzemeAdi, value)) OnPropertyChanged(nameof(KalemSilBaslik)); }
        }
        public string KalemSilBaslik => string.IsNullOrEmpty(_sagTikMalzemeAdi)
            ? "Kalemi Sil" : $"\"{_sagTikMalzemeAdi}\" Kalemini Sil";

        public YolKolonu SeciliKolon
        {
            get => _seciliKolon;
            set { if (SetProperty(ref _seciliKolon, value)) SeciliKolonDegisti(); }
        }

        public YolKesitVerisi SeciliIstasyon
        {
            get => _seciliIstasyon;
            set { if (SetProperty(ref _seciliIstasyon, value)) SeciliIstasyonDegisti(); }
        }

        public double ToplamKaziHacmi => _seciliKolon?.KubajSonucu?.ToplamKaziHacmi ?? 0;
        public double ToplamDolguHacmi => _seciliKolon?.KubajSonucu?.ToplamDolguHacmi ?? 0;
        public double NetHacim => _seciliKolon?.KubajSonucu?.NetHacim ?? 0;

        public ICommand KolonEkleCommand { get; }
        public ICommand KolonSilCommand { get; }
        public ICommand IstasyonEkleCommand { get; }
        public ICommand IstasyonSilCommand { get; }
        public ICommand KalemEkleCommand { get; }
        public ICommand HesaplaCommand { get; }
        public ICommand TemizleCommand { get; }
        public ICommand ExcelAktarCommand { get; }
        public ICommand IstasyonlariSifirlaCommand { get; }
        public ICommand TumHatchTemizleCommand { get; }
        public ICommand KalemSilCommand { get; }
        public ICommand AlanEkleCommand { get; }
        public ICommand AlanCikarCommand { get; }
        public ICommand KaydetCommand { get; }
        public ICommand YukleCommand { get; }

        public YolMetrajViewModel()
        {
            KolonEkleCommand = new RelayCommand(KolonEkle);
            KolonSilCommand = new RelayCommand(KolonSil, () => SeciliKolon != null);
            IstasyonEkleCommand = new RelayCommand(IstasyonEkle, () => SeciliKolon != null);
            IstasyonSilCommand = new RelayCommand(IstasyonSil, () => SeciliIstasyon != null);
            IstasyonlariSifirlaCommand = new RelayCommand(IstasyonlariSifirla, () => SeciliKolon != null && SeciliKolon.Istasyonlar.Count > 0);
            KalemEkleCommand = new RelayCommand(KalemEkle, () => SeciliIstasyon != null);
            KalemSilCommand = new RelayCommand(KalemSil, () => SeciliIstasyon != null && !string.IsNullOrEmpty(SagTikMalzemeAdi));
            AlanEkleCommand = new RelayCommand(AlanEkle, () => SeciliIstasyon != null && !string.IsNullOrEmpty(SagTikMalzemeAdi));
            AlanCikarCommand = new RelayCommand(AlanCikar, () => SeciliIstasyon != null && !string.IsNullOrEmpty(SagTikMalzemeAdi));
            HesaplaCommand = new RelayCommand(Hesapla, () => SeciliKolon != null && SeciliKolon.Istasyonlar.Count >= 2);
            TemizleCommand = new RelayCommand(Temizle);
            ExcelAktarCommand = new RelayCommand(ExcelAktar, () => Kolonlar.Count > 0);
            TumHatchTemizleCommand = new RelayCommand(TumHatchTemizle);
            KaydetCommand = new RelayCommand(Kaydet, () => Kolonlar.Count > 0);
            YukleCommand = new RelayCommand(Yukle);
        }

        private void KolonEkle()
        {
            var kesitService = ServiceContainer.GetRequiredService<IYolKesitService>();
            string kolonHarfi = kesitService.KolonHarfiUret(_kolonSayaci);
            var kolon = new YolKolonu { KolonHarfi = kolonHarfi, Aciklama = $"G\u00FCzergah {kolonHarfi}" };
            Kolonlar.Add(kolon);
            SeciliKolon = kolon;
            _kolonSayaci++;
            DurumMesaji = $"Kolon {kolonHarfi} eklendi. '\u0130stasyon Ekle' ile istasyon girin.";
        }

        private void KolonSil()
        {
            if (SeciliKolon == null) return;
            Kolonlar.Remove(SeciliKolon);
            SeciliKolon = Kolonlar.Count > 0 ? Kolonlar[Kolonlar.Count - 1] : null;
            DurumMesaji = $"{Kolonlar.Count} kolon mevcut.";
        }

        private void IstasyonEkle()
        {
            if (SeciliKolon == null) { DurumMesaji = "\u00D6nce kolon ekleyin."; return; }

            try
            {
                var kesitService = ServiceContainer.GetRequiredService<IYolKesitService>();
                var kesit = kesitService.TiklaIsaretleKesitOku(SeciliKolon.KolonHarfi);
                if (kesit == null) { DurumMesaji = "\u0130ptal edildi."; return; }
                if (kesit.KatmanAlanlari.Count == 0) { DurumMesaji = "Alan i\u015Faretlenmedi."; return; }

                var mevcut = SeciliKolon.Istasyonlar.FirstOrDefault(i => Math.Abs(i.Istasyon - kesit.Istasyon) < 0.01);
                if (mevcut != null)
                {
                    BirlesimYap(mevcut, kesit.KatmanAlanlari);
                    SeciliKolonDegisti();
                    SeciliIstasyon = mevcut;
                }
                else
                {
                    SeciliKolon.Istasyonlar.Add(kesit);
                    SeciliKolonDegisti();
                    SeciliIstasyon = kesit;
                }

                var sonIst = mevcut ?? kesit;
                DurumMesaji = $"Km {sonIst.IstasyonMetni}: {sonIst.KatmanAlanlari.Count} kalem";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("\u0130stasyon ekleme hatas\u0131", ex);
            }
        }

        private void KalemEkle()
        {
            if (SeciliKolon == null || SeciliIstasyon == null)
            {
                DurumMesaji = "\u00D6nce istasyon se\u00E7in.";
                return;
            }

            try
            {
                var kesitService = ServiceContainer.GetRequiredService<IYolKesitService>();
                var yeniKalemler = kesitService.KalemEkle(SeciliKolon.KolonHarfi);
                if (yeniKalemler == null || yeniKalemler.Count == 0)
                {
                    DurumMesaji = "Kalem eklenmedi.";
                    return;
                }

                BirlesimYap(SeciliIstasyon, yeniKalemler);
                SeciliKolonDegisti();
                SeciliIstasyon = SeciliIstasyon; // detay\u0131 yenile
                SeciliIstasyonDegisti();
                DurumMesaji = $"Km {SeciliIstasyon.IstasyonMetni}: {SeciliIstasyon.KatmanAlanlari.Count} kalem";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Kalem ekleme hatas\u0131", ex);
            }
        }

        private void BirlesimYap(YolKesitVerisi hedef, List<KatmanAlanBilgisi> yeniKalemler)
        {
            foreach (var yeni in yeniKalemler)
            {
                var mevcut = hedef.KatmanAlanlari.FirstOrDefault(k =>
                    k.MalzemeAdi.Equals(yeni.MalzemeAdi, StringComparison.OrdinalIgnoreCase));
                if (mevcut != null)
                {
                    mevcut.Alan += yeni.Alan;
                    mevcut.TiklamaNoktalari.AddRange(yeni.TiklamaNoktalari);
                }
                else
                    hedef.KatmanAlanlari.Add(yeni);
            }
            hedef.ToplamKaziAlani = hedef.KatmanAlanlari
                .Where(k => k.MalzemeAdi.Equals("Yarma", StringComparison.OrdinalIgnoreCase)).Sum(k => k.Alan);
            hedef.ToplamDolguAlani = hedef.KatmanAlanlari
                .Where(k => k.MalzemeAdi.Equals("Dolgu", StringComparison.OrdinalIgnoreCase)).Sum(k => k.Alan);
        }

        private void IstasyonSil()
        {
            if (SeciliKolon == null || SeciliIstasyon == null) return;
            SeciliKolon.Istasyonlar.Remove(SeciliIstasyon);
            SeciliIstasyon = null;
            SeciliKolonDegisti();
        }

        private void KalemSil()
        {
            if (SeciliIstasyon == null || string.IsNullOrEmpty(SagTikMalzemeAdi)) return;
            var kalem = SeciliIstasyon.KatmanAlanlari.FirstOrDefault(k =>
                k.MalzemeAdi.Equals(SagTikMalzemeAdi, StringComparison.OrdinalIgnoreCase));
            if (kalem == null) { DurumMesaji = $"{SagTikMalzemeAdi} bulunamad\u0131."; return; }

            // Hatch ve etiketi cizimdcen sil
            try
            {
                var hatchService = ServiceContainer.GetRequiredService<IHatchOlusturmaService>();
                var ayarService = ServiceContainer.GetRequiredService<Services.MalzemeHatchAyarService>();
                var ayar = ayarService.MalzemeAyariGetir(SagTikMalzemeAdi);
                if (ayar != null && kalem.TiklamaNoktalari.Count > 0)
                    hatchService.MalzemeHatchSil(ayar.LayerAdi, ayar.EtiketLayerAdi, kalem.TiklamaNoktalari);
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("Hatch silme hatasi", ex);
            }

            SeciliIstasyon.KatmanAlanlari.Remove(kalem);
            SeciliIstasyon.ToplamKaziAlani = SeciliIstasyon.KatmanAlanlari
                .Where(k => k.MalzemeAdi.Equals("Yarma", StringComparison.OrdinalIgnoreCase)).Sum(k => k.Alan);
            SeciliIstasyon.ToplamDolguAlani = SeciliIstasyon.KatmanAlanlari
                .Where(k => k.MalzemeAdi.Equals("Dolgu", StringComparison.OrdinalIgnoreCase)).Sum(k => k.Alan);

            string silinen = SagTikMalzemeAdi;
            SeciliKolonDegisti();
            SeciliIstasyon = SeciliIstasyon;
            SeciliIstasyonDegisti();
            DurumMesaji = $"Km {SeciliIstasyon?.IstasyonMetni}: \"{silinen}\" kalemi silindi.";
        }

        private void AlanEkle() { AlanDuzelt(true); }
        private void AlanCikar() { AlanDuzelt(false); }

        private void AlanDuzelt(bool ekleme)
        {
            if (SeciliIstasyon == null || SeciliKolon == null || string.IsNullOrEmpty(SagTikMalzemeAdi)) return;

            var kalem = SeciliIstasyon.KatmanAlanlari.FirstOrDefault(k =>
                k.MalzemeAdi.Equals(SagTikMalzemeAdi, StringComparison.OrdinalIgnoreCase));
            if (kalem == null) { DurumMesaji = $"{SagTikMalzemeAdi} bulunamad\u0131."; return; }

            try
            {
                var kesitService = ServiceContainer.GetRequiredService<IYolKesitService>();
                double duzeltme = kesitService.AlanDuzelt(SeciliKolon.KolonHarfi, SagTikMalzemeAdi, ekleme);
                if (Math.Abs(duzeltme) < Constants.AlanToleransi) return;

                kalem.Alan = Math.Max(0, kalem.Alan + duzeltme);

                SeciliIstasyon.ToplamKaziAlani = SeciliIstasyon.KatmanAlanlari
                    .Where(k => k.MalzemeAdi.Equals("Yarma", StringComparison.OrdinalIgnoreCase)).Sum(k => k.Alan);
                SeciliIstasyon.ToplamDolguAlani = SeciliIstasyon.KatmanAlanlari
                    .Where(k => k.MalzemeAdi.Equals("Dolgu", StringComparison.OrdinalIgnoreCase)).Sum(k => k.Alan);

                string islem = ekleme ? "eklendi" : "\u00E7\u0131kar\u0131ld\u0131";
                SeciliKolonDegisti();
                SeciliIstasyon = SeciliIstasyon;
                SeciliIstasyonDegisti();
                DurumMesaji = $"Km {SeciliIstasyon?.IstasyonMetni}: \"{SagTikMalzemeAdi}\" {islem} ({duzeltme:+0.00;-0.00} m\u00B2)";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Alan d\u00FCzeltme hatas\u0131", ex);
            }
        }

        private void IstasyonlariSifirla()
        {
            if (SeciliKolon == null || SeciliKolon.Istasyonlar.Count == 0) return;
            SeciliKolon.Istasyonlar.Clear();
            SeciliIstasyon = null;
            SeciliKolonDegisti();
            SeciliIstasyonKatmanlari.Clear();
            MalzemeOzetleri.Clear();
            SeciliKolon.KubajSonucu = null;
            OnPropertiesChanged("ToplamKaziHacmi", "ToplamDolguHacmi", "NetHacim");
            DurumMesaji = $"Kolon {SeciliKolon.KolonHarfi} istasyonlar\u0131 temizlendi.";
        }

        private void Hesapla()
        {
            if (SeciliKolon == null || SeciliKolon.Istasyonlar.Count < 2)
            { DurumMesaji = "En az 2 istasyon gerekli."; return; }

            try
            {
                var kubajService = ServiceContainer.GetRequiredService<IYolKubajService>();
                SeciliKolon.KubajSonucu = kubajService.KubajHesapla(SeciliKolon.Istasyonlar, SeciliMetot);
                MalzemeOzetleri.Clear();
                foreach (var ozet in SeciliKolon.KubajSonucu.MalzemeOzetleri)
                    MalzemeOzetleri.Add(ozet);
                OnPropertiesChanged("ToplamKaziHacmi", "ToplamDolguHacmi", "NetHacim");
                DurumMesaji = $"K\u00FCbaj: kaz\u0131={ToplamKaziHacmi:F2} m\u00B3, dolgu={ToplamDolguHacmi:F2} m\u00B3";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hesaplama hatas\u0131: " + ex.Message;
                LoggingService.Error("K\u00FCbaj hatas\u0131", ex);
            }
        }

        private void Temizle()
        {
            try { ServiceContainer.GetRequiredService<IHatchOlusturmaService>().TumHatchTemizle(); } catch { }
            Kolonlar.Clear();
            SeciliKolonIstasyonlari.Clear();
            SeciliIstasyonKatmanlari.Clear();
            MalzemeOzetleri.Clear();
            SeciliKolon = null;
            SeciliIstasyon = null;
            _kolonSayaci = 0;
            OnPropertiesChanged("ToplamKaziHacmi", "ToplamDolguHacmi", "NetHacim");
            DurumMesaji = "Temizlendi.";
        }

        private void TumHatchTemizle()
        {
            try
            {
                ServiceContainer.GetRequiredService<IHatchOlusturmaService>().TumHatchTemizle();
                DurumMesaji = "Hatch ve etiketler silindi.";
            }
            catch (System.Exception ex) { DurumMesaji = "Hata: " + ex.Message; }
        }

        // === KAYDET / Y\u00DCKLE ===

        private void Kaydet()
        {
            try
            {
                var veri = new YolMetrajKayitVerisi
                {
                    Kolonlar = Kolonlar.ToList(),
                    KolonSayaci = _kolonSayaci,
                    KayitTarihi = DateTime.Now
                };

                var dosyaYolu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "YolMetraj_Kayit.json");

                string json = JsonConvert.SerializeObject(veri, Formatting.Indented);
                File.WriteAllText(dosyaYolu, json);
                DurumMesaji = "Kaydedildi: " + dosyaYolu;
                LoggingService.Info("Yol metraj kaydedildi: {Dosya}", dosyaYolu);
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Kaydetme hatas\u0131: " + ex.Message;
                LoggingService.Error("Kaydetme hatas\u0131", ex);
            }
        }

        private void Yukle()
        {
            try
            {
                var dosyaYolu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "YolMetraj_Kayit.json");

                if (!File.Exists(dosyaYolu))
                {
                    DurumMesaji = "Kay\u0131t dosyas\u0131 bulunamad\u0131: " + dosyaYolu;
                    return;
                }

                string json = File.ReadAllText(dosyaYolu);
                var veri = JsonConvert.DeserializeObject<YolMetrajKayitVerisi>(json);
                if (veri == null || veri.Kolonlar == null)
                {
                    DurumMesaji = "Ge\u00E7ersiz kay\u0131t dosyas\u0131.";
                    return;
                }

                Kolonlar.Clear();
                foreach (var kolon in veri.Kolonlar)
                    Kolonlar.Add(kolon);

                _kolonSayaci = veri.KolonSayaci;
                SeciliKolon = Kolonlar.Count > 0 ? Kolonlar[0] : null;

                // Hatch'leri yeniden olu\u015Ftur
                int hatchSayisi = HatchleriYenidenOlustur();

                DurumMesaji = $"Y\u00FCklendi: {Kolonlar.Count} kolon, {Kolonlar.Sum(k => k.Istasyonlar.Count)} istasyon, {hatchSayisi} hatch";
                LoggingService.Info("Yol metraj y\u00FCklendi: {Dosya}", dosyaYolu);
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Y\u00FCkleme hatas\u0131: " + ex.Message;
                LoggingService.Error("Y\u00FCkleme hatas\u0131", ex);
            }
        }

        private void ExcelAktar()
        {
            try
            {
                var excelService = ServiceContainer.GetRequiredService<IExcelExportService>() as ExcelExportService;
                if (excelService == null) { DurumMesaji = "Excel servis hatas\u0131."; return; }

                var rapor = new YolMetrajRaporu
                {
                    Kesitler = Kolonlar.SelectMany(k => k.Istasyonlar).ToList(),
                    KubajSonucu = SeciliKolon?.KubajSonucu,
                    ProjeAdi = "Yol Metraj",
                    OlusturmaTarihi = DateTime.Now
                };

                var dosyaYolu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "YolMetraj_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");

                var result = excelService.YolMetrajExport(rapor, dosyaYolu);
                DurumMesaji = result.Basarili ? "Excel: " + result.DosyaYolu : "Excel hatas\u0131: " + result.HataMesaji;
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Excel hatas\u0131: " + ex.Message;
                LoggingService.Error("Excel hatas\u0131", ex);
            }
        }

        private int HatchleriYenidenOlustur()
        {
            int sayac = 0;
            try
            {
                var hatchService = ServiceContainer.GetRequiredService<IHatchOlusturmaService>();
                var ayarService = ServiceContainer.GetRequiredService<IMalzemeHatchAyarService>();

                foreach (var kolon in Kolonlar)
                {
                    foreach (var ist in kolon.Istasyonlar)
                    {
                        foreach (var katman in ist.KatmanAlanlari)
                        {
                            if (katman.TiklamaNoktalari == null || katman.TiklamaNoktalari.Count == 0)
                                continue;

                            var ayar = ayarService.MalzemeAyariGetir(katman.MalzemeAdi);
                            ObjectId sonHatchId = ObjectId.Null;

                            foreach (var nokta in katman.TiklamaNoktalari)
                            {
                                if (nokta.Length < 2) continue;
                                var pt = new Autodesk.AutoCAD.Geometry.Point3d(nokta[0], nokta[1], 0);
                                var (hatchId, _) = hatchService.HatchOlustur(pt, ayar);
                                if (!hatchId.IsNull)
                                {
                                    sonHatchId = hatchId;
                                    sayac++;
                                }
                            }

                            // En son hatch'e etiket at
                            if (!sonHatchId.IsNull)
                                hatchService.EtiketYaz(sonHatchId, kolon.KolonHarfi, ayar);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("Hatch yeniden olu\u015Fturma hatas\u0131", ex);
            }
            return sayac;
        }

        private void SeciliKolonDegisti()
        {
            SeciliKolonIstasyonlari.Clear();
            MalzemeOzetleri.Clear();
            if (_seciliKolon != null)
            {
                foreach (var ist in _seciliKolon.Istasyonlar)
                    SeciliKolonIstasyonlari.Add(ist);
                if (_seciliKolon.KubajSonucu != null)
                    foreach (var ozet in _seciliKolon.KubajSonucu.MalzemeOzetleri)
                        MalzemeOzetleri.Add(ozet);
            }
            OnPropertiesChanged("ToplamKaziHacmi", "ToplamDolguHacmi", "NetHacim");
            SeciliIstasyon = null;
        }

        private void SeciliIstasyonDegisti()
        {
            SeciliIstasyonKatmanlari.Clear();
            if (_seciliIstasyon == null) return;
            foreach (var katman in _seciliIstasyon.KatmanAlanlari)
                SeciliIstasyonKatmanlari.Add(katman);
        }
    }

    public class YolMetrajKayitVerisi
    {
        public List<YolKolonu> Kolonlar { get; set; } = new List<YolKolonu>();
        public int KolonSayaci { get; set; }
        public DateTime KayitTarihi { get; set; }
    }
}
