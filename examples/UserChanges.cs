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

// Required references: System, System.Xml.Linq, AcUtils.dll
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcUtils;

namespace UserChanges
{
    class Program
    {
        static int Main()
        {
            string user = "barnyrd";
            string startTime = "2016/11/29 00:00:00";
            string endTime = "2016/11/30 23:59:59";

            Task<bool> r = userChangesAsync(user, startTime, endTime);
            return (r.Result) ? 0 : 1;
        }

        public static async Task<bool> userChangesAsync(string user, string startTime, string endTime)
        {
            Console.WriteLine(@"User: {0}, ""{1} - {2}""{3}", user, startTime, endTime, Environment.NewLine);
            List<string> depots = await AcQuery.getDepotNameListAsync();
            if (depots == null) return false; // operation failed, check log file
            foreach (string depot in depots)
            {
                // start-end times reversed as workaround for AccuRev issue 15780
                string time = String.Format("{0} - {1}", endTime, startTime);
                string hist = String.Format(@"hist -p ""{0}"" -t ""{1}"" -u ""{2}"" -k keep -fx", depot, time, user);
                AcResult r1 = await AcCommand.runAsync(hist);
                if (r1 == null || r1.RetVal != 0) return false; // operation failed, check log file

                XElement x1 = XElement.Parse(r1.CmdResult);
                foreach (XElement t in x1.Elements("transaction"))
                {
                    int transID = (int)t.Attribute("id");
                    string tcomment = t.acxComment();
                    Console.WriteLine("Depot: {0}, {{{1}}} {2}{3}", depot, transID, (DateTime)t.acxTime("time"),
                        String.IsNullOrEmpty(tcomment) ? String.Empty : ", " + tcomment);

                    foreach (XElement v in t.Elements("version"))
                    {
                        string path = (string)v.Attribute("path");
                        Console.WriteLine("\tEID: {0} {1} ({2})", (int)v.Attribute("eid"), path, (string)v.Attribute("real"));
                        string mergedAgainstNamed = v.acxMergedAgainstNamed();
                        Console.WriteLine("\tReal: {0}, Ancestor: {1}{2}", v.acxRealNamed(), v.acxAncestorNamed(),
                            String.IsNullOrEmpty(mergedAgainstNamed) ? String.Empty : ", Merged against: " + mergedAgainstNamed);

                        string realNamed = (string)v.Attribute("realNamedVersion");
                        string anno = String.Format(@"annotate -v ""{0}"" -fxtu ""{1}""", realNamed, path);
                        AcResult r2 = await AcCommand.runAsync(anno);
                        if (r2 == null || r2.RetVal != 0) return false; // operation failed, check log file

                        // get this transaction from the annotate results
                        XElement x2 = XElement.Parse(r2.CmdResult);
                        XElement trans = (from a in x2.Descendants("trans")
                                          where (int)a.Attribute("number") == transID && // comparing transaction ID's
                                          (string)a.Attribute("principal_name") == user &&
                                          (string)a.Attribute("version_name") == realNamed
                                          select a).SingleOrDefault();
                        if (trans != null)
                        {
                            XElement diff = trans.Parent; // get diff element for this transaction from annotate results
                            foreach (XElement ln in diff.Elements("line")) // line elements are transaction element siblings
                            {
                                Console.WriteLine("\tLine number: {0} \"{1}\" {{{2}}}, {3}", (int)ln.Attribute("number"),
                                    (string)ln.Attribute("type"), (int)ln.Attribute("trans"), (string)ln);
                            }
                        }

                        Console.WriteLine();
                    }
                }
            }

            return true;
        }
    }
}

