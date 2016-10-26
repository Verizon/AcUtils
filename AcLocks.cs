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
    /// The kind of AccuRev \c lock.
    /// </summary>
    public enum LockKind {
        /*! \var from
        A \e from lock on the stream. */
        from,
        /*! \var to
        A \e to lock on the stream. */
        to,
        /*! \var all
        Both \e to and \e from lock on the stream. */
        all
    };

    /*! \ingroup acenum */
    /// <summary>
    /// Whether the \c lock is for a user or a group.
    /// </summary>
    public enum PrinType
    {
        /*! \var group
        Lock applies to a group. */
        group,
        /*! \var user
        Lock applies to a user. */
        user,
        /*! \var none
        Lock does not apply to a principal. */
        none
    };
    ///@}

    /*! \ingroup acenum */
    /// <summary>
    /// Lock applies to the principal only or to all except the principal.
    /// </summary>
    public enum OnlyExcept {
        /*! \var Only
        Lock applies to the principal only. */
        Only,
        /*! \var Except
        Lock applies to all except the principal. */
        Except
    };
    ///@}
    #endregion

    /// <summary>
    /// A lock object that defines the attributes of an AccuRev lock: 
    /// The stream name the lock is on and manner ([from, to, all](@ref AcUtils#LockKind)), 
    /// the principal name and [type it applies to](@ref AcUtils#PrinType), and how 
    /// it's applied ([except-for, only-for](@ref AcUtils#OnlyExcept)).
    /// </summary>
    /// <remarks>AcLock objects are instantiated during AcLocks construction.</remarks>
    [Serializable]
    public sealed class AcLock : IFormattable, IEquatable<AcLock>, IComparable<AcLock>, IComparable
    {
        #region class variables
        // Kind and Name are the only ones that always exist in the XML.
        // Others exist only if there are values for them.
        private LockKind _kind;
        private string _name;
        private PrinType _type;
        private string _exceptFor;
        private string _onlyFor;
        private string _comment;
        #endregion

        /// <summary>
        /// Constructor used during AcLocks list construction. It is called internally and not by user code. 
        /// </summary>
        internal AcLock() {}

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcLock. 
        /// Uses stream name the lock is on and manner ([from, to, all](@ref AcUtils#LockKind)) to compare instances.
        /// </summary>
        /// <param name="other">The AcLock object being compared to \e this instance.</param>
        /// <returns>\e true if AcLock \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcLock other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            // [stream], [from|to|all]
            var left = Tuple.Create(Name, Kind);
            var right = Tuple.Create(other.Name, other.Kind);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcLock)](@ref AcLock#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return Equals(other as AcLock);
        }

        /// <summary>
        /// Override appropriate for type AcLock.
        /// </summary>
        /// <returns>Hash of stream name and [LockKind](@ref AcUtils#LockKind).</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(Name, Kind);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcLock objects to sort 
        /// by [LockKind](@ref AcUtils#LockKind) (descending) then stream name.
        /// </summary>
        /// <param name="other">An AcLock object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcLock objects being compared.</returns>
        /*! \sa [OrderBy example](@ref AcUtils#AcLocks#AcLocks) */
        public int CompareTo(AcLock other)
        {
            int result;
            if (AcLock.ReferenceEquals(this, other))
                result = 0;
            else
            {
                result = (-1 * Kind.CompareTo(other.Kind)); // descending
                if (result == 0)
                    result = String.Compare(Name, other.Name);
            }

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcLock object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcLock)](@ref AcLock#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcLock object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcLock))
                throw new ArgumentException("Argument is not an AcLock", "other");
            AcLock o = (AcLock)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Kind of AccuRev lock: \e from, \e to, or \e all.
        /// </summary>
        public LockKind Kind
        {
            get { return _kind; }
            internal set { _kind = value; }
        }

        /// <summary>
        /// Name of stream the lock applies to.
        /// </summary>
        public string Name
        {
            get { return _name ?? String.Empty; }
            internal set { _name = value; }
        }

        /// <summary>
        /// Whether the lock is for a \e user or \e group.
        /// </summary>
        public PrinType Type
        {
            get { return _type; }
            internal set { _type = value; }
        }

        /// <summary>
        /// Lock applies to all \e except this principal.
        /// </summary>
        public string ExceptFor
        {
            get { return _exceptFor ?? String.Empty; }
            internal set { _exceptFor = value; }
        }

        /// <summary>
        /// Lock applies \e only to this principal.
        /// </summary>
        public string OnlyFor
        {
            get { return _onlyFor ?? String.Empty; }
            internal set { _onlyFor = value; }
        }

        /// <summary>
        /// Comment given to the lock.
        /// </summary>
        public string Comment
        {
            get { return _comment ?? String.Empty; }
            internal set { _comment = value; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(rule.ToString("e"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Default when not using a format specifier.
        /// \arg \c K Kind - Kind of AccuRev lock: \e from, \e to, or \e all.
        /// \arg \c N Name - Name of stream the lock applies to.
        /// \arg \c T Type - Whether the lock is for a \e user or \e group.
        /// \arg \c E ExceptFor - Lock applies to all \e except this principal.
        /// \arg \c O OnlyFor - Lock applies \e only to this principal.
        /// \arg \c C Comment - Comment given to the lock.
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
                case "G": // long version and default when not using a format specifier
                {
                    string kind = String.Format("{0}", (Kind == LockKind.all) ? "promotions to and from" : "promotions " + Kind);
                    string howfor = String.Format("{0}", ((Kind == LockKind.all) || (String.IsNullOrEmpty(ExceptFor) && String.IsNullOrEmpty(OnlyFor))) ? 
                        "for all" : !String.IsNullOrEmpty(ExceptFor) ? 
                        ("except for " + Type + " " + ExceptFor) : ("for " + Type + " " + OnlyFor + " only"));
                    string text = String.Format("Lock {0} {1} {2}. {3}", kind, Name, howfor, Comment);
                    return text;
                }
                case "K": // kind of AccuRev lock: from, to, or all
                    return Kind.ToString();
                case "N": // name of stream the lock applies to
                    return Name;
                case "T": // whether lock is for a user or group
                    return Type.ToString();
                case "E": // lock applies to all except this principal
                    return ExceptFor;
                case "O": // lock applies only to this principal
                    return OnlyFor;
                case "C": // comment given to the lock
                    return Comment;
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
    /// A container of AcLock objects that define the AccuRev locks that prevent certain users 
    /// from making changes to streams.
    /// </summary>
    [Serializable]
    public sealed class AcLocks : List<AcLock>
    {
        #region class variables
        [NonSerialized] private readonly object _locker = new object();
        #endregion

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container of AcLock objects that define the AccuRev locks that prevent certain users 
        /// from making changes to streams.
        /// </summary>
        /*! \code
            // get locks on all streams in the repository
            AcLocks locks = new AcLocks();
            if (!(await locks.initAsync())) return false;

            // show DEV locks in NEPTUNE depot and MAINT locks in JUPITER depot
            var arr = new[] { "NEPTUNE_DEV", "JUPITER_MAINT" }; // elements are string subsets of our targets
            IEnumerable<AcLock> query = locks.Where(n => arr.Any(s => n.Name.Contains(s)));

            // list in stream name order with "to" lock followed by "from" lock
            foreach (AcLock lk in query.OrderBy(n => n.Name).ThenByDescending(n => n.Kind))
                Console.WriteLine(lk);
            ...
            Lock promotions to JUPITER_MAINT except for group LEADS. Authorized users only.
            Lock promotions from JUPITER_MAINT for all.
            Lock promotions to JUPITER_MAINT1 except for group REPORTING. Work on defect 1405.
            Lock promotions from JUPITER_MAINT1 except for group LEADS.
            Lock promotions to JUPITER_MAINT2 for all. On hold until further notice.
            Lock promotions from JUPITER_MAINT2 for group SYSTEM_TEST only. UAT in progress.
            Lock promotions to JUPITER_MAINT3 except for user robert. GUI defect 1345.
            Lock promotions from JUPITER_MAINT3 except for group ADMIN.
            Lock promotions to NEPTUNE_DEV1 except for group DEV. October ER.
            Lock promotions from NEPTUNE_DEV1 except for group ENV_SUPPORT.
            Lock promotions to NEPTUNE_DEV2 except for user thomas.
            Lock promotions from NEPTUNE_DEV2 except for group LEADS. Authorized users only.
            Lock promotions to and from NEPTUNE_DEV3 for all.
            \endcode */
        /*! \sa initAsync(AcDepot), initAsync(DepotsCollection), initAsync(StreamsCollection), 
             <a href="_lock_streams_8cs-example.html">LockStreams.cs</a>, 
             <a href="_promotion_rights_8cs-example.html">PromotionRights.cs</a>,
             [default comparer](@ref AcLock#CompareTo) */
        public AcLocks() { }

        /// <summary>
        /// Populate this container with AcLock objects on streams in \e depot or all AcLock objects in the repository.
        /// </summary>
        /// <param name="depot">Limit the list of locks to those on \e depot only, otherwise 
        /// \e null for all locks in the repository.</param>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcLocks constructor](@ref AcUtils#AcLocks#AcLocks), initAsync(DepotsCollection), initAsync(StreamsCollection) */
        /*! \show_ <tt>show -fx locks</tt> */
        public async Task<bool> initAsync(AcDepot depot = null)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = "show -fx locks";
                AcResult r = await AcCommand.runAsync(cmd);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = null;
                    if (depot == null)
                        query = from e in xml.Descendants("Element")
                                select e;
                    else
                        query = from e in xml.Descendants("Element")
                                join AcStream s in depot.Streams on (string)e.Attribute("Name") equals s.Name
                                select e;
                    ret = runCommand(query);
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcLocks.initAsync(AcDepot){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcLocks.initAsync(AcDepot){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        /// <summary>
        /// Populate this container with AcLock objects on streams in \e depots.
        /// </summary>
        /// <param name="depots">Limit the list of locks to those on streams in \e depots only. Depot names in \e depots 
        /// must match their respective AccuRev depot name exactly.</param>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcLocks constructor](@ref AcUtils#AcLocks#AcLocks), initAsync(AcDepot), initAsync(StreamsCollection) */
        /*! \show_ <tt>show -fx locks</tt> */
        public async Task<bool> initAsync(DepotsCollection depots)
        {
            AcDepots dlist = new AcDepots();
            if (!(await dlist.initAsync(depots))) return false;

            bool ret = false; // assume failure
            try
            {
                string cmd = "show -fx locks";
                AcResult r = await AcCommand.runAsync(cmd);
                if (r != null && r.RetVal == 0)
                {
                    bool result = true;
                    XElement xml = XElement.Parse(r.CmdResult);
                    for (int ii = 0; ii < dlist.Count && result; ii++)
                    {
                        AcDepot depot = dlist[ii];
                        IEnumerable<XElement> query = from e in xml.Descendants("Element")
                                                      join AcStream s in depot.Streams on (string)e.Attribute("Name") equals s.Name
                                                      select e;
                        result = runCommand(query);
                    }

                    ret = result;
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcLocks.initAsync(DepotsCollection){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcLocks.initAsync(DepotsCollection){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        /// <summary>
        /// Populate this container with AcLock objects on \e streams.
        /// </summary>
        /// <param name="streams">Limit the list of locks to those on \e streams only. Stream names in \e streams 
        /// must match their respective AccuRev stream name exactly.</param>
        /// <returns>\e true if initialization succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c show command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcLocks constructor](@ref AcUtils#AcLocks#AcLocks), initAsync(AcDepot), initAsync(DepotsCollection) */
        /*! \show_ <tt>show -fx locks</tt> */
        public async Task<bool> initAsync(StreamsCollection streams)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = "show -fx locks";
                AcResult r = await AcCommand.runAsync(cmd);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from e in xml.Descendants("Element")
                                                  where streams.OfType<StreamElement>().Any(se => String.Equals(se.Stream, (string)e.Attribute("Name")))
                                                  select e;
                    ret = runCommand(query);
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcLocks.initAsync(StreamsCollection){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcLocks.initAsync(StreamsCollection){0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
        //@}
        #endregion

        /// <summary>
        /// Helper function that populates this container with AcLock objects as per \e query sent by an initAsync method.
        /// </summary>
        /// <param name="query">The query to iterate.</param>
        /// <returns>\e true if no failure occurred and initialization was successful, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on failure to handle a range of exceptions.</exception>
        /*! \note <tt>show -fx locks</tt> XML attributes for stream name and [LockKind](@ref AcUtils#LockKind) 
             always exist. Other attributes exist only if they have values. */
        private bool runCommand(IEnumerable<XElement> query)
        {
            bool ret = false; // assume failure
            try
            {
                foreach (XElement e in query)
                {
                    AcLock lk = new AcLock();
                    // Kind and Name are the only ones that always exist in the XML
                    // others exist only if there are values for them
                    string kind = (string)e.Attribute("kind");
                    lk.Kind = (LockKind)Enum.Parse(typeof(LockKind), kind);
                    lk.Name = (string)e.Attribute("Name");
                    string type = (string)e.Attribute("userType") ?? String.Empty;
                    lk.Type = String.IsNullOrEmpty(type) ? PrinType.none :
                        (PrinType)Enum.Parse(typeof(PrinType), type);
                    lk.ExceptFor = (string)e.Attribute("exceptFor") ?? String.Empty;
                    lk.OnlyFor = (string)e.Attribute("onlyFor") ?? String.Empty;
                    lk.Comment = (string)e.Attribute("comment") ?? String.Empty;
                    lock (_locker) { Add(lk); }
                }

                ret = true; // operation succeeded
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcLocks.runCommand{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        /// <summary>
        /// Determine if \e stream has a \c lock on it.
        /// </summary>
        /// <param name="stream">Name of stream to query.</param>
        /// <returns>\e true if \e stream has a lock on it, \e false otherwise.</returns>
        public bool hasLock(string stream)
        {
            bool found = false;
            for (int ii=0; ii < Count && !found; ii++)
            {
                AcLock lk = this[ii];
                if (String.Equals(lk.Name, stream))
                    found = true;
            }

            return found;
        }

        /// <summary>
        /// Put a \c lock on \e stream as per lock \e kind.
        /// </summary>
        /// <param name="stream">Name of stream or workspace to lock.</param>
        /// <param name="comment">Comment to be used for the lock.</param>
        /// <param name="kind">Type of lock to apply: \e to, \e from, or \e all.</param>
        /// <param name="prncpl">AccuRev principal name of user or group in the case of \e to or \e from lock.</param>
        /// <param name="onlyexcept">Apply \c lock to \e prncpl only or to all except \e prncpl.</param>
        /// <returns>\e true if operation succeeded, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c lock command failure.</exception>
        /*! \lock_ <tt>lock -c \<comment\> [-kf [-e|-o prncpl] | -kt [-e|-o prncpl]] \<stream\></tt> */
        /*! \accunote_ The CLI \c lock command can be used on a workspace stream but the same cannot be done using the AccuRev GUI client. AccuRev defect 23850. */
        public async Task<bool> lockAsync(string stream, string comment, LockKind kind = LockKind.all, AcPrincipal prncpl = null, OnlyExcept onlyexcept = OnlyExcept.Except)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = null;
                if (kind == LockKind.from)
                    if (prncpl != null)
                        cmd = String.Format(@"lock -c ""{0}"" -kf {1} ""{2}"" ""{3}""", comment, (onlyexcept == OnlyExcept.Except) ? "-e" : "-o", prncpl, stream);
                    else
                        cmd = String.Format(@"lock -c ""{0}"" -kf ""{1}""", comment, stream);  // lock 'from' for all
                else if (kind == LockKind.to)
                    if (prncpl != null)
                        cmd = String.Format(@"lock -c ""{0}"" -kt {1} ""{2}"" ""{3}""", comment, (onlyexcept == OnlyExcept.Except) ? "-e" : "-o", prncpl, stream);
                    else
                        cmd = String.Format(@"lock -c ""{0}"" -kt ""{1}""", comment, stream); // lock 'to' for all
                else if (kind == LockKind.all)
                    cmd = String.Format(@"lock -c ""{0}"" ""{1}""", comment, stream); // lock 'to and from' for all

                AcResult r = await AcCommand.runAsync(cmd);
                if (r != null && r.RetVal == 0)
                    ret = true; // operation succeeded
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException in AcLocks.lockAsync caught and logged.{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }

        /// <summary>
        /// Remove \c lock \e kind on \e stream.
        /// </summary>
        /// <param name="stream">Name of stream or workspace to unlock.</param>
        /// <param name="kind">Type of lock to remove: \e to, \e from, or \e all.</param>
        /// <returns>\e true if \e stream was unlocked successfully, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c unlock command failure.</exception>
        /*! \unlock_ <tt>unlock [-kf | -kt] \<stream\></tt> */
        public async Task<bool> unlockAsync(string stream, LockKind kind = LockKind.all)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = null;
                if (kind == LockKind.from)
                    cmd = String.Format(@"unlock -kf ""{0}""", stream);
                else if (kind == LockKind.to)
                    cmd = String.Format(@"unlock -kt ""{0}""", stream);
                else if (kind == LockKind.all)
                    cmd = String.Format(@"unlock ""{0}""", stream);

                AcResult r = await AcCommand.runAsync(cmd);
                if (r != null && r.RetVal == 0)
                    ret = true; // operation succeeded
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException in AcLocks.unlockAsync caught and logged.{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
    }
}
