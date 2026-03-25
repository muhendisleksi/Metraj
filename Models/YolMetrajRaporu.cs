using System;
using System.Collections.Generic;

namespace Metraj.Models
{
    public class YolMetrajRaporu
    {
        public List<YolKesitVerisi> Kesitler { get; set; } = new List<YolKesitVerisi>();
        public YolKubajSonucu KubajSonucu { get; set; }
        public KatmanEslestirmeAyarlari EslestirmeAyarlari { get; set; }
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public string ProjeAdi { get; set; }
    }
}
