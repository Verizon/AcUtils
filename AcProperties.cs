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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    #region enums
    /*! \ingroup acenum */
    /// <summary>
    /// Type of the property, either \e principal or \e stream.
    /// </summary>
    public enum PropKind
    {
        /*! \var principal
        The property kind is a principal. */
        principal,
        /*! \var stream
        The property kind is a stream. */
        stream
    }
    ///@}
    #endregion

    /// <summary>
    /// A property object that defines the attributes of an AccuRev property 
    /// assigned to a stream or principal by the \c setproperty command.
    /// </summary>
    [Serializable]
    public sealed class AcProperty : IFormattable, IEquatable<AcProperty>, IComparable<AcProperty>, IComparable
    {
        #region class variables
        private PropKind _kind; // type of property, either "principal" or "stream"
        private AcDepot _depot; // depot when Kind is a stream, otherwise null
        private int _id; // stream or principal ID number
        private string _name; // stream or principal name
        private string _propName; // property name
        private string _propValue; // property value
        #endregion

        /// <summary>
        /// Constructor used during AcProperties list construction. It is called internally and not by user code. 
        /// </summary>
        internal AcProperty() { }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcProperty. 
        /// Uses [Kind](@ref AcUtils#PropKind), depot (when [Kind](@ref AcUtils#PropKind) is \e stream, otherwise \e null), 
        /// stream or principal ID number, and property name to compare instances.
        /// </summary>
        /// <param name="other">The AcProperty object being compared to \e this instance.</param>
        /// <returns>\e true if AcProperty \e rhs is the same, \e false otherwise.</returns>
        public bool Equals(AcProperty other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            var left = Tuple.Create(Kind, Depot, ID, PropName);
            var right = Tuple.Create(other.Kind, other.Depot, other.ID, other.PropName);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcProperty)](@ref AcProperty#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as AcProperty);
        }

        /// <summary>
        /// Override appropriate for type AcProperty.
        /// </summary>
        /// <returns>Hash of [Kind](@ref AcUtils#PropKind), depot (when [Kind](@ref AcUtils#PropKind) is \e stream, otherwise \e null), 
        /// stream or principal number, and property name.</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(Kind, Depot, ID, PropName);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcProperty objects. 
        /// Sorts by [[depot, stream] | principal] name then property name.
        /// </summary>
        /// <param name="other">An AcProperty object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcProperty objects being compared.</returns>
        /*! \sa [AcProperties constructor example](@ref AcUtils#AcProperties#AcProperties) */
        public int CompareTo(AcProperty other)
        {
            int result = 0;
            if (AcProperty.ReferenceEquals(this, other))
                result = 0;
            else
            {
                if (Depot != null && other.Depot != null)
                    result = Depot.CompareTo(other.Depot);
                if (result == 0)
                    result = String.Compare(Name, other.Name);
                if (result == 0)
                    result = String.Compare(PropName, other.PropName);
            }

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcProperty object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcProperty)](@ref AcProperty#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcProperty object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcProperty))
                throw new ArgumentException("Argument is not an AcProperty", "other");
            AcProperty o = (AcProperty)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Kind of property, either \b principal or \b stream.
        /// </summary>
        public PropKind Kind
        {
            get { return _kind; }
            internal set { _kind = value; }
        }

        /// <summary>
        /// Depot when [Kind](@ref AcUtils#PropKind) is \b stream, otherwise \e null.
        /// </summary>
        public AcDepot Depot
        {
            get { return _depot; }
            internal set { _depot = value; }
        }

        /// <summary>
        /// Stream or principal ID number this property is assigned to.
        /// </summary>
        public int ID
        {
            get { return _id; }
            internal set { _id = value; }
        }

        /// <summary>
        /// Stream or principal name this property is assigned to.
        /// </summary>
        public string Name
        {
            get { return _name ?? String.Empty; }
            internal set { _name = value; }
        }

        /// <summary>
        /// Property name.
        /// </summary>
        public string PropName
        {
            get { return _propName ?? String.Empty; }
            internal set { _propName = value; }
        }

        /// <summary>
        /// Property value.
        /// </summary>
        public string PropValue
        {
            get { return _propValue ?? String.Empty; }
            internal set { _propValue = value; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(stream.ToString("pn"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Depot stream/principal name along with the name-value property pair (default when not using a format specifier).
        /// \arg \c K [Kind](@ref AcUtils#PropKind).
        /// \arg \c D Depot name when [Kind](@ref AcUtils#PropKind) is a stream, otherwise empty.
        /// \arg \c I Stream or principal ID number this property is assigned to.
        /// \arg \c N Stream or principal name this property is assigned to.
        /// \arg \c PN Property name.
        /// \arg \c PV Property value.
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
                case "G": //  Depot stream/principal name along with the name-value property pair (default when not using a format specifier).
                {
                    string text;
                    if (_depot == null)
                        text = String.Format("{0} ({1}), {2}={3}", Name, ID, PropName, PropValue);
                    else
                        text = String.Format("{0}, {1} ({2}), {3}={4}", Depot, Name, ID, PropName, PropValue);
                    return text;
                }
                case "K": // type of the property, either "principal" or "stream"
                    return Kind.ToString();
                case "D": // depot name when property type is a stream, otherwise an empty string
                    return (Depot == null) ? String.Empty : Depot.ToString();
                case "I": // stream or principal ID number
                    return ID.ToString();
                case "N": // stream or principal name
                    return Name;
                case "PN": // property name
                    return PropName;
                case "PV": // property value
                    return PropValue;
                default:
                    throw new FormatException(String.Format("The {0} format string is not supported.", format));
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
    /// A container of AcProperty objects that define AccuRev properties assigned to a 
    /// stream or principal by the \c setproperty command.
    /// </summary>
    [Serializable]
    public sealed class AcProperties : List<AcProperty>
    {
        #region class variables
        [NonSerialized] private readonly object _locker = new object();
        #endregion

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container of AcProperty objects that define AccuRev properties assigned to a 
        /// stream or principal by the \c setproperty command.
        /// </summary>
        /*! \code
            public static async Task<bool> showPropertiesAsync()
            {
                // true for dynamic streams only
                AcDepot depot = new AcDepot("MARS", true);
                if (!(await depot.initAsync())) return false;

                // get the properties for all dynamic streams in the MARS depot
                AcProperties properties = new AcProperties();
                if (!(await properties.initAsync(depot))) return false;

                foreach (AcProperty prop in properties.OrderBy(n => n))
                    Console.WriteLine(prop);

                return true;
            }

            MARS_DEV3, Release=2015-11-15
            MARS_DEV4, Release=2016-01-17
            MARS_MAINT, Release=2015-10-27
            MARS_MAINT1, Release=2015-10-13
            MARS_MAINT2, Release=2015-11-10
            ...
            \endcode */
        /*! \sa initAsync(string, bool), initAsync(AcDepot, AcStream, bool) */
        public AcProperties() { }

        /// <summary>
        /// Populate this container with AcProperty objects for all streams in \e depot or just \e stream in \e depot. 
        /// Optionally include hidden (removed) streams.
        /// </summary>
        /// <param name="depot">The depot to query for properties.</param>
        /// <param name="stream">The stream for a specific stream only, otherwise \e null for all streams in \e depot.</param>
        /// <param name="includeHidden">\e true to include properties for hidden (removed) streams, \e false otherwise.</param>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) in 
        /// <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c getproperty command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcProperties constructor](@ref AcUtils#AcProperties#AcProperties), initAsync(string, bool) */
        /*! \getproperty_ <tt>getproperty \<-fx | -fix\> \<-s \<stream\> | -ks -p \<depot\>\></tt> */
        public async Task<bool> initAsync(AcDepot depot, AcStream stream = null, bool includeHidden = false)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = null;
                if (stream != null && includeHidden)
                    cmd = String.Format(@"getproperty -fix -s ""{0}""", stream);
                else if (stream != null && !includeHidden)
                    cmd = String.Format(@"getproperty -fx -s ""{0}""", stream);
                else if (includeHidden) // request is for all streams in depot including those that are hidden
                    cmd = String.Format(@"getproperty -fix -ks -p ""{0}""", depot);
                else  // request is for all streams except those that are hidden
                    cmd = String.Format(@"getproperty -fx -ks -p ""{0}""", depot);

                AcResult r = await AcCommand.runAsync(cmd).ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("property") select element;
                    foreach (XElement e in query)
                    {
                        AcProperty property = new AcProperty();
                        string kind = (string)e.Attribute("kind");
                        property.Kind = (PropKind)Enum.Parse(typeof(PropKind), kind);
                        property.Depot = depot;
                        property.ID = (int)e.Attribute("streamNumber");
                        property.Name = (string)e.Attribute("streamName");
                        property.PropName = (string)e.Attribute("propertyName");
                        property.PropValue = (string)e;
                        lock (_locker) { Add(property); }
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcProperties.initAsync(AcDepot, AcStream, bool){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcProperties.initAsync(AcDepot, AcStream, bool){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        /// <summary>
        /// Populate this container with AcProperty objects for all principals or a specific principal only. 
        /// Optionally include hidden (removed) principals.
        /// </summary>
        /// <param name="prncpl">Principal name or \e null for all principals.</param>
        /// <param name="includeHidden">\e true to include properties for hidden (removed) principals, \e false for all.</param>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) in 
        /// <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c getproperty command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcProperties constructor](@ref AcUtils#AcProperties#AcProperties), initAsync(AcDepot, AcStream, bool) */
        /*! \getproperty_ <tt>getproperty \<-fx | -fix\> \<-u \<prncpl\> | -ku\></tt> */
        public async Task<bool> initAsync(string prncpl = null, bool includeHidden = false)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = null;
                if (!String.IsNullOrEmpty(prncpl) && includeHidden)
                    cmd = String.Format(@"getproperty -fix -u ""{0}""", prncpl);
                else if (!String.IsNullOrEmpty(prncpl) && !includeHidden)
                    cmd = String.Format(@"getproperty -fx -u ""{0}""", prncpl);
                else if (includeHidden) // request is for all principals including those that are hidden
                    cmd = "getproperty -fix -ku";
                else // request is for all principals except those that are hidden
                    cmd = "getproperty -fx -ku";

                AcResult r = await AcCommand.runAsync(cmd).ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("property") select element;
                    foreach (XElement e in query)
                    {
                        AcProperty property = new AcProperty();
                        string kind = (string)e.Attribute("kind");
                        property.Kind = (PropKind)Enum.Parse(typeof(PropKind), kind);
                        property.ID = (int)e.Attribute("principalNumber");
                        property.Name = (string)e.Attribute("principalName");
                        property.PropName = (string)e.Attribute("propertyName");
                        property.PropValue = (string)e;
                        lock (_locker) { Add(property); }
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcProperties.initAsync(string, bool){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcProperties.initAsync(string, bool){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
        //@}
        #endregion
    }
}
