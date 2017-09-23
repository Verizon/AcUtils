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
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    /// <summary>
    /// A user's AccuRev [principal attributes](@ref AcUtils#AcPrincipal) \e name, \e ID, and \e status (active or inactive). 
    /// In addition, the user's AccuRev [group membership](@ref AcUtils#AcUser#initGroupsListAsync) list along with regular and 
    /// other <a href="class_ac_utils_1_1_ac_user.html#properties">user properties</a> from Active Directory can be included 
    /// (both optional) during [list construction](@ref AcUtils#AcUsers#AcUsers).
    /// </summary>
    /*! \attention User properties require that AccuRev principal names match login names stored on the LDAP server. */
    [Serializable]
    [TypeDescriptionProvider(typeof(PrncplDescriptionProvider))]
    public sealed class AcUser : IFormattable, IEquatable<AcUser>, IComparable<AcUser>, IComparable
    {
        #region class variables
        private AcPrincipal _principal = new AcPrincipal();
        private string _givenName; // John
        private string _middleName;
        private string _surname; // Doe
        private string _displayName; // Doe, John
        private string _business; // business phone number
        private string _emailAddress; // John.Doe@VerizonWireless.com
        private string _description;
        private string _distinguishedName;
        // [title,value] pairs for user properties outside the default set
        private Dictionary<string, object> _other = new Dictionary<string, object>();
        #endregion

        /// <summary>
        /// Initialize user's AccuRev [principal attributes](@ref AcUtils#AcPrincipal) \e name, \e ID, and \e status (active or inactive). 
        /// AcUser objects are instantiated during AcUsers list construction. This constructor is called internally and not by user code.
        /// </summary>
        /// <param name="id">User's AccuRev principal ID number.</param>
        /// <param name="name">User's AccuRev principal name.</param>
        /// <param name="status">Whether the user is active or inactive in AccuRev.</param>
        /*! \sa [AcUsers.initAsync](@ref AcUtils#AcUsers#initAsync), [AcUser.initFromADAsync](@ref AcUtils#AcUser#initFromADAsync), 
             [AcUser.initGroupsListAsync](@ref AcUtils#AcUser#initGroupsListAsync) */
        internal AcUser(int id, string name, PrinStatus status)
        {
            _principal.ID = id;
            _principal.Name = name;
            _principal.Status = status;
        }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcUser. 
        /// Uses the user's principal ID to compare instances as this number is immutable in AccuRev; 
        /// a user's principal attributes can change except their ID.
        /// </summary>
        /// <param name="other">The AcUser object being compared to \e this instance.</param>
        /// <returns>\e true if AcUser \e rhs is the same, \e false otherwise.</returns>
        public bool Equals(AcUser other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Principal.ID == other.Principal.ID;
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcUser)](@ref AcUser#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as AcUser);
        }

        /// <summary>
        /// Override appropriate for type AcUser.
        /// </summary>
        /// <returns>AccuRev principal ID number since it's immutable and unique across both users and groups.</returns>
        public override int GetHashCode()
        {
            return Principal.ID;
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcUser objects to sort by [DisplayName](@ref AcUtils#AcUser#DisplayName) 
        /// from Active Directory if available, otherwise their AccuRev principal name.
        /// </summary>
        /// <param name="other">An AcUser object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcUser objects being compared.</returns>
        /*! \sa [AcUsers constructor example](@ref AcUtils#AcUsers#AcUsers) */
        public int CompareTo(AcUser other)
        {
            int result;
            if (AcUser.ReferenceEquals(this, other))
                result = 0;
            else
            {
                if (!String.IsNullOrEmpty(DisplayName) && !String.IsNullOrEmpty(other.DisplayName))
                    result = String.Compare(DisplayName, other.DisplayName);
                else
                    result = String.Compare(Principal.Name, other.Principal.Name);
            }

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcUser object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcUser)](@ref AcUser#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcUser object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcUser))
                throw new ArgumentException("Argument is not an AcUser", "other");
            AcUser o = (AcUser)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// AccuRev principal attributes \e name, \e ID, and \e status (active or inactive), 
        /// and optionally their [group membership list](@ref AcUtils#AcUser#initGroupsListAsync) as per 
        /// [AcUsers constructor](@ref AcUtils#AcUsers#AcUsers) \e includeGroupsList parameter.
        /// </summary>
        public AcPrincipal Principal
        {
            get { return _principal; }
        }

        /*! \name User properties default set (Active Directory) */
        /**@{*/
        /// <summary>
        /// User's given name from Active Directory, e.g. John
        /// </summary>
        public string GivenName
        {
            get { return _givenName ?? String.Empty; }
        }

        /// <summary>
        /// User's middle name from Active Directory.
        /// </summary>
        public string MiddleName
        {
            get { return _middleName ?? String.Empty; }
        }

        /// <summary>
        /// User's surname from Active Directory, e.g. Doe
        /// </summary>
        public string Surname
        {
            get { return _surname ?? String.Empty; }
        }

        /// <summary>
        /// User's display name from Active Directory, e.g. Doe, John
        /// </summary>
        public string DisplayName
        {
            get { return _displayName ?? String.Empty; }
        }

        /// <summary>
        /// User's business phone number from Active Directory.
        /// </summary>
        public string Business
        {
            get { return _business ?? String.Empty; }
        }

        /// <summary>
        /// User's email address from Active Directory.
        /// </summary>
        public string EmailAddress
        {
            get { return _emailAddress ?? String.Empty; }
        }

        /// <summary>
        /// User's description from Active Directory.
        /// </summary>
        public string Description
        {
            get { return _description ?? String.Empty; }
        }

        /// <summary>
        /// User's distinguished name from Active Directory.
        /// </summary>
        public string DistinguishedName
        {
            get { return _distinguishedName ?? String.Empty; }
        }
        /**@}*/

        /// <summary>
        /// Additional Active Directory user properties not in the regular 
        /// <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a>.
        /// </summary>
        /// <remarks>
        /// Optionally initialized during [list construction](@ref AcUtils#AcUsers#AcUsers) for 
        /// user properties beyond the <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a>. 
        /// Elements are [\e title,\e value] pairs: \e title from the [properties section](@ref AcUtils#PropCollection) 
        /// in <tt>\<prog_name\>.exe.config</tt> and its value from Active Directory.
        /// </remarks>
        /*! \code
            <activeDir>
              <domains>
                <add host="xyzdc.mycorp.com" path="DC=XYZ,DC=xy,DC=zcorp,DC=com"/>
                <add host="abcdc.mycorp.com" path="DC=ABC,DC=ab,DC=com"/>
              </domains>
              <properties>
                <add field="mobile" title="Mobile"/>
                <add field="manager" title="Manager"/>
                <add field="department" title="Department"/>
              </properties>
            </activeDir>
            ...
            row["GivenName"] = user.GivenName;
            row["Surname"] = user.Surname;
            row["Mobile"] = user.Other.ContainsKey("Mobile") ? user.Other["Mobile"] : String.Empty;
            row["EmailAddress"] = user.EmailAddress;
            row["Manager"] = user.Other.ContainsKey("Manager") ? getManager((string)user.Other["Manager"]) : String.Empty;
            row["Department"] = user.Other.ContainsKey("Department") ? user.Other["Department"] : String.Empty;
            row["Description"] = user.Description;
            ...
           \endcode */
        /*! \sa PropCollection */
        public IDictionary<string, object> Other
        {
            get { return _other; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(user.ToString("e"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G User's display name from Active Directory if available (e.g. Doe, John), otherwise their AccuRev principal name. The default when no format specifier is used.
        /// \arg \c LV Long version (verbose).
        /// \arg \c I User's AccuRev principal ID number.
        /// \arg \c N User's AccuRev principal name.
        /// \arg \c S Principal's status in AccuRev: \e Active or \e Inactive.
        /// \arg \c F User's given name from Active Directory.
        /// \arg \c M User's middle name from Active Directory.
        /// \arg \c L User's surname from Active Directory.
        /// \arg \c DN User's display name from Active Directory.
        /// \arg \c B User's business phone number from Active Directory.
        /// \arg \c E User's email address from Active Directory.
        /// \arg \c D User's description from Active Directory.
        /// \arg \c DG User's distinguished name from Active Directory.
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
                case "G": // user's display name from Active Directory if available, otherwise their AccuRev principal name
                    return _displayName ?? Principal.Name;
                case "LV": // long version (verbose)
                {
                    string text = String.Format("{0} ({1}), Business: {2}, {3}",
                        DisplayName, Principal.Name,
                        String.IsNullOrEmpty(Business) ? "N/A" : Business, 
                        EmailAddress);
                    return text;
                }
                case "I": // user's AccuRev principal ID
                    return Principal.ID.ToString();
                case "N": // user's AccuRev principal name
                    return Principal.Name;
                case "S": // principal's status in AccuRev: Active or Inactive
                    return Principal.Status.ToString();
                case "F": // user's given name from Active Directory
                    return GivenName;
                case "M": // user's middle name from Active Directory
                    return MiddleName;
                case "L": // user's surname from Active Directory
                    return Surname;
                case "DN": // user's display name from Active Directory
                    return DisplayName;
                case "B": // business phone number from Active Directory
                    return Business;
                case "E": // user's email address from Active Directory
                    return EmailAddress;
                case "D": // description from Active Directory
                    return Description;
                case "DG": // distinguished name from Active Directory
                    return DistinguishedName;
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

        /// <summary>
        /// Optionally called during [list construction](@ref AcUtils#AcUsers#AcUsers) to initialize the 
        /// <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a> of regular user properties 
        /// and others from Active Directory. This method is called internally and not by user code.
        /// </summary>
        /// <param name="dc">List of DomainElement objects as defined in <tt>\<prog_name\>.exe.config</tt> \e domains section. Used to initialize 
        /// the user properties <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a> from Active Directory.</param>
        /// <param name="pc">List of PropElement objects as defined in <tt>\<prog_name\>.exe.config</tt> \e properties section. Used to add 
        /// and initialize user properties from Active Directory that are outside the regular <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a>.</param>
        /// <returns>\e true if no exception was thrown and operation succeeded, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        /*! \code
          <activeDir>
            <domains>
              <add host="xyzdc.mycorp.com" path="DC=XYZ,DC=xy,DC=zcorp,DC=com"/>
              <add host="abcdc.mycorp.com" path="DC=ABC,DC=ab,DC=com"/>
            </domains>
            <properties>
              <add field="mobile" title="Mobile" />
              <add field="manager" title="Manager" />
              <add field="department" title="Department" />
                ...
            </properties >
          </activeDir>
          \endcode */
        /*! \sa DomainCollection, PropCollection */
        /*! \attention User properties require that AccuRev principal names match login names stored on the LDAP server. */
        internal async Task<bool> initFromADAsync(DomainCollection dc, PropCollection pc = null)
        {
            return await Task.Run(() =>
            {
                bool ret = true; // assume success
                foreach (DomainElement de in dc)
                {
                    PrincipalContext ad = null;
                    try
                    {
                        ad = new PrincipalContext(ContextType.Domain, de.Host.Trim(), de.Path.Trim());
                        UserPrincipal up = new UserPrincipal(ad);
                        up.SamAccountName = Principal.Name;
                        using (PrincipalSearcher ps = new PrincipalSearcher(up))
                        {
                            UserPrincipal rs = (UserPrincipal)ps.FindOne();
                            if (rs != null)
                            {
                                _givenName = rs.GivenName;
                                _middleName = rs.MiddleName;
                                _surname = rs.Surname;
                                _displayName = rs.DisplayName;
                                _business = rs.VoiceTelephoneNumber;
                                _emailAddress = rs.EmailAddress;
                                _description = rs.Description;
                                _distinguishedName = rs.DistinguishedName;
                                
                                if (pc != null)
                                {
                                    DirectoryEntry lowerLdap = (DirectoryEntry)rs.GetUnderlyingObject();
                                    foreach (PropElement pe in pc)
                                    {
                                        PropertyValueCollection pvc = lowerLdap.Properties[pe.Field];
                                        if (pvc != null)
                                            _other.Add(pe.Title.Trim(), pvc.Value);
                                    }
                                }

                                break;
                            }
                        }
                    }

                    catch (Exception ecx)
                    {
                        ret = false;
                        string msg = String.Format("Exception caught and logged in AcUser.initFromADAsync{0}{1}", Environment.NewLine, ecx.Message);
                        AcDebug.Log(msg);
                    }

                    // avoid CA2202: Do not dispose objects multiple times
                    finally { if (ad != null) ad.Dispose(); }
                }

                return ret;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Optionally called during [list construction](@ref AcUtils#AcUsers#AcUsers) to initialize the list of groups 
        /// this user is a member of by way of direct or indirect (implicit) membership, e.g. Mary is implicitly a member 
        /// of groupA because she's a member of groupB which is a member of groupA. This method is called internally 
        /// and not by user code.
        /// </summary>
        /// <remarks>Membership lists for inactive users are initialized too.</remarks>
        /// <returns>\e true if no exception was thrown and operation succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcGroups.initMembersListAsync](@ref AcUtils#AcGroups#initMembersListAsync) */
        /*! \show_ <tt>show -fx -u \<user\> groups</tt> */
        /*! \accunote_ Unlike the \c show command used here, its <tt>show -fx -g \<group\> members</tt> counterpart 
             does not include memberships resulting from indirect (implicit) membership. */
        internal async Task<bool> initGroupsListAsync()
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = String.Format(@"show -fx -u ""{0}"" groups", Principal.Name); // works for inactive users too
                AcResult r = await AcCommand.runAsync(cmd).ConfigureAwait(false);
                if (r != null && r.RetVal == 0) // if command succeeded
                {
                    SortedSet<string> members = new SortedSet<string>();
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("Element") select element;
                    foreach (XElement e in query)
                    {
                        string name = (string)e.Attribute("Name");
                        members.Add(name);
                    }

                    Principal.Members = members;
                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcUser.initGroupsListAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcUser.initGroupsListAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        /// <summary>
        /// Get the [list of groups](@ref AcUtils#AcUser#initGroupsListAsync) this user is a member of as a formatted string. 
        /// List optionally initialized by [AcUsers constructor](@ref AcUtils#AcUsers#AcUsers) when \e includeGroupsList param is \e true.
        /// </summary>
        /// <returns>User's group membership list as an ordered formatted comma-delimited string, otherwise \e null if not found.</returns>
        /*! \sa [AcGroups.isMember](@ref AcUtils#AcGroups#isMember), [AcGroups.getMembers](@ref AcUtils#AcGroups#getMembers) */
        public string getGroups()
        {
            string list = null;
            if (Principal.Members != null)
            {
                IEnumerable<string> e = Principal.Members.OrderBy(n => n);
                list = String.Join(", ", e);
            }

            return list;
        }
    }

    /// <summary>
    /// A container of AcUser objects that define AccuRev users.
    /// </summary>
    /// <remarks>
    /// Elements contain user [principal attributes](@ref AcUtils#AcPrincipal) \e name, \e ID, and \e status (active or inactive). 
    /// In addition, the user's AccuRev group membership list, regular and other 
    /// <a href="class_ac_utils_1_1_ac_user.html#properties">user properties</a> from Active Directory, and deactivated (removed) 
    /// users can be included as well.
    /// </remarks>
    /*! \attention User properties require that AccuRev principal names match login names stored on the LDAP server. */
    [Serializable]
    public sealed class AcUsers : List<AcUser>
    {
        #region class variables
        private DomainCollection _dc;
        private PropCollection _pc; // user properties from Active Directory beyond the default set
        private bool _includeGroupsList;
        private bool _includeDeactivated;
        [NonSerialized] private readonly object _locker = new object();
        #endregion

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container of AcUser objects that define AccuRev users. Elements contain user 
        /// [principal attributes](@ref AcUtils#AcPrincipal) \e name, \e ID, and \e status (active or inactive). 
        /// Optionally, the user's AccuRev group membership list, regular and other 
        /// <a href="class_ac_utils_1_1_ac_user.html#properties">user properties</a> from Active Directory, 
        /// and deactivated (removed) users can be included as well.
        /// </summary>
        /// <remarks>
        /// Including group membership initialization (<em>includeGroupsList = true</em>) is slower, so we give the option to exclude it when 
        /// the list isn't needed. A user's group membership list includes groups from implicit membership, i.e. if user is a member of groupA 
        /// and groupA is a member of groupB, the user is implicitly a member of groupB.
        /// </remarks>
        /// <param name="dc">List of DomainElement objects as defined in <tt>\<prog_name\>.exe.config</tt> \e domains section. Used to initialize 
        /// the user properties <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a> from Active Directory.</param>
        /// <param name="pc">List of PropElement objects as defined in <tt>\<prog_name\>.exe.config</tt> \e properties section. Used to add 
        /// and initialize user properties from Active Directory that are outside the regular <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a>. 
        /// Note: requires a valid DomainCollection \e dc parameter.
        /// </param>
        /// <param name="includeGroupsList">\e true to include group membership initialization for each user (slower), \e false for no initialization (faster).</param>
        /// <param name="includeDeactivated">\e true to include deactivated (removed) users, otherwise \e false.</param>
        /*! \code
            ADSection adSection = ConfigurationManager.GetSection("activeDir") as ADSection;
            _domains = adSection.Domains;
            _pc = adSection.Props;
            ...
            genToolStripStatusLabel.Text = "Loading users...";
            genToolStripProgressBar.Maximum = await AcQuery.getUsersCountAsync();
            var progress = new Progress<int>(i => genToolStripProgressBar.Value = i);
            AcUsers users = new AcUsers(_domains, _pc, true); // true to include group membership initialization (slower)
            if (await users.initAsync(progress)) // if successful
            {
                foreach (AcUser user in users.OrderBy(n => n)) // use default comparer
                ...
            \endcode */
        /*! \attention User properties require that AccuRev principal names match login names stored on the LDAP server. */
        /*! \sa initAsync, [AcUser.initGroupsListAsync](@ref AcUtils#AcUser#initGroupsListAsync), [AcUser.initFromADAsync](@ref AcUtils#AcUser#initFromADAsync), 
            [default comparer](@ref AcUtils#AcUser#CompareTo), [AcGroups constructor](@ref AcUtils#AcGroups#AcGroups) */
        public AcUsers(DomainCollection dc = null, PropCollection pc = null, bool includeGroupsList = false, bool includeDeactivated = false)
        {
            _dc = dc;
            _pc = pc;
            _includeGroupsList = includeGroupsList;
            _includeDeactivated = includeDeactivated;
        }

        /// <summary>
        /// Populate this container with AcUser objects as per [constructor parameters](@ref AcUtils#AcUsers#AcUsers).
        /// </summary>
        /// <param name="progress">Optionally report progress back to the caller.</param>
        /// <returns>\e true if list initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \show_ <tt>show \<-fx | -fix\> users</tt> */
        public async Task<bool> initAsync(IProgress<int> progress = null)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = _includeDeactivated ? "show -fix users" : "show -fx users";
                AcResult r = await AcCommand.runAsync(cmd);
                if (r != null && r.RetVal == 0) // if command succeeded
                {
                    List<Task<bool>>[] tasks = new List<Task<bool>>[2];
                    tasks[0] = new List<Task<bool>>(); // active directory user properties
                    tasks[1] = new List<Task<bool>>(); // group membership lists
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("Element") select element;
                    foreach (XElement e in query)
                    {
                        string name = (string)e.Attribute("Name");
                        int id = (int)e.Attribute("Number");
                        // XML attribute isActive exists only if the user is inactive, otherwise it isn't there
                        PrinStatus status = (e.Attribute("isActive") == null) ? PrinStatus.Active : PrinStatus.Inactive;
                        AcUser user = new AcUser(id, name, status);
                        lock (_locker) { Add(user); }

                        if (_dc != null)
                            // initialize default set and other user properties from Active Directory
                            tasks[0].Add(user.initFromADAsync(_dc, _pc));
                        if (_includeGroupsList)
                            // include group membership list initialization
                            tasks[1].Add(user.initGroupsListAsync());
                    }

                    int counter = 0; // used to report progress back to the user during list construction
                    if (_dc != null && tasks[0].Count > 0)
                    {
                        while (tasks[0].Count > 0)
                        {
                            // run all user's LDAP initialization in parallel and report sequentially as they complete
                            Task<bool> n1 = await Task.WhenAny(tasks[0]).ConfigureAwait(false);
                            if (!(await n1.ConfigureAwait(false))) return false; //  // a failure occurred, see log file
                            tasks[0].Remove(n1); // remove completed task from the list
                            if (progress != null)
                                progress.Report(++counter);
                        }
                    }

                    if (_includeGroupsList && tasks[1].Count > 0)
                    {
                        counter = 0; // reset for group membership initialization
                        while (tasks[1].Count > 0)
                        {
                            // run all user's group initialization in parallel and report sequentially as they complete
                            Task<bool> n2 = await Task.WhenAny(tasks[1]).ConfigureAwait(false);
                            if (!(await n2.ConfigureAwait(false))) return false; //  // a failure occurred, see log file
                            tasks[1].Remove(n2);
                            if (progress != null)
                                progress.Report(++counter);
                        }
                    }

                    ret = true; // operation completed with no errors
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcUsers.initAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcUsers.initAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
        //@}
        #endregion

        /// <summary>
        /// Get the AcUser object for AccuRev principal \e name.
        /// </summary>
        /// <param name="name">AccuRev principal name.</param>
        /// <returns>AcUser object for principal \e name or \e null if not found.</returns>
        public AcUser getUser(string name)
        {
            return this.SingleOrDefault(n => String.Equals(n.Principal.Name, name));
        }

        /// <summary>
        /// Get the AcUser object for AccuRev principal \e ID number.
        /// </summary>
        /// <param name="ID">AccuRev principal ID number.</param>
        /// <returns>AcUser object for principal \e ID number or \e null if not found.</returns>
        public AcUser getUser(int ID)
        {
            return this.SingleOrDefault(n => n.Principal.ID == ID);
        }

        /// <summary>
        /// Get the AcUser object owner of AccuRev workspace \e name.
        /// </summary>
        /// <param name="name">Name of the workspace to query.</param>
        /// <returns>AcUser object for owner of workspace \e name or \e null if not found.</returns>
        public AcUser getWorkspaceOwner(string name)
        {
            int index = name.LastIndexOf('_');
            string prncpl = name.Substring(++index);
            return getUser(prncpl);
        }
    }
}
