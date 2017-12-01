using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;

namespace AdultEmby.Plugins.Base
{
    public class Throttle : IThrottle
    {
        private readonly object _lock = new object();
        private readonly TimeSpan _interval;
        private DateTime _nextTime;
        private readonly ILogger _logger;


        public Throttle(TimeSpan interval, string name, ILogManager logManager)
        {
            _interval = interval;
            _nextTime = DateTime.Now.Subtract(interval);
            _logger = logManager.GetLogger(GetType().FullName + "." + name);
        }
        
        public Task GetNext(CancellationToken cancellationToken)
        {
            TimeSpan delay;
            return GetNext(out delay, cancellationToken);
        }

        public Task GetNext(out TimeSpan delay, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                _nextTime = _nextTime.Add(_interval);
                if (_nextTime > now)
                {
                    delay = _nextTime - now;
                    _logger.Info("Delay is [{0}]", delay);
                    return Task.Delay(delay, cancellationToken);
                }
                _nextTime = now;
                delay = TimeSpan.Zero;
                _logger.Info("Delay is [{0}]", delay);
                return Task.FromResult(true);
            }
        }
    }
}
