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

    [System.Obsolete("MalzemeAdi string kullan\u0131n. \u00C7oklu malzeme sistemine ge\u00E7ildi.")]
    public enum AlanTipi
    {
        Yarma,
        Dolgu
    }

    public enum MalzemeKategorisi
    {
        Ustyapi,        // Aşınma, Binder, Bitümlü Temel
        Alttemel,       // Plentmiks, Kırmataş, Stabilize
        ToprakIsleri,   // Kazı, Dolgu, Şev
        Ozel            // Kullanıcı tanımlı (Banket, Hendek vb.)
    }
}
