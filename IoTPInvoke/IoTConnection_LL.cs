using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static IoTPInvoke.IoTWrapper;

namespace IoTPInvoke
{
    /// <summary>
    /// Wraps an Azure connection
    /// </summary>
    class IoTConnection_LL : IDisposable
    {
        private string _connectionString;
        private Protocols _protocol;
        UIntPtr _iotHandle;
        bool _disposed = false;

        public enum Protocols
        {
            AMQP,
            AMQP_WebSocket,
            HTTP,
            MQTT,
            MQTT_WebSocket,
        }

        public const string OPTION_HTTP_PROXY = "proxy_data";

        public event EventHandler<ConfirmationCallbackEventArgs> ConfirmationCallback;
        public event EventHandler<MessageCallbackArgs> MessageCallback;
        public event EventHandler<DeviceMethodCallbackArgs> DeviceMethodCallback;

        public IoTConnection_LL(string connectionString, Protocols protocol) : 
            this(connectionString, protocol, UIntPtr.Zero)
        {
            
        }

        public IoTConnection_LL(string connectionString, Protocols protocol, UIntPtr userContextCallback) 
        {
            _connectionString = connectionString;
            _protocol = protocol;
            _iotHandle = UIntPtr.Zero;

            int ret;

            ret = IoTWrapper.platform_init();

            if (ret != 0)
            {
                throw new InvalidOperationException("Failed to intialize the platform: " + ret);
            }

            _iotHandle = IoTHubClient_LL_CreateFromConnectionString(_connectionString, getProtocolFunction(protocol));

            if (_iotHandle == UIntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create IoT handle");
            }

            ret = IoTHubClient_LL_SetMessageCallback(_iotHandle, messageReceived, userContextCallback);
            ret = IoTHubDeviceClient_LL_SetDeviceMethodCallback(_iotHandle, deviceMethodReceived, userContextCallback);
        }

        public int SetProxy(string proxyName, int proxyPort, string proxyUserid = null, string proxyPassword = null)
        {
            ProxyData proxyData = new ProxyData
            {
                host_address = proxyName,
                port = proxyPort,
                username = proxyUserid,
                password = proxyPassword
            };

            return IoTHubClient_LL_SetOption_Proxy(_iotHandle, OPTION_HTTP_PROXY, ref proxyData);
        }

        public int SendEvent(string message)
        {
            return SendEvent(message, UIntPtr.Zero);
        }

        public int SendEvent(string message, UIntPtr userContextCallback)
        {
            int ret;
            IoTMessage iotMessage = new IoTMessage(message);

            ret =  SendEvent(iotMessage, userContextCallback);
            iotMessage.Dispose();

            return ret;
        }

        public int SendEvent(IoTMessage message)
        {
            return SendEvent(message, UIntPtr.Zero);
        }

        public int SendEvent(IoTMessage message, UIntPtr userContextCallback)
        {
            return IoTHubClient_LL_SendEventAsync(_iotHandle, message.MessageHandle, eventConfirmationCallback, userContextCallback);
        }

        public void DoWork()
        {
            IoTHubClient_LL_DoWork(_iotHandle);
        }

        public int Close()
        {
            int ret = IoTWrapper.IoTHubClient_LL_Destroy(_iotHandle);

            return ret;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void OnConfirmationCallback(ConfirmationCallbackEventArgs e)
        {
            EventHandler<ConfirmationCallbackEventArgs> handler = ConfirmationCallback;

            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual int OnMessageReceived(MessageCallbackArgs e)
        {
            EventHandler<MessageCallbackArgs> handler = MessageCallback;

            if (handler != null)
            {
                handler(this, e);
            }

            return e.Success ? 0 : 1;
        }

        protected virtual int OnDeviceMethodReceived(DeviceMethodCallbackArgs e)
        {
            EventHandler<DeviceMethodCallbackArgs> handler = DeviceMethodCallback;

            if (handler != null)
            {
                handler(this, e);
            }

            return e.Result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Nothing to do here - typically free managed objects
            }

            int ret;

            // Could check for errors here if we care
            ret = Close();
            ret = IoTWrapper.platform_deinit();
            _disposed = true;
        }

        ~IoTConnection_LL()
        {
            Dispose(false);
        }

        private void eventConfirmationCallback(int result, UIntPtr userContextCallback)
        {
            OnConfirmationCallback(new ConfirmationCallbackEventArgs(result, userContextCallback));
        }

        private int messageReceived(UIntPtr message, UIntPtr userContextCallback)
        {
            return OnMessageReceived(new MessageCallbackArgs(new IoTMessage(message), userContextCallback));
        }

        private int deviceMethodReceived(IntPtr method, IntPtr payload, int size, out IntPtr response, out int response_size, UIntPtr userContextCallback)
        {
            string strMethod = Marshal.PtrToStringAnsi(method);
            DeviceMethodCallbackArgs deviceMethodCallbackArgs = new DeviceMethodCallbackArgs(strMethod, payload, size, userContextCallback);

            int result = OnDeviceMethodReceived(deviceMethodCallbackArgs);
            int length = deviceMethodCallbackArgs.Response.Length + 1;
            byte[] work = Encoding.UTF8.GetBytes(deviceMethodCallbackArgs.Response);


            response = malloc(work.Length);
            Marshal.Copy(work, 0, response, work.Length);
            response_size = work.Length;

            return result;
        }

        private Protocol getProtocolFunction(Protocols protocol)
        {
            switch (protocol)
            {
                case Protocols.AMQP:
                    return IoTWrapper.AMQP_Protocol;
                case Protocols.AMQP_WebSocket:
                    return IoTWrapper.AMQP_Protocol_over_WebSocketsTls;
                case Protocols.HTTP:
                    return IoTWrapper.HTTP_Protocol;
                case Protocols.MQTT:
                    return IoTWrapper.MQTT_Protocol;
                case Protocols.MQTT_WebSocket:
                    return IoTWrapper.MQTT_WebSocket_Protocol;
                default:
                    throw new ArgumentException("Invalid protocol", "protocol");
            }
        }
    }

    public class ConfirmationCallbackEventArgs : EventArgs
    {
        public int Result { get; private set; }
        public UIntPtr UserContextCallback { get; private set; }
        public ConfirmationCallbackEventArgs(int result, UIntPtr userContextCallback)
        {
            Result = result;
            UserContextCallback = userContextCallback;
        }

        public override string ToString()
        {
            return "result=" + Result;
        }
    }

    public class MessageCallbackArgs : EventArgs
    {
        public MessageCallbackArgs(IoTMessage message, UIntPtr userContextCallback)
        {
            ReceivedMessage = message;
            UserContextCallback = userContextCallback;
            Success = true;
        }

        public IoTMessage ReceivedMessage { get; private set; }
        public UIntPtr UserContextCallback { get; private set; }
        public bool Success { get; set; }
    }

    public class DeviceMethodCallbackArgs : EventArgs
    {
        public DeviceMethodCallbackArgs(string method, IntPtr payload, int size, UIntPtr userContextCallback)
        {
            byte[] work = new byte[size];
            Method = method;
            Marshal.Copy(payload, work, 0, size);
            Payload = work;
            Result = 200;
            Response = "No handler installed";
        }

        public string Method { get; private set; }
        public byte[] Payload { get; private set; }
        public UIntPtr UserContextCallback { get; private set; }
        public int Result { get; set; }
        public string Response { get; set; }
    }
}
