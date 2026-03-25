using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IYolKesitService
    {
        YolKesitVerisi TekKesitOku();
        double IstasyonParse(string metin);
    }
}
