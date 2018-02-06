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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    /// <summary>
    /// Holds the attributes for an AccuRev user session: principal name, 
    /// host machine IP address, and length of time logged in. 
    /// AcSession objects are instantiated during AcSessions construction.
    /// </summary>
    /*! \accunote_ A server IP address change requires renewing the session token 
         for the service account that runs the AccuRev service, otherwise trigger 
         operations will fail. To do so, reissue the persistent login command 
         <tt>login -n</tt> for the service account. */
    [Serializable]
    public sealed class AcSession : IFormattable, IEquatable<AcSession>, IComparable<AcSession>, IComparable
    {
        #region class variables
        private string _name; // user's principal name
        private string _host; // IP address of the host machine
        private AcDuration _duration; // length of time user has been logged on
        #endregion

        /// <summary>
        /// Constructor used during AcSessions list construction. It is called internally and not by user code. 
        /// </summary>
        internal AcSession() { }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcSession. 
        /// Uses AccuRev principal name and IP address of the host machine to compare instances.
        /// </summary>
        /// <param name="other">The AcSession object being compared to \e this instance.</param>
        /// <returns>\e true if AcSession \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcSession other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            var left = Tuple.Create(Name, Host);
            var right = Tuple.Create(other.Name, other.Host);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcSession)](@ref AcSession#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as AcSession);
        }

        /// <summary>
        /// Override appropriate for type AcSession.
        /// </summary>
        /// <returns>Hash of AccuRev principal name and IP address of the host machine.</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(Name, Host);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcSession objects 
        /// to sort by Duration and then Name.
        /// </summary>
        /// <param name="other">An AcSession object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcSession objects being compared.</returns>
        /*! \sa [AcSessions example](@ref AcUtils#AcSessions#AcSessions) */
        public int CompareTo(AcSession other)
        {
            int result;
            if (AcSession.ReferenceEquals(this, other))
                result = 0;
            else
            {
                result = Duration.CompareTo(other.Duration);
                if (result == 0)
                    result = Name.CompareTo(other.Name);
            }

            //return -1 * result; // sort in reverse order to show longer sessions first
            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcSession object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcSession)](@ref AcSession#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcSession object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcSession))
                throw new ArgumentException("Argument is not an AcSession", "other");
            AcSession o = (AcSession)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// User's AccuRev principal name.
        /// </summary>
        public string Name
        {
            get { return _name ?? String.Empty; }
            internal set { _name = value; }
        }

        /// <summary>
        /// IP address of the host machine.
        /// </summary>
        public string Host
        {
            get { return _host ?? String.Empty; }
            internal set { _host = value; }
        }

        /// <summary>
        /// Length of time the user has been logged on.
        /// </summary>
        /*! \sa [AcDuration.ToString](@ref AcUtils#AcDuration#ToString) */
        public AcDuration Duration
        {
            get { return _duration; }
            internal set { _duration = value; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, 
        /// e.g. <b>Console.WriteLine(rule.ToString("n"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using 
        /// [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Default when not using a format specifier.
        /// \arg \c N Name - User's AccuRev principal name.
        /// \arg \c H Host - IP address of the host machine.
        /// \arg \c D Duration - Length of time the user has been logged on.
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
                    string text = $"{Name}, {Host}, {Duration}";
                    return text;
                }
                case "N": // user's AccuRev principal name
                    return Name;
                case "H": // IP address of the host machine
                    return Host;
                case "D": // length of time user has been logged on
                    return Duration.ToString();
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

    /// <summary>
    /// A container for AcSession objects that define AccuRev user sessions.
    /// </summary>
    [Serializable]
    public sealed class AcSessions : List<AcSession>
    {
        #region class variables
        [NonSerialized] private readonly object _locker = new object();
        #endregion

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container for AcSession objects that define AccuRev user sessions.
        /// </summary>
        /*! \code
            public static async Task<bool> showSessionsAsync()
            {
                AcSessions sessions = new AcSessions();
                if (!(await sessions.initAsync())) return false;

                foreach (AcSession session in sessions
                    .Where(n => n.Duration != TimeSpan.Zero).OrderBy(n => n))
                {
                    Console.WriteLine(session);
                }

                return true;
            }
            \endcode */
        /*! \sa initAsync, [default comparer](@ref AcSession#CompareTo) */
        public AcSessions() { }

        /// <summary>
        /// Populate this container with AcSession objects, the currently active login sessions.
        /// </summary>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) in 
        /// <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \show_ <tt>show -fx sessions</tt> */
        public async Task<bool> initAsync()
        {
            bool ret = false; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync("show -fx sessions").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("Element")
                                                  select element;
                    foreach (XElement e in query)
                    {
                        AcSession session = new AcSession();
                        session.Name = (string)e.Attribute("Username");
                        session.Host = (string)e.Attribute("Host");
                        string temp = (string)e.Attribute("Duration");
                        if (!String.Equals(temp, "(timed out)"))
                        {
                            double duration = double.Parse(temp);
                            session.Duration = TimeSpan.FromMinutes(duration);
                        }

                        lock (_locker) { Add(session); }
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcSessions.initAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcSessions.initAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }
        //@}
        #endregion
    }
}
