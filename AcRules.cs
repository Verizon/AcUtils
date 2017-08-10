/*! \file
Copyright (C) 2016 Verizon. All Rights Reserved.

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
                        text = String.Format("SetInStream: {1}{0}Cross-link (basis): {2}{0}Location: {3}{0}Rule kind: {4}{0}Element type: {5}{0}", Environment.NewLine,
                            SetInStream, XlinkToStream, Location, Kind, Type);
                    else
                        text = String.Format("SetInStream: {1}{0}Location: {2}{0}Rule kind: {3}{0}Element type: {4}{0}", Environment.NewLine,
                            SetInStream, Location, Kind, Type);
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
                    throw new FormatException(String.Format("The {0} format string is not supported.", format));
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
        [NonSerialized] private readonly object _locker = new object();

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
            // show the rules barnyrd set on his workspaces throughout the repository
            AcDepots depots = new AcDepots();
            if (!(await depots.initAsync())) return false;

            List<Task<bool>> tasks = new List<Task<bool>>();
            List<AcRules> list = new List<AcRules>();
            foreach (AcDepot depot in depots.OrderBy(n => n)) // use default comparer
            {
                foreach (AcStream stream in depot.Streams.Where(n => n.Type == StreamType.workspace &&
                    n.Name.EndsWith("barnyrd")).OrderBy(n => n)) // workspace names always have principal name appended
                {
                    AcRules r = new AcRules(true); // true for explicitly-set rules only
                    tasks.Add(r.initAsync(stream));
                    list.Add(r);
                }
            }

            bool[] arr = await Task.WhenAll(tasks); // asynchronously await multiple asynchronous operations.. fast!
            if (arr == null || arr.Any(n => n == false)) // if one or more failed to initialize
                return false;

            foreach (AcRules rules in list)
                foreach (AcRule rule in rules.OrderBy(n => n)) // use default comparer
                    Console.WriteLine(rule);
            \endcode */
        /*! [Default comparer](@ref AcRule#CompareTo) */
        public AcRules(bool explicitOnly = false)
        {
            _explicitOnly = explicitOnly;
        }

        /// <summary>
        /// Populate this container with AcRule objects for \e stream as per constructor parameter \e explicitOnly.
        /// </summary>
        /// <param name="stream">The stream to query for rules.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /*! \sa [AcRules constructor](@ref AcUtils#AcRules#AcRules) */
        /*! \lsrules_ <tt>lsrules -s \<stream\> [-d] -fx</tt>  */
        public async Task<bool> initAsync(AcStream stream)
        {
            string cmd = String.Empty;
            if (_explicitOnly)
                // Include only rules that were explicitly set for the workspace or stream,
                // not rules that apply because they are inherited from higher level streams.
                cmd = String.Format(@"lsrules -s ""{0}"" -d -fx", stream);
            else
                // Include rules that are inherited from higher level streams.
                cmd = String.Format(@"lsrules -s ""{0}"" -fx", stream);

            return await runCmdAsync(cmd).ConfigureAwait(false);
        }

        /// <summary>
        /// Populate this container with AcRule objects for all streams in \e depot as per constructor parameter \e explicitOnly.
        /// </summary>
        /// <param name="depot">All streams in \e depot to query for rules.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /*! \sa [AcRules constructor](@ref AcUtils#AcRules#AcRules) */
        /*! \lsrules_ <tt>lsrules -s \<stream\> [-d] -fx</tt>  */
        public async Task<bool> initAsync(AcDepot depot)
        {
            string cmd = String.Empty;
            List<Task<bool>> tasks = new List<Task<bool>>(depot.Streams.Count());
            foreach (AcStream stream in depot.Streams)
            {
                if (_explicitOnly)
                    // Include only rules that were explicitly set for the workspace or stream,
                    // not rules that apply because they are inherited from higher level streams.
                    cmd = String.Format(@"lsrules -s ""{0}"" -d -fx", stream);
                else
                    // Include rules that are inherited from higher level streams.
                    cmd = String.Format(@"lsrules -s ""{0}"" -fx", stream);

                tasks.Add(runCmdAsync(cmd));
            }

            bool[] arr = await Task.WhenAll(tasks).ConfigureAwait(false);
            return (arr != null && arr.All(n => n == true)); // true if all succeeded
        }

        /// <summary>
        /// Populate this container with AcRule objects for select \e streams as per constructor parameter \e explicitOnly.
        /// </summary>
        /// <param name="streams">The list of streams to query for rules.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /*! \sa [AcRules constructor](@ref AcUtils#AcRules#AcRules) */
        /*! \lsrules_ <tt>lsrules -s \<stream\> [-d] -fx</tt>  */
        public async Task<bool> initAsync(StreamsCollection streams)
        {
            string cmd = String.Empty;
            List<Task<bool>> tasks = new List<Task<bool>>(streams.Count);
            foreach (StreamElement se in streams)
            {
                if (_explicitOnly)
                    // Include only rules that were explicitly set for the workspace or stream,
                    // not rules that apply because they are inherited from higher level streams.
                    cmd = String.Format(@"lsrules -s ""{0}"" -d -fx", se.Stream);
                else
                    // Include rules that are inherited from higher level streams.
                    cmd = String.Format(@"lsrules -s ""{0}"" -fx", se.Stream);

                tasks.Add(runCmdAsync(cmd));
            }

            bool[] arr = await Task.WhenAll(tasks).ConfigureAwait(false);
            return (arr != null && arr.All(n => n == true)); // true if all succeeded
        }

        /// <summary>
        /// Populate this container with AcRule objects for all streams in select \e depots as per constructor parameter \e explicitOnly.
        /// </summary>
        /// <param name="depots">The list of depots to query for rules.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /*! \sa [AcRules constructor](@ref AcUtils#AcRules#AcRules) */
        /*! \lsrules_ <tt>lsrules -s \<stream\> [-d] -fx</tt>  */
        public async Task<bool> initAsync(DepotsCollection depots)
        {
            AcDepots dlist = new AcDepots();
            if (!(await dlist.initAsync(depots).ConfigureAwait(false))) return false;

            string cmd = String.Empty;
            List<Task<bool>> tasks = new List<Task<bool>>();
            foreach (AcDepot d in dlist)
            {
                foreach (AcStream stream in d.Streams)
                {
                    if (_explicitOnly)
                        // Include only rules that were explicitly set for the workspace or stream,
                        // not rules that apply because they are inherited from higher level streams.
                        cmd = String.Format(@"lsrules -s ""{0}"" -d -fx", stream);
                    else
                        // Include rules that are inherited from higher level streams.
                        cmd = String.Format(@"lsrules -s ""{0}"" -fx", stream);

                    tasks.Add(runCmdAsync(cmd));
                }
            }

            bool[] arr = await Task.WhenAll(tasks).ConfigureAwait(false);
            return (arr != null && arr.All(n => n == true)); // true if all succeeded
        }
        //@}
        #endregion

        /// <summary>
        /// Helper function that runs the \c lsrules command sent by one of the initAsync overloads.
        /// </summary>
        /// <param name="cmd">AccuRev \c lsrules command to run.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c lsrules command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
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
        private async Task<bool> runCmdAsync(string cmd)
        {
            bool ret = false; // assume failure
            try
            {
                AcResult r = await AcCommand.runAsync(cmd).ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("element") select element;
                    foreach (XElement e in query)
                    {
                        AcRule rule = new AcRule();
                        string kind = (string)e.Attribute("kind");
                        rule.Kind = (RuleKind)Enum.Parse(typeof(RuleKind), kind);
                        string type = (string)e.Attribute("elemType");
                        rule.Type = (ElementType)Enum.Parse(typeof(ElementType), type);
                        rule.Location = (string)e.Attribute("location");
                        rule.SetInStream = (string)e.Attribute("setInStream");
                        rule.XlinkToStream = (e.Attribute("xlinkToStream") != null) ?
                            (string)e.Attribute("xlinkToStream") : null;
                        lock (_locker) { Add(rule); }
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcRules.runCmdAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcRules.runCmdAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
    }
}
