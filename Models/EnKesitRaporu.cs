using System;
using System.Collections.Generic;

namespace Metraj.Models
{
    public class EnKesitRaporu
    {
        public string Istasyon { get; set; } = "0+000";
        public List<EnKesitAlanOlcumu> Katmanlar { get; set; } = new List<EnKesitAlanOlcumu>();
        public DateTime Tarih { get; set; } = DateTime.Now;
    }
}
