using MySqlConnector;
using ServiceManager.Class.DM;
using System.Drawing;

namespace ServiceManager.Process
{
    public class AsyncCheckConnection
    {
        private string? pBancoLeitora = "Server=192.168.50.11;Database=leitoras;Uid=desenv;Pwd=root;";
        private string? pBancoVision = "Server=192.168.50.11;Database=visionbd;Uid=desenv;Pwd=root;";

        public async Task ServicoComunicacao()
        {
            while (true)
            {
                await VerificarComunicacao();

                await Aguardar(5);

                await SetStatusToValid();

                await Aguardar(10);

            }
        }

        public async Task VerificarComunicacao()
        {
            List<Task> tarefasValidacao = new List<Task>();

            string query = "SELECT code_leitora FROM tb_leitora";
            string? pLeitora;
            try
            {

                using (var connection = new MySqlConnection(pBancoLeitora))
                {
                    await connection.OpenAsync();
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                pLeitora = reader.GetString("code_leitora");

                                var tarefaValidacao = StartCheckLeitora(pLeitora);
                                tarefasValidacao.Add(tarefaValidacao);
                            }
                        }
                    }
                }
                await Task.WhenAll(tarefasValidacao);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            
            
        }

        public async Task StartCheckLeitora(string leitora)
        {
            string query = "SELECT checkleitora FROM tb_leitora WHERE code_leitora = '" + leitora + "'";
            bool valid = false;
            int statusId = 0;
            try
            {

                for (int i = 0; i < 50; i++)
                {
                
                    using (var connection = new MySqlConnection(pBancoLeitora))
                    {
                        await connection.OpenAsync(); 
                        using (MySqlCommand command = new MySqlCommand(query, connection))
                        {
                            using (MySqlDataReader reader = await command.ExecuteReaderAsync()) 
                            {
                                if (await reader.ReadAsync()) 
                                {
                                    statusId = reader.GetInt32("checkleitora");
                                }
                            }
                        }
                    }
                    if (statusId == 0)
                    {
                        Console.WriteLine("[ Maquina: " + leitora + " - Conectada ]");
                        break;
                    }
                    await Aguardar(1); 

                }
            
            }
            catch(Exception e)
            {
                Console.WriteLine($"{e.Message}");
            }
            if (statusId == 1)
            {
               Console.WriteLine("[ Maquina: " + leitora + " - SEM COMUNICAÇAO ]");
            }
        }

        private async Task SetStatusToValid()
        {
            string query = "";

            try

            {
                using (var connection = new MySqlConnection(pBancoLeitora))
                {
                    await connection.OpenAsync();
                    query = "UPDATE tb_leitora SET checkleitora = 1";

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
        public async Task Aguardar(int segundos)
        {
            await Task.Delay(TimeSpan.FromSeconds(segundos));
        }
    }
}
