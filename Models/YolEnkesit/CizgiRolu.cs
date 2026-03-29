namespace Metraj.Models.YolEnkesit
{
    public enum CizgiRolu
    {
        Tanimsiz = 0,

        // Malzemeler (kapali entity → .Area oku)
        Siyirma,
        Yarma,
        Dolgu,
        Asinma,
        Binder,
        BitumluTemel,
        Plentmiks,
        AltTemel,
        BTYerineKonan,
        BTYerineKonmayan,

        // Sinir cizgileri (acik cizgi, geometrik fallback hesap icin)
        Zemin,
        ProjeKotu,

        // Filtre / sistem
        CerceveCizgisi,
        GridCizgisi,
        Diger
    }

    public enum DogrulamaDurumu
    {
        Bekliyor,
        Onaylandi,
        Duzeltildi,
        Sorunlu
    }
}
