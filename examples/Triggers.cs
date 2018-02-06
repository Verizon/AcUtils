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

// Required references: AcUtils.dll, System.Xml.Linq
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcUtils;

namespace Triggers
{
    class Program
    {
        static int Main()
        {
            // general program initialization
            if (!init()) return 1;

            Task<Triggers> t = getTriggersAsync();
            Triggers triggers = t.Result;
            if (triggers == null) return 1;
            Console.WriteLine(triggers);
            return 0;
        }

        // Get the list of triggers created with the mktrig command for the specified 
        // depots. Returns Triggers object on success, otherwise null on error.
        private async static Task<Triggers> getTriggersAsync()
        {
            List<string> depots = await AcQuery.getDepotNameListAsync(); // names of all depots in repository
            if (depots == null) return null;
            Triggers triggers = new Triggers(depots);
            return (await triggers.initAsync()) ? triggers : null;
        }

        // General program initialization routines. Returns true 
        // if initialization succeeded, otherwise false.
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

            return true;
        }
    }

    // List of depots and their triggers created by the mktrig command.
    public sealed class Triggers : List<XElement>
    {
        #region class variables
        private List<string> _depots; // depots to include in this list
        private readonly object _locker = new object();  // token for lock keyword scope
        #endregion

        // Constructor and list of depots to include in this list.
        public Triggers(List<string> depots) { _depots = depots; }

        // Initialize the list of triggers created by the mktrig command for the depots 
        // specified in the constructor. Returns true if operation succeeded, false otherwise.
        public async Task<bool> initAsync()
        {
            List<Task<bool>> tasks = new List<Task<bool>>(_depots.Count);
            foreach (string depot in _depots)
                tasks.Add(initListAsync(depot));

            bool[] arr = await Task.WhenAll(tasks); // finish running show commands in parallel
            return (arr != null && arr.All(n => n == true)); // true if all succeeded
        }

        // Run the show triggers command for depot and add the results to our list. Returns true 
        // if the operation succeeded, false otherwise. AcUtilsException caught and logged in 
        // %LOCALAPPDATA%\AcTools\Logs\Triggers-YYYY-MM-DD.log on show command failure.
        // Exception caught and logged in same for a range of exceptions.
        private async Task<bool> initListAsync(string depot)
        {
            bool ret = false; // assume failure
            try
            {
                AcResult result = await AcCommand.runAsync($@"show -p ""{depot}"" -fx triggers");
                if (result != null && result.RetVal == 0)
                {
                    XElement xml = XElement.Parse(result.CmdResult);
                    xml.AddAnnotation(depot); // add depot since it's not in the XML
                    lock (_locker) { Add(xml); }
                    ret = true;
                }
            }

            catch (AcUtilsException exc)
            {
                AcDebug.Log($"AcUtilsException caught and logged in Program.initListAsync{Environment.NewLine}{exc.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.initListAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // Return the list of depots and triggers (if any) ordered by depot then by trigger 
        // with the current date/time at the top of the list.
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"As of {DateTime.Now.ToString()}{Environment.NewLine}");
            foreach (XElement e in this.OrderBy(n => n.Annotation<string>())) // order by depot name
            {
                string depot = e.Annotation<string>();
                sb.AppendLine(depot);
                foreach (XElement t in e.Elements("Element").OrderBy(n => (string)n.Attribute("Type"))) // order by trigger name
                    sb.AppendLine($"\t{(string)t.Attribute("Type")}: {(string)t.Attribute("Name")}");
            }

            return sb.ToString();
        }
    }
}
