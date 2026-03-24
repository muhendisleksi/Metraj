using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public class ExportResult
    {
        public bool Basarili { get; set; }
        public string DosyaYolu { get; set; }
        public string HataMesaji { get; set; }
    }

    public interface IExcelExportService
    {
        ExportResult Export(MetrajRaporu rapor, string dosyaYolu);
    }
}
