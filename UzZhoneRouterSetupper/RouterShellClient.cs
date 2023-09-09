using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace UzZhoneRouterSetupper
{
    public class RouterShellClient
    {
        public RouterShellClient(TcpClient telnetLine)
        {
            ResponseTimeout = 500;
            State = CommanderState.InitialLogin;
            TelnetClient = new TelnetClient(telnetLine);
            TelnetClient.Start();
        }

        public enum CommanderState
        {
            InitialLogin,
            ExecutiveShell,
            PriviledgeExecutiveShell,
            Configuration,
            Error
        }

        public TelnetClient TelnetClient { get; private set; }
        public int ResponseTimeout { get; set; }

        public CommanderState State { get; private set; }
        public string Hostname { get; private set; }
        public string ConfigMode { get; private set; }

        private const bool _debugOut = false;

        public bool IsRootConfigMode
        {
            get { return ConfigMode == "config"; }
        }

        private bool _waitNoResponeWithTime()
        {
            if (!TelnetClient.MessageAwaitSemaphore.Wait(ResponseTimeout))
            {
                State = CommanderState.Error;
                return true;
            }

            return false;
        }

        private enum _newLineParserStates
        {
            Normal,
            CrViewed,
        }

        private bool _readMessagesUntill(StringBuilder? strBuffer, Func<string, bool> finishPredicate, Func<string, int, bool> rejectLinePredicate)
        {
            string line = string.Empty;
            bool continueLookup = true;

            const string lvlShift = "      ";
            if (_debugOut) Console.WriteLine($"{lvlShift}Starting to awaiting msg");

            for (int i = 0; continueLookup; i++)
            {
                if (_waitNoResponeWithTime())
                {
                    if (_debugOut) Console.WriteLine($"{lvlShift}timeout, leaving...");
                    return false;
                }

                //StringReader strReader;
                string message;
                lock (TelnetClient.IncomeMessages)
                    message = TelnetClient.IncomeMessages.Dequeue();

                if (_debugOut) Console.WriteLine($"{lvlShift}  Got message: {message}");

                //while ((line = strReader.ReadLine()) != null)

                for (int j=0; j < message.Length; j++)
                {
                    _newLineParserStates state = _newLineParserStates.Normal;
                    StringBuilder buffer = new StringBuilder();
                    bool newLineDisc = false;
                    if (_debugOut) Console.WriteLine($"{lvlShift}  Parsing message");

                    for (bool lookForw = true; lookForw && (j < message.Length); j++)
                    {
                        switch (state)
                        {
                            case _newLineParserStates.Normal:
                                if (message[j] == '\r')
                                {
                                    state = _newLineParserStates.CrViewed;
                                    newLineDisc = true;
                                    if (_debugOut) Console.WriteLine($"{lvlShift}  Got CR");
                                    break;
                                }
                                else if (message[j] == '\n')
                                {
                                    j--;
                                    newLineDisc = true;
                                    lookForw = false;
                                    if (_debugOut) Console.WriteLine($"{lvlShift}  Got LF (first)");
                                    if (_debugOut) Console.WriteLine($"{lvlShift}  leaving the parser");

                                    break;
                                }

                                buffer.Append(message[j]);
                                break;
                            case _newLineParserStates.CrViewed:
                                if (message[j] != '\n')
                                {
                                    j--;

                                    if (_debugOut) Console.WriteLine($"{lvlShift}  Got LF (after CR)");
                                }

                                if (_debugOut) Console.WriteLine($"{lvlShift}  leaving the parser");
                                j--;
                                lookForw = false;
                                break;                            
                        }
                    }

                    line += buffer.ToString();

                    if (_debugOut) Console.WriteLine($"{lvlShift}line:{line}");
                    if (_debugOut) Console.WriteLine($"{lvlShift}newLineDisc: {newLineDisc}");

                    if (rejectLinePredicate(line, i))
                    {
                        if (_debugOut) Console.WriteLine($"{lvlShift}Line got ignored");
                        continue;
                    }

                    if (finishPredicate(line))
                    {
                        if (_debugOut) Console.WriteLine($"{lvlShift}Finishing...");
                        continueLookup = false;
                        break;
                    }
                    
                    if (newLineDisc)
                    {
                        strBuffer?.AppendLine(line);
                        line = string.Empty;
                    }
                }
            }

            if (_debugOut) Console.WriteLine($"{lvlShift}Leaving...");
            return true;
        }

        private bool _readMessagesUntill(StringBuilder? strBuffer, Func<string, bool> finishPredicate) =>
            _readMessagesUntill(strBuffer, finishPredicate, (_1, _2) => false);

        private bool _readMessagesUntill(Func<string, bool> finishPredicate) =>
            _readMessagesUntill(null, finishPredicate, (_1, _2) => false);

        public bool SupplyCredentials(string username, string password)
        {
            if (State != CommanderState.InitialLogin)
                throw new Exception("Invalid state");

            if (!_readMessagesUntill(line => line.EndsWith("Login: ")))
                return false;

            while (TelnetClient.MessageAwaitSemaphore.CurrentCount > 0)
                TelnetClient.MessageAwaitSemaphore.Wait(1);
            TelnetClient.SendMessageWithNewLine(username);

            if (!_readMessagesUntill(line => line.EndsWith("Password: ")))
                return false;

            while (TelnetClient.MessageAwaitSemaphore.CurrentCount > 0)
                TelnetClient.MessageAwaitSemaphore.Wait(1);
            TelnetClient.SendMessageWithNewLine(password);


            if (!_readMessagesUntill(line =>
                            {
                                if (line.EndsWith("> "))
                                {
                                    Hostname = line.Substring(0, line.Length - 2);
                                    State = CommanderState.ExecutiveShell;
                                    return true;
                                }
                                else if (line.Contains("Login incorrect"))
                                    return true;

                                return false;
                            })
                )
                return false;

            return true;
        }

        public bool DoEnable()
        {
            if ((State != CommanderState.ExecutiveShell) && (State != CommanderState.PriviledgeExecutiveShell))
                throw new Exception("Invalid state");

            while (TelnetClient.MessageAwaitSemaphore.CurrentCount > 0)
                TelnetClient.MessageAwaitSemaphore.Wait(1);
            TelnetClient.SendMessageWithNewLine("enable");

            if (!_readMessagesUntill(line => line.EndsWith($"{Hostname}# ")))
                return false;


            State = CommanderState.PriviledgeExecutiveShell;
            return true;
        }

        public string? ExecCommand(string cmd)
        {
            if ((State != CommanderState.ExecutiveShell) && (State != CommanderState.PriviledgeExecutiveShell))
                throw new Exception("Invalid state");

            while (TelnetClient.MessageAwaitSemaphore.CurrentCount > 0)
                TelnetClient.MessageAwaitSemaphore.Wait(1);
            TelnetClient.SendMessageWithNewLine(cmd);

            StringBuilder strBuffer = new StringBuilder();
            char modeChar = (State == CommanderState.ExecutiveShell) ? '>' : '#';
            
            if (!_readMessagesUntill(
                                    strBuffer,
                                    line => line.EndsWith($"{Hostname}{modeChar} "),
                                    (line, lineNum) => (lineNum == 0) && (line.ToLowerInvariant().Trim() == cmd.ToLowerInvariant().ToLowerInvariant().Trim())
                                    ))
                return null;

            return strBuffer.ToString();
        }

        public bool EnterConfigMode()
        {
            if (State != CommanderState.PriviledgeExecutiveShell)
                throw new Exception("Invalid state");

            while (TelnetClient.MessageAwaitSemaphore.CurrentCount > 0)
                TelnetClient.MessageAwaitSemaphore.Wait(1);
            TelnetClient.SendMessageWithNewLine("conf t");
              
            if (!_readMessagesUntill(line => line.EndsWith($"{Hostname}(config)# ")))
                return false;
            
            ConfigMode = "config";
            State = CommanderState.Configuration;

            return true;
        }

        public string DoConfig(string cmd)
        {
            if (State != CommanderState.Configuration)
                throw new Exception("Invalid state");

            while (TelnetClient.MessageAwaitSemaphore.CurrentCount > 0)
                TelnetClient.MessageAwaitSemaphore.Wait(1);
            TelnetClient.SendMessageWithNewLine(cmd);

            StringBuilder strBuffer = new StringBuilder();
            bool firstLine = true;
            string line;

            do
            {
                if (!TelnetClient.MessageAwaitSemaphore.Wait(ResponseTimeout))
                {
                    State = CommanderState.Error;
                    return null;
                }

                StringReader strReader;
                lock (TelnetClient.IncomeMessages)
                    strReader = new StringReader(TelnetClient.IncomeMessages.Dequeue());

                while (((line = strReader.ReadLine()) != null))
                {
                    if (firstLine && (line.ToLowerInvariant().Trim() == cmd.ToLowerInvariant().ToLowerInvariant().Trim()))
                        continue;

                    string[] configSplit = Regex.Split(line, @"([a-z0-9A-Z\-_]+)\((.+)\)#\s*");

                    if (configSplit.Length == 4)
                        if ((configSplit[0] == string.Empty) && (configSplit[3] == string.Empty))
                        {
                            Hostname = configSplit[1];
                            ConfigMode = configSplit[2];
                            break;
                        }

                    strBuffer.AppendLine(line);

                    firstLine = false;
                }
            } while (line == null);

            return strBuffer.ToString();
        }

    }
}
