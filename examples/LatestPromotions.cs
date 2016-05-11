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

// Required references: AcUtils.dll, System, System.configuration, System.Xml, System.Xml.Linq
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcUtils;

namespace LatestPromotions
{
    class Program
    {
        #region class variables
        private static DomainCollection _domains;
        private static PropCollection _properties;
        private static DepotsCollection _selDepots;
        private static StreamsCollection _selStreams;
        private static int _fromHoursAgo;
        private static string _outputFile;
        private static AcDepots _depots;
        private static AcUsers _users;
        #endregion

        static int Main()
        {
            // general program startup initialization
            if (!init()) return 1; // initialization failure, check log file

            // true for dynamic streams only in select depots
            _depots = new AcDepots(true);
            Task<bool> dini = _depots.initAsync(_selDepots);

            // false to exclude group membership initialization, true to include deactivated users
            _users = new AcUsers(_domains, _properties, false, true);
            Task<bool> uini = _users.initAsync();

            // initialize in parallel and continue after both complete
            Task<bool[]> all = Task.WhenAll(dini, uini);
            if (all == null || all.Result.Any(n => n == false)) return 1; // initialization failure, check log file

            DateTime past = DateTime.Now.AddHours(_fromHoursAgo * -1); // go back this many hours
            if (!getTransactions(past)) return 1; // operation failed, check log file
            if (Hist.Transactions.Count > 0)
            {
                XElement report = buildReport(past);
                report.Save(_outputFile);
            }

            return 0;
        }

        // Initialize our static Hist object with the transactions found for each stream listed 
        // in the LatestPromotions.exe.config Streams section. AcUtilsException caught and logged 
        // in %LOCALAPPDATA%\AcTools\Logs\LatestPromotions-YYYY-MM-DD.log on hist command failure.
        // Exception caught and logged in same on failure to handle a range of exceptions.
        private static bool getTransactions(DateTime past)
        {
            // get date in string format suitable for AccuRev CLI
            string timeHrsAgo = AcDateTime.DateTime2AcDate(past);
            foreach (AcDepot depot in _depots.OrderBy(n => n)) // order using AcDepot default comparer
            {
                // Alternatively change Equals() to Contains() and modify LatestPromotions.exe.config stream values 
                // accordingly to filter on stream name subsets, e.g. <add stream="DEV"/> for all streams with DEV in their name.
                IEnumerable<AcStream> filter = depot.Streams.Where(n =>
                    _selStreams.OfType<StreamElement>().Any(s => n.Name.Equals(s.Stream)));

                foreach (AcStream stream in filter.OrderBy(n => n)) // order using AcStream default comparer
                {
                    try
                    {
                        string cmd = String.Format(@"hist -k promote -s ""{0}"" -t now-""{1}"" -fx", stream, timeHrsAgo);
                        AcResult result = AcCommand.run(cmd);
                        if (result.RetVal == 0) // if AccuRev command succeeded
                        {
                            XElement xml = XElement.Parse(result.CmdResult); // CmdResult is XML from AccuRev
                            IEnumerable<XElement> query = from e in xml.Descendants("transaction")
                                                          where e.Element("version").Attribute("path") != null
                                                          select e;
                            foreach (XElement e in query) // for each transaction
                            {
                                // add root element normally emitted by AccuRev and expected by Hist.init()
                                XElement t = new XElement("AcResponse", e);
                                string trans = t.ToString(); // get the XML
                                if (!Hist.init(trans)) // convert XML into .NET objects and add the transaction to our list
                                    return false; // parsing failed, check log file
                            }
                        }
                    }

                    catch (AcUtilsException ecx)
                    {
                        string msg = String.Format("AcUtilsException caught and logged in Program.getTransactions{0}{1}",
                            Environment.NewLine, ecx.Message);
                        AcDebug.Log(msg);
                    }

                    catch (Exception ecx)
                    {
                        string msg = String.Format("Exception caught and logged in Program.getTransactions{0}{1}",
                            Environment.NewLine, ecx.Message);
                        AcDebug.Log(msg);
                    }
                }
            }

            return true;
        }

        // Returns the content for our HTML file.
        private static XElement buildReport(DateTime past)
        {
            return new XElement("html",
                new XElement("head"),
                new XElement("body",
                    new XAttribute("topmargin", 10),
                    new XAttribute("leftmargin", 10),
                    new XAttribute("rightmargin", 10),
                    new XAttribute("bottommargin", 10),
                    new XAttribute("marginwidth", 5),
                    new XAttribute("marginheight", 5),
                    new XAttribute("style", "font family: Arial; font-size: 12pt"),
                    new XAttribute("text", "#0000ff"),
                    new XAttribute("bgcolor", "#cccccc"),
                    new XElement("p", "Promotions since " + past.ToString("f") + " to:", new XElement("ul",
                        from s in _selStreams.OfType<StreamElement>()
                        orderby s.Stream
                        select new XElement("li", s.Stream)),
                        new XElement("table",
                            new XAttribute("border", 1),
                            new XAttribute("cellpadding", 3),
                            new XAttribute("cellspacing", 2),
                            new XAttribute("bordercolor", "#cccccc"),
                            new XAttribute("style", "font family: Arial; font-size: 10pt"),
                            new XAttribute("bgcolor", "#ffffff"),
                            new XElement("thead",
                                new XElement("tr",
                                    new XElement("td", "TransID"),
                                    new XElement("td", "TransTime"),
                                    new XElement("td", "Promoter"),
                                    new XElement("td", "Elements")
                                )
                            ),
                            new XElement("tbody",
                                from trans in Hist.Transactions
                                orderby _users.getUser(trans.User), trans.Time descending // by user with their latest transactions on top
                                select new XElement("tr",
                                    new XElement("td", trans.ID),
                                    new XElement("td", trans.Time.ToString("g")),
                                    new XElement("td", _users.getUser(trans.User) + " (" + trans.User + ")", new XElement("br"),
                                        business(trans.User), new XElement("br"), mobile(trans.User)),
                                    new XElement("td",
                                        new XElement("table",
                                            new XElement("caption", trans.Comment),
                                            new XAttribute("border", 1),
                                            new XAttribute("cellpadding", 3),
                                            new XAttribute("cellspacing", 2),
                                            new XAttribute("bordercolor", "#cccccc"),
                                            new XAttribute("style", "font family: Arial; font-size: 10pt"),
                                            new XAttribute("bgcolor", "#ffffff"),
                                            new XElement("thead",
                                                new XElement("tr",
                                                    new XElement("td", "EID"),
                                                    new XElement("td", "Element"),
                                                    new XElement("td", "Location"),
                                                    new XElement("td", "Real"),
                                                    new XElement("td", "Virtual")
                                                )
                                            ),
                                            new XElement("tbody",
                                                from ver in trans.Versions
                                                orderby Path.GetFileName(ver.Location)
                                                select new XElement("tr",
                                                    new XElement("td", ver.EID),
                                                    new XElement("td", Path.GetFileName(ver.Location)),
                                                    new XElement("td", Path.GetDirectoryName(ver.Location)),
                                                    new XElement("td", ver.Workspace + '/' + ver.RealVersionNumber),
                                                    new XElement("td", ver.Stream + '/' + ver.VirVersionNumber)
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );
        }

        // Get user's business phone number if available, otherwise an empty string.
        private static string business(string prncpl)
        {
            string phone = null;
            AcUser user = _users.getUser(prncpl);
            if (user != null)
                phone = user.Business;

            return (String.IsNullOrEmpty(phone)) ? String.Empty : phone + "(b)";
        }

        // Get user's mobile phone number if available, otherwise an empty string.
        // Demonstrates how to retrieve user properties beyond the regular default set.
        private static string mobile(string prncpl)
        {
            string phone = null;
            AcUser user = _users.getUser(prncpl);
            if (user != null)
                phone = user.Other.ContainsKey("Mobile") ? (string)user.Other["Mobile"] : null;

            return (String.IsNullOrEmpty(phone)) ? String.Empty : phone + "(m)";
        }

        // General program startup initialization.
        private static bool init()
        {
            // initialize our logging support so we can log errors
            if (!AcDebug.initAcLogging())
            {
                Console.WriteLine("Logging support initialization failed.");
                return false;
            }

            // in the event of an unhandled exception, save it to our log file before the program terminates
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(AcDebug.unhandledException);

            // ensure we're logged into AccuRev
            Task<string> prncpl = AcQuery.getPrincipalAsync();
            if (String.IsNullOrEmpty(prncpl.Result))
            {
                string msg = String.Format("Not logged into AccuRev.{0}Please login and try again.",
                    Environment.NewLine);
                AcDebug.Log(msg);
                return false;
            }

            // initialize our class variables from LatestPromotions.exe.config
            if (!initAppConfigData())
                return false; // initialization failure, see log file

            return true;
        }

        // Initialize our class variables with values from LatestPromotions.exe.config. Returns true if all values 
        // successfully read and class variables initialized, false otherwise. ConfigurationErrorsException caught 
        // and logged in %LOCALAPPDATA%\AcTools\Logs\LatestPromotions-YYYY-MM-DD.log on initialization failure.
        private static bool initAppConfigData()
        {
            bool ret = true; // assume success
            try
            {
                _fromHoursAgo = AcQuery.getAppConfigSetting<int>("FromHoursAgo");
                _outputFile = AcQuery.getAppConfigSetting<string>("OutputFile").Trim();

                ADSection adSection = ConfigurationManager.GetSection("activeDir") as ADSection;
                if (adSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating ADSection");
                    ret = false;
                }
                else
                {
                    _domains = adSection.Domains;
                    _properties = adSection.Props;
                }

                DepotsSection depotsConfigSection = ConfigurationManager.GetSection("Depots") as DepotsSection;
                if (depotsConfigSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating DepotsSection");
                    ret = false;
                }
                else
                    _selDepots = depotsConfigSection.Depots;

                StreamsSection streamsConfigSection = ConfigurationManager.GetSection("Streams") as StreamsSection;
                if (streamsConfigSection == null)
                {
                    AcDebug.Log("Error in Program.initAppConfigData creating StreamsSection");
                    ret = false;
                }
                else
                    _selStreams = streamsConfigSection.Streams;
            }

            catch (ConfigurationErrorsException exc)
            {
                Process currentProcess = Process.GetCurrentProcess();
                ProcessModule pm = currentProcess.MainModule;
                string exeFile = pm.ModuleName;
                string msg = String.Format("ConfigurationErrorsException caught and logged in Program.initAppConfigData{0}Invalid data in {1}.config{0}{2}",
                    Environment.NewLine, exeFile, exc.Message);
                AcDebug.Log(msg);
                ret = false;
            }

            return ret;
        }
    }
}
