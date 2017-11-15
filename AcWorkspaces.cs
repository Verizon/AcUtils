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
    ///@{
    /// <summary>
    /// Workspace type the user created. These numeric values are passed by AccuRev and converted to our enum type.
    /// </summary>
    public enum WsType {
        /*! \var Workspace
        1: A workspace. */
        Workspace = 1,
        /*! \var RefTree
        3: A reference tree. */
        RefTree = 3,
        /*! \var Exclusive
        9: Type of file locking is \e exclusive. */
        Exclusive = 9,
        /*! \var Anchor
        17: Type of file locking is <em>anchor required</em>. */
        Anchor = 17
    };

    /// <summary>
    /// End-of-line character used by the workspace. These numeric values are passed by AccuRev and converted to our enum type.
    /// </summary>
    public enum WsEOL {
        /*! \var Platform
        0: Recognize the host OS to determine the EOL. */
        Platform = 0,
        /*! \var Unix
        1: Always use Unix EOL. */
        Unix = 1,
        /*! \var Windows
        2: Always use Windows EOL. */
        Windows = 2
    };
    ///@}
    #endregion

    /// <summary>
    /// A workspace object that defines the attributes of an AccuRev workspace. 
    /// AcWorkspace objects are instantiated during AcWorkspaces construction.
    /// </summary>
    /// <remarks>An AcWorkspace object contains the (additional) attributes of 
    /// a workspace not found in its corresponding AcStream object when the stream 
    /// object is a workspace. Unlike AcStreams, AcWorkspace objects are not 
    /// instantiated during depot creation but done explicitly when needed.</remarks>
    [Serializable]
    [DebuggerDisplay("{Name} ({ID}), {Host}, {Location} ULevel-Target [{UpdateLevel}:{TargetLevel}]")]
    public sealed class AcWorkspace : IFormattable, IEquatable<AcWorkspace>, IComparable<AcWorkspace>, IComparable
    {
        #region class variables
        private string _name; // workspace name
        private int _id; // workspace ID number
        private string _location; // workspace location; the path on the machine where the workspace is located
        private string _storage; // location on the host (storage) where the elements physically reside
        private string _host; // machine name (host) where the elements physically reside
        private bool _hidden; // true if the workspace has been removed (hidden), false if it is active
        private AcDepot _depot; // depot the workspace is located in
        private int _targetLevel; // how up-to-date the workspace should be
        private int _updateLevel; // how up-to-date the workspace actually is
        private DateTime? _lastUpdate; // time the workspace was last updated
        private WsType _type; // 1 (standard workspace), 3 (reference tree), 9 (exclusive-file locking), 17 (anchor-required)
        private WsEOL _eol; // 0 (platform-appropriate), 1 (Unix/Linux style:NL), 2 (Windows style: CR-LF)
        private AcPrincipal _principal = new AcPrincipal(); // principal who owns the workspace
        #endregion

        /// <summary>
        /// Constructor used during AcWorkspaces list construction. It is called internally and not by user code. 
        /// </summary>
        internal AcWorkspace() { }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcWorkspace. 
        /// Uses the workspace ID number and depot to compare instances.
        /// </summary>
        /// <param name="other">The AcWorkspace object being compared to \e this instance.</param>
        /// <returns>\e true if AcWorkspace \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcWorkspace other)
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
        /// <returns>Return value of generic [Equals(AcWorkspace)](@ref AcWorkspace#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return Equals(other as AcWorkspace);
        }

        /// <summary>
        /// Override appropriate for type AcWorkspace.
        /// </summary>
        /// <returns>Hash of workspace ID number and depot.</returns>
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
        /// Generic IComparable implementation (default) for comparing AcWorkspace objects to sort by depot name and then workspace name.
        /// </summary>
        /// <param name="other">An AcWorkspace object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcWorkspace objects being compared.</returns>
        /*! \sa [AcWorkspaces constructor](@ref AcUtils#AcWorkspaces#AcWorkspaces) */
        public int CompareTo(AcWorkspace other)
        {
            int result;
            if (AcWorkspace.ReferenceEquals(this, other))
                result = 0;
            else
            {
                result = Depot.CompareTo(other.Depot);
                if (result == 0)
                    result = Name.CompareTo(other.Name);
            }

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcWorkspace object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcWorkspace)](@ref AcWorkspace#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcWorkspace object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcWorkspace))
                throw new ArgumentException("Argument is not an AcWorkspace", "other");
            AcWorkspace o = (AcWorkspace)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Name of the workspace, e.g. MARS_DEV3_barnyrd
        /// </summary>
        public string Name
        {
            get { return _name ?? String.Empty; }
            internal set { _name = value; }
        }

        /// <summary>
        /// \e True if the workspace has been removed (hidden), \e False if it is active.
        /// </summary>
        public bool Hidden
        {
            get { return _hidden; }
            internal set { _hidden = value; }
        }

        /// <summary>
        /// Here \e Loc is the workspace location; the path on the machine where the workspace is located. Corresponds 
        /// to the <tt>-l \<location\></tt> used with the \c mkws command. [Storage](@ref AcUtils#AcWorkspace#Storage) 
        /// is the location on the [Host](@ref AcUtils#AcWorkspace#Host) where the elements physically reside.
        /// </summary>
        /*! \code
            C:\Temp>net use W: \\machine_name_omitted\AcTriggers
            C:\Temp>accurev mkws -w winTest -b AcTools -l w:\backup
            C:\Temp>accurev show -fvx wspaces
            ...
            <Element
              Name="winTest_barnyrd"
              Loc="w:/backup"  <!-- subfolder of the AcTriggers share -->
              Storage="/AcTriggers/backup"
              Host="machine_name_omitted"
              Stream="19"
              depot="AcTools"
              Target_trans="0"
              Trans="0"
              fileModTime="0"
              Type="1"
              EOL="0"
              user_id="74"
              user_name="barnyrd"/>
            \endcode */
        public string Location
        {
            get { return _location ?? String.Empty; }
            internal set { _location = value; }
        }

        /// <summary>
        /// Location on the [Host](@ref AcUtils#AcWorkspace#Host) where the elements physically reside.
        /// </summary>
        /*! \sa AcWorkspace.Location */
        public string Storage
        {
            get { return _storage ?? String.Empty; }
            internal set { _storage = value; }
        }

        /// <summary>
        /// Machine name where the elements physically reside.
        /// </summary>
        /*! \sa AcWorkspace.Location */
        public string Host
        {
            get { return _host ?? String.Empty; }
            internal set { _host = value; }
        }

        /// <summary>
        /// Workspace ID number. The same number as [AcStream.ID](@ref AcStream#ID) when the stream object is a workspace.
        /// </summary>
        public int ID
        {
            get { return _id; }
            internal set { _id = value; }
        }

        /// <summary>
        /// Depot the workspace is located in.
        /// </summary>
        public AcDepot Depot
        {
            get { return _depot; }
            internal set { _depot = value; }
        }

        /// <summary>
        /// Current target level of the workspace. Also known as the <em>target transaction level</em> 
        /// or <em>target x-action</em>.
        /// </summary>
        /// <remarks>
        /// This value indicates how up-to-date the workspace should be; the transaction number of 
        /// the depot's most recent transaction at the time of the last \c update operation.
        /// </remarks>
        public int TargetLevel
        {
            get { return _targetLevel; }
            internal set { _targetLevel = value; }
        }

        /// <summary>
        /// Current update level of the workspace. Also known as the <em>workspace transaction level</em> 
        /// or <em>x-action</em>.
        /// </summary>
        /// <remarks>
        /// This value indicates how up-to-date the workspace actually is. Normally, it is the transaction 
        /// number of the depot's most recent transaction at the time of the last \c update operation, known 
        /// as the [target level](@ref AcWorkspace#TargetLevel). However, if \c update failed this number will 
        /// remain the same and not be updated to the same as the [target level](@ref AcWorkspace#TargetLevel).
        /// </remarks>
        public int UpdateLevel
        {
            get { return _updateLevel; }
            internal set { _updateLevel = value; }
        }

        /// <summary>
        /// Time the workspace was last updated.
        /// </summary>
        public DateTime? LastUpdate
        {
            get { return _lastUpdate; }
            internal set { _lastUpdate = value; }
        }

        /// <summary>
        /// Type of workspace: standard, exclusive-file locking, anchor-required, or reference tree.
        /// </summary>
        public WsType Type
        {
            get { return _type; }
            internal set { _type = value; }
        }

        /// <summary>
        /// End-of-line character in use by the workspace: platform-appropriate, Unix/Linux style, or Windows style.
        /// </summary>
        public WsEOL EOL
        {
            get { return _eol; }
            internal set { _eol = value; }
        }

        /// <summary>
        /// AccuRev principal who owns the workspace.
        /// </summary>
        public AcPrincipal Principal
        {
            get { return _principal; }
            internal set { _principal = value; }
        }

        /// <summary>
        /// Get the basis (parent) stream for this workspace.
        /// </summary>
        /// <returns>Basis AcStream object for this workspace or \e null if not found.</returns>
        public AcStream getBasis()
        {
            AcStream basis = _depot.getBasis(_id);
            return basis;
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(workspace.ToString("L"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Name of the workspace, e.g. MARS_DEV3_barnyrd. Default when not using a format specifier.
        /// \arg \c LV Long version (verbose).
        /// \arg \c I Workspace ID number.
        /// \arg \c L Workspace [location](@ref AcUtils#AcWorkspace#Location).
        /// \arg \c S Location on the host ([storage](@ref AcUtils#AcWorkspace#Storage)) where the elements physically reside.
        /// \arg \c M Machine name ([host](@ref AcUtils#AcWorkspace#Host)) where the elements physically reside.
        /// \arg \c H \e True if the workspace is hidden, \e False otherwise.
        /// \arg \c D Depot name.
        /// \arg \c TL [Target level](@ref AcUtils#AcWorkspace#TargetLevel): how up-to-date the workspace should be.
        /// \arg \c UL [Update level](@ref AcUtils#AcWorkspace#UpdateLevel): how up-to-date the workspace actually is.
        /// \arg \c U Time the workspace was last updated.
        /// \arg \c T [Workspace type](@ref AcUtils#WsType): standard, exclusive-file locking, anchor-required, or reference tree.
        /// \arg \c E [End-of-line character](@ref AcUtils#WsEOL) in use by the workspace: platform-appropriate, Unix/Linux style, or Windows style.
        /// \arg \c PI Workspace owner's principal ID number.
        /// \arg \c PN Workspace owner's principal name.
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
                case "G": // name of the workspace, e.g. MARS_DEV3_barnyrd
                    return Name; // general format should be short since it can be called by anything
                case "LV": // long version (verbose)
                {
                    string text = String.Format(@"{1} ({2}) {{{3}}}, Updated {4}{0}Location: ""{5}"", Storage: ""{6}""{0}Host: {7}, ULevel-Target [{8}:{9}]{10}{0}Depot: {11}, EOL: {12}, Hidden: {13}{0}",
                        Environment.NewLine, Name, ID, Type, LastUpdate, Location, Storage, Host, UpdateLevel, TargetLevel, (TargetLevel != UpdateLevel) ? " (incomplete)" : "", Depot, EOL, Hidden);
                    return text;
                }
                case "I": // workspace ID number
                    return ID.ToString();
                case "L": // workspace location
                    return Location;
                case "S": // location on the host (storage) where the elements physically reside
                    return Storage;
                case "M": // machine name (host) where the elements physically reside
                    return Host;
                case "H": // True if workspace is hidden, False otherwise
                    return Hidden.ToString();
                case "D": // depot name
                    return Depot.ToString();
                case "TL": // how up-to-date the workspace should be
                    return TargetLevel.ToString();
                case "UL": // how up-to-date the workspace actually is
                    return UpdateLevel.ToString();
                case "U": // time the workspace was last updated
                    return LastUpdate.ToString();
                case "T": // type of workspace: standard, exclusive-file locking, anchor-required, or reference tree
                    return Type.ToString();
                case "E": // end-of-line character in use by the workspace: platform-appropriate, Unix/Linux style, or Windows style
                    return EOL.ToString();
                case "PI": // ID number of the AccuRev principal who owns the workspace
                    return Principal.ID.ToString();
                case "PN": // principal name of the workspace owner
                    return Principal.Name;
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
    /// A container of AcWorkspace objects that define AccuRev workspaces in the repository.
    /// </summary>
    [Serializable]
    public sealed class AcWorkspaces : List<AcWorkspace>
    {
        #region class variables
        private AcDepots _depots;
        private bool _allWSpaces;
        private bool _includeHidden;
        private bool _includeRefTrees;
        [NonSerialized] private readonly object _locker = new object();
        #endregion

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container of AcWorkspace objects that define AccuRev workspaces in the repository.
        /// </summary>
        /// <param name="depots">AcDepots creation defaults to the list of depots the script user has access rights to view based on their ACL permissions.</param>
        /// <param name="allWSpaces">\e true to include all workspaces, not just those that belong to the principal.</param>
        /// <param name="includeHidden">\e true to include deactivated (removed) workspaces/reference trees.</param>
        /// <param name="includeRefTrees">\e true to include reference trees.</param>
        /*! \code
            public static async Task<bool> showWorkspacesAsync()
            {
                var progress = new Progress<int>(n =>
                {
                    if ((n % 10) == 0) Console.WriteLine("Initializing: " + n);
                });

                // depots list is required for workspace list construction
                // defaults to those the script user can view based on ACL permissions
                AcDepots depots = new AcDepots();
                if (!(await depots.initAsync(null, progress))) return false;

                // false to include only the script user's workspaces (not all)
                // true to include their deactivated (hidden) workspaces
                AcWorkspaces wspaces = new AcWorkspaces(depots, allWSpaces: false, includeHidden: true);
                if (!(await wspaces.initAsync())) return false;

                foreach (AcWorkspace wspace in wspaces.OrderBy(n => n.Hidden).ThenBy(n => n.Name))
                    Console.WriteLine(wspace.ToString("lv") + Environment.NewLine);

                return true;
            }
            \endcode */
        /*! \sa initAsync, [default comparer](@ref AcWorkspace#CompareTo) */
        /*! \pre \e depots cannot be initialized in parallel with AcWorkspaces initialization; it must be a fully initialized list. */
        public AcWorkspaces(AcDepots depots, bool allWSpaces, bool includeHidden, bool includeRefTrees = false)
        {
            _depots = depots;
            _allWSpaces = allWSpaces;
            _includeHidden = includeHidden;
            _includeRefTrees = includeRefTrees;
        }

        /// <summary>
        /// Populate this container with AcWorkspace objects as per constructor parameters.
        /// </summary>
        /// <remarks>The resultant list includes only workspaces from depots the user has [permission to access](@ref AcUtils#AcPermissions#AcPermissions).</remarks>
        /// <param name="depot">Specify a depot to limit the query to workspaces in \e depot only, not all depots in the repository.</param>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /*! \sa [AcWorkspaces constructor](@ref AcUtils#AcWorkspaces#AcWorkspaces) */
        public async Task<bool> initAsync(AcDepot depot = null)
        {
            Task<AcResult> ws = getWorkspacesXMLAsync();
            if (_includeRefTrees)
            {
                // run both in parallel
                Task<AcResult> rt = getReferenceTreesXMLAsync();
                AcResult[] arr = await Task.WhenAll(ws, rt).ConfigureAwait(false);
                bool ret = (arr != null && arr.All(n => n != null && n.RetVal == 0)); // true if both were successful
                if (!ret)
                    return false;
                foreach (AcResult r in arr)
                {
                    if (!storeWSpaces(r, depot))
                        return false;
                }
            }
            else
            {
                AcResult r = await ws.ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    if (!storeWSpaces(r, depot))
                        return false;
                }
                else
                    return false;
            }

            return true;
        }
        //@}
        #endregion

        /// <summary>
        /// Get the list of workspaces in XML for all or the current user and optionally include 
        /// inactive workspaces as per [AcWorkspaces constructor](@ref AcUtils#AcWorkspaces#AcWorkspaces) \e includeHidden parameter.
        /// </summary>
        /// <returns>AcResult initialized with the \c show command results, otherwise \e null on error.</returns>
        /*! \show_ <tt>show \<-fvx | -fvix\> \<-a | \> wspaces</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on <tt>show wspaces</tt> command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa AcWorkspaces.getReferenceTreesXMLAsync */
        private async Task<AcResult> getWorkspacesXMLAsync()
        {
            AcResult result = null;
            try
            {
                string cmd = String.Empty;
                // for all below, -v option adds the Loc (location) to output
                if (_allWSpaces && _includeHidden)
                    // All workspaces, not just those that belong to the principal. Include deactivated workspaces.
                    cmd = "show -fvix -a wspaces"; // -a is the only option available for show wspaces
                else if (_allWSpaces && !_includeHidden)
                    // All workspaces, not just those that belong to the principal. Do not include deactivated workspaces.
                    cmd = "show -fvx -a wspaces";
                else if (!_allWSpaces && _includeHidden)
                    // Only those workspaces that belong to the principal. Include deactivated workspaces.
                    cmd = "show -fvix wspaces";
                else if (!_allWSpaces && !_includeHidden)
                    // Only those workspaces that belong to the principal. Do not include deactivated workspaces.
                    cmd = "show -fvx wspaces";
                result = await AcCommand.runAsync(cmd).ConfigureAwait(false);
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcWorkspaces.getWorkspacesXMLAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcWorkspaces.getWorkspacesXMLAsync{Environment.NewLine}{ecx.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the list of reference trees in XML and optionally include those that are inactive 
        /// as per [AcWorkspaces constructor](@ref AcUtils#AcWorkspaces#AcWorkspaces) \e includeHidden parameter.
        /// </summary>
        /// <returns>AcResult initialized with the \c show command results, or \e null on error.</returns>
        /*! \show_ <tt>show \<-fvx | -fvix\> refs</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on <tt>show refs</tt> command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa AcWorkspaces.getWorkspacesXMLAsync */
        private async Task<AcResult> getReferenceTreesXMLAsync()
        {
            AcResult result = null;
            try
            {
                // -fvix: display all reference trees including deactivated ones
                // -fvx: display only active reference trees
                result = await AcCommand.runAsync($"show {(_includeHidden ? "-fvix" : "-fvx")} refs").ConfigureAwait(false);
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcWorkspaces.getReferenceTreesXMLAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcWorkspaces.getReferenceTreesXMLAsync{Environment.NewLine}{ecx.Message}");
            }

            return result;
        }

        /// <summary>
        /// Convert AcResult.CmdResult XML from \e result into AcWorkspace objects and add them to our container. 
        /// Filters out workspace/ref trees that are not in the user's default list of depots they can view based on ACL permissions.
        /// </summary>
        /// <param name="result">Previously initialized AcResult object from AcWorkspaces.getWorkspacesXMLAsync and AcWorkspaces.getReferenceTreesXMLAsync.</param>
        /// <param name="depot">Specify a depot to limit the query to workspaces and ref trees in \e depot only, not the users default list of depots.</param>
        /// <returns>\e true if object initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        private bool storeWSpaces(AcResult result, AcDepot depot = null)
        {
            bool ret = false; // assume failure
            try
            {
                // filter out workspace/ref trees not in the user's default list
                XElement xml = XElement.Parse(result.CmdResult);
                IEnumerable<XElement> filter;
                if (depot != null)
                    filter = from w in xml.Descendants("Element")
                             join AcDepot d in _depots on
                             (string)w.Attribute("depot") equals d.ToString()
                             where String.Equals((string)w.Attribute("depot"), depot.Name)
                             select w;
                else
                    filter = from w in xml.Descendants("Element")
                             join AcDepot d in _depots on
                             (string)w.Attribute("depot") equals d.ToString()
                             select w;

                foreach (XElement e in filter)
                {
                    AcWorkspace ws = new AcWorkspace();
                    ws.Name = (string)e.Attribute("Name");
                    ws.Hidden = (e.Attribute("hidden") != null); // attribute hidden exists only when true
                    ws.Location = (string)e.Attribute("Loc");
                    ws.Storage = (string)e.Attribute("Storage");
                    ws.Host = (string)e.Attribute("Host");
                    ws.ID = (int)e.Attribute("Stream");
                    string temp = (string)e.Attribute("depot");
                    ws.Depot = _depots.getDepot(temp);
                    ws.TargetLevel = (int)e.Attribute("Target_trans");
                    ws.UpdateLevel = (int)e.Attribute("Trans");
                    string filemod = (string)e.Attribute("fileModTime");
                    ws.LastUpdate = AcDateTime.AcDate2DateTime(filemod);
                    int type = (int)e.Attribute("Type");
                    ws.Type = (WsType)type;
                    int eol = (int)e.Attribute("EOL");
                    ws.EOL = (WsEOL)eol;
                    ws.Principal.ID = (int)e.Attribute("user_id");
                    ws.Principal.Name = (string)e.Attribute("user_name");
                    lock (_locker) { Add(ws); }
                }

                ret = true; // operation succeeded
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcWorkspaces.storeWSpaces{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        /// <summary>
        /// Get the AcDepot object that workspace \e name is located in.
        /// </summary>
        /// <param name="name">Workspace name to query.</param>
        /// <returns>AcDepot object this workspace is located in, otherwise \e null if not found.</returns>
        public AcDepot getDepot(string name)
        {
            AcDepot depot = null;
            foreach (AcWorkspace w in AsReadOnly())
            {
                if (String.Equals(w.Name, name))
                {
                    depot = w.Depot;
                    break;
                }
            }

            return depot;
        }

        /// <summary>
        /// Get the AcWorkspace object in \e depot with workspace \e ID number.
        /// </summary>
        /// <param name="depot">Depot where the workspace resides.</param>
        /// <param name="ID">Workspace ID number to query.</param>
        /// <returns>AcWorkspace object otherwise \e null if not found.</returns>
        public AcWorkspace getWorkspace(AcDepot depot, int ID)
        {
            return this.SingleOrDefault(n => n.ID == ID && n.Depot.Equals(depot));
        }

        /// <summary>
        /// Get the AcWorkspace object for workspace \e name.
        /// </summary>
        /// <param name="name">Workspace name to query.</param>
        /// <returns>AcWorkspace object otherwise \e null if not found.</returns>
        public AcWorkspace getWorkspace(string name)
        {
            return this.SingleOrDefault(n => String.Equals(n.Name, name));
        }
    }
}
