namespace Metraj.Models.YolEnkesit
{
    public class TabloKiyasSonucu
    {
        public string MalzemeAdi { get; set; }
        public double TabloAlani { get; set; }
        public double HesaplananAlan { get; set; }
        public double Fark { get; set; }
        public double FarkYuzde { get; set; }
        public bool Uyumlu { get; set; }
    }
}
