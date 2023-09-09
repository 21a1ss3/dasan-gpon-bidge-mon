using System;
using System.Collections.Generic;
using System.Text;

namespace UzZhoneRouterSetupper
{
    public class PppoeClientStatus
    {
        public string InterfaceName { get; set; }
        public string InterfaceType { get; set; }
        public string Status { get; set; }
        public int Uptime { get; set; }
        public int Mtu { get; set; }
        public string LastError { get; set; }
    }
}
