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

// Required references: AcUtils.dll, System.Xml.Linq
using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcUtils;

namespace FileHist
{
    class Program
    {
        static int Main()
        {
            string depot = "MARS";
            int eid = 594;
            string startTime = "2016/11/01 11:30:00";
            string endTime = "2016/11/01 14:30:00";

            Task<bool> r = fileHistAsync(depot, eid, startTime, endTime);
            bool ret = r.Result;
            return (ret) ? 0 : 1;
        }

        private static async Task<bool> fileHistAsync(string depot, int eid, string startTime, string endTime)
        {
            Console.WriteLine($@"Depot: {depot}, EID: {eid} ""{startTime} - {endTime}""{Environment.NewLine}");
            string time = $"{endTime} - {startTime}"; // reverse start-end times as workaround for AccuRev issue 15780
            AcResult result = await AcCommand.runAsync($@"hist -p ""{depot}"" -t ""{time}"" -e {eid} -fevx");
            if (result == null || result.RetVal != 0) return false; // operation failed, check log file
            XElement xml = XElement.Parse(result.CmdResult);
            XElement e = xml.Element("element");

            foreach (XElement t in e.Elements("transaction"))
            {
                Console.WriteLine($"Transaction: {(int)t.Attribute("id")} " + // transaction ID
                    $"{{{(string)t.Attribute("type")}}}, " + // transaction type, e.g. keep, move, promote, purge, etc.
                    $"{t.acxTime("time")}"); // convert Epoch "time" attribute to .NET DateTime object

                string tcomment = t.acxComment();
                Console.WriteLine($"User: {(string)t.Attribute("user")}{(String.IsNullOrEmpty(tcomment) ? String.Empty : ", " + tcomment)}");

                string fromStream = t.acxFromStream();
                if (!String.IsNullOrEmpty(fromStream))
                    Console.WriteLine($"From {fromStream} to {t.acxToStream()}"); // attributes that exist for promote transactions only

                string virtualNamed = t.acxVirtualNamed();
                if (!String.IsNullOrEmpty(virtualNamed)) Console.WriteLine($"Virtual: {virtualNamed}"); // a promote or co operation

                Console.WriteLine();
                foreach (XElement v in t.Elements("version"))
                {
                    string realNamed = v.acxRealNamed();
                    if (String.IsNullOrEmpty(realNamed)) continue; // null in first (redundant) version element in promote transactions

                    string vcomment = v.acxComment();
                    if (!String.IsNullOrEmpty(vcomment)) Console.WriteLine("\t" + vcomment);

                    string path = (string)v.Attribute("path");
                    if (!String.IsNullOrEmpty(path)) Console.WriteLine($"\tEID: {eid} {path}");

                    DateTime? mtime = v.acxTime("mtime"); // convert Epoch "mtime" attribute
                    Console.WriteLine($"\tReal: {realNamed} {((mtime == null) ? String.Empty : "Modified: " + mtime)}");

                    string ancestorNamed = v.acxAncestorNamed();
                    if (!String.IsNullOrEmpty(ancestorNamed)) Console.WriteLine($"\tAncestor: {ancestorNamed}");

                    string mergedAgainstNamed = v.acxMergedAgainstNamed();
                    if (!String.IsNullOrEmpty(mergedAgainstNamed)) Console.WriteLine($"\tMerged against: {mergedAgainstNamed}");

                    Console.WriteLine();
                }

                Console.WriteLine("--------------------------------------------------------");
            }

            return true;
        }
    }
}
