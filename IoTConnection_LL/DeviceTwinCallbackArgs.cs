using System;

namespace IoTPInvoke
{
    public class DeviceTwinCallbackArgs : EventArgs
    {
        public enum UpdateStateValue
        {
            Complete,
            Partial,
        }

        public DeviceTwinCallbackArgs(int updateState, string payload, UIntPtr userContextCallback)
        {
            UpdateState = (UpdateStateValue)updateState;
            Payload = payload;
            UserContextCallback = userContextCallback;
        }

        public UpdateStateValue UpdateState { get; private set; }
        public string Payload { get; private set; }
        public UIntPtr UserContextCallback { get; private set; }

        public override string ToString() => string.Format("updateState={0};payload={1}", UpdateState.ToString(), Payload);
    }
}
