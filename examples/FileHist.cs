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
            Console.WriteLine(@"Depot: {0}, EID: {1} ""{2} - {3}""{4}", depot, eid, startTime, endTime, Environment.NewLine);
            string time = String.Format("{0} - {1}", endTime, startTime); // reverse start-end times as workaround for AccuRev issue 15780
            string cmd = String.Format(@"hist -p ""{0}"" -t ""{1}"" -e {2} -fevx", depot, time, eid);
            AcResult result = await AcCommand.runAsync(cmd);
            if (result == null || result.RetVal != 0) return false; // operation failed, check log file
            XElement xml = XElement.Parse(result.CmdResult);
            XElement e = xml.Element("element");

            foreach (XElement t in e.Elements("transaction"))
            {
                Console.WriteLine("Transaction: {0} {{{1}}}, {2}", (int)t.Attribute("id"),
                    (string)t.Attribute("type"), // transaction type, e.g. keep, move, promote, purge, etc.
                    t.acxTime("time")); // convert Epoch "time" attribute

                string tcomment = t.acxComment();
                Console.WriteLine("User: {0}{1}", (string)t.Attribute("user"),
                    String.IsNullOrEmpty(tcomment) ? String.Empty : ", " + tcomment);

                string fromStream = t.acxFromStream();
                if (!String.IsNullOrEmpty(fromStream))
                    Console.WriteLine("From {0} to {1}", fromStream, t.acxToStream()); // attributes that exist for promote transactions only

                string virtualNamed = t.acxVirtualNamed();
                if (!String.IsNullOrEmpty(virtualNamed)) Console.WriteLine("Virtual: {0}", virtualNamed); // a promote or co operation

                Console.WriteLine();
                foreach (XElement v in t.Elements("version"))
                {
                    string realNamed = v.acxRealNamed();
                    if (String.IsNullOrEmpty(realNamed)) continue; // null in first (redundant) version element in promote transactions

                    string vcomment = v.acxComment();
                    if (!String.IsNullOrEmpty(vcomment)) Console.WriteLine("\t" + vcomment);

                    string path = (string)v.Attribute("path");
                    if (!String.IsNullOrEmpty(path)) Console.WriteLine("\tEID: {0} {1}", eid, path);

                    DateTime? mtime = v.acxTime("mtime"); // convert Epoch "mtime" attribute
                    Console.WriteLine("\tReal: {0} {1}", realNamed,
                        (mtime == null) ? String.Empty : "Modified: " + mtime);

                    string ancestorNamed = v.acxAncestorNamed();
                    if (!String.IsNullOrEmpty(ancestorNamed)) Console.WriteLine("\tAncestor: {0}", ancestorNamed);

                    string mergedAgainstNamed = v.acxMergedAgainstNamed();
                    if (!String.IsNullOrEmpty(mergedAgainstNamed)) Console.WriteLine("\tMerged against: {0}", mergedAgainstNamed);

                    Console.WriteLine();
                }

                Console.WriteLine("--------------------------------------------------------");
            }

            return true;
        }
    }
}
