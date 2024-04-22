using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infra.EventBus.Subscriptions
{
    public class Subscription
    {
        public Type EventType { get; private set; }
        public Type HandlerType { get; private set; }

        public Subscription(Type eventType, Type handlerType)
        {
            EventType = eventType;
            HandlerType = handlerType;
        }
    }
}
