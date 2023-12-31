namespace Infinity.Http
{
    /// <summary>
    /// Callbacks/actions to use when various events are encountered.
    /// </summary>
    public class WebserverEvents
    {
        /// <summary>
        /// Method to use for sending log messages.
        /// </summary>
        public Action<string> Logger { get; set; } = null;

        /// <summary>
        /// Event to fire when a connection is received.
        /// </summary>
        public event EventHandler<NewConnectionEventArgs> ConnectionReceived = delegate { };

        /// <summary>
        /// Event to fire when a connection is denied.
        /// This event is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        public event EventHandler<NewConnectionEventArgs> ConnectionDenied = delegate { };

        /// <summary>
        /// Event to fire  when a request is received. 
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived = delegate { };

        /// <summary>
        /// Event to fire  when a request is denied due to access control. 
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestDenied = delegate { };
         
        /// <summary>
        /// Event to fire when a requestor disconnected unexpectedly.
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestorDisconnected = delegate { };

        /// <summary>
        /// Event to fire when a response is sent.
        /// </summary>
        public event EventHandler<ResponseEventArgs> ResponseSent = delegate { };

        /// <summary>
        /// Event to fire when an exception is encountered.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> ExceptionEncountered = delegate { };

        /// <summary>
        /// Event to fire when the server is started.
        /// </summary>
        public event EventHandler ServerStarted = delegate { };

        /// <summary>
        /// Event to fire when the server is stopped.
        /// </summary>
        public event EventHandler ServerStopped = delegate { };

        /// <summary>
        /// Event to fire when the server is being disposed.
        /// </summary>
        public event EventHandler ServerDisposing = delegate { }; 

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public WebserverEvents()
        {
        }

        /// <summary>
        /// Handle connection received event.
        /// </summary>
        /// <param name="_sender">Sender.</param>
        /// <param name="_args">Args.</param>
        public void HandleConnectionReceived(object _sender, NewConnectionEventArgs _args)
        {
            WrappedEventHandler(() => ConnectionReceived?.Invoke(_sender, _args), "ConnectionReceived", _sender);
        }

        /// <summary>
        /// Handle connection denied event.
        /// This event is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        /// <param name="_sender">Sender.</param>
        /// <param name="_args">Args.</param>
        public void HandleConnectionDenied(object _sender, NewConnectionEventArgs _args)
        {
            WrappedEventHandler(() => ConnectionDenied?.Invoke(_sender, _args), "ConnectionDenied", _sender);
        }

        /// <summary>
        /// Handle request received event.
        /// </summary>
        /// <param name="_sender">Sender.</param>
        /// <param name="_args">Args.</param>
        public void HandleRequestReceived(object _sender, RequestEventArgs _args)
        {
            WrappedEventHandler(() => RequestReceived?.Invoke(_sender, _args), "RequestReceived", _sender);
        }

        /// <summary>
        /// Handle request denied event.
        /// </summary>
        /// <param name="_sender">Sender.</param>
        /// <param name="_args">Args.</param>
        public void HandleRequestDenied(object _sender, RequestEventArgs _args)
        {
            WrappedEventHandler(() => RequestDenied?.Invoke(_sender, _args), "RequestDenied", _sender);
        }

        /// <summary>
        /// Handle response sent event.
        /// </summary>
        /// <param name="_sender">Sender.</param>
        /// <param name="_args">Args.</param>
        public void HandleResponseSent(object _sender, ResponseEventArgs _args)
        {
            WrappedEventHandler(() => ResponseSent?.Invoke(_sender, _args), "ResponseSent", _sender);
        }

        /// <summary>
        /// Handle exception encountered event.
        /// </summary>
        /// <param name="_sender">Sender.</param>
        /// <param name="_args">Args.</param>
        public void HandleExceptionEncountered(object _sender, ExceptionEventArgs _args)
        {
            WrappedEventHandler(() => ExceptionEncountered?.Invoke(_sender, _args), "ExceptionEncountered", _sender);
        }

        /// <summary>
        /// Handle server started event.
        /// </summary>
        /// <param name="_sender">Sender.</param>
        /// <param name="_args">Args.</param>
        public void HandleServerStarted(object _sender, EventArgs _args)
        {
            WrappedEventHandler(() => ServerStarted?.Invoke(_sender, _args), "ServerStarted", _sender);
        }

        /// <summary>
        /// Handle server stopped event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleServerStopped(object _sender, EventArgs _args)
        {
            WrappedEventHandler(() => ServerStopped?.Invoke(_sender, _args), "ServerStopped", _sender);
        }

        /// <summary>
        /// Handle server disposing event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleServerDisposing(object _sender, EventArgs _args)
        {
            WrappedEventHandler(() => ServerDisposing?.Invoke(_sender, _args), "ServerDisposing", _sender);
        }

        private void WrappedEventHandler(Action _action, string _handler, object _sender)
        {
            if (_action == null)
            {
                return;
            }

            try
            {
                _action.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Invoke("Event handler exception in " + _handler + ": " + e.Message);
            }
        }
    }
}
