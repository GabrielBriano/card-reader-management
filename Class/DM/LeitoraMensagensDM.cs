namespace ServiceManager.Class.DM
{
    public class LeitoraMensagensDM
    {
        public string? Mensagem_Sucesso { get; set; }
        public string? Mensagem_Aguarde { get; set; }
        public string? Mensagem_Erro_Saldo { get; set; }
        public string? Mensagem_Desativada { get; set; }
        public string? Mensagem_Erro_Comunicacao { get; set; }
        public string? Mensagem_Emitir_Ticket { get; set; }
        public string? Mensagem_Emitir_Brinde { get; set; }

        public int Tempo_Alterna_Led { get; set; }
        public int Tempo_Aciona_Led { get; set; }

    }
}
