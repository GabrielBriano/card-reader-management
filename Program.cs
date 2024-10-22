using Microsoft.Extensions.Logging;
using MySqlConnector;
using ServiceManager.Process;
using System.Globalization;

namespace ServiceManager
{

    /// <summary>
    /// Software de Gerenciamento das Leitoras / Readers Management Software 
    /// Desenvolvido por Gabriel Briano de Oliveira / Developed by Gabriel Briano de Oliveira  
    /// para I/O Eletronica / for I/O Eletronica  
    /// Atualizado em 07/10/2024 / Updated on 10/07/2024
    /// </summary>
    class Program {
        static void Main()
        {
            IncializeService incializeService = new IncializeService();
            Console.WriteLine("Iniciando serviço...");
            incializeService.StartService();
        }
    }
}

