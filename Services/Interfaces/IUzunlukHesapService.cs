using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IUzunlukHesapService
    {
        List<UzunlukOlcumu> Hesapla(SelectionSet secim);
        List<UzunlukOlcumu> Hesapla(IEnumerable<ObjectId> nesneler);
        Dictionary<string, List<UzunlukOlcumu>> Grupla(List<UzunlukOlcumu> sonuclar, GruplamaTipi gruplama);
    }
}
