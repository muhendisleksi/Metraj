namespace Metraj.Models.YolEnkesit
{
    public enum CizgiRolu
    {
        Tanimsiz = 0,

        // Ana referans çizgileri
        Zemin,
        SiyirmaTaban,
        ProjeKotu,
        UstyapiAltKotu,

        // Üstyapı tabakaları (yukarıdan aşağıya)
        AsinmaTaban,
        BinderTaban,
        BitumluTemelTaban,
        PlentmiksTaban,
        AltTemelTaban,
        KirmatasTaban,

        // Özel elemanlar
        HendekCizgisi,
        SevCizgisi,
        BanketCizgisi,
        EksenCizgisi,

        // Filtrelenenler
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
