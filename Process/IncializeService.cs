using MySqlConnector;
using ServiceManager.Class.DM;
using ServiceManager.Class.Enum;
using System.Collections.Generic;
using System.Data;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace ServiceManager.Process
{

    public class IncializeService
    {
        private string? pBancoLeitora = "Server=192.168.50.11;Database=leitoras;Uid=desenv;Pwd=root;";
        private string? pBancoVision = "Server=192.168.50.11;Database=visionbd;Uid=desenv;Pwd=root;";

        private MySqlConnection _connectionLeitora;
        private MySqlConnection _connectionVision;

        public IncializeService()
        {
            _connectionLeitora = new MySqlConnection(pBancoLeitora);
            _connectionVision = new MySqlConnection(pBancoLeitora);
        }

        public void StartService()
        {
            AsyncDesconto asyncDesconto = new AsyncDesconto();
            AsyncCheckConnection asyncCheckProcess = new AsyncCheckConnection();
            AsyncProcess asyncProcess = new AsyncProcess();
            if (LimpezaDaTabela()) // Método para Limpar a Tabela do banco de Leitora 
            {
                if (ContrucaoDaTabela())// Método para Construir as Leitoras no banco de dados
                {
                    ColetarDescontos();

                    asyncProcess.ServicoCheckUpdate();
                    Console.WriteLine("Serviço de Verificação de Atualizaçoes Iniciado...");

                    asyncCheckProcess.ServicoComunicacao();
                    Console.WriteLine("Serviço de Verificação de Comunicação Iniciado...");

                    asyncDesconto.ServicoDesconto();
                    Console.WriteLine("Serviço de Verificação de Desconto Iniciado...");

                    StartCheckService(); // Inicialização do Serviço de verificação
                }
            }
            else
                Console.WriteLine("Falha Critica na inicialização do serviço");
        }
        private void StartCheckService()
        {
            while (true)
            {
                try
                {
                    using (var connection = new MySqlConnection(pBancoLeitora))
                    {
                        connection.Open();

                        string query = "SELECT * FROM tb_leitora WHERE status = @status AND codigo_cartao IS NOT NULL LIMIT 1";
                        using (var command = new MySqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@status", StatusCode.W); // Evita SQL Injection

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    LeitoraDM leitora = new LeitoraDM();

                                    leitora.Code_Leitora = reader.IsDBNull(reader.GetOrdinal("code_leitora")) ? "" : reader.GetString("code_leitora");
                                    leitora.CardCode = reader.IsDBNull(reader.GetOrdinal("codigo_cartao")) ? "" : reader.GetString("codigo_cartao");
                                    leitora.Display1 = reader.IsDBNull(reader.GetOrdinal("display1")) ? "" : reader.GetString("display1");
                                    leitora.Display2 = reader.IsDBNull(reader.GetOrdinal("display2")) ? "" : reader.GetString("display2");

                                    leitora.Mensagem_Sucesso = reader.IsDBNull(reader.GetOrdinal("mensagem_sucesso")) ? "" : reader.GetString(reader.GetOrdinal("mensagem_sucesso"));
                                    leitora.Mensagem_Aguarde = reader.IsDBNull(reader.GetOrdinal("mensagem_aguarde")) ? "" : reader.GetString(reader.GetOrdinal("mensagem_aguarde"));
                                    leitora.Mensagem_Erro_Saldo = reader.IsDBNull(reader.GetOrdinal("mensagem_erro_saldo")) ? "" : reader.GetString(reader.GetOrdinal("mensagem_erro_saldo"));
                                    leitora.Mensagem_Desativada = reader.IsDBNull(reader.GetOrdinal("mensagem_desativada")) ? "" : reader.GetString(reader.GetOrdinal("mensagem_desativada"));
                                    leitora.Mensagem_Erro_Comunicacao = reader.IsDBNull(reader.GetOrdinal("mensagem_erro_comunicacao")) ? "" : reader.GetString(reader.GetOrdinal("mensagem_erro_comunicacao"));
                                    leitora.Mensagem_Emitir_Ticket = reader.IsDBNull(reader.GetOrdinal("mensagem_emitir_ticket")) ? "" : reader.GetString(reader.GetOrdinal("mensagem_emitir_ticket"));
                                    leitora.Mensagem_Emitir_Brinde = reader.IsDBNull(reader.GetOrdinal("mensagem_emitir_brinde")) ? "" : reader.GetString(reader.GetOrdinal("mensagem_emitir_brinde"));

                                    leitora.Tempo_Alterna_Led = reader.IsDBNull(reader.GetOrdinal("tempo_alterna_led")) ? 0 : reader.GetInt32("tempo_alterna_led");
                                    leitora.Tempo_Aciona_Led = reader.IsDBNull(reader.GetOrdinal("tempo_aciona_led")) ? 0 : reader.GetInt32("tempo_aciona_led");

                                    leitora.TemDesc = reader.IsDBNull(reader.GetOrdinal("temdesc")) ? (short)0 : reader.GetInt16("temdesc");

                                    DescontoDM desconto = null;
                                    if (leitora.TemDesc != 0)
                                    {
                                        desconto = new DescontoDM
                                        {
                                            desconto1 = reader.IsDBNull(reader.GetOrdinal("desc1")) ? 0 : reader.GetInt32("desc1"),
                                            desconto2 = reader.IsDBNull(reader.GetOrdinal("desc2")) ? 0 : reader.GetInt32("desc2"),
                                            desconto_inicio1 = reader.IsDBNull(reader.GetOrdinal("descinicio1")) ? "" : reader.GetString("descinicio1"),
                                            desconto_fim1 = reader.IsDBNull(reader.GetOrdinal("descfim1")) ? "" : reader.GetString("descfim1"),
                                            desconto_inicio2 = reader.IsDBNull(reader.GetOrdinal("descinicio2")) ? "" : reader.GetString("descinicio2"),
                                            desconto_fim2 = reader.IsDBNull(reader.GetOrdinal("descfim2")) ? "" : reader.GetString("descfim2")
                                        };
                                    }

                                    // Atualizar o Status para processando e arranca o Codigo do cartão
                                    if (AtualizarLeitoraAguarde(leitora.Code_Leitora))
                                    {
                                        AsyncProcess asyncProcess = new AsyncProcess();
                                        Task.Run(() => asyncProcess.Transacao(leitora, desconto));
                                    }
                                }
                            }
                        }
                    }
                    // Aguarda um intervalo antes de iniciar a próxima iteração para evitar sobrecarga do banco
                    Task.Delay(4).Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro no StartCheckService: {ex.Message}");
                }
            }
        }
        private void ColetarDescontos()
        {
            ProcessDescontos processDescontos = new ProcessDescontos();

            try
            {
                _connectionLeitora.Open();

                string query = "SELECT code_leitora, temdesc FROM tb_leitora WHERE temdesc != 0";
                List<LeitoraConfiguracaoDM> leitoraConfig = new List<LeitoraConfiguracaoDM>();

                using (MySqlCommand command = new MySqlCommand(query, _connectionLeitora))
                {
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            LeitoraConfiguracaoDM leitoraDesc = new LeitoraConfiguracaoDM();
                            leitoraDesc.Code_Leitora = reader.GetString("code_leitora");
                            leitoraDesc.TemDesc = reader.GetInt16("temdesc");
                            leitoraConfig.Add(leitoraDesc);
                        }
                    }
                }

                _connectionLeitora.Close();

                processDescontos.ProcurarDescontos(leitoraConfig);

            }
            catch (Exception ex)
            {
                _connectionLeitora.Close();
                Console.WriteLine(ex.ToString());
            }

        }
        public bool LimpezaDaTabela()
        {
            string query = "";
            bool retorno = false;
            try
            {
                _connectionLeitora.Open();
                query = "DELETE FROM tb_leitora";

                using (MySqlCommand command = new MySqlCommand(query, _connectionLeitora))
                {
                    command.ExecuteNonQuery();
                }

                retorno = true;
                Console.WriteLine("Banco limpo...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return retorno;
        }

        public bool ContrucaoDaTabela()
        {
            string query = "";
            bool retorno = false;
            int totalInsert = 0;
            try
            {

                _connectionVision = new MySqlConnection(pBancoVision);

                _connectionVision.Open();

                query = "SELECT * FROM tb_parametros_globais_leitoras";
                LeitoraMensagensDM leitoraMensagem = new LeitoraMensagensDM();
                using (MySqlCommand command = new MySqlCommand(query, _connectionVision))
                {
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            leitoraMensagem.Mensagem_Sucesso = reader.IsDBNull(reader.GetOrdinal("mensagem_sucesso")) ? (string?)"" : reader.GetString(reader.GetOrdinal("mensagem_sucesso"));
                            leitoraMensagem.Mensagem_Aguarde = reader.IsDBNull(reader.GetOrdinal("mensagem_aguarde")) ? (string?)"" : reader.GetString(reader.GetOrdinal("mensagem_aguarde"));
                            leitoraMensagem.Mensagem_Erro_Saldo = reader.IsDBNull(reader.GetOrdinal("mensagem_erro_saldo")) ? (string?)"" : reader.GetString(reader.GetOrdinal("mensagem_erro_saldo"));
                            leitoraMensagem.Mensagem_Desativada = reader.IsDBNull(reader.GetOrdinal("mensagem_desativada")) ? (string?)"" : reader.GetString(reader.GetOrdinal("mensagem_desativada"));
                            leitoraMensagem.Mensagem_Erro_Comunicacao = reader.IsDBNull(reader.GetOrdinal("mensagem_erro_comunicacao")) ? (string?)"" : reader.GetString(reader.GetOrdinal("mensagem_erro_comunicacao"));
                            leitoraMensagem.Mensagem_Emitir_Ticket = reader.IsDBNull(reader.GetOrdinal("mensagem_emitir_ticket")) ? (string?)"" : reader.GetString(reader.GetOrdinal("mensagem_emitir_ticket"));
                            leitoraMensagem.Mensagem_Emitir_Brinde = reader.IsDBNull(reader.GetOrdinal("mensagem_emitir_brinde")) ? (string?)"" : reader.GetString(reader.GetOrdinal("mensagem_emitir_brinde"));

                            leitoraMensagem.Tempo_Alterna_Led = reader.GetInt32("tempo_alterna_led");
                            leitoraMensagem.Tempo_Aciona_Led = reader.GetInt32("tempo_aciona_led");
                        }
                    }
                }

                query =
                    "SELECT * FROM tb_machine_settings " +
                    "LEFT JOIN tb_parametros_maquinas ON " +
                    "tb_machine_settings.parametrosid = tb_parametros_maquinas.id";

                using (MySqlCommand command = new MySqlCommand(query, _connectionVision))
                {
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            LeitoraConfiguracaoDM leitoraConfig = new LeitoraConfiguracaoDM();
                            leitoraConfig.Code_Leitora = reader.GetString("code_leitora");
                            leitoraConfig.Display = reader.GetString("display1");
                            leitoraConfig.Ticket_Tipo = reader.GetString("tipo_ticket");
                            leitoraConfig.Nome_Brinquedo = reader.GetString("nome_brinquedo");

                            // Pode ser Null
                            leitoraConfig.Mensagem_Sucesso = leitoraMensagem.Mensagem_Sucesso;
                            if (!reader.IsDBNull(reader.GetOrdinal("mensagem_sucesso")))
                            {
                                leitoraConfig.Mensagem_Sucesso = reader.GetString("Mensagem_sucesso");
                            }

                            leitoraConfig.Mensagem_Aguarde = leitoraMensagem.Mensagem_Aguarde;
                            if (!reader.IsDBNull(reader.GetOrdinal("mensagem_aguarde")))
                            {
                                leitoraConfig.Mensagem_Aguarde = reader.GetString("mensagem2");
                            }

                            leitoraConfig.Mensagem_Erro_Saldo = leitoraMensagem.Mensagem_Erro_Saldo;
                            if (!reader.IsDBNull(reader.GetOrdinal("mensagem_erro_saldo")))
                            {
                                leitoraConfig.Mensagem_Erro_Saldo = reader.GetString("mensagem_erro_saldo");
                            }

                            leitoraConfig.Mensagem_Desativada = leitoraMensagem.Mensagem_Desativada;
                            if (!reader.IsDBNull(reader.GetOrdinal("mensagem_desativada")))
                            {
                                leitoraConfig.Mensagem_Desativada = reader.GetString("mensagem_desativada");
                            }

                            leitoraConfig.Mensagem_Erro_Comunicacao = leitoraMensagem.Mensagem_Erro_Comunicacao;
                            if (!reader.IsDBNull(reader.GetOrdinal("mensagem_erro_comunicacao")))
                            {
                                leitoraConfig.Mensagem_Erro_Comunicacao = reader.GetString("mensagem_erro_comunicacao");
                            }

                            leitoraConfig.Mensagem_Emitir_Ticket = leitoraMensagem.Mensagem_Emitir_Ticket;
                            if (!reader.IsDBNull(reader.GetOrdinal("mensagem_emitir_ticket")))
                            {
                                leitoraConfig.Mensagem_Emitir_Ticket = reader.GetString("mensagem_emitir_ticket");
                            }

                            leitoraConfig.Mensagem_Emitir_Brinde = leitoraMensagem.Mensagem_Emitir_Brinde;
                            if (!reader.IsDBNull(reader.GetOrdinal("mensagem_emitir_brinde")))
                            {
                                leitoraConfig.Mensagem_Emitir_Brinde = reader.GetString("mensagem_emitir_brinde");
                            }

                            leitoraConfig.Ticket_Min = reader.IsDBNull(reader.GetOrdinal("ticket_minimo")) ? 0 : reader.GetInt32(reader.GetOrdinal("ticket_minimo"));
                            leitoraConfig.Ticket_Max = reader.IsDBNull(reader.GetOrdinal("ticket_maximo")) ? 0 : reader.GetInt32(reader.GetOrdinal("ticket_maximo"));
                            leitoraConfig.Tempo_Pulso = reader.IsDBNull(reader.GetOrdinal("tempo_pulso")) ? null : reader.GetInt32(reader.GetOrdinal("tempo_pulso"));
                            leitoraConfig.Multiplica_Ticket = reader.IsDBNull(reader.GetOrdinal("multiplica_ticket")) ? null : reader.GetInt32(reader.GetOrdinal("multiplica_ticket"));
                            leitoraConfig.Divide_Ticket = reader.IsDBNull(reader.GetOrdinal("divide_ticket")) ? null : reader.GetInt32(reader.GetOrdinal("divide_ticket"));
                            leitoraConfig.Codigo_patrimonio = reader.IsDBNull(reader.GetOrdinal("codigo_patrimonio")) ? (string?)null : reader.GetString(reader.GetOrdinal("codigo_patrimonio"));
                            leitoraConfig.GroupId = reader.IsDBNull(reader.GetOrdinal("groupid")) ? null : reader.GetInt32(reader.GetOrdinal("groupid"));

                            leitoraConfig.Rele = reader.GetInt32("aciona_rele");

                            leitoraConfig.Tempo_Alterna_Led = leitoraMensagem.Tempo_Alterna_Led;
                            if (!reader.IsDBNull(reader.GetOrdinal("tempo_alterna_led")))
                            {
                                leitoraConfig.Tempo_Alterna_Led = reader.GetInt32("tempo_alterna_led");
                            }

                            leitoraConfig.Tempo_Aciona_Led = leitoraMensagem.Tempo_Aciona_Led;
                            if (!reader.IsDBNull(reader.GetOrdinal("tempo_aciona_led")))
                            {
                                leitoraConfig.Tempo_Aciona_Led = reader.GetInt32("tempo_aciona_led");
                            }

                            leitoraConfig.Preco_Normal = reader.GetDecimal("preco_normal");
                            leitoraConfig.Preco_Vip = reader.GetDecimal("preco_vip");

                            leitoraConfig.Cor_Led = reader.GetInt16("cor_led");
                            leitoraConfig.Aceita_Ticket = reader.GetInt16("aceita_ticket");
                            leitoraConfig.Maquina_Brinde = reader.GetInt16("maquina_brinde");
                            leitoraConfig.Aceita_Bonus = reader.GetInt16("aceita_bonus");
                            leitoraConfig.Aceita_Tempo = reader.GetInt16("aceita_tempo");
                            leitoraConfig.Aceita_Festa = reader.GetInt16("aceita_festa");
                            leitoraConfig.Aceita_Jogadas = reader.GetInt16("aceita_jogadas");

                            leitoraConfig.Multiplica_Ticket = reader.IsDBNull(reader.GetOrdinal("multiplica_ticket")) ? 0 : reader.GetInt16(reader.GetOrdinal("multiplica_ticket"));
                            leitoraConfig.Divide_Ticket = reader.IsDBNull(reader.GetOrdinal("divide_ticket")) ? 0 : reader.GetInt16(reader.GetOrdinal("divide_ticket"));

                            leitoraConfig.TemDesc = ((short)(reader.IsDBNull(reader.GetOrdinal("discountid")) ? 0 : reader.GetInt16(reader.GetOrdinal("discountid"))));
                            leitoraConfig.Ativa = reader.GetInt16("ativa");
                            // Inserir o registro na tabela de destino
                            InserirMaquina(leitoraConfig);
                            totalInsert++;
                        }
                        retorno = true;

                        Console.WriteLine("Total de maquinas: " + totalInsert);

                    }
                }

                _connectionVision.Close();
            }
            catch
            {
                _connectionVision.Close();
            }
            return retorno;
        }

        public void InserirMaquina(LeitoraConfiguracaoDM leitoraConfig)
        {
            try
            {
                _connectionLeitora = new MySqlConnection(pBancoLeitora);
                _connectionLeitora.Open();

                int corLed = leitoraConfig.Cor_Led * 10 + leitoraConfig.Tempo_Alterna_Led;

                // Defina os valores permitidos para tipo_ticket
                string[] validTicketTypes = { "M", "N", "P", "V" }; // Adicione todos os valores permitidos aqui

                // Verifique se o valor de Ticket_Tipo é válido
                if (leitoraConfig.Ticket_Tipo.Length == 1 && !Array.Exists(validTicketTypes, element => element == leitoraConfig.Ticket_Tipo[0].ToString()))
                {
                    Console.WriteLine("Valor de tipo_ticket inválido: " + leitoraConfig.Ticket_Tipo);
                    _connectionLeitora.Close();
                    return;
                }

                string query = @"
                INSERT INTO tb_leitora (
                    code_leitora, display1, display2, led_base, hora, ticket_min, 
                    ticket_max, tipo_ticket, checkleitora, aceita_ticket, mensagem_sucesso, 
                    mensagem_aguarde, mensagem_erro_saldo, mensagem_desativada, 
                    mensagem_erro_comunicacao, mensagem_emitir_ticket, mensagem_emitir_brinde, 
                    tempo_alterna_led, tempo_aciona_led, maquina_brinde, aceita_bonus, 
                    aceita_tempo, aceita_jogadas, multiplica_ticket, divide_ticket, temdesc, ativa, tempo_Pulso
                ) VALUES (
                    @code_leitora, @display1, @display2, @led_base, NOW(), @ticket_min, 
                    @ticket_max, @tipo_ticket, @checkleitora, @aceita_ticket, @mensagem_sucesso, 
                    @mensagem_aguarde, @mensagem_erro_saldo, @mensagem_desativada, 
                    @mensagem_erro_comunicacao, @mensagem_emitir_ticket, @mensagem_emitir_brinde, 
                    @tempo_alterna_led, @tempo_aciona_led, @maquina_brinde, @aceita_bonus, 
                    @aceita_tempo, @aceita_jogadas, @multiplica_ticket, @divide_ticket, @temdesc, @ativa, @tempo_Pulso
                )";

                using (MySqlCommand command = new MySqlCommand(query, _connectionLeitora))
                {
                    command.Parameters.AddWithValue("@code_leitora", leitoraConfig.Code_Leitora);
                    command.Parameters.AddWithValue("@display1", leitoraConfig.Display);
                    command.Parameters.AddWithValue("@display2", "R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip);
                    command.Parameters.AddWithValue("@led_base", corLed);
                    command.Parameters.AddWithValue("@ticket_min", leitoraConfig.Ticket_Min);
                    command.Parameters.AddWithValue("@ticket_max", leitoraConfig.Ticket_Max);
                    command.Parameters.AddWithValue("@tipo_ticket", leitoraConfig.Ticket_Tipo); // valor char
                    command.Parameters.AddWithValue("@checkleitora", 1);
                    command.Parameters.AddWithValue("@aceita_ticket", leitoraConfig.Aceita_Ticket);
                    command.Parameters.AddWithValue("@mensagem_sucesso", leitoraConfig.Mensagem_Sucesso);
                    command.Parameters.AddWithValue("@mensagem_aguarde", leitoraConfig.Mensagem_Aguarde);
                    command.Parameters.AddWithValue("@mensagem_erro_saldo", leitoraConfig.Mensagem_Erro_Saldo);
                    command.Parameters.AddWithValue("@mensagem_desativada", leitoraConfig.Mensagem_Desativada);
                    command.Parameters.AddWithValue("@mensagem_erro_comunicacao", leitoraConfig.Mensagem_Erro_Comunicacao);
                    command.Parameters.AddWithValue("@mensagem_emitir_ticket", leitoraConfig.Mensagem_Emitir_Ticket);
                    command.Parameters.AddWithValue("@mensagem_emitir_brinde", leitoraConfig.Mensagem_Emitir_Brinde);
                    command.Parameters.AddWithValue("@tempo_alterna_led", leitoraConfig.Tempo_Alterna_Led);
                    command.Parameters.AddWithValue("@tempo_aciona_led", leitoraConfig.Tempo_Aciona_Led);
                    command.Parameters.AddWithValue("@maquina_brinde", leitoraConfig.Maquina_Brinde);
                    command.Parameters.AddWithValue("@aceita_bonus", leitoraConfig.Aceita_Bonus);
                    command.Parameters.AddWithValue("@aceita_tempo", leitoraConfig.Aceita_Tempo);
                    command.Parameters.AddWithValue("@aceita_jogadas", leitoraConfig.Aceita_Jogadas);
                    command.Parameters.AddWithValue("@multiplica_ticket", leitoraConfig.Multiplica_Ticket);
                    command.Parameters.AddWithValue("@divide_ticket", leitoraConfig.Divide_Ticket);
                    command.Parameters.AddWithValue("@temdesc", leitoraConfig.TemDesc);
                    command.Parameters.AddWithValue("@ativa", leitoraConfig.Ativa);
                    command.Parameters.AddWithValue("@tempo_Pulso", leitoraConfig.Tempo_Pulso);

                    command.ExecuteNonQuery();
                }

                _connectionLeitora.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                _connectionLeitora.Close();
            }
        }

        public bool AtualizaColAtiva()
        {
            string query = "";
            bool retorno = false;
            try
            {
                using (var connectionVision = new MySqlConnection(pBancoVision))
                {
                    connectionVision.Open();

                    query = "SELECT code_leitora, ativa, display1, cor_led, tempo_alterna_led, preco_normal, preco_vip FROM tb_machine_settings";
                    var leitoraConfigs = new Dictionary<string, (string ativaValue, string display1, int corLed, int tempoAlternaLed, decimal precoNormal, decimal precoVip)>(); // Dicionário para mapear code_leitora para os valores

                    using (var commandVision = new MySqlCommand(query, connectionVision))
                    {
                        using (var reader = commandVision.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string codeLeitora = reader.GetString("code_leitora");
                                bool bitValue = reader.GetBoolean("ativa");
                                string ativaValue = bitValue ? "1" : "0"; 
                                string display1 = reader.GetString("display1");
                                int corLed = reader.GetInt16("cor_led");
                                int tempoAlternaLed = reader.GetInt16("tempo_alterna_led");
                                decimal precoNormal = reader.GetDecimal("preco_normal");
                                decimal precoVip = reader.GetDecimal("preco_vip");

                                leitoraConfigs[codeLeitora] = (ativaValue, display1, corLed, tempoAlternaLed, precoNormal, precoVip);
                            }
                        }
                    }
                    System.Threading.Thread.Sleep(10000);
                    using (var connectionLeitora = new MySqlConnection(pBancoLeitora))
                    {
                        connectionLeitora.Open();

                        foreach (var config in leitoraConfigs)
                        {
                            query = "SELECT led_base FROM tb_leitora WHERE code_leitora = @code_leitora";
                            uint ledBaseAtual = 0; 

                            using (var commandLeitora = new MySqlCommand(query, connectionLeitora))
                            {
                                commandLeitora.Parameters.AddWithValue("@code_leitora", config.Key);

                                using (var reader = commandLeitora.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        ledBaseAtual = reader.GetUInt32("led_base"); 
                                    }
                                }
                            }

                            // Converter led_base para string para manipular os dígitos
                            string ledBaseStr = ledBaseAtual.ToString().PadLeft(2, '0'); // Garantir ao menos 2 dígitos

                            // Verificar se tempo_alterna_led tem pelo menos dois dígitos
                            string tempoAlternaLedStr = config.Value.tempoAlternaLed.ToString().PadLeft(2, '0');
                            char segundoDigitoTempoAlternaLed = tempoAlternaLedStr[1]; // Pegar o segundo dígito de tempo_alterna_led

                            // Verificar se led_base tem pelo menos dois dígitos
                            if (ledBaseStr.Length >= 2)
                            {
                                // Substituir o primeiro dígito de led_base por cor_led e o segundo pelo segundo dígito de tempo_alterna_led
                                string novoLedBaseStr = config.Value.corLed.ToString() + segundoDigitoTempoAlternaLed + ledBaseStr.Substring(2);

                                // Converter o novo led_base de volta para número (UInt32)
                                uint novoLedBase = uint.Parse(novoLedBaseStr);

                                // Criar a string para display2 usando os valores de preco_normal e preco_vip
                                string display2 = $"R${config.Value.precoNormal} VIP{config.Value.precoVip}";

                                // Atualizar o registro em tb_leitora com o novo led_base e display2
                                query = "UPDATE tb_leitora SET ativa = @ativa, display1 = @display1, led_base = @led_base, display2 = @display2 WHERE code_leitora = @code_leitora";

                                using (var commandLeitoraUpdate = new MySqlCommand(query, connectionLeitora))
                                {
                                    commandLeitoraUpdate.Parameters.AddWithValue("@ativa", config.Value.ativaValue);
                                    commandLeitoraUpdate.Parameters.AddWithValue("@display1", config.Value.display1);
                                    commandLeitoraUpdate.Parameters.AddWithValue("@led_base", novoLedBase); // Usando o novo valor de led_base
                                    commandLeitoraUpdate.Parameters.AddWithValue("@display2", display2); // Usando a string montada para display2
                                    commandLeitoraUpdate.Parameters.AddWithValue("@code_leitora", config.Key);

                                    commandLeitoraUpdate.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
                retorno = true; // Indica que a operação foi bem-sucedida
            }
            catch (Exception ex)
            {
                // Tratar exceções se necessário
                Console.WriteLine("Erro: " + ex.Message);
            }
            return retorno; // Retorna o resultado da operação
        }




        public async Task LoopAtiva()
        {
            while (true)
            {
                bool sucesso = AtualizaColAtiva();  

                await Task.Delay(1000);  
            }
        }


        private void GerarLog(string LogMsg)
        {
            string query = "";

            try
            {
                using (var connection = new MySqlConnection(pBancoLeitora))
                {

                    connection.Open();

                    query = "INSERT INTO tb_logs (log) VALUE ('" + LogMsg + "')";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                }

            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }
        }

        private bool AtualizarLeitoraAguarde(string? id)
        {
            bool retorno = false;

            string query = "";

            try
            {
                _connectionLeitora = new MySqlConnection(pBancoLeitora);
                _connectionLeitora.Open();

                query = "UPDATE tb_leitora SET STATUS = '" + StatusCode.P + "', codigo_cartao = NULL WHERE code_leitora = '" + id + "'";

                using (MySqlCommand command = new MySqlCommand(query, _connectionLeitora))
                {
                    command.ExecuteNonQuery();
                }

                retorno = true;
                _connectionLeitora.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
                _connectionLeitora.Close();
            }
            return retorno;
        }
    }
}