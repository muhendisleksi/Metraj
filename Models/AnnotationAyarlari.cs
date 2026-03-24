namespace Metraj.Models
{
    public class AnnotationAyarlari
    {
        public double TextYuksekligi { get; set; } = 2.5;
        public string KatmanAdi { get; set; } = Constants.LayerEtiket;
        public string Format { get; set; } = "{0:F2}";
        public bool LeaderGoster { get; set; } = false;
        public double LeaderUzunlugu { get; set; } = 5.0;
        public string TextStilAdi { get; set; } = "Standard";
    }
}
