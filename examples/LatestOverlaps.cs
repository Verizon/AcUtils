/* Copyright (C) 2018 Verizon. All Rights Reserved.

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
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using AcUtils;

namespace LatestOverlaps
{
    class Program
    {
        #region class variables
        private static DomainCollection _domains;
        private static PropCollection _properties;
        private static DepotsCollection _selDepots;
        private static int _fromHoursAgo;
        private static string _outputFile;
        private static AcUsers _users;
        private static AcDepots _depots;
        private static List<XElement> _hist;
        #endregion

        static int Main(string[] args)
        {
            // general program startup initialization
            if (!init()) return 1; // initialization failure, check log file
            return (reportAsync().Result) ? 0 : 1;
        }

        private static async Task<bool> reportAsync()
        {
            DateTime past = DateTime.Now.AddHours(_fromHoursAgo * -1); // go back this many hours
            if (!(await initHistAsync(past))) return false;
            if (_hist != null && _hist.Count > 0)
            {
                if (!(await initStatAsync())) return false;
                XmlWriter writer = null;
                try
                {
                    XmlWriterSettings settings = new XmlWriterSettings {
                        OmitXmlDeclaration = true, Indent = true, IndentChars = "\t",
                        Encoding = new UTF8Encoding(false) // false to exclude Unicode byte order mark (BOM)
                    };
                    using (writer = XmlWriter.Create(_outputFile, settings))
                    {
                        XDocument report = buildReport(past);
                        report.WriteTo(writer);
                    }
                }

                catch (Exception exc)
                {
                    AcDebug.Log($"Exception caught and logged in Program.reportAsync{Environment.NewLine}" +
                        $"Failed writing to {_outputFile}{Environment.NewLine}{exc.Message}");
                    return false;
                }

                finally { if (writer != null) writer.Close(); }
            }

            return true;
        }

        // Run the hist command for depots listed in LatestOverlaps.exe.config and add the results to 
        // our history list class variable. Returns true if initialization succeeded, false otherwise. 
        // AcUtilsException caught and logged in %LOCALAPPDATA%\AcTools\Logs\LatestOverlaps-YYYY-MM-DD.log 
        // on hist command failure. Exception caught and logged in same for a range of exceptions.
        private static async Task<bool> initHistAsync(DateTime past)
        {
            bool ret = false; // assume failure
            try
            {
                string timeHrsAgo = AcDateTime.DateTime2AcDate(past); // get date in string format suitable for AccuRev CLI
                List<Task<AcResult>> tasks = new List<Task<AcResult>>(_depots.Count);
                foreach (AcDepot depot in _depots)
                    tasks.Add(AcCommand.runAsync($@"hist -k promote -p ""{depot}"" -t now-""{timeHrsAgo}"" -fex"));

                _hist = new List<XElement>(_depots.Count);
                while (tasks.Count > 0)
                {
                    Task<AcResult> r = await Task.WhenAny(tasks);
                    tasks.Remove(r);
                    if (r == null || r.Result.RetVal != 0) return false;
                    XElement xml = XElement.Parse(r.Result.CmdResult);
                    _hist.Add(xml);
                }

                ret = true; // if we're here then all completed successfully
            }

            catch (AcUtilsException exc)
            {
                AcDebug.Log($"AcUtilsException caught and logged in Program.initHistAsync{Environment.NewLine}{exc.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.initHistAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // Run the stat command for streams in depots listed in LatestOverlaps.exe.config and add the 
        // results to the Stat.Elements list. Returns true if initialization succeeded, false otherwise. 
        // AcUtilsException caught and logged in %LOCALAPPDATA%\AcTools\Logs\LatestOverlaps-YYYY-MM-DD.log 
        // on stat command failure. Exception caught and logged in same for a range of exceptions.
        private static async Task<bool> initStatAsync()
        {
            bool ret = false; // assume failure
            try
            {
                IEnumerable<XElement> trans = from e in _hist.Elements("transaction")
                                              select e;
                ILookup<string, XElement> map = trans.ToLookup(n => (string)n.Attribute("streamName"), n => n);

                List<Task<AcResult>> tasks = new List<Task<AcResult>>();
                foreach (var ii in map) // for each stream
                {
                    AcStream stream = _depots.getStream(ii.Key);
                    if (!stream.HasDefaultGroup) continue; // run stat only if default group exists
                    tasks.Add(AcCommand.runAsync($@"stat -s ""{stream}"" -o -fx"));
                }

                bool op = true;
                while (tasks.Count > 0 && op)
                {
                    Task<AcResult> r = await Task.WhenAny(tasks);
                    tasks.Remove(r);
                    op = (r != null && r.Result.RetVal == 0 && Stat.init(r.Result.CmdResult));
                }

                ret = op; // true if all completed successfully
            }

            catch (AcUtilsException exc)
            {
                AcDebug.Log($"AcUtilsException caught and logged in Program.initStatAsync{Environment.NewLine}{exc.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in Program.initStatAsync{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // Returns the content for our HTML file.
        private static XDocument buildReport(DateTime past)
        {
            // transactions where one or more versions have overlap status
            IEnumerable<XElement> trans = from t in _hist.Elements("transaction")
                                          where Stat.getElement(t.Element("version")) != null
                                          select t;
            ILookup<string, XElement> map = trans.ToLookup(n => (string)n.Attribute("streamName"), n => n);

            XDocument doc = new XDocument(
                new XDocumentType("HTML", null, null, null),
                new XElement("html", new XAttribute("lang", "en"),
                new XElement("head",
                    new XElement("meta", new XAttribute("charset", "utf-8")), new XElement("meta", new XAttribute("name", "description"),
                        new XAttribute("content", "Promotions to select depots within the past specified number of hours that have versions with overlap status.")),
                    new XElement("title", "Promotions with overlaps since " + past.ToString("f")),
                    new XElement("style",
                    $@"body {{
                    	background-color: Gainsboro;
                    	color: #0000ff;
                    	font-family: Arial, sans-serif;
                    	font-size: 13px;
                    	margin: 10px; }}
                    table {{
                    	background-color: #F1F1F1;
                    	color: #000000;
                    	font-size: 12px;
                    	border: 2px solid black;
                    	padding: 10px; }}"
                    )
                ),
                new XElement("body",
                    new XElement("p", "Promotions with overlaps since " + past.ToString("f") + " exist in:"),
                    new XElement("ul",
                        from s in map
                        orderby s.Key // stream name
                        select new XElement("li", s.Key)),
                        new XElement("table",
                            new XElement("thead",
                                new XElement("tr",
                                    new XElement("td", "TransID"),
                                    new XElement("td", "TransTime"),
                                    new XElement("td", "Promoter"),
                                    new XElement("td", "Elements")
                                )
                            ),
                            new XElement("tbody",
                                from t in trans
                                orderby _users.getUser((string)t.Attribute("user")), t.acxTime("time") descending // by user with their latest transactions on top
                                select new XElement("tr",
                                    new XElement("td", (int)t.Attribute("id")),
                                    new XElement("td", ((DateTime)t.acxTime("time")).ToString("g")),
                                    new XElement("td", _users.getUser((string)t.Attribute("user")) + " (" + (string)t.Attribute("user") + ")", new XElement("br"),
                                        business((string)t.Attribute("user")), new XElement("br"), mobile((string)t.Attribute("user"))),
                                    new XElement("td",
                                        new XElement("table",
                                            new XElement("caption", t.acxComment()),
                                            new XElement("thead",
                                                new XElement("tr",
                                                    new XElement("td", "EID"),
                                                    new XElement("td", "Element"),
                                                    new XElement("td", "Location"),
                                                    new XElement("td", "Real"),
                                                    new XElement("td", "Virtual"),
                                                    new XElement("td", "Status")
                                                )
                                            ),
                                            new XElement("tbody",
                                                from v in t.Elements("version")
                                                where (int?)v.Attribute("eid") != null
                                                orderby Path.GetFileName((string)v.Attribute("path"))
                                                select new XElement("tr",
                                                    new XElement("td", (int)v.Attribute("eid")),
                                                    new XElement("td", Path.GetFileName((string)v.Attribute("path"))),
                                                    new XElement("td", Path.GetDirectoryName((string)v.Attribute("path"))),
                                                    new XElement("td", $"{(string)v.Attribute("realNamedVersion")} ({(string)v.Attribute("real")})"),
                                                    new XElement("td", $"{(string)v.Attribute("virtualNamedVersion")} ({(string)v.Attribute("virtual")})"),
                                                    new XElement("td", (Stat.getElement(v) != null ? Stat.getElement(v).Status : "(earlier)"))
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

            return doc;
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
                AcDebug.Log($"Not logged into AccuRev.{Environment.NewLine}Please login and try again.");
                return false;
            }

            // initialize our class variables from LatestOverlaps.exe.config
            if (!initAppConfigData()) return false;

            _depots = new AcDepots(dynamicOnly: true); // dynamic streams only
            Task<bool> dini = _depots.initAsync(_selDepots);

            // exclude group membership initialization, include deactivated users
            _users = new AcUsers(_domains, _properties, includeGroupsList: false, includeDeactivated: true);
            Task<bool> uini = _users.initAsync();

            Task<bool[]> arr = Task.WhenAll(dini, uini); // initialize both in parallel
            if (arr == null || arr.Result.Any(n => n == false)) return false;

            return true;
        }

        // Initialize our class variables with values from LatestOverlaps.exe.config. Returns true if all values 
        // successfully read and class variables initialized, false otherwise. ConfigurationErrorsException caught 
        // and logged in %LOCALAPPDATA%\AcTools\Logs\LatestOverlaps-YYYY-MM-DD.log on initialization failure.
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
            }

            catch (ConfigurationErrorsException exc)
            {
                Process currentProcess = Process.GetCurrentProcess();
                ProcessModule pm = currentProcess.MainModule;
                AcDebug.Log($"Invalid data in {pm.ModuleName}.config{Environment.NewLine}{exc.Message}");
                ret = false;
            }

            return ret;
        }
    }
}
