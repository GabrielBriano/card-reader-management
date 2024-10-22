using ServiceManager.Class.Enum;

namespace ServiceManager.Class.DM
{
    public class CardDM
    {
        public string? CardCode { get; set; }
        public string? Vip { get; set; }
        public decimal? Bonus { get; set; }
        public decimal? Saldo { get; set; }
        public decimal? TotalGasto { get; set; }
        public int? AcionaRele { get; set; }
        public string? Status { get; set; }

        public string? Name { get; set; }

        public DateTime? Data { get; set; }
        public DateTime? DataDes { get; set; }
        public DateTime UltimaPassagem { get; set; }
    }
}
