using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace UKLepraBotFaaS
{
    public class TimingScopeWrapper:DisposableBase
    {
        private ILogger _log;
        private readonly string _message;
        private readonly DateTimeOffset _timeStarted;

        public TimingScopeWrapper(ILogger logger, string message)
        {
            _log = logger;
            _message = message;
            _timeStarted = DateTimeOffset.Now;
        }

        protected override void Dispose(bool disposing)
        {
            _log.LogInformation(_message, (DateTimeOffset.Now - _timeStarted).TotalMilliseconds);
        }
    }

    public abstract class DisposableBase : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Is the instance disposed
        /// </summary>
        public bool IsDisposed
        {
            get { return _disposed; }
        }

        /// <summary>
        ///  Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            DisposeInternal(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~DisposableBase()
        {
            DisposeInternal(false);
        }

        private void DisposeInternal(bool disposing)
        {
            if (!_disposed)
                Dispose(disposing);

            _disposed = true;
        }

        /// <summary>
        /// Dispose method to be overriden by child implementations
        /// </summary>
        /// <param name="disposing"></param>
        protected abstract void Dispose(bool disposing);


    }
}
