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

namespace WSpaceTransLevel
{
    class Program
    {
        #region class variables
        private static DepotsCollection _selDepots;
        private static AcWorkspaces _wspaces;
        #endregion

        static int Main()
        {
            if (!init()) return 1;
            Task<bool> rpt = reportAsync();
            bool ret = rpt.Result;
            return (ret) ? 0 : 1;
        }

        // Generate the report and send the results to the console ordered by depot, then transaction time in 
        // reverse chronological order (latest transactions on top), then by workspace name. Appends workspace 
        // {UpdateLevel - TargetLevel} for workspaces that are in an inconsistent state (update cancellation/failure).
        private static async Task<bool> reportAsync()
        {
            int num = (from ws in _wspaces
                       where ws.UpdateLevel > 0 && ws.TargetLevel > 0
                       select ws).Count();
            List<Task<XElement>> tasks = new List<Task<XElement>>(num);

            foreach (AcWorkspace ws in _wspaces.OrderBy(n => n))
            {
                if (ws.UpdateLevel > 0 && ws.TargetLevel > 0)
                    tasks.Add(latestTransAsync(ws));
                else
                    Console.WriteLine($"{ws} off {ws.getBasis()} in depot {ws.Depot} needs an update.");
            }

            XElement[] arr = await Task.WhenAll(tasks); // finish running hist commands in parallel
            if (arr == null || arr.Any(n => n == null)) return false;

            foreach (XElement t in arr.OrderBy(n => n.Annotation<AcWorkspace>().Depot)
                .ThenByDescending(n => n.acxTime("time"))
                .ThenBy(n => n.Annotation<AcWorkspace>().Name))
            {
                AcWorkspace ws = t.Annotation<AcWorkspace>();
                string levels = (ws.UpdateLevel == ws.TargetLevel) ? String.Empty : $", {{{ws.UpdateLevel} - {ws.TargetLevel}}}";
                Console.WriteLine($"The last time {ws} off {ws.getBasis()} was successfully updated,{Environment.NewLine}" +
                    $"the latest transaction {(int)t.Attribute("id")} in depot {ws.Depot} occurred on {t.acxTime("time")}{levels}");
            }

            return true;
        }

        // Get the latest transaction in the depot that occurred the last time wspace was successfully updated, 
        // otherwise returns null on error. Adds wspace to the transaction as an annotation. AcUtilsException 
        // caught and logged in %LOCALAPPDATA%\AcTools\Logs\WSpaceTransLevel-YYYY-MM-DD.log on hist command failure. 
        // Exception caught and logged in same for a range of exceptions.
        private static async Task<XElement> latestTransAsync(AcWorkspace wspace)
        {
            XElement trans = null; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync($@"hist -fx -p ""{wspace.Depot}"" -t {wspace.UpdateLevel}");
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    trans = xml.Element("transaction");
                    if (trans != null)
                        trans.AddAnnotation(wspace);
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in Program.latestTransAsync{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.latestTransAsync{Environment.NewLine}{ecx.Message}");
            }

            return trans;
        }

        // General program startup initialization. Returns true if initialization was successful, false otherwise.
        private static bool init()
        {
            // initialize our logging support so we can log errors
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

            // initialize our depots list class variable with select depots from WSpaceTransLevel.exe.config
            if (!initAppConfigData()) return false;

            // initialize our workspaces list class variable
            Task<bool> wslist = initWSListAsync();
            if (!wslist.Result)
            {
                AcDebug.Log($"Workspaces list initialization failed. See log file:{Environment.NewLine}{AcDebug.getLogFile()}");
                return false;
            }

            return true;
        }

        // Initialize depots list class variable with select depots from WSpaceTransLevel.exe.config. Returns true 
        // if initialization was successful, false otherwise. ConfigurationErrorsException caught and logged in 
        // %LOCALAPPDATA%\AcTools\Logs\WSpaceTransLevel-YYYY-MM-DD.log on initialization failure.
        private static bool initAppConfigData()
        {
            bool ret = false; // assume failure
            try
            {
                DepotsSection depotsConfigSection = ConfigurationManager.GetSection("Depots") as DepotsSection;
                if (depotsConfigSection == null)
                    AcDebug.Log("Error in Program.initAppConfigData creating DepotsSection");
                else
                {
                    _selDepots = depotsConfigSection.Depots;
                    ret = true;
                }
            }

            catch (ConfigurationErrorsException exc)
            {
                Process currentProcess = Process.GetCurrentProcess();
                ProcessModule pm = currentProcess.MainModule;
                AcDebug.Log($"Invalid data in {pm.ModuleName}.config{Environment.NewLine}{exc.Message}");
            }

            return ret;
        }

        // Initialize our workspaces list class variable. 
        // Returns true if initialization was successful, false otherwise.
        private static async Task<bool> initWSListAsync()
        {
            // fully initialized depots list is required for workspaces list construction below
            AcDepots depots = new AcDepots();
            if (!(await depots.initAsync(_selDepots))) return false;

            // include all workspaces (not just the script user), include only workspaces that are active
            _wspaces = new AcWorkspaces(depots, allWSpaces: true, includeHidden: false);
            if (!(await _wspaces.initAsync())) return false;

            return true;
        }
    }
}
