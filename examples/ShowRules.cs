/* Copyright (C) 2016 Verizon. All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License. */

// Required references: AcUtils.dll, System.configuration
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcUtils;

namespace ShowRules
{
    class Program
    {
        static int Main()
        {
            Task<bool> rini = showRulesAsync();
            return (rini.Result) ? 0 : 1;
        }

        public static async Task<bool> showRulesAsync()
        {
            // show the rules barnyrd set on his workspaces throughout the repository
            AcDepots depots = new AcDepots();
            if (!(await depots.initAsync())) return false;

            List<Task<bool>> tasks = new List<Task<bool>>();
            List<AcRules> list = new List<AcRules>();
            foreach (AcDepot depot in depots.OrderBy(n => n)) // use default comparer
            {
                foreach (AcStream stream in depot.Streams.Where(n => n.Type == StreamType.workspace &&
                    n.Name.EndsWith("barnyrd")).OrderBy(n => n)) // workspace names always have principal name appended
                {
                    AcRules r = new AcRules(true); // true for explicitly-set rules only
                    tasks.Add(r.initAsync(stream));
                    list.Add(r);
                }
            }

            bool[] arr = await Task.WhenAll(tasks); // asynchronously await multiple asynchronous operations.. fast!
            if (arr == null || arr.Any(n => n == false)) // if one or more failed to initialize
                return false;

            foreach (AcRules rules in list)
                foreach (AcRule rule in rules.OrderBy(n => n)) // use default comparer
                    Console.WriteLine(rule);

            return true;
        }
    }
}
