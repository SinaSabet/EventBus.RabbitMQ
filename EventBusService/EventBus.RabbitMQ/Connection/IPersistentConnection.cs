using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.RabbitMQ.Connection
{
    public interface IPersistentConnection
    {
        event EventHandler OnReconnectedAfterConnectionFailure;
        bool IsConnected { get; }

        bool TryConnect();
        IModel CreateModel();
    }
}
