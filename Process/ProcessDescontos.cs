
using MySqlConnector;
using ServiceManager.Class.DM;
using ServiceManager.Class.Enum;

namespace ServiceManager.Process
{
    /// <summary>
    /// Software de Gerenciamento das Leitoras / Readers Management Software 
    /// Desenvolvido por Gabriel Briano de Oliveira / Developed by Gabriel Briano de Oliveira  
    /// para I/O Eletronica / for I/O Eletronica  
    /// Atualizado em 07/10/2024 / Updated on 10/07/2024
    /// </summary>
    
    public class ProcessDescontos
    {
        private static string? pBancoLeitora = "Server=192.168.50.11;Database=leitoras;Uid=desenv;Pwd=root;";
        private static string? pBancoVision = "Server=192.168.50.11;Database=visionbd;Uid=desenv;Pwd=root;";

        private static MySqlConnection _connectionLeitora = new MySqlConnection(pBancoLeitora);
        private static MySqlConnection _connectionVision = new MySqlConnection(pBancoVision);

        public void ProcurarDescontos(List<LeitoraConfiguracaoDM> leitoraConfig)
        {

            foreach (LeitoraConfiguracaoDM leitoraDesc in leitoraConfig)
            {
                DescontoDM desconto = VerSeTemDescontoHoje(leitoraDesc.TemDesc);

                if (desconto != null)
                {
                    AdicionarDesconto(desconto, leitoraDesc);
                }
            }

            Console.WriteLine("Adicionado: " + leitoraConfig.Count + " Hoje");
        }

        private void AdicionarDesconto(DescontoDM desconto, LeitoraConfiguracaoDM leitoraDesc)
        {
            string query = "";

            try
            {
                _connectionLeitora.Open();

                query = "UPDATE tb_leitora SET desc1 = " + desconto.desconto1 + ", descinicio1 = '" + desconto.desconto_inicio1 + "', descfim1 = '" + desconto.desconto_fim1 + "', desc2 = " + desconto.desconto2 + ", descinicio2 = '" + desconto.desconto_inicio2 + "', descfim2 = '" + desconto.desconto_fim2 + "' WHERE code_leitora = '" + leitoraDesc.Code_Leitora + "'";

                using (MySqlCommand command = new MySqlCommand(query, _connectionLeitora))
                {
                    command.ExecuteNonQuery();
                }

                _connectionLeitora.Close();
            }

            catch (Exception ex)
            {
                _connectionLeitora.Close();
                Console.WriteLine($"Erro ao inserir dados: {ex.Message}");
                
            }

        }

        private DescontoDM VerSeTemDescontoHoje(int descId)
        {
            DescontoDM desconto = new DescontoDM();
            DateTime hoje = DateTime.Today;
            DayOfWeek diaDaSemana = hoje.DayOfWeek;
            string DiaInString = DiaSemanaComoString(diaDaSemana);
            bool achouDesc = false;
            try
            {
                _connectionVision.Open();

                string query = "SELECT * FROM tb_desconto WHERE id = " + descId + " && " + DiaInString + " = 1";
                string DescSemConvert1 = "";
                string DescSemConvert2 = "";

                using (MySqlCommand command = new MySqlCommand(query, _connectionVision))
                {
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            desconto.desconto1 = reader.IsDBNull(reader.GetOrdinal(DiaInString + "_desconto1")) ? 0 : reader.GetInt32(DiaInString + "_desconto1");
                            DescSemConvert1 = reader.IsDBNull(reader.GetOrdinal(DiaInString + "_periodo1")) ? string.Empty : reader.GetString(DiaInString + "_periodo1");

                            desconto.desconto2 = reader.IsDBNull(reader.GetOrdinal(DiaInString + "_desconto2")) ? 0 : reader.GetInt32(DiaInString + "_desconto2");
                            DescSemConvert2 = reader.IsDBNull(reader.GetOrdinal(DiaInString + "_periodo2")) ? string.Empty : reader.GetString(DiaInString + "_periodo2");

                            achouDesc = true;
                        }
                    }
                }


                _connectionVision.Close();

                if (achouDesc)
                {
                    // Coletar os descontos e dividilos
                    if (DescSemConvert1 != "")
                    {
                        desconto.desconto_inicio1 = DescSemConvert1.Substring(0, 5);
                        desconto.desconto_fim1 = DescSemConvert1.Substring(8, 5);
                    }
                    else
                    {
                        desconto.desconto_inicio1 = "00:00";
                        desconto.desconto_fim1 = "23:59";
                    }

                    if (DescSemConvert2 != "")
                    {
                        desconto.desconto_inicio2 = DescSemConvert2.Substring(0, 5);
                        desconto.desconto_fim2 = DescSemConvert2.Substring(8, 5);
                    }
                    else
                    {
                        desconto.desconto_inicio2 = "00:00";
                        desconto.desconto_fim2 = "23:59";
                    }
                }
                else
                {
                    desconto = null;
                }

                

            }
            catch (Exception ex)
            {
                _connectionVision.Close();
                Console.WriteLine(ex.ToString());
            }

            return desconto;
        }

        private string DiaSemanaComoString(DayOfWeek diaDaSemana)
        {
            switch (diaDaSemana)
            {
                case DayOfWeek.Sunday:
                    return "dom";
                case DayOfWeek.Monday:
                    return "seg";
                case DayOfWeek.Tuesday:
                    return "ter";
                case DayOfWeek.Wednesday:
                    return "qua";
                case DayOfWeek.Thursday:
                    return "qui";
                case DayOfWeek.Friday:
                    return "sex";
                case DayOfWeek.Saturday:
                    return "sab";
                default:
                    throw new ArgumentException("Valor inválido para o dia da semana");
            }
        }

    }
}
