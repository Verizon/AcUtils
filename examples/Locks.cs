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

// Required references: AcUtils.dll, System.configuration
using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using AcUtils;

namespace Locks
{
    class Program
    {
        private static StreamsCollection _selStreams;

        static int Main()
        {
            bool ret = false; // assume failure
            StreamsSection streamsConfigSection = ConfigurationManager.GetSection("Streams") as StreamsSection;
            if (streamsConfigSection == null)
                Console.WriteLine("Error creating StreamsSection");
            else
            {
                _selStreams = streamsConfigSection.Streams;
                Task<bool> pini = promoRightsAsync();
                ret = pini.Result;
            }

            return (ret) ? 0 : 1;
        }

        public static async Task<bool> promoRightsAsync()
        {
            AcLocks locks = new AcLocks();
            if (!(await locks.initAsync(_selStreams))) return false;

            // list them ordered by stream name then 'To' lock followed by 'From' lock
            foreach (AcLock lk in locks.OrderBy(n => n.Name).ThenByDescending(n => n.Kind))
                Console.WriteLine(lk);

            return true;
        }
    }
}
