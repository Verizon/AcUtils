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

// Required references: AcUtils.dll, System, System.configuration
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AcUtils;

namespace PromoRights
{
    class Program
    {
        #region class variables
        private static DepotsCollection _depots;
        private static AcUsers _users;
        private static AcLocks _locks;
        #endregion

        static int Main()
        {
            Task<bool> ini = initAsync(); // general program startup initialization
            if (!ini.Result) return 1;
            return (promoRights()) ? 0 : 1;
        }

        private static bool promoRights()
        {
            bool ret = false; // assume failure
            try
            {
                foreach (AcUser user in _users.OrderBy(n => n))
                {
                    SortedSet<string> groups = user.Principal.Members; // the list of groups this user is a member of
                    IEnumerable<string> query = from ef in _locks.Select(lk => lk.ExceptFor) // locks applied to all except this group
                                                where groups.Any(g => g == ef) // any group in groups list that matches an ExceptFor group
                                                select ef;
                    string found = query.FirstOrDefault(); // not null indicates the user has promote privileges somewhere
                    Console.WriteLine($"{user}\t{((found == null) ? "None" : "ExceptFor")}");
                }

                ret = true;
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.promoRights{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // General program startup initialization.
        private static async Task<bool> initAsync()
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
            string prncpl = await AcQuery.getPrincipalAsync();
            if (String.IsNullOrEmpty(prncpl))
            {
                AcDebug.Log($"Not logged into AccuRev.{Environment.NewLine}Please login and try again.");
                return false;
            }

            // initialize our depots list class variable from PromoRights.exe.config
            if (!initAppConfigData()) return false;

            _users = new AcUsers(null, null, includeGroupsList: true);
            _locks = new AcLocks();
            bool[] arr = await Task.WhenAll(
                _users.initAsync(), // all users with their respective group membership list initialized
                _locks.initAsync(_depots) // locks on all streams in select depots from PromoRights.exe.config
            );

            return (arr != null && arr.All(n => n == true));
        }

        // Initialize our depots list class variable from PromoRights.exe.config.
        // Returns true if values successfully read and variable initialized, false otherwise.
        // ConfigurationErrorsException caught and logged in %LOCALAPPDATA%\AcTools\Logs\PromoRights-YYYY-MM-DD.log 
        // on initialization failure.
        private static bool initAppConfigData()
        {
            bool ret = true; // assume success
            try
            {
                DepotsSection depotsConfigSection = ConfigurationManager.GetSection("Depots") as DepotsSection;
                if (depotsConfigSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating DepotsSection");
                    ret = false;
                }
                else
                    _depots = depotsConfigSection.Depots;
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
    }
}
