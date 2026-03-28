namespace Metraj.Models.YolEnkesit
{
    public class AlanHesapSonucu
    {
        public string MalzemeAdi { get; set; }
        public double Alan { get; set; }
        public CizgiRolu UstCizgiRolu { get; set; }
        public CizgiRolu AltCizgiRolu { get; set; }
        public string Aciklama { get; set; }
    }
}
