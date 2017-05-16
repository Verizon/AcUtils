﻿/* Copyright (C) 2017 Verizon. All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License. */

// Required references: AcUtils.dll, System, System.configuration, System.Xml.Linq, Microsoft.VisualBasic
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualBasic.Logging;
using AcUtils;

namespace Stranded
{
    class Program
    {
        #region class variables
        private static DepotsCollection _excludeList; // list of depots from Stranded.exe.config that should be ignored
        // map each stream found to have stranded elements with its list of status types and the count of each type
        private static SortedList<AcStream, SortedList<string, int>> _map = new SortedList<AcStream, SortedList<string, int>>();
        private static readonly object _locker = new object();
        private static int _totalStranded;
        private static FileLogTraceListener _tl; // logging support for stranded files found
        #endregion

        static int Main()
        {
            // general program startup initialization
            if (!init()) return 1;
#if DEBUG
            Stopwatch stopWatch = new Stopwatch(); // time the operation
            stopWatch.Start();
#endif
            Task<bool> sini = getStrandedAsync();
            bool ret = sini.Result;
            if (ret)
            {
                report();
                string endMsg = String.Format("Total stranded elements: {0}", _totalStranded);
                log(endMsg);
#if DEBUG
                AcDuration ts = stopWatch.Elapsed;
                string execTime = String.Format("{0} to complete execution", ts.ToString());
                log(execTime);
#endif
            }

            return (ret) ? 0 : 1;
        }

        // Get the stranded elements along with the list of status types found and the total count for each type. 
        // Returns true if the operation completed successfully, false otherwise. 
        // Exception caught and logged in %LOCALAPPDATA%\AcTools\Logs\Stranded-YYYY-MM-DD.log on failure to 
        // handle a range of exceptions.
        private static async Task<bool> getStrandedAsync()
        {
            bool ret = false; // assume failure
            try
            {
                AcDepots depots = new AcDepots(true); // true for dynamic streams only
                if (!(await depots.initAsync())) return false;

                List<Task<bool>> tasks = new List<Task<bool>>();
                // select all depots except those from Stranded.exe.config that should be ignored
                IEnumerable<AcDepot> filter = from d in depots
                                              where !_excludeList.OfType<DepotElement>().Any(de => de.Depot == d.Name)
                                              select d;

                foreach (AcDepot depot in filter)
                    foreach (AcStream stream in depot.Streams)
                        tasks.Add(runStatCommandAsync(stream));

                bool[] arr = await Task.WhenAll(tasks); // finish running stat commands in parallel
                ret = (arr != null && arr.All(n => n == true)); // true if all succeeded or if no tasks were run
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in Program.getStrandedAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return (ret) ? true : false;
        }

        // Run the AccuRev stat command for the stream param and initialize our class dictionary variable _map 
        // with the results. Returns true if the operation succeeded, false otherwise. AcUtilsException caught 
        // and logged in %LOCALAPPDATA%\AcTools\Logs\Stranded-YYYY-MM-DD.log on stat command failure.
        private static async Task<bool> runStatCommandAsync(AcStream stream)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = String.Format(@"stat -fx -s ""{0}"" -i", stream);
                AcResult result = await AcCommand.runAsync(cmd);
                if (result != null && result.RetVal == 0)
                {
                    XElement xml = XElement.Parse(result.CmdResult);
                    int num = xml.Elements("element").Count();
                    if (num > 0)
                        lock (_locker) { _map.Add(stream, initVal(xml)); }
                }

                ret = true;
            }

            catch (AcUtilsException exc)
            {
                string msg = String.Format("AcUtilsException caught and logged in Program.runStatCommandAsync{0}{1}", 
                    Environment.NewLine, exc.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        // Helper function for the runStatCommandAsync method. Used to initialize the value portion of class dictionary 
        // variable _map that associates each stream with their stranded elements info; the list of status types found 
        // and their count.
        private static SortedList<string, int> initVal(XElement xml)
        {
            SortedList<string, int> status = new SortedList<string, int>();
            foreach (XElement e in xml.Elements("element"))
            {
                int sc; // status type count
                string sval = (string)e.Attribute("status");
                if (status.TryGetValue(sval, out sc))
                {
                    sc++; // another instance of this status type was found, so increment the count
                    status[sval] = sc;
                }
                else // a new type so we add it to our dictionary object
                    status[sval] = 1;

                _totalStranded++; // used for grand total of stranded elements found
            }

            return status;
        }

        // Write the stream names and the status types found per stream along with 
        // the count per type to StrandedFound-YYYY-MM-DD.log.
        private static void report()
        {
            foreach (KeyValuePair<AcStream, SortedList<string, int>> ii in _map)
            {
                log(ii.Key.Name); // stream name
                SortedList<string, int> sc = ii.Value; // status type and count
                foreach (KeyValuePair<string, int> jj in sc)
                {
                    string line = String.Format("{0} {{{1}}}", jj.Key, jj.Value);
                    log(line);
                }

                log("");
            }
        }

        // General program startup initialization routines.
        // Returns true if initialization was successful, false otherwise.
        private static bool init()
        {
            // initialize logging support for reporting program errors
            if (!AcDebug.initAcLogging())
            {
                Console.WriteLine("Logging support initialization failed.");
                return false;
            }

            // save an unhandled exception in log file before program terminates
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(AcDebug.unhandledException);

            // ensure we're logged into AccuRev
            Task<string> prncpl = AcQuery.getPrincipalAsync();
            if (String.IsNullOrEmpty(prncpl.Result))
            {
                string msg = String.Format("Not logged into AccuRev.{0}Please login and try again.",
                    Environment.NewLine);
                AcDebug.Log(msg);
                return false;
            }

            // initialize our class variables from Stranded.exe.config
            if (!initAppConfigData()) return false;
            // initialize our logging support to log stranded elements found
            if (!initStrandedFoundLogging()) return false;

            return true;
        }

        // Initialize our class variables from Stranded.exe.config.
        // Returns true if variables were successfully initialized, false otherwise.
        // ConfigurationErrorsException caught and logged in 
        // %LOCALAPPDATA%\AcTools\Logs\Stranded-YYYY-MM-DD.log on initialization failure.
        private static bool initAppConfigData()
        {
            bool ret = false; // assume failure
            try
            {
                DepotsSection depotsConfigSection = ConfigurationManager.GetSection("Depots") as DepotsSection;
                if (depotsConfigSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating DepotsSection");
                    ret = false;
                }
                else
                    _excludeList = depotsConfigSection.Depots;

                ret = true;
            }

            catch (ConfigurationErrorsException exc)
            {
                Process currentProcess = Process.GetCurrentProcess();
                ProcessModule pm = currentProcess.MainModule;
                string exeFile = pm.ModuleName;
                string msg = String.Format("Invalid data in {1}.config{0}{2}",
                    Environment.NewLine, exeFile, exc.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        // Initialize our logging support for stranded elements found. Output is sent to the daily log file 
        // StrandedFound-YYYY-MM-DD.log created (or updated) in the same folder where \c Stranded.exe resides. 
        // Returns true if logging was successfully initialized, false otherwise. Exception caught and logged in 
        // %LOCALAPPDATA%\AcTools\Logs\Stranded-YYYY-MM-DD.log on initialization failure.
        private static bool initStrandedFoundLogging()
        {
            bool ret = false; // assume failure
            try
            {
                TraceSource ts = new TraceSource("Stranded"); // this program name
                _tl = (FileLogTraceListener)ts.Listeners["StrandedFound"];
                _tl.Location = LogFileLocation.ExecutableDirectory; // create the log file in the same folder as our executable
                _tl.BaseFileName = "StrandedFound"; // our log file name begins with this name
                _tl.MaxFileSize = 83886080; // log file can grow to a maximum of 80 megabytes in size
                _tl.ReserveDiskSpace = 2500000000; // at least 2.3GB free disk space must exist for logging to continue
                // append -YYYY-MM-DD to the log file name
                // additional runs of this app in the same day append to this file (by default)
                _tl.LogFileCreationSchedule = LogFileCreationScheduleOption.Daily;
                _tl.AutoFlush = true; // true to make sure we capture data in the event of an exception
                ret = true;
            }

            catch (Exception exc)
            {
                string msg = String.Format("Exception in Program.initStrandedFoundLogging caught and logged.{0}{1}",
                    Environment.NewLine, exc.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        // Write text to the StrandedFound-YYYY-MM-DD.log daily log file located in the same folder as the executable.
        private static void log(string text)
        {
            if (_tl != null)
                _tl.WriteLine(text);
        }
    }
}