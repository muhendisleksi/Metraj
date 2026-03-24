namespace Metraj.Models
{
    public class MetrajAyarlari
    {
        public int OndalikSayisi { get; set; } = 2;
        public BirimTipi AlanBirimi { get; set; } = BirimTipi.Metrekare;
        public SayiFormati SayiFormati { get; set; } = SayiFormati.Virgul;
        public GruplamaTipi VarsayilanGruplama { get; set; } = GruplamaTipi.Yok;
    }
}
