using System;

namespace IoTPInvoke
{
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
}
