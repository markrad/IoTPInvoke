using System;

namespace IoTPInvoke
{
    public class ConfirmationCallbackEventArgs : EventArgs
    {
        public ConfirmationCallbackEventArgs(int result, UIntPtr userContextCallback)
        {
            Result = result;
            UserContextCallback = userContextCallback;
        }

        public int Result { get; private set; }
        public UIntPtr UserContextCallback { get; private set; }

        public override string ToString() => "result=" + Result;
    }
}
