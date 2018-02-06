/*! \file
Copyright (C) 2016-2018 Verizon. All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;

namespace AcUtils
{
    /// <summary>
    /// AccuRev program return value and command result.
    /// </summary>
    [Serializable]
    public sealed class AcResult
    {
        #region class variables
        private int _retVal = -1;
        private string _cmdResult;
        #endregion

        #region Constructors
        /*! \name Constructors */
        /**@{*/
        /// <summary>
        /// Default constructor.
        /// </summary>
        public AcResult() { }

        /// <summary>
        /// Constructor for object initialization with \e retval and \e cmdresult params.
        /// </summary>
        /// <param name="retval">AccuRev program return value.</param>
        /// <param name="cmdresult">AccuRev program command result.</param>
        public AcResult(int retval, string cmdresult)
        {
            _retVal = retval;
            _cmdResult = cmdresult;
        }
        /**@}*/
        #endregion

        /// <summary>
        /// The AccuRev program return value for the command, otherwise <em>minus one (-1)</em> on error.
        /// </summary>
        public int RetVal
        {
            get { return _retVal; }
            set { _retVal = value; }
        }

        /// <summary>
        /// The command result (usually XML) emitted by AccuRev.
        /// </summary>
        public string CmdResult
        {
            get { return _cmdResult ?? String.Empty; }
            set { _cmdResult = value; }
        }
    }

    /// <summary>
    /// Implement to change the default logic used to determine if an AcUtilsException should be 
    /// thrown based on the command's AccuRev program return value and the version of AccuRev in use.
    /// </summary>
    public interface ICmdValidate
    {
        /// <summary>
        /// Defines the logic used to determine if an AcUtilsException should be 
        /// thrown based on the AccuRev program \e retval for \e command.
        /// </summary>
        /// <param name="command">The command that was issued.</param>
        /// <param name="retval">The AccuRev program return value for \e command.</param>
        /// <returns>\e true if \e command should succeed, otherwise \e false to have an AcUtilsException thrown.</returns>
        bool isValid(string command, int retval);
    }

    /// <summary>
    /// The default logic for determining if an AcUtilsException should be 
    /// thrown based on the command's AccuRev program return value.
    /// </summary>
    [Serializable]
    public class CmdValidate : ICmdValidate
    {
        /// <summary>
        /// Default logic used to determine if an AcUtilsException should be thrown based on 
        /// AccuRev's program \e retval for \e command and the version of AccuRev in use.
        /// </summary>
        /// <remarks>AccuRev's program return value is usually <em>zero (0)</em> for success and 
        /// <em>one (1)</em> for error, except in the case of <tt>merge</tt> and <tt>diff</tt> where 
        /// <em>zero (0)</em> for no conflicts/differences found, <em>one (1)</em> for conflicts/differences 
        /// found, and <em>two (2)</em> on program error. Also, starting with AccuRev 6.0, the 
        /// <tt>files</tt> command returns <em>zero (0)</em> for files found and <em>one (1)</em> for no files found.
        /// </remarks>
        /// <param name="command">The command that was issued.</param>
        /// <param name="retval">The AccuRev program return value for \e command.</param>
        /// <returns>\e true if \e command should succeed, otherwise \e false to have an AcUtilsException thrown.</returns>
        /*! \code
            if (retval == 0 || (retval == 1 && (
                command.StartsWith("diff", StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("merge", StringComparison.OrdinalIgnoreCase)))
            )
                return true;
            else
                return false;
            \endcode */
        /*! \sa [AcCommand.runAsync](@ref AcUtils#AcCommand#runAsync), [AcCommand.run](@ref AcUtils#AcCommand#run) */
        public virtual bool isValid(string command, int retval)
        {
            if (retval == 0 || (retval == 1 && (
                command.StartsWith("diff", StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("merge", StringComparison.OrdinalIgnoreCase)))
            )
                return true;
            else
                return false;
        }
    }

    /// <summary>
    /// AccuRev command processing.
    /// </summary>
    [Serializable]
    public static class AcCommand
    {
        #region class variables
        private static readonly int MaxConcurrencyDefault = 8; // default when ACUTILS_MAXCONCURRENT environment variable doesn't exist
        private static TaskFactory _taskFactory;
        #endregion

        /// <summary>
        /// Initialize our task scheduler that allows no more than n tasks to execute simultaneously.
        /// </summary>
        /*! \sa [LimitedConcurrencyLevelTaskScheduler](@ref System#Threading#Tasks#Schedulers#LimitedConcurrencyLevelTaskScheduler), 
        <a href="https://blogs.msdn.microsoft.com/pfxteam/2010/04/09/parallelextensionsextras-tour-7-additional-taskschedulers/">ParallelExtensionsExtras Tour - #7 - Additional TaskSchedulers</a> */
        static AcCommand()
        {
            try
            {
                string maxconcurrent = Environment.GetEnvironmentVariable("ACUTILS_MAXCONCURRENT");
                int max = (maxconcurrent != null) ? Int32.Parse(maxconcurrent) : MaxConcurrencyDefault;
                LimitedConcurrencyLevelTaskScheduler ts = new LimitedConcurrencyLevelTaskScheduler(max);
                _taskFactory = new TaskFactory(ts);
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcCommand constructor{Environment.NewLine}{ecx.Message}");
            }
        }

        /// <summary>Run the AccuRev \e command asynchronously with non-blocking I/O.</summary>
        /// <remarks>To reduce the risk of the AccuRev server becoming unresponsive due to an excess of commands, 
        /// the maximum number of commands that will run simultaneously for a client application is eight (8). 
        /// Other commands [are queued](@ref System#Threading#Tasks#Schedulers#LimitedConcurrencyLevelTaskScheduler) 
        /// until space is available. You can override this default value by creating the environment variable 
        /// \b ACUTILS_MAXCONCURRENT and specifying a different number, e.g. \c ACUTILS_MAXCONCURRENT=12.</remarks>
        /// <param name="command">The AccuRev command to run, e.g. <tt>hist -fx -p AcTools -t 453</tt></param>
        /// <param name="validator">Use to change the [default logic](@ref AcUtils#CmdValidate#isValid) for determining 
        /// if an AcUtilsException should be thrown based on AccuRev's program return value for \e command.</param>
        /// <returns>An AcResult object with AcResult.RetVal set to the AccuRev program return value and the command result 
        /// (usually XML) in AcResult.CmdResult. Returns \e null if an exception occurs.
        /// </returns>
        /// <exception cref="AcUtilsException">thrown on AccuRev program invocation failure or <tt>merge/diff</tt> 
        /// program error (return value <em>two (2)</em>).</exception>
        /// <exception cref="Win32Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on error spawning the AccuRev process that runs the command.</exception>
        /// <exception cref="InvalidOperationException">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \attention Do not use this function for the <tt>ismember</tt> command. Instead, use AcGroups#isMember. */
        public static async Task<AcResult> runAsync(string command, ICmdValidate validator = null)
        {
            TaskCompletionSource<AcResult> tcs = new TaskCompletionSource<AcResult>();
            StringBuilder error = new StringBuilder(512);
            try
            {
                await _taskFactory.StartNew(() =>
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = "accurev";
                        process.StartInfo.Arguments = command;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardInput = true; // fix for AccuRev defect 29059
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                        process.ErrorDataReceived += (sender, e) =>
                        {
                            error.AppendLine(e.Data);
                        };
                        process.Start();
                        process.BeginErrorReadLine();
                        Task<string> output = process.StandardOutput.ReadToEndAsync();
                        process.WaitForExit();
                        if (process.HasExited)
                        {
                            string err = error.ToString().Trim();
                            if (!String.IsNullOrEmpty(err) &&
                                !(String.Equals("You are not in a directory associated with a workspace", err)))
                            {
                                AcDebug.Log(err, false);
                            }

                            ICmdValidate validate = validator ?? new CmdValidate();
                            if (validate.isValid(command, process.ExitCode))
                            {
                                tcs.SetResult(new AcResult(process.ExitCode, output.Result));
                            }
                            else
                            {
                                tcs.SetException(new AcUtilsException($"AccuRev program return: {process.ExitCode}{Environment.NewLine}accurev {command}")); // let calling method handle
                            }
                        }
                    }
                }).ConfigureAwait(false);
            }

            catch (Win32Exception ecx)
            {
                string msg = String.Format(@"Win32Exception caught and logged in AcCommand.runAsync{0}{1}{0}""accurev {2}""{0}errorcode: {3}{0}native errorcode: {4}{0}{5}{0}{6}{0}{7}{0}{8}",
                    Environment.NewLine, ecx.Message, command, ecx.ErrorCode.ToString(), ecx.NativeErrorCode.ToString(), ecx.StackTrace, ecx.Source, ecx.GetBaseException().Message, error.ToString());
                AcDebug.Log(msg);
                tcs.SetException(ecx);
            }

            catch (InvalidOperationException ecx)
            {
                string msg = String.Format(@"InvalidOperationException caught and logged in AcCommand.runAsync{0}{1}{0}""accurev {2}""{0}{3}",
                    Environment.NewLine, ecx.Message, command, error.ToString());
                AcDebug.Log(msg);
                tcs.SetException(ecx);
            }

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>Run the AccuRev \e command synchronously (blocks) on the current thread.</summary>
        /// <param name="command">The AccuRev command to run, e.g. <tt>hist -fx -p AcTools -t 453</tt></param>
        /// <param name="validator">Use to change the [default logic](@ref AcUtils#CmdValidate#isValid) for determining 
        /// if an AcUtilsException should be thrown based on AccuRev's program return value for \e command.</param>
        /// <returns>On success an AcResult object with AcResult.RetVal set to the AccuRev program return value and the command 
        /// result (usually XML) in AcResult.CmdResult. Otherwise, AcResult.RetVal is <em>minus one (-1)</em> (default) on error.
        /// </returns>
        /// <exception cref="AcUtilsException">thrown on AccuRev program invocation failure or <tt>merge/diff</tt> 
        /// program error (return value <em>two (2)</em>).</exception>
        /// <exception cref="Win32Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on error spawning the AccuRev process that runs the command.</exception>
        /// <exception cref="InvalidOperationException">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \attention Do not use this function for the <tt>ismember</tt> command. Instead, use AcGroups#isMember. */
        public static AcResult run(string command, ICmdValidate validator = null)
        {
            AcResult result = new AcResult();
            StringBuilder error = new StringBuilder(512);
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "accurev";
                    process.StartInfo.Arguments = command;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardInput = true; // fix for AccuRev defect 29059
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        error.AppendLine(e.Data);
                    };
                    StringBuilder output = new StringBuilder();
                    output.Capacity = 4096;
                    process.OutputDataReceived += (sender, e) =>
                    {
                        output.AppendLine(e.Data);
                    };
                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    process.WaitForExit(); // blocks here. recommended to ensure output buffer has been flushed
                    if (process.HasExited)
                    {
                        string err = error.ToString().Trim();
                        if (!String.IsNullOrEmpty(err) &&
                            !(String.Equals("You are not in a directory associated with a workspace", err)))
                        {
                            AcDebug.Log(err, false);
                        }

                        ICmdValidate validate = validator ?? new CmdValidate();
                        if (validate.isValid(command, process.ExitCode))
                        {
                            result.RetVal = process.ExitCode;
                            result.CmdResult = output.ToString();
                        }
                        else
                        {
                            throw new AcUtilsException($"AccuRev program return: {process.ExitCode}{Environment.NewLine}accurev {command}"); // let calling method handle
                        }
                    }
                }
            }

            catch (Win32Exception ecx)
            {
                string msg = String.Format(@"Win32Exception caught and logged in AcCommand.run{0}{1}{0}""accurev {2}""{0}errorcode: {3}{0}native errorcode: {4}{0}{5}{0}{6}{0}{7}{0}{8}",
                    Environment.NewLine, ecx.Message, command, ecx.ErrorCode.ToString(), ecx.NativeErrorCode.ToString(), ecx.StackTrace, ecx.Source, ecx.GetBaseException().Message, error.ToString());
                AcDebug.Log(msg);
            }

            catch (InvalidOperationException ecx)
            {
                string msg = String.Format(@"InvalidOperationException caught and logged in AcCommand.run{0}{1}{0}""accurev {2}""{0}{3}",
                    Environment.NewLine, ecx.Message, command, error.ToString());
                AcDebug.Log(msg);
            }

            return result;
        }
    }
}
