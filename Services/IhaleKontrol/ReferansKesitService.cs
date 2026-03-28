using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Models.IhaleKontrol;
using Metraj.Services.IhaleKontrol.Interfaces;
using Newtonsoft.Json;

namespace Metraj.Services.IhaleKontrol
{
    public class ReferansKesitService : IReferansKesitService
    {
        private readonly IDocumentContext _documentContext;
        private readonly IEditorService _editorService;

        public ReferansKesitService(IDocumentContext documentContext, IEditorService editorService)
        {
            _documentContext = documentContext;
            _editorService = editorService;
        }

        public ReferansKesitAyarlari ReferansKesitTanimla()
        {
            var ayarlar = new ReferansKesitAyarlari
            {
                OlusturmaTarihi = DateTime.Now
            };

            _editorService.WriteMessage("\n=== REFERANS KESİT TANIMLAMA ===\n");
            _editorService.WriteMessage("Çizgileri seçin — parça parça olabilir, hepsini birden seçebilirsiniz.\n");

            // 1. Arazi çizgisi
            _editorService.WriteMessage("\n[1/5] Arazi çizgisi/çizgilerini seçin (mevcut zemin profili)...\n");
            ayarlar.AraziCizgisi = CizgiGrubuSec(CizgiRolu.Arazi, "Arazi");
            if (ayarlar.AraziCizgisi == null) { _editorService.WriteMessage("\nİptal edildi.\n"); return null; }

            // 2. Proje hattı
            _editorService.WriteMessage("\n[2/5] Proje hattını seçin (kırmızı kot / yol platformu)...\n");
            ayarlar.ProjeHatti = CizgiGrubuSec(CizgiRolu.ProjeHatti, "Proje hattı");
            if (ayarlar.ProjeHatti == null) { _editorService.WriteMessage("\nİptal edildi.\n"); return null; }

            // 3. Siyah kot (opsiyonel)
            _editorService.WriteMessage("\n[3/5] Siyah kotu seçin (alt temel tabanı). Yoksa Enter ile atlayın...\n");
            ayarlar.SiyahKot = CizgiGrubuSec(CizgiRolu.SiyahKot, "Siyah kot", opsiyonel: true);

            // 4. CL çizgisi
            _editorService.WriteMessage("\n[4/5] CL (merkez hat) çizgisini seçin...\n");
            ayarlar.CLCizgisi = CizgiGrubuSec(CizgiRolu.CL, "CL");
            if (ayarlar.CLCizgisi == null) { _editorService.WriteMessage("\nİptal edildi.\n"); return null; }

            // 5. Tabaka çizgileri (opsiyonel, çoklu grup)
            _editorService.WriteMessage("\n[5/5] Tabaka çizgilerini seçin (isteğe bağlı). Her tabakayı ayrı seçin, Enter ile bitirin.\n");
            while (true)
            {
                _editorService.WriteMessage($"  Tabaka #{ayarlar.TabakaCizgileri.Count + 1} seçin (Enter=bitti): ");
                var tabaka = CizgiGrubuSec(CizgiRolu.TabakaSiniri, "Tabaka", opsiyonel: true);
                if (tabaka == null) break;
                ayarlar.TabakaCizgileri.Add(tabaka);
                _editorService.WriteMessage($"  Tabaka eklendi ({ayarlar.TabakaCizgileri.Count} adet).\n");
            }

            // Proje adı al
            var projeResult = _editorService.GetString("\nProje adı girin (kayıt için): ", "");
            if (projeResult.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(projeResult.StringResult))
                ayarlar.ProjeAdi = projeResult.StringResult;
            else
                ayarlar.ProjeAdi = "Proje_" + DateTime.Now.ToString("yyyyMMdd");

            OzetGoster(ayarlar);

            LoggingService.Info("Referans kesit tanımlandı: {Proje}, Arazi={AraziLayer}, Proje={ProjeLayer}",
                ayarlar.ProjeAdi, ayarlar.AraziCizgisi.LayerAdi, ayarlar.ProjeHatti.LayerAdi);

            return ayarlar;
        }

        /// <summary>
        /// Kullanıcıdan bir veya birden fazla nesne seçtirir.
        /// Seçilenlerin ortak Layer + Renk bilgisini CizgiTanimi olarak döndürür.
        /// </summary>
        private CizgiTanimi CizgiGrubuSec(CizgiRolu rol, string aciklama, bool opsiyonel = false)
        {
            var selResult = _editorService.GetSelection($"  {aciklama} seçin (birden fazla seçebilirsiniz): ");

            if (selResult.Status != PromptStatus.OK || selResult.Value == null || selResult.Value.Count == 0)
                return opsiyonel ? null : null;

            using (var tr = _documentContext.BeginTransaction())
            {
                // Seçilen nesnelerin Layer + Renk bilgilerini topla
                var layerSayac = new Dictionary<string, int>();
                var renkSayac = new Dictionary<int, int>();
                var tipSayac = new Dictionary<string, int>();

                foreach (SelectedObject selObj in selResult.Value)
                {
                    if (selObj == null) continue;
                    var entity = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;

                    string layer = entity.Layer;
                    int renk = entity.ColorIndex;
                    string tip = entity.GetRXClass().DxfName;

                    if (!layerSayac.ContainsKey(layer)) layerSayac[layer] = 0;
                    layerSayac[layer]++;

                    if (!renkSayac.ContainsKey(renk)) renkSayac[renk] = 0;
                    renkSayac[renk]++;

                    if (!tipSayac.ContainsKey(tip)) tipSayac[tip] = 0;
                    tipSayac[tip]++;
                }

                tr.Commit();

                if (layerSayac.Count == 0) return null;

                // En çok kullanılan değerleri al
                string enCokLayer = layerSayac.OrderByDescending(kvp => kvp.Value).First().Key;
                int enCokRenk = renkSayac.OrderByDescending(kvp => kvp.Value).First().Key;
                string enCokTip = tipSayac.OrderByDescending(kvp => kvp.Value).First().Key;

                var tanim = new CizgiTanimi
                {
                    LayerAdi = enCokLayer,
                    RenkIndex = (short)enCokRenk,
                    NesneTipi = enCokTip,
                    Rol = rol,
                    Aciklama = aciklama
                };

                int secilenAdet = selResult.Value.Count;
                _editorService.WriteMessage(
                    $"  → {secilenAdet} nesne seçildi. Layer: {tanim.LayerAdi}, Renk: {tanim.RenkIndex}, Tip: {tanim.NesneTipi}\n");

                return tanim;
            }
        }

        private void OzetGoster(ReferansKesitAyarlari ayarlar)
        {
            _editorService.WriteMessage("\n=== REFERANS KESİT ÖZETİ ===\n");
            _editorService.WriteMessage($"Proje: {ayarlar.ProjeAdi}\n");
            _editorService.WriteMessage($"Arazi:  Layer={ayarlar.AraziCizgisi.LayerAdi}, Renk={ayarlar.AraziCizgisi.RenkIndex}\n");
            _editorService.WriteMessage($"Proje:  Layer={ayarlar.ProjeHatti.LayerAdi}, Renk={ayarlar.ProjeHatti.RenkIndex}\n");

            if (ayarlar.SiyahKot != null)
                _editorService.WriteMessage($"S.Kot:  Layer={ayarlar.SiyahKot.LayerAdi}, Renk={ayarlar.SiyahKot.RenkIndex}\n");

            _editorService.WriteMessage($"CL:     Layer={ayarlar.CLCizgisi.LayerAdi}, Renk={ayarlar.CLCizgisi.RenkIndex}\n");

            if (ayarlar.TabakaCizgileri.Count > 0)
                _editorService.WriteMessage($"Tabaka: {ayarlar.TabakaCizgileri.Count} tabaka tanımlı\n");

            _editorService.WriteMessage("===========================\n");
        }

        public void AyarlariKaydet(ReferansKesitAyarlari ayarlar)
        {
            if (ayarlar == null) return;

            string dosyaYolu = AyarDosyaYolu(ayarlar.ProjeAdi);
            ayarlar.DosyaYolu = dosyaYolu;

            try
            {
                string klasor = Path.GetDirectoryName(dosyaYolu);
                if (!Directory.Exists(klasor))
                    Directory.CreateDirectory(klasor);

                string json = JsonConvert.SerializeObject(ayarlar, Formatting.Indented);
                File.WriteAllText(dosyaYolu, json);
                LoggingService.Info("Referans kesit ayarları kaydedildi: {DosyaYolu}", dosyaYolu);
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Referans kesit kayıt hatası: {Hata}", ex);
            }
        }

        public ReferansKesitAyarlari AyarlariYukle(string projeAdi)
        {
            string dosyaYolu = AyarDosyaYolu(projeAdi);

            if (!File.Exists(dosyaYolu))
            {
                LoggingService.Warning("Referans kesit dosyası bulunamadı: {DosyaYolu}", null, dosyaYolu);
                return null;
            }

            try
            {
                string json = File.ReadAllText(dosyaYolu);
                var ayarlar = JsonConvert.DeserializeObject<ReferansKesitAyarlari>(json);
                LoggingService.Info("Referans kesit ayarları yüklendi: {Proje}", projeAdi);
                return ayarlar;
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Referans kesit yükleme hatası: {Hata}", ex);
                return null;
            }
        }

        public List<string> KayitliProfilleriListele()
        {
            var profiller = new List<string>();
            string klasor = ReferansKesitKlasoru();

            if (!Directory.Exists(klasor))
                return profiller;

            foreach (string dosya in Directory.GetFiles(klasor, "*_referans.json"))
            {
                string ad = Path.GetFileNameWithoutExtension(dosya);
                if (ad.EndsWith("_referans"))
                    ad = ad.Substring(0, ad.Length - "_referans".Length);
                profiller.Add(ad);
            }

            return profiller;
        }

        private string AyarDosyaYolu(string projeAdi)
        {
            string guvenliAd = string.Join("_",
                projeAdi.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            return Path.Combine(ReferansKesitKlasoru(), guvenliAd + "_referans.json");
        }

        private string ReferansKesitKlasoru()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "MetrajAsistani", Constants.IhaleKontrolKlasor);
        }
    }
}
