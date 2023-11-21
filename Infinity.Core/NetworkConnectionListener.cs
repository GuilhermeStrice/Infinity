using System.Net;

namespace Infinity.Core
{
    /// <summary>
    ///     Abstract base class for a <see cref="ConnectionListener"/> for network based connections.
    /// </summary>
    /// <threadsafety static="true" instance="true"/>
    public abstract class NetworkConnectionListener : IDisposable
    {
        /// <summary>
        ///     The local end point the listener is listening for new clients on.
        /// </summary>
        public IPEndPoint ?EndPoint { get; protected set; }

        /// <summary>
        ///     The <see cref="IPMode">IPMode</see> the listener is listening for new clients on.
        /// </summary>
        public IPMode IPMode { get; protected set; }

        /// <summary>
        /// The max size it attempts to read from the network.
        /// Defaults to 8096.
        /// </summary>
        public int ReceiveBufferSize = 8096;

        public abstract double AveragePing { get; }
        public abstract int ConnectionCount { get; }

        /// <summary>
        /// A callback for early connection rejection. 
        /// * Return false to reject connection.
        /// * A null response is ok, we just won't send anything.
        /// </summary>
        public AcceptConnectionCheck? AcceptConnection;
        public delegate bool AcceptConnectionCheck(IPEndPoint endPoint, byte[] input, out byte[] response);

        /// <summary>
        ///     Invoked when a new client connects.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         NewConnection is invoked each time a client connects to the listener. The 
        ///         <see cref="NewConnectionEventArgs"/> contains the new <see cref="Connection"/> for communication with this
        ///         client.
        ///     </para>
        ///     <para>
        ///         Infinity may or may not store connections so it is your responsibility to keep track and properly Dispose of 
        ///         connections to your server. 
        ///     </para>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Event_Thread_Safety_Warning']/*" />
        /// </remarks>
        /// <example>
        ///     <code language="C#" source="DocInclude/TcpListenerExample.cs"/>
        /// </example>
        public event Action<NewConnectionEventArgs> NewConnection;

        /// <summary>
        ///      Invoked when an internal error causes the listener to be unable to continue handling messages.
        /// </summary>
        /// <remarks>
        ///      Support for this is still pretty limited. At the time of writing, only iOS devices need this in one case:
        ///      When iOS suspends an app, it might also free our socket while not allowing Unity to run in the background.
        ///      When Unity resumes, it can't know that time passed or the socket is freed, so we used to continuously throw internal errors.
        /// </remarks>
        public event Action<InfinityInternalErrors> OnInternalError;

        /// <summary>
        ///     Makes this connection listener begin listening for connections.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This instructs the listener to begin listening for new clients connecting to the server. When a new client 
        ///         connects the <see cref="NewConnection"/> event will be invoked containing the connection to the new client.
        ///     </para>
        ///     <para>
        ///         To stop listening you should call <see cref="Dispose()"/>.
        ///     </para>
        /// </remarks>
        /// <example>
        ///     <code language="C#" source="DocInclude/TcpListenerExample.cs"/>
        /// </example>
        public abstract void Start();

        /// <summary>
        ///     Invokes the NewConnection event with the supplied connection.
        /// </summary>
        /// <param name="msg">The user sent bytes that were received as part of the handshake.</param>
        /// <param name="connection">The connection to pass in the arguments.</param>
        /// <remarks>
        ///     Implementers should call this to invoke the <see cref="NewConnection"/> event before data is received so that
        ///     subscribers do not miss any data that may have been sent immediately after connecting.
        /// </remarks>
        protected void InvokeNewConnection(MessageReader msg, NetworkConnection connection)
        {
            // Make a copy to avoid race condition between null check and invocation
            Action<NewConnectionEventArgs> handler = NewConnection;
            if (handler != null)
            {
                try
                {
                    handler(new NewConnectionEventArgs(msg, connection));
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        ///     Invokes the InternalError event with the supplied reason.
        /// </summary>
        protected void InvokeInternalError(InfinityInternalErrors reason)
        {
            // Make a copy to avoid race condition between null check and invocation
            Action<InfinityInternalErrors> handler = OnInternalError;
            if (handler != null)
            {
                try
                {
                    handler(reason);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        ///     Call to dispose of the connection listener.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        ///     Called when the object is being disposed.
        /// </summary>
        /// <param name="disposing">Are we disposing?</param>
        protected virtual void Dispose(bool disposing)
        {
            NewConnection = null;
            OnInternalError = null;
        }
    }
}
