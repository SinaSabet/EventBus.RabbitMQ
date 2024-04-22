using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.RabbitMQ.Connection
{
    public class PersistentConnection(
        IConnectionFactory connectionFactory,
        ILogger<PersistentConnection> logger,
        int timeoutBeforeReconnecting = 15
        ) : IPersistentConnection,IDisposable
    {
        private  IConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        private  TimeSpan _timeoutBeforeReconnecting = TimeSpan.FromSeconds(timeoutBeforeReconnecting);

        private  IConnection _connection;
        private bool _disposed;

        private readonly object _locker = new object();
        private bool _connectionFailed = false;
        #region Implementation
        public bool IsConnected
        {
            get
            {
                return (_connection != null) && (_connection.IsOpen) && (!_disposed);
            }
        }

        public event EventHandler OnReconnectedAfterConnectionFailure;

        public IModel CreateModel()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action.");
            }

            return _connection.CreateModel();
        }

        public bool TryConnect()
        {
            logger.LogInformation("Trying to connect to RabbitMQ...");

            var policy = Policy
                  .Handle<SocketException>()
                  .Or<BrokerUnreachableException>()
                  .WaitAndRetryForever((duration) => _timeoutBeforeReconnecting, (ex, time) =>
                  {
                      logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut} seconds ({ExceptionMessage}). Waiting to try again...", $"{(int)time.TotalSeconds}", ex.Message);
                  });

            policy.Execute(() =>
            {
                _connection = _connectionFactory.CreateConnection();
            });

            if (!IsConnected)
            {
                logger.LogCritical("ERROR: could not connect to RabbitMQ.");
                _connectionFailed = true;
                return false;
            }

            // These event handlers hadle situations where the connection is lost by any reason. They try to reconnect the client.
            _connection.ConnectionShutdown += OnConnectionShutdown;
            _connection.CallbackException += OnCallbackException;
            _connection.ConnectionBlocked += OnConnectionBlocked;
            _connection.ConnectionUnblocked += OnConnectionUnblocked;


            logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events", _connection.Endpoint.HostName);

            // If the connection has failed previously because of a RabbitMQ shutdown or something similar, we need to guarantee that the exchange and queues exist again.
            // It's also necessary to rebind all application event handlers. We use this event handler below to do this.
            if (_connectionFailed)
            {
                OnReconnectedAfterConnectionFailure?.Invoke(this, null);
                _connectionFailed = false;
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _connection.Dispose();
            }
            catch (IOException ex)
            {
                logger.LogCritical(ex.ToString());
            }
        }
        #endregion



        #region RabbitMQOptions

        private void OnCallbackException(object sender, CallbackExceptionEventArgs args)
        {
            _connectionFailed = true;

            logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");
            TryConnectIfNotDisposed();
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs args)
        {
            _connectionFailed = true;

            logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");
            TryConnectIfNotDisposed();
        }

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs args)
        {
            _connectionFailed = true;

            logger.LogWarning("A RabbitMQ connection is blocked. Trying to re-connect...");
            TryConnectIfNotDisposed();
        }

        private void OnConnectionUnblocked(object sender, EventArgs args)
        {
            _connectionFailed = true;

            logger.LogWarning("A RabbitMQ connection is unblocked. Trying to re-connect...");
            TryConnectIfNotDisposed();
        }

        private void TryConnectIfNotDisposed()
        {
            if (_disposed)
            {
                logger.LogInformation("RabbitMQ client is disposed. No action will be taken.");
                return;
            }

            TryConnect();
        }


        #endregion
    }
}
