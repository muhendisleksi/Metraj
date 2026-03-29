using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Metraj.Infrastructure;
using Metraj.Services;
using Metraj.Services.YolEnkesit;

[assembly: CommandClass(typeof(Metraj.Commands.EnkesitOkuCommands))]

namespace Metraj.Commands
{
    public class EnkesitOkuCommands
    {
        private static EnkesitOkuPaletteManager _manager;

        internal static void Initialize()
        {
            _manager = new EnkesitOkuPaletteManager();
        }

        internal static void Terminate()
        {
            _manager?.Dispose();
        }

        [CommandMethod("YOLENKESITOKU")]
        public static void YolEnkesitOku()
        {
            EnsureManager();
            _manager.Toggle();
        }

        [CommandMethod("YOLKALIBRE")]
        public static void YolKalibre()
        {
            EnsureManager();
            _manager.Toggle();
        }

        [CommandMethod("YOLTARA")]
        public static void YolTara()
        {
            EnsureManager();
            _manager.Toggle();
        }

        [CommandMethod("YOLDOGRULA")]
        public static void YolDogrula()
        {
            EnsureManager();
            _manager.Toggle();
        }

        /// <summary>
        /// Debug: sol tikla → en yakin kesitin hesap polygon'larini DWG'ye ciz.
        /// Birden fazla tiklayabilirsin. Escape ile cik.
        /// Once "Toplu Tara" calistirmis olmalisin (polygon verileri oradan gelir).
        /// </summary>
        [CommandMethod("METRAJDEBUG")]
        public static void MetrajDebug()
        {
            try
            {
                var veriler = KesitAlanHesapService.DebugVerileriAl();
                if (veriler == null || veriler.Count == 0)
                {
                    var doc = Application.DocumentManager.MdiActiveDocument;
                    doc?.Editor.WriteMessage("\nOnce Toplu Tara calistirin — debug verisi yok.\n");
                    return;
                }

                var kesitAdlari = new System.Collections.Generic.HashSet<string>();
                foreach (var v in veriler) kesitAdlari.Add(v.KesitAdi);

                var editor = Application.DocumentManager.MdiActiveDocument?.Editor;
                if (editor == null) return;

                editor.WriteMessage($"\nMETRAJDEBUG: {veriler.Count} polygon, {kesitAdlari.Count} kesit hazir.");
                editor.WriteMessage("\nKesit yakininda bir noktaya tiklayin (Escape = cikis):\n");

                // Eski debug cizimlerini temizle
                var svc = ServiceContainer.GetRequiredService<IKesitAlanHesapService>();
                svc.DebugKatmaniTemizle();

                while (true)
                {
                    var ppo = new PromptPointOptions("\nDebug cizmek icin nokta sec [Escape=cikis]: ");
                    ppo.AllowNone = true;
                    var sonuc = editor.GetPoint(ppo);

                    if (sonuc.Status != PromptStatus.OK) break;

                    double px = sonuc.Value.X;
                    double py = sonuc.Value.Y;

                    string enYakin = KesitAlanHesapService.EnYakinKesitBul(px, py);
                    if (enYakin == null)
                    {
                        editor.WriteMessage("\nYakin kesit bulunamadi.\n");
                        continue;
                    }

                    KesitAlanHesapService.DebugKesitCiz(enYakin);
                    editor.WriteMessage($"\n{enYakin} kesiti cizildi.\n");
                }

                editor.WriteMessage("\nMETRAJDEBUG tamamlandi.\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error($"METRAJDEBUG hatasi: {ex.Message}");
            }
        }

        /// <summary>Debug katmanini temizle.</summary>
        [CommandMethod("METRAJDEBUGTEMIZLE")]
        public static void MetrajDebugTemizle()
        {
            try
            {
                var svc = ServiceContainer.GetRequiredService<IKesitAlanHesapService>();
                svc.DebugKatmaniTemizle();

                var editor = Application.DocumentManager.MdiActiveDocument?.Editor;
                editor?.WriteMessage("\nMETRAJ_DEBUG katmani temizlendi.\n");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error($"METRAJDEBUGTEMIZLE hatasi: {ex.Message}");
            }
        }

        private static void EnsureManager()
        {
            if (_manager == null)
                _manager = new EnkesitOkuPaletteManager();
        }
    }
}
