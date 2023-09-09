using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UzZhoneRouterSetupper;

namespace UzZhoneRouterTests
{
    [TestClass]
    public class RouterParsesTest
    {
        private string _pppoeStatus = @"

PPPoE Status



Interface         Interface Type    Status         Uptime         Current MTU    Last Error                  

---------         --------------    ------         ------         -----------    ----------                  

eth0.v1071.ppp    PPPoE_IP_Bridged  Disconnected   0              0              ERROR_AUTHENTICATION_FAILURE


";
        private string _uptimeTemplate = @"


System Time



System Uptime                {0}

System Date and Time         Tue May  2 10:16:09 1995
";


        [TestMethod]
        public void TestPppoeStatus()
        {
            PppoeClientStatus[] statuses = CommandParsers.ParsePppoeStatuses(_pppoeStatus);

            Assert.AreEqual(1, statuses.Length);
            Assert.AreEqual("eth0.v1071.ppp", statuses[0].InterfaceName);
            Assert.AreEqual("PPPoE_IP_Bridged", statuses[0].InterfaceType);
            Assert.AreEqual("Disconnected", statuses[0].Status);
            Assert.AreEqual(0, statuses[0].Uptime);
            Assert.AreEqual(0, statuses[0].Mtu);
            Assert.AreEqual("ERROR_AUTHENTICATION_FAILURE", statuses[0].LastError);
        }

        private void _doUptimeTest(string inputStr, TimeSpan expectedTime)
        {
            TimeSpan? parsedTimeSpan = CommandParsers.ParseSystemUptime(inputStr);

            Assert.IsNotNull(parsedTimeSpan);
            Assert.AreEqual(expectedTime, parsedTimeSpan.Value);
        }

        [TestMethod]
        public void TestUptime_Sec()
        {
            string uptimeStr = string.Format(_uptimeTemplate, "58 seconds");

            _doUptimeTest(uptimeStr, new TimeSpan(0, 0, 58));
        }

        [TestMethod]
        public void TestUptime_MinSec()
        {
            string uptimeStr = string.Format(_uptimeTemplate, "59 minutes, 58 seconds");

            _doUptimeTest(uptimeStr, new TimeSpan(0, 59, 58));
        }

        [TestMethod]
        public void TestUptime_HoursSec()
        {
            string uptimeStr = string.Format(_uptimeTemplate, "1 hours, 1 seconds");

            _doUptimeTest(uptimeStr, new TimeSpan(1, 0, 1));
        }

        [TestMethod]
        public void TestUptime_HoursMinSec()
        {
            string uptimeStr = string.Format(_uptimeTemplate, "1 hours, 2 minutes, 12 seconds");

            _doUptimeTest(uptimeStr, new TimeSpan(1, 2, 12));
        }

        [TestMethod]
        public void TestUptime_DaysHoursMinSec()
        {
            string uptimeStr = string.Format(_uptimeTemplate, "4 days, 20 hours, 40 minutes, 34 seconds");

            _doUptimeTest(uptimeStr, new TimeSpan(4, 20, 40, 34));
        }


    }
}
