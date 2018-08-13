using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using static IoTPInvoke.IoTHubException;

namespace IoTPInvoke
{
    class Program
    {
        //private const string CONNECTIONSTRING = "<Connection String>";
        private const string CONNECTIONSTRING = "HostName=MarkRadHub2.azure-devices.net;DeviceId=X509SSTest1;x509=true";
        static void Main(string[] args)
        {
            IOTHUB_CLIENT_RESULT ret;
            bool quit = false;
            int counter = 0;

            // Create my reported device twin
            JObject twin = null;

            //IoTConnection_LL.Protocols protocol = IoTConnection_LL.Protocols.AMQP;
            //IoTConnection_LL.Protocols protocol = IoTConnection_LL.Protocols.AMQP_WebSocket;
            //IoTConnection_LL.Protocols protocol = IoTConnection_LL.Protocols.MQTT;
            IoTConnection_LL.Protocols protocol = IoTConnection_LL.Protocols.MQTT_WebSocket;

            IoTConnection_LL conn = new IoTConnection_LL(CONNECTIONSTRING, protocol);

            // Optional - turn on SDK detailed logging with true
            ret = conn.SetLogging(false);

            // Optional - route via a web proxy
            //ret = conn.SetProxy("proxyname", 9999, null, null);

            // For MQTT use URL encode and decode
            if (protocol ==  IoTConnection_LL.Protocols.MQTT || protocol == IoTConnection_LL.Protocols.MQTT_WebSocket)
            {
                ret = conn.SetUrlEncodeDecode(true);
            }

            // Required for X.509 authentication
            string certificate;
            string privateKey;

            // Certificates can be hardcoded as in this example
            /*
                        certificate = string.Join(Environment.NewLine,
            "-----BEGIN CERTIFICATE-----",
            "<<< SNIP >>>",
            "-----END CERTIFICATE-----");

                        privateKey = string.Join(Environment.NewLine,
            "-----BEGIN RSA PRIVATE KEY-----",
            "<<< SNIP >>>",
            "-----END RSA PRIVATE KEY-----");
            */

            // or certificates can be read from a file for my flexibility
            certificate = File.ReadAllText(@"C:\Users\markrad\OneDrive\Documents\AzureSamples\Certificates\MarkRadHub2_X509SSTest1_Certs\public1.cer");
            privateKey = File.ReadAllText(@"C:\Users\markrad\OneDrive\Documents\AzureSamples\Certificates\MarkRadHub2_X509SSTest1_Certs\private1.key");
            ret = conn.SetCertificateAndKey(certificate, privateKey);

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
                    Console.WriteLine("Expected JSON element was not found or was wrong data type in twin request: " + e.Message);
                }
            };

            conn.ReportedStateCallback += (o, i) =>
            {
                Console.WriteLine("Twin state callback = {0}", i.StatusCode);
            };

            conn.ClientConnectionStatusCallback += (o, i) =>
            {
                Console.WriteLine(i.ToString());
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
            bool flipflop = true;

            while (!quit)
            {
                if (counter++ % 200 == 0)
                {
                    flipflop ^= true;

                    IoTMessage message = new IoTMessage("Message at : " + counter.ToString());

                    message["Number"] = messageNumber++.ToString();

                    Debug.Assert(message["Number"] == (messageNumber - 1).ToString());
                    Debug.Assert(message["novalue"] == null);

                    message["Name"] = flipflop
//                        ? Uri.EscapeDataString("Mark+Radbourne")
                        ? "Mark+Radbourne"
                        : "MarkRadbourne";
                    Console.WriteLine("Name=" + message["Name"]);
                    string[] keys = message.GetPropertyKeys();

                    Debug.Assert(keys.Length == 2 && keys.Contains("Number") && keys.Contains("Name"));

                    conn.SendEvent(message);
                }
                conn.DoWork();

                Thread.Sleep(10);
            }
        }
    }
}
