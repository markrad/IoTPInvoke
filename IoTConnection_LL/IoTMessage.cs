using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static IoTPInvoke.IoTWrapper;

namespace IoTPInvoke
{
    /// <summary>
    /// Wraps an Azure message
    /// </summary>
    public class IoTMessage : IDisposable
    {
        /// <summary>
        /// Possible data types in an IoT message
        /// </summary>
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

        /// <summary>
        /// Construct a new IoT message from a string
        /// </summary>
        /// <param name="message">Message content</param>
        public IoTMessage(string message)
        {
            _message = message;
            _messageHandle = UIntPtr.Zero;
            _messageType = MessageTypes.String;
            _byteMessage = null;
            _owned = true;
        }

        /// <summary>
        /// Construct an instance of this class from an existing IoT message handle
        /// </summary>
        /// <param name="messageHandle">Message handle</param>
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

        /// <summary>
        /// Clone the message
        /// </summary>
        /// <remarks>
        /// The new instance will destory the message handle when disposed
        /// </remarks>
        /// <returns>New instance of IoTMessage for clone</returns>
        public IoTMessage Clone()
        {
            IsDisposed();
            UIntPtr clone = IoTHubMessage_Clone(MessageHandle);

            if (clone == UIntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to clone IoT message");
            }

            IoTMessage ret = new IoTMessage(clone);

            // Since the new message handle is completely invisible to the client
            // set this to owned so it is released when the instance is destroyed
            ret.Owned = true;

            return new IoTMessage(clone);
        }

        /// <summary>
        /// Gets and sets the owned flag - true for instances created from a string and typically false for instances created from a handle. <see cref="Clone()"/> for exception.
        /// </summary>
        protected bool Owned
        {
            get { return _owned; }
            set { _owned = value; }
        }

        /// <summary>
        /// Gets the message string if the instance is a string type otherwise null. Message can only be set prior to creating the handle
        /// </summary>
        /// <remarks>
        /// The message handle is created when MessageHandle is first referenced or if the class was constructed with a message handle
        /// </remarks>
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

                if (MessageHandle != UIntPtr.Zero)
                {
                    throw new InvalidOperationException("Message cannot be modified after the handle has been created");
                }

                _message = value;
            }
        }

        /// <summary>
        /// Get the content of the message as a byte array
        /// </summary>
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

        /// <summary>
        /// Helper function that will attempt to return a ByteArray message as a string
        /// </summary>
        /// <returns>Message as byte array unless the message type is unknown</returns>
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

        /// <summary>
        /// Gets all of the message property keys
        /// </summary>
        /// <returns>Returns an array of strings representing the keys in the message properties</returns>
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

        /// <summary>
        /// Get the message handle. If the message has not yet been created then it will be by this function.
        /// </summary>
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

        /// <summary>
        /// Get the message type
        /// </summary>
        public MessageTypes MessageType
        {
            get
            {
                IsDisposed();
                return _messageType;
            }
        }

        /// <summary>
        /// Get or set a message property
        /// </summary>
        /// <param name="key">The name of the property - can be a new key</param>
        /// <returns>The value of the message property when using get</returns>
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

        /// <summary>
        /// Get or set the correlation ID
        /// </summary>
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

        /// <summary>
        /// Override to get the message when ToString is called
        /// </summary>
        /// <returns>Message as a string or null if the type is unknown</returns>
        public override string ToString()
        {
            if (_disposed)
            {
                return null;
            }
            else
            {
                return GetByteArrayAsString();
            }
        }

        /// <summary>
        /// Clean up resources ready for garbage collection
        /// </summary>
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
                // Free up this memory
                _message = null;
                _byteMessage = null;
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

        /// <summary>
        /// Destructor - clean up resources
        /// </summary>
        ~IoTMessage()
        {
            Dispose(false);
        }

    }
}
