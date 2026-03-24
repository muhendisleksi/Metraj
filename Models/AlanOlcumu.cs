namespace Metraj.Models
{
    public class AlanOlcumu : OlcumSonucu
    {
        public double Alan { get; set; }
        public double BirimAlan { get; set; }
        public double Cevre { get; set; }
        public string NesneTipi { get; set; }
        public string KatmanAdi { get; set; }

        public AlanOlcumu()
        {
            Birim = "m\u00B2";
        }
    }
}
