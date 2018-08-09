namespace IoTPInvoke
{
    public class ClientConnectionStatusCallbackArgs
    {
        public enum IOTHUB_CLIENT_CONNECTION_STATUS
        {
            IOTHUB_CLIENT_CONNECTION_AUTHENTICATED,
            IOTHUB_CLIENT_CONNECTION_UNAUTHENTICATED
        }

        public enum IOTHUB_CLIENT_CONNECTION_STATUS_REASON
        {
            IOTHUB_CLIENT_CONNECTION_EXPIRED_SAS_TOKEN,            
            IOTHUB_CLIENT_CONNECTION_DEVICE_DISABLED,              
            IOTHUB_CLIENT_CONNECTION_BAD_CREDENTIAL,               
            IOTHUB_CLIENT_CONNECTION_RETRY_EXPIRED,                
            IOTHUB_CLIENT_CONNECTION_NO_NETWORK,                   
            IOTHUB_CLIENT_CONNECTION_COMMUNICATION_ERROR,          
            IOTHUB_CLIENT_CONNECTION_OK                            
        }

        public ClientConnectionStatusCallbackArgs(int result, int reason)
        {
            Result = (IOTHUB_CLIENT_CONNECTION_STATUS)result;
            Reason = (IOTHUB_CLIENT_CONNECTION_STATUS_REASON)reason;
        }

        public IOTHUB_CLIENT_CONNECTION_STATUS Result { get; private set; }
        public IOTHUB_CLIENT_CONNECTION_STATUS_REASON Reason { get; private set; }

        public override string ToString() => string.Format("result={0};reason={1}", Result, Reason);
    }
}