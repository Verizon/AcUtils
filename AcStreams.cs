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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    #region enums
    /*! \ingroup acenum */
    /// <summary>
    /// The type of stream.
    /// </summary>
    /*! \accunote_ AccuRev is not consistent in its designation of stream types. The <tt>show streams</tt> command returns 
    \b passthrough and \b normal while the server_admin_trig receives \b passthru and \b regular and the server_master_trig 
    receives /b dynamic. We cover them all here in one enum. */
    public enum StreamType {
        /*! \var unknown
        A defect where \"<b>* unknown *</b>\" is sent. */
        unknown,
        /*! \var dynamic
        A dynamic stream. */
        dynamic,
        /*! \var normal
        A dynamic stream. */
        normal,
        /*! \var regular
        A dynamic stream. */
        regular,
        /*! \var workspace
        A workspace stream. */
        workspace,
        /*! \var snapshot
        A snapshot stream. */
        snapshot,
        /*! \var passthru
        A passthrough stream. */
        passthru,
        /*! \var passthrough
        A passthrough stream. */
        passthrough,
        /*! \var gated
        A gated stream. */
        gated,
        /*! \var staging
        A staging stream. */
        staging
    };
    ///@}
    #endregion

    /// <summary>
    /// A stream object that defines the attributes of an AccuRev stream. 
    /// AcStream objects are instantiated during AcDepot and AcDepots construction.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{Name} ({ID}) {Type}, basis: {BasisName} ({BasisID})")]
    public sealed class AcStream : IFormattable, IEquatable<AcStream>, IComparable<AcStream>, IComparable
    {
        #region class variables
        private string _name; // stream name
        private int _id; // stream ID number
        private string _basisName; // basis stream name
        private int _basisID; // basis stream ID number
        private AcDepot _depot; // depot where the stream resides
        private bool _isDynamic; // false in the case of workspace, snapshot and passthrough, true if normal
        private StreamType _type; // unknown, normal, regular, workspace, snapshot, passthru, passthrough
        private DateTime? _time;  // basis time; XML attribute "time" only exists for snapshot streams (creation date) or when a dynamic stream has a time basis
        // note, the result of specifying 'As transaction #' in the GUI results in a time basis set on the stream
        private DateTime _startTime; // time the stream was created
        private bool _hidden; // hidden (removed) stream
        private bool _hasDefaultGroup; // true if the stream has a default group
        #endregion

        /// <summary>
        /// AcStream objects are instantiated during AcDepot and AcDepots construction. 
        /// This constructor is called internally and not by user code. 
        /// </summary>
        internal AcStream() { }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcStream. 
        /// Uses the stream ID number and depot to compare instances.
        /// </summary>
        /// <param name="other">The AcStream object being compared to \e this instance.</param>
        /// <returns>\e true if AcStream \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcStream other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            var left = Tuple.Create(ID, Depot);
            var right = Tuple.Create(other.ID, other.Depot);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcStream)](@ref AcStream#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as AcStream);
        }

        /// <summary>
        /// Override appropriate for type AcStream.
        /// </summary>
        /// <returns>Hash of stream ID number and depot.</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(ID, Depot);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcStream objects to sort by stream name.
        /// </summary>
        /// <param name="other">An AcStream object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcStream objects being compared.</returns>
        /*! \sa [AcStreams constructor](@ref AcUtils#AcStreams#AcStreams) */
        public int CompareTo(AcStream other)
        {
            int result;
            if (AcStream.ReferenceEquals(this, other))
                result = 0;
            else
                result = String.Compare(Name, other.Name);

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcStream object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcStream)](@ref AcStream#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcStream object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcStream))
                throw new ArgumentException("Argument is not an AcStream", "other");
            AcStream o = (AcStream)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Stream name.
        /// </summary>
        public string Name
        {
            get { return _name ?? String.Empty; }
            internal set { _name = value; }
        }

        /// <summary>
        /// Stream ID number.
        /// </summary>
        public int ID
        {
            get { return _id; }
            internal set { _id = value; }
        }

        /// <summary>
        /// Basis stream name.
        /// </summary>
        public string BasisName
        {
            get { return _basisName ?? String.Empty; }
            internal set { _basisName = value; }
        }

        /// <summary>
        /// Basis stream ID number.
        /// </summary>
        public int BasisID
        {
            get { return _basisID; }
            internal set { _basisID = value; }
        }

        /// <summary>
        /// Depot the stream is located in.
        /// </summary>
        public AcDepot Depot
        {
            get { return _depot; }
            internal set { _depot = value; }
        }

        /// <summary>
        /// Whether the stream is a dynamic stream or not. \e false in the 
        /// case of a workspace, snapshot or passthrough stream.
        /// </summary>
        public bool IsDynamic
        {
            get { return _isDynamic; }
            internal set { _isDynamic = value; }
        }

        /// <summary>
        /// The kind of stream: \e unknown, \e normal, \e regular, \e workspace, 
        /// \e snapshot, \e passthru, or \e passthrough.
        /// </summary>
        public StreamType Type
        {
            get { return _type; }
            internal set { _type = value; }
        }

        /// <summary>
        /// Stream's time basis or when the snapshot stream was created, otherwise \e null.
        /// </summary>
        /// <remarks>The XML attribute \e time exists only for snapshot streams 
        /// or when a dynamic stream has a time basis.</remarks>
        /*! \note The result of using <b>As transaction #</b> in the GUI results 
             in a time basis set on the stream. */
        public DateTime? Time
        {
            get { return _time; }
            internal set { _time = value; }
        }

        /// <summary>
        /// Time the stream was created.
        /// </summary>
        public DateTime StartTime
        {
            get { return _startTime; }
            internal set { _startTime = value; }
        }

        /// <summary>
        /// Whether the stream is hidden or not.
        /// </summary>
        public bool Hidden
        {
            get { return _hidden; }
            internal set { _hidden = value; }
        }

        /// <summary>
        /// Whether the stream has a default group or not.
        /// </summary>
        public bool HasDefaultGroup
        {
            get { return _hasDefaultGroup; }
            internal set { _hasDefaultGroup = value; }
        }

        /// <summary>
        /// Get this stream's basis (parent) stream.
        /// </summary>
        /// <returns>Basis AcStream object for this stream or \e null if not found.</returns>
        public AcStream getBasis()
        {
            AcStream basis = _depot.getBasis(_id);
            return basis;
        }

        #region ToString
        /// <summary>
        /// The actual implementation of the \e ToString method.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(stream.ToString("lv"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Stream name. Default when not using a format specifier.
        /// \arg \c LV Long version (verbose).
        /// \arg \c I Stream ID number.
        /// \arg \c T [Stream type](@ref AcUtils#StreamType):  \e unknown, \e normal, \e regular, \e workspace, \e snapshot, \e passthru, or \e passthrough.
        /// \arg \c BT Stream's basis time: snapshot stream creation or dynamic stream time basis.
        /// \arg \c C Time the stream was created.
        /// \arg \c BN Basis stream name.
        /// \arg \c BI Basis stream ID number.
        /// \arg \c D Depot name.
        /// \arg \c DY \e True if dynamic, \e False in the case of workspace, snapshot or passthrough.
        /// \arg \c H \e True if stream is hidden, \e False otherwise.
        /// \arg \c DG \e True if stream has a default group, \e False otherwise.
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
                case "G": // stream name; default when not using a format specifier
                    return Name; // general format should be short since it can be called by anything
                case "LV": // long version (verbose)
                {
                    string text = String.Format("{1} ({2}) {{{3}}} {4}{0}Basis: {5} ({6}){0}Depot: {7}, Hidden: {8}{9}",
                        Environment.NewLine, Name, ID, Type, Time, BasisName, BasisID, Depot, Hidden,
                        (Hidden) ? "" : ", HasDefaultGroup: " + HasDefaultGroup);
                    return text;
                }
                case "I": // stream's ID number
                    return ID.ToString();
                case "T": // type of stream: unknown, normal, regular, dynamic, workspace, snapshot, passthru, or passthrough
                    return Type.ToString();
                case "BT": // stream's time basis
                    return Time.ToString();
                case "C": // time the stream was created
                    return StartTime.ToString();
                case "BN": // basis stream name
                    return BasisName;
                case "BI": // basis stream ID number
                    return BasisID.ToString();
                case "D": // depot name
                    return Depot.ToString();
                case "DY": // true if dynamic, false in the case of workspace, snapshot or passthrough
                    return IsDynamic.ToString();
                case "H": // True if stream is hidden, False otherwise
                    return Hidden.ToString();
                case "DG": // True if stream has a default group, False otherwise
                    return HasDefaultGroup.ToString();
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
    /// A container of AcStream objects that define AccuRev streams. 
    /// AcStream objects are instantiated during AcDepot and AcDepots construction.
    /// </summary>
    [Serializable]
    public sealed class AcStreams : List<AcStream>
    {
        #region class variables
        private bool _dynamicOnly; // true if request is for dynamic streams only
        private bool _includeHidden; // true if removed streams should be included
        [NonSerialized] private readonly object _locker = new object();
        #endregion

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container of AcStream objects that define AccuRev streams. 
        /// AcStream objects are instantiated during AcDepot and AcDepots construction. 
        /// This constructor is called internally and not by user code.
        /// </summary>
        /// <param name="dynamicOnly">\e true for dynamic streams only, \e false for all stream types.</param>
        /// <param name="includeHidden">\e true to include hidden (removed) streams, otherwise do not include hidden streams.</param>
        /*! \code
            // get streams and workspaces in the repository with DEV3 in their name that have a default group
            AcDepots depots = new AcDepots(); // two-part object construction
            if (!(await depots.initAsync())) return false; // operation failed, check log file

            foreach(AcDepot depot in depots.OrderBy(n => n))
            {
                IEnumerable<AcStream> filter = depot.Streams.Where(n => n.Name.Contains("DEV3") && n.HasDefaultGroup == true);
                foreach (AcStream stream in filter.OrderBy(n => n))
                    Console.WriteLine(stream.ToString("lv") + Environment.NewLine);
            }
            \endcode */
        /*! \sa initAsync, [default comparer](@ref AcStream#CompareTo) */
        internal AcStreams(bool dynamicOnly, bool includeHidden)
        {
            _dynamicOnly = dynamicOnly;
            _includeHidden = includeHidden;
        }

        /// <summary>
        /// Populate this container with AcStream objects as per constructor parameters. 
        /// AcStream objects are instantiated during AcDepot and AcDepots construction. 
        /// This function is called internally and not by user code.
        /// </summary>
        /// <param name="depot">The depot for which streams will be created.</param>
        /// <param name="listfile">List file <tt>\%APPDATA\%\\AcTools\\<prog_name\>\\<depot_name\>.streams</tt> for the 
        /// <a href="https://supportline.microfocus.com/Documentation/books/AccuRev/AccuRev/7.0.1/webhelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_show.html">show -l <list-file> streams</a> 
        /// command if found, otherwise \e null.<br>
        /// Example: <tt>"C:\Users\barnyrd\AppData\Roaming\AcTools\FooApp\NEPTUNE.streams"</tt>.</param>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure 
        /// to handle a range of exceptions.</exception>
        /*! \sa [AcStreams constructor](@ref AcUtils#AcStreams#AcStreams) */
        /*! \show_ <tt>show \<-fxig|-fxg\> -p \<depot\> [-l listfile] streams</tt> */
        /*! \accunote_ The following XML attributes from <tt>show -fx -p \<depot\> streams</tt> may exist depending on the version of AccuRev in use. 
        They are used internally by AccuRev and are not intended for customer usage. Micro Focus incident #3132463.
- \e eventStream: Efficient way to determine if the server process needs to fire the event trigger processing.
- \e eventStreamHWM: Used by GitCentric to track the high watermark for synchronization.
- \e hasProperties: Efficient way for the GUI to determine if the property icon is displayed in the StreamBrowser. */
        internal async Task<bool> initAsync(AcDepot depot, string listfile = null)
        {
            bool ret = false; // assume failure
            try
            {
                AcResult result = await AcCommand
                    .runAsync($@"show {(_includeHidden ? "-fxig" : "-fxg")} -p ""{depot}"" {((listfile == null) ? String.Empty : "-l " + "" + listfile + "")} streams")
                    .ConfigureAwait(false);
                if (result != null && result.RetVal == 0)
                {
                    XElement xml = XElement.Parse(result.CmdResult);
                    IEnumerable<XElement> filter = null;
                    if (_dynamicOnly)
                        filter = from s in xml.Descendants("stream")
                                 where (bool)s.Attribute("isDynamic") == true select s;
                    else
                        filter = from s in xml.Descendants("stream") select s;

                    foreach (XElement e in filter)
                    {
                        AcStream stream = new AcStream();
                        stream.Name = (string)e.Attribute("name");
                        stream.ID = (int)e.Attribute("streamNumber");
                        stream.BasisName = (string)e.Attribute("basis") ?? String.Empty;
                        stream.BasisID = (int?)e.Attribute("basisStreamNumber") ?? -1;
                        stream.Depot = depot;
                        stream.IsDynamic = (bool)e.Attribute("isDynamic");
                        string type = (string)e.Attribute("type");
                        stream.Type = (StreamType)Enum.Parse(typeof(StreamType), type);
                        // basis time; XML attribute "time" only exists for snapshot streams (creation date) or when a dynamic stream has a time basis
                        long time = (long?)e.Attribute("time") ?? 0;
                        if (time != 0)
                            stream.Time = AcDateTime.AcDate2DateTime(time);
                        long startTime = (long)e.Attribute("startTime");
                        stream.StartTime = (DateTime)AcDateTime.AcDate2DateTime(startTime);
                        // hidden attribute exists only if the stream is hidden
                        stream.Hidden = (e.Attribute("hidden") != null);
                        stream.HasDefaultGroup = (bool)e.Attribute("hasDefaultGroup");
                        lock (_locker) { Add(stream); }
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcStreams.initAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcStreams.initAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }
        //@}
        #endregion

        /// <summary>
        /// Get the AcStream object for stream \e name.
        /// </summary>
        /// <param name="name">Name of stream to query.</param>
        /// <returns>AcStream object for stream \e name, otherwise \e null if not found.</returns>
        public AcStream getStream(string name)
        {
            return this.SingleOrDefault(n => String.Equals(n.Name, name));
        }
    }
}
