using Newtonsoft.Json.Linq;
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

            // Create my reported device twin
            JObject twin = null;

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

                JObject input = JObject.Parse(Encoding.Default.GetString(i.Payload));

                Console.WriteLine("Payload = \r\n{0}", input.ToString());

                JObject response = new JObject(
                    new JProperty("success", "true"),
                    new JProperty("message", "Method complete"));

                i.Response = response.ToString();
                i.Result = 200;
            };

            conn.DeviceTwinCallback += (o, i) =>
            {
                // A little explanation here. 
                //
                // The first time the device connects the hub will send the complete reported and desired JSON document. One can
                // determine if it is a new connection if the JSON contains a "desired" element. In this instance one would
                // compare desired to reported, make changes as necessary and report back a status.
                // 
                // If the device twin is updated at the hub then an update will be sent. This will consist of the modified 
                // values. One would update the status and send a new report.
                //
                // Note that this code has been simplified to the bare minimum to demonstrate the logic flow.
                //
                // See https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-device-twins for further information.
                JObject payload = JObject.Parse(i.Payload);
                Console.WriteLine("Update state = {0}", i.UpdateState);
                Console.WriteLine("Payload = \r\n{0}", payload.ToString());

                try
                {
                    bool updateRequired = false;

                    // The first call after connection will be a complete JSON document for desired and reported
                    if (payload.ContainsKey("desired"))
                    {
                        twin = payload;

                        if ((string)payload["desired"]["mark"] != (string)twin["reported"]["mark"])
                        {
                            twin["reported"]["mark"] = (string)payload["desired"]["mark"];
                            twin["reported"]["$version"] = (string)payload["desired"]["$version"];
                            updateRequired = true;
                        }
                    }
                    else
                    {
                        if ((string)payload["mark"] != (string)twin["reported"]["mark"])
                        {
                            twin["reported"]["mark"] = (string)payload["mark"];
                            updateRequired = true;
                        }
                    }

                    if (updateRequired)
                    {
                        JObject report = new JObject(
                            new JProperty("mark", twin["reported"]["mark"].ToString()));

                        Console.WriteLine("Reporting \r\n{0}", report.ToString());
                        conn.SendReportedTwinState(report.ToString());
                    }
                }
                catch (NullReferenceException e)
                {
                    Console.WriteLine("Expected JSON element was not found or was wrong data type in twin request");
                }
            };

            conn.ReportedStateCallback += (o, i) =>
            {
                Console.WriteLine("Twin state callback = {0}", i.StatusCode);
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
    }
}
