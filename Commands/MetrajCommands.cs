using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using Metraj.Infrastructure;
using Metraj.Services;
using Metraj.ViewModels;
using Metraj.Views;

[assembly: CommandClass(typeof(Metraj.Commands.MetrajCommands))]
[assembly: ExtensionApplication(typeof(Metraj.Commands.MetrajCommands))]

namespace Metraj.Commands
{
    public class MetrajCommands : IExtensionApplication
    {
        private static bool _initialized;
        private static ModuleWindowManager _windowManager;

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

                _windowManager = new ModuleWindowManager();

                // Enkesit Oku modülü başlat
                EnkesitOkuCommands.Initialize();

                // Ribbon menü oluştur
                RibbonManager.CreateRibbon();

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
                EnkesitOkuCommands.Terminate();
                _windowManager?.Dispose();
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
                _windowManager.Toggle();
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("METRAJ komutu hatası", ex);
            }
        }

        [CommandMethod("METRAJKAPAT")]
        public static void ClosePanel()
        {
            _windowManager?.Close();
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

        [CommandMethod("YOLMETRAJ")]
        public static void ToggleYolMetraj()
        {
            if (!_initialized) return;
            try
            {
                _windowManager.Toggle();
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nYol Metraj sekmesini kullan\u0131n.\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("YOLMETRAJ komutu hatas\u0131", ex);
            }
        }

        [CommandMethod("YOLTABLO")]
        public static void YolTablo()
        {
            if (!_initialized) return;
            try { _windowManager.Toggle(); }
            catch (System.Exception ex) { LoggingService.Error("YOLTABLO hatas\u0131", ex); }
        }

        [CommandMethod("YOLKOLONEKLE")]
        public static void YolKolonEkle()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<YolMetrajViewModel>();
                vm.KolonEkleCommand.Execute(null);
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\nKolon eklendi. '\u0130stasyon Ekle' ile devam edin.\n");
            }
            catch (System.Exception ex) { LoggingService.Error("YOLKOLONEKLE hatas\u0131", ex); }
        }

        [CommandMethod("YOLISTASYONEKLE")]
        public static void YolIstasyonEkle()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<YolMetrajViewModel>();
                vm.IstasyonEkleCommand.Execute(null);
            }
            catch (System.Exception ex) { LoggingService.Error("YOLISTASYONEKLE hatas\u0131", ex); }
        }

        [CommandMethod("YOLKALEMEKLE")]
        public static void YolKalemEkle()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<YolMetrajViewModel>();
                vm.KalemEkleCommand.Execute(null);
            }
            catch (System.Exception ex) { LoggingService.Error("YOLKALEMEKLE hatas\u0131", ex); }
        }

        [CommandMethod("YOLHESAPLA")]
        public static void YolHesapla()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<YolMetrajViewModel>();
                vm.HesaplaCommand.Execute(null);
                var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
                if (vm.SeciliKolon?.KubajSonucu != null)
                    ed?.WriteMessage($"\nKaz\u0131: {vm.ToplamKaziHacmi:F2} m\u00B3, Dolgu: {vm.ToplamDolguHacmi:F2} m\u00B3, Net: {vm.NetHacim:F2} m\u00B3\n");
            }
            catch (System.Exception ex) { LoggingService.Error("YOLHESAPLA hatas\u0131", ex); }
        }

        [CommandMethod("YOLEXCEL")]
        public static void YolExcel()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<YolMetrajViewModel>();
                vm.ExcelAktarCommand.Execute(null);
            }
            catch (System.Exception ex) { LoggingService.Error("YOLEXCEL hatas\u0131", ex); }
        }

        [CommandMethod("YOLKAYDET")]
        public static void YolKaydet()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<YolMetrajViewModel>();
                vm.KaydetCommand.Execute(null);
            }
            catch (System.Exception ex) { LoggingService.Error("YOLKAYDET hatas\u0131", ex); }
        }

        [CommandMethod("YOLYUKLE")]
        public static void YolYukle()
        {
            if (!_initialized) return;
            try
            {
                var vm = ServiceContainer.GetRequiredService<YolMetrajViewModel>();
                vm.YukleCommand.Execute(null);
            }
            catch (System.Exception ex) { LoggingService.Error("YOLYUKLE hatas\u0131", ex); }
        }

        // === Modul Pencere Komutlari ===

        [CommandMethod("METRAJUZUNLUKPANEL")]
        public static void UzunlukPanel()
        {
            if (!_initialized) return;
            _windowManager.ToggleModul("Uzunluk",
                new UzunlukControl(),
                ServiceContainer.GetRequiredService<UzunlukViewModel>());
        }

        [CommandMethod("METRAJALPANEL")]
        public static void AlanPanel()
        {
            if (!_initialized) return;
            _windowManager.ToggleModul("Alan",
                new AlanControl(),
                ServiceContainer.GetRequiredService<AlanViewModel>());
        }

        [CommandMethod("METRAJTOPLAMAPANEL")]
        public static void ToplamaPanel()
        {
            if (!_initialized) return;
            _windowManager.ToggleModul("Toplama",
                new ToplamaControl(),
                ServiceContainer.GetRequiredService<ToplamaViewModel>());
        }

        [CommandMethod("YOLMETRAJPANEL")]
        public static void YolMetrajPanel()
        {
            if (!_initialized) return;
            _windowManager.ToggleModul("Yol Metraj",
                new YolMetrajControl(),
                ServiceContainer.GetRequiredService<YolMetrajViewModel>());
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
