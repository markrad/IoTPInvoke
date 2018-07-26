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
    /// Wraps an Azure message
    /// </summary>
    public class IoTMessage : IDisposable
    {
        public enum MessageTypes
        {
            ByteArray,
            String,
            Unknown,
        };

        private string _message;
        private byte[] _byteMessage;
        private UIntPtr _messageHandle;
        private MessageTypes _messageType;
        private bool _disposed;
        private bool _owned;

        public IoTMessage(string message)
        {
            _message = message;
            _messageHandle = UIntPtr.Zero;
            _messageType = MessageTypes.String;
            _byteMessage = null;
            _owned = true;
        }

        public IoTMessage(UIntPtr messageHandle)
        {
            _messageHandle = messageHandle;
            _owned = false;
            _message = null;
            _byteMessage = null;

            _messageType = (MessageTypes)IoTHubMessage_GetContentType(_messageHandle);

            switch (_messageType)
            {
                case MessageTypes.String:
                    IntPtr pmsg = IoTHubMessage_GetString(_messageHandle);

                    if (pmsg == IntPtr.Zero)
                    {
                        Message = "";
                    }
                    else
                    {
                        Message = Marshal.PtrToStringAnsi(pmsg);
                    }
                    break;
                case MessageTypes.ByteArray:
                    IntPtr buffer = IntPtr.Zero;
                    int bufferLength = 0;
                    int ret = IoTHubMessage_GetByteArray(_messageHandle, out buffer, out bufferLength);

                    if (ret == 0)
                    {
                        _byteMessage = new byte[bufferLength];
                        Marshal.Copy(buffer, _byteMessage, 0, bufferLength);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unable to determine message type");
            }
        }

        public IoTMessage Clone()
        {
            IsDisposed();
            UIntPtr clone = IoTHubMessage_Clone(MessageHandle);

            if (clone == UIntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to clone IoT message");
            }

            return new IoTMessage(clone);
        }

        public string Message
        {
            get
            {
                IsDisposed();
                return _messageType == MessageTypes.String? _message : null;
            }

            set
            {
                IsDisposed();
                _message = value;
            }
        }

        public byte[] ByteMessage
        {
            get
            {
                IsDisposed();

                if (_messageType == MessageTypes.ByteArray)
                {
                    return _byteMessage;
                }
                else
                {
                    return Encoding.UTF8.GetBytes(_message);
                }
            }
        }

        public string GetByteArrayAsString()
        {
            switch (_messageType)
            {
                case MessageTypes.String:
                    return _message;
                case MessageTypes.ByteArray:
                    return Encoding.UTF8.GetString(_byteMessage);
                default:
                    return null;
            }
        }

        public string[] GetPropertyKeys()
        {
            IsDisposed();

            UIntPtr mapHandle = IoTHubMessage_Properties(MessageHandle);
            IntPtr keys;
            IntPtr values;
            int count;

            int res = IoTWrapper.Map_GetInternals(mapHandle, out keys, out values, out count);

            if (res != 0)
                throw new InvalidOperationException("Failed to acquire message properties: " + res.ToString());

            List<string> keyList = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                IntPtr work = IntPtr.Zero;

                work = Marshal.ReadIntPtr(keys, i * Marshal.SizeOf(work));
                string key = Marshal.PtrToStringAnsi(work);
                keyList.Add(key);
            }

            return keyList.ToArray();
        }

        public UIntPtr MessageHandle
        {
            get
            {
                IsDisposed();

                if (_messageHandle == UIntPtr.Zero)
                {
                    _messageHandle = IoTHubMessage_CreateFromString(_message);

                    if (_messageHandle == UIntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to create IotHub message");
                    }
                }

                return _messageHandle;
            }
        }

        public MessageTypes MessageType
        {
            get
            {
                IsDisposed();
                return _messageType;
            }
        }

        //public string GetValue(string key)

        public string this[string key]
        {
            get
            {
                IsDisposed();
                IntPtr value = IoTHubMessage_GetProperty(MessageHandle, key);

                return (value == IntPtr.Zero)
                    ? null
                    : Marshal.PtrToStringAnsi(value);
            }
            set
            {
                IsDisposed();
                int res = IoTHubMessage_SetProperty(MessageHandle, key, value);

                if (res != 0)
                    throw new InvalidOperationException("Failed to set message value: " + res.ToString());
            }
        }

        /// <summary>
        /// Get or set the message ID
        /// </summary>
        public string MessageId
        {
            get
            {
                IsDisposed();
                IntPtr messageId = IoTHubMessage_GetMessageId(MessageHandle);

                return (messageId == IntPtr.Zero)
                    ? null
                    : Marshal.PtrToStringAnsi(messageId);
            }
            set
            {
                IsDisposed();

                if (0 != IoTHubMessage_SetMessageId(MessageHandle, value))
                {
                    throw new InvalidOperationException("Failed to set message id");
                }
            }
        }

        public string CorrelationId
        {
            get
            {
                IsDisposed();
                IntPtr correlationId = IoTHubMessage_GetCorrelationId(MessageHandle);

                return (correlationId == IntPtr.Zero)
                    ? null
                    : Marshal.PtrToStringAnsi(correlationId);
            }
            set
            {
                IsDisposed();

                if (0 != IoTHubMessage_SetCorrelationId(MessageHandle, value))
                {
                    throw new InvalidOperationException("Failed to set correlation id");
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void IsDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("IoTMessage");
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

            if (_owned &&_messageHandle != UIntPtr.Zero)
            {
                ret = IoTHubMessage_Destroy(_messageHandle);
                _messageHandle = UIntPtr.Zero;
                _disposed = true;
            }
        }

        ~IoTMessage()
        {
            Dispose(false);
        }

    }
}
