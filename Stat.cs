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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace AcUtils
{
    /*! \ingroup acenum */
    /// <summary>
    /// Indicates the element's type. By default, AccuRev determines 
    /// the element type for a newly created version automatically.
    /// </summary>
    /*! \accunote_ A defect exists where the XML \e elem_type attribute value 
        can be the string \"<b>* unknown *</b>\". */
    /*! \accunote_ (\e external)(\e elink) elements are grayed out in the Mac GUI and cannot be 
        added to the depot. Defect 1101826. */
    public enum ElementType {
        /*! \var unknown
        Should not occur normally except for noted defect. */
        unknown = 0,
        /*! \var dir
        1: The element is a folder. */
        dir = 1,
        /*! \var text
        2: The element is a text file. */
        text = 2,
        /*! \var binary
        3: The element is a binary file. */
        binary = 3,
        /*! \var ptext
        4: Text files that are handled the same as binary files with no end-of-line manipulation. */
        ptext = 4,
        /*! \var elink
        5: An element-link. */
        elink = 5,
        /*! \var slink
        6: A symbolic-link. */
        slink = 6,
        /*! \var unsupported
        99: An unsupported type. */
        unsupported = 99
    };

    /// <summary>
    /// Defines the attributes of an element from the \c stat command.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{EID} {Location} {Status} {NamedVersion}")]
    public sealed class Element : IFormattable, IEquatable<Element>, IComparable<Element>, IComparable
    {
        #region class variables
        private string _location;
        private bool _folder;
        private bool _executable;
        private int _eid;
        private ElementType _elementType;
        private long _size;
        private DateTime? _modTime;
        private string _hierType;
        private int _virStreamNumber;
        private int _virVersionNumber;
        private string _namedVersion; // named stream\version number format, e.g. MARS_STAGE\7 or MARS_STAGE_barnyrd\24
        private int _realStreamNumber;
        private int _realVersionNumber;
        private string _lapStream;
        // AccuRev tech support, overlapInWs "was put in place for a feature that was started 
        // and then shelved and may be removed in a future release. Currently it is always true."
        // private bool _overlapInWs;
        private string _timeBasedStream;
        private string _status;
        #endregion

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>IEquatable implementation to determine the equality of instances of type Element.</summary>
        /// <remarks>Uses the element's EID, virtual stream/version numbers, real stream/version numbers, 
        /// and named version to compare instances.</remarks>
        /// <param name="other">The Element object being compared to \e this instance.</param>
        /// <returns>\e true if Element \e other is the same, \e false otherwise.</returns>
        public bool Equals(Element other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            var left = Tuple.Create(EID, VirStreamNumber, VirVersionNumber, RealStreamNumber, RealVersionNumber, NamedVersion);
            var right = Tuple.Create(other.EID, other.VirStreamNumber, other.VirVersionNumber,
                other.RealStreamNumber, other.RealVersionNumber, other.NamedVersion);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(Element)](@ref AcUtils#Element#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return Equals(other as Element);
        }

        /// <summary>
        /// Override appropriate for type Element.
        /// </summary>
        /// <returns>Hash of element's EID, virtual stream/version numbers, real stream/version numbers, 
        /// and named version.</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(EID, VirStreamNumber, VirVersionNumber, RealStreamNumber, RealVersionNumber, NamedVersion);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing Element objects 
        /// to sort by [element location](@ref AcUtils#Element#Location).
        /// </summary>
        /// <param name="other">An Element object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the Element objects based on location.</returns>
        public int CompareTo(Element other)
        {
            int result;
            if (Element.ReferenceEquals(this, other))
                result = 0;
            else
                result = Location.CompareTo(other.Location);

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An Element object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(Element)](@ref AcUtils#Element#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an Element object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is Element))
                throw new ArgumentException("Argument is not an Element", "other");
            Element o = (Element)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Depot-relative path of the element.
        /// </summary>
        public string Location
        {
            get { return _location ?? String.Empty; }
            internal set { _location = value; }
        }

        /// <summary>
        /// \e true if the element is a folder, \e false otherwise.
        /// </summary>
        public bool Folder
        {
            get { return _folder; }
            internal set { _folder = value; }
        }

        /// <summary>
        /// UNIX/Linux systems only: \e true if the executable bit is set, \e false if cleared.
        /// </summary>
        /*! \sa [AccuRev chmode command](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_chmod.html) */
        public bool Executable
        {
            get { return _executable; }
            internal set { _executable = value; }
        }

        /// <summary>
        /// The element ID.
        /// </summary>
        public int EID
        {
            get { return _eid; }
            internal set { _eid = value; }
        }

        /// <summary>
        /// The element's type: \e dir, \e text, \e binary, \e ptext, \e elink, or \e slink.
        /// </summary>
        public ElementType ElementType
        {
            get { return _elementType; }
            internal set { _elementType = value; }
        }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        /*! \sa [Convert file size bytes to megabyte/gigabyte string using C#](http://www.joe-stevens.com/2009/10/21/convert-file-size-bytes-to-megabytegigabyte-string-using-c/) */
        public long Size
        {
            get { return _size; }
            internal set { _size = value; }
        }

        /// <summary>
        /// The element's modification time.
        /// </summary>
        public DateTime? ModTime
        {
            get { return _modTime; }
            internal set { _modTime = value; }
        }

        /// <summary>
        /// "Hierarchy type" of the element: \e parallel or \e serial.
        /// </summary>
        public string HierType
        {
            get { return _hierType ?? String.Empty; }
            internal set { _hierType = value; }
        }

        /// <summary>
        /// Virtual stream number.
        /// </summary>
        public int VirStreamNumber
        {
            get { return _virStreamNumber; }
            internal set { _virStreamNumber = value; }
        }

        /// <summary>
        /// Virtual version number.
        /// </summary>
        public int VirVersionNumber
        {
            get { return _virVersionNumber; }
            internal set { _virVersionNumber = value; }
        }

        /// <summary>
        /// Named stream\\version number designation, e.g. \c MARS_MAINT3\17 or \c MARS_MAINT3_barnyrd\22
        /// </summary>
        public string NamedVersion
        {
            get { return _namedVersion ?? String.Empty; }
            internal set { _namedVersion = value; }
        }

        /// <summary>
        /// Real stream number.
        /// </summary>
        public int RealStreamNumber
        {
            get { return _realStreamNumber; }
            internal set { _realStreamNumber = value; }
        }

        /// <summary>
        /// Real version number.
        /// </summary>
        public int RealVersionNumber
        {
            get { return _realVersionNumber; }
            internal set { _realVersionNumber = value; }
        }

        /// <summary>
        /// Stream where the element is located when it has (\e underlap)(\e member) or (\e overlap)(\e member) status. 
        /// </summary>
        /// <remarks>Include the \c -B option in the \c stat command to initialize this field.</remarks>
        /*! \sa [Example XML from stat command](@ref findOvrUnder) */
        public string LapStream
        {
            get { return _lapStream ?? String.Empty; }
            internal set { _lapStream = value; }
        }

        /// <summary>
        /// Name of the first time-based stream found when <tt>stat -s \<stream\> -o -B -fox</tt> is 
        /// used to retrieve elements with (\e overlap) and/or (\e underlap) status in the specified stream and higher. 
        /// Returns the specified <tt>-s \<stream\></tt> name if it is a time-based stream.
        /// </summary>
        /// <remarks>Without \c -B, the \c -fo option does nothing. It should be viewed as a modifier of the \c -B option, 
        /// as it causes the search to include (\e overlap) and (\e underlap) elements above time-based streams. 
        /// (\c -B without \c -fo stops when the first time-based stream is encountered and does not include these elements.) 
        /// The \c -fox option is required to initialize this property. The \c -o option is required in all cases. 
        /// The XML emitted via \c -fx includes the element-ID, so there's no need to include 'e' as in \c -foex to ensure 
        /// [Element.EID](@ref AcUtils#Element#EID) is initialized.</remarks>
        /*! \sa [Example XML from stat command](@ref findOvrUnder) */
        public string TimeBasedStream
        {
            get { return _timeBasedStream ?? String.Empty; }
            internal set { _timeBasedStream = value; }
        }

        /// <summary>
        /// The version's status, e.g. (\e kept)(\e member)
        /// </summary>
        public string Status
        {
            get { return _status ?? String.Empty; }
            internal set { _status = value; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(elem.ToString("fs"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types 
        /// using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c LV Long version (verbose).
        /// \arg \c G Location, the depot-relative path of the element (default when not using a format specifier).
        /// \arg \c F \e True if the element is a folder, \e False otherwise.
        /// \arg \c E \e True if the executable bit is set, \e False if cleared. UNIX/Linux systems only.
        /// \arg \c I Element ID.
        /// \arg \c T The element's type: \e dir, \e text, \e binary, \e ptext, \e elink, or \e slink.
        /// \arg \c FS File size in bytes.
        /// \arg \c MT Element's modification time.
        /// \arg \c H "Hierarchy type" of the element, one of two possible values: \e parallel or \e serial.
        /// \arg \c V Virtual stream\\version number format, e.g. \c 5\12
        /// \arg \c R Real stream\\version number format.
        /// \arg \c N The named stream-name\\version-number designation, e.g. \c MARS_STAGE\7 or \c MARS_STAGE_barnyrd\24
        /// \arg \c L Stream where the element is located when it has (\e underlap)(\e member) or (\e overlap)(\e member) status.
        /// \arg \c TB Name of first time-based stream found when <tt>stat -s \<stream\> -o -B -fox</tt> is used to retrieve elements with (\e overlap) and/or (\e underlap) status.
        /// \arg \c S The version's status, e.g. (\e kept)(\e member)
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
                case "LV": // long version (verbose)
                {
                    string text;
                    if (_modTime != null)
                    {
                        text = String.Format("{1}, {2}{0}\tEID: {3} {{{4}}}, Size: {5}, ModTime: {6},{0}\t{7}, Virtual: {8}\\{9}, Real: {10}\\{11}",
                            Environment.NewLine, Location, Status, EID, ElementType, Size, ModTime, NamedVersion, 
                            VirStreamNumber, VirVersionNumber, RealStreamNumber, RealVersionNumber);
                    }
                    else
                    {
                        text = String.Format("{1}, {2}{0}\tEID: {3} {{{4}}}{0}\t{5}, Virtual: {6}\\{7}, Real: {8}\\{9}",
                            Environment.NewLine, Location, Status, EID, ElementType, NamedVersion, 
                            VirStreamNumber, VirVersionNumber, RealStreamNumber, RealVersionNumber);
                    }

                    return text;
                }
                case "G":  // location, the depot-relative path of the element (default when not using a format specifier)
                    return Location;
                case "F":  // True if the element is a folder, False otherwise
                    return Folder.ToString();
                case "E":  // UNIX/Linux systems only: True if the executable bit is set, False if cleared
                    return Executable.ToString();
                case "I":  // element ID
                    return EID.ToString();
                case "T":  // element's type: dir, text, binary, ptext, elink, or slink
                    return ElementType.ToString();
                case "FS": // file size in bytes
                    return Size.ToString();
                case "MT": // element's modification time
                    return ModTime.ToString();
                case "H": // hierarchy type: parallel or serial
                    return HierType;
                case "V": // virtual stream\\version number designation, e.g. 5\12
                    return $"{VirStreamNumber}\\{VirVersionNumber}";
                case "R": // real stream\\version number designation, e.g. 5\12
                    return $"{RealStreamNumber}\\{RealVersionNumber}";
                case "N": // named stream\version number format, e.g. MARS_STAGE\7 or MARS_STAGE_barnyrd\24
                    return NamedVersion;
                case "L": // stream where element is located when it has (\e underlap)(\e member) or (\e overlap)(\e member) status
                    return LapStream;
                case "TB": // first time-based stream found when 'stat -s stream -o -B -fox' is used to retrieve elements with (overlap) and/or (underlap) status
                    return TimeBasedStream;
                case "S": // version's status, e.g. (kept)(member)
                    return Status;
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
    /// The list of Element objects from the \c stat command.
    /// </summary>
    /*! \code
        try {
            AcResult result = await AcCommand.runAsync(@"stat -foix -B -d -s ""NEPTUNE_DEV2_barnyrd""");
            if (result == null || result.RetVal != 0) return false; // error occurred, check log file
            if (Stat.init(result.CmdResult)) // populate list using XML from the stat command
                foreach (Element elem in Stat.Elements.OrderBy(n => n.Location).ThenByDescending(n => n.ModTime))
                    Console.WriteLine(elem.ToString("LV"));
        }

        catch (AcUtilsException ecx)
        {
            Console.WriteLine($"AcUtilsException caught in Program.show{Environment.NewLine}{ecx.Message}");
        }

        catch (Exception ecx)
        {
            Console.WriteLine($"Exception caught in Program.show{Environment.NewLine}{ecx.Message}");
        }
        \endcode */
    [Serializable]
    public static class Stat
    {
        #region class variables
        private static List<Element> _elements = new List<Element>();
        [NonSerialized] private static readonly object _locker = new object();
        #endregion

        /// <summary>
        /// The list of Element objects.
        /// </summary>
        public static List<Element> Elements
        {
            get { return _elements; }
            private set { _elements = value; }
        }

        /// <summary>
        /// Remove all elements from the (static) element's list.
        /// </summary>
        public static void clear()
        {
            _elements.Clear();
        }

        /// <summary>
        /// Populate this list with elements from the XML emitted by the 
        /// [stat](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_stat.html) command.
        /// </summary>
        /// <param name="xml">XML from the AccuRev \c stat command.</param>
        /// <returns>\e true if parsing was successful, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public static bool init(string xml)
        {
            bool ret = false; // assume failure
            try
            {
                XElement elements = XElement.Parse(xml);
                IEnumerable<XElement> query = from e in elements.Descendants("element")
                                              where !e.Attribute("status").Value.Contains("no such elem")
                                              select e;
                foreach (XElement e in query)
                {
                    Element element = new Element();
                    element.Status = (string)e.Attribute("status") ?? String.Empty;
                    element.Location = (string)e.Attribute("location") ?? String.Empty;
                    string dir = (string)e.Attribute("dir") ?? String.Empty;
                    element.Folder = String.Equals(dir, "yes");
                    string exe = (string)e.Attribute("executable") ?? String.Empty;
                    element.Executable = String.Equals(exe, "yes");
                    element.EID = (int?)e.Attribute("id") ?? 0;
                    string etype = (string)e.Attribute("elemType") ?? String.Empty;
                    element.ElementType = String.IsNullOrEmpty(etype) ?
                        ElementType.unknown : (ElementType)Enum.Parse(typeof(ElementType), etype);
                    element.Size = (long?)e.Attribute("size") ?? 0;
                    long modtime = (long?)e.Attribute("modTime") ?? 0;
                    if (modtime != 0)
                        element.ModTime = AcDateTime.AcDate2DateTime(modtime);
                    element.HierType = (string)e.Attribute("hierType") ?? String.Empty;
                    int ival;
                    string vir = (string)e.Attribute("Virtual") ?? String.Empty;
                    if (!String.IsNullOrEmpty(vir))
                    {
                        string[] arrVir = vir.Split('\\');
                        if (Int32.TryParse(arrVir[0], NumberStyles.Integer, null, out ival))
                            element.VirStreamNumber = ival;
                        if (Int32.TryParse(arrVir[1], NumberStyles.Integer, null, out ival))
                            element.VirVersionNumber = ival;
                    }
                    element.NamedVersion = (string)e.Attribute("namedVersion") ?? String.Empty;
                    string real = (string)e.Attribute("Real") ?? String.Empty;
                    if (!String.IsNullOrEmpty(real))
                    {
                        string[] arrReal = real.Split('\\');
                        if (Int32.TryParse(arrReal[0], NumberStyles.Integer, null, out ival))
                            element.RealStreamNumber = ival;
                        if (Int32.TryParse(arrReal[1], NumberStyles.Integer, null, out ival))
                            element.RealVersionNumber = ival;
                    }
                    element.LapStream = (string)e.Attribute("overlapStream") ?? String.Empty;
                    element.TimeBasedStream = (string)e.Attribute("timeBasisStream") ?? String.Empty;
                    lock (_locker) { _elements.Add(element); }
                }

                ret = true; // operation succeeded
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Stat.init{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        /// <summary>
        /// Get the \c stat command's Element object from this list that corresponds to the \e version element in a transaction from the \c hist command.
        /// </summary>
        /// <param name="version">The version element to query.</param>
        /*! \code
            <version path="\.\foo.java" eid="65" virtual="11/8" real="19/5" virtualNamedVersion="MARS_DEV2/8" realNamedVersion="MARS_DEV2_barnyrd/5" elem_type="text" dir="no" />
            \endcode */
        /// <returns>The Element object that corresponds to the \e version element if found, otherwise \e null.</returns>
        /*! 
        The Element object and \e version element relate when all of the following conditions are true:
        - Their element ID's match.
        - Their respective virtual and real stream/version numbers match.
        - The Element.NamedVersion stream name matches the version's \e virtualNamedVersion or \e realNamedVersion stream name.
        */
        /*! \sa <a href="_latest_promotions_8cs-example.html">LatestPromotions.cs</a>, <a href="_latest_overlaps_8cs-example.html">LatestOverlaps.cs</a> */
        public static Element getElement(XElement version)
        {
            Debug.Assert(version.Name == "version", @"version.Name == ""version""");
            int? virEID = (int?)version.Attribute("eid");
            if (virEID == null) return null;

            int[] virStreamVersion = version.acxStreamVersion(RealVirtual.Virtual);
            if (virStreamVersion == null) return null;
            int[] realStreamVersion = version.acxStreamVersion(RealVirtual.Real);
            if (realStreamVersion == null) return null;

            string virStream = version.acxStreamName(RealVirtual.Virtual);
            if (virStream == null) return null;
            string realStream = version.acxStreamName(RealVirtual.Real);
            if (realStream == null) return null;

            IEnumerable<Element> query = (from e in _elements
                                          where e.EID == virEID &&
                                          e.VirStreamNumber == virStreamVersion[0] && e.VirVersionNumber == virStreamVersion[1] &&
                                          e.RealStreamNumber == realStreamVersion[0] && e.RealVersionNumber == realStreamVersion[1] &&
                                          (e.NamedVersion.StartsWith(virStream) || e.NamedVersion.StartsWith(realStream))
                                          select e).Distinct();
            return query.SingleOrDefault();
        }
    }
}

