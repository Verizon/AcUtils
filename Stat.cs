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
        This should never occur. */
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
    /// Custom class to sort by element status and then location.
    /// </summary>
    /*! \code
        try
        {
            AcResult result = await AcCommand.runAsync(@"stat -fx -B -d -s ""NEPTUNE_DEV1_barnyrd""");
            if (result == null || result.RetVal != 0) return false; // error occurred, check log file
            Stat.init(result.CmdResult); // populate list using XML from the stat command
            Stat.Elements.Sort(new ElementComparer());
            foreach (Element elem in Stat.Elements)
                Console.WriteLine(elem.ToString("LV"));
        }

        catch (AcUtilsException ecx)
        {
            string msg = String.Format("AcUtilsException caught and logged in Program.show{0}{1}", Environment.NewLine, ecx.Message);
            Console.WriteLine(msg);
        }

        catch (Exception ecx)
        {
            string msg = String.Format("Exception caught and logged in Program.show{0}{1}", Environment.NewLine, ecx.Message);
            Console.WriteLine(msg);
        }
        \endcode */
    public class ElementComparer : IComparer<Element>
    {
        public int Compare(Element x, Element y)
        {
            int result;
            if (Element.ReferenceEquals(x, y))
                result = 0;
            else
            {
                if (x == null)
                    result = 1;
                else if (y == null)
                    result = -1;
                else
                {
                    result = x.Status.CompareTo(y.Status);
                    if (result == 0)
                        result = x.Location.CompareTo(y.Location);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Defines the attributes of an element from the \c stat command.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{EID} {Location} {Status} {NamedVersion}")]
    public sealed class Element : IFormattable
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
        private string _namedVersion;
        private int _realStreamNumber;
        private int _realVersionNumber;
        private string _lapStream;
        // AccuRev tech support, overlapInWs "was put in place for a feature that was started 
        // and then shelved and maybe removed in a future release. Currently it is always true."
        // private bool _overlapInWs;
        private string _timeBasedStream;
        private string _status;
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
        /*! \sa [AccuRev chmode command](http://supportline.microfocus.com/Documentation/books/AccuRev/AccuRev/5.6/webhelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_chmod.html) */
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
        /// Named stream\\version number designation, e.g. \c MARS_STAGE\17
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
        /*! \sa [Example XML from stat](@ref findOvrUnder) */
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
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(user.ToString("fs"));</b></param>
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
        /// \arg \c N The named stream_name\\version_number designation, e.g. \c MARS_STAGE\\7
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
                    return String.Format("{0}\\{1}", VirStreamNumber, VirVersionNumber);
                case "R": // real stream\\version number designation, e.g. 5\12
                    return String.Format("{0}\\{1}", RealStreamNumber, RealVersionNumber);
                case "N": // named stream\version number format, e.g. MARS_STAGE\17
                    return NamedVersion;
                case "L": // stream where element is located when it has (\e underlap)(\e member) or (\e overlap)(\e member) status
                    return LapStream;
                case "TB": // first time-based stream found when 'stat -s stream -o -B -fox' is used to retrieve elements with (overlap) and/or (underlap) status
                    return TimeBasedStream;
                case "S": // version's status, e.g. (kept)(member)
                    return Status;
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
    /// The list of Element objects from the \c stat command.
    /// </summary>
    /*! \code
        try {
            AcResult result = await AcCommand.runAsync(@"stat -foix -B -d -s ""NEPTUNE_DEV2_barnyrd""");
            if (result == null || result.RetVal != 0) return false; // error occurred, check log file
            Stat.init(result.CmdResult); // populate list using XML from the stat command
            foreach (Element elem in Stat.Elements.OrderBy(n => n.Location).ThenByDescending(n => n.ModTime))
                Console.WriteLine(elem.ToString("LV"));
        }

        catch (AcUtilsException ecx)
        {
            string msg = String.Format("AcUtilsException caught and logged in Program.show{0}{1}", Environment.NewLine, ecx.Message);
            Console.WriteLine(msg);
        }

        catch (Exception ecx)
        {
            string msg = String.Format("Exception caught and logged in Program.show{0}{1}", Environment.NewLine, ecx.Message);
            Console.WriteLine(msg);
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
        /// [stat](http://supportline.microfocus.com/Documentation/books/AccuRev/AccuRev/5.6/webhelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_stat.html#1283989) command.
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
                    string vir = (string)e.Attribute("Virtual") ?? String.Empty;
                    string[] arrVir = vir.Split('\\');
                    int ival;
                    if (Int32.TryParse(arrVir[0], NumberStyles.Integer, null, out ival))
                        element.VirStreamNumber = ival;
                    if (Int32.TryParse(arrVir[1], NumberStyles.Integer, null, out ival))
                        element.VirVersionNumber = ival;
                    element.NamedVersion = (string)e.Attribute("namedVersion") ?? String.Empty;
                    string real = (string)e.Attribute("Real") ?? String.Empty;
                    string[] arrReal = real.Split('\\');
                    if (Int32.TryParse(arrReal[0], NumberStyles.Integer, null, out ival))
                        element.RealStreamNumber = ival;
                    if (Int32.TryParse(arrReal[1], NumberStyles.Integer, null, out ival))
                        element.RealVersionNumber = ival;
                    element.LapStream = (string)e.Attribute("overlapStream") ?? String.Empty;
                    element.TimeBasedStream = (string)e.Attribute("timeBasisStream") ?? String.Empty;
                    lock (_locker) { _elements.Add(element); }
                }

                ret = true; // operation succeeded
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in Stat.init{0}{1}", 
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
    }
}
