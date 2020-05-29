using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PS5000A
{
    public class Switch
    {
        public const int E_OK = 0;
        public const int E_TIMEOUT = 1;
        public const int E_CONNECTION = 2;
        public const int E_CONNECTION_LOST = 3;
        public const int E_TRANSMISSION_FAIL = 4;
        public SerialPort port;
        public string Receive_str()
        {
            if (port.BytesToRead > 0)
            {
                return port.ReadLine();
            }
            else
            {
                return "";
            }
        }
        public void OpenPort()
        {
            // получаем список доступных портов 
            string[] ports = SerialPort.GetPortNames();
            Console.WriteLine("Выберите порт:");
            // выводим список портов
            for (int i = 0; i < ports.Length; i++)
            {
                Console.WriteLine("[" + i.ToString() + "] " + ports[i].ToString());
            }
            port = new SerialPort();
            // читаем номер из консоли
            string n = Console.ReadLine();
            int num = int.Parse(n);
            try
            {
                // настройки порта
                port.PortName = ports[num];
                port.BaudRate = 57600;
                port.DataBits = 8;
                port.Parity = System.IO.Ports.Parity.None;
                port.StopBits = System.IO.Ports.StopBits.One;
                port.ReadTimeout = 1000;
                port.WriteTimeout = 1000;
                port.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: невозможно открыть порт:" + e.ToString());
                return;
            }
        }
        public void OpenPort(int num)
        {

            // получаем список доступных портов 
            string[] ports = SerialPort.GetPortNames();
            try
            {
                port = new SerialPort
                {
                    // настройки порта
                    PortName = ports[num],
                    BaudRate = 57600,
                    DataBits = 8,
                    Parity = System.IO.Ports.Parity.None,
                    StopBits = System.IO.Ports.StopBits.One,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                port.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: невозможно открыть порт:" + e.ToString());
                return;
            }
        }
        public void SendCmd(int status /*старшая цифра - команда */, int addr /*младшая цифра - адресс */)
        {
            port.Write(status.ToString() + addr.ToString());
        }
        //отправка команды ввиде строки
        public void SendCmd(string s)
        {
            port.Write(s);
        }
        public void SetOut(int addr)
        {
            SendCmd(0, addr);
        }
        public void SetIn(int addr)
        {
            SendCmd(1, addr);
        }
        //отправка команды из консоли
        // старшая цифра - команда младшая цифра - адресс 
        public void SendCmd2()
        {
            port.Write(Console.ReadLine());
        }
        public void ClosePort_()
        {
            if (port.IsOpen)
            {
                port.Close();
            }
        }

        public string GetAccepted()
        {
            if (port.BytesToRead > 0)
            {
                return port.ReadLine();
            }
            return "";
        }
    }

}
