using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UzZhoneRouterSetupper;

namespace UzZhoneRouterTests
{
    [TestClass]
    public class RouterShellClientUnitTest
    {
        private byte[] _firstPacket = { 0xff, 0xfd, 0x01, 0xff, 0xfd, 0x21, 0xff, 0xfb, 0x01, 0xff, 0xfb, 0x03, };
        private string _telnetWelcomeMsg1 = @"
               Welcome to DASAN Zhone Solutions
";
        private string _telnetWelcomeMsg2 = @"               Model: ZNID-GPON-2426A1-EU Router
               Release: S4.1.037

Copyright (C) 2009-2017 by DASAN Zhone Solutions, Inc.  All Rights Reserved.
Confidential, Unpublished Property of DASAN Zhone Solutions.
Rights Reserved Under the Copyright Laws of the United States.

Login: ";
        private int _waitTimeout = 500;

        private (TelnetClient, RouterShellClient) _produceTestConnection()
        {

            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.3"), 0);
            TcpClient srvClient = null;
            server.Start();
            server.AcceptTcpClientAsync().ContinueWith(async (srvClientTask) =>
            {
                srvClient = await srvClientTask;
            }, System.Threading.Tasks.TaskContinuationOptions.RunContinuationsAsynchronously);

            TcpClient client = new TcpClient();
            client.Connect(IPAddress.Parse("127.0.0.3"), ((IPEndPoint)server.LocalEndpoint).Port);

            while (srvClient == null)
                ;

            TelnetClient testClinet = new TelnetClient(srvClient);
            testClinet.Start();
            RouterShellClient commander = new RouterShellClient(client);

            return (testClinet, commander);
        }

        [TestMethod]
        public void TestLogin()
        {
            var tstConn = _produceTestConnection();
            TelnetClient testClinet = tstConn.Item1;
            RouterShellClient commander = tstConn.Item2;


            testClinet.TelnetStream.Write(_firstPacket, 0, _firstPacket.Length);
            testClinet.TelnetStream.Flush();
            //System.Threading.Thread.Sleep(1);
            testClinet.SendMessage(_telnetWelcomeMsg1);
            //System.Threading.Thread.Sleep(1);
            testClinet.SendMessage(_telnetWelcomeMsg2);

            string username = "admin";
            string password = "zhone";

            string usernameToCompare = null;
            string passwordToCompare = null;

            var serverTask = testClinet.MessageAwaitSemaphore.WaitAsync().ContinueWith(async (__t1) =>
            {
                await __t1;

                usernameToCompare = testClinet.IncomeMessages.Dequeue();
                Assert.AreEqual(username, usernameToCompare);
                testClinet.MessageAwaitSemaphore.Wait(_waitTimeout);
                Assert.AreEqual("\r\n", testClinet.IncomeMessages.Dequeue());
                Assert.AreEqual(0, testClinet.IncomeMessages.Count);

                testClinet.SendMessageWithNewLine(usernameToCompare);
                testClinet.SendMessage("Password: ");

                Console.WriteLine("Password prompt submit");
                
                await testClinet.MessageAwaitSemaphore.WaitAsync().ContinueWith(async (__t2) =>
                {
                    await __t2;

                    passwordToCompare = testClinet.IncomeMessages.Dequeue();
                    Assert.AreEqual(password, passwordToCompare);
                    testClinet.MessageAwaitSemaphore.Wait(_waitTimeout);
                    Assert.AreEqual("\r\n", testClinet.IncomeMessages.Dequeue());
                    Assert.AreEqual(0, testClinet.IncomeMessages.Count);

                    testClinet.SendMessage("\r");
                    testClinet.SendMessage("ZNID24xxA1-Router> ");

                    Console.WriteLine("Shell prompt submit");
                });

            }, System.Threading.Tasks.TaskContinuationOptions.RunContinuationsAsynchronously);

            bool status = commander.SupplyCredentials(username, password);

            serverTask.GetAwaiter().GetResult().GetAwaiter().GetResult();

            Assert.IsTrue(status);
            Assert.AreEqual(RouterShellClient.CommanderState.ExecutiveShell, commander.State);
            Assert.AreEqual("ZNID24xxA1-Router", commander.Hostname);
        }

        [TestMethod]
        public void TestEnable()
        {
            var tstConn = _produceTestConnection();
            TelnetClient testClinet = tstConn.Item1;
            RouterShellClient commander = tstConn.Item2;

            testClinet.TelnetStream.Write(_firstPacket, 0, _firstPacket.Length);
            testClinet.TelnetStream.Flush();

            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.State)).GetSetMethod(true).
                   Invoke(commander, new object[] { RouterShellClient.CommanderState.ExecutiveShell });
            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.Hostname)).GetSetMethod(true).
                   Invoke(commander, new object[] { "ZNID24xxA1-Router" });

            var serverTask = testClinet.MessageAwaitSemaphore.WaitAsync().ContinueWith(async (__t1) =>
            {
                await __t1;

                string enableCmd = testClinet.IncomeMessages.Dequeue();

                testClinet.SendMessageWithNewLine(enableCmd);
                testClinet.SendMessage("ZNID24xxA1-Router# ");

                //StringAssert.Matches(enableCmd, new Regex(@"(\r\n{0,1}\s*)*enable(\r\n{0,1}\s*)*"));
                if (testClinet.IncomeMessages.Count > 0)
                    Assert.AreEqual("enable", enableCmd);
                else
                    Assert.AreEqual("enable\r\n", enableCmd);

                if (testClinet.IncomeMessages.Count > 0)
                    Assert.AreEqual("\r\n", testClinet.IncomeMessages.Dequeue());
                Assert.AreEqual(0, testClinet.IncomeMessages.Count);
            });

            bool result = commander.DoEnable();

            serverTask.GetAwaiter().GetResult().GetAwaiter().GetResult();

            Assert.IsTrue(result);
            Assert.AreEqual(RouterShellClient.CommanderState.PriviledgeExecutiveShell, commander.State);
            Assert.AreEqual("ZNID24xxA1-Router", commander.Hostname);
        }


        [TestMethod]
        public void TestConfMode()
        {
            var tstConn = _produceTestConnection();
            TelnetClient testClinet = tstConn.Item1;
            RouterShellClient commander = tstConn.Item2;

            testClinet.TelnetStream.Write(_firstPacket, 0, _firstPacket.Length);
            testClinet.TelnetStream.Flush();

            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.State)).GetSetMethod(true).
                   Invoke(commander, new object[] { RouterShellClient.CommanderState.PriviledgeExecutiveShell });
            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.Hostname)).GetSetMethod(true).
                   Invoke(commander, new object[] { "ZNID24xxA1-Router" });

            var serverTask = testClinet.MessageAwaitSemaphore.WaitAsync().ContinueWith(async (__t1) =>
            {
                await __t1;
                string configCmd = testClinet.IncomeMessages.Dequeue();

                testClinet.SendMessageWithNewLine(configCmd);
                testClinet.SendMessage("ZNID24xxA1-Router(config)# ");

                if (testClinet.IncomeMessages.Count > 0)
                    Assert.AreEqual("conf t", configCmd);
                else
                    Assert.AreEqual("conf t\r\n", configCmd);

                if (testClinet.IncomeMessages.Count > 0)
                    Assert.AreEqual("\r\n", testClinet.IncomeMessages.Dequeue());
                Assert.AreEqual(0, testClinet.IncomeMessages.Count);
            });

            bool result = commander.EnterConfigMode();

            serverTask.GetAwaiter().GetResult().GetAwaiter().GetResult();

            Assert.IsTrue(result);
            Assert.AreEqual(RouterShellClient.CommanderState.Configuration, commander.State);
            Assert.AreEqual("ZNID24xxA1-Router", commander.Hostname);
            Assert.AreEqual("config", commander.ConfigMode);
            Assert.IsTrue(commander.IsRootConfigMode);
        }

               
        [TestMethod]
        public void TestNewHostname()
        {
            var tstConn = _produceTestConnection();
            TelnetClient testClinet = tstConn.Item1;
            RouterShellClient commander = tstConn.Item2;

            testClinet.TelnetStream.Write(_firstPacket, 0, _firstPacket.Length);
            testClinet.TelnetStream.Flush();

            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.State)).GetSetMethod(true).
                   Invoke(commander, new object[] { RouterShellClient.CommanderState.Configuration });
            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.Hostname)).GetSetMethod(true).
                   Invoke(commander, new object[] { "ZNID24xxA1-Router" });
            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.ConfigMode)).GetSetMethod(true).
                   Invoke(commander, new object[] { "config" });

            Assert.IsTrue(commander.IsRootConfigMode);

            var serverTask = testClinet.MessageAwaitSemaphore.WaitAsync().ContinueWith(async (__t1) =>
            {
                await __t1;
                string configCmd = testClinet.IncomeMessages.Dequeue();

                testClinet.SendMessageWithNewLine(configCmd);
                testClinet.SendMessage("NewName(config)# ");

                if (testClinet.IncomeMessages.Count > 0)
                    Assert.AreEqual("New Hostname", configCmd);
                else
                    Assert.AreEqual("New Hostname\r\n", configCmd);

                if (testClinet.IncomeMessages.Count > 0)
                    Assert.AreEqual("\r\n", testClinet.IncomeMessages.Dequeue());
                Assert.AreEqual(0, testClinet.IncomeMessages.Count);
            });

            string result = commander.DoConfig("New Hostname");

            serverTask.GetAwaiter().GetResult().GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.AreEqual(RouterShellClient.CommanderState.Configuration, commander.State);
            Assert.AreEqual("NewName", commander.Hostname);
            Assert.AreEqual("config", commander.ConfigMode);
            Assert.IsTrue(commander.IsRootConfigMode);
        }

        [TestMethod]
        public void TestDeepConfigMode()
        {
            var tstConn = _produceTestConnection();
            TelnetClient testClinet = tstConn.Item1;
            RouterShellClient commander = tstConn.Item2;

            testClinet.TelnetStream.Write(_firstPacket, 0, _firstPacket.Length);
            testClinet.TelnetStream.Flush();

            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.State)).GetSetMethod(true).
                   Invoke(commander, new object[] { RouterShellClient.CommanderState.Configuration });
            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.Hostname)).GetSetMethod(true).
                   Invoke(commander, new object[] { "ZNID24xxA1-Router" });
            typeof(RouterShellClient).GetProperty(nameof(RouterShellClient.ConfigMode)).GetSetMethod(true).
                   Invoke(commander, new object[] { "config" });

            Assert.IsTrue(commander.IsRootConfigMode);

            var serverTask = testClinet.MessageAwaitSemaphore.WaitAsync().ContinueWith(async (__t1) =>
            {
                await __t1;
                string configCmd = testClinet.IncomeMessages.Dequeue();

                testClinet.SendMessageWithNewLine(configCmd);
                testClinet.SendMessage("ZNID24xxA1-Router(config-test)# ");

                if (testClinet.IncomeMessages.Count > 0)
                    Assert.AreEqual("Deep Config", configCmd);
                else
                    Assert.AreEqual("Deep Config\r\n", configCmd);

                if (testClinet.IncomeMessages.Count > 0)
                    Assert.AreEqual("\r\n", testClinet.IncomeMessages.Dequeue());
                Assert.AreEqual(0, testClinet.IncomeMessages.Count);
            });

            string result = commander.DoConfig("Deep Config");

            serverTask.GetAwaiter().GetResult().GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.AreEqual(RouterShellClient.CommanderState.Configuration, commander.State);
            Assert.AreEqual("ZNID24xxA1-Router", commander.Hostname);
            Assert.AreEqual("config-test", commander.ConfigMode);
            Assert.IsFalse(commander.IsRootConfigMode);
        }

    }
}
