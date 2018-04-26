/* Copyright (C) 2018 Verizon. All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License. */

// Required references: AcUtils.dll, System, System.configuration, System.Xml.Linq
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcUtils;

namespace XLinked
{
    class Program
    {
        #region class variables
        private static AcDepots _depots;
        private static ElementType[] _etypes;
        private static Dictionary<AcDepot, HashSet<XElement>> _map = new Dictionary<AcDepot, HashSet<XElement>>();
        private static XNodeEqualityComparer _comparer = new XNodeEqualityComparer();
        private static readonly object _locker = new object(); // token for lock keyword scope
        #endregion

        static int Main(string[] args)
        {
            if (!init()) return 1;
            Task<bool> xini = xlinkedAsync();
            if (!xini.Result) return 1;
            return report() ? 0 : 1;
        }

        private static async Task<bool> xlinkedAsync()
        {
            List<Task<bool>> tasks = new List<Task<bool>>(_depots.Count);
            foreach (AcDepot depot in _depots)
                tasks.Add(initAsync(depot));

            bool[] arr = await Task.WhenAll(tasks); // finish running all in parallel
            return (arr != null && arr.All(n => n == true)); // true if all succeeded
        }

        // Run the stat command for all dynamic streams in depot and initialize our dictionary class variable 
        // with the xlinked elements found that have a type listed in ElementTypes from XLinked.exe.config. 
        // Returns true if the operation succeeded, false otherwise. AcUtilsException caught and logged in 
        // %LOCALAPPDATA%\AcTools\Logs\XLinked-YYYY-MM-DD.log on stat command failure. Exception caught and 
        // logged in same for a range of exceptions.
        private static async Task<bool> initAsync(AcDepot depot)
        {
            bool ret = false; // assume failure
            try
            {
                int num = depot.Streams.Count();
                List<Task<AcResult>> tasks = new List<Task<AcResult>>(num);
                foreach (AcStream stream in depot.Streams)
                    // -k: Display the element type (that is, data type) of this version
                    // -v: Display the target of an element link or symbolic link
                    tasks.Add(AcCommand.runAsync($@"stat -s ""{stream}"" -a -fkvx"));

                HashSet<XElement> hset = new HashSet<XElement>(_comparer);
                while (tasks.Count > 0)
                {
                    Task<AcResult> r = await Task.WhenAny(tasks);
                    tasks.Remove(r);
                    if (r == null || r.Result.RetVal != 0) return false;
                    XElement xml = XElement.Parse(r.Result.CmdResult);
                    foreach (XElement e in xml.Elements("element")
                        // attribute xlinked="true" exists only when status includes (xlinked), 
                        // otherwise it isn't there (i.e. there never is an xlinked="false" in the XML)
                        .Where(n => (string)n.Attribute("xlinked") != null &&
                            _etypes.Any(t => t == n.acxType("elemType"))))
                    {
                        hset.Add(e);
                    }
                }

                lock (_locker) { _map.Add(depot, hset); }
                ret = true;
            }

            catch (AcUtilsException exc)
            {
                AcDebug.Log($"AcUtilsException caught and logged in Program.initAsync{Environment.NewLine}{exc.Message}");
            }
            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.initAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // Send program results to the console. Returns true if the operation succeeded, false otherwise.
        // Exception caught and logged in %LOCALAPPDATA%\AcTools\Logs\XLinked-YYYY-MM-DD.log for a range of exceptions.
        private static bool report()
        {
            bool ret = false; // assume failure
            try
            {
                foreach (KeyValuePair<AcDepot, HashSet<XElement>> pair in _map.OrderBy(n => n.Key)) // order by AcDepot
                {
                    Console.WriteLine(pair.Key);  // depot
                    foreach (XElement e in pair.Value // unique list of elements found throughout depot
                        // order by stream name then element's depot-relative path
                        .OrderBy(n => ((string)n.Attribute("namedVersion"))
                            .Substring(0, ((string)n.Attribute("namedVersion")).IndexOf('\\')))
                        .ThenBy(n => (string)n.Attribute("location")))
                    {
                        string mtime = e.acxTime("modTime") == null ? String.Empty :
                            "Last modified: " + e.acxTime("modTime").ToString() + " ";
                        Console.WriteLine($"\tEID: {(string)e.Attribute("id")} {{{(string)e.Attribute("elemType")}}} " +
                            $"{(string)e.Attribute("location")}{Environment.NewLine}" +
                            $"\t\t{mtime}{(string)e.Attribute("namedVersion")} {(string)e.Attribute("status")}");
                    }
                }

                ret = true;
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.report{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // General program startup initialization.
        private static bool init()
        {
            // initialize our logging support so we can log errors
            if (!AcDebug.initAcLogging())
            {
                Console.WriteLine("Logging support initialization failed.");
                return false;
            }

            // in the event of an unhandled exception, save it to our log file before the program terminates
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(AcDebug.unhandledException);

            // ensure we're logged into AccuRev
            Task<string> prncpl = AcQuery.getPrincipalAsync();
            if (String.IsNullOrEmpty(prncpl.Result))
            {
                AcDebug.Log($"Not logged into AccuRev.{Environment.NewLine}Please login and try again.");
                return false;
            }

            // initialize our ElementType array class variable from XLinked.exe.config
            if (!initAppConfigData()) return false;

            _depots = new AcDepots(dynamicOnly: true); // dynamic streams only
            Task<bool> dini = _depots.initAsync();
            if (!dini.Result)
            {
                AcDebug.Log($"Depots list initialization failed. See log file:{Environment.NewLine}" +
                    $"{AcDebug.getLogFile()}");
                return false;
            }

            return true;
        }

        // Initialize our ElementType array class variable with values from XLinked.exe.config. 
        // Returns true if successfully read and initialized, false otherwise. ConfigurationErrorsException 
        // caught and logged in %LOCALAPPDATA%\AcTools\Logs\XLinked-YYYY-MM-DD.log on initialization failure.
        private static bool initAppConfigData()
        {
            bool ret = false; // assume failure
            try
            {
                string[] arr = AcQuery.getAppConfigSetting<string>("ElementTypes")
                    .Split(',').Select(s => s.Trim()).ToArray();
                _etypes = Array.ConvertAll(arr, new Converter<string, ElementType>(n =>
                    (ElementType)Enum.Parse(typeof(ElementType), n)));
                ret = true;
            }

            catch (ConfigurationErrorsException exc)
            {
                Process currentProcess = Process.GetCurrentProcess();
                ProcessModule pm = currentProcess.MainModule;
                AcDebug.Log($"Invalid data in {pm.ModuleName}.config{Environment.NewLine}{exc.Message}");
            }

            return ret;
        }
    }
}
