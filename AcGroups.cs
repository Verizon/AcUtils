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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    /// <summary>
    /// A container class for AcPrincipal objects that define AccuRev groups.
    /// </summary>
    /// <remarks>
    /// Elements contain AccuRev group [principal attributes](@ref AcUtils#AcPrincipal) \e name, \e ID, and 
    /// \e status (active or inactive), and optionally their [members list](@ref AcUtils#AcGroups#initMembersListAsync). 
    /// Deactivated (removed) groups can be included in the list as well.
    /// </remarks>
    [Serializable]
    public sealed class AcGroups : List<AcPrincipal>
    {
        #region Class variables
        private bool _includeMembersList;
        private bool _includeDeactivated;
        [NonSerialized] private readonly object _locker = new object();
        #endregion

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container class for AcPrincipal objects that define AccuRev groups. 
        /// Elements contain AccuRev group [principal attributes](@ref AcUtils#AcPrincipal) \e name, \e ID, 
        /// and \e status (active or inactive), and optionally their members list. Including group membership 
        /// (<em>includeMembersList = true</em>) is slower, so we give the option to exclude it when the list 
        /// isn't needed. Deactivated (removed) groups can be included in the list as well.
        /// </summary>
        /// <remarks>
        /// A group's membership list is comprised of principals (users and groups) who were explicitly added 
        /// to the group by the \c addmember command and not those with implicit membership, i.e. if principal 
        /// is a member of groupA and groupA is a member of groupB, principal is implicitly a member of groupB. 
        /// In this case the principal would not appear in groupB's members list.
        /// </remarks>
        /// <param name="includeMembersList">\e true to include group membership initialization for each group (slower), 
        /// \e false for no initialization (faster).</param>
        /// <param name="includeDeactivated">\e true to include deactivated (removed) groups, otherwise \e false.</param>
        /*! \code
            static async Task<bool> showGroupsAsync()
            {
                // list all group names
                AcGroups groups = new AcGroups(); // two-part object construction
                if (!(await groups.initAsync())) // ..
                    return false; // see log file for error

                foreach (AcPrincipal prncpl in groups.OrderBy(n => n)) // use default comparer
                    Console.WriteLine(prncpl);

                return true; // operation succeeded
            }
            \endcode */
        /*! \sa initAsync, #initMembersListAsync, [default comparer](@ref AcPrincipal#CompareTo), [AcUsers constructor](@ref AcUtils#AcUsers#AcUsers) */
        public AcGroups(bool includeMembersList = false, bool includeDeactivated = false)
        {
            _includeMembersList = includeMembersList;
            _includeDeactivated = includeDeactivated;
        }

        /// <summary>
        /// Populate this container with AcPrincipal objects as per [constructor parameters](@ref AcUtils#AcGroups#AcGroups).
        /// </summary>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \show_ <tt>show \<-fx | -fix\> groups</tt> */
        public async Task<bool> initAsync()
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = _includeDeactivated ? "show -fix groups" : "show -fx groups";
                AcResult r = await AcCommand.runAsync(cmd).ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    List<Task<bool>> tasks = new List<Task<bool>>();
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("Element") select element;
                    foreach (XElement e in query)
                    {
                        AcPrincipal group = new AcPrincipal();
                        group.Name = (string)e.Attribute("Name");
                        group.ID = (int)e.Attribute("Number");
                        // XML attribute isActive exists only if the group is inactive, otherwise it isn't there
                        group.Status = (e.Attribute("isActive") == null) ? PrinStatus.Active : PrinStatus.Inactive;
                        lock (_locker) { Add(group); }
                        if (_includeMembersList)
                        {
                            Task<bool> task = initMembersListAsync(group.Name);
                            tasks.Add(task);
                        }
                    }

                    if (_includeMembersList && tasks.Count > 0)
                    {
                        bool[] arr = await Task.WhenAll(tasks).ConfigureAwait(false); // run all group's membership initialization in parallel
                        if (arr == null || arr.Any(n => n == false))
                            return false; // failure in initMembersListAsync, see log file
                    }

                    ret = true; // list initialization succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcGroups.initAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcGroups.initAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
        //@}
        #endregion

        /// <summary>
        /// Called (optional) during [list construction](@ref AcUtils#AcGroups#AcGroups) to initialize the 
        /// [list of principals](@ref AcPrincipal#Members) (users and groups) that are direct (explicit) members of \e group.
        /// </summary>
        /// <remarks>Membership lists for inactive groups are empty (not initialized). An inactive group's membership list will 
        /// reappear when the group is reactivated.</remarks>
        /// <param name="group">Name of AccuRev group.</param>
        /// <returns>\e true if no exception was thrown and operation succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcUser.initGroupsListAsync](@ref AcUtils#AcUser#initGroupsListAsync) */
        /*! \show_ <tt>show -fx -g \<group\> members</tt> */
        /*! \accunote_ Unlike the \c show command used here, its <tt>show -fx -u \<user\> groups</tt> counterpart 
             does include memberships resulting from indirect (implicit) membership. */
        private async Task<bool> initMembersListAsync(string group)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = String.Format(@"show -fx -g ""{0}"" members", group);
                AcResult r = await AcCommand.runAsync(cmd).ConfigureAwait(false);
                if (r != null && r.RetVal == 0) // if command succeeded
                {
                    SortedSet<string> members = new SortedSet<string>();
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("Element") select element;
                    foreach (XElement e in query)
                    {
                        string name = (string)e.Attribute("User");
                        members.Add(name);
                    }

                    lock (_locker)
                    {
                        AcPrincipal prncpl = getPrincipal(group);
                        prncpl.Members = members;
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcGroups.initMembersListAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcGroups.initMembersListAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        /// <summary>
        /// Determines if \e user is a member of \e group by way of direct or indirect (implicit) membership, 
        /// e.g. Mary is implicitly a member of groupA because she's a member of groupB which is a member of groupA.
        /// </summary>
        /// <param name="user">AccuRev principal name of \e user.</param>
        /// <param name="group">AccuRev principal name of \e group.</param>
        /// <returns>\e true if \e user is a member of \e group or \e false if not a member.</returns>
        /// <exception cref="AcUtilsException">thrown on AccuRev program invocation failure for the \c ismember command.</exception>
        /// <exception cref="Win32Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on error spawning the AccuRev process that runs the command.</exception>
        /// <exception cref="InvalidOperationException">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcGroups.getMembers](@ref AcUtils#AcGroups#getMembers), [AcUser.getGroups](@ref AcUtils#AcUser#getGroups) */
        /*! \ismember_ <tt>ismember \<user\> \<group\></tt> */
        /*! \note The AccuRev program return value for \c ismember is <em>zero (0)</em> whether the user is a member of the group or not. 
             It is the command's output (STDOUT) that is \"<b>1</b>\" (\e one) if the user is a member of the group or \"<b>0</b>\" (\e zero) if not a member. */
        /*! \warning The server admin trigger must return <em>zero (0)</em> for \c ismember to prevent an endless recursive loop. */
        public static bool isMember(string user, string group)
        {
            bool ret = false; // assume not a member
            string command = String.Format(@"ismember ""{0}"" ""{1}""", user, group);
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "accurev";
                    process.StartInfo.Arguments = command;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardInput = true; // fix for AccuRev defect 29059
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                    process.ErrorDataReceived += new DataReceivedEventHandler(AcDebug.errorDataHandler);
                    process.Start();
                    process.BeginErrorReadLine();
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        if (result.Length > 0 && result[0] == '1')
                            ret = true;
                    }
                    else
                    {
                        string err = String.Format("AccuRev program return: {0}{1}{2}", process.ExitCode, Environment.NewLine, "accurev " + command);
                        throw new AcUtilsException(err); // let calling method handle
                    }
                }
            }

            catch (Win32Exception ecx)
            {
                string msg = String.Format("Win32Exception caught and logged in AcGroups.isMember{0}{1}{0}accurev {2}{0}errorcode: {3}{0}native errorcode: {4}{0}{5}{0}{6}{0}{7}",
                    Environment.NewLine, ecx.Message, command, ecx.ErrorCode.ToString(), ecx.NativeErrorCode.ToString(), ecx.StackTrace, ecx.Source, ecx.GetBaseException().Message);
                AcDebug.Log(msg);
            }

            catch (InvalidOperationException ecx)
            {
                string msg = String.Format("InvalidOperationException caught and logged in AcGroups.isMember{0}{1}{0}accurev {2}",
                    Environment.NewLine, ecx.Message, command);
                AcDebug.Log(msg);
            }

            return ret;
        }

        /// <summary>
        /// Retrieves the AcPrincipal object for AccuRev group \e name.
        /// </summary>
        /// <param name="name">AccuRev group name to query.</param>
        /// <returns>AcPrincipal object for group \e name if found, otherwise \e null.</returns>
        public AcPrincipal getPrincipal(string name)
        {
            return this.SingleOrDefault(n => String.Equals(n.Name, name));
        }

        /// <summary>
        /// Returns the [list of members](@ref AcUtils#AcGroups#initMembersListAsync) in \e group as a formatted string. 
        /// List optionally initialized by [AcGroups constructor](@ref AcUtils#AcGroups#AcGroups) when \e includeMembersList param is \e true.
        /// </summary>
        /// <param name="group">AccuRev group name to query.</param>
        /// <returns>Group's membership list as an ordered formatted comma-delimited string, otherwise \e null if not found.</returns>
        /*! \sa [AcGroups.isMember](@ref AcUtils#AcGroups#isMember), [AcUser.getGroups](@ref AcUtils#AcUser#getGroups) */
        public string getMembers(string group)
        {
            string list = null;
            AcPrincipal prncpl = getPrincipal(group);
            if (prncpl != null)
            {
                SortedSet<string> members = prncpl.Members;
                if (members != null)
                {
                    IEnumerable<string> e = members.OrderBy(s => s);
                    list = String.Join(", ", e);
                }
            }

            return list;
        }
    }
}
