using System;
using System.Runtime.InteropServices;

namespace IoTPInvoke
{
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
