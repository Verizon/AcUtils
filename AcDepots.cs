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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    #region enums
    /*! \ingroup acenum */
    ///@{
    /// <summary>
    /// Indicates whether the depot is case \e sensitive or \e insensitive (defined when the depot is created).
    /// </summary>
    public enum CaseSensitivity {
        /*! \var insensitive
        The depot is case \e insensitive. */
        insensitive,
        /*! \var sensitive
        The depot is case \e sensitive. */
        sensitive
    };
    ///@}
    #endregion

    /// <summary>
    /// A depot object that defines the attributes of an AccuRev depot.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{ToString(\"lv\")}")]
    public sealed class AcDepot : IFormattable, IEquatable<AcDepot>, IComparable<AcDepot>, IComparable
    {
        #region class variables
        private string _name; // depot name
        private int _id; // depot ID number
        private int _slice; // depot slice number
        private bool _exclusiveLocking; // whether or not all workspaces created for this depot use exclusive file locking
        private CaseSensitivity _case; // whether the depot is case sensitive or insensitive
        private bool _dynamicOnly; // true if request is for dynamic streams only
        private bool _includeHidden; // true if removed streams should be included in the list
        internal AcStreams _streams; // list of streams in this depot as per constructor parameters
        [NonSerialized] private Task<MultiValueDictionary<int, int>> _hierarchy; // [parent,children]
        [NonSerialized] private object _hierSync = null;
        [NonSerialized] private bool _hierInit;
        #endregion

        #region object construction:
        //! \name Object construction:
        //@{
        /// <summary>
        /// Constructor used during AcDepots list construction. It is called internally and not by user code. 
        /// </summary>
        /// <param name="dynamicOnly">\e true for dynamic streams only, \e false for all stream types.</param>
        /// <param name="includeHidden">\e true to include hidden (removed) streams, otherwise do not include hidden streams.</param>
        internal AcDepot(bool dynamicOnly, bool includeHidden)
        {
            _dynamicOnly = dynamicOnly;
            _includeHidden = includeHidden;
        }

        /// <summary>
        /// Constructor for specifying the depot name.
        /// </summary>
        /// <param name="name">Depot name.</param>
        /// <param name="dynamicOnly">\e true for dynamic streams only, \e false for all stream types.</param>
        /// <param name="includeHidden">\e true to include hidden (removed) streams, otherwise do not include hidden streams.</param>
        /*! \sa [AcDepot.listFile](@ref AcUtils#AcDepot#listFile) */
        public AcDepot(string name, bool dynamicOnly = false, bool includeHidden = false)
            : this(dynamicOnly, includeHidden)
        {
            _name = name;
        }

        /// <summary>
        /// Constructor for specifying the depot ID number.
        /// </summary>
        /// <param name="id">Depot ID number.</param>
        /// <param name="dynamicOnly">\e true for dynamic streams only, \e false for all stream types.</param>
        /// <param name="includeHidden">\e true to include hidden (removed) streams, otherwise do not include hidden streams.</param>
        /*! \sa [AcDepot.listFile](@ref AcUtils#AcDepot#listFile) */
        public AcDepot(int id, bool dynamicOnly = false, bool includeHidden = false)
            : this(dynamicOnly, includeHidden)
        {
            _id = id;
        }

        /// <summary>
        /// Initialize this AcDepot object with data from AccuRev as per constructor parameter's depot \e name or \e ID number.
        /// </summary>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa AcDepot(string, bool, bool), AcDepot(int, bool, bool), [AcDepots constructor](@ref AcUtils#AcDepots#AcDepots) */
        /*! \show_ <tt>show -fx depots</tt> */
        public async Task<bool> initAsync()
        {
            bool ret = false; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync("show -fx depots").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> filter = null;
                    if (_id > 0)
                        filter = from element in xml.Elements("Element")
                                 where (int)element.Attribute("Number") == _id
                                 select element;
                    else
                        filter = from element in xml.Elements("Element")
                                 where (string)element.Attribute("Name") == _name
                                 select element;

                    XElement e = filter.SingleOrDefault();
                    if (e != null)
                    {
                        ID = (int)e.Attribute("Number");
                        Name = (string)e.Attribute("Name");
                        Slice = (int)e.Attribute("Slice");
                        ExclusiveLocking = (bool)e.Attribute("exclusiveLocking");
                        string temp = (string)e.Attribute("case");
                        Case = (CaseSensitivity)Enum.Parse(typeof(CaseSensitivity), temp);
                        _streams = new AcStreams(_dynamicOnly, _includeHidden);
                        ret = await _streams.initAsync(this, listFile()).ConfigureAwait(false);
                    }
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcDepot.initAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcDepot.initAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }
        //@}
        #endregion

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcDepot. 
        /// Uses the depot ID number to compare instances.
        /// </summary>
        /// <param name="other">The AcDepot object being compared to \e this instance.</param>
        /// <returns>\e true if AcDepot \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcDepot other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ID == other.ID;
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcDepot)](@ref AcDepot#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as AcDepot);
        }

        /// <summary>
        /// Override appropriate for type AcDepot.
        /// </summary>
        /// <returns>Depot ID number since it's immutable and unique in the repository.</returns>
        public override int GetHashCode()
        {
            return ID;
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcDepot objects to sort by depot name.
        /// </summary>
        /// <param name="other">An AcDepot object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcDepot objects being compared.</returns>
        /*! \sa [AcDepots constructor](@ref AcUtils#AcDepots#AcDepots), <a href="_list_dyn_streams_8cs-example.html">ListDynStreams.cs</a> */
        public int CompareTo(AcDepot other)
        {
            int result;
            if (AcDepot.ReferenceEquals(this, other))
                result = 0;
            else
                result = String.Compare(Name, other.Name);

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcDepot object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcDepot)](@ref AcDepot#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcDepot object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcDepot))
                throw new ArgumentException("Argument is not an AcDepot", "other");
            AcDepot o = (AcDepot)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Depot ID number.
        /// </summary>
        public int ID
        {
            get { return _id; }
            internal set { _id = value; }
        }

        /// <summary>
        /// Depot name.
        /// </summary>
        public string Name
        {
            get { return _name ?? String.Empty; }
            internal set { _name = value; }
        }

        /// <summary>
        /// Depot's slice number.
        /// </summary>
        public int Slice
        {
            get { return _slice; }
            internal set { _slice = value; }
        }

        /// <summary>
        /// Whether or not all workspaces created for this depot use exclusive file locking.
        /// </summary>
        public bool ExclusiveLocking
        {
            get { return _exclusiveLocking; }
            internal set { _exclusiveLocking = value; }
        }

        /// <summary>
        /// Whether the depot is case \e sensitive or \e insensitive.
        /// </summary>
        public CaseSensitivity Case
        {
            get { return _case; }
            internal set { _case = value; }
        }

        /// <summary>
        /// The list of streams in this depot.
        /// </summary>
        public IEnumerable<AcStream> Streams
        {
            get { return _streams; }
        }

        /// <summary>
        /// Get the AcStream object for stream \e name.
        /// </summary>
        /// <param name="name">Stream name to query.</param>
        /// <returns>AcStream object for stream \e name or \e null if not found.</returns>
        public AcStream getStream(string name)
        {
            AcStream stream = _streams.SingleOrDefault(s => s.Name == name);
            return stream;
        }

        /// <summary>
        /// Get the AcStream object with stream \e ID number.
        /// </summary>
        /// <param name="ID">Stream ID number to query.</param>
        /// <returns>AcStream object for stream \e ID number or \e null if not found.</returns>
        public AcStream getStream(int ID)
        {
            AcStream stream = _streams.SingleOrDefault(s => s.ID == ID);
            return stream;
        }

        /// <summary>
        /// Get the basis (parent) stream for stream \e name.
        /// </summary>
        /// <param name="name">Stream name to query.</param>
        /// <returns>Basis AcStream object for stream \e name or \e null if not found.</returns>
        public AcStream getBasis(string name)
        {
            AcStream basis = null;
            // ignore root stream (1) since it has no parent
            AcStream stream = _streams.Where(s => s.ID > 1).SingleOrDefault(s => s.Name == name);
            if (stream != null)
                basis = getStream(stream.BasisID);

            return basis;
        }

        /// <summary>
        /// Get the basis (parent) stream for stream \e ID number.
        /// </summary>
        /// <param name="ID">Stream ID number to query.</param>
        /// <returns>Basis AcStream object for stream \e ID number, otherwise \e null if not found.</returns>
        public AcStream getBasis(int ID)
        {
            AcStream basis = null;
            // ignore root stream (1) since it has no parent
            AcStream stream = _streams.Where(s => s.ID > 1).SingleOrDefault(s => s.ID == ID);
            if (stream != null)
                basis = getStream(stream.BasisID);

            return basis;
        }

        /// <summary>
        /// Run the specified action \e cb for \e stream and all child streams in its hierarchy.
        /// </summary>
        /// <param name="stream">Top-level stream to begin the operation with.</param>
        /// <param name="cb">Delegate to invoke for each stream.</param>
        /// <param name="includeWSpaces">\e true to include workspaces in the list.</param>
        /// <returns>\e true if operation succeeded with no errors, \e false on error.</returns>
        /*! \pre Using \e includeWSpaces to include workspaces requires that all stream types be specified at AcDepot 
             object creation as per the \e dynamicOnly=false (default) constructor parameter. */
        /*! \code
            AcDepot depot = new AcDepot("NEPTUNE", dynamicOnly: true);
            if (!(await depot.initAsync())) return false; // error occurred, check log file

            // list streams beginning with NEPTUNE_DEV2 and its hierarchy
            AcStream stream = depot.getStream("NEPTUNE_DEV2");
            if (!(await depot.forStreamAndAllChildrenAsync(stream, n => Console.WriteLine(n))))
                return false; // ..
            \endcode */
        /*! \sa [AcDepot.getChildrenAsync](@ref AcUtils#AcDepot#getChildrenAsync) */
        /*! \attention To use \e forStreamAndAllChildrenAsync you must deploy 
            <a href="https://www.nuget.org/packages/Microsoft.Experimental.Collections">Microsoft.Experimental.Collections.dll</a> with your application. */
        public async Task<bool> forStreamAndAllChildrenAsync(AcStream stream, Action<AcStream> cb, bool includeWSpaces = false)
        {
            cb(stream);
            var children = await getChildrenAsync(stream, includeWSpaces).ConfigureAwait(false);
            if (children == null) return false; //  operation failed, check log file
            if (children.Item1 == null) return false; // ..
            if (children.Item1 == true) // if children exist
                foreach (AcStream child in children.Item2)
                    await forStreamAndAllChildrenAsync(child, cb, includeWSpaces).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Get the list of child streams that have \e stream as their immediate parent (basis) stream.
        /// </summary>
        /// <param name="stream">Stream to query for child streams.</param>
        /// <param name="includeWSpaces">\e true to include workspaces in the list that have \e stream as their backing stream.</param>
        /// <returns>\e true if children found and the list of child streams, \e false if no children found, or \e null on error.</returns>
        /*! \pre Using \e includeWSpaces to include workspaces in the list requires that all stream types be specified at AcDepot 
             object creation as per the \e dynamicOnly=false (default) constructor parameter. */
        /*! \code
            AcDepot depot = new AcDepot("MARS"); // includes workspace streams
            if (!(await depot.initAsync())) return false; // operation failed, check log file

            AcStream stage = depot.getStream("MARS_STAGE");
            var children = await depot.getChildrenAsync(stage, includeWSpaces: true);
            bool? res = children.Item1;
            if (res == null) return false; // operation failed, check log file

            if (res == false)
                Console.WriteLine("No children!");
            else
            {
                IList<AcStream> list = children.Item2;
                foreach (AcStream child in list)
                    Console.WriteLine(child);
            }
            \endcode */
        /*! \sa [AcDepot.forStreamAndAllChildrenAsync](@ref AcUtils#AcDepot#forStreamAndAllChildrenAsync) */
        /*! \attention To use \e getChildrenAsync you must deploy 
            <a href="https://www.nuget.org/packages/Microsoft.Experimental.Collections">Microsoft.Experimental.Collections.dll</a> with your application. */
        public async Task<Tuple<bool?, IList<AcStream>>> getChildrenAsync(AcStream stream, bool includeWSpaces = false)
        {
            IList<AcStream> children = null;
            if (stream.Type == StreamType.workspace)
                return Tuple.Create((bool?)false, children); // workspaces don't have children

            bool? ret = null;
            // thread safe one-time stream hierarchy initialization
            // see "C# Lazy Initialization && Race-to-initialize" http://stackoverflow.com/questions/11555755/c-sharp-lazy-initialization-race-to-initialize
            await LazyInitializer.EnsureInitialized(ref _hierarchy, ref _hierInit, ref _hierSync, 
                async () => await getHierarchyAsync(includeWSpaces).ConfigureAwait(false)).ConfigureAwait(false);
            if (_hierarchy == null)
                return Tuple.Create(ret, children); // initialization failed, check log file

            IReadOnlyCollection<int> list = null;
            ret = _hierarchy.Result.TryGetValue(stream.ID, out list);
            if (ret == true)
            {
                children = new List<AcStream>(list.Count);
                foreach (int id in list)
                {
                    AcStream child = getStream(id);
                    children.Add(child);
                }
            }

            return Tuple.Create(ret, children);
        }

        /// <summary>
        /// Get the depot's stream and workspace (optional) hierarchy relationship data from AccuRev. 
        /// This method is called internally and not by user code.
        /// </summary>
        /// <param name="includeWSpaces">\e true to include workspaces in the list.</param>
        /// <returns>Fully initialized [MultiValueDictionary](https://www.nuget.org/packages/Microsoft.Experimental.Collections) 
        /// object for the depot with [parent(key),children(values)] basis/stream ID's if no exception was thrown 
        /// and the operation succeeded, otherwise \e null on error.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \pre Using \e includeWSpaces to include workspaces in the list requires that all stream types be specified at AcDepot 
             object creation as per the \e dynamicOnly=false (default) constructor parameter. */
        /*! \show_ <tt>show -p \<depot\> [-fix | -fx] -s 1 -r streams</tt> */
        /*! \attention This method requires that you deploy 
            <a href="https://www.nuget.org/packages/Microsoft.Experimental.Collections">Microsoft.Experimental.Collections.dll</a> with your application. */
        private async Task<MultiValueDictionary<int, int>> getHierarchyAsync(bool includeWSpaces = false)
        {
            MultiValueDictionary<int, int> hierarchy = null;
            try
            {
                AcResult r = await AcCommand.runAsync($@"show -p ""{this}"" {(_includeHidden ? " -fix" : " -fx")} -s 1 -r streams")
                    .ConfigureAwait(false);
                if (r != null && r.RetVal == 0) // if command succeeded
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> filter;
                    if (includeWSpaces)
                        filter = from s in xml.Elements("stream")
                                 select s;
                    else
                        filter = from s in xml.Elements("stream")
                                 where (string)s.Attribute("type") != "workspace" // all except workspaces
                                 select s;

                    int capacity = filter.Count();
                    hierarchy = new MultiValueDictionary<int, int>(capacity);
                    foreach (XElement e in filter)
                    {
                        // XML attribute basisStreamNumber does not exist in the case of root streams
                        int parent = (int?)e.Attribute("basisStreamNumber") ?? -1;
                        int child = (int)e.Attribute("streamNumber");
                        hierarchy.Add(parent, child);
                    }
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcDepot.getHierarchyAsync{Environment.NewLine}{ecx.Message}");
                hierarchy = null;
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcDepot.getHierarchyAsync{Environment.NewLine}{ecx.Message}");
                hierarchy = null;
            }

            return hierarchy;
        }

        /// <summary>
        /// When this file exists (created manually), it is used as the \e list-file to populate the depot with select streams. 
        /// This function is called internally and not by user code.
        /// </summary>
        /// <remarks>Implemented with <a href="https://www.microfocus.com/documentation/accurev/72/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_show.html">show -l <list-file> streams</a>
        /// </remarks>
/*! > -l <list-file> Process the streams listed in the specified file. The file must be text only, containing one stream name or number per line. Extra whitespace is not allowed; make sure there are no empty lines and no leading or trailing whitespace around the filenames. There is no provision for comment lines in a list-file. This option can also be used with one of -1 ("dash-one"), -r, or -R. You cannot also specify streams using the -s option. */
        /// <returns>Full path of the list file <tt>\%APPDATA\%\\AcTools\\<prog_name\>\\<depot_name\>.streams</tt> if found, otherwise \e null.<br>
        /// Example: <tt>"C:\Users\barnyrd\AppData\Roaming\AcTools\FooApp\NEPTUNE.streams"</tt>
        /// </returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        /*! \code
            list-file content:
            NEPTUNE_DEV3
            NEPTUNE_MAINT2
            ...
            AcDepot depot = new AcDepot("NEPTUNE"); // two-part object construction
            if (!(await depot.initAsync())) return false; // operation failed, check log file
            foreach (AcStream stream in depot.Streams.OrderBy(n => n))
                Console.WriteLine(stream.ToString("lv") + Environment.NewLine);
            ...
            NEPTUNE_DEV3 (10) {normal} 
            Basis: NEPTUNE_UAT (8)
            Depot: NEPTUNE, Hidden: False, HasDefaultGroup: True

            NEPTUNE_MAINT2 (6) {normal} 
            Basis: NEPTUNE_MAINT (4)
            Depot: NEPTUNE, Hidden: False, HasDefaultGroup: False
            \endcode */
        internal string listFile()
        {
            string listfile = null;
            try
            {
                string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string actools = Path.Combine(appdata, "AcTools");
                string exeroot = String.Empty;
                using (Process currentProcess = Process.GetCurrentProcess())
                {
                    ProcessModule pm = currentProcess.MainModule;
                    exeroot = Path.GetFileNameWithoutExtension(pm.ModuleName);
                }

                string progfolder = Path.Combine(actools, exeroot);
                string temp = Path.Combine(progfolder, Name);
                string file = Path.ChangeExtension(temp, "streams");
                if (File.Exists(file))
                    listfile = file;
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcDepot.listFile{Environment.NewLine}{ecx.Message}");
            }

            return listfile;
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(depot.ToString("c"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Depot name. Default when not using a format specifier.
        /// \arg \c LV Long version (verbose).
        /// \arg \c I Depot ID number.
        /// \arg \c S Depot's slice number.
        /// \arg \c E Exclusive locking status: \e true or \e false.
        /// \arg \c C [Case sensitivity](@ref AcUtils#CaseSensitivity): \e Sensitive or \e Insensitive.
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
                case "G": // Depot name (default when not using a format specifier).
                    return Name; // general format should be short since it can be called by anything
                case "LV": // long version (verbose)
                    return $"{Name} ({ID}), slice {Slice}, {Case}{(ExclusiveLocking ? ", exclusive locking" : String.Empty)}";
                case "I": // depot ID number
                    return ID.ToString();
                case "S": // depot's slice number
                    return Slice.ToString();
                case "E": // exclusive locking status
                    return ExclusiveLocking.ToString();
                case "C": // whether the depot is case sensitive or insensitive
                    return Case.ToString();
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
    /// A container of AcDepot objects that define AccuRev depots in the repository.
    /// </summary>
    [Serializable]
    public sealed class AcDepots : List<AcDepot>
    {
        #region class variables
        private AcPermissions _permissions;
        [NonSerialized] private Task<bool> _depotPermObj;
        [NonSerialized] private object _permSync = null;
        [NonSerialized] private bool _permInit;
        private bool _dynamicOnly; // true if request is for dynamic streams only
        private bool _includeHidden; // true if removed streams should be included in the list
        [NonSerialized] private readonly object _locker = new object(); // token for lock keyword scope
        [NonSerialized] private int _counter; // used to report initialization progress back to the caller
        #endregion

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container of AcDepot objects that define AccuRev depots in the repository.
        /// </summary>
        /// <param name="dynamicOnly">\e true for dynamic streams only, \e false for all stream types.</param>
        /// <param name="includeHidden">\e true to include hidden (removed) streams, otherwise do not include hidden streams.</param>
        /*! \code
            // list all depots in the repository that are case-sensitive
            AcDepots depots = new AcDepots(dynamicOnly: true); // two-part object construction
            if (!(await depots.initAsync())) return false; // operation failed, check log file

            foreach(AcDepot depot in depots.Where(n => n.Case == CaseSensitivity.sensitive).OrderBy(n => n)) // use default comparer
                Console.WriteLine(depot);
            \endcode */
        /*! \sa initAsync, [default comparer](@ref AcDepot#CompareTo), <a href="_list_dyn_streams_8cs-example.html">ListDynStreams.cs</a>, [AcDepot.listFile](@ref AcUtils#AcDepot#listFile) */
        public AcDepots(bool dynamicOnly = false, bool includeHidden = false)
        {
            _dynamicOnly = dynamicOnly;
            _includeHidden = includeHidden;
        }

        /// <summary>
        /// Populate this container with AcDepot objects as per [constructor parameters](@ref AcUtils#AcDepots#AcDepots).
        /// </summary>
        /// <param name="depotsCol">List of depots to create, otherwise \e null for all depots.</param>
        /// <param name="progress">Optionally report progress back to the caller.</param>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) in 
        /// <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \code
            DepotsSection ds = ConfigurationManager.GetSection("Depots") as DepotsSection;
            DepotsCollection _dcol = ds.Depots; // list of depots from FooApp.exe.config
            ...
            private async Task<bool> initComboBoxDepotsAsync()
            {
                bool ret = false; // assume failure
                try
                {
                    toolStripStatusLabel.Text = "Initializing...";
                    toolStripProgressBar.Visible = true;
                    toolStripProgressBar.Maximum = _dcol.Count;
                    Progress<int> progress = new Progress<int>(i => toolStripProgressBar.Value = i);

                    AcDepots depots = new AcDepots(dynamicOnly: true);
                    if (await depots.initAsync(_dcol, progress)) // if initialization succeeds
                    {
                        foreach (AcDepot depot in depots)
                            toolStripComboBoxDepot.Items.Add(depot); // a ToolStripComboBox control
                        ret = true; // operation succeeded
                    }
                    else
                    {
                        AcDebug.Log("Failure in FooApp.initComboBoxDepotsAsync");
                        MessageBox.Show($"Depots list initialization failed. See log file:{Environment.NewLine}{AcDebug.getLogFile()}",
                            "FooApp", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    }
                }

                catch (Exception ecx)
                {
                    AcDebug.Log($"Exception caught and logged in FooApp.initComboBoxDepotsAsync{Environment.NewLine}{ecx.Message}");
                }

                finally
                {
                    toolStripStatusLabel.Text = "Ready";
                    toolStripProgressBar.Visible = false;
                }

                return ret;
            }
            \endcode */
        /*! \sa [AcDepots constructor](@ref AcUtils#AcDepots#AcDepots), DepotsCollection, AcGroups.initAsync */
        /*! \show_ <tt>show -fx depots</tt> */
        /*! \accunote_ The XML attribute \e locWidth from <tt>show -fx depots</tt> is obsolete. 
            It's an AccuRev 4.x artifact and should be removed. RPI defect 1112042, SupportLine AR3325. */
        public async Task<bool> initAsync(DepotsCollection depotsCol = null, IProgress<int> progress = null)
        {
            bool ret = false; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync("show -fx depots").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = null;
                    if (depotsCol == null)
                        query = from e in xml.Elements("Element") select e;
                    else
                        query = from e in xml.Elements("Element")
                                where depotsCol.OfType<DepotElement>().Any(de => de.Depot == (string)e.Attribute("Name"))
                                select e;

                    int num = query.Count();
                    List<Task<bool>> tasks = new List<Task<bool>>(num);
                    Func<Task<bool>, bool> cf = t =>
                    {
                        bool res = t.Result;
                        if (res && progress != null) progress.Report(Interlocked.Increment(ref _counter));
                        return res;
                    };

                    foreach (XElement e in query)
                    {
                        AcDepot depot = new AcDepot(_dynamicOnly, _includeHidden);
                        depot.ID = (int)e.Attribute("Number");
                        depot.Name = (string)e.Attribute("Name");
                        depot.Slice = (int)e.Attribute("Slice");
                        depot.ExclusiveLocking = (bool)e.Attribute("exclusiveLocking");
                        string temp = (string)e.Attribute("case");
                        depot.Case = (CaseSensitivity)Enum.Parse(typeof(CaseSensitivity), temp);
                        depot._streams = new AcStreams(_dynamicOnly, _includeHidden);
                        lock (_locker) { Add(depot); }
                        Task<bool> t = depot._streams.initAsync(depot, depot.listFile()).ContinueWith(cf);
                        tasks.Add(t);
                    }

                    bool[] arr = await Task.WhenAll(tasks).ConfigureAwait(false);
                    ret = (arr != null && arr.All(n => n == true)); // true if all succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcDepots.initAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcDepots.initAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }
        //@}
        #endregion

        #pragma warning disable 0642
        /// <summary>
        /// Get the list of depots \e user has permission to view based on their principal name and group membership 
        /// <a href="https://www.microfocus.com/documentation/accurev/72/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_setacl.html">access control list (ACL) entries</a>.
        /// </summary>
        /// <remarks>A plus sign (+) appended to the depot name denotes the Inheritable attribute.</remarks>
        /// <param name="user">User for which to query.</param>
        /// <returns>The list of depots as an ordered formatted comma-delimited string, otherwise \e null on failure.</returns>
        /*! \pre Assumes \e user's group membership list was initialized via [AcUsers](@ref AcUtils#AcUsers#AcUsers) \e includeGroupsList constructor parameter. */
        /*! \sa [AcPermissions](@ref AcUtils#AcPermissions#AcPermissions) */
        public async Task<string> canViewAsync(AcUser user)
        {
            // create permissions object as a singleton (thread safe)
            // see "C# Lazy Initialization && Race-to-initialize" http://stackoverflow.com/questions/11555755/c-sharp-lazy-initialization-race-to-initialize
            await LazyInitializer.EnsureInitialized(ref _depotPermObj, ref _permInit, ref _permSync,
                async () =>
                {
                    _permissions = new AcPermissions(PermKind.depot);
                    return await _permissions.initAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            if (_depotPermObj.Result == false)
                return null; // initialization failure, check log file

            // get the membership list for the user (includes explicit and implicit membership)
            SortedSet<string> members = user.Principal.Members;
            // list that will contain the depots this user has permission to view
            List<string> canView = new List<string>();

            foreach (AcDepot depot in this)
            {
                bool isInheritable = false;
                // Any() returns true if the collection contains one or more items that satisfy the condition defined by the predicate
                // first, is an 'all' or 'none' permission explicitly set for this user on this depot?
                if (_permissions.Any(p =>
                {
                    bool all = (p.Type == PermType.user && p.Rights == PermRights.all &&
                        p.AppliesTo == user.Principal.Name && p.Name == depot.Name);
                    if (all)
                        isInheritable = (p.Inheritable == true && p.Type == PermType.user && p.Rights == PermRights.all &&
                            p.AppliesTo == user.Principal.Name && p.Name == depot.Name);
                    return all;
                }))
                {
                    canView.Add($"{depot.Name}{(isInheritable ? "+" : String.Empty)}");
                }
                else if (_permissions.Any(p =>
                {
                    return (p.Type == PermType.user && p.Rights == PermRights.none &&
                        p.AppliesTo == user.Principal.Name && p.Name == depot.Name);
                }))
                {
                    ; // user does not have permission to this depot
                }
                else // check permissions based on user's group memberships
                {
                    bool all = false;
                    bool none = _permissions.Any(p =>
                    {
                        return (p.Name == depot.Name && p.Rights == PermRights.none &&
                            p.Type == PermType.group && members.Any(m => p.AppliesTo == m));
                    });

                    // by default everyone has access unless there's a "none" with no "all" override
                    if (none)
                    {
                        all = _permissions.Any(p =>
                        {
                            return (p.Name == depot.Name && p.Rights == PermRights.all &&
                                p.Type == PermType.group && members.Any(m => p.AppliesTo == m));
                        });
                    }

                    if (none && !all)
                        ; // user does not have permission to this depot
                    else // no permission(s) set or an 'all' that overrides one or more 'none' permissions
                    {
                        isInheritable = _permissions.Any(p =>
                        {
                            return (p.Inheritable == true && p.Name == depot.Name && 
                                p.Type == PermType.group && members.Any(m => p.AppliesTo == m));
                        });

                        canView.Add($"{depot.Name}{(isInheritable ? "+" : String.Empty)}");
                    }
                }
            }

            IEnumerable<string> e = canView.OrderBy(n => n);
            string depots = String.Join(", ", e);
            return depots;
        }

        /// <summary>
        /// Get the AcDepot object for depot \e name.
        /// </summary>
        /// <param name="name">Depot name to query.</param>
        /// <returns>AcDepot object for depot \e name, otherwise \e null if not found.</returns>
        public AcDepot getDepot(string name)
        {
            return this.SingleOrDefault(n => n.Name == name);
        }

        /// <summary>
        /// Get the AcDepot object for depot \e ID number.
        /// </summary>
        /// <param name="ID">Depot ID number to query.</param>
        /// <returns>AcDepot object for depot \e ID number, otherwise \e null if not found.</returns>
        public AcDepot getDepot(int ID)
        {
            return this.SingleOrDefault(n => n.ID == ID);
        }

        /// <summary>
        /// Get the AcDepot object for stream \e name.
        /// </summary>
        /// <param name="name">Stream name to query.</param>
        /// <returns>AcDepot object for stream \e name, otherwise \e null if not found.</returns>
        public AcDepot getDepotForStream(string name)
        {
            AcDepot depot = (from d in this
                             from s in d.Streams.Where(n => n.Name == name)
                             select d).SingleOrDefault();
            return depot;
        }

        /// <summary>
        /// Get the AcStream object for stream \e name.
        /// </summary>
        /// <param name="name">Stream name to query.</param>
        /// <returns>AcStream object for stream \e name, otherwise \e null if not found.</returns>
        public AcStream getStream(string name)
        {
            AcStream stream = (from d in this
                               from s in d.Streams.Where(n => n.Name == name)
                               select s).SingleOrDefault();
            return stream;
        }
    }
}
