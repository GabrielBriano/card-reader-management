namespace ServiceManager.Class.Enum
{
    public enum StatusCode
    {
        W, // W - wait: esperando processamento
        A, // A - active: Ativa e aguardando ação
        P, // P - Process: Processando 
        E, // E - Error: aconteceu um Erro
        S  // S - Service: cartõ de serviço
    }
}
