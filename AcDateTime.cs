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
using System.Globalization;

namespace AcUtils
{
    /// <summary>
    /// Use to validate and convert date/time values to and from .NET and AccuRev/Unix formats.
    /// </summary>
    [Serializable]
    public static class AcDateTime
    {
        #region class variables
        private static CultureInfo _ci;
        #endregion

        /// <summary>
        /// Instantiate CultureInfo class variable.
        /// </summary>
        static AcDateTime()
        {
            _ci = new CultureInfo("en-US");
        }

        /// <summary>
        /// Convert DateTime \e dt parameter to a string suitable for AccuRev command line processing.
        /// </summary>
        /// <param name="dt">The DateTime object to convert.</param>
        /// <returns>A string in <tt>yyyy/mm/dd hh:mm:ss</tt> format or an empty string if \e dt is \e null.</returns>
        /*! \sa <a href="_latest_promotions_8cs-example.html">LatestPromotions.cs</a> */
        public static string DateTime2AcDate(DateTime? dt)
        {
            string val = (dt == null) ? String.Empty :
                dt.Value.ToString("yyyy\\/MM\\/dd HH\\:mm\\:ss", CultureInfo.InvariantCulture);
            return val;
        }

        /// <summary>
        /// Convert an AccuRev date given in Unix time (\e seconds param) to a .NET DateTime in local time.
        /// </summary>
        /// <param name="seconds">Unix time expressed as the number of seconds since January 1, 1970 UTC.</param>
        /// <returns>DateTime object with the converted value or \e null on error.</returns>
        /// <exception cref="ArgumentOutOfRangeException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to convert \e seconds 
        /// to a date and time value that represents the same moment in time as the Unix time.</exception>
        /*! \sa <a href="_latest_promotions_8cs-example.html">LatestPromotions.cs</a> */
        public static DateTime? AcDate2DateTime(long seconds)
        {
            DateTime? dt = null;
            try
            {
                DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(seconds);
                dt = dto.DateTime.ToLocalTime();
            }

            catch (ArgumentOutOfRangeException ecx)
            {
                AcDebug.Log($"ArgumentOutOfRangeException caught and logged in AcDateTime.AcDate2DateTime{Environment.NewLine}{ecx.Message}");
            }

            return dt;
        }

        /// <summary>
        /// Convert an AccuRev date given in Unix time (\e seconds param) to a .NET DateTime in local time.
        /// </summary>
        /// <param name="seconds">Unix time expressed as the number of seconds since January 1, 1970 UTC.</param>
        /// <returns>DateTime object with the converted value or \e null on error.</returns>
        public static DateTime? AcDate2DateTime(string seconds)
        {
            long val;
            bool result = long.TryParse(seconds, out val);
            return (result) ? AcDate2DateTime(val) : null;
        }

        /// <summary>
        /// Determine if \e dt parameter in AccuRev string format is a valid date and time.
        /// </summary>
        /// <param name="dt">The date and time in AccuRev string format <tt>yyyy/mm/dd hh:mm:ss</tt>.</param>
        /// <returns>\e true if \e dt is a valid date and time, \e false otherwise.</returns>
        /*! \sa <a href="_latest_promotions_8cs-example.html">LatestPromotions.cs</a> */
        public static bool AcDateValid(string dt)
        {
            DateTime temp;
            return DateTime.TryParseExact(dt, "yyyy\\/MM\\/dd HH\\:mm\\:ss", _ci, DateTimeStyles.None, out temp);
        }
    }
}
