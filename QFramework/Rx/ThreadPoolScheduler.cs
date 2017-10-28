﻿/****************************************************************************
 * Copyright (c) 2017 liangxie
 * 
 * http://liangxiegame.com
 * https://github.com/liangxiegame/QFramework
 * https://github.com/liangxiegame/QSingleton
 * https://github.com/liangxiegame/QChain
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 ****************************************************************************/

#if !UNITY_METRO
namespace QFramework.Core.Rx
{
    using System;
    using System.Collections.Generic;
    using Utils.Scheduler;

    public static partial class Scheduler
    {
        public static readonly IScheduler ThreadPool = new ThreadPoolScheduler();

        class ThreadPoolScheduler : IScheduler, ISchedulerPeriodic, ISchedulerQueueing
        {
            public ThreadPoolScheduler()
            {
            }

            public DateTimeOffset Now
            {
                get { return Utils.Scheduler.Scheduler.Now; }
            }

            public IDisposable Schedule(Action action)
            {
                var d = new BooleanDisposable();

                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    if (!d.IsDisposed)
                    {
                        action();
                    }
                });

                return d;
            }

            public IDisposable Schedule(DateTimeOffset dueTime, Action action)
            {
                return Schedule(dueTime - Now, action);
            }

            public IDisposable Schedule(TimeSpan dueTime, Action action)
            {
                return new Timer(dueTime, action);
            }

            public IDisposable SchedulePeriodic(TimeSpan period, Action action)
            {
                return new PeriodicTimer(period, action);
            }

            public void ScheduleQueueing<T>(ICancelable cancel, T state, Action<T> action)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(callBackState =>
                {
                    if (!cancel.IsDisposed)
                    {
                        action((T)callBackState);
                    }
                }, state);
            }

            // timer was borrwed from Rx Official

            sealed class Timer : IDisposable
            {
                static readonly HashSet<System.Threading.Timer> s_timers = new HashSet<System.Threading.Timer>();

                private readonly SingleAssignmentDisposable _disposable;

                private Action _action;
                private System.Threading.Timer _timer;

                private bool _hasAdded;
                private bool _hasRemoved;

                public Timer(TimeSpan dueTime, Action action)
                {
                    _disposable = new SingleAssignmentDisposable();
                    _disposable.Disposable = Disposable.Create(Unroot);

                    _action = action;
                    _timer = new System.Threading.Timer(Tick, null, dueTime, TimeSpan.FromMilliseconds(System.Threading.Timeout.Infinite));

                    lock (s_timers)
                    {
                        if (!_hasRemoved)
                        {
                            s_timers.Add(_timer);

                            _hasAdded = true;
                        }
                    }
                }

                private void Tick(object state)
                {
                    try
                    {
                        if (!_disposable.IsDisposed)
                        {
                            _action();
                        }
                    }
                    finally
                    {
                        Unroot();
                    }
                }

                private void Unroot()
                {
                    _action = Stubs.Nop;

                    var timer = default(System.Threading.Timer);

                    lock (s_timers)
                    {
                        if (!_hasRemoved)
                        {
                            timer = _timer;
                            _timer = null;

                            if (_hasAdded && timer != null)
                                s_timers.Remove(timer);

                            _hasRemoved = true;
                        }
                    }

                    if (timer != null)
                        timer.Dispose();
                }

                public void Dispose()
                {
                    _disposable.Dispose();
                }
            }

            sealed class PeriodicTimer : IDisposable
            {
                static readonly HashSet<System.Threading.Timer> s_timers = new HashSet<System.Threading.Timer>();

                private Action _action;
                private System.Threading.Timer _timer;
                private readonly AsyncLock _gate;

                public PeriodicTimer(TimeSpan period, Action action)
                {
                    this._action = action;
                    this._timer = new System.Threading.Timer(Tick, null, period, period);
                    this._gate = new AsyncLock();

                    lock (s_timers)
                    {
                        s_timers.Add(_timer);
                    }
                }

                private void Tick(object state)
                {
                    _gate.Wait(() =>
                    {
                        _action();
                    });
                }

                public void Dispose()
                {
                    var timer = default(System.Threading.Timer);

                    lock (s_timers)
                    {
                        timer = _timer;
                        _timer = null;

                        if (timer != null)
                            s_timers.Remove(timer);
                    }

                    if (timer != null)
                    {
                        timer.Dispose();
                        _action = Stubs.Nop;
                    }
                }
            }
        }
    }
}

#endif