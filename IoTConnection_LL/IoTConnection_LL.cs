using System;
using System.Runtime.InteropServices;
using System.Text;
using static IoTPInvoke.IoTHubException;
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
        private ClientConnectionStatusCallback _clientConnectionStatusCallback;

        /// <summary>
        /// Protocols that can be used to connect to the Azure IoT Hub
        /// </summary>
        public enum Protocols
        {
            AMQP,
            AMQP_WebSocket,
            HTTP,
            MQTT,
            MQTT_WebSocket,
        }

        private const string OPTION_HTTP_PROXY = "proxy_data";
        private const string OPTION_LOG_TRACE = "logtrace";
        private const string OPTION_X509_CERT = "x509certificate";
        private const string OPTION_X509_PRIVATE_KEY = "x509privatekey";

        /// <summary>
        /// Event raised when a message has been either sent to the hub or an error occurred
        /// </summary>
        public event EventHandler<ConfirmationCallbackEventArgs> ConfirmationCallback;

        /// <summary>
        /// Event raised when a message is received from the cloud
        /// </summary>
        public event EventHandler<MessageCallbackArgs> MessageCallback;

        /// <summary>
        /// Event raised when a device method call is received from the cloud
        /// </summary>
        public event EventHandler<DeviceMethodCallbackArgs> DeviceMethodCallback;

        /// <summary>
        /// Event raised then a device twin update is received from the cloud
        /// </summary>
        public event EventHandler<DeviceTwinCallbackArgs> DeviceTwinCallback;

        /// <summary>
        /// Event raised when a device twin message has either been sent to the hub or an error occured
        /// </summary>
        public event EventHandler<ReportedStateCallbackArgs> ReportedStateCallback;

        /// <summary>
        /// Event raised when the connection status changes
        /// </summary>
        public event EventHandler<ClientConnectionStatusCallbackArgs> ClientConnectionStatusCallback;

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
            _clientConnectionStatusCallback = new ClientConnectionStatusCallback(clientConnectionStatusCallback);

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

            if (ret != 0)
            {
                throw new IoTHubException("Failed to set message callback", ret);
            }

            ret = IoTHubClient_LL_SetDeviceMethodCallback(_iotHandle, _deviceMethodCallback, userContextCallback);

            if (ret != 0)
            {
                throw new IoTHubException("Failed to set device method callback", ret);
            }
            ret = IoTHubClient_LL_SetDeviceTwinCallback(_iotHandle, _deviceTwinCallback, userContextCallback);

            if (ret != 0)
            {
                throw new IoTHubException("Failed to set device twin callback", ret);
            }
            ret = IoTHubClient_LL_SetConnectionStatusCallback(_iotHandle, clientConnectionStatusCallback, UIntPtr.Zero);

            if (ret != 0)
            {
                throw new IoTHubException("Failed to set client connection status callback", ret);
            }
        }

        /// <summary>
        /// Provide web proxy server information for WebSocket or HTTP connections
        /// </summary>
        /// <param name="proxyName">Name or IP address of web proxy</param>
        /// <param name="proxyPort">Port to use on proxy</param>
        /// <param name="proxyUserid">Optional proxy user identity</param>
        /// <param name="proxyPassword">Optional proxy password</param>
        /// <returns>Zero if successful</returns>
        public IOTHUB_CLIENT_RESULT SetProxy(string proxyName, int proxyPort, string proxyUserid = null, string proxyPassword = null)
        {
            IsDisposed();

            ProxyData proxyData = new ProxyData
            {
                host_address = proxyName,
                port = proxyPort,
                username = proxyUserid,
                password = proxyPassword
            };

            return (IOTHUB_CLIENT_RESULT)IoTHubClient_LL_SetOption_Proxy(_iotHandle, OPTION_HTTP_PROXY, ref proxyData);
        }

        /// <summary>
        /// Turn C SDK logging on or off
        /// </summary>
        /// <param name="traceValue">True to turn on logging; false to turn off</param>
        /// <returns>Zero if successful</returns>
        public IOTHUB_CLIENT_RESULT SetLogging(bool traceValue)
        {
            IsDisposed();

            return (IOTHUB_CLIENT_RESULT)IoTHubClient_LL_SetOption_Logging(_iotHandle, OPTION_LOG_TRACE, ref traceValue);
        }

        /// <summary>
        /// Pass certificate and private key for X.509 authentication
        /// </summary>
        /// <param name="certificate">The certificate to use</param>
        /// <param name="privateKey">The corresponding private key</param>
        /// <returns>IOTHUB_CLIENT_OK if successful</returns>
        public IOTHUB_CLIENT_RESULT SetCertificateAndKey(string certificate, string privateKey)
        {
            IsDisposed();

            IOTHUB_CLIENT_RESULT ret;

            ret = (IOTHUB_CLIENT_RESULT)IoTHubClient_LL_SetOption_X509_Certificate(_iotHandle, OPTION_X509_CERT, certificate);

            if (ret == IOTHUB_CLIENT_RESULT.IOTHUB_CLIENT_OK && privateKey != null)
            {
                ret = (IOTHUB_CLIENT_RESULT)IoTHubClient_LL_SetOption_X509_Private_Key(_iotHandle, OPTION_X509_PRIVATE_KEY, privateKey);
            }

            return ret;
        }

        /// <summary>
        /// Send string message to IoT hub
        /// </summary>
        /// <param name="message">Message content</param>
        /// <returns>Zero if successful</returns>
        public IOTHUB_CLIENT_RESULT SendEvent(string message)
        {
            return SendEvent(message, UIntPtr.Zero);
        }

        /// <summary>
        /// Send string message to IoT hub
        /// </summary>
        /// <param name="message">Message content</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns>Zero if successful</returns>
        public IOTHUB_CLIENT_RESULT SendEvent(string message, UIntPtr userContextCallback)
        {
            IOTHUB_CLIENT_RESULT ret;
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
        public IOTHUB_CLIENT_RESULT SendEvent(IoTMessage message)
        {
            return SendEvent(message, UIntPtr.Zero);
        }

        /// <summary>
        /// Send an IoT message to the IoT hub
        /// </summary>
        /// <param name="message">IoT message wrapper</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns>Zero if successful</returns>
        public IOTHUB_CLIENT_RESULT SendEvent(IoTMessage message, UIntPtr userContextCallback)
        {
            IsDisposed();

            return (IOTHUB_CLIENT_RESULT)IoTHubClient_LL_SendEventAsync(_iotHandle, message.MessageHandle, _messageConfirmationCallback, userContextCallback);
        }

        /// <summary>
        /// Send reported twin state to IoT hub
        /// </summary>
        /// <param name="reportedState">State as JSON string</param>
        /// <returns>Zero if successful</returns>
        public IOTHUB_CLIENT_RESULT SendReportedTwinState(string reportedState)
        {
            return (IOTHUB_CLIENT_RESULT)SendReportedTwinState(reportedState, UIntPtr.Zero);
        }

        /// <summary>
        /// Send reported twin state to IoT hub
        /// </summary>
        /// <param name="reportedState">State as JSON string</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns>Zero if successful</returns>
        public IOTHUB_CLIENT_RESULT SendReportedTwinState(string reportedState, UIntPtr userContextCallback)
        {
            IsDisposed();

            return (IOTHUB_CLIENT_RESULT)IoTHubClient_LL_SendReportedState(_iotHandle, reportedState, reportedState.Length, _reportedStateCallback, userContextCallback);
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
        public IOTHUB_CLIENT_RESULT Close()
        {
            IsDisposed();

            IOTHUB_CLIENT_RESULT ret = (IOTHUB_CLIENT_RESULT)IoTWrapper.IoTHubClient_LL_Destroy(_iotHandle);

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

        private void OnClientConnectionStatusCallback(ClientConnectionStatusCallbackArgs e)
        {
            EventHandler<ClientConnectionStatusCallbackArgs> handler = ClientConnectionStatusCallback;

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
            ret = (int)Close();
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

            // This call to malloc may not be reliable but is forced upon the code by the C SDK
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

        private void clientConnectionStatusCallback(int result, int reason, UIntPtr userContextCallback)
        {
            ClientConnectionStatusCallbackArgs connectionStatusCallbackArgs = new ClientConnectionStatusCallbackArgs(result, reason);

            OnClientConnectionStatusCallback(connectionStatusCallbackArgs);
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
}
