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
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using AcUtils;

namespace LockStreams
{
    class Program
    {
        private static DepotsCollection _selDepots;

        static int Main()
        {
            bool ret = false; // assume failure
            DepotsSection depotsConfigSection = ConfigurationManager.GetSection("Depots") as DepotsSection;
            if (depotsConfigSection == null)
                Console.WriteLine("Error creating DepotsSection");
            else
            {
                _selDepots = depotsConfigSection.Depots;
                Task<bool> init = lockStreamsAsync();
                ret = init.Result;
            }

            return (ret) ? 0 : 1;
        }

        public static async Task<bool> lockStreamsAsync()
        {
            // set 'To' lock on dynamic streams in select depots that have these strings in their name
            var selStreams = new[] { "DEV2", "UAT" };

            // lock for all except those in DEV_LEAD group
            AcGroups groups = new AcGroups();
            if (!(await groups.initAsync())) return false;
            AcPrincipal group = groups.getPrincipal("DEV_LEAD");
            if (group == null) return false;

            AcDepots depots = new AcDepots(true); // true for dynamic streams only
            if (!(await depots.initAsync(_selDepots))) return false;
            foreach (AcDepot depot in depots.OrderBy(n => n)) // use default sort ordering
            {
                AcLocks locks = new AcLocks();
                if (!(await locks.initAsync(depot))) return false;

                IEnumerable<AcStream> filter = depot.Streams.Where(n => selStreams.Any(s => n.Name.Contains(s)));
                foreach (AcStream stream in filter.OrderBy(n => n)) // ..
                {
                    bool ret = await locks.lockAsync(stream.Name, "Authorized users only", LockKind.to, group);
                    string msg = String.Format(@"{0} ""{1}"" lock {2}", stream, LockKind.to, ret ? "succeeded" : "failed");
                    Console.WriteLine(msg);
                }
            }

            return true;
        }
    }
}
