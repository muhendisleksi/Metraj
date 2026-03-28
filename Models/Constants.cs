namespace Metraj.Models
{
    public static class Constants
    {
        // Layer adları
        public const string LayerUzunluk = "METRAJ-UZUNLUK";
        public const string LayerAlan = "METRAJ-ALAN";
        public const string LayerHacim = "METRAJ-HACIM";
        public const string LayerToplama = "METRAJ-TOPLAMA";
        public const string LayerEtiket = "METRAJ-ETIKET";

        // Layer renkleri (AutoCAD color index)
        public const short RenkUzunluk = 4;    // Cyan
        public const short RenkAlan = 3;        // Green
        public const short RenkHacim = 6;       // Magenta
        public const short RenkToplama = 2;     // Yellow
        public const short RenkEtiket = 7;      // White

        // Varsayılan text yükseklikleri
        public const double VarsayilanTextYuksekligi = 2.5;

        // Toleranslar
        public const double UzunlukToleransi = 0.001;
        public const double AlanToleransi = 0.0001;

        // Varsayılan ondalık sayısı
        public const int VarsayilanOndalikSayisi = 2;

        // Yol Metraj
        public const string LayerYolMetraj = "METRAJ-YOL";
        public const short RenkYolMetraj = 5;       // Blue
        public const string KatmanEslestirmeDosyaAdi = "MetrajKatmanEslestirme.json";

        // Hatch layer ve renkleri
        public const string LayerYarmaHatch = "METRAJ-YARMA";
        public const string LayerDolguHatch = "METRAJ-DOLGU";
        public const short RenkYarma = 1;           // Red
        public const short RenkDolgu = 5;           // Blue
        public const string LayerEtiketYarma = "METRAJ-YARMA-ETIKET";
        public const string LayerEtiketDolgu = "METRAJ-DOLGU-ETIKET";
        public const double EtiketYuksekligi = 1.5;

        // Hatch ayarları JSON dosyası
        public const string HatchAyarlariDosyaAdi = "MetrajHatchAyarlari.json";

        // İhale Kontrol
        public const string LayerIhaleKontrol = "METRAJ-IHALE";
        public const short RenkIhaleKontrol = 1;  // Red
        public const string ReferansKesitDosyaAdi = "MetrajReferansKesit.json";
        public const string IhaleKontrolKlasor = "IhaleKontrol";
    }
}
