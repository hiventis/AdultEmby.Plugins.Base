using System;
using System.Threading;
using System.Threading.Tasks;

namespace AdultEmby.Plugins.Base
{
    public interface IThrottle
    {
        Task GetNext(CancellationToken cancellationToken);

        Task GetNext(out TimeSpan delay, CancellationToken cancellationToken);
    }
}
