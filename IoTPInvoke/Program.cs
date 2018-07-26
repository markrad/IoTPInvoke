using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IoTPInvoke
{
    class Program
    {
        static void Main(string[] args)
        {
            int ret;
            bool quit = false;
            int counter = 0;
            string connectionString = "<Connection String>";
            string test = Directory.GetCurrentDirectory();

            //IoTConnection_LL conn = new IoTConnection_LL(connectionString, IoTConnection_LL.Protocols.AMQP);
            //IoTConnection_LL conn = new IoTConnection_LL(connectionString, IoTConnection_LL.Protocols.AMQP_WebSocket);
            //IoTConnection_LL conn = new IoTConnection_LL(connectionString, IoTConnection_LL.Protocols.MQTT);
            IoTConnection_LL conn = new IoTConnection_LL(connectionString, IoTConnection_LL.Protocols.MQTT_WebSocket);

            // Optional - turn on SDK detailed logging with true
            ret = conn.SetLogging(false);

            // Callback for message confirmation
            conn.ConfirmationCallback += (o, i) =>
            {
                Console.WriteLine("Message calback with " + i.ToString());
            };

            // Callback for cloud to device call
            conn.MessageCallback += (o, i) =>
            {
                string message = i.ReceivedMessage.MessageType == IoTMessage.MessageTypes.String
                    ? i.ReceivedMessage.Message 
                    : i.ReceivedMessage.GetByteArrayAsString();

                Console.WriteLine("Received {0}", message);
                i.Success = true;
            };

            // Callback for direct method call
            conn.DeviceMethodCallback += (o, i) =>
            {
                Console.WriteLine("Method = {0}", i.Method);
                Console.WriteLine("Payload = {0}", Encoding.Default.GetString(i.Payload));
                i.Response = "{ \"success\": \"true\", \"message\": \"Method complete\"}";
                i.Result = 200;
            };

            // Optional - use proxy for WebSockets
            // ret = conn.SetProxy("<Proxy Address>", 8888);

            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Exiting");
                e.Cancel = true;
                quit = true;
            };

            int messageNumber = 0;

            while (!quit)
            {
                if (counter++ % 200 == 0)
                {
                    //conn.SendEvent("This is a test");
                    IoTMessage message = new IoTMessage("Message at : " + counter.ToString());

                    message["Number"] = messageNumber++.ToString();

                    string check = message["Number"];

                    if (check != (messageNumber - 1).ToString())
                        throw new InvalidOperationException("Values do not match");

                    check = message["novalue"];

                    if (check != null)
                        throw new InvalidOperationException("Unexpected value from nonexistent keyword");

                    message["Name"] = "Mark";
                    string[] keys = message.GetPropertyKeys();

                    conn.SendEvent(message);
                }
                conn.DoWork();

                Thread.Sleep(10);
            }
        }

        private static void Conn_ConfirmationCallback(object sender, ConfirmationCallbackEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
