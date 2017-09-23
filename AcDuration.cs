/*! \file
Copyright (C) 2016 Verizon. All Rights Reserved.

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

namespace AcUtils
{
    /// <summary>
    /// Wrapper around TimeSpan so we can return our own formatted elapsed time
    /// string and still sort correctly in a grid based on the actual time span.
    /// </summary>
    [Serializable]
    public struct AcDuration : IComparable<AcDuration>
    {
        private TimeSpan _ts;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AcDuration(TimeSpan ts)
        {
            _ts = ts;
        }

        /// <summary>
        /// User-defined conversion from AcDuration to TimeSpan.
        /// </summary>
        public static implicit operator TimeSpan(AcDuration rhs)
        {
            return rhs._ts;
        }

        /// <summary>
        /// User-defined conversion from TimeSpan to AcDuration.
        /// </summary>
        public static implicit operator AcDuration(TimeSpan rhs)
        {
            return new AcDuration(rhs);
        }

        /// <summary>
        /// Used by the grid to order our AcDuration objects.
        /// </summary>
        public int CompareTo(AcDuration rhs)
        {
            int result;
            if (AcDuration.ReferenceEquals(this, rhs))
                result = 0;
            else
                result = _ts.CompareTo(rhs._ts);
            return result;
        }

        /// <summary>
        /// Formatted string to show elapsed time in days, hours, minutes and seconds.
        /// </summary>
        public override string ToString()
        {
            string hours = String.Format("{0:D2}", _ts.Hours);
            string minutes = String.Format("{0:D2}", _ts.Minutes);
            string seconds = String.Format("{0:D2}", _ts.Seconds);
            string elapsedTime;
            if (_ts.Days > 0)
                elapsedTime = String.Format("{0} {1}, {2}:{3}:{4}", _ts.Days,
                    (_ts.Days == 1) ? "day" : "days", hours, minutes, seconds);
            else
                elapsedTime = String.Format("{0}:{1}:{2}", hours, minutes, seconds);
            return elapsedTime;
        }
    }
}
