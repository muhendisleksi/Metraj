namespace Metraj.Models
{
    public enum OlcumTipi
    {
        Uzunluk,
        Alan,
        Hacim,
        Toplama
    }

    public enum GruplamaTipi
    {
        Yok,
        Katman,
        Renk,
        NesneTipi
    }

    public enum BirimTipi
    {
        Metre,
        Metrekare,
        Hektar,
        Donum,
        Metrekup
    }

    public enum HacimMetodu
    {
        OrtalamaAlan,
        Prismoidal
    }

    public enum SayiFormati
    {
        Nokta,
        Virgul,
        Otomatik
    }

    public enum MalzemeKategorisi
    {
        Ustyapi,        // Aşınma, Binder, Bitümlü Temel
        Alttemel,       // Plentmiks, Kırmataş, Stabilize
        ToprakIsleri,   // Kazı, Dolgu, Şev
        Ozel            // Kullanıcı tanımlı (Banket, Hendek vb.)
    }
}
