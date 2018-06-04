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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcUtils
{
    #region enums
    /*! \ingroup acenum */
    ///@{
    /// <summary>
    /// The kind of AccuRev rule.
    /// </summary>
    public enum RuleKind {
        /*! \var unknown
        This should never occur. */
        unknown,
        /*! \var clear
        Remove an include/exclude rule. */
        clear,
        /*! \var incl
        The kind of rule is to \e include elements in a workspace or stream. */
        incl,
        /*! \var incldo
        The kind of rule is to include a directory and not its contents in a workspace or stream */
        incldo,
        /*! \var excl
        The kind of rule is to \e exclude elements from a workspace or stream. */
        excl
    };
    ///@}
    #endregion

    /// <summary>
    /// The attributes of a stream or workspace \e include or \e exclude rule: 
    /// [RuleKind](@ref AcUtils#RuleKind), [ElementType](@ref AcUtils#ElementType), 
    /// location affected by the rule, stream or workspace the rule was applied to, 
    /// and (if applicable) the basis stream for cross-links.
    /// </summary>
    [Serializable]
    public sealed class AcRule : IFormattable, IEquatable<AcRule>, IComparable<AcRule>, IComparable
    {
        #region class variables
        private RuleKind _kind = RuleKind.unknown;
        private ElementType _type = ElementType.unknown;
        private string _location;
        private string _setInStream;
        private string _xlinkToStream;
        #endregion

        /// <summary>
        /// Constructor used during AcRules list construction. It is called internally and not by user code. 
        /// </summary>
        internal AcRule() { }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcRule. 
        /// Values used to compare instances are [RuleKind](@ref AcUtils#RuleKind), depot-relative 
        /// path of the element the rule affects, and name of stream or workspace the rule is applied to.
        /// </summary>
        /// <param name="other">The AcRule object being compared to \e this instance.</param>
        /// <returns>\e true if AcRule \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcRule other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            var left = Tuple.Create(Kind, Location, SetInStream);
            var right = Tuple.Create(other.Kind, other.Location, other.SetInStream);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcRule)](@ref AcRule#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as AcRule);
        }

        /// <summary>
        /// Override appropriate for type AcRule.
        /// </summary>
        /// <returns>Hash of [RuleKind](@ref AcUtils#RuleKind), depot-relative path of the element 
        /// the rule affects, and name of stream or workspace the rule is applied to.</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(Kind, Location, SetInStream);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcRule objects. 
        /// Sorts by the stream or workspace name the rule is applied to and then location; 
        /// the depot-relative path of the element the rule affects.
        /// </summary>
        /// <param name="other">An AcRule object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcRule objects being compared.</returns>
        /*! \sa [AcRules constructor example](@ref AcUtils#AcRules#AcRules) */
        public int CompareTo(AcRule other)
        {
            int result;
            if (AcRule.ReferenceEquals(this, other))
                result = 0;
            else
            {
                result = String.Compare(SetInStream, other.SetInStream);
                if (result == 0)
                    result = String.Compare(Location, other.Location);
            }

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcRule object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcRule)](@ref AcRule#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcRule object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcRule))
                throw new ArgumentException("Argument is not an AcRule", "other");
            AcRule o = (AcRule)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Kind of AccuRev rule in use.
        /// </summary>
        public RuleKind Kind
        {
            get { return _kind; }
            internal set { _kind = value; }
        }

        /// <summary>
        /// Type of element the rule was placed on: \e dir, \e text, \e binary, \e ptext, \e elink, or \e slink.
        /// </summary>
        public ElementType Type
        {
            get { return _type; }
            internal set { _type = value; }
        }

        /// <summary>
        /// Depot-relative path of the element the rule affects.
        /// </summary>
        public string Location
        {
            get { return _location ?? String.Empty; }
            internal set { _location = value; }
        }

        /// <summary>
        /// Name of stream or workspace the rule is applied to.
        /// </summary>
        public string SetInStream
        {
            get { return _setInStream ?? String.Empty; }
            internal set { _setInStream = value; }
        }

        /// <summary>
        /// In the case of a cross-link this is the basis stream for SetInStream.
        /// </summary>
        public string XlinkToStream
        {
            get { return _xlinkToStream ?? String.Empty; }
            internal set { _xlinkToStream = value; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(rule.ToString("k"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Long version and default when not using a format specifier.
        /// \arg \c K [RuleKind](@ref AcUtils#RuleKind) - Kind of rule in use.
        /// \arg \c T [ElementType](@ref AcUtils#ElementType) - Type of element the rule was placed on: \e dir, \e text, \e binary, \e ptext, \e elink, or \e slink.
        /// \arg \c L Location - Depot-relative path of the element the rule affects.
        /// \arg \c S SetInStream - Stream or workspace the rule is applied to.
        /// \arg \c X XlinkToStream - If cross-link the basis stream for the SetInStream.
        public string ToString(string format, IFormatProvider provider)
        {
            if (provider != null)
            {
                ICustomFormatter fmt = provider.GetFormat(this.GetType()) as ICustomFormatter;
                if (fmt != null)
                    return fmt.Format(format, this, provider);
            }

            if (String.IsNullOrEmpty(format))
                format = "G";

            switch (format.ToUpperInvariant())
            {
                case "G":
                {
                    string text;
                    if (!String.IsNullOrEmpty(_xlinkToStream))
                        text = $"SetInStream: {SetInStream}{Environment.NewLine}" +
                                $"Cross-link (basis): {XlinkToStream}{Environment.NewLine}" +
                                $"Location: {Location}{Environment.NewLine}" +
                                $"Rule kind: {Kind}{Environment.NewLine}" +
                                $"Element type: {Type}{Environment.NewLine}";
                    else
                        text = $"SetInStream: {SetInStream}{Environment.NewLine}" +
                                $"Location: {Location}{Environment.NewLine}" +
                                $"Rule kind: {Kind}{Environment.NewLine}" +
                                $"Element type: {Type}{Environment.NewLine}";
                    return text;
                }
                case "K": // kind of rule in use
                    return Kind.ToString();
                case "T": // type of element the rule was placed on
                    return Type.ToString();
                case "L": // depot-relative path of the element the rule affects
                    return Location;
                case "S": // stream or workspace the rule is applied to
                    return SetInStream;
                case "X": // if cross-link the basis stream for the SetInStream
                    return XlinkToStream;
                default:
                    throw new FormatException($"The {format} format string is not supported.");
            }
        }

        // Calls ToString(string, IFormatProvider) version with a null IFormatProvider argument.
        public string ToString(string format)
        {
            return ToString(format, null);
        }

        // Calls ToString(string, IFormatProvider) version with the general format and a null IFormatProvider argument.
        public override string ToString()
        {
            return ToString("G", null);
        }
        #endregion ToString
    }

    /// <summary>
    /// A container of AcRule objects that define the attributes of stream and workspace \e include and \e exclude rules.
    /// </summary>
    /*! \accunote_ The <tt>lsrules -t</tt> option to display the rules set that existed as of a specified time does not work. AccuRev defect 22659.
        \accunote_ The <tt>-l \<file\></tt> option for \c mkrules will crash the server when the file argument contains bad XML. AccuRev defect 26252. */
    [Serializable]
    public sealed class AcRules : List<AcRule>
    {
        private bool _explicitOnly;
        [NonSerialized] private readonly object _locker = new object(); // token for lock keyword scope
        [NonSerialized] private int _counter; // used to report initialization progress back to the caller

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container of AcRule objects that define the attributes of stream and workspace \e include and \e exclude rules.
        /// </summary>
        /// <param name="explicitOnly">\e false to include rules inherited from higher-level streams, 
        /// \e true to include only rules that were explicitly set for the workspace or stream and 
        /// not those inherited from higher level streams. 
        /// </param>
        /*! \code
            // show rules explicitly set on dynamic streams in depot name
            public static async Task<bool> showRulesAsync(string name)
            {
                AcDepot depot = new AcDepot(name, dynamicOnly: true); // dynamic streams only
                if (!(await depot.initAsync())) return false;

                AcRules rules = new AcRules(explicitOnly: true); // exclude rules inherited from higher-level streams
                if (!(await rules.initAsync(depot))) return false;

                foreach(AcRule rule in rules.OrderBy(n => n))
                    Console.WriteLine(rule);

                return true;
            }
            \endcode */
        /*! \sa [Default comparer](@ref AcRule#CompareTo), <a href="_show_rules_8cs-example.html">ShowRules.cs</a> */
        public AcRules(bool explicitOnly = false)
        {
            _explicitOnly = explicitOnly;
        }

        /// <summary>
        /// Populate this container with AcRule objects for \e stream as per 
        /// [constructor parameter](@ref AcUtils#AcRules#AcRules) \e explicitOnly.
        /// </summary>
        /// <param name="stream">The stream to query for rules.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) in 
        /// <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c lsrules command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \lsrules_ <tt>lsrules -s \<stream\> [-d] -fx</tt>  */
        /*! \code
            <!-- accurev lsrules -s "PG_DEV1" -fx -->
            <AcResponse
              Command="lscomp"
              TaskId="54394">
              <element
                kind="incl"
                elemType="dir"
                dir="yes" // Not used as its redundant. We use ElementType.Dir instead.
                location="\.\Iconic"
                setInStream="PG_DEV1"
                xlinkToStream="IC_DEV1"
                options="1"/> // Obsolete. Was used for compatibility of rules in version 4.5.3 and earlier. Will always be a value of 1.
              <element
                kind="incl"
                elemType="dir"
                dir="yes"
                location="\.\"
                setInStream="PlayGround"
                options="1"/>
            </AcResponse>
            \endcode */
        public async Task<bool> initAsync(AcStream stream)
        {
            bool ret = false; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync($@"lsrules -s ""{stream}"" {(_explicitOnly ? "-d" : String.Empty)} -fx")
                    .ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    foreach (XElement e in xml.Elements("element"))
                    {
                        AcRule rule = new AcRule();
                        string kind = (string)e.Attribute("kind");
                        rule.Kind = (RuleKind)Enum.Parse(typeof(RuleKind), kind);
                        string type = (string)e.Attribute("elemType");
                        rule.Type = (ElementType)Enum.Parse(typeof(ElementType), type);
                        rule.Location = (string)e.Attribute("location");
                        rule.SetInStream = (string)e.Attribute("setInStream");
                        rule.XlinkToStream = (string)e.Attribute("xlinkToStream") ?? String.Empty;
                        lock (_locker) { Add(rule); }
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                AcDebug.Log($"AcUtilsException caught and logged in AcRules.initAsync(AcStream){Environment.NewLine}{ecx.Message}");
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcRules.initAsync(AcStream){Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        /// <summary>
        /// Populate this container with AcRule objects for all streams in \e depot 
        /// as per [constructor parameter](@ref AcUtils#AcRules#AcRules) \e explicitOnly.
        /// </summary>
        /// <param name="depot">All streams in \e depot to query for rules.</param>
        /// <param name="progress">Optionally report progress back to the caller.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) in 
        /// <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public async Task<bool> initAsync(AcDepot depot, IProgress<int> progress = null)
        {
            bool ret = false; // assume failure
            try
            {
                int num = depot.Streams.Count();
                List<Task<bool>> tasks = new List<Task<bool>>(num);
                Func<Task<bool>, bool> cf = t =>
                {
                    bool res = t.Result;
                    if (res && progress != null) progress.Report(Interlocked.Increment(ref _counter));
                    return res;
                };

                foreach (AcStream stream in depot.Streams)
                {
                    Task<bool> t = initAsync(stream).ContinueWith(cf);
                    tasks.Add(t);
                }

                bool[] arr = await Task.WhenAll(tasks).ConfigureAwait(false);
                ret = (arr != null && arr.All(n => n == true)); // true if all succeeded
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcRules.initAsync(AcDepot){Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        /// <summary>
        /// Populate this container with AcRule objects for all streams in \e streamsCol 
        /// as per [constructor parameter](@ref AcUtils#AcRules#AcRules) \e explicitOnly.
        /// </summary>
        /// <param name="streamsCol">The list of streams to query for rules.</param>
        /// <param name="progress">Optionally report progress back to the caller.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) in 
        /// <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public async Task<bool> initAsync(StreamsCollection streamsCol, IProgress<int> progress = null)
        {
            bool ret = false; // assume failure
            try
            {
                AcDepots depots = new AcDepots();
                if (!(await depots.initAsync(null, progress).ConfigureAwait(false))) return false;
                int num = 0; // get number of streams for tasks list
                foreach (AcDepot depot in depots)
                {
                    IEnumerable<AcStream> filter = depot.Streams.Where(s =>
                        streamsCol.OfType<StreamElement>().Any(se => s.Name == se.Stream));
                    num += filter.Count();
                }

                List<Task<bool>> tasks = new List<Task<bool>>(num);
                Func<Task<bool>, bool> cf = t =>
                {
                    bool res = t.Result;
                    if (res && progress != null) progress.Report(Interlocked.Increment(ref _counter));
                    return res;
                };

                foreach (AcDepot depot in depots)
                {
                    IEnumerable<AcStream> filter = depot.Streams.Where(s =>
                        streamsCol.OfType<StreamElement>().Any(se => s.Name == se.Stream));
                    foreach (AcStream stream in filter)
                    {
                        Task<bool> t = initAsync(stream).ContinueWith(cf);
                        tasks.Add(t);
                    }
                }

                bool[] arr = await Task.WhenAll(tasks).ConfigureAwait(false);
                ret = (arr != null && arr.All(n => n == true)); // true if all succeeded
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcRules.initAsync(StreamsCollection){Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }

        /// <summary>
        /// Populate this container with AcRule objects for all streams in \e depotsCol 
        /// as per [constructor parameter](@ref AcUtils#AcRules#AcRules) \e explicitOnly.
        /// </summary>
        /// <param name="depotsCol">The list of depots to query for rules.</param>
        /// <param name="progress">Optionally report progress back to the caller.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) in 
        /// <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        public async Task<bool> initAsync(DepotsCollection depotsCol, IProgress<int> progress = null)
        {
            bool ret = false; // assume failure
            try
            {
                AcDepots depots = new AcDepots();
                if (!(await depots.initAsync(depotsCol, progress).ConfigureAwait(false))) return false;
                int num = 0; // get number of streams for tasks list
                foreach (AcDepot depot in depots)
                    num += depot.Streams.Count();
                List<Task<bool>> tasks = new List<Task<bool>>(num);
                Func<Task<bool>, bool> cf = t =>
                {
                    bool res = t.Result;
                    if (res && progress != null) progress.Report(Interlocked.Increment(ref _counter));
                    return res;
                };

                foreach (AcStream stream in depots.SelectMany(d => d.Streams))
                {
                    Task<bool> t = initAsync(stream).ContinueWith(cf);
                    tasks.Add(t);
                }

                bool[] arr = await Task.WhenAll(tasks).ConfigureAwait(false);
                ret = (arr != null && arr.All(n => n == true));
            }

            catch (Exception ecx)
            {
                AcDebug.Log($"Exception caught and logged in AcRules.initAsync(DepotsCollection){Environment.NewLine}{ecx.Message}");
            }

            return ret;
        }
        //@}
        #endregion
    }
}
