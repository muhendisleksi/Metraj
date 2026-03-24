using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using Metraj.Infrastructure;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services;
using Metraj.Services.Interfaces;
using Metraj.ViewModels;

[assembly: CommandClass(typeof(Metraj.Commands.MetrajCommands))]
[assembly: ExtensionApplication(typeof(Metraj.Commands.MetrajCommands))]

namespace Metraj.Commands
{
    public class MetrajCommands : IExtensionApplication
    {
        private static bool _initialized;

        public void Initialize()
        {
            try
            {
                // NuGet DLL çözümleme
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                // Logging başlat
                LoggingService.Initialize();
                LoggingService.Info("Metraj eklentisi yükleniyor...");

                // DI container başlat
                ServiceContainer.Initialize();

                _initialized = true;
                LoggingService.Info("Metraj eklentisi başarıyla yüklendi.");
            }
            catch (System.Exception ex)
            {
                LoggingService.Fatal("Metraj başlatma hatası", ex);
            }
        }

        public void Terminate()
        {
            try
            {
                ServiceContainer.Dispose();
                LoggingService.Close();
                AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            }
            catch
            {
                // Sessizce kapat
            }
        }

        [CommandMethod("METRAJ")]
        public static void ToggleMetrajPanel()
        {
            if (!_initialized)
            {
                LoggingService.Warning("Metraj henüz başlatılmadı.");
                return;
            }

            try
            {
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nMetraj Asistanı v1.0 - Panel hazırlanıyor...\n");
                // ModuleWindowManager entegrasyonu Faz 2'de eklenecek
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("METRAJ komutu hatası", ex);
            }
        }

        [CommandMethod("METRAJKAPAT")]
        public static void ClosePanel()
        {
            // Faz 2'de implement edilecek
        }

        [CommandMethod("METRAJUZUNLUK")]
        public static void QuickUzunluk()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<UzunlukViewModel>();
                vm.NesneSecVeHesaplaCommand.Execute(null);
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\nToplam Uzunluk: {vm.ToplamUzunluk:F2} m\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("METRAJUZUNLUK hatası", ex);
            }
        }

        [CommandMethod("METRAJALAN")]
        public static void QuickAlan()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<AlanViewModel>();
                vm.NesneSecVeHesaplaCommand.Execute(null);
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\nToplam Alan: {vm.ToplamAlan:F2} m\u00B2\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("METRAJALAN hatası", ex);
            }
        }

        [CommandMethod("METRAJTOPLA")]
        public static void QuickTopla()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<ToplamaViewModel>();
                vm.MetinSecVeToplaCommand.Execute(null);
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\nToplam: {vm.Toplam:F2}\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("METRAJTOPLA hatası", ex);
            }
        }

        [CommandMethod("METRAJANNOTASYON")]
        public static void YazAnnotasyon()
        {
            if (!_initialized) return;
            try
            {
                var editorService = ServiceContainer.GetRequiredService<IEditorService>();
                var annotationService = ServiceContainer.GetRequiredService<IAnnotationService>();
                var ayarlarVm = ServiceContainer.GetRequiredService<AyarlarViewModel>();

                var textResult = editorService.GetString("\nYazilacak metni girin: ");
                if (textResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

                var pointResult = editorService.GetPoint("\nYazi konumunu secin: ");
                if (pointResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

                annotationService.YaziYaz(pointResult.Value, textResult.StringResult, ayarlarVm.GetAnnotationAyarlari());
                editorService.WriteMessage("\nAnnotasyon yazildi.\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("METRAJANNOTASYON hatasi", ex);
            }
        }

        [CommandMethod("METRAJEXCEL")]
        public static void ExportExcel()
        {
            if (!_initialized) return;
            try
            {
                var uzunlukVm = ServiceContainer.GetRequiredService<UzunlukViewModel>();
                var alanVm = ServiceContainer.GetRequiredService<AlanViewModel>();
                var hacimVm = ServiceContainer.GetRequiredService<HacimViewModel>();
                var toplamaVm = ServiceContainer.GetRequiredService<ToplamaViewModel>();
                var excelService = ServiceContainer.GetRequiredService<IExcelExportService>();

                var rapor = new MetrajRaporu
                {
                    UzunlukSonuclari = new List<UzunlukOlcumu>(uzunlukVm.Sonuclar),
                    AlanSonuclari = new List<AlanOlcumu>(alanVm.Sonuclar),
                    HacimSonucu = hacimVm.Sonuc,
                    ToplamaSonuclari = new List<ToplamaOgesi>(toplamaVm.Ogeler)
                };

                var dosyaYolu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Metraj_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");

                var result = excelService.Export(rapor, dosyaYolu);
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;

                if (result.Basarili)
                    ed?.WriteMessage("\nExcel dosyasi olusturuldu: " + result.DosyaYolu + "\n");
                else
                    ed?.WriteMessage("\nExcel hatasi: " + result.HataMesaji + "\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("METRAJEXCEL hatasi", ex);
            }
        }

        [CommandMethod("METRAJTEMIZLE")]
        public static void TemizleAnnotasyonlar()
        {
            if (!_initialized) return;
            try
            {
                var annotationService = ServiceContainer.GetRequiredService<IAnnotationService>();
                annotationService.AnnotasyonlariTemizle(Constants.LayerUzunluk);
                annotationService.AnnotasyonlariTemizle(Constants.LayerAlan);
                annotationService.AnnotasyonlariTemizle(Constants.LayerHacim);
                annotationService.AnnotasyonlariTemizle(Constants.LayerToplama);
                annotationService.AnnotasyonlariTemizle(Constants.LayerEtiket);

                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nMetraj annotasyonlari temizlendi.\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("METRAJTEMIZLE hatasi", ex);
            }
        }

        [CommandMethod("METRAJAYARLAR")]
        public static void ToggleAyarlar()
        {
            // Panel aciksa Ayarlar sekmesine gec
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\nAyarlar sekmesine gecin.\n");
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var assemblyPath = Path.Combine(assemblyDir, assemblyName + ".dll");

            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            return null;
        }
    }
}
