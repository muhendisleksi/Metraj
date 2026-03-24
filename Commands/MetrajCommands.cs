using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using Metraj.Infrastructure;
using Metraj.Services;
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
