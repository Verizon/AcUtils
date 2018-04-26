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
        private static readonly object _locker = new object(); // token for lock keyword scope

        static int Main()
        {
            // show the rules barnyrd set on his workspaces throughout the repository
            Task<bool> rini = showRulesAsync("barnyrd");
            return (rini.Result) ? 0 : 1;
        }

        public static async Task<bool> showRulesAsync(string user)
        {
            List<AcRules> rules = new List<AcRules>();
            Func<AcStream, Task<bool>> init = (s) =>
            {
                AcRules r = new AcRules(explicitOnly: true); // explicitly-set rules only
                lock (_locker) { rules.Add(r); }
                return r.initAsync(s);
            };

            AcDepots depots = new AcDepots();
            if (!(await depots.initAsync())) return false;

            var tasks = from s in depots.SelectMany(d => d.Streams)
                        where s.Type == StreamType.workspace &&
                        !s.Hidden && s.Name.EndsWith(user) // workspace names have principal name appended
                        select init(s);

            bool[] arr = await Task.WhenAll(tasks);
            if (arr == null || arr.Any(n => n == false)) return false;

            foreach (AcRule rule in rules.SelectMany(n => n).OrderBy(n => n))
                Console.WriteLine(rule);

            return true;
        }
    }
}
