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
    public class IoTConnection_LL : IDisposable
    {
        // TODO: Add enums for return codes to match those of the C SDK
        private string _connectionString;
        private Protocols _protocol;
        UIntPtr _iotHandle;
        bool _disposed = false;

        private MessageConfirmationCallback _messageConfirmationCallback;
        private MessageCallback _messageCallback;
        private ReportedStateCallback _reportedStateCallback;
        private DeviceMethodCallback _deviceMethodCallback;
        private DeviceTwinCallback _deviceTwinCallback;

        public enum Protocols
        {
            AMQP,
            AMQP_WebSocket,
            HTTP,
            MQTT,
            MQTT_WebSocket,
        }

        public const string OPTION_HTTP_PROXY = "proxy_data";
        public const string OPTION_LOG_TRACE = "logtrace";

        public event EventHandler<ConfirmationCallbackEventArgs> ConfirmationCallback;
        public event EventHandler<MessageCallbackArgs> MessageCallback;
        public event EventHandler<DeviceMethodCallbackArgs> DeviceMethodCallback;
        public event EventHandler<DeviceTwinCallbackArgs> DeviceTwinCallback;
        public event EventHandler<ReportedStateCallbackArgs> ReportedStateCallback;

        /// <summary>
        /// Create an instance of the IoTConnection_LL object
        /// </summary>
        /// <param name="connectionString">Azure IoT device connection string</param>
        /// <param name="protocol">Required communicaton protocol</param>
        public IoTConnection_LL(string connectionString, Protocols protocol) : 
            this(connectionString, protocol, UIntPtr.Zero)
        {
            
        }

        /// <summary>
        /// Create an instance of the IoTConnection_LL object
        /// </summary>
        /// <param name="connectionString">Azure IoT device connection string</param>
        /// <param name="protocol">Required communicaton protocol</param>
        /// <param name="userContextCallback">User data</param>
        public IoTConnection_LL(string connectionString, Protocols protocol, UIntPtr userContextCallback) 
        {
            _connectionString = connectionString;
            _protocol = protocol;
            _iotHandle = UIntPtr.Zero;

            _messageConfirmationCallback = new MessageConfirmationCallback(confirmationCallback);
            _messageCallback = new MessageCallback(messageReceived);
            _reportedStateCallback = new ReportedStateCallback(reportedStateCallback);
            _deviceMethodCallback = new DeviceMethodCallback(deviceMethodReceived);
            _deviceTwinCallback = new DeviceTwinCallback(deviceTwinCallback);

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

            ret = IoTHubClient_LL_SetMessageCallback(_iotHandle, _messageCallback, userContextCallback);
            ret = IoTHubClient_LL_SetDeviceMethodCallback(_iotHandle, _deviceMethodCallback, userContextCallback);
            ret = IoTHubClient_LL_SetDeviceTwinCallback(_iotHandle, _deviceTwinCallback, userContextCallback);
        }

        /// <summary>
        /// Provide web proxy server information for WebSocket or HTTP connections
        /// </summary>
        /// <param name="proxyName">Name or IP address of web proxy</param>
        /// <param name="proxyPort">Port to use on proxy</param>
        /// <param name="proxyUserid">Optional proxy user identity</param>
        /// <param name="proxyPassword">Optional proxy password</param>
        /// <returns>Zero if successful</returns>
        public int SetProxy(string proxyName, int proxyPort, string proxyUserid = null, string proxyPassword = null)
        {
            IsDisposed();

            ProxyData proxyData = new ProxyData
            {
                host_address = proxyName,
                port = proxyPort,
                username = proxyUserid,
                password = proxyPassword
            };

            return IoTHubClient_LL_SetOption_Proxy(_iotHandle, OPTION_HTTP_PROXY, ref proxyData);
        }

        /// <summary>
        /// Turn C SDK logging on or off
        /// </summary>
        /// <param name="traceValue">True to turn on logging; false to turn off</param>
        /// <returns>Zero if successful</returns>
        public int SetLogging(bool traceValue)
        {
            IsDisposed();

            return IoTHubClient_LL_SetOption_Logging(_iotHandle, OPTION_LOG_TRACE, ref traceValue);
        }

        /// <summary>
        /// Send string message to IoT hub
        /// </summary>
        /// <param name="message">Message content</param>
        /// <returns>Zero if successful</returns>
        public int SendEvent(string message)
        {
            return SendEvent(message, UIntPtr.Zero);
        }

        /// <summary>
        /// Send string message to IoT hub
        /// </summary>
        /// <param name="message">Message content</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns>Zero if successful</returns>
        public int SendEvent(string message, UIntPtr userContextCallback)
        {
            int ret;
            IoTMessage iotMessage = new IoTMessage(message);

            ret =  SendEvent(iotMessage, userContextCallback);
            iotMessage.Dispose();

            return ret;
        }

        /// <summary>
        /// Send an IoT message to the IoT hub
        /// </summary>
        /// <param name="message">IoT message wrapper</param>
        /// <returns>Zero if successful</returns>
        public int SendEvent(IoTMessage message)
        {
            return SendEvent(message, UIntPtr.Zero);
        }

        /// <summary>
        /// Send an IoT message to the IoT hub
        /// </summary>
        /// <param name="message">IoT message wrapper</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns>Zero if successful</returns>
        public int SendEvent(IoTMessage message, UIntPtr userContextCallback)
        {
            IsDisposed();

            return IoTHubClient_LL_SendEventAsync(_iotHandle, message.MessageHandle, _messageConfirmationCallback, userContextCallback);
        }

        /// <summary>
        /// Send reported twin state to IoT hub
        /// </summary>
        /// <param name="reportedState">State as JSON string</param>
        /// <returns>Zero if successful</returns>
        public int SendReportedTwinState(string reportedState)
        {
            return SendReportedTwinState(reportedState, UIntPtr.Zero);
        }

        /// <summary>
        /// Send reported twin state to IoT hub
        /// </summary>
        /// <param name="reportedState">State as JSON string</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns>Zero if successful</returns>
        public int SendReportedTwinState(string reportedState, UIntPtr userContextCallback)
        {
            IsDisposed();

            return IoTHubClient_LL_SendReportedState(_iotHandle, reportedState, reportedState.Length, _reportedStateCallback, userContextCallback);
        }

        /// <summary>
        /// Causes the C SDK to send any outstanding messages and receive any data from the IoT hub.
        /// This function should be called frequently such as about every 10ms.
        /// </summary>
        public void DoWork()
        {
            IsDisposed();

            IoTHubClient_LL_DoWork(_iotHandle);
        }

        /// <summary>
        /// Close the connection to the IoT hub
        /// </summary>
        /// <returns>Zero if successful</returns>
        public int Close()
        {
            IsDisposed();

            int ret = IoTWrapper.IoTHubClient_LL_Destroy(_iotHandle);

            return ret;
        }

        /// <summary>
        /// Recover resource used by this class prior to destruction
        /// </summary>
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

        protected virtual void OnReportedStateCallback(ReportedStateCallbackArgs e)
        {
            EventHandler<ReportedStateCallbackArgs> handler = ReportedStateCallback;

            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDeviceTwinCallback(DeviceTwinCallbackArgs e)
        {
            EventHandler<DeviceTwinCallbackArgs> handler = DeviceTwinCallback;

            if (handler != null)
            {
                handler(this, e);
            }
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

        private void IsDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("IoTConnection_LL");
        }


        private void confirmationCallback(int result, UIntPtr userContextCallback)
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

        private void deviceTwinCallback(int update_state, IntPtr payload, int size, UIntPtr userContextCallback)
        {
            string strPayload = Marshal.PtrToStringAnsi(payload, size);
            DeviceTwinCallbackArgs deviceTwinCallbackArgs = new DeviceTwinCallbackArgs(update_state, strPayload, userContextCallback);

            OnDeviceTwinCallback(deviceTwinCallbackArgs);
        }

        private void reportedStateCallback(int status_code, UIntPtr userContextCallback)
        {
            ReportedStateCallbackArgs reportedStateCallbackArgs = new ReportedStateCallbackArgs(status_code, userContextCallback);

            OnReportedStateCallback(reportedStateCallbackArgs);
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

    public class ReportedStateCallbackArgs : EventArgs
    {
        public int StatusCode { get; private set; }
        public UIntPtr UserContextCallback { get; private set; }
        public ReportedStateCallbackArgs(int statusCode, UIntPtr userContextCallback)
        {
            StatusCode = statusCode;
            UserContextCallback = userContextCallback;
        }
    }

    public class DeviceTwinCallbackArgs : EventArgs
    {
        public enum UpdateStateValue
        {
            Complete,
            Partial,
        }

        public UpdateStateValue UpdateState { get; private set; }
        public string Payload { get; private set; }
        public UIntPtr UserContextCallback { get; private set; }

        public DeviceTwinCallbackArgs(int updateState, string payload, UIntPtr userContextCallback)
        {
            UpdateState = (UpdateStateValue)updateState;
            Payload = payload;
            UserContextCallback = userContextCallback;
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
