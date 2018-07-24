using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace IoTPInvoke
{
    /// <summary>
    /// Defines the interface to the Azure IoT SDK in C
    /// </summary>
    class IoTWrapper
    {
        /// <summary>
        /// This is a hack. It is likely not portable between different versions of Windows. It calls the C
        /// runtime library malloc function since the SDK requires the client to malloc a buffer to return
        /// data when responding to a direct message.
        /// </summary>
        /// <param name="bytes">Number of bytes to allocate</param>
        /// <returns></returns>
        [DllImport("ucrtbased.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr malloc(int bytes);

        /// <summary>
        /// See malloc above. Frees bytes allocated by malloc. Not used but added for completeness.
        /// </summary>
        /// <param name="memory">Value returned by a call to malloc</param>
        [DllImport("ucrtbased.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void free(IntPtr memory);

        /// <summary>
        /// Used to specify web proxy information to be used by the SDK when communicating with the
        /// Azure IoT hub.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct ProxyData
        {
            public string host_address;
            public int port;
            public string username;
            public string password;
        }

        /// <summary>
        /// Function pointer to transport layer
        /// </summary>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate UIntPtr Protocol();

        /// <summary>
        /// Function pointer that will be called when the message confirmation is callback is invoked.
        /// </summary>
        /// <param name="result">Zero if the message was sent successfully</param>
        /// <param name="userContextCallback">User data passed to message send</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MessageConfirmationCallback(int result, UIntPtr userContextCallback);

        /// <summary>
        /// Function pointer called when a Cloud to Device message is received
        /// </summary>
        /// <param name="message">Payload</param>
        /// <param name="userContextCallback">User data passed when callback was established</param>
        /// <returns>Zero if message was processed</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int MessageCallback(UIntPtr message, UIntPtr userContextCallback);

        /// <summary>
        /// Function pointer called when a direct message is received
        /// </summary>
        /// <param name="method_name">Name of method to invoke</param>
        /// <param name="payload">Data</param>
        /// <param name="size">Size of data</param>
        /// <param name="response">This must be malloced and populated with response</param>
        /// <param name="response_size">Size of response</param>
        /// <param name="userContextCallback">User data passed when callback was established</param>
        /// <returns>200 for successful processing</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int DeviceMethodCallback(IntPtr method_name, IntPtr payload, int size, out IntPtr response, out int response_size, UIntPtr userContextCallback);

        // Next four functions are protocol specifiers. These are not called directly but passed to the SDK
        // to specify the protocol to use.

        [DllImport("iothub_client_dll.dll")]
        public static extern UIntPtr AMQP_Protocol();

        [DllImport("iothub_client_dll.dll")]
        public static extern UIntPtr AMQP_Protocol_over_WebSocketsTls();

        [DllImport("iothub_client_dll.dll")]
        public static extern UIntPtr HTTP_Protocol();

        [DllImport("iothub_client_dll.dll")]
        public static extern UIntPtr MQTT_Protocol();

        [DllImport("iothub_client_dll.dll")]
        public static extern UIntPtr MQTT_WebSocket_Protocol();

        /// <summary>
        /// Initialize the platform
        /// </summary>
        /// <returns></returns>
        [DllImport("aziotsharedutil.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int platform_init();

        /// <summary>
        /// Deinitialize the platform
        /// </summary>
        /// <returns></returns>
        [DllImport("aziotsharedutil.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int platform_deinit();

        /// <summary>
        /// Creates a new IoT hub handle
        /// </summary>
        /// <param name="connectionString">Device connection string</param>
        /// <param name="protocol">Protocol function</param>
        /// <returns>IoT Hub handle</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr IoTHubClient_LL_CreateFromConnectionString(
            [MarshalAs(UnmanagedType.LPStr)] string connectionString, 
            Protocol protocol);

        /// <summary>
        /// Set the option to use a web proxy.
        /// </summary>
        /// <param name="iotHubClientHandle">IoT Hub handle</param>
        /// <param name="optionName">Must be 'proxy_data'</param>
        /// <param name="proxyData">Pointer to proxy structure (ProxyData)</param>
        /// <returns>Zero is successful</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "IoTHubClient_LL_SetOption")]
        public static extern int IoTHubClient_LL_SetOption_Proxy(
            UIntPtr iotHubClientHandle,
            [MarshalAs(UnmanagedType.LPStr)] string optionName,
            ref ProxyData proxyData);

        /// <summary>
        /// Send a message to the IoT hub
        /// </summary>
        /// <param name="iotHubClientHandle">IoT Hub handle</param>
        /// <param name="eventMessageHandle">Message handle</param>
        /// <param name="eventConfirmationCallback">Callback function</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns></returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubClient_LL_SendEventAsync(
            UIntPtr iotHubClientHandle,
            UIntPtr eventMessageHandle,
            MessageConfirmationCallback eventConfirmationCallback,
            UIntPtr userContextCallback);

        /// <summary>
        /// Set callback for cloud to device message
        /// </summary>
        /// <param name="iotHubClientHandle">IoT Hub handle</param>
        /// <param name="messageCallback">Function to call</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns></returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubClient_LL_SetMessageCallback(
            UIntPtr iotHubClientHandle, 
            MessageCallback messageCallback, 
            UIntPtr userContextCallback);

        /// <summary>
        /// Specify function to call when a direct method is received
        /// </summary>
        /// <param name="iotHubClientHandle">IoT Hub handle</param>
        /// <param name="deviceMethodCallback">Function to call</param>
        /// <param name="userContextCallback">User data</param>
        /// <returns></returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubDeviceClient_LL_SetDeviceMethodCallback(
            UIntPtr iotHubClientHandle, 
            DeviceMethodCallback deviceMethodCallback, 
            UIntPtr userContextCallback);

        /// <summary>
        /// Function needs to be called frequently to drive socket communications
        /// </summary>
        /// <param name="iotHubClientHandle">IoT Hub handle</param>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void IoTHubClient_LL_DoWork(
            UIntPtr iotHubClientHandle);

        /// <summary>
        /// Close and destroy connection
        /// </summary>
        /// <param name="iotHubClientHandle">IoT Hub handle</param>
        /// <returns>Zero if successful</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubClient_LL_Destroy(
            UIntPtr iotHubClientHandle);

        /// <summary>
        /// Create a message to send
        /// </summary>
        /// <param name="source">Data to send</param>
        /// <returns>Zero if successful</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr IoTHubMessage_CreateFromString(
            [MarshalAs(UnmanagedType.LPStr)] string source);

        /// <summary>
        /// Get the message's data type
        /// </summary>
        /// <param name="iotHubMessageHandle">Message handle</param>
        /// <returns>String, Byte Array or Unknown</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_GetContentType(
            UIntPtr iotHubMessageHandle);

        /// <summary>
        /// Get the message content as a string
        /// </summary>
        /// <param name="iotHubMessageHandle"></param>
        /// <returns>String data</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IoTHubMessage_GetString(
            UIntPtr iotHubMessageHandle);

        /// <summary>
        /// Get the message data as a byte array
        /// </summary>
        /// <param name="iotHubMessageHandle">Message handle</param>
        /// <param name="buffer">Output buffer</param>
        /// <param name="size">Output size of buffer</param>
        /// <returns>Zero if successful</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_GetByteArray(
            UIntPtr iotHubMessageHandle, 
            out IntPtr buffer, 
            out int size);

        /// <summary>
        /// Clone an IoT message
        /// </summary>
        /// <param name="messageHandle">Handle to message to clone</param>
        /// <returns>Handle to copy of IoT message</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr IoTHubMessage_Clone(
            UIntPtr messageHandle);

        /// <summary>
        /// Retreive the MAP handle for the message properties
        /// </summary>
        /// <param name="messageHandle">Handle to the message</param>
        /// <returns>MAP handle of properties or NULL if failed</returns>
        /// <remarks>Currently the MAP functions have not been exposed so the MAP handle is useless - WIP</remarks> 
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr IoTHubMessage_Properties(
            UIntPtr messageHandle);

        /// <summary>
        /// Set a key/value pair on the message
        /// </summary>
        /// <param name="messageHandle">Handle to message</param>
        /// <param name="key">The key of which to set or reset the value</param>
        /// <param name="value">The new value</param>
        /// <returns>Zero if successful</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_SetProperty(
            UIntPtr messageHandle,
            [MarshalAs(UnmanagedType.LPStr)] string key,
            [MarshalAs(UnmanagedType.LPStr)] string value);

        /// <summary>
        /// Get the value of the property for the specified key
        /// </summary>
        /// <param name="messageHandle">Handle to message</param>
        /// <param name="key">Key name</param>
        /// <returns>String pointer</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IoTHubMessage_GetProperty(
            UIntPtr messageHandle,
            [MarshalAs(UnmanagedType.LPStr)] string key);

        /// <summary>
        /// Get the message id
        /// </summary>
        /// <param name="messageHandle">Message handle</param>
        /// <returns>IntPtr pointing to string</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IoTHubMessage_GetMessageId(
            UIntPtr messageHandle);

        /// <summary>
        /// Set the message id
        /// </summary>
        /// <param name="messageHandle">Message handle</param>
        /// <param name="messageId">Message id</param>
        /// <returns>Zero if successful</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_SetMessageId(
            UIntPtr messageHandle,
            [MarshalAs(UnmanagedType.LPStr)] string messageId);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IoTHubMessage_GetCorrelationId(
            UIntPtr messageHandle);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_SetCorrelationId(
            UIntPtr messageHandle,
            [MarshalAs(UnmanagedType.LPStr)] string correlationId);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IoTHubMessage_GetOutputName(
            UIntPtr messageHandle);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_SetOutputName(
            UIntPtr messageHandle,
            [MarshalAs(UnmanagedType.LPStr)] string outputName);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IoTHubMessage_GetInputName(
            UIntPtr messageHandle);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_SetInputName(
            UIntPtr messageHandle,
            [MarshalAs(UnmanagedType.LPStr)] string inputName);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IoTHubMessage_GetConnectionModuleId(
            UIntPtr messageHandle);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_SetConnectionModuleId(
            UIntPtr messageHandle,
            [MarshalAs(UnmanagedType.LPStr)] string connectionModuleId);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IoTHubMessage_GetConnectionDeviceId(
            UIntPtr messageHandle);

        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_SetConnectionDeviceId(
            UIntPtr messageHandle,
            [MarshalAs(UnmanagedType.LPStr)] string connectionDeviceId);

        /// <summary>
        /// Destroy a message
        /// </summary>
        /// <param name="iotHubMessageHandle">Message handle</param>
        /// <returns>Zero if successful</returns>
        [DllImport("iothub_client_dll.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IoTHubMessage_Destroy(UIntPtr iotHubMessageHandle);
    }
}
