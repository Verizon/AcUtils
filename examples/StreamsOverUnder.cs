﻿/* Copyright (C) 2016-2018 Verizon. All Rights Reserved.

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

namespace StreamsOverUnder
{
    public struct LapStreamEqualityComparer : IEqualityComparer<Element>
    {
        public bool Equals(Element x, Element y)
        {
            if (Object.ReferenceEquals(x, y))
                return true;
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;
            return x.LapStream.Equals(y.LapStream);
        }

        public int GetHashCode(Element elem)
        {
            return elem.LapStream.GetHashCode();
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            bool ret = false; // assume failure
            Task<bool> init = initStatAsync();
            if (init.Result) // if all ran successfully
            {
                LapStreamEqualityComparer comparer = new LapStreamEqualityComparer();
                // Tip: add a Where clause to drill down further
                //foreach (Element e in Stat.Elements.Where(n => n.Status.Contains("underlap")).Distinct(comparer).OrderBy(n => n.LapStream))
                foreach (Element e in Stat.Elements.Distinct(comparer).OrderBy(n => n.LapStream))
                    Console.WriteLine(e.ToString("L"));

                ret = true; // operation completed successfully
            }

            return (ret) ? 0 : 1;
        }

        // Initialize Stat with all versions in the repository with overlap or underlap status.
        private static async Task<bool> initStatAsync()
        {
            AcDepots depots = new AcDepots(dynamicOnly: true); // dynamic streams only
            if (!(await depots.initAsync())) return false;

            List<Task<bool>> tasks = new List<Task<bool>>();
            foreach (AcStream stream in depots.SelectMany(d => d.Streams).Where(s => s.HasDefaultGroup))
                tasks.Add(runStatCommandAsync($@"stat -s ""{stream}"" -o -B -fx"));

            bool[] arr = await Task.WhenAll(tasks); // continue running stat commands in parallel
            return (arr != null && arr.All(n => n == true)); // true if all succeeded
        }

        // Helper function that runs our stat commands.
        private static async Task<bool> runStatCommandAsync(string cmd)
        {
            bool ret = false; // assume failure
            AcResult result = null;
            try
            {
                result = await AcCommand.runAsync(cmd);
                ret = (result != null && result.RetVal == 0);
            }

            catch (AcUtilsException exc)
            {
                Console.WriteLine($"AcUtilsException caught in runStatCommandAsync{Environment.NewLine}{exc.Message}");
            }

            return (ret && Stat.init(result.CmdResult));
        }
    }
}
