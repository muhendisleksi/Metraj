using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IToplamaService
    {
        List<ToplamaOgesi> ToplaMetinleri(SelectionSet secim, string onEkFiltre, string sonEkFiltre);
        double ToplamDeger(List<ToplamaOgesi> ogeler);
    }
}
