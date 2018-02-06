/* Copyright (C) 2017-2018 Verizon. All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License. */

// Required references: AcUtils.dll, Microsoft.VisualBasic, System, System.configuration, System.Xml.Linq
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualBasic.Logging;
using AcUtils;

namespace EvilTwins
{
    // Compare two AccuRev elements based on their depot-relative path (case-insensitive) and element ID.
    public struct TwinEqualityComparer : IEqualityComparer<Tuple<string, int>> // [element, EID]
    {
        public bool Equals(Tuple<string, int> x, Tuple<string, int> y)
        {
            if (Object.ReferenceEquals(x, y)) // are the compared objects the same object?
                return true;
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null)) // are either of them null?
                return false;

            bool path = String.Equals(x.Item1.ToUpperInvariant(), y.Item1.ToUpperInvariant());
            bool eid = (x.Item2 == y.Item2);
            return (path && eid);
        }

        public int GetHashCode(Tuple<string, int> elem)
        {
            int path = elem.Item1.ToUpperInvariant().GetHashCode();
            return path ^ elem.Item2; // path ^ EID
        }
    }

    class Program
    {
        #region class variables
        private static string _twinsExcludeFile; // optional TwinsExcludeFile specified in EvilTwins.exe.config
        private static List<String> _excludeList; // list of elements from TwinsExcludeFile in depot-relative format
        private static DepotsCollection _selDepots; // list of depots from EvilTwins.exe.config to query for evil twins
        private static Dictionary<AcDepot, HashSet<Tuple<string, int>>> _map; // [element, EID] for all elements in all dynamic streams in depot
        private static TwinEqualityComparer _tcompare; // help determine if two elements are evil
        private static FileLogTraceListener _tl; // logging support for evil twins that are found
        private static readonly object _locker = new object(); // token for lock keyword scope
        #endregion

        // Returns zero (0) if program ran successfully, otherwise 
        // one (1) in the event of an exception or program initialization failure.
        static int Main()
        {
            // general program startup initialization
            if (!init()) return 1;

            // if a TwinsExcludeFile was specified in EvilTwins.exe.config, 
            // validate that it exists and retrieve its content
            if (!String.IsNullOrEmpty(_twinsExcludeFile))
            {
                if (!File.Exists(_twinsExcludeFile))
                {
                    AcDebug.Log($"TwinsExcludeFile {_twinsExcludeFile} specified in EvilTwins.exe.config not found");
                    return 1;
                }
                else
                    _excludeList = getTwinsExcludeList();
            }

            // all stream types in order to include workspace streams, include hidden (removed) streams
            AcDepots depots = new AcDepots(dynamicOnly: false, includeHidden: true);
            Task<bool> dini = depots.initAsync(_selDepots);
            if (!dini.Result) return 1;

            _tcompare = new TwinEqualityComparer();
            _map = new Dictionary<AcDepot, HashSet<Tuple<string, int>>>();
            List<Task<bool>> tasks = new List<Task<bool>>(depots.Count);
            foreach (AcDepot depot in depots)
                tasks.Add(initMapAsync(depot));

            Task<bool[]> arr = Task.WhenAll(tasks); // finish running stat commands and initialization in parallel
            if (arr == null || arr.Result.Any(n => n == false)) return 1; // check log file

            Task<bool> r = reportAsync();
            return (r.Result) ? 0 : 1;
        }

        // Initialize our dictionary class variable with [element, EID] for all elements in all dynamic streams in depot. 
        // AcUtilsException caught and logged in %LOCALAPPDATA%\AcTools\Logs\EvilTwins-YYYY-MM-DD.log on stat command failure. 
        // Exception caught and logged in same for a range of exceptions.
        private static async Task<bool> initMapAsync(AcDepot depot)
        {
            bool ret = false; // assume failure
            try
            {
                IEnumerable<AcStream> filter = from n in depot.Streams
                                               where n.IsDynamic && !n.Hidden
                                               select n;
                List<Task<AcResult>> tasks = new List<Task<AcResult>>(filter.Count());
                foreach (AcStream stream in filter)
                    tasks.Add(AcCommand.runAsync($@"stat -a -s ""{stream}"" -fx")); // -a for all elements in stream

                AcResult[] arr = await Task.WhenAll(tasks); // finish running stat commands in parallel
                if (arr != null && arr.All(n => n.RetVal == 0)) // true if all initialized successfully
                {
                    HashSet<Tuple<string, int>> hset = new HashSet<Tuple<string, int>>(_tcompare);
                    foreach (AcResult r in arr)
                    {
                        // if empty the stream has an ACL that is preventing us from reading it or some other error occurred
                        if (r == null || String.IsNullOrEmpty(r.CmdResult)) continue;
                        XElement xml = XElement.Parse(r.CmdResult);
                        foreach (XElement e in xml.Elements("element"))
                        {
                            string path = (string)e.Attribute("location");
                            int eid = (int)e.Attribute("id");
                            hset.Add(new Tuple<string, int>(path, eid));
                        }
                    }

                    lock (_locker) { _map.Add(depot, hset); }
                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in Program.initMapAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.initMapAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // Report evil twins if found. Assumes the initMapAsync method has been called. Exception caught 
        // and logged in %LOCALAPPDATA%\AcTools\Logs\EvilTwins-YYYY-MM-DD.log on operation failure.
        private static async Task<bool> reportAsync()
        {
            bool ret = false; // assume failure
            try
            {
                foreach (KeyValuePair<AcDepot, HashSet<Tuple<string, int>>> pair in _map.OrderBy(n => n.Key)) // order by AcDepot
                {
                    AcDepot depot = pair.Key; // depot
                    HashSet<Tuple<string, int>> hset = pair.Value; // [element, EID] from all dynamic streams in depot
                    // from our hashset create a collection of elements mapped to their EID's
                    Lookup<string, Tuple<string, int>> col = (Lookup<string, Tuple<string, int>>)hset.ToLookup(n => n.Item1);
                    // where more than one EID exists for the element, order by element
                    foreach (var ii in col.Where(n => n.Count() > 1).OrderBy(n => n.Key))
                    {
                        string element = ii.Key;
                        if (_excludeList != null && _excludeList.Contains(element))
                            continue; // ignore if in TwinsExcludeFile

                        log(element);
                        IEnumerable<AcStream> filter = from n in depot.Streams
                                                       where n.IsDynamic && !n.Hidden
                                                       select n;
                        List<Task<XElement>> tasks = new List<Task<XElement>>(filter.Count());
                        foreach (AcStream stream in filter)
                            tasks.Add(getElementInfoAsync(stream, element));

                        XElement[] arr = await Task.WhenAll(tasks); // finish running stat commands in parallel
                        if (arr != null && arr.All(n => n != null)) // true if all ran successfully
                        {
                            foreach (Tuple<string, int> jj in ii.OrderBy(n => n.Item2)) // order twins by EID
                            {
                                int eid = jj.Item2;
                                log($"\tEID: {eid} on {DateTime.Now.ToString("ddd MMM d h:mm tt")}");
                                // C# language short-circuit: the test for id equals eid isn't evaluated if status equals "(no such elem)", 
                                // otherwise an exception would be thrown since the id attribute doesn't exist in this case
                                foreach (XElement e in arr.Where(n => (string)n.Attribute("status") != "(no such elem)" &&
                                    (int)n.Attribute("id") == eid).OrderBy(n => n.Annotation<AcStream>()))
                                {
                                    log($"\t\t{e.Annotation<AcStream>()} {(string)e.Attribute("status")}");
                                    string namedVersion = (string)e.Attribute("namedVersion"); // virtual stream name and version number
                                    string temp = (string)e.Attribute("Real");
                                    string[] real = temp.Split('\\'); // workspace stream and version numbers
                                    AcStream wkspace = depot.getStream(int.Parse(real[0])); // workspace stream
                                    ElementType elemType = e.acxType("elemType");
                                    string twin;
                                    if ((long?)e.Attribute("size") != null)
                                        twin = $"\t\t\tSize: {(long)e.Attribute("size")}, ModTime: {e.acxTime("modTime")} {{{elemType}}}" +
                                            $"{Environment.NewLine}\t\t\tReal: {wkspace}\\{real[1]}, Virtual: {namedVersion}";
                                    else // a folder or link
                                        twin = $"\t\t\tReal: {wkspace}\\{real[1]}, Virtual: {namedVersion} {{{elemType}}}";

                                    log(twin);
                                }

                                log("");
                            }
                        }
                    }
                }

                ret = true; // operation succeeded
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.reportAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // Returns the attributes for the element in stream if the query succeeded, otherwise null. 
        // AcUtilsException caught and logged in %LOCALAPPDATA%\AcTools\Logs\EvilTwins-YYYY-MM-DD.log on stat command failure. 
        // Exception caught and logged in same for a range of exceptions.
        private static async Task<XElement> getElementInfoAsync(AcStream stream, string element)
        {
            XElement e = null;
            try
            {
                AcResult r = await AcCommand.runAsync($@"stat -fx -s ""{stream}"" ""{element}""");
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    e = xml.Element("element");
                    e.AddAnnotation(stream); // add stream since it's not in the XML
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in Program.getElementInfoAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.getElementInfoAsync{Environment.NewLine}{ecx.Message}");
            }

            return e;
        }

        // Returns the list of elements in TwinsExcludeFile from EvilTwins.exe.config with elements that can be ignored. 
        // Assumes caller has determined that the TwinsExcludeFile specified in EvilTwins.exe.config exists. 
        // Exception caught and logged in %LOCALAPPDATA%\AcTools\Logs\EvilTwins-YYYY-MM-DD.log on operation failure.
        private static List<String> getTwinsExcludeList()
        {
            List<String> exclude = new List<String>();
            FileStream fs = null;
            try
            {
                // constructor arguments that avoid an exception thrown when file is in use by another process
                fs = new FileStream(_twinsExcludeFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (StreamReader sr = new StreamReader(fs))
                {
                    String line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string temp = line.Trim();
                        if (temp.Length == 0 || temp.StartsWith("#"))
                            continue; // this is an empty line or a comment
                        exclude.Add(temp);
                    }
                }
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.getTwinsExcludeList{Environment.NewLine}{ecx.Message}");
            }

            finally // avoids CA2202: Do not dispose objects multiple times
            {
                if (fs != null) fs.Dispose();
            }

            return exclude;
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
                AcDebug.Log($"Not logged into AccuRev.{Environment.NewLine}Please login and try again.");
                return false;
            }

            // initialize our class variables from EvilTwins.exe.config
            if (!initAppConfigData()) return false;
            // initialize our logging support to log evil twins found
            if (!initEvilTwinsLogging()) return false;

            return true;
        }

        // Initialize our class variables from EvilTwins.exe.config.
        // Returns true if variables were successfully initialized, false otherwise.
        // ConfigurationErrorsException caught and logged in 
        // %LOCALAPPDATA%\AcTools\Logs\EvilTwins-YYYY-MM-DD.log on initialization failure.
        private static bool initAppConfigData()
        {
            bool ret = true; // assume success
            try
            {
                _twinsExcludeFile = AcQuery.getAppConfigSetting<string>("TwinsExcludeFile").Trim();
                DepotsSection depotsConfigSection = ConfigurationManager.GetSection("Depots") as DepotsSection;
                if (depotsConfigSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating DepotsSection");
                    ret = false;
                }
                else
                    _selDepots = depotsConfigSection.Depots;
            }

            catch (ConfigurationErrorsException exc)
            {
                Process currentProcess = Process.GetCurrentProcess();
                ProcessModule pm = currentProcess.MainModule;
                AcDebug.Log($"Invalid data in {pm.ModuleName}.config{Environment.NewLine}{exc.Message}");
                ret = false;
            }

            return ret;
        }

        // Initialize our logging support for evil twins found. Output is sent to the daily log file 
        // EvilTwinsFound-YYYY-MM-DD.log created (or updated) in the same folder where EvilTwins.exe resides.
        // Returns true if logging was successfully initialized, false otherwise. Exception caught and logged in 
        // %LOCALAPPDATA%\AcTools\Logs\EvilTwins-YYYY-MM-DD.log on initialization failure.
        private static bool initEvilTwinsLogging()
        {
            bool ret = false; // assume failure
            try
            {
                TraceSource ts = new TraceSource("EvilTwins"); // this program name
                _tl = (FileLogTraceListener)ts.Listeners["EvilTwinsFound"];
                _tl.Location = LogFileLocation.ExecutableDirectory; // create the log file in the same folder as our executable
                _tl.BaseFileName = "EvilTwinsFound"; // our log file name begins with this name
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
                AcDebug.Log($"Exception caught and logged in Program.initEvilTwinsLogging{Environment.NewLine}{exc.Message}");
            }

            return ret;
        }

        // Write text to the EvilTwinsFound-YYYY-MM-DD.log daily log file located in the same folder where EvilTwins.exe resides.
        private static void log(string text)
        {
            if (_tl != null)
                _tl.WriteLine(text);
        }
    }
}
