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
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    /// <summary>
    /// Miscellaneous stuff that didn't fit in elsewhere.
    /// </summary>
    [Serializable]
    public static class AcQuery
    {
        /// <summary>
        /// Determines if the current user is logged into AccuRev and, if so, retrieves their principal name.
        /// </summary>
        /// <remarks>Implemented by extracting the user's principal name or string <b>\"(not logged in)\"</b> from the \c info command results.</remarks>
        /// <returns>A string initialized to the name of the principal logged into AccuRev or \e null if not logged in.</returns>
        /*! \info_ \c info */
        /*! \accunote_ To support GUIs logging into replicas, get host and port from \c info command 
            (instead of from XML \<serverInfo\> results, which are not correct for replica) 
            to use in titlebar, preferences.xml, and MQTT messages. 35934/17776122 (35921/17776020) */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c info command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \note The user must have <tt>AccuRev\\bin</tt> in their \e Path environment variable to run 
             AccuRev commands issued by the AcUtils library. Since this method is often the first function called 
             by a client application (in this case \c info), a failure here usually means this entry is missing.*/
        public static async Task<string> getPrincipalAsync()
        {
            string prncpl = null;
            try
            {
                AcResult r = await AcCommand.runAsync("info").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    using (StringReader reader = new StringReader(r.CmdResult))
                    {
                        bool? login = null; // don't know yet
                        string line;
                        char[] sep = new char[] { ':' };
                        while ((line = reader.ReadLine()) != null && login == null)
                        {
                            string[] arr = line.Split(sep); // "Principal:      barnyrd"
                            if (arr.Length == 2)
                            {
                                if (String.Equals(arr[0], "Principal"))
                                {
                                    string temp = arr[1].Replace("\t", "");
                                    if (String.Equals(temp, "(not logged in)"))
                                        login = false;
                                    else
                                    {
                                        prncpl = temp; // AccuRev principal name
                                        login = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getPrincipalAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getPrincipalAsync{Environment.NewLine}{ecx.Message}");
            }

            return prncpl; // principal name or null if not logged in
        }

        /// <summary>
        /// Use to retrieve the contents of a text file under version control.
        /// </summary>
        /// <remarks>
        /// Puts the content of the specified file in a temporary file. The caller is responsible for deleting the file.
        /// </remarks>
        /// <param name="eid">The file's element ID.</param>
        /// <param name="depot">The depot.</param>
        /// <param name="ver_spec">The version specification in the numeric or text format, e.g. \c 32/1 or \c PG_MAINT1_barnyrd\4.</param>
        /// <returns>A string initialized to the name of the temporary file with the contents from the 
        /// AccuRev <tt>cat -v \<ver_spec\> -p \<depot\> -e \<eid\></tt> command on success, otherwise \e null on error.
        /// </returns>
        /*! \cat_ <tt>cat -v \<ver_spec\> -p \<depot\> -e \<eid\></tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c cat command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \accunote_ The CLI \c cat command fails on Windows for ptext files larger than 62,733 bytes. Fixed in 6.0. AccuRev defect 28177. */
        /*! \accunote_ For the \c name, \c anc and \c cat commands, the validity of the results is guaranteed only if the commands are issued outside a workspace, or in a workspace whose 
             backing stream is in the same depot for both workspace and the <tt>-v \<ver_spec\></tt> option used. If issued from a workspace that has a different backing stream than 
             the <tt>-v \<ver_spec\></tt>, provided both workspace and ver_spec share the same depot, the command results can be deemed valid. However, if issued from a workspace whose 
             backing stream is in a different depot than the <tt>-v \<ver_spec\></tt>, the command results are invalid. Applications should set the default directory to a non-workspace 
             location prior to issuing these commands. AccuRev defects 18080, 21469, 1097778. */
        public static async Task<string> getCatFileAsync(int eid, AcDepot depot, string ver_spec)
        {
            string tmpFile = null;
            try
            {
                AcResult r = await AcCommand.runAsync($@"cat -v ""{ver_spec}"" -p ""{depot}"" -e {eid}")
                    .ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    tmpFile = Path.GetTempFileName();
                    using (StreamWriter streamWriter = new StreamWriter(tmpFile))
                    {
                        streamWriter.Write(r.CmdResult);
                    }
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getCatFileAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx) // IOException, DirectoryNotFoundException, PathTooLongException, SecurityException... others
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getCatFileAsync{Environment.NewLine}{ecx.Message}");
            }

            return tmpFile;
        }

        /// <summary>
        /// For the real version specified in the argument list, get the real stream/version 
        /// for the version in the specified version's workspace backing stream.
        /// </summary>
        /// <remarks>\c diff used here with the \c -i option, not to run a diff but to get the EID's only.</remarks>
        /// <param name="realverspec">Real version specification in text format, 
        /// for example <tt>PG_MAINT1_barnyrd\4</tt> (numeric format won't work).</param>
        /// <param name="depot">The depot.</param>
        /// <param name="depotrelpath">Depot relative path, for example <tt>\\\\\.\\Bin\\foo.java</tt></param>
        /// <returns>An array initialized as <em>int[]={realStreamNumber, realVersionNumber}</em> if available, otherwise \e null.</returns>
        /*! \diff_ The \c diff command returns <em>zero (0)</em> for no differences found, 
            <em>one (1)</em> for differences found, or <em>two (2)</em> on \c diff program error.<br>
            <tt>diff -fx -v \<realverspec\> -b -i -p \<depot\> \<depotrelpath\></tt> */
        /*! > -v -b Compare the specified version (X) with the version in X's stream's backing stream. (Use -v -V instead if comparing elements of type text that are active in a time-based stream.) */
        /*! > -i Information only: Report the IDs of the two versions, but don't actually compare them. This option is valid only in a command that uses a -v/-V combination or a -v/-b combination. If an element appears only in the workspace stream, not in the workspace's backing stream, this option does not list it. */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c diff command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        public static async Task<int[]> getBackedVersionAsync(string realverspec, AcDepot depot, string depotrelpath)
        {
            int[] arr = null; // {realStreamNumber, realVersionNumber}
            try
            {
                AcResult r = await AcCommand.runAsync($@"diff -fx -v ""{realverspec}"" -b -i -p ""{depot}"" ""{depotrelpath}""")
                    .ConfigureAwait(false);
                if (r != null && r.RetVal < 2)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    XElement stream2 = xml.Descendants().Where(n => n.Name == "Stream2").SingleOrDefault();
                    if (stream2 != null)
                    {
                        XAttribute version = stream2.Attributes("Version").SingleOrDefault();
                        if (version != null)
                        {
                            string temp = (string)version;
                            string[] a = temp.Split('/');
                            int realStreamNumber = Int32.Parse(a[0], NumberStyles.Integer);
                            int realVersionNumber = Int32.Parse(a[1], NumberStyles.Integer);
                            arr = new int[] { realStreamNumber, realVersionNumber };
                        }
                    }
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getBackedVersionAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getBackedVersionAsync{Environment.NewLine}{ecx.Message}");
            }

            return arr;
        }

        /// <summary>
        /// Get the depot-relative path for the element in \e stream with \e EID. 
        /// Also returns the element's parent folder EID (folder where the element resides).
        /// </summary>
        /// <param name="stream">Name of the stream where the element resides.</param>
        /// <param name="EID">The element ID of the element on which to query.</param>
        /// <returns>An tuple initialized as <em>{depot-relative path, parent EID}</em> on success, otherwise \e null.</returns>
        /*! \name_ <tt>name -v \<stream\> -fx -e \<EID\></tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c name command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \code
        accurev name -v MARS_MAINT2 -fx -e 13763
        <?xml version="1.0" encoding="utf-8"?>
        <AcResponse
            Command="name"
            TaskId="51498">
          <element
              location="\.\scripts\build.xml"
              parent_id="12269"/>
        </AcResponse>
        \endcode */
        /*! \accunote_ For the \c name, \c anc and \c cat commands, the validity of the results is guaranteed only if the commands are issued outside a workspace, or in a workspace whose 
             backing stream is in the same depot for both workspace and the <tt>-v \<ver_spec\></tt> option used. If issued from a workspace that has a different backing stream than 
             the <tt>-v \<ver_spec\></tt>, provided both workspace and ver_spec share the same depot, the command results can be deemed valid. However, if issued from a workspace whose 
             backing stream is in a different depot than the <tt>-v \<ver_spec\></tt>, the command results are invalid. Applications should set the default directory to a non-workspace 
             location prior to issuing these commands. AccuRev defects 18080, 21469, 1097778. */
        public static async Task<Tuple<string, int>> getElementNameAsync(string stream, int EID)
        {
            Tuple<string, int> ret = null; // depot-relative path, parent folder EID
            try
            {
                AcResult r = await AcCommand.runAsync($@"name -v ""{stream}"" -fx -e {EID}").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    string location = (string)xml.Element("element").Attribute("location");
                    int parent_id = (int)xml.Element("element").Attribute("parent_id");
                    ret = Tuple.Create(location, parent_id);
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getElementNameAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getElementNameAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        /// <summary>
        /// Type safe way to retrieve values from <tt>\<prog_name\>.exe.config</tt> files.
        /// </summary>
        /// <typeparam name="T">Data type for <tt>\<prog_name\>.exe.config</tt> file entry.</typeparam>
        /// <param name="key">Key name from <tt>\<prog_name\>.exe.config</tt> file entry.</param>
        /// <returns><tt>\<prog_name\>.exe.config</tt> value for \e key.</returns>
        /*! \sa <a href="https://msdn.microsoft.com/en-us/library/dtb69x08(v=vs.110).aspx">Convert.ChangeType Method (Object, Type)</a> */
        public static T getAppConfigSetting<T>(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            return (T)System.Convert.ChangeType(value, typeof(T),
                CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Retrieves the AccuRev program major, minor and patch version numbers.
        /// </summary>
        /// <returns>An array initialized as <em>int[]={major, minor, patch}</em> on success, otherwise \e null on error.</returns>
        /*! \xml_ <tt>xml -l \<xmlfile\></tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c xml command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \code
        <?xml version="1.0" encoding="utf-8"?>
        <serverInfo>
          <serverVersion
              major="5"
              minor="6"
              patch="0"/>
          <serverHostPort>machine_name_omitted:5050</serverHostPort>
        </serverInfo>
         \endcode */
        public static async Task<int[]> getAccuRevVersionAsync()
        {
            int[] arr = null; // major, minor, patch
            string tempFile = null;
            try
            {
                tempFile = Path.GetTempFileName(); // the AccuRev xml command requires a file as its argument
                using (StreamWriter streamWriter = new StreamWriter(tempFile))
                {
                    streamWriter.Write(@"<serverInfo/>"); // set up the query
                }

                AcResult r = await AcCommand.runAsync($@"xml -l ""{tempFile}""").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    using (StringReader reader = new StringReader(r.CmdResult))
                    {
                        XElement doc = XElement.Load(reader);
                        XElement sv = doc.Element("serverVersion");
                        int major = (int)sv.Attribute("major");
                        int minor = (int)sv.Attribute("minor");
                        int patch = (int)sv.Attribute("patch");
                        arr = new int[] { major, minor, patch };
                    }
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getAccuRevVersionAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getAccuRevVersionAsync{Environment.NewLine}{ecx.Message}");
            }

            finally
            {
                if (tempFile != null)
                    File.Delete(tempFile);
            }

            return arr;
        }

        /// <summary>
        /// Get the number of active users.
        /// </summary>
        /// <param name="includeDeactivated">\e true to include deactivated (removed) users, otherwise \e false.</param>
        /// <returns>Number of active users or <em>minus one (-1)</em> on error.</returns>
        /*! \show_ <tt>show -fx users</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        public static async Task<int> getUsersCountAsync(bool includeDeactivated = false)
        {
            int count = -1; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync(includeDeactivated ? "show -fix users" : "show -fx users").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement t = XElement.Parse(r.CmdResult);
                    count = t.Elements("Element").Count();
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getUsersCountAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getUsersCountAsync{Environment.NewLine}{ecx.Message}");
            }

            return count;
        }

        /// <summary>
        /// Get the number of active depots in the repository.
        /// </summary>
        /// <returns>Number of depots or <em>minus one (-1)</em> on error.</returns>
        /*! \show_ <tt>show -fx depots</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        public static async Task<int> getDepotsCountAsync()
        {
            int count = -1; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync("show -fx depots").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement t = XElement.Parse(r.CmdResult);
                    count = t.Elements("Element").Count();
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getDepotsCountAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getDepotsCountAsync{Environment.NewLine}{ecx.Message}");
            }

            return count;
        }

        /// <summary>
        /// Get the number of dynamic streams in the repository that have a default group.
        /// </summary>
        /// <returns>Number of dynamic streams with a default group or <em>minus one (-1)</em> on error.</returns>
        /*! \show_ <tt>show -fx -d streams</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        public static async Task<int> getDynStreamsCountAsync()
        {
            int count = -1; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync("show -fx -d streams").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement s = XElement.Parse(r.CmdResult);
                    count = (from d in s.Elements("stream") where d.Attribute("isDynamic").Value == "true" select d).Count();
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getDynStreamsCountAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getDynStreamsCountAsync{Environment.NewLine}{ecx.Message}");
            }

            return count;
        }

        /// <summary>
        /// Get the total number of streams (including workspace streams) in the repository including those that are hidden.
        /// </summary>
        /// <returns>Total number of streams or <em>minus one (-1)</em> on error.</returns>
        /*! \show_ <tt>show -fix streams</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        public static async Task<int> getTotalStreamsCountAsync()
        {
            int count = -1; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync("show -fix streams").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement t = XElement.Parse(r.CmdResult);
                    count = t.Elements("stream").Count();
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getTotalStreamsCountAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getTotalStreamsCountAsync{Environment.NewLine}{ecx.Message}");
            }

            return count;
        }

        /// <summary>
        /// Get the number of streams (including workspace streams) in the repository that have a default group.
        /// </summary>
        /// <returns>Number of streams with a default group or <em>minus one (-1)</em> on error.</returns>
        /*! \show_ <tt>show -fx -d streams</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        public static async Task<int> getStreamsWithDefaultGroupCountAsync()
        {
            int count = -1; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync("show -fx -d streams").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement t = XElement.Parse(r.CmdResult);
                    count = t.Elements("stream").Count();
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getStreamsWithDefaultGroupCountAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getStreamsWithDefaultGroupCountAsync{Environment.NewLine}{ecx.Message}");
            }

            return count;
        }

        /// <summary>
        /// Get the total number of workspaces in the repository including those that are hidden.
        /// </summary>
        /// <returns>Total number of workspaces or <em>minus one (-1)</em> on error.</returns>
        /*! \show_ <tt>show -fix -a wspaces</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        public static async Task<int> getTotalWorkspaceCountAsync()
        {
            int count = -1; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync("show -fix -a wspaces").ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement t = XElement.Parse(r.CmdResult);
                    count = t.Elements("Element").Count();
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getTotalWorkspaceCountAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getTotalWorkspaceCountAsync{Environment.NewLine}{ecx.Message}");
            }

            return count;
        }

        /// <summary>
        /// Determine if \c acgui.exe is found in the Path and thus can be spawned.
        /// </summary>
        /// <param name="path">The full path to \c acgui.exe on the host machine if found, otherwise \e null.</param>
        /// <returns>\e true if function ran successfully, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public static bool getAcGUIpath(out string path)
        {
            bool error = false; // assume success
            path = null;
            string temp = String.Empty;
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.Arguments = "acgui.exe";
                    process.StartInfo.FileName = "where";
                    process.Start();
                    temp = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                }
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getAcGUIpath{Environment.NewLine}{ecx.Message}");
                error = true;
            }

            bool result = (!error && !String.IsNullOrEmpty(temp));
            if (result)
                path = temp.Substring(0, temp.IndexOf('\r'));
            return result;
        }

        /// <summary>
        /// Helper function for [getServerFromAcClientCnf](@ref AcQuery#getServerFromAcClientCnf) 
        /// returns the full path to \c acclient.cnf on the host machine.
        /// </summary>
        /// <param name="path">Full path to \c acclient.cnf.</param>
        /// <returns>\e true if function ran successfully, \e false otherwise.</returns>
        private static bool getAcClientCnfPath(out string path)
        {
            path = String.Empty;
            string temp;
            bool ret = getAcGUIpath(out temp);
            if (ret)
            {
                string dir = Path.GetDirectoryName(temp);
                path = Path.Combine(dir, "acclient.cnf");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the default AccuRev server from \c acclient.cnf. The default server is the first line in the file.
        /// </summary>
        /// <param name="server">The AccuRev server in the format \c hostname:port, otherwise \e null.</param>
        /// <returns>\e true if function ran successfully, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public static bool getServerFromAcClientCnf(out string server)
        {
            server = null;
            FileStream fs = null;
            try
            {
                string cnfFile;
                if (getAcClientCnfPath(out cnfFile))
                {
                    fs = new FileStream(cnfFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        string line = sr.ReadLine();
                        string[] arr = line.Split('=');
                        server = arr[1].Trim();
                    }

                    return true;
                }
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcQuery.getServerFromAcClientCnf{Environment.NewLine}{ecx.Message}");
            }

            finally // avoids CA2202: Do not dispose objects multiple times
            {
                if (fs != null) fs.Dispose();
            }

            return false;
        }

        /// <summary>
        /// Get the list of depot names in sorted order.
        /// </summary>
        /// <returns>The list of depot names on success, otherwise \e null on error.</returns>
        /*! \show_ <tt>show -fx depots</tt> */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        public static async Task<List<string>> getDepotNameListAsync()
        {
            List<string> depots = null;
            AcResult result = null;
            try { result = await AcCommand.runAsync("show -fx depots").ConfigureAwait(false); }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcQuery.getDepotNameListAsync{Environment.NewLine}{ecx.Message}");
            }

            if (result != null && result.RetVal == 0) // if command succeeded
            {
                XElement xml = XElement.Parse(result.CmdResult);
                IEnumerable<XElement> query = from e in xml.Elements("Element")
                                              select e;
                int num = query.Count();
                depots = new List<string>(num);
                foreach (XElement n in query)
                    depots.Add((string)n.Attribute("Name"));

                depots.Sort();
            }

            return depots;
        }
    }
}
