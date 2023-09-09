using System;
using System.Collections.Generic;
using System.Text;

namespace UzZhoneRouterSetupper
{
    public class Configuration
    {
        public string RouterAddress { get; set; }
        public int RouterPort { get; set; }
        public string RouterUsername { get; set; }
        public string RouterPassword { get; set; }

        public string MikrotikApiUrl { get; set; }
        public string MikrotikUsername { get; set; }
        public string MikrotikPassword { get; set; }

    }
}
