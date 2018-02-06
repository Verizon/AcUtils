/* Copyright (C) 2016-2018 Verizon. All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License. */

// Required references: AcUtils.dll
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
            // show the rules barnyrd set on his workspaces throughout the repository
            Task<bool> rini = showRulesAsync("barnyrd");
            return (rini.Result) ? 0 : 1;
        }

        public static async Task<bool> showRulesAsync(string user)
        {
            AcDepots depots = new AcDepots();
            if (!(await depots.initAsync())) return false;

            List<Task<bool>> tasks = new List<Task<bool>>();
            Dictionary<AcDepot, List<AcRules>> map = new Dictionary<AcDepot, List<AcRules>>(depots.Count);

            foreach (AcDepot depot in depots)
            {
                IEnumerable<AcStream> filter = depot.Streams.Where(n => n.Type == StreamType.workspace &&
                    !n.Hidden && n.Name.EndsWith(user)); // workspace names have principal name appended
                int num = filter.Count();
                List<Task<bool>> tlist = new List<Task<bool>>(num);
                List<AcRules> rlist = new List<AcRules>(num);

                foreach (AcStream stream in filter)
                {
                    AcRules rules = new AcRules(explicitOnly: true); // explicitly-set rules only
                    tlist.Add(rules.initAsync(stream));
                    rlist.Add(rules);
                }

                tasks.AddRange(tlist);
                map.Add(depot, rlist);
            }

            bool[] arr = await Task.WhenAll(tasks); // finish running in parallel
            if (arr == null || arr.Any(n => n == false)) // if one or more failed to initialize
                return false;

            foreach (KeyValuePair<AcDepot, List<AcRules>> pair in map.OrderBy(n => n.Key)) // order by depot
            {
                List<AcRules> list = pair.Value;
                foreach (AcRules rules in list)
                    foreach (AcRule rule in rules.OrderBy(n => n))
                        Console.WriteLine(rule);
            }

            return true;
        }
    }
}
