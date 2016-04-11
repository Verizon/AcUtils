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

// Required references: AcUtils.dll, System.configuration, System.XML, System.Xml.Linq
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
            Task<bool> init = userChangesAsync();
            return (init.Result) ? 0 : 1;
        }

        public async static Task<bool> userChangesAsync()
        {
            AcDepots depots = new AcDepots();
            if (!(await depots.initAsync())) return false;

            // show changes made by barnyrd in all depots over a two day period
            foreach (AcDepot depot in depots.OrderBy(n => n)) // default order comparer sorts by depot name
            {
                // start-end times reversed as workaround for AccuRev issue 15780
                string hist = String.Format(@"hist -p ""{0}"" -t ""2016/01/11 23:59:59 - 2016/01/10 00:00:00"" -u barnyrd -k keep -fx", depot);
                AcResult r1 = AcCommand.run(hist);
                if (r1.RetVal == 0) // if successful
                {
                    Hist.clear(); // empty old results
                    if (!Hist.init(r1.CmdResult)) // convert hist command XML into .NET objects
                        continue; // parsing failed

                    // Transaction and Version fully qualified to avoid ambiguity with .NET framework classes
                    foreach (AcUtils.Transaction tran in Hist.Transactions.OrderBy(n => n.Time)) // sort by transaction time
                    {
                        List<AcUtils.Version> versions = tran.Versions;
                        foreach (AcUtils.Version ver in versions)
                        {
                            string verspec = String.Format(@"{0}/{1}", ver.Workspace, ver.RealVersionNumber);
                            string anno = String.Format(@"annotate -v ""{0}"" -fxtu ""{1}""", verspec, ver.Location);
                            AcResult r2 = AcCommand.run(anno);
                            if (r2.RetVal == 0) // if successful
                            {
                                XElement e = XElement.Parse(r2.CmdResult);
                                IEnumerable<XElement> q1 = from pn in e.Descendants("diff").Elements("trans")
                                                           where (string)pn.Attribute("principal_name") == "barnyrd" && (int)pn.Attribute("number") == tran.ID
                                                           select pn;
                                foreach (XElement tr in q1)
                                {
                                    /* <trans number="59688" time="1424989734" principal_name="barnyrd" version_name="MARS_DEV2_barnyrd/1">
                                        <comment>merged</comment>
                                        </trans> */
                                    XElement diff = tr.Parent; // go up one level to get the diff where the line elements are, the siblings to trans element
                                    /*  <diff>
                                            <trans number="59688" time="1424989734" principal_name="barnyrd" version_name="MARS_DEV2_barnyrd/1">
                                                <comment>merged</comment>
                                            </trans>
                                            <line number="17" type="rem" trans="59676">pos.module.version.suffix=MAINT</line>
                                            <line number="17" type="add" trans="59688">pos.module.version.suffix=INT</line>
                                        </diff> */
                                    IEnumerable<XElement> q2 = from ln in diff.Descendants("line") where (int)ln.Attribute("trans") == tran.ID select ln;
                                    foreach (XElement line in q2)
                                    {
                                        // <line number="17" type="add" trans="59688">pos.module.version.suffix=INT</line>
                                        string chng = String.Format(@"{0} {{{1}}} {2}, line {3} {4}: ""{5}"", eid {6}, ""{7}""",
                                            verspec, tran.ID, tran.Time, (int)line.Attribute("number"), (string)line.Attribute("type"),
                                            line.Value, ver.EID, ver.Location);
                                        Console.WriteLine(chng);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}

