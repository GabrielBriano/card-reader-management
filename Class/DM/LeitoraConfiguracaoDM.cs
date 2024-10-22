
namespace ServiceManager.Class.DM
{
    public class LeitoraConfiguracaoDM
    {
        public string? Code_Leitora { get; set; }
        public string? Display { get; set; }
        public string? Nome_Brinquedo { get; set; }
        public string? Ticket_Tipo { get; set; }
        public string? Display1 { get; set; }
        public string? Mensagem_Sucesso { get; set; }
        public string? Mensagem_Aguarde { get; set; }
        public string? Mensagem_Erro_Saldo { get; set; }
        public string? Mensagem_Desativada { get; set; }
        public string? Mensagem_Erro_Comunicacao { get; set; }
        public string? Mensagem_Emitir_Ticket { get; set; }
        public string? Mensagem_Emitir_Brinde { get; set; }
        public string? Display2 { get; set; }

        public int? Rele { get; set; }
        public int? ReleLei { get; set; }
        public int? Ticket_Min { get; set; }
        public int? Ticket_Max { get; set; }
        public int? Tempo_Pulso { get; set; }
        public int Tempo_Alterna_Led { get; set; }
        public int Tempo_Aciona_Led { get; set; }
        public int? Multiplica_Ticket { get; set; }
        public int? Divide_Ticket { get; set; }
        public string? Codigo_patrimonio { get; set; }
        public int? GroupId { get; set; }

        public short? parametrosId { get; set; }
        public short? Maquina_Brinde { get; set; }
        public short Cor_Led { get; set; }
        public short? Aceita_Ticket { get; set; }
        public short? Aceita_Bonus { get; set; }
        public short? Aceita_Tempo { get; set; }
        public short? Aceita_Festa { get; set; }
        public short? Aceita_Jogadas { get; set; }

        public decimal? Preco_Normal { get; set; }
        public decimal? Preco_Vip { get; set; }
        public short? Ativa { get;  set; }

        // descontos
        public short TemDesc { get; set; }
        public int? desc1 { get; set; }
        public string? PeriodoDesc1_Inicio { get; set; }

        public string? PeriodoDesc1_Fim { get; set; }
        public int? desc2 { get; set; }
        public string? PeriodoDesc2_Inicio { get; set; }
        public string? PeriodoDesc2_Fim { get; set; }

    }
}
