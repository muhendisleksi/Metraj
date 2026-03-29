using System;
using System.Collections.Generic;

namespace Metraj.Models.YolEnkesit
{
    public class RolEslestirmeKurali
    {
        public CizgiRolu Rol { get; set; }
        public string LayerPattern { get; set; }
        public short? RenkIndex { get; set; }
        public double? BagintiliYPozisyonu { get; set; }
    }

    public class LayerRenkEsleme
    {
        public string LayerAdi { get; set; }
        public short RenkIndex { get; set; }
        public CizgiRolu Rol { get; set; }
    }

    public class ReferansKesitSablonu
    {
        public List<RolEslestirmeKurali> Kurallar { get; set; } = new List<RolEslestirmeKurali>();
        public List<LayerRenkEsleme> LayerRenkEslemeleri { get; set; } = new List<LayerRenkEsleme>();
        public KesitPenceresi Pencere { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string ProjeAdi { get; set; }
    }
}
