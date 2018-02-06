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

namespace AcUtils
{
    /// <summary>
    /// Wrapper around TimeSpan so we can return our own formatted elapsed time
    /// string and still sort correctly in a grid based on the actual time span.
    /// </summary>
    [Serializable]
    public class AcDuration : IFormattable, IEquatable<AcDuration>, IComparable<AcDuration>, IComparable
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
            return (rhs != null) ? rhs._ts : new TimeSpan();
        }

        /// <summary>
        /// User-defined conversion from TimeSpan to AcDuration.
        /// </summary>
        public static implicit operator AcDuration(TimeSpan rhs)
        {
            return new AcDuration(rhs);
        }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcDuration. 
        /// </summary>
        /// <param name="other">The AcDuration object being compared to \e this instance.</param>
        /// <returns>\e true if AcDuration \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcDuration other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _ts.Equals(other._ts);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcDuration)](@ref AcDuration#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as AcDuration);
        }

        /// <summary>
        /// Override appropriate for type AcDuration.
        /// </summary>
        /// <returns>Hash of our TimeSpan class data member.</returns>
        public override int GetHashCode()
        {
            return _ts.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcDuration objects to sort by timespan.
        /// </summary>
        /// <param name="other">An AcDuration object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcDuration objects being compared.</returns>
        public int CompareTo(AcDuration other)
        {
            int result;
            if (AcDuration.ReferenceEquals(this, other))
                result = 0;
            else
                result = _ts.CompareTo(other._ts);

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcDuration object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcDuration)](@ref AcDuration#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcDuration object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcDuration))
                throw new ArgumentException("Argument is not an AcDuration", "other");
            AcDuration o = (AcDuration)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(session.ToString("h"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Show elapsed time in days, hours, minutes and seconds. Default when not using a format specifier.
        /// \arg \c H Hours.
        /// \arg \c M Minutes.
        /// \arg \c S Seconds.
        /// \arg \c D \b day or \b days.
        public string ToString(string format, IFormatProvider provider)
        {
            if (provider != null)
            {
                ICustomFormatter fmt = provider.GetFormat(this.GetType()) as ICustomFormatter;
                if (fmt != null)
                    return fmt.Format(format, this, provider);
            }

            if (String.IsNullOrEmpty(format))
                format = "G";

            switch (format.ToUpperInvariant())
            {
                case "G": // default when not using a format specifier
                {
                    string hours = $"{_ts.Hours:D2}";
                    string minutes = $"{_ts.Minutes:D2}";
                    string seconds = $"{_ts.Seconds:D2}";
                    string elapsedTime;
                    if (_ts.Days > 0)
                        elapsedTime = $"{_ts.Days} {((_ts.Days == 1) ? "day" : "days")}, {hours}:{minutes}:{seconds}";
                    else
                        elapsedTime = $"{hours}:{minutes}:{seconds}";
                    return elapsedTime;
                }
                case "H":
                    return $"{_ts.Hours:D2}";
                case "M":
                    return $"{_ts.Minutes:D2}";
                case "S":
                    return $"{_ts.Seconds:D2}";
                case "D":
                    return $"{_ts.Days} {((_ts.Days == 1) ? "day" : "days")}";
                default:
                    throw new FormatException($"The {format} format string is not supported.");
            }
        }

        // Calls ToString(string, IFormatProvider) version with a null IFormatProvider argument.
        public string ToString(string format)
        {
            return ToString(format, null);
        }

        // Calls ToString(string, IFormatProvider) version with the general format and a null IFormatProvider argument.
        public override string ToString()
        {
            return ToString("G", null);
        }
        #endregion ToString
    }
}
