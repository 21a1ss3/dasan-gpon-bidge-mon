using System;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;


namespace UzZhoneRouterSetupper
{
    class Program
    {
        static int Main(string[] args)
        {
            IConfiguration cfgManager = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Configuration config = cfgManager.Get<Configuration>();

            using TcpClient tcpClient = new TcpClient();
            try
            {
                tcpClient.Connect(config.RouterAddress, config.RouterPort);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Unable to connect to router. Stack trace:");
                Console.WriteLine(ex);

                return -1;
            }
            RouterShellClient shell = new RouterShellClient(tcpClient);

            if (!shell.SupplyCredentials(config.RouterUsername, config.RouterPassword))
            {
                Console.WriteLine("Wrong credentials");
                return 1;
            }

            if (!shell.DoEnable())
            {
                Console.WriteLine("Failed to enable privileges");
                return 2;
            }

            string pppoeIfs = shell.ExecCommand("show interface pppoe status all");
            PppoeClientStatus[] pppoeClients = CommandParsers.ParsePppoeStatuses(pppoeIfs);

            string timeInfo = shell.ExecCommand("show system time");
            TimeSpan? uptime = CommandParsers.ParseSystemUptime(timeInfo);

            Console.WriteLine($"PPPoE Sessions: {pppoeClients.Length}");
            foreach (var pppoeSessInfo in pppoeClients)
                Console.WriteLine($"    Status: {pppoeSessInfo.Status}; LastError: {pppoeSessInfo.LastError}");

            if (uptime != null)
                Console.WriteLine($"Uptime has been succsessfully parsed. Uptime is {uptime.Value}");
            else
            {
                Console.WriteLine($"   time info: {timeInfo}");
                Console.WriteLine("WARN: Fail to parse Uptime!");
            }

            switch (args.Length>0?args[0]:string.Empty)
            {
                case "checkPppoe":
                    {
                        if (pppoeClients.Length == 1)
                            if (pppoeClients[0].LastError == "ERROR_AUTHENTICATION_FAILURE")
                            {
                                if (!shell.EnterConfigMode())
                                {
                                    Console.WriteLine("Failed to switch in config mode");
                                    return 3;
                                }

                                shell.ResponseTimeout = 6_000;

                                shell.DoConfig("vlan no vlan 1071");

                                shell.DoConfig("vlan vlan bridge 1071 PPPoE_Br");

                                for (int i = 0; i < 5; i++)
                                    shell.DoConfig($"vlan port tagged eth{i} 1071");
                            }
                    }

                    break;
                case "reboot":
                    {
                        if (!shell.EnterConfigMode())
                        {
                            Console.WriteLine("Failed to switch in config mode");
                            return 3;
                        }
                        shell.ResponseTimeout = 6_000;

                        shell.DoConfig("system reboot");

                        Console.WriteLine("Command to reaboothas been sent");
                    }
                    break;

                default:
                    Console.WriteLine($"Invalid command: {(args.Length > 0 ? args[0]:string.Empty)}");
                    break;
            }
                      

            return 0;
        }
    }
}
