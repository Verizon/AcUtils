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
using System.Diagnostics;
using System.IO;
using System.Xml.XPath;
using Microsoft.VisualBasic.Logging;

namespace AcUtils
{
    /*! \defgroup acenum Enums */
    /*! \ingroup acenum */
    ///@{
    /// <summary>
    /// For trigger development, debugging, and troubleshooting.
    /// </summary>
    /// <remarks>
    /// Use to copy the <a href="https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html">XML param data</a> 
    /// sent to the trigger by AccuRev to <tt>\%LOCALAPPDATA\%\\AcTools\\Param\\<trigger-command-principal-timestamp\>.xml</tt> 
    /// on the machine where the trigger process runs. The \c AcTools\\Param folder is created if not already there. 
    /// Example file: \c server_preop_trig-promote-barnyrd-2013429445183.xml.<br><br>
    /// The following triggers cause the command to succeed or fail based on the trigger's program return value: 
    /// <em>zero (0)</em> to have the command succeed, \e non-zero to have it fail (repository remains unchanged).</remarks><small>
    /*!
    - pre-create-trig
    - pre-keep-trig
    - pre-promote-trig
    - server_preop_trig
    - server_admin_trig
    - server_auth_trig

    </small> */
    /*! \par Example \<trigger\>.exe.config file \e appSettings entry and client code: */
    /*! \code
    <appSettings>
      ...
      <add key="ParamFileCopy" value="CopyParmFileExitSuccess" />
    </appSettings>
    \endcode */

    /*! \code
        private static ParamFileCopy _paramFileCopy;

        string pfc = ConfigurationManager.AppSettings["ParamFileCopy"]; // get value from server_admin_trig.exe.config
        _paramFileCopy = String.IsNullOrEmpty(pfc) ? 
            ParamFileCopy.NoParamFileCopy : (ParamFileCopy)Enum.Parse(typeof(ParamFileCopy), pfc);
        ...
        _xmlParamFile = args[0]; // trigger XML param file passed to this trigger by AccuRev
        if (!TriggerParamXML.init(_xmlParamFile)) // initialize TriggerParamXML class static data members
            return 1; //  error already logged. non-zero return causes the operation to fail

        // save the XML param file content to our file in %LOCALAPPDATA%\AcTools\Param
        AcDebug.paramFileCopy(_xmlParamFile, _paramFileCopy, TriggerParamXML.Trigger,
            TriggerParamXML.Principal, TriggerParamXML.AcCommand);
    \endcode */
    public enum ParamFileCopy
    {
        /*! \var NoParamFileCopy
        Do not create copies of the XML param data. The trigger and AccuRev operation execute normally. */
        NoParamFileCopy,
        /*! \var CopyParmFileTrigTarget
        Create copies of the XML param data for <em>select users and triggers</em> if a matching entry is found in 
        [TriggersTarget.xml](@ref AcUtils#AcDebug#entryInTrigTargetsFile) located on the [AccuRevTriggers](@ref AcUtils#AcDebug#TrigTargetEnv) 
        network share. The trigger and AccuRev operation execute normally. */
        CopyParmFileTrigTarget,
        /*! \var CopyParmFileExitSuccess
        Run the trigger just long enough to create copies of the XML param data and then immediately terminate it with a 
        <em>zero (0)</em> return value (\e Success) thus allowing the operation to succeed (where applicable). */
        CopyParmFileExitSuccess,
        /*! \var CopyParmFileExitFailure
        Run the trigger just long enough to create copies of the XML param data and then immediately terminate it with a 
        <em>one (1)</em> return value (\e Failure) thus causing the operation to fail (where applicable). */
        CopyParmFileExitFailure,
        /*! \var CopyParmFileContinue
        Create copies of the XML param data with no early trigger termination. The trigger and AccuRev operation execute normally. */
        CopyParmFileContinue
    };
    ///@}

    /// <summary>
    /// Use to [log](@ref AcUtils#AcDebug#Log) and display error and general purpose text messages, and to [save](@ref AcUtils#ParamFileCopy) the 
    /// [XML param data](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html) 
    /// sent by AccuRev to a trigger for development, debugging, and troubleshooting purposes. For OS environment problems, log the system 
    /// network activity generated by the program by modifying <tt>\<prog_name\>.exe.config</tt> similar to [this version](@ref netDiagLog).
    /// </summary>
    [Serializable]
    public static class AcDebug
    {
        private static readonly string _trigTargetEnvVar = "AccuRevTriggers";

        /// <summary>
        /// Returns \b "AccuRevTriggers", the name of the environment variable on the AccuRev server that points to the network share 
        /// where [TriggersTarget.xml](@ref AcUtils#AcDebug#entryInTrigTargetsFile) and other application support files are located.
        /// </summary>
        public static string TrigTargetEnv
        {
            get { return _trigTargetEnvVar; }
        }

        private static readonly string _trigTargetFile = "TriggersTarget.xml";
        // global logging support
        private static FileLogTraceListener _log;
        [NonSerialized] private static readonly object _locker = new object();

        /// <summary>
        /// Used by a trigger to save the content of the [XML param file](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html) 
        /// passed to it by AccuRev to <tt>\%LOCALAPPDATA\%\\AcTools\\Param\\<trigger-command-principal-timestamp\>.xml</tt>.
        /// </summary>
        /// <param name="xmlFile">XML param file sent to the trigger by AccuRev.</param>
        /// <param name="pfcopy">[ParamFileCopy](@ref AcUtils#ParamFileCopy) enum to indicate in what manner to handle the XML param file passed by AccuRev.</param>
        /// <param name="trigger">Name of this trigger.</param>
        /// <param name="prncpl">Principal name of the user who issued the command.</param>
        /// <param name="command">AccuRev command issued.</param>
        /// <returns>\e true if operation ran successfully, \e false on error.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<trig_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        /*! \sa paramFileCopy(ParamFileCopy, string, string, string) */
        public static bool paramFileCopy(string xmlFile, ParamFileCopy pfcopy, string trigger, string prncpl, string command)
        {
            bool ret = true; // assume success
            try
            {
                if (pfcopy != ParamFileCopy.NoParamFileCopy)
                {
                    if (
                        (pfcopy == ParamFileCopy.CopyParmFileTrigTarget &&
                            entryInTrigTargetsFile(trigger, prncpl)) ||
                        (pfcopy == ParamFileCopy.CopyParmFileContinue) ||
                        (pfcopy == ParamFileCopy.CopyParmFileExitSuccess) ||
                        (pfcopy == ParamFileCopy.CopyParmFileExitFailure)
                        )
                    {
                        string localappdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string paramFolder = Path.Combine(localappdata, "AcTools\\Param");
                        if (!Directory.Exists(paramFolder))
                            Directory.CreateDirectory(paramFolder);
                        FileInfo fileInfoXml = new FileInfo(xmlFile);
                        string pFile;
                        if (String.IsNullOrEmpty(command))
                        {
                            // command is null in the case of server_post_promote
                            pFile = String.Format(@"{0}\{1}-{2}-{3}.xml",
                                paramFolder, trigger, prncpl, getStamp());
                        }
                        else
                        {
                            pFile = String.Format(@"{0}\{1}-{2}-{3}-{4}.xml",
                                paramFolder, trigger, command, prncpl, getStamp());
                        }

                        fileInfoXml.CopyTo(pFile, true);
                        if (pfcopy == ParamFileCopy.CopyParmFileExitSuccess)
                            Environment.Exit(0);
                        else if (pfcopy == ParamFileCopy.CopyParmFileExitFailure)
                            Environment.Exit(1);
                    }
                }
            }

            catch (Exception ecx)
            {
                Log($"Exception caught and logged in AcDebug.paramFileCopy(string, ParamFileCopy, string, string, string){Environment.NewLine}{ecx.Message}");
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Used by the <a href="https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/user_auth.html">server_auth_trig</a> 
        /// to save the content of the [XML param data](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html) 
        /// passed to it by AccuRev to <tt>\%LOCALAPPDATA\%\\AcTools\\Param\\server_auth_trig-principal-timestamp.xml</tt>.
        /// </summary>
        /// <remarks>
        /// Overloaded version used by the <a href="https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/user_auth.html">server_auth_trig</a> 
        /// since the [XML param data](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html) 
        /// is sent, for security reasons, via STDIN and not in a file.
        /// </remarks>
        /// <param name="pfcopy">[ParamFileCopy](@ref AcUtils#ParamFileCopy) enum to indicate in what manner to handle the XML param data passed by AccuRev.</param>
        /// <param name="xml">String containing the XML param data.</param>
        /// <param name="trigger">Name of the trigger (\e server_auth_trig until something changes).</param>
        /// <param name="prncpl">Principal name of the user who issued the command.</param>
        /// <returns>\e true if operation ran successfully, \e false on error.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\server_auth_trig-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        /*! \sa paramFileCopy(string, ParamFileCopy, string, string, string) */
        public static bool paramFileCopy(ParamFileCopy pfcopy, string xml, string trigger, string prncpl)
        {
            bool ret = true; // assume success
            try
            {
                if (pfcopy != ParamFileCopy.NoParamFileCopy)
                {
                    if (
                        (pfcopy == ParamFileCopy.CopyParmFileTrigTarget &&
                            entryInTrigTargetsFile(trigger, prncpl)) ||
                        (pfcopy == ParamFileCopy.CopyParmFileContinue) ||
                        (pfcopy == ParamFileCopy.CopyParmFileExitSuccess) ||
                        (pfcopy == ParamFileCopy.CopyParmFileExitFailure)
                        )
                    {
                        string localappdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string paramFolder = Path.Combine(localappdata, "AcTools\\Param");
                        if (!Directory.Exists(paramFolder))
                            Directory.CreateDirectory(paramFolder);
                        string pFile = String.Format(@"{0}\{1}-{2}-{3}.xml",
                            paramFolder, trigger, prncpl, getStamp());
                        createParamFile(pFile, xml);
                        if (pfcopy == ParamFileCopy.CopyParmFileExitSuccess)
                            Environment.Exit(0);
                        else if (pfcopy == ParamFileCopy.CopyParmFileExitFailure)
                            Environment.Exit(1);
                    }
                }
            }

            catch (Exception ecx)
            {
                Log($"Exception caught and logged in AcDebug.paramFileCopy(ParamFileCopy, string, string, string){Environment.NewLine}{ecx.Message}");
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Helper function that creates the 
        /// [XML param data](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html) 
        /// copy.
        /// </summary>
        /// <param name="fileName">Name of file to create.</param>
        /// <param name="content">String containing the XML param data.</param>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<trig_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        private static void createParamFile(string fileName, string content)
        {
            FileStream fs = null;
            try
            {
                if (File.Exists(fileName)) // highly unlikely it would exist but check anyway
                    File.Delete(fileName);

                fs = new FileStream(fileName, FileMode.Create);
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.Write(content);
                }
            }

            catch (Exception exc)
            {
                String msg = String.Format("Exception in AcDebug.createParamFile caught and logged.{0}{1}{0}Filename: {2}{0}{3}",
                    Environment.NewLine, exc.Message, fileName, content);
                Log(msg);
            }

            finally // avoids CA2202: Do not dispose objects multiple times
            {
                if (fs != null) fs.Dispose();
            }
        }

        /// <summary>
        /// Helper function for creating a unique filename for our copy of the 
        /// [XML param data](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html).
        /// </summary>
        /// <returns>String to ensure the copied file is unique and doesn't get subsequently overwritten.</returns>
        private static string getStamp()
        {
            string stamp = DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString() +
                DateTime.Now.Hour.ToString() + DateTime.Now.Minute.ToString() + DateTime.Now.Second.ToString() +
                DateTime.Now.Millisecond.ToString();
            return stamp;
        }

        /// <summary>
        /// Determine if an entry exists for this user and trigger combo in \c TriggersTarget.xml.
        /// </summary>
        /// <remarks>
        /// Use the [CopyParmFileTrigTarget](@ref AcUtils#ParamFileCopy) enum directive in the \e appSettings section of a 
        /// <tt>\<trigger\>.exe.config</tt> file (located in the same folder as the trigger's executable) to have the trigger's 
        /// [XML param data](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html) 
        /// copied to <tt>\%LOCALAPPDATA\%\\AcTools\\Param\\<trigger-command-principal-timestamp\>.xml</tt> for <em>specific users and triggers</em>. 
        /// Only param data for matching entries in <tt>TriggersTarget.xml</tt> located on the [AccuRevTriggers](@ref AcUtils#AcDebug#TrigTargetEnv) 
        /// network share are copied.
        /// </remarks>
        /// <param name="trigger">The name of the trigger sent by AccuRev.</param>
        /// <param name="prncpl">Name of the principal sent by AccuRev.</param>
        /// <returns>\e true if entry exists, \e false otherwise.</returns>
        /// <exception cref="XPathException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<trig_name\>-YYYY-MM-DD.log</tt> on xpath failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \par In \<trigger\>.exe.config: */
        /*! \code
        <appSettings>
        ...
        <add key="ParamFileCopy" value="CopyParmFileTrigTarget" />
        </appSettings>
        \endcode */
        /*! \par Example TriggersTarget.xml: */
        /*! \code
        <Triggers>
          <Trigger Name="server-post-promote">
            <Principal Name="barnyrd"></Principal>
          </Trigger>
          <Trigger Name="server_admin_trig">
            <Principal Name="robert"></Principal>
            <Principal Name="barnyrd"></Principal>
          </Trigger>
          <Trigger Name="server_preop_trig">
            <Principal Name="thomas"></Principal>
          </Trigger>
          <Trigger Name="server_auth_trig">
            <Principal Name="madhuri"></Principal>
            <Principal Name="thomas"></Principal>
          </Trigger>
        </Triggers>
        \endcode */
        private static bool entryInTrigTargetsFile(string trigger, string prncpl)
        {
            try
            {
                string triggersFolder = Environment.GetEnvironmentVariable(_trigTargetEnvVar);
                if (triggersFolder != null) // if the AccuRevTriggers environment variable is set
                {
                    string xmlTriggersFile = Path.Combine(triggersFolder, _trigTargetFile);
                    XPathDocument doc = new XPathDocument(xmlTriggersFile);
                    XPathNavigator nav = doc.CreateNavigator();
                    XPathNodeIterator iterDebug = nav.Select($"Triggers/Trigger[@Name='{trigger}']/Principal");
                    foreach (XPathNavigator iterCmd in iterDebug) // iterate thru all principals in TriggersTarget.xml
                    {
                        string name = iterCmd.GetAttribute("Name", String.Empty);
                        if (String.Equals(prncpl, name))
                        {
                            return true;
                        }
                    }
                }
            }

            catch (XPathException ecx)
            {
                Log($"XPathException in AcDebug.entryInTrigTargetsFile caught and logged.{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception exc)
            {
                String msg = String.Format("Exception in AcDebug.entryInTrigTargetsFile caught and logged.{0}Trigger: {1}{0}Principal: {2}{0}{3}",
                    Environment.NewLine, trigger, prncpl, exc.Message);
                Log(msg);
            }

            return false;
        }

        /// <summary>
        /// Write the \e message text to STDOUT, to [weekly log files](@ref AcUtils#AcDebug#initAcLogging) located 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs</tt>, and to \c trigger.log in the AccuRev server's 
        /// <tt>..storage\\site_slice\\logs</tt> folder in the case of triggers.
        /// </summary>
        /// <param name="message">The text to be logged.</param>
        /// <param name="formatting">\e true to include the date, time and newline formatting in the log file. 
        /// \e false for no formatting.</param>
        /*! \sa Log(string, string, bool) */
        public static void Log(string message, bool formatting = true)
        {
            lock (_locker)
            {
                Console.WriteLine(message); // to STDOUT and trigger.log
                if (_log != null)
                {
                    if (formatting)
                    {
                        // include date/time in our log file and separate each log entry with an empty line
                        string logmsg = String.Format("{1}{0}{2}{0}", Environment.NewLine, DateTime.Now.ToString(), message);
                        _log.WriteLine(logmsg);
                    }
                    else
                        _log.WriteLine(message);
                }
            }
        }

        /// <summary>
        /// Used by triggers, this method overload includes all functionality of Log(string, bool) and adds overwriting 
        /// the trigger's \e xmlParamFile with the \e message text so it is displayed in the console or error dialog 
        /// given to the user on trigger failure.
        /// </summary>
        /// <remarks>
        /// Results from AccuRev commands issued on the command line are sent to STDOUT for both success and failure. 
        /// For the AccuRev GUI, results are discarded if the program return value is <em>zero (0)</em> (\e Success). 
        /// For triggers that return a <em>non-zero</em> program return value (\e Failure), the \e message text will 
        /// display in the console or error dialog provided the \e xmlParamFile sent by AccuRev was overwritten with it. 
        /// Otherwise, the raw [XML param data](https://www.microfocus.com/documentation/accurev/71/WebHelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_Admin/trig_param_file.html) 
        /// will display which is not user-friendly.
        /// </remarks>
        /// <param name="message">The text to be logged and displayed to the user on trigger failure.</param>
        /// <param name="xmlParamFile">The XML trigger param file sent by AccuRev to the trigger.</param>
        /// <param name="formatting">\e true to include the date, time and newline formatting in the log file. \e false for no formatting.</param>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<trig_name\>-YYYY-MM-DD.log</tt> on failure to overwrite the XML param file.</exception>
        /*! \sa [AcUtils.ParamFileCopy](@ref AcUtils#ParamFileCopy) */
        public static void Log(string message, string xmlParamFile, bool formatting = true)
        {
            Log(message, formatting);
            try
            {
                // Overwrite the XML param file to display our custom message text in the error dialog given
                // to the user. Otherwise, the contents of the XML param file will display (yuck).
                using (StreamWriter writer = new StreamWriter(xmlParamFile, false)) // false to overwrite file
                {
                    writer.WriteLine(message);
                }
            }

            catch (Exception e) // IOException, DirectoryNotFoundException, PathTooLongException, SecurityException... others
            {
                String err = String.Format("Unable to overwrite XML param file {1}{0}{2}",
                    Environment.NewLine, xmlParamFile, e.Message);
                Log(err);
            }
        }

        /// <summary>
        /// Initialize application logging support, for general purpose or error messages, in conjunction 
        /// with a client application's <tt>\<prog_name\>.exe.config</tt> file and Log(string, bool).
        /// </summary>
        /// <remarks>Creates weekly <a href="https://msdn.microsoft.com/en-us/library/microsoft.visualbasic.logging.filelogtracelistener(v=vs.110).aspx">log files</a> 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs</tt> in the format <tt>\<prog_name\>-YYYY-MM-DD.log</tt> with the first day 
        /// of the current week in the file name. The \c AcTools\\Logs folder is created if not already there. Log files can grow 
        /// to a maximum of 80MB in size. At least 2.3GB free space must exist for logging to continue. Additional log files are 
        /// created with an iteration part (blue circle) should a write conflict occur:</remarks>
        /*! \htmlonly
              <img src="LogFileIteration.png"/>
            \endhtmlonly
        */
        /// <returns>\e true if logging support successfully initialized, \e false on error.</returns>
        /// <exception cref="Exception">caught on failure to handle a range of exceptions with error sent to STDOUT.</exception>
        /*! \par Example usage: */
        /*! \code
            // Init our logging support so we can log errors.
            string error;
            if (!AcDebug.initAcLogging())
            {
                MessageBox.Show(error, "PromoHist", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            \endcode */
        /*! \par XML that must exist in \<\e prog_name\>.exe.config. Change \e source \e name attribute to \<\e prog_name\>. */
        /*! \code
              <system.diagnostics>
                <assert assertuienabled="false"/>
                <sources>
                  <source name="PromoHist">  <!-- name attribute value must be program executable's root name -->
                    <listeners>
                      <remove name="Default" />
                      <add name="AcLog" type="Microsoft.VisualBasic.Logging.FileLogTraceListener, Microsoft.VisualBasic, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL" />
                    </listeners>
                  </source>
                </sources>
              </system.diagnostics>
            \endcode */
        public static bool initAcLogging()
        {
            bool ret = true; // assume success
            try
            {
                // Get root name of our executable for initializing our TraceSource object below.
                // This name must be specified in the application's .config file for logging to work.
                string exeRootName = String.Empty;
                using (Process currentProcess = Process.GetCurrentProcess())
                {
                    ProcessModule pm = currentProcess.MainModule;
                    string module = pm.ModuleName;
                    exeRootName = Path.GetFileNameWithoutExtension(module);
                }

                string localappdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logsFolder = Path.Combine(localappdata, "AcTools\\Logs");
                if (!Directory.Exists(logsFolder))
                    Directory.CreateDirectory(logsFolder);

                TraceSource ts = new TraceSource(exeRootName);
                _log = (FileLogTraceListener)ts.Listeners["AcLog"];
                _log.Location = LogFileLocation.Custom;
                _log.CustomLocation = logsFolder;
                // our log file name begins with this name
                _log.BaseFileName = exeRootName;
                // allow log files to grow up to 40 megabytes in size
                //_log.MaxFileSize = 41943040;
                // allow log files to grow up to 80 megabytes in size
                _log.MaxFileSize = 83886080;
                // at least 2.3GB free disk space must exist for logging to continue
                _log.ReserveDiskSpace = 2500000000;
                // use the first day of the current week in the log file name
                _log.LogFileCreationSchedule = LogFileCreationScheduleOption.Weekly;
                // true to make sure we capture data in the event of an exception
                _log.AutoFlush = true;
            }

            catch (Exception exc)
            {
                Console.WriteLine($"Exception caught in AcDebug.initAcLogging{Environment.NewLine}{exc.Message}");
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Get the [log file](@ref AcUtils#AcDebug#initAcLogging) full path name.
        /// </summary>
        /// <returns>Full path to the log file if it exists, otherwise \e null.</returns>
        public static string getLogFile()
        {
            // referencing FullLogFileName property will create a zero-byte log file if it doesn't exist
            return (_log != null) ? _log.FullLogFileName : null;
        }

        /// <summary>
        /// Ensure that an unhandled exception gets logged to <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\\<prog_name\>-YYYY-MM-DD.log</tt> 
        /// before the system default handler terminates the application.
        /// </summary>
        /*! \code
            // save an unhandled exception in log file before program terminates
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(AcDebug.unhandledException);
           \endcode */
        /*! \sa <a href="https://msdn.microsoft.com/query/dev12.query?appId=Dev12IDEF1&l=EN-US&k=k(System.AppDomain.UnhandledException">AppDomain.UnhandledException Event</a> */
        public static void unhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            AcDebug.Log(e.ToString(), false);
        }

        /// <summary>
        /// Centralized error handler.
        /// </summary>
        /// <param name="sendingProcess">The spawned process.</param>
        /// <param name="errLine">The error message sent to this method by the spawned process.</param>
        public static void errorDataHandler(object sendingProcess, DataReceivedEventArgs errLine)
        {
            if (!String.IsNullOrEmpty(errLine.Data))
            {
                string errline = errLine.Data.Trim();
                // Report error if something other than "not in a directory associated with a workspace"
                if (errline.Length > 0 &&
                    !String.Equals("You are not in a directory associated with a workspace", errline))
                {
                    AcDebug.Log(errline);
                }
            }
        }
    }
}
