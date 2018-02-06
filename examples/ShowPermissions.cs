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

namespace ShowPermissions
{
    class Program
    {
        private static DomainCollection _domains;

        static int Main()
        {
            bool ret = false; // assume failure
            ADSection adSection = ConfigurationManager.GetSection("activeDir") as ADSection;
            if (adSection == null)
                Console.WriteLine("Error creating ADSection");
            else
            {
                _domains = adSection.Domains;
                Task<bool> pini = permissionsAsync();
                ret = pini.Result;
            }

            return (ret) ? 0 : 1;
        }

        public static async Task<bool> permissionsAsync()
        {
            // since the default is the list of depots the script user can view, 
            // this is typically run by an admin so that all depots are in the list
            AcDepots depots = new AcDepots(dynamicOnly: true); // dynamic streams only
            Task<bool> dini = depots.initAsync();

            // include group membership initialization for each user (slower, but it's required here)
            AcUsers users = new AcUsers(_domains, null, includeGroupsList: true);
            Task<bool> uini = users.initAsync();

            // initialize both lists in parallel
            bool[] lists = await Task.WhenAll(dini, uini);
            if (lists == null || lists.Any(n => n == false)) return false;

            // show depots for these users only
            var arr = new[] { "thomas", "barnyrd", "madhuri", "robert" };
            IEnumerable<AcUser> filter = users.Where(n => arr.Any(user => n.Principal.Name == user));

            // list depots each user has permission to access
            // default order comparer sorts by display name from LDAP if available, otherwise principal name
            foreach (AcUser user in filter.OrderBy(n => n))
            {
                string availDepots = await depots.canViewAsync(user);
                if (!String.IsNullOrEmpty(availDepots))
                    Console.WriteLine($"{user}{Environment.NewLine}{availDepots}{Environment.NewLine}");
            }

            return true;
        }
    }
}
