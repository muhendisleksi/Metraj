using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IAlanHesapService
    {
        List<AlanOlcumu> Hesapla(SelectionSet secim);
        List<AlanOlcumu> Hesapla(IEnumerable<ObjectId> nesneler);
        double BirimDonustur(double metrekare, BirimTipi hedefBirim);
    }
}
