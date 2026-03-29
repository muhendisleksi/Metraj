using System.Collections.Generic;
using Metraj.Models.IhaleKontrol;
using Metraj.Models.YolEnkesit;

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
        ExportResult IhaleKontrolExport(IhaleKontrolRaporu rapor, string dosyaYolu);
        ExportResult EnkesitOkumaExport(List<KesitGrubu> kesitler, string dosyaYolu);
    }
}
