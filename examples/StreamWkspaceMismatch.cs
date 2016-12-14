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
using System.Linq;
using System.Threading.Tasks;
using AcUtils;

namespace StreamWkspaceMismatch
{
    class Program
    {
        static int Main()
        {
            Task<bool> init = streamWkspaceMismatchAsync();
            return (init.Result) ? 0 : 1;
        }

        public static async Task<bool> streamWkspaceMismatchAsync()
        {
            bool ret = false; // assume failure
            try
            {
                // false for all streams and not just dynamic, true to include hidden streams
                AcDepots depots = new AcDepots(false, true);
                if (!(await depots.initAsync())) return false;

                // true for all workspaces (not just the script user), true to include hidden workspaces
                AcWorkspaces wkspaces = new AcWorkspaces(depots, true, true);
                if (!(await wkspaces.initAsync())) return false;

                foreach (AcDepot depot in depots.OrderBy(n => n))
                {
                    var query = from AcStream s in depot.Streams
                                join AcWorkspace w in wkspaces on s.Depot equals w.Depot
                                where w.ID == s.ID && !string.Equals(s.Name, w.Name) // stream ID's equal but names don't
                                orderby w.Name
                                select new
                                {
                                    Hidden = w.Hidden,
                                    WorkspaceName = w.Name,
                                    StreamName = s.Name
                                };

                    foreach (var f in query)
                    {
                        string msg = String.Format("{0} wspace: {1}, stream: {2}", f.Hidden ? "Hidden" : "Visible",
                            f.WorkspaceName, f.StreamName);
                        Console.WriteLine(msg);
                    }
                }

                ret = true; // operation succeeded
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in Program.streamWkspaceMismatchAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                Console.WriteLine(msg);
            }

            return ret;
        }
    }
}
