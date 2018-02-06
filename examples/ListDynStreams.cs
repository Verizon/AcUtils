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
using System.Linq;
using System.Threading.Tasks;
using AcUtils;

namespace ListDynStreams
{
    class Program
    {
        static int Main()
        {
            Task<bool> init = listDynStreamsAsync();
            return (init.Result) ? 0 : 1;
        }

        public static async Task<bool> listDynStreamsAsync()
        {
            // true for dynamic streams only
            AcDepots depots = new AcDepots(dynamicOnly: true); // typical two-part object construction
            if (!(await depots.initAsync())) // ..
                return false; // initialization failure

            foreach (AcDepot depot in depots.OrderBy(d => d)) // default comparer orders by depot name
                foreach (AcStream stream in depot.Streams.OrderBy(s => s)) // .. orders by stream name
                    Console.WriteLine(stream);

            return true;
        }
    }
}
