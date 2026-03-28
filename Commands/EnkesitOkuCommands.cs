using Autodesk.AutoCAD.Runtime;
using Metraj.Services;

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

        private static void EnsureManager()
        {
            if (_manager == null)
                _manager = new EnkesitOkuPaletteManager();
        }
    }
}
