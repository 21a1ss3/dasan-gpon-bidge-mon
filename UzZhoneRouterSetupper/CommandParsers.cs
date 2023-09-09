using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UzZhoneRouterSetupper
{
    public static class CommandParsers
    {
        public static PppoeClientStatus[] ParsePppoeStatuses(string statusOutput)
        {
            List<PppoeClientStatus> result = new List<PppoeClientStatus>();
            StringReader reader = new StringReader(statusOutput);
            string line;

            do
                line = reader.ReadLine();
            while ((line != null) && !line.Contains(@"---------"));

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] values = Regex.Split(line, @"(\S+)\s*");

                PppoeClientStatus clientStatus = new PppoeClientStatus();

                clientStatus.InterfaceName = values[1];
                clientStatus.InterfaceType = values[3];
                clientStatus.Status = values[5];
                clientStatus.Uptime = int.Parse(values[7]);
                clientStatus.Mtu = int.Parse(values[9]);
                clientStatus.LastError = values[11];

                result.Add(clientStatus);
            }

            return result.ToArray();
        }

        /*


System Time



System Uptime                59 minutes, 58 seconds

System Date and Time         Mon Mar 20 04:55:07 1995

        //*/
        public static TimeSpan? ParseSystemUptime(string timeOutput)
        {
            StringReader reader = new StringReader(timeOutput);
            string line;

            while (((line = reader.ReadLine()) != null) && !line.StartsWith("System Uptime")) ;

            if (line == null)
                return null;

            int days = 0;
            int hours = 0;
            int minutes = 0;
            int seconds = 0;
            int cValue = 0;
            StringBuilder strBuffer = new StringBuilder();

            /*
                0 - Terminate
                1 - Expecting sapces (skipping label)
                5 - Expecting 2nd Sspace
                2 - traversing spaces (expecting digit)
                3 - reading cValue or expecting space
                4 - reading unit name or expecting `,` or end of string


            //*/

            int state = 1;

            for (int i = 0; i < line.Length; i++)
            {
                switch (state)
                {
                    case 1:
                        if (char.IsWhiteSpace(line[i]))
                            state = 2;

                        break;
                    case 2:
                        if (char.IsWhiteSpace(line[i]))
                            state = 5;
                        else
                            state = 1;
                        break;

                    case 5:
                        if (char.IsDigit(line[i]))
                        {
                            strBuffer.Append(line[i]);
                            state = 3;
                        }
                        else if (!char.IsWhiteSpace(line[i]))
                            throw new Exception($"ParseSystemUptime: Unexpected character (1) `{line[i]}`");

                        break;
                    case 3:
                        if (char.IsDigit(line[i]))
                            strBuffer.Append(line[i]);
                        else if (char.IsWhiteSpace(line[i]))
                        {
                            cValue = int.Parse(strBuffer.ToString());
                            strBuffer.Clear();
                            state = 4;
                        }
                        else
                            throw new Exception($"ParseSystemUptime: Unexpected character (2) `{line[i]}`");

                        break;
                    case 4:
                        bool unitFinish = i == (line.Length - 1);
                        switch (line[i])
                        {
                            case ',':
                                unitFinish = true;
                                state = 5;
                                break;
                            default:
                                strBuffer.Append(line[i]);
                                break;
                        }

                        if (unitFinish)
                        {
                            string unitName = strBuffer.ToString().Trim();
                            strBuffer.Clear();

                            switch (unitName)
                            {
                                case "days":
                                    days = cValue;
                                    break;
                                case "hours":
                                    hours = cValue;
                                    break;
                                case "minutes":
                                    minutes = cValue;
                                    break;
                                case "seconds":
                                    seconds = cValue;
                                    break;
                                default:
                                    throw new Exception($"Unexpected unit name {unitName}");
                            }
                        }

                        break;
                } //switch (state)
            } //for (int i = 0; i < line.Length; i++)

            return new TimeSpan(days, hours, minutes, seconds);
        }
    }
}
