using System;

namespace IoTPInvoke
{
    [Serializable]
    public class IoTHubException : Exception
    {
        public enum IOTHUB_CLIENT_RESULT
        {
            IOTHUB_CLIENT_OK,
            IOTHUB_CLIENT_INVALID_ARG,
            IOTHUB_CLIENT_ERROR,
            IOTHUB_CLIENT_INVALID_SIZE,
            IOTHUB_CLIENT_INDEFINITE_TIME
        }

        public IOTHUB_CLIENT_RESULT Result { get; private set; }

        public IoTHubException(string message, int ret) : base(message) => Result = (IOTHUB_CLIENT_RESULT)ret;
    }
}