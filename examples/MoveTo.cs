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

// Required references: AcUtils.dll, System, System.Configuration, System.Xml.Linq
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcUtils;

namespace MoveTo
{
    class Program
    {
        #region class variables
        // list of element statuses from MoveTo.exe.config that should be ignored
        private static string[] _skipOver;
        #endregion

        // Returns one (1) on program failure or no elements found to move, zero (0) on success.
        static int Main(string[] args)
        {
            bool ret = false; // assume failure
            if (args.Length != 1)
            {
                Console.WriteLine(@"Example usage: C:\Workspaces\MARS_DEV2\Foo>moveto ..\Bar");
                return 1;
            }

            if (!init()) return 1; // program startup initialization

            string destfolder = args[0];
            string tempfile;
            if (store(destfolder, out tempfile)) // get elements to be moved
            {
                if (ready(destfolder)) // ensure folder exists and is in AccuRev
                {
                    try
                    {
                        AcResult r = AcCommand.run($@"move -l ""{tempfile}"" ""{destfolder}""");
                        ret = (r.RetVal == 0);
                    }

                    catch (AcUtilsException ecx)
                    {
                        Console.WriteLine($"AcUtilsException caught in Program.Main{Environment.NewLine}{ecx.Message}");
                    }
                }
            }

            if (tempfile != null) File.Delete(tempfile);
            return ret ? 0 : 1;
        }

        // Store the list of elements for the move operation in a temp file.
        // Returns true if the operation succeeded, otherwise false on error or if no elements found.
        private static bool store(string destfolder, out string tempfile)
        {
            tempfile = null;
            bool ret = false; // assume failure
            try
            {
                AcResult r = AcCommand.run("stat -fax *"); // the current directory
                if (r.RetVal == 0) // if command succeeded
                {
                    string fullpath = Path.GetFullPath(destfolder); // in case the relative path was given
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> filter = from e in xml.Elements("element")
                                                   where !_skipOver.Any(s => e.Attribute("status").Value.Contains(s)) &&
                                                   // avoid: "You cannot move an element into itself."
                                                   !fullpath.Equals((string)e.Attribute("location"), StringComparison.OrdinalIgnoreCase)
                                                   select e;
                    tempfile = Path.GetTempFileName();
                    using (StreamWriter sw = new StreamWriter(tempfile))
                    {
                        foreach (XElement e in filter)
                            sw.WriteLine((string)e.Attribute("location"));
                    }

                    FileInfo fi = new FileInfo(tempfile);
                    ret = fi.Length > 0;
                }
            }

            catch (AcUtilsException ecx)
            {
                Console.WriteLine($"AcUtilsException caught in Program.store{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                Console.WriteLine($"Exception caught in Program.store{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // Ensure that the destination folder exists and is in AccuRev.
        // Returns true if the operation succeeded, false on error.
        private static bool ready(string dest)
        {
            bool ret = false; // assume failure
            try
            {
                if (!Directory.Exists(dest))
                {
                    Directory.CreateDirectory(dest);
                    AcCommand.run($@"add ""{dest}""");
                }
                else
                {
                    AcResult r = AcCommand.run($@"stat -fx ""{dest}""");
                    if (r.RetVal == 0)
                    {
                        XElement xml = XElement.Parse(r.CmdResult);
                        string status = (string)xml.Element("element").Attribute("status");
                        if (status == "(external)")
                            AcCommand.run($@"add ""{dest}""");
                    }
                }

                ret = true;
            }

            catch (AcUtilsException ecx)
            {
                Console.WriteLine($"AcUtilsException caught in Program.ready{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                Console.WriteLine($"Exception caught in Program.ready{Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        // General program startup initialization. Returns true if the operation succeeded, false otherwise.
        private static bool init()
        {
            try
            {
                // ensure we're logged into AccuRev
                Task<string> prncpl = AcQuery.getPrincipalAsync();
                if (String.IsNullOrEmpty(prncpl.Result))
                {
                    Console.WriteLine($"Not logged into AccuRev.{Environment.NewLine}Please login and try again.");
                    return false;
                }

                if (!isCurrDirInWSpace())
                {
                    Console.WriteLine($"No workspace found for location {Environment.CurrentDirectory}");
                    return false;
                }

                char[] sep = new char[] { };
                string temp = AcQuery.getAppConfigSetting<string>("SkipOver").Trim();
                if (!String.IsNullOrEmpty(temp))
                    _skipOver = temp.Split(sep);
                else
                    _skipOver = new string[] { };
            }

            catch (ConfigurationErrorsException exc)
            {
                Process currentProcess = Process.GetCurrentProcess();
                ProcessModule pm = currentProcess.MainModule;
                Console.WriteLine($"Invalid data in {pm.ModuleName}.config{Environment.NewLine}{exc.Message}");
            }

            catch (Exception ecx)
            {
                Console.WriteLine($"Exception caught in Program.init{Environment.NewLine}{ecx.Message}");
            }

            return true;
        }

        // Determines whether the user's default directory is located somewhere in the current workspace tree.
        // Returns true if the operation succeeded, false otherwise.
        private static bool isCurrDirInWSpace()
        {
            bool found = false; // assume no workspace found
            try
            {
                AcResult r = AcCommand.run("info");
                if (r.RetVal == 0)
                {
                    using (StringReader sr = new StringReader(r.CmdResult))
                    {
                        string line;
                        char[] sep = new char[] { ':' };
                        while ((line = sr.ReadLine()) != null)
                        {
                            string[] arr = line.Split(sep); // "Workspace/ref:      MARS_DEV2_barnyrd"
                            if (arr.Length == 2)
                            {
                                if (String.Equals(arr[0], "Workspace/ref"))
                                {
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            catch (AcUtilsException ecx)
            {
                Console.WriteLine($"AcUtilsException caught in Program.isCurrDirInWSpace{Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                Console.WriteLine($"Exception caught in Program.isCurrDirInWSpace{Environment.NewLine}{ecx.Message}");
            }

            return found;
        }
    }
}
