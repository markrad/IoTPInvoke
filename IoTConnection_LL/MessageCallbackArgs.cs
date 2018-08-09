using System;

namespace IoTPInvoke
{
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
}
