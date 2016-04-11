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

// Required references: AcUtils.dll, System.configuration, System.Xml.Linq
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AcUtils;

namespace LatestPromotions
{
    class Program
    {
        static int Main(string[] args)
        {
            // initialize our logging support so we can log errors
            if (!AcDebug.initAcLogging())
            {
                Console.WriteLine("Logging support initialization failed.");
                return 1;
            }

            string depot = args[0];
            int hoursAgo;
            if (!int.TryParse(args[1], out hoursAgo))
                return 1;

            bool ret = latestPromotions(depot, hoursAgo);
            return (ret == true) ? 0 : 1;
        }

        private static bool latestPromotions(string depot, int hoursAgo)
        {
            bool ret = false; // assume failure
            try
            {
                DateTime dt = DateTime.Now.AddHours(hoursAgo * -1); // get the time hours ago
                string timeHrsAgo = AcDateTime.DateTime2AcDate(dt);
                if (!AcDateTime.AcDateValid(timeHrsAgo))
                {
                    AcDebug.Log("Invalid date returned for hoursAgo!");
                    return false;
                }

                string cmd = String.Format(@"hist -k promote -p ""{0}"" -t ""{1}-now"" -ftex", depot, timeHrsAgo);
                AcResult res = AcCommand.run(cmd);
                if (res.RetVal == 0)
                {
                    XElement xml = XElement.Parse(res.CmdResult);
                    // order with most recent transactions on top
                    IEnumerable<XElement> query = from e in xml.Descendants("transaction")
                                                  orderby (long)e.Attribute("time") descending
                                                  select e;
                    foreach (XElement e in query)
                    {
                        int transaction = (int)e.Attribute("id");
                        long lval = (long)e.Attribute("time");
                        DateTime? transTime = AcDateTime.AcDate2DateTime(lval);
                        string user = (string)e.Attribute("user");
                        string stream = (string)e.Attribute("streamName");
                        int id = (int)e.Attribute("streamNumber");
                        string comment = (string)e.Element("comment");
                        string line = String.Format("{0} {1} {2} {3} ({4}) {5}",
                            transaction, transTime, user, stream, id, comment);
                        Console.WriteLine(line);
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught in Program.latestPromotions{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught in Program.latestPromotions{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
    }
}
