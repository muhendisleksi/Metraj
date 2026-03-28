namespace Metraj.Models.YolEnkesit
{
    public class KesitSecimOgesi
    {
        public int Index { get; set; }
        public string IstasyonMetni { get; set; }
        public string Aciklama { get; set; }
        public string Gosterim => $"Km {IstasyonMetni}{Aciklama}";
    }
}
