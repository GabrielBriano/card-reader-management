using Microsoft.Extensions.Logging;
using MySqlConnector;
using Org.BouncyCastle.Asn1.Ocsp;
using ServiceManager.Class.DM;
using ServiceManager.Class.Enum;
using System;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Microsoft.Extensions.Logging;
using System.Reflection.PortableExecutable;

namespace ServiceManager.Process
{


    public class AsyncProcess
    {
        private string? pDbLeitoras = "Server=192.168.50.11;Database=leitoras;Uid=desenv;Pwd=root;";
        private string? pDbVision = "Server=192.168.50.11;Database=visionbd;Uid=desenv;Pwd=root;";

        private int selectColor = 0;
        private decimal vipValue = 0;
        private DateTime dateParty;
        private DateTime startHour;
        private DateTime endHour;
        private DateTime? lastCardReadTime = null;

        bool actionTaken = false;
        
        decimal? remBonus = 0;
        decimal vipVl = 0;

        string crd = "";
        string ms1 = "";
        string displayMessage = "";

        public async Task Transacao(LeitoraDM leitora, DescontoDM desconto, int maxTentativas = 1, int tentativaAtual = 1)
        {

            DateTime nwTime = DateTime.Now;
            string dataHora = nwTime.ToString("dd/MM/yyyy HH:mm:ss");
            string query = "";
            string msgDisplay = "";
            string msgDisplay2 = "";
            bool valid1 = false;
            bool valid2 = false;
            int timer = 1;

            CardDM card = new CardDM();
            LeitoraConfiguracaoDM leitoraConfig = new LeitoraConfiguracaoDM();

            try
            {
                query =
                    "SELECT * FROM tb_machine_settings " +
                    "LEFT JOIN tb_parametros_maquinas ON " +
                    "tb_machine_settings.parametrosid = tb_parametros_maquinas.id " +
                    "WHERE code_leitora = '" + leitora.Code_Leitora + "'";

                using (var connection = new MySqlConnection(pDbVision))
                {
                    connection.Open();
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                leitoraConfig.Code_Leitora = reader.GetString("code_leitora");
                                leitoraConfig.Display = reader.GetString("display1");

                                leitoraConfig.Ticket_Min = reader.IsDBNull(reader.GetOrdinal("ticket_minimo")) ? (int?)0 : reader.GetInt32(reader.GetOrdinal("ticket_minimo"));
                                leitoraConfig.Ticket_Max = reader.IsDBNull(reader.GetOrdinal("ticket_maximo")) ? (int?)0 : reader.GetInt32(reader.GetOrdinal("ticket_maximo"));
                                leitoraConfig.Tempo_Pulso = reader.IsDBNull(reader.GetOrdinal("tempo_pulso")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("tempo_pulso"));
                                leitoraConfig.Multiplica_Ticket = reader.IsDBNull(reader.GetOrdinal("multiplica_ticket")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("multiplica_ticket"));
                                leitoraConfig.Divide_Ticket = reader.IsDBNull(reader.GetOrdinal("divide_ticket")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("divide_ticket"));
                                leitoraConfig.Codigo_patrimonio = reader.IsDBNull(reader.GetOrdinal("codigo_patrimonio")) ? (string?)null : reader.GetString(reader.GetOrdinal("codigo_patrimonio"));
                                leitoraConfig.GroupId = reader.IsDBNull(reader.GetOrdinal("groupid")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("groupid"));
                                leitoraConfig.Rele = reader.GetInt32("aciona_rele");

                                leitoraConfig.Preco_Normal = reader.GetDecimal("preco_normal");
                                leitoraConfig.Preco_Vip = reader.GetDecimal("preco_vip");

                                decimal precoNormalDecimal = reader.GetDecimal("preco_normal");
                                decimal precoVipDecimal = reader.GetDecimal("preco_vip");
                                string precoNormalStr = precoNormalDecimal.ToString("0.0");
                                string precoVipStr = precoVipDecimal.ToString("0.0");

                                int digitosPrecoNormal = precoNormalStr.Split('.')[0].Length;
                                int digitosPrecoVip = precoVipStr.Split('.')[0].Length;

                                leitoraConfig.Cor_Led = reader.GetInt16("cor_led");
                                selectColor = leitoraConfig.Cor_Led;
                                leitoraConfig.Aceita_Ticket = reader.GetInt16("aceita_ticket");
                                leitoraConfig.Maquina_Brinde = reader.GetInt16("maquina_brinde");
                                leitoraConfig.Aceita_Bonus = reader.GetInt16("aceita_bonus");
                                leitoraConfig.Aceita_Tempo = reader.GetInt16("aceita_tempo");
                                leitoraConfig.Aceita_Festa = reader.GetInt16("aceita_festa");
                                leitoraConfig.Aceita_Jogadas = reader.GetInt16("aceita_jogadas");

                                leitoraConfig.Display1 = reader.GetString("display1");
                                if (digitosPrecoNormal == 1 && digitosPrecoVip == 1)
                                {
                                    leitoraConfig.Display2 = " R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip;
                                }
                                else if (digitosPrecoNormal == 2 && digitosPrecoVip == 1)
                                {
                                    leitoraConfig.Display2 = "R$" + leitoraConfig.Preco_Normal + "  VIP" + leitoraConfig.Preco_Vip;
                                }
                                else
                                {
                                    leitoraConfig.Display2 = "R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip;
                                }


                                valid1 = true;
                            }
                            else
                            {
                                msgDisplay = "Erro na leitora";
                            }
                        }
                    }

                    query = "SELECT vip, bonus, saldo, status, date_active, date_desactive, total_gasto, name FROM tb_card WHERE NUMBER = '" + leitora.CardCode + "'";
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                card.CardCode = leitora.CardCode;
                                card.Vip = reader.GetString("vip");
                                card.Bonus = reader.GetDecimal("bonus");
                                card.Saldo = reader.GetDecimal("saldo");
                                card.Status = reader.GetString("status");
                                card.Data = reader.IsDBNull(reader.GetOrdinal("date_active")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("date_active"));
                                card.DataDes = reader.IsDBNull(reader.GetOrdinal("date_desactive")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("date_desactive"));

                                card.TotalGasto = reader.GetDecimal("total_gasto");
                                card.Name = reader.IsDBNull(reader.GetOrdinal("name")) ? (String?)null : reader.GetString(reader.GetOrdinal("name"));
                                TratarCartao(leitoraConfig, card, leitora, desconto);
                            }
                            else
                            {
                                if (tentativaAtual < maxTentativas)
                                {
                                    Console.WriteLine(dataHora + " - " + "Cartão não encontrado. Tentativa " + tentativaAtual + " de " + maxTentativas);
                                    await Transacao(leitora, desconto, maxTentativas, tentativaAtual + 1);
                                }
                                else
                                {
                                    Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | " + " Cartão " + leitora.CardCode + " - ");
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.Write("Cartão não encontrado\n");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    TratarCartao(leitoraConfig, card, leitora, desconto);
                                }
                                return;
                            }
                        }
                    }
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task<decimal> ObterVipValueAsync()
        {
            string query = "SELECT vip_value FROM tb_config LIMIT 1";
            decimal vipVl = 0;

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal("vip_value")))
                                {
                                    vipVl = reader.GetDecimal(reader.GetOrdinal("vip_value"));

                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter vip_value: {ex.Message}");
            }

            return vipVl;
        }

        private async Task<bool> ObterDataFesta()
        {
            string query = "SELECT party_date, start_hour, end_hour, display_message FROM tb_party";
            bool actionTaken = false;
            DateTime dataAtual = DateTime.Now;

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                DateTime dateParty = reader.GetDateTime(reader.GetOrdinal("party_date"));
                                DateTime startHour = reader.GetDateTime(reader.GetOrdinal("start_hour"));
                                DateTime endHour = reader.GetDateTime(reader.GetOrdinal("end_hour"));
                                displayMessage = reader.IsDBNull(reader.GetOrdinal("display_message")) ? (String?)null : reader.GetString(reader.GetOrdinal("display_message"));



                                if (dateParty.Date == dataAtual.Date &&
                                    DateTime.Now >= startHour && DateTime.Now <= endHour)
                                {
                                    actionTaken = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter as datas da festa: {ex.Message}");
            }

            return actionTaken;
        }


        private async Task AtualizaVip(LeitoraConfiguracaoDM leitoraConfiguracao, LeitoraDM leitora)
        {
            string query = "UPDATE tb_card SET vip = 'A' WHERE number = @number";

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@number", leitora.CardCode);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao atualizar VIP: {ex.Message}");
            }
        }


        // Metodo para Validar o Cartão e executar a leitura e descontos do cartão
        private async void TratarCartao(LeitoraConfiguracaoDM leitoraConfiguracao, CardDM card, LeitoraDM leitora, DescontoDM desconto)
        {
            bool SemSaldo = false;
            bool isVip = false;
            bool temDesc = false;

            int corLed;
            double descontoAgora = 0.00;

            string msgDisplay;
            string msgDisplay2;
            string log = "";

            decimal? precoapagar;
            decimal? cardSaldo;
            decimal? soma;

            DateTime agora = DateTime.Now;
            string dataHora = agora.ToString("dd/MM/yyyy HH:mm:ss");

            // logico para aplica os desconto
            if (!string.IsNullOrEmpty(desconto.desconto_inicio1) &&
                !string.IsNullOrEmpty(desconto.desconto_fim1) &&
                !string.IsNullOrEmpty(desconto.desconto_inicio2) &&
                !string.IsNullOrEmpty(desconto.desconto_fim2))
            {
                TimeOnly horaInicio1 = TimeOnly.Parse(desconto.desconto_inicio1);
                TimeOnly horaFim1 = TimeOnly.Parse(desconto.desconto_fim1);

                TimeOnly horaInicio2 = TimeOnly.Parse(desconto.desconto_inicio2);
                TimeOnly horaFim2 = TimeOnly.Parse(desconto.desconto_fim2);

                TimeOnly horaAgora = TimeOnly.Parse(DateTime.Now.ToString("HH:mm:ss"));

                if (horaAgora >= horaInicio1 && horaAgora <= horaFim1)
                {
                    descontoAgora = (double)desconto.desconto1 / 10;
                    temDesc = true;
                }
                else if (horaAgora >= horaInicio2 && horaAgora <= horaFim2)
                {
                    descontoAgora = (double)desconto.desconto2 / 10;
                    temDesc = true;
                }
            }

            await Task.Delay(100);


            if (card.Vip == "A")
            {
                precoapagar = leitoraConfiguracao.Preco_Vip;

                // aplicando desconto para a conta
                if (temDesc)
                {
                    double rem = (double)precoapagar * descontoAgora;
                    double resul = (double)precoapagar - rem;

                    precoapagar = (decimal)resul;
                }

                cardSaldo = card.Saldo;
                soma = card.Saldo + card.Bonus;

                if (card.Saldo >= precoapagar)
                {
                    card.Saldo = card.Saldo - precoapagar;
                    remBonus = card.Bonus;
                }
                else if (soma >= precoapagar)
                {
                    decimal? valorRestante = precoapagar - card.Saldo;
                    card.Bonus = card.Bonus - valorRestante;
                    card.Saldo = 0;
                }
                else
                {
                    SemSaldo = true;
                }

                isVip = true;
            }
            else
            {
                precoapagar = leitoraConfiguracao.Preco_Normal;

                // aplicando desconto para a conta
                if (temDesc)
                {
                    double rem = (double)precoapagar * descontoAgora;
                    double resul = (double)precoapagar - rem;
                    precoapagar = (decimal)resul;
                }

                cardSaldo = card.Saldo;
                soma = card.Saldo + card.Bonus;

                if (card.Saldo >= precoapagar)
                {
                    card.Saldo = card.Saldo - precoapagar;
                    remBonus = card.Bonus;
                }
                else if (soma >= precoapagar)
                {
                    decimal? valorRestante = precoapagar - card.Saldo;
                    card.Bonus = card.Bonus - valorRestante;
                    card.Saldo = 0;
                }
                else
                {
                    SemSaldo = true;
                }
            }

            vipVl = await ObterVipValueAsync();
            if (card.TotalGasto >= vipVl)
            {
                await AtualizaVip(leitoraConfiguracao, leitora);
            }

            bool actionTaken = await ObterDataFesta();


            if (card.Status == "S")
            {
                corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                corLed += (int)leitora.Tempo_Aciona_Led;

                await AtualizarSaldo(card);

                log = "Cartão " + card.CardCode + " de Serviço!";
                await GerarLog(log);

                await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                msgDisplay = leitora.Display1;
                msgDisplay2 = card.Name;

                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                await Aguardar((int)leitora.Tempo_Aciona_Led);

                await AtualizarDisplayPadrao(leitoraConfiguracao);

                return;
            }
            else if (card.Status == "A" || card.Status == "B")
            {
                if (SemSaldo)
                {
                    msgDisplay = leitora.Mensagem_Erro_Saldo;
                    msgDisplay2 = "  Saldo: " + card.Saldo;
                    corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                    corLed += (int)leitora.Tempo_Alterna_Led;

                    await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                    Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | " + leitoraConfiguracao.Preco_Normal + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Cartão sem saldo\n");
                    Console.ForegroundColor = ConsoleColor.White;

                    await Aguardar((int)leitora.Tempo_Aciona_Led);

                    await AtualizarDisplayPadrao(leitoraConfiguracao);

                    log = "ERROR - Cartão " + card.CardCode + " sem Saldo!";
                    await GerarLog(log);

                    return;
                }
                if (isVip)
                {
                    if (card.Saldo >= leitoraConfiguracao.Preco_Vip)
                    {

                        corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                        corLed += (int)leitora.Tempo_Aciona_Led;

                        await AtualizarTotalGasto(card, leitoraConfiguracao.Preco_Vip ?? 0m);

                        await AtualizarSaldo(card);

                        await CriaHistorico(leitora, card.Saldo, card.Bonus, leitoraConfiguracao.Preco_Vip, 00);

                        log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                        await GerarLog(log);

                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Vip + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Sucesso\n");
                        Console.ForegroundColor = ConsoleColor.White;

                        await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                        msgDisplay = leitora.Mensagem_Sucesso;
                        msgDisplay2 = " Saldo: " + card.Saldo;

                        await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                        await Aguardar((int)leitora.Tempo_Aciona_Led);

                        await AtualizarDisplayPadrao(leitoraConfiguracao);
                    }
                    else if (card.Saldo >= 0 && remBonus == card.Bonus)
                    {
                        corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                        corLed += (int)leitora.Tempo_Aciona_Led;

                        await AtualizarTotalGasto(card, leitoraConfiguracao.Preco_Vip ?? 0m);

                        await AtualizarSaldo(card);

                        await CriaHistorico(leitora, card.Saldo, card.Bonus, leitoraConfiguracao.Preco_Vip, 00);

                        log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                        await GerarLog(log);

                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Vip + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Sucesso\n");
                        Console.ForegroundColor = ConsoleColor.White;

                        await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                        msgDisplay = leitora.Mensagem_Sucesso;
                        msgDisplay2 = " Saldo: " + card.Saldo;

                        await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                        await Aguardar((int)leitora.Tempo_Aciona_Led);

                        await AtualizarDisplayPadrao(leitoraConfiguracao);
                    }
                    else
                    {
                        if (selectColor != 3)
                        {
                            corLed = (int)(Led)Convert.ToInt16(Led.Jog_Sem_Ticket) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            await AtualizarSaldo(card);

                            log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                            await GerarLog(log);

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Vip + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + ",00 | Bonus " + card.Bonus + " - ");
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.Write("Bônus\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await CriaHistorico(leitora, card.Saldo, card.Bonus, 00, leitoraConfiguracao.Preco_Vip);

                            await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                            msgDisplay = leitora.Mensagem_Sucesso;
                            msgDisplay2 = " Bonus: " + card.Bonus;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                            await Aguardar((int)leitora.Tempo_Aciona_Led);

                            await AtualizarDisplayPadrao(leitoraConfiguracao);
                        }
                        else
                        {

                            msgDisplay = leitora.Display1;
                            msgDisplay2 = "Nao Aceita Bonus";

                            corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                            Console.ForegroundColor = ConsoleColor.DarkMagenta;
                            Console.Write("Cartão bonus não aceito em máquina GRUA\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                            await Aguardar((int)leitora.Tempo_Aciona_Led);

                            await AtualizarDisplayPadrao(leitoraConfiguracao);

                            return;
                        }
                    }
                }
                else
                {
                    if (card.Saldo >= leitoraConfiguracao.Preco_Normal)
                    {

                        corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                        corLed += (int)leitora.Tempo_Aciona_Led;

                        await AtualizarTotalGasto(card, leitoraConfiguracao.Preco_Normal ?? 0m);

                        await AtualizarSaldo(card);

                        await CriaHistorico(leitora, card.Saldo, card.Bonus, leitoraConfiguracao.Preco_Normal, 00);

                        log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                        await GerarLog(log);

                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Normal + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Sucesso\n");
                        Console.ForegroundColor = ConsoleColor.White;

                        await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                        msgDisplay = leitora.Mensagem_Sucesso;
                        msgDisplay2 = "  Saldo: " + card.Saldo;

                        await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                        await Aguardar((int)leitora.Tempo_Aciona_Led);

                        await AtualizarDisplayPadrao(leitoraConfiguracao);
                    }
                    else if (card.Saldo >= 0 && remBonus == card.Bonus)
                    {
                        corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                        corLed += (int)leitora.Tempo_Aciona_Led;

                        await AtualizarTotalGasto(card, leitoraConfiguracao.Preco_Normal ?? 0m);

                        await AtualizarSaldo(card);

                        await CriaHistorico(leitora, card.Saldo, card.Bonus, leitoraConfiguracao.Preco_Normal, 00);

                        log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                        await GerarLog(log);

                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Normal + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Sucesso\n");
                        Console.ForegroundColor = ConsoleColor.White;

                        await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                        msgDisplay = leitora.Mensagem_Sucesso;
                        msgDisplay2 = " Saldo: " + card.Saldo;

                        await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                        await Aguardar((int)leitora.Tempo_Aciona_Led);

                        await AtualizarDisplayPadrao(leitoraConfiguracao);
                    }
                    else
                    {
                        if (selectColor != 3)
                        {
                            card.Bonus += card.Saldo;
                            corLed = (int)(Led)Convert.ToInt16(Led.Jog_Sem_Ticket) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            await AtualizarSaldo(card);

                            log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                            await GerarLog(log);

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Normal + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + ",00 | Bonus " + card.Bonus + " - ");
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.Write("Bônus\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await CriaHistorico(leitora, card.Saldo, card.Bonus, 00, leitoraConfiguracao.Preco_Normal);

                            await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                            msgDisplay = leitora.Mensagem_Sucesso;
                            msgDisplay2 = " Bonus: " + card.Bonus;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                            await Aguardar((int)leitora.Tempo_Aciona_Led);

                            await AtualizarDisplayPadrao(leitoraConfiguracao);
                        }
                        else
                        {
                            msgDisplay = leitora.Display1;
                            msgDisplay2 = "Nao Aceita Bonus";

                            corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                            Console.ForegroundColor = ConsoleColor.DarkMagenta;
                            Console.Write("Cartão bonus não aceito em máquina GRUA\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                            await Aguardar((int)leitora.Tempo_Aciona_Led);

                            await AtualizarDisplayPadrao(leitoraConfiguracao);

                            return;
                        }
                    }
                }
            }
            else if (card.Status == "FA" && actionTaken == false)
            {
                if (selectColor != 3)
                {
                    if (SemSaldo)
                    {
                        msgDisplay = leitora.Mensagem_Erro_Saldo;
                        msgDisplay2 = "  Saldo: " + card.Saldo;
                        corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                        corLed += (int)leitora.Tempo_Alterna_Led;

                        await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | " + leitoraConfiguracao.Preco_Normal + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("Cartão sem saldo\n");
                        Console.ForegroundColor = ConsoleColor.White;

                        await Aguardar((int)leitora.Tempo_Aciona_Led);

                        await AtualizarDisplayPadrao(leitoraConfiguracao);

                        log = "ERROR - Cartão " + card.CardCode + " sem Saldo!";
                        await GerarLog(log);

                        return;
                    }
                    if (isVip)
                    {
                        if (card.Saldo >= leitoraConfiguracao.Preco_Vip)
                        {

                            corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            await AtualizarTotalGasto(card, leitoraConfiguracao.Preco_Vip ?? 0m);

                            await AtualizarSaldo(card);

                            await CriaHistorico(leitora, card.Saldo, card.Bonus, leitoraConfiguracao.Preco_Vip, 00);

                            log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                            await GerarLog(log);

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Vip + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("Sucesso\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                            msgDisplay = leitora.Mensagem_Sucesso;
                            msgDisplay2 = " Saldo: " + card.Saldo;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                            await Aguardar((int)leitora.Tempo_Aciona_Led);

                            await AtualizarDisplayPadrao(leitoraConfiguracao);
                        }
                        else if (card.Saldo >= 0 && remBonus == card.Bonus)
                        {
                            corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            await AtualizarTotalGasto(card, leitoraConfiguracao.Preco_Vip ?? 0m);

                            await AtualizarSaldo(card);

                            await CriaHistorico(leitora, card.Saldo, card.Bonus, leitoraConfiguracao.Preco_Vip, 00);

                            log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                            await GerarLog(log);

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Vip + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("Sucesso\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                            msgDisplay = leitora.Mensagem_Sucesso;
                            msgDisplay2 = " Saldo: " + card.Saldo;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                            await Aguardar((int)leitora.Tempo_Aciona_Led);

                            await AtualizarDisplayPadrao(leitoraConfiguracao);
                        }
                        else
                        {
                            if (selectColor != 3)
                            {
                                corLed = (int)(Led)Convert.ToInt16(Led.Jog_Sem_Ticket) * 10;
                                corLed += (int)leitora.Tempo_Aciona_Led;

                                await AtualizarSaldo(card);

                                log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                                await GerarLog(log);

                                Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Vip + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + ",00 | Bonus " + card.Bonus + " - ");
                                Console.ForegroundColor = ConsoleColor.Blue;
                                Console.Write("Bônus\n");
                                Console.ForegroundColor = ConsoleColor.White;

                                await CriaHistorico(leitora, card.Saldo, card.Bonus, 00, leitoraConfiguracao.Preco_Vip);

                                await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                                msgDisplay = leitora.Mensagem_Sucesso;
                                msgDisplay2 = " Bonus: " + card.Bonus;

                                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                                await Aguardar((int)leitora.Tempo_Aciona_Led);

                                await AtualizarDisplayPadrao(leitoraConfiguracao);
                            }
                            else
                            {

                                msgDisplay = leitora.Display1;
                                msgDisplay2 = "Nao Aceita Bonus";

                                corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                                corLed += (int)leitora.Tempo_Aciona_Led;

                                Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                                Console.Write("Cartão bonus não aceito em máquina GRUA\n");
                                Console.ForegroundColor = ConsoleColor.White;

                                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                                await Aguardar((int)leitora.Tempo_Aciona_Led);

                                await AtualizarDisplayPadrao(leitoraConfiguracao);

                                return;
                            }
                        }
                    }
                    else
                    {
                        if (card.Saldo >= leitoraConfiguracao.Preco_Normal)
                        {

                            corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            await AtualizarTotalGasto(card, leitoraConfiguracao.Preco_Normal ?? 0m);

                            await AtualizarSaldo(card);

                            await CriaHistorico(leitora, card.Saldo, card.Bonus, leitoraConfiguracao.Preco_Normal, 00);

                            log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                            await GerarLog(log);

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Normal + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("Sucesso\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                            msgDisplay = leitora.Mensagem_Sucesso;
                            msgDisplay2 = "  Saldo: " + card.Saldo;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                            await Aguardar((int)leitora.Tempo_Aciona_Led);

                            await AtualizarDisplayPadrao(leitoraConfiguracao);
                        }
                        else if (card.Saldo >= 0 && remBonus == card.Bonus)
                        {
                            corLed = (int)(Led)Convert.ToInt16(Led.Cartao_Aceito) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            await AtualizarTotalGasto(card, leitoraConfiguracao.Preco_Normal ?? 0m);

                            await AtualizarSaldo(card);

                            await CriaHistorico(leitora, card.Saldo, card.Bonus, leitoraConfiguracao.Preco_Normal, 00);

                            log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                            await GerarLog(log);

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Normal + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + " | Bonus " + card.Bonus + " - ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("Sucesso\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                            msgDisplay = leitora.Mensagem_Sucesso;
                            msgDisplay2 = " Saldo: " + card.Saldo;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                            await Aguardar((int)leitora.Tempo_Aciona_Led);

                            await AtualizarDisplayPadrao(leitoraConfiguracao);
                        }
                        else
                        {
                            if (selectColor != 3)
                            {
                                card.Bonus += card.Saldo;
                                corLed = (int)(Led)Convert.ToInt16(Led.Jog_Sem_Ticket) * 10;
                                corLed += (int)leitora.Tempo_Aciona_Led;

                                await AtualizarSaldo(card);

                                log = "Cartão " + card.CardCode + " Passou com Sucesso!";
                                await GerarLog(log);

                                Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Preço pago: R$ " + leitoraConfiguracao.Preco_Normal + " | Cartão " + leitora.CardCode + "\nSaldo " + card.Saldo + ",00 | Bonus " + card.Bonus + " - ");
                                Console.ForegroundColor = ConsoleColor.Blue;
                                Console.Write("Bônus\n");
                                Console.ForegroundColor = ConsoleColor.White;

                                await CriaHistorico(leitora, card.Saldo, card.Bonus, 00, leitoraConfiguracao.Preco_Normal);

                                await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                                msgDisplay = leitora.Mensagem_Sucesso;
                                msgDisplay2 = " Bonus: " + card.Bonus;

                                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                                await Aguardar((int)leitora.Tempo_Aciona_Led);

                                await AtualizarDisplayPadrao(leitoraConfiguracao);
                            }
                            else
                            {
                                msgDisplay = leitora.Display1;
                                msgDisplay2 = "Nao Aceita Bonus";

                                corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                                corLed += (int)leitora.Tempo_Aciona_Led;

                                Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                                Console.Write("Cartão bonus não aceito em máquina GRUA\n");
                                Console.ForegroundColor = ConsoleColor.White;

                                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                                await Aguardar((int)leitora.Tempo_Aciona_Led);

                                await AtualizarDisplayPadrao(leitoraConfiguracao);

                                return;
                            }
                        }
                    }
                }
            }
            else if (card.Status == "FA" && actionTaken == true)
            {
                if (selectColor != 3)
                {
                    if (actionTaken == false)
                    {
                        corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                        corLed += (int)leitora.Tempo_Aciona_Led;

                        await AtualizarSaldo(card);

                        log = "Cartão " + card.CardCode + " de Festa (tempo acabou)";
                        await GerarLog(log);

                        await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                        msgDisplay = leitora.Display1;
                        msgDisplay2 = " Saldo Invalido ";

                        await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                        await Aguardar((int)leitora.Tempo_Aciona_Led);

                        await AtualizarDisplayPadrao(leitoraConfiguracao);
                    }
                    else
                    {
                        if (!await AtualizarDataCartao(card.CardCode))
                        {
                            msgDisplay = leitora.Display1;
                            msgDisplay2 = "Espere 2 Minutos";
                            corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                            corLed += (int)leitora.Tempo_Aciona_Led;

                            Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("Cartão Festa Bloqueado por 2 Min.\n");
                            Console.ForegroundColor = ConsoleColor.White;

                            await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);
                            return; 
                        }

                        corLed = (int)(Led)Convert.ToInt16(Led.Maq_Brinde) * 10;
                        corLed += (int)leitora.Tempo_Aciona_Led;

                        await AtualizarSaldo(card);

                        log = "Cartão " + card.CardCode + " de Festa!";
                        await GerarLog(log);

                        await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                        msgDisplay = leitora.Mensagem_Sucesso;
                        msgDisplay2 = displayMessage;

                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.Write("Cartão Festa\n");
                        Console.ForegroundColor = ConsoleColor.White;

                        await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                        await Aguardar((int)leitora.Tempo_Aciona_Led);

                        await AtualizarDisplayPadrao(leitoraConfiguracao);

                        await AtualizarDisplayPadrao(leitoraConfiguracao);

                        return;
                    }
                }
                else
                {
                    msgDisplay = leitora.Display1;
                    msgDisplay2 = "Nao Aceita Festa";
                    corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                    corLed += (int)leitora.Tempo_Aciona_Led;

                    Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    Console.Write("Cartão Não Aceito em Máquina Grua\n");
                    Console.ForegroundColor = ConsoleColor.White;

                    await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                    await Aguardar((int)leitora.Tempo_Aciona_Led);

                    await AtualizarDisplayPadrao(leitoraConfiguracao);

                    return;
                }
            }
            else if (card.Status == "FE")
            {
                corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                corLed += (int)leitora.Tempo_Aciona_Led;

                await AtualizarSaldo(card);

                log = "Cartão festa " + card.CardCode + " em estoque!";
                await GerarLog(log);


                await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                msgDisplay = leitora.Display1;
                msgDisplay2 = " Cartao Sem Uso ";

                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                await Aguardar((int)leitora.Tempo_Aciona_Led);

                await AtualizarDisplayPadrao(leitoraConfiguracao);

                return;
            }
            else if (card.Status == "E")
            {
                corLed = (int)(Led)Convert.ToInt16(Led.Maq_Normal) * 10;
                corLed += (int)leitora.Tempo_Aciona_Led;

                await AtualizarSaldo(card);

                log = "Cartão " + card.CardCode + " em estoque!";
                await GerarLog(log);

                await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);


                msgDisplay = leitora.Display1;
                msgDisplay2 = " Cartao Sem Uso ";

                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                await Aguardar((int)leitora.Tempo_Aciona_Led);

                await AtualizarDisplayPadrao(leitoraConfiguracao);

                return;
            }
            else if (card.Status == "I")
            {
                corLed = (int)(Led)Convert.ToInt16(Led.Saldo_Insuficiente) * 10;
                corLed += (int)leitora.Tempo_Aciona_Led;

                await AtualizarSaldo(card);

                log = "Cartão " + card.CardCode + " inativo";
                await GerarLog(log);

                await GerarHistorico(leitoraConfiguracao, card, leitora, isVip);

                msgDisplay = leitora.Display1;
                msgDisplay2 = " Cartao Inativo ";

                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                await Aguardar((int)leitora.Tempo_Aciona_Led);

                await AtualizarDisplayPadrao(leitoraConfiguracao);

                return;
            }
            else
            {
                msgDisplay = leitora.Display1;
                msgDisplay2 = " Nao Encontrado ";
                corLed = (int)(Led)Convert.ToInt16(Led.NaoPisca) * 0;
                corLed += (int)leitora.Tempo_Alterna_Led;

                await AtualizarLeitora(leitora.Code_Leitora, msgDisplay, msgDisplay2, corLed, StatusCode.A);

                await Aguardar((int)leitora.Tempo_Aciona_Led);

                await AtualizarDisplayPadrao(leitoraConfiguracao);

                log = "ERROR - Cartão " + card.CardCode + " não encontrado!";
                await GerarLog(log);

                return;
            }

        }

        private async Task<bool> AtualizarDataCartao(string cardCode)
        {
            string connectionString = pDbVision;

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string selectQuery = "SELECT date_desactive FROM tb_card WHERE number = @cardCode";
                using (var selectCommand = new MySqlCommand(selectQuery, connection))
                {
                    selectCommand.Parameters.AddWithValue("@cardCode", cardCode);

                    var result = await selectCommand.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value) // Verifica se não é nulo
                    {
                        DateTime dateDesactive = (DateTime)result;

                        if (DateTime.Now <= dateDesactive)
                        {
                            return false;
                        }
                    }
                }

                string updateQuery = "UPDATE tb_card SET date_active = @dateActive, date_desactive = @dateDesactive WHERE number = @cardCode";
                using (var updateCommand = new MySqlCommand(updateQuery, connection))
                {
                    DateTime dateActive = DateTime.Now;
                    DateTime dateDesactive = dateActive.AddMinutes(2);

                    updateCommand.Parameters.AddWithValue("@dateActive", dateActive);
                    updateCommand.Parameters.AddWithValue("@dateDesactive", dateDesactive);
                    updateCommand.Parameters.AddWithValue("@cardCode", cardCode);

                    await updateCommand.ExecuteNonQueryAsync();
                }
            }

            return true;
        }

        public async Task CriaHistorico(LeitoraDM leitora, decimal? saldoAtual, decimal? bonusAtual, decimal? saldoPago, decimal? bonusPago)
        {
            string query1 = "SELECT id FROM tb_machine_settings WHERE code_leitora = @codeLeitora";
            string query2 = "SELECT codigo FROM tb_card WHERE number = @cardNumber";
            string insertQuery = "INSERT INTO tb_historico_jogadas (maquina, codigo_cartao, date_change, valor_saldo_atualizado, valor_bonus_atualizado, valor_saldo, valor_bonus) " +
                                 "VALUES (@id, @codigoCartao, @dateChange, @saldoAtual, @bonusAtual, @saldoPago, @bonusPago)";

            int machineSettingsId = 0;
            string cardNumber = string.Empty;
            DateTime dataHoraAtual = DateTime.Now;

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    // Consulta para pegar o ID da tabela tb_machine_settings
                    using (var command1 = new MySqlCommand(query1, connection))
                    {
                        command1.Parameters.AddWithValue("@codeLeitora", leitora.Code_Leitora);
                        using (var reader1 = await command1.ExecuteReaderAsync())
                        {
                            if (await reader1.ReadAsync())
                            {
                                machineSettingsId = reader1.GetInt32("id");
                            }
                        }
                    }

                    // Consulta para pegar o código do cartão (codigo) da tabela tb_card
                    using (var command2 = new MySqlCommand(query2, connection))
                    {
                        command2.Parameters.AddWithValue("@cardNumber", leitora.CardCode);
                        using (var reader2 = await command2.ExecuteReaderAsync())
                        {
                            if (await reader2.ReadAsync())
                            {
                                cardNumber = reader2.GetString("codigo");
                            }
                        }
                    }

                    // Inserir os dados na tabela tb_historico_jogadas com saldo e bônus atualizados
                    using (var insertCommand = new MySqlCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@id", machineSettingsId);
                        insertCommand.Parameters.AddWithValue("@codigoCartao", cardNumber);
                        insertCommand.Parameters.AddWithValue("@dateChange", dataHoraAtual);
                        insertCommand.Parameters.AddWithValue("@saldoAtual", saldoAtual);
                        insertCommand.Parameters.AddWithValue("@bonusAtual", bonusAtual);
                        insertCommand.Parameters.AddWithValue("@saldoPago", saldoPago);
                        insertCommand.Parameters.AddWithValue("@bonusPago", bonusPago);

                        await insertCommand.ExecuteNonQueryAsync();
                    }

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar e inserir histórico: {ex.Message}");
            }
        }



        private async Task EnviarReleAsync(LeitoraConfiguracaoDM leitoraConfiguracao, LeitoraDM leitora, int num)
        {
            string query = "";

            try

            {
                using (var connection = new MySqlConnection(pDbLeitoras))
                {
                    await connection.OpenAsync();
                    query = "UPDATE tb_leitora SET aciona_rele = " + num + " WHERE code_leitora = '" + leitora.Code_Leitora + "'";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }
        }

        public async Task VerifVipValue()
        {
            string query = "SELECT vip_value FROM tb_config LIMIT 1";

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    while (true)
                    {
                        using (MySqlCommand command = new MySqlCommand(query, connection))
                        {
                            using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    vipValue = reader.GetDecimal("vip_value");

                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
            }
        }

        private async Task GerarHistorico(LeitoraConfiguracaoDM leitoraConfiguracao, CardDM card, LeitoraDM leitora, bool isVip)
        {
            string query = "";
            DateTime agora = DateTime.Now;
            string dataHora = agora.ToString("dd/MM/yyyy HH:mm:ss");

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();


                    string saldoValue = card.Saldo.Value.ToString(CultureInfo.InvariantCulture);
                    string bonusValue = card.Bonus.Value.ToString(CultureInfo.InvariantCulture);
                    string precoPago = "";

                    if (card.Status == "S")
                    {
                        // Console.WriteLine("É cartão de servico?");
                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("Cartão de serviço - " + card.Name + "\n");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (card.Status == "E")
                    {
                        //Console.WriteLine("É cartão de estoque?");
                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("Cartão em estoque\n");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (card.Status == "FE")
                    {
                        //Console.WriteLine("É cartão de festa em estoque?");
                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write("Cartão festa em estoque\n");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (card.Status == "I")
                    {
                        //Console.WriteLine("É cartão inativo?");
                        Console.Write(dataHora + " - " + "Leitora " + leitora.Code_Leitora + " | Cartão " + leitora.CardCode + " - ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("Cartão Inativo\n");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                    {
                        precoPago = leitoraConfiguracao.Preco_Normal.Value.ToString(CultureInfo.InvariantCulture);
                    }


                    query = @"
                    INSERT INTO tb_movimentacaocliente 
                    (
                        cartao,
                        codigo_leitora,
                        codigo_brinquedo,
                        nome_brinquedo,
                        preco_pago,
                        saldo_cliente,
                        saldo_bonus,
                        horario,
                        ticket_gerado,
                        ticket_cliente
                    )
                    VALUE
                    (" +
                        "'" + card.CardCode + "'" + // codigo do cartão
                        "," + "'" + leitora.Code_Leitora + "'" +// codigo da leitora
                        "," + leitoraConfiguracao.Codigo_patrimonio + // codigo da maquina
                        "," + "'" + leitoraConfiguracao.Nome_Brinquedo + "'" + // nome da maquina
                        "," + precoPago + // preco pago
                        "," + saldoValue + // saldo do cliente
                        "," + bonusValue + // bonus do bonus
                        ",NOW()" + // horario da movimentação
                        "," + 0 + // ticket gerado
                        "," + 0 + // ticket total do cliente
                    ")";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }
        }

        private async Task GerarLog(string LogMsg)
        {
            string query = "";

            try
            {
                using (var connection = new MySqlConnection(pDbLeitoras))
                {

                    await connection.OpenAsync();

                    query = "INSERT INTO tb_logs (log) VALUE ('" + LogMsg + "')";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }
        }

        private async Task AtualizarSaldo(CardDM card)
        {
            string query = "";
            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    decimal saldoValue = card.Saldo.Value;
                    saldoValue = decimal.Parse(saldoValue.ToString(CultureInfo.InvariantCulture).Replace(',', '.'), CultureInfo.InvariantCulture);
                    // Tratamento para Bonus
                    decimal bonusValue = card.Bonus.Value;
                    bonusValue = decimal.Parse(bonusValue.ToString(CultureInfo.InvariantCulture).Replace(',', '.'), CultureInfo.InvariantCulture);

                    query = string.Format(CultureInfo.InvariantCulture, "UPDATE tb_card SET saldo = {0}, bonus = {1} WHERE number = '{2}'", saldoValue, bonusValue, card.CardCode);

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {

                        await command.ExecuteNonQueryAsync();
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }
        }

        private async Task AtualizarTotalGasto(CardDM card, decimal? valorPago)
        {
            string query = "";
            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    decimal saldoValue = card.Saldo.Value;
                    saldoValue = decimal.Parse(saldoValue.ToString(CultureInfo.InvariantCulture).Replace(',', '.'), CultureInfo.InvariantCulture);

                    decimal bonusValue = card.Bonus.Value;
                    bonusValue = decimal.Parse(bonusValue.ToString(CultureInfo.InvariantCulture).Replace(',', '.'), CultureInfo.InvariantCulture);

                    decimal valor = valorPago ?? 0m; // usar 0 se valorPago for null

                    query = string.Format(CultureInfo.InvariantCulture,
                        "UPDATE tb_card SET saldo = {0}, bonus = {1}, total_gasto = total_gasto + {2} WHERE number = '{3}'",
                        saldoValue, bonusValue, valor, card.CardCode);

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }
        }

        private async Task AtualizarDisplayPadrao(LeitoraConfiguracaoDM leitoraConfig)
        {
            string query = "";

            try
            {
                using (var connection = new MySqlConnection(pDbLeitoras))
                {
                    await connection.OpenAsync();


                    query = "UPDATE tb_leitora SET display1 = '" + leitoraConfig.Display1 + "', display2 = '" + leitoraConfig.Display2 + "'" + ", status = '" + StatusCode.A + "' WHERE code_leitora = '" + leitoraConfig.Code_Leitora + "'";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }


        }

        private async Task AtualizarLeitora(string code_leitora, string msgDisplay1, string msgDisplay2, int led, StatusCode status)
        {
            string query = "";

            try
            {
                using (var connection = new MySqlConnection(pDbLeitoras))
                {
                    await connection.OpenAsync();

                    query = "UPDATE tb_leitora SET display1 = '" + msgDisplay1 + "', display2 = '" + msgDisplay2 + "', aciona_led = " + led + ", status = '" + status + "' WHERE code_leitora = '" + code_leitora + "'";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }
        }


        private async Task Aguardar(int timer)
        {
            Thread.Sleep(timer * 1000);
        }

        public async Task ServicoCheckUpdate()
        {
            string query = "SELECT code_leitora FROM tb_machine_settings WHERE checkupdate = 1";

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    while (true)
                    {
                        using (MySqlCommand command = new MySqlCommand(query, connection))
                        {
                            using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    string code_leitora = reader.GetString("code_leitora");

                                    await ContrucaoDaLeitoraAtualizada(code_leitora);
                                    await SetZeroCheckUpdate(code_leitora);

                                    Console.WriteLine("Atualização concluída!");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
            }
        }

        private async Task SetZeroCheckUpdate(string code_leitora)
        {
            string query = "";

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    query = "UPDATE tb_machine_settings SET checkupdate = 0 WHERE code_leitora = '" + code_leitora + "'";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
            }
        }

        private async Task ContrucaoDaLeitoraAtualizada(string code_leitora)
        {
            string query = "";
            bool retorno = false;

            try
            {
                using (var connection = new MySqlConnection(pDbVision))
                {
                    await connection.OpenAsync();

                    query = "SELECT * FROM tb_parametros_globais_leitoras";
                    LeitoraMensagensDM leitoraMensagem = new LeitoraMensagensDM();

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
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
                        "tb_machine_settings.parametrosid = tb_parametros_maquinas.id " +
                        "Where tb_machine_settings.code_leitora = '" + code_leitora + "'";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                LeitoraConfiguracaoDM leitoraConfig = new LeitoraConfiguracaoDM();
                                leitoraConfig.Code_Leitora = reader.GetString("code_leitora");
                                leitoraConfig.Display = reader.GetString("display1");
                                leitoraConfig.Ticket_Tipo = reader.GetString("tipo_ticket");
                                leitoraConfig.Nome_Brinquedo = reader.GetString("nome_brinquedo");

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

                                leitoraConfig.Ticket_Min = reader.IsDBNull(reader.GetOrdinal("ticket_minimo")) ? (int?)0 : reader.GetInt32(reader.GetOrdinal("ticket_minimo"));
                                leitoraConfig.Ticket_Max = reader.IsDBNull(reader.GetOrdinal("ticket_maximo")) ? (int?)0 : reader.GetInt32(reader.GetOrdinal("ticket_maximo"));
                                leitoraConfig.Tempo_Pulso = reader.IsDBNull(reader.GetOrdinal("tempo_pulso")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("tempo_pulso"));
                                leitoraConfig.Multiplica_Ticket = reader.IsDBNull(reader.GetOrdinal("multiplica_ticket")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("multiplica_ticket"));
                                leitoraConfig.Divide_Ticket = reader.IsDBNull(reader.GetOrdinal("divide_ticket")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("divide_ticket"));
                                leitoraConfig.Codigo_patrimonio = reader.IsDBNull(reader.GetOrdinal("codigo_patrimonio")) ? (string?)null : reader.GetString(reader.GetOrdinal("codigo_patrimonio"));
                                leitoraConfig.GroupId = reader.IsDBNull(reader.GetOrdinal("groupid")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("groupid"));

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
                                leitoraConfig.Ativa = reader.GetInt16("ativa");



                                await UpdateMaquina(leitoraConfig, code_leitora);

                            }
                            retorno = true;

                        }
                    }
                }

            }

            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private async Task UpdateMaquina(LeitoraConfiguracaoDM leitoraConfig, string code_leitora)
        {

            try
            {
                bool isUpdate = false;
                string query = "SELECT * FROM tb_leitora WHERE code_leitora = '" + code_leitora + "'";

                using (var connection = new MySqlConnection(pDbLeitoras))
                {
                    await connection.OpenAsync();
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                isUpdate = true;
                            }
                        }
                    }
                }
                using (var connection = new MySqlConnection(pDbLeitoras))
                {
                    await connection.OpenAsync();
                    string ativaString = leitoraConfig.Ativa == 1 ? "1" : "0";
                    int corLed;
                    corLed = leitoraConfig.Cor_Led * 10;
                    corLed += leitoraConfig.Tempo_Alterna_Led;

                    string precoNormalStr = Convert.ToDecimal(leitoraConfig.Preco_Normal).ToString("0.0");
                    string precoVipStr = Convert.ToDecimal(leitoraConfig.Preco_Vip).ToString("0.0");

                    int digitosPrecoNormal = precoNormalStr.Split('.')[0].Length;
                    int digitosPrecoVip = precoVipStr.Split('.')[0].Length;

                    if (!isUpdate)
                    {

                        Console.WriteLine("[ Maquina: " + code_leitora + " - Maquina adicionada ]");
                        query = "UPDATE tb_leitora SET "
                        + "code_leitora = '" + leitoraConfig.Code_Leitora + "',"
                        + "display1 = '" + leitoraConfig.Display + "',";

                        if (digitosPrecoNormal == 1 && digitosPrecoVip == 1)
                        {
                            query += "display2 = ' R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip + " ',";
                        }
                        else if (digitosPrecoNormal == 2 && digitosPrecoVip == 1)
                        {
                            query += "display2 = 'R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip + " ',";
                        }
                        else
                        {
                            query += "display2 = 'R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip + "',";
                        }

                        // Continuação da query...
                        query += "led_base = " + corLed + ","
                        + "ticket_min = " + leitoraConfig.Ticket_Min + ","
                        + "ticket_max = " + leitoraConfig.Ticket_Max + ","
                        + "tipo_ticket = '" + leitoraConfig.Ticket_Tipo + "',"
                        + "checkleitora = 1,"
                        + "aceita_ticket = " + leitoraConfig.Aceita_Ticket + ","
                        + "ativa = " + ativaString + ","
                        + "mensagem_sucesso = '" + leitoraConfig.Mensagem_Sucesso + "',"
                        + "mensagem_aguarde = '" + leitoraConfig.Mensagem_Aguarde + "',"
                        + "mensagem_erro_saldo = '" + leitoraConfig.Mensagem_Erro_Saldo + "',"
                        + "mensagem_desativada = '" + leitoraConfig.Mensagem_Desativada + "',"
                        + "mensagem_erro_comunicacao = '" + leitoraConfig.Mensagem_Erro_Comunicacao + "',"
                        + "mensagem_emitir_ticket = '" + leitoraConfig.Mensagem_Emitir_Ticket + "',"
                        + "mensagem_emitir_brinde = '" + leitoraConfig.Mensagem_Emitir_Brinde + "',"
                        + "tempo_alterna_led = " + leitoraConfig.Tempo_Alterna_Led + ","
                        + "tempo_aciona_led = " + leitoraConfig.Tempo_Aciona_Led + " "
                        + "WHERE code_leitora = '" + code_leitora + "'";
                    }
                    else
                    {
                        query = "UPDATE tb_leitora SET "
                        + "code_leitora = '" + leitoraConfig.Code_Leitora + "',"
                        + "display1 = '" + leitoraConfig.Display + "',";

                        if (digitosPrecoNormal == 1 && digitosPrecoVip == 1)
                        {
                            query += "display2 = ' R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip + " ',";
                        }
                        else if (digitosPrecoNormal == 2 && digitosPrecoVip == 1)
                        {
                            query += "display2 = 'R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip + " ',";
                        }
                        else
                        {
                            query += "display2 = 'R$" + leitoraConfig.Preco_Normal + " VIP" + leitoraConfig.Preco_Vip + "',";
                        }

                        // Continuação da query...
                        query += "led_base = " + corLed + ","
                        + "ticket_min = " + leitoraConfig.Ticket_Min + ","
                        + "ticket_max = " + leitoraConfig.Ticket_Max + ","
                        + "tipo_ticket = '" + leitoraConfig.Ticket_Tipo + "',"
                        + "checkleitora = 1,"
                        + "aceita_ticket = " + leitoraConfig.Aceita_Ticket + ","
                        + "ativa = " + ativaString + ","
                        + "mensagem_sucesso = '" + leitoraConfig.Mensagem_Sucesso + "',"
                        + "mensagem_aguarde = '" + leitoraConfig.Mensagem_Aguarde + "',"
                        + "mensagem_erro_saldo = '" + leitoraConfig.Mensagem_Erro_Saldo + "',"
                        + "mensagem_desativada = '" + leitoraConfig.Mensagem_Desativada + "',"
                        + "mensagem_erro_comunicacao = '" + leitoraConfig.Mensagem_Erro_Comunicacao + "',"
                        + "mensagem_emitir_ticket = '" + leitoraConfig.Mensagem_Emitir_Ticket + "',"
                        + "mensagem_emitir_brinde = '" + leitoraConfig.Mensagem_Emitir_Brinde + "',"
                        + "tempo_alterna_led = " + leitoraConfig.Tempo_Alterna_Led + ","
                        + "tempo_aciona_led = " + leitoraConfig.Tempo_Aciona_Led + " "
                        + "WHERE code_leitora = '" + code_leitora + "'";
                    }

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}