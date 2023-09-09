using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UzZhoneRouterSetupper
{
    public class TelnetClient
    {
        public TelnetClient(TcpClient telnetTcpChannel)
        {
            _unprocessedInternalMsgs = new SemaphoreSlim(0, int.MaxValue);
            MessageAwaitSemaphore = new SemaphoreSlim(0, int.MaxValue);

            TelnetTcpChannel = telnetTcpChannel ?? throw new ArgumentNullException(nameof(telnetTcpChannel));
            TelnetStream = telnetTcpChannel.GetStream();

            IncomeMessages = new Queue<string>();
            _unprocessedBuffers = new Queue<byte[]>();
        }

        public TcpClient TelnetTcpChannel { get; private set; }
        public NetworkStream TelnetStream { get; private set; }
        public Queue<string> IncomeMessages { get; private set; }
        public SemaphoreSlim MessageAwaitSemaphore { get; private set; }

        private SemaphoreSlim _unprocessedInternalMsgs;
        private Queue<byte[]> _unprocessedBuffers;

        public void Start()
        {
            Task.Run(async () =>
            {
                byte[] buffer = new byte[1500];
                int readAmount = 0;
                do
                {
                    readAmount = await TelnetStream.ReadAsync(buffer, 0, buffer.Length);

                    if (readAmount > 0)
                    {
                        byte[] cutBuffer = new byte[readAmount];
                        Array.Copy(buffer, 0, cutBuffer, 0, readAmount);
                        lock (_unprocessedBuffers)
                            _unprocessedBuffers.Enqueue(cutBuffer);
                        _unprocessedInternalMsgs.Release();
                    }
                } while (TelnetTcpChannel.Connected);

            });

            Task.Run(async () =>
            {
                byte[] buffer;
                StringBuilder stringBuffer = new StringBuilder();

                do
                {
                    await _unprocessedInternalMsgs.WaitAsync();
                    lock (_unprocessedBuffers)
                        buffer = _unprocessedBuffers.Dequeue();


                    _telnetParserStates state = _telnetParserStates.Normal;

                    int lineChars = 0;
                    bool prevIsCr = false;
                    byte commandCode = 0;
                    byte commandArgument = 0;

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        switch (state)
                        {
                            case _telnetParserStates.Normal:
                                switch (buffer[i])
                                {
                                    case 0xFF:
                                        prevIsCr = false;
                                        state = _telnetParserStates.ExpectCommandCode;
                                        break;
                                    case 0x00:
                                    case 0x07:
                                        prevIsCr = false;
                                        break;

                                    case 0x0B:
                                    case 0x0C:
                                        throw new NotImplementedException("VT and FF are not implemented");

                                    case 0x0A:
                                    case 0x0D:
                                        lineChars = 0;
                                        if ((buffer[i] == 0x0A) && prevIsCr)
                                        {
                                            prevIsCr = false;
                                            break;
                                        }
                                        else if (buffer[i] == 0x0D)
                                            prevIsCr = true;

                                        stringBuffer.AppendLine();
                                        //stringBuffer.Append('\n');
                                        break;

                                    case 0x08:
                                        prevIsCr = false;
                                        if (lineChars > 0)
                                        {
                                            stringBuffer.Remove(stringBuffer.Length - 1, 1);
                                            lineChars--;
                                        }
                                        break;
                                    default:
                                        lineChars++;
                                        prevIsCr = false;
                                        char[] lineChar = Encoding.ASCII.GetChars(new byte[] { buffer[i] });

                                        stringBuffer.Append(lineChar);
                                        break;
                                }
                                break;

                            case _telnetParserStates.ExpectCommandCode:
                                switch (buffer[i])
                                {
                                    case 0xFF:
                                        lineChars++;
                                        prevIsCr = false;
                                        char[] lineChar = Encoding.ASCII.GetChars(new byte[] { buffer[i] });

                                        stringBuffer.Append(lineChar);
                                        break;

                                    case 0xFB:
                                    case 0xFC:
                                    case 0xFD:
                                    case 0xFE:
                                        commandCode = buffer[i];
                                        state = _telnetParserStates.ExpectOption1;
                                        break;

                                    default:
                                        commandCode = buffer[i];
                                        state = _telnetParserStates.Normal;
                                        break;
                                }
                                break;
                            case _telnetParserStates.ExpectOption1:
                                commandArgument = buffer[i];
                                state = _telnetParserStates.Normal;

                                break;
                            default:
                                throw new NotImplementedException();
                        } //switch (state)

                        if ((commandCode != 0) && (state == _telnetParserStates.Normal))
                        {
                            //TODO: Handle commands

                            commandCode = 0;
                        }

                    } //for (int i = 0; i < readAmount; i++)

                    if (stringBuffer.Length > 0)
                    {
                        lock (IncomeMessages)
                            IncomeMessages.Enqueue(stringBuffer.ToString());
                        MessageAwaitSemaphore.Release();
                    }

                    stringBuffer.Clear();
                } while (TelnetTcpChannel.Connected);
            });
        }

        public void SendMessage(string message)
        {
            byte[] rawMsg = Encoding.ASCII.GetBytes(message);
            TelnetStream.Write(rawMsg, 0, rawMsg.Length);
            TelnetStream.Flush();
        }

        public void SendMessageWithNewLine(string message)
        {
            byte[] rawMsg = Encoding.ASCII.GetBytes(message);
            TelnetStream.Write(rawMsg, 0, rawMsg.Length);
            TelnetStream.Flush();
            //System.Threading.Thread.Sleep(1);
            TelnetStream.Write(new byte[] { 0x0D }, 0, 1);
            TelnetStream.Flush();
        }

        private enum _telnetParserStates
        {
            Normal,
            ExpectCommandCode,
            ExpectOption1,
        }
    }
}
