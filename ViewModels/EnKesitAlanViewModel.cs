using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Metraj.Infrastructure;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services;
using Metraj.Services.Interfaces;

namespace Metraj.ViewModels
{
    public class EnKesitAlanViewModel : ViewModelBase
    {
        private string _istasyon = "0+000";
        private double _toplamAlan;
        private string _durumMesaji = "Hesaplama yöntemi seçin.";

        public ObservableCollection<EnKesitAlanOlcumu> Katmanlar { get; } = new ObservableCollection<EnKesitAlanOlcumu>();

        public string Istasyon
        {
            get => _istasyon;
            set => SetProperty(ref _istasyon, value);
        }

        public double ToplamAlan
        {
            get => _toplamAlan;
            set => SetProperty(ref _toplamAlan, value);
        }

        public string DurumMesaji
        {
            get => _durumMesaji;
            set => SetProperty(ref _durumMesaji, value);
        }

        public ICommand IkiCizgiSecCommand { get; }
        public ICommand KapaliNesneSecCommand { get; }
        public ICommand NoktaTiklaSurekliCommand { get; }
        public ICommand TemizleCommand { get; }

        public EnKesitAlanViewModel()
        {
            IkiCizgiSecCommand = new RelayCommand(IkiCizgiSec);
            KapaliNesneSecCommand = new RelayCommand(KapaliNesneSec);
            NoktaTiklaSurekliCommand = new RelayCommand(NoktaTiklaSurekli);
            TemizleCommand = new RelayCommand(Temizle);
        }

        private void IkiCizgiSec()
        {
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();
                var enkesitService = ServiceContainer.GetRequiredService<IEnKesitAlanService>();

                // Üst çizgiyi seç
                var ustResult = editorService.GetEntity("\nÜst çizgiyi seçin (arazi/üst sınır): ");
                if (ustResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    DurumMesaji = "Seçim iptal edildi.";
                    return;
                }

                // Alt çizgiyi seç
                var altResult = editorService.GetEntity("\nAlt çizgiyi seçin (proje/alt sınır): ");
                if (altResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    DurumMesaji = "Seçim iptal edildi.";
                    return;
                }

                // Yarma/Dolgu tipi sor
                var tipResult = editorService.GetKeywords("\nTip seçin [Yarma/Dolgu]: ", new[] { "Yarma", "Dolgu" }, "Yarma");
                string tip = "Yarma";
                if (tipResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK && !string.IsNullOrWhiteSpace(tipResult.StringResult))
                    tip = tipResult.StringResult;

                // Katman adini sor
                var adResult = editorService.GetString("\nKatman ad\u0131n\u0131 girin: ", tip);
                string malzemeAdi = tip;
                if (adResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK && !string.IsNullOrWhiteSpace(adResult.StringResult))
                    malzemeAdi = adResult.StringResult;

                // Alan hesapla
                double alan = enkesitService.IkiCizgiArasiAlan(ustResult.ObjectId, altResult.ObjectId);

                // Layer adını al
                string katmanAdi = "";
                var docContext = ServiceContainer.GetRequiredService<IDocumentContext>();
                using (var tr = docContext.BeginTransaction())
                {
                    var entity = tr.GetObject(ustResult.ObjectId, OpenMode.ForRead) as Entity;
                    katmanAdi = entity?.Layer ?? "";
                    tr.Commit();
                }

                Katmanlar.Add(new EnKesitAlanOlcumu
                {
                    MalzemeAdi = malzemeAdi,
                    Alan = alan,
                    KatmanAdi = katmanAdi,
                    Yontem = "İki Çizgi",
                    KaynakNesneler = new System.Collections.Generic.List<ObjectId> { ustResult.ObjectId, altResult.ObjectId }
                });

                ToplamAlan = Katmanlar.Sum(k => k.Alan);
                DurumMesaji = $"{malzemeAdi}: {alan:F2} m² eklendi.";
                LoggingService.Info("En kesit alan: {Malzeme} = {Alan:F2} m² (iki çizgi)", malzemeAdi, alan);
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("İki çizgi alan hatası", ex);
            }
        }

        private void KapaliNesneSec()
        {
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();
                var enkesitService = ServiceContainer.GetRequiredService<IEnKesitAlanService>();

                var selResult = editorService.GetSelection("\nKapalı nesneleri seçin: ");
                if (selResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    DurumMesaji = "Seçim iptal edildi.";
                    return;
                }

                var docContext = ServiceContainer.GetRequiredService<IDocumentContext>();
                int eklenen = 0;

                foreach (var id in selResult.Value.GetObjectIds())
                {
                    double alan = enkesitService.KapaliNesneAlan(id);
                    if (alan <= 0.0001) continue;

                    string katmanAdi = "";
                    using (var tr = docContext.BeginTransaction())
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        katmanAdi = entity?.Layer ?? "";
                        tr.Commit();
                    }

                    Katmanlar.Add(new EnKesitAlanOlcumu
                    {
                        MalzemeAdi = katmanAdi,
                        Alan = alan,
                        KatmanAdi = katmanAdi,
                        Yontem = "Kapalı Nesne",
                        KaynakNesneler = new System.Collections.Generic.List<ObjectId> { id }
                    });
                    eklenen++;
                }

                ToplamAlan = Katmanlar.Sum(k => k.Alan);
                DurumMesaji = $"{eklenen} kapalı nesne eklendi.";
                LoggingService.Info("En kesit: {Count} kapalı nesne eklendi", eklenen);
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Kapalı nesne alan hatası", ex);
            }
        }

        private void NoktaTiklaSurekli()
        {
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();
                var enkesitService = ServiceContainer.GetRequiredService<IEnKesitAlanService>();

                editorService.WriteMessage("\nKapalı bölgelerin içine tıklayın. Çıkmak için ESC veya sağ tık.\n");

                while (true)
                {
                    var pointResult = editorService.GetPoint("\nAlan hesaplamak için bölge içine tıklayın (ESC=Çık): ", true);
                    if (pointResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        break;

                    var olcum = enkesitService.BoundaryAlanHesapla(pointResult.Value);
                    if (olcum == null)
                    {
                        editorService.WriteMessage("\nKapalı bölge bulunamadı. Farklı bir nokta deneyin.\n");
                        continue;
                    }

                    Katmanlar.Add(olcum);
                    ToplamAlan = Katmanlar.Sum(k => k.Alan);
                    editorService.WriteMessage($"\n{olcum.MalzemeAdi}: {olcum.Alan:F2} m² eklendi.\n");
                }

                DurumMesaji = $"{Katmanlar.Count} alan hesaplandı. Toplam: {ToplamAlan:F2} m²";
            }
            catch (System.Exception ex)
            {
                DurumMesaji = "Hata: " + ex.Message;
                LoggingService.Error("Sürekli tıklama hatası", ex);
            }
        }

        private void Temizle()
        {
            Katmanlar.Clear();
            ToplamAlan = 0;
            DurumMesaji = "Hesaplama yöntemi seçin.";
        }
    }
}
