namespace Metraj.Models.YolEnkesit
{
    public class KesitPenceresi
    {
        public double Genislik { get; set; }
        public double Yukseklik { get; set; }
        public double OffsetSolX { get; set; }
        public double OffsetSagX { get; set; }
        public double OffsetAltY { get; set; }
        public double OffsetUstY { get; set; }

        /// <summary>CL bazli otomatik tespitle olusturuldu mu</summary>
        public bool OtomatikTespit { get; set; }

        /// <summary>Platform genisligi (yol genisligi + margin). CL'den saga ve sola bu kadar alinir.</summary>
        public double PlatformYariGenislik { get; set; }

        /// <summary>CL bazli pencere olustur</summary>
        public static KesitPenceresi CL_Bazli(double clMinY, double clMaxY, double platformYariGenislik, double yMargin = 2.0)
        {
            double yukseklik = clMaxY - clMinY + yMargin * 2;
            double genislik = platformYariGenislik * 2;
            return new KesitPenceresi
            {
                Genislik = genislik,
                Yukseklik = yukseklik,
                OffsetSolX = platformYariGenislik,
                OffsetSagX = platformYariGenislik,
                OffsetAltY = (clMaxY - clMinY) / 2 + yMargin,
                OffsetUstY = (clMaxY - clMinY) / 2 + yMargin,
                OtomatikTespit = true,
                PlatformYariGenislik = platformYariGenislik
            };
        }
    }
}
