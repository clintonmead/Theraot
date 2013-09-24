using System;
using System.Threading;

namespace Theraot.Threading.Needles
{
    [Serializable]
    [global::System.Diagnostics.DebuggerNonUserCode]
    public sealed class PromiseNeedle : IPromise
    {
        private int _hashCode;
        private Internal _internal;

        public PromiseNeedle(bool done)
        {
            _internal = new Internal();
            if (done)
            {
                _internal.OnCompleted();
            }
            _hashCode = base.GetHashCode();
        }

        public PromiseNeedle(Exception exception)
        {
            _internal = new Internal(exception);
            _hashCode = exception.GetHashCode();
        }

        public PromiseNeedle(out IPromised promised, bool done)
        {
            _internal = new Internal();
            if (done)
            {
                _internal.OnCompleted();
            }
            promised = _internal;
            _hashCode = base.GetHashCode();
        }

        public PromiseNeedle(out IPromised promised, Exception exception)
        {
            _internal = new Internal(exception);
            promised = _internal;
            _hashCode = exception.GetHashCode();
        }

        public Exception Error
        {
            get
            {
                return _internal.Error;
            }
        }

        public bool IsCanceled
        {
            get
            {
                return _internal.IsCanceled;
            }
        }

        public bool IsCompleted
        {
            get
            {
                return _internal.IsCompleted;
            }
        }

        public bool IsFaulted
        {
            get
            {
                return _internal.IsFaulted;
            }
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override string ToString()
        {
            return string.Format("{{Promise: {0}}}", _internal.ToString());
        }

        public void Wait()
        {
            _internal.Wait();
        }

        [Serializable]
        private class Internal : IPromised
        {
            private Exception _error;
            private int _isCompleted;

            private StructNeedle<ManualResetEvent> _waitHandle;

            public Internal()
            {
                _waitHandle = new ManualResetEvent(false);
            }

            public Internal(Exception error)
            {
                _error = error;
                Thread.VolatileWrite(ref _isCompleted, 1);
                _waitHandle = new ManualResetEvent(true);
            }

            ~Internal()
            {
                var waitHandle = _waitHandle.Value;
                if (!ReferenceEquals(waitHandle, null))
                {
                    waitHandle.Close();
                }
                _waitHandle.Value = null;
            }

            public Exception Error
            {
                get
                {
                    Wait();
                    return _error;
                }
            }

            object INeedle<object>.Value
            {
                get
                {
                    throw new NotSupportedException();
                }
                set
                {
                    throw new NotSupportedException();
                }
            }

            bool IReadOnlyNeedle<object>.IsAlive
            {
                get
                {
                    return false;
                }
            }

            object IReadOnlyNeedle<object>.Value
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public bool IsCanceled
            {
                get
                {
                    return false;
                }
            }

            public bool IsCompleted
            {
                get
                {
                    return Thread.VolatileRead(ref _isCompleted) == 1;
                }
            }

            public bool IsFaulted
            {
                get
                {
                    return !ReferenceEquals(_error, null);
                }
            }

            void INeedle<object>.Release()
            {
                //Empty
            }

            void IObserver<object>.OnNext(object value)
            {
                //Empty
            }

            public void OnCompleted()
            {
                _error = null;
                Thread.VolatileWrite(ref _isCompleted, 1);
                _waitHandle.Value.Set();
            }

            public void OnError(Exception error)
            {
                _error = error;
                Thread.VolatileWrite(ref _isCompleted, 1);
                _waitHandle.Value.Set();
            }

            public void Release()
            {
                Thread.VolatileWrite(ref _isCompleted, 0);
                _waitHandle.Value.Reset();
                _error = null;
            }

            public override string ToString()
            {
                if (IsCompleted)
                {
                    if (ReferenceEquals(_error, null))
                    {
                        return "[Done]";
                    }
                    else
                    {
                        return _error.ToString();
                    }
                }
                else
                {
                    return "[Not Created]";
                }
            }

            public void Wait()
            {
                _waitHandle.Value.WaitOne();
            }
        }
    }
}