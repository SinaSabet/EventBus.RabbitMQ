using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infra.EventBus.EventsService
{
    public interface IEventHandler<in T> where T : Event
    {
        Task HandleAsync(T @event);
    }
}
