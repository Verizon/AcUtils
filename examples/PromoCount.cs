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

// Required references: AcUtils.dll, Microsoft.VisualBasic, System, System.Configuration, System.Xml.Linq
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualBasic.Logging;
using AcUtils;

namespace PromoCount
{
    class Program
    {
        #region class variables
        private static DomainCollection _domains;
        private static DepotsCollection _selDepots;
        private static StreamsCollection _selStreams;
        private static string _startTime;
        private static string _endTime;
        private static AcUsers _users;
        private static AcDepots _depots;
        private static readonly object _locker = new object(); // token for lock keyword scope
        private static FileLogTraceListener _tl; // for logging program results
        #endregion

        static int Main()
        {
            if (!init()) return 1;
            Task<bool> r = promoCountAsync();
            return (r.Result) ? 0 : 1;
        }

        // Run the AccuRev hist command for all streams in PromoCount.exe.config, generate the results and send it 
        // to the daily log file PromoCountResults-YYYY-MM-DD.log created (or updated) in the same folder where 
        // PromoCount.exe resides. Returns true if the operation succeeded, false otherwise. AcUtilsException 
        // caught and logged in %LOCALAPPDATA%\AcTools\Logs\PromoCount-YYYY-MM-DD.log on hist command failure. 
        // Exception caught and logged in same for a range of exceptions.
        private static async Task<bool> promoCountAsync()
        {
            bool ret = false; // assume failure
            try
            {
                Dictionary<AcStream, Task<AcResult>> map = new Dictionary<AcStream, Task<AcResult>>(_selStreams.Count);
                Func<AcStream, Task<AcResult>> run = (stream) =>
                {
                    // start-end times reversed as workaround for AccuRev issue 15780
                    Task<AcResult> result = AcCommand.runAsync(
                        $@"hist -fx -k promote -s ""{stream}"" -t ""{_endTime} - {_startTime}""");
                    lock (_locker) { map.Add(stream, result); }
                    return result;
                };

                var tasks = from s in _depots.SelectMany(d => d.Streams)
                            where _selStreams.OfType<StreamElement>().Any(se => se.Stream == s.Name)
                            select run(s);

                AcResult[] arr = await Task.WhenAll(tasks); // finish running hist commands in parallel
                if (arr == null || arr.Any(r => r.RetVal != 0)) return false;

                log($"Promotions to select streams from {_startTime} to {_endTime}.{Environment.NewLine}");
                int tgrandtot = 0; int vgrandtot = 0;
                foreach (var ii in map.OrderBy(n => n.Key))
                {
                    log($"{ii.Key} {{{$"promotions\\versions"}}}:"); // key is stream
                    AcResult r = ii.Value.Result;
                    XElement xml = XElement.Parse(r.CmdResult);
                    ILookup<string, XElement> look = xml.Elements("transaction")
                        .ToLookup(n => (string)n.Attribute("user"), n => n);
                    int tsubtot = 0; int vsubtot = 0;
                    foreach (var jj in look.OrderBy(n => _users.getUser(n.Key)))
                    {
                        AcUser user = _users.getUser(jj.Key);
                        int tnum = jj.Count();
                        int vnum = jj.Elements("version").Count();
                        string val = $"{{{tnum}\\{vnum}}}";
                        log($"\t{user.ToString().PadRight(40, '.')}{val.PadLeft(13, '.')}");
                        tsubtot += tnum; tgrandtot += tnum;
                        vsubtot += vnum; vgrandtot += vnum;
                    }

                    log($"\tTotal {tsubtot} promotions and {vsubtot} versions.{Environment.NewLine}");
                }

                log($"Grand total of {tgrandtot} promotions and {vgrandtot} versions.");
                ret = true;
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in Program.promoCountAsync{Environment.NewLine}{ecx.Message}");
            }
            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.promoCountAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // General program startup initialization. Returns true if initialization succeeded, false otherwise.
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

            // initialize our class variables from PromoCount.exe.config
            if (!initAppConfigData()) return false;
            // initialize our logging support for program results
            if (!initPromoCountResultsLogging()) return false;

            _users = new AcUsers(_domains, null, includeGroupsList: false, includeDeactivated: true);
            _depots = new AcDepots(dynamicOnly: true);
            Task<bool[]> lists = Task.WhenAll(_depots.initAsync(_selDepots), _users.initAsync());
            if (lists == null || lists.Result.Any(n => n == false)) return false;

            return true;
        }

        // Initialize our class variables from PromoCount.exe.config. Returns true if all values successfully read 
        // and class variables initialized, false otherwise. ConfigurationErrorsException caught and logged in 
        // %LOCALAPPDATA%\AcTools\Logs\PromoCount-YYYY-MM-DD.log on initialization failure.
        private static bool initAppConfigData()
        {
            bool ret = true; // assume success
            try
            {
                _startTime = AcQuery.getAppConfigSetting<string>("StartTime").Trim();
                _endTime = AcQuery.getAppConfigSetting<string>("EndTime").Trim();

                ADSection adSection = ConfigurationManager.GetSection("activeDir") as ADSection;
                if (adSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating ADSection");
                    ret = false;
                }
                else
                    _domains = adSection.Domains;

                DepotsSection depotsConfigSection = ConfigurationManager.GetSection("Depots") as DepotsSection;
                if (depotsConfigSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating DepotsSection");
                    ret = false;
                }
                else
                    _selDepots = depotsConfigSection.Depots;

                StreamsSection streamsConfigSection = ConfigurationManager.GetSection("Streams") as StreamsSection;
                if (streamsConfigSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating StreamsSection");
                    ret = false;
                }
                else
                    _selStreams = streamsConfigSection.Streams;
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

        // Initialize our logging support for PromoCount program results. Output is sent to the daily log file 
        // PromoCountResults-YYYY-MM-DD.log created (or updated) in the same folder where PromoCount.exe resides.
        // Returns true if logging support was successfully initialized, false otherwise. Exception caught and 
        // logged in %LOCALAPPDATA%\AcTools\Logs\PromoCount-YYYY-MM-DD.log on initialization failure.
        private static bool initPromoCountResultsLogging()
        {
            bool ret = false; // assume failure
            try
            {
                TraceSource ts = new TraceSource("PromoCount"); // this program name
                _tl = (FileLogTraceListener)ts.Listeners["PromoCountResults"];
                _tl.Location = LogFileLocation.ExecutableDirectory; // create the log file in the same folder as our executable
                _tl.BaseFileName = "PromoCountResults"; // our log file name begins with this name
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
                AcDebug.Log($"Exception caught and logged in Program.initPromoCountResultsLogging{Environment.NewLine}{exc.Message}");
            }

            return ret;
        }

        // Write text to the PromoCountResults-YYYY-MM-DD.log daily log file located in the same folder as the executable.
        private static void log(string text)
        {
            if (_tl != null)
                _tl.WriteLine(text);
        }
    }
}
