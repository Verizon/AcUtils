/*! \file
Copyright (C) 2016-2018 Verizon. All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Diagnostics;

namespace AcUtils
{
    /// <summary>
    /// Determine the amount of time a task takes to run.
    /// </summary>
    [Serializable]
    public sealed class AcStopWatch
    {
        #region class variables
        public delegate void ElapsedTimeHandler(string elapsedTime);  /*!< The \e ElapsedTimeHandler delegate type. \sa Example in AcStopWatchMarker. */
        private ElapsedTimeHandler _elapsedTimeHandler;
        [NonSerialized] private Stopwatch _stopwatch;
        #endregion

        /// <summary>
        /// Constructor that takes an \e ElapsedTimeHandler.
        /// </summary>
        /// <param name="elapsedTimeHandler">The client's \e ElapsedTimeHandler to call with the elapsed time.</param>
        /*! \sa Example in AcStopWatchMarker */
        public AcStopWatch(ElapsedTimeHandler elapsedTimeHandler)
        {
            _elapsedTimeHandler = elapsedTimeHandler;
        }

        /// <summary>
        /// Set the stopwatch to zero and begin measuring elapsed time.
        /// </summary>
        public void Start()
        {
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Call the client's \e ElapsedTimeHandler with the elapsed time as a formatted string in days, hours, minutes and seconds.
        /// </summary>
        public void Stop()
        {
            if (_elapsedTimeHandler != null)
            {
                AcDuration ts = _stopwatch.Elapsed;
                _elapsedTimeHandler(ts.ToString());
            }
        }
    }

    /// <summary>
    /// Makes the \e using statement available for AcStopWatch objects. 
    /// Adapted from [Profiling with Stopwatch](http://blogs.msdn.com/b/shawnhar/archive/2009/07/07/profiling-with-stopwatch.aspx) by Shawn Hargreaves.
    /// </summary>
    /*! \code
        AcStopWatch spwatch = new AcStopWatch(log); // here the ElapsedTimeHandler is log
        using (new AcStopWatchMarker(spwatch))
        {
           // work for which to calculate time spent
           ...
        }  <-- log method called on closing brace

        /// <summary>
        /// Reports the amount of time our operation took to complete.
        /// </summary>
        /// <param name="elapsedTime">The time it took the operation to run.</param>
        private static void log(string elapsedTime)
        {
          Console.WriteLine($"{elapsedTime} to complete.");
        }
    \endcode */
    /*! \sa AcStopWatch#ElapsedTimeHandler delegate type */
    public struct AcStopWatchMarker : IDisposable
    {
        private AcStopWatch _stopwatch;

        /// <summary>
        /// Constructor to initialize and start the \e stopwatch. Implemented by calling AcStopWatch#Start.
        /// </summary>
        public AcStopWatchMarker(AcStopWatch stopwatch)
        {
            _stopwatch = stopwatch;
            _stopwatch.Start();
        }

        /// <summary>
        /// Calls the client's [ElapsedTimeHandler](@ref AcStopWatch#ElapsedTimeHandler) at the closing brace of the \e using statement. 
        /// Implemented by calling AcStopWatch#Stop.
        /// </summary>
        public void Dispose()
        {
            _stopwatch.Stop();
        }
    }
}
