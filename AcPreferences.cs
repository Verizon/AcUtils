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
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    /// <summary>
    /// Get user preferences retrieved by way of the \c getpref command.
    /// </summary>
    [Serializable]
    public static class AcPreferences
    {
        private static string _acHomeFolder;  // The AccuRev home directory

        /// <summary>
        /// Get the user's Diff/Merge and Ignore Options (AccuRev Diff only) preferences.
        /// </summary>
        /// <remarks>
        /// <em>Ignore Whitespace</em> is \e true if spaces, tabs and empty lines should be ignored, \e false otherwise. 
        /// A setting of \e true overrides <em>Ignore Changes in Whitespace</em> setting.<br>
        /// <em>Ignore Changes in Whitespace</em> is \e true if a change in the amount of whitespace should be considered 
        /// a change to that line, \e false otherwise.<br>
        /// <em>Ignore Case</em> is \e true if uppercase and lowercase characters should be considered the same when 
        /// comparing text, \e false otherwise.
        /// </remarks>
        /// <returns>An array initialized as <em>bool[]={ignoreWhitespace, ignoreWhitespaceChanges, ignoreCase}</em> 
        /// on success, otherwise \e null.</returns>
        /*! \accunote_ Selecting "Ignore Whitespace" and "Ignore Changes in Whitespace" applies to \c diff only. 
        The \c merge command does not use these settings and will show all conflicts. */
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public static async Task<bool[]> getIgnoreOptionsAsync()
        {
            string tmpFile = await getPreferencesAsync().ConfigureAwait(false);
            if (String.IsNullOrEmpty(tmpFile)) // unlikely
                return null; // error already logged

            bool[] arr = null; // ignoreWhitespace, ignoreWhitespaceChanges, ignoreCase
            try
            {
                using (StreamReader reader = new StreamReader(tmpFile))
                {
                    XElement doc = XElement.Load(reader);
                    bool ignoreWhitespace = (bool)doc.Element("diffIgnoreWhitespace");
                    bool ignoreWhitespaceChanges = (bool)doc.Element("diffIgnoreWhitespaceChanges");
                    bool ignoreCase = (bool)doc.Element("diffIgnoreCase");
                    arr = new bool[] { ignoreWhitespace, ignoreWhitespaceChanges, ignoreCase };
                }
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcPreferences.getIgnoreOptionsAsync{Environment.NewLine}{ecx.Message}");
            }

            finally
            {
                if (!String.IsNullOrEmpty(tmpFile))
                    File.Delete(tmpFile);
            }

            return arr;
        }

        /// <summary>
        /// Get the user's USE_IGNORE_ELEMS_OPTIMIZATION setting.
        /// </summary>
        /// <returns>A bool set to \e true or \e false on operation success, otherwise \e null on error.
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public static async Task<bool?> getUseIgnoreElemsOptimizationAsync()
        {
            string tmpFile = await getPreferencesAsync().ConfigureAwait(false);
            if (String.IsNullOrEmpty(tmpFile)) // unlikely
                return null; // error already logged

            bool? useIgnoreElemsOptimization = null;
            try
            {
                using (StreamReader reader = new StreamReader(tmpFile))
                {
                    XElement doc = XElement.Load(reader);
                    useIgnoreElemsOptimization = (bool)doc.Element("USE_IGNORE_ELEMS_OPTIMIZATION");
                }
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcPreferences.getUseIgnoreElemsOptimizationAsync{Environment.NewLine}{ecx.Message}");
            }

            finally
            {
                if (!String.IsNullOrEmpty(tmpFile))
                    File.Delete(tmpFile);
            }

            return useIgnoreElemsOptimization;
        }

        /// <summary>
        /// Get the AccuRev home folder for the current user.
        /// </summary>
        /// <returns>The full path to the user's AccuRev home folder on success, otherwise \e null on error.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public static async Task<string> getAcHomeFolderAsync()
        {
            if (_acHomeFolder == null) // first time initialization only
            {
                string tmpFile = await getPreferencesAsync().ConfigureAwait(false);
                if (String.IsNullOrEmpty(tmpFile)) // unlikely
                    return null; // error already logged

                try
                {
                    using (StreamReader reader = new StreamReader(tmpFile))
                    {
                        XElement doc = XElement.Load(reader);
                        string home = (string) doc.Element("HOME");
                        _acHomeFolder = Path.Combine(home, ".accurev");
                    }
                }

                catch (Exception ecx)
                {
                    AcDebug.Log($"Exception caught and logged in AcPreferences.getAcHomeFolderAsync{Environment.NewLine}{ecx.Message}");
                }

                finally
                {
                    if (!String.IsNullOrEmpty(tmpFile))
                        File.Delete(tmpFile);
                }
            }

            return _acHomeFolder;
        }

        /// <summary>
        /// Get user preferences retrieved by way of the \c getpref command.
        /// </summary>
        /// <returns>The full path to a temp file with the XML results from the \e getpref command, otherwise \e null on error. 
        /// The caller is responsible for deleting the file.</returns>
        /*! \getpref_  \c getpref */
        /*! \accunote_ When using the AccuRev GUI client, changes in <em>User Preferences</em> do not always persist. 
            As a workaround, use [setpref](https://supportline.microfocus.com/Documentation/books/AccuRev/AccuRev/7.0.1/webhelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_setpref.html):
 -# Exit the AccuRev client.
 -# Put the setting\(s\) needed in a file named <tt>set_pref.xml</tt>
 -# Open a command window and cd to the folder where the file is located.
 -# Run the command <tt>accurev setpref -l set_pref.xml</tt> \(-l switch is a lowercase L\)
 -# Run the AccuRev client and verify the setting\(s\).
\verbatim
<AcRequest>
<USE_IGNORE_ELEMS_OPTIMIZATION>true</USE_IGNORE_ELEMS_OPTIMIZATION>
...
</AcRequest>
\endverbatim */
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c getpref command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        private static async Task<string> getPreferencesAsync()
        {
            // Putting the XML into a file as opposed to using it directly avoids exceptions thrown
            // due to illegal characters. Needs further investigation.
            string tempFile = null;
            try
            {
                AcResult r = await AcCommand.runAsync("getpref").ConfigureAwait(false);
                if (r != null && r.RetVal == 0) // if command succeeded
                {
                    tempFile = Path.GetTempFileName();
                    using (StreamWriter writer = new StreamWriter(tempFile))
                    {
                        writer.Write(r.CmdResult);
                    }
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcPreferences.getPreferencesAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx) // IOException, DirectoryNotFoundException, PathTooLongException, SecurityException... others
            {
                AcDebug.Log($"Exception caught and logged in AcPreferences.getPreferencesAsync{Environment.NewLine}{ecx.Message}");
            }

            return tempFile;
        }
    }
}
