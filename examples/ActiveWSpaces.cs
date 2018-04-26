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
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using AcUtils;

namespace ActiveWSpaces
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
                Task<bool> wini = showActiveWSpacesAsync();
                ret = wini.Result;
            }

            return (ret) ? 0 : 1;
        }

        public static async Task<bool> showActiveWSpacesAsync()
        {
            AcDepots depots = new AcDepots();
            if (!(await depots.initAsync(_selDepots))) return false;

            var filter = from s in depots.SelectMany(d => d.Streams)
                         where s.Name.Contains("DEV3") && s.HasDefaultGroup &&
                         s.Type == StreamType.workspace
                         select s;

            foreach (AcStream s in filter.OrderBy(n => n.Depot).ThenBy(n => n))
                Console.WriteLine(s.ToString("lv"));

            return true;
        }
    }
}
