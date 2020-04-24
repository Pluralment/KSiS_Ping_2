using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Lab2
{
    class Program
    {
        static readonly int maxTTL = 30;

        static void Main()
        {
            Console.Write("Domain: ");
            string HostName = Console.ReadLine();
            Program.UsrTrasert(HostName);
            Console.ReadKey();
        }

        static void UsrTrasert(string HostName)
        {
            Socket host = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            host.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000);

            IPHostEntry HostData = null;
            IPEndPoint IPendPoint = null;
            EndPoint endPoint = null;
            try
            {
                // Получение списка IP-адресов, за которыми закреплен домен.
                HostData = Dns.GetHostEntry(HostName);
                IPendPoint = new IPEndPoint(HostData.AddressList[0], 25000);
                endPoint = new IPEndPoint(HostData.AddressList[0], 25000);
                Console.WriteLine("Starting to send packets to " + endPoint.ToString());
                Console.WriteLine();
            }
            catch
            {
                Console.WriteLine();
                Console.WriteLine("Wrong domain or another issue occured");
                Environment.Exit(0);
            }

            IcmpPacket icmpPacket = new IcmpPacket();
            // Длина блока данных = 2 (ID) + 2 (SeqNum) + Length(MessageData)
            icmpPacket.DataLength = 4 + icmpPacket.MsgData.Length;   
            // Длина пакета = 1 (Type) + 1 (Code) + 2 (Checksum) + DataLength
            int PkgSize = 4 + icmpPacket.DataLength;
            icmpPacket.Checksum = icmpPacket.GetChecksum();

            for (int ttl = 1; ttl <= maxTTL; ttl++)
            {
                host.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);
                int badAttempt = 0;   
                int PkgLength = 0;

                try
                {
                    DateTime timestart;
                    TimeSpan[] MsgArrivalTime = new TimeSpan[3];
                    byte[] data = null;
                    for (int i = 0; i <= 2; i++)
                    {
                        timestart = DateTime.Now;
                        host.SendTo(icmpPacket.ToBytes(), PkgSize, SocketFlags.None, IPendPoint);
                        data = new byte[1024];
                        PkgLength = host.ReceiveFrom(data, ref endPoint);
                        MsgArrivalTime[i] = DateTime.Now - timestart;
                    }

                    // Проверка типа полученного пакета.
                    // 11 - ttl expired.
                    if (data[20] == 11)
                    {
                        Console.WriteLine(ttl + "     " + (MsgArrivalTime[0].Milliseconds.ToString())
                            + " мс     " + (MsgArrivalTime[1].Milliseconds.ToString()) + " мс     " + 
                            (MsgArrivalTime[2].Milliseconds.ToString()) + " мс:    " + endPoint.ToString());
                        Console.WriteLine();
                    }

                    // 0 - echo-answer.
                    if (data[20] == 0)
                    {
                        Console.WriteLine(ttl + "     " + (MsgArrivalTime[0].Milliseconds.ToString())
                            + " мс     " + (MsgArrivalTime[1].Milliseconds.ToString()) + " мс     " + 
                            (MsgArrivalTime[2].Milliseconds.ToString()) + " мс:    " + endPoint.ToString() + ":   the target is reached");
                        break;
                    }
                }
                catch (SocketException)
                {
                    Console.WriteLine(ttl + "     no answer from " + endPoint);
                    Console.WriteLine();
                    badAttempt++;

                    if (badAttempt == 3)
                    {
                        Console.WriteLine("Impossible to reach the host");
                    }
                }
            }
            host.Close();
        }

        class IcmpPacket
        {
            public byte Type;
            public byte Code;
            public ushort Checksum;
            public ushort ID;
            public ushort SeqNum;
            // Передаваемое сообщение
            public byte[] MsgData;
            // Длина передаваемого сообщения + IP и SeqNum
            public int DataLength;

            public IcmpPacket()
            {
                // Установка значений ICMP-пакета для отправки эхо-запроса.
                Type = 8;
                // Обнуление контрольной суммы для её последующего подсчета.
                Code = 0;
                Checksum = 0;        
                ID = 0;
                SeqNum = 0;
                MsgData = Encoding.ASCII.GetBytes("PING");
            }

            public IcmpPacket(byte[] data, int size)
            {
                Type = data[20];
                Code = data[21];
                // Чтение 22 и 23 байтов (Checksum).
                Checksum = BitConverter.ToUInt16(data, 22);
                DataLength = size - 24;
                MsgData = data;
            }

            public ushort GetChecksum() 
            {
                uint checksum = 0; 
                byte[] data = ToBytes();
                int packetsize = DataLength + 4;
                int index = 0;

                while (index < packetsize)
                {
                    checksum += Convert.ToUInt32(BitConverter.ToUInt16(data, index)); 
                    index += 2;
                }
                return (ushort)(~checksum);
            }

            // Формирование ICMP-пакета в виде массива байтов.
            public byte[] ToBytes()
            {
                // byte[DataLength + 1(Type) + 1(Code) + 2(Checksum)].
                byte[] data = new byte[DataLength + 4];
                Buffer.BlockCopy(BitConverter.GetBytes(Type), 0, data, 0, 1);
                Buffer.BlockCopy(BitConverter.GetBytes(Code), 0, data, 1, 1);
                Buffer.BlockCopy(BitConverter.GetBytes(Checksum), 0, data, 2, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(ID), 0, data, 4, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(SeqNum), 0, data, 6, 2);
                Buffer.BlockCopy(MsgData, 0, data, 8, (DataLength - 4));
                return data;
            }
        }
    }
}