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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace AcUtils
{
    /// <summary>
    /// An object that defines the attributes of an AccuRev transaction.
    /// </summary>
    /*! \deprecated
        Prefer using LINQ to XML and custom query operators from the Extensions class.
    */
    [Serializable]
    [DebuggerDisplay("{ID} {Type} {User} {Time}")]
    public sealed class Transaction : IFormattable, IEquatable<Transaction>, IComparable<Transaction>, IComparable
    {
        #region class variables
        private int _id; // transaction ID
        private string _type; // transaction type, e.g. promote, keep, add, etc.
        private DateTime _time; // transaction time
        private string _user; // user's principal name who executed the transaction
        private string _streamName; // stream name where the transaction occurred (promote only)
        private int _streamNumber; // stream number for same
        private string _fromStreamName; // name of the source stream (promote only)
        private int _fromStreamNumber; // stream number for same
        private string _comment; // comment for the transaction as given by the user
        private List<AcUtils.Version> _versions = new List<AcUtils.Version>(); // list of versions for this transaction
        private List<Move> _moves = new List<Move>();
        private List<CompRule> _compRules = new List<CompRule>();
        private List<Tuple<AcStream, AcWorkspace>> _streams = new List<Tuple<AcStream, AcWorkspace>>();
        #endregion

        /// <summary>
        /// Default constructor. It is called internally and not by user code.
        /// </summary>
        internal Transaction() { }

        #region Equality comparison
        /*! \name Equality comparison */
    /**@{*/
    /// <summary>
    /// IEquatable implementation to determine the equality of instances of type Transaction. 
    /// Compares transactions for equality based on transaction ID, type, time, and principal.
    /// </summary>
    /// <remarks>Although transaction ID's are unique within a depot, our Hist container class 
    /// can hold transactions from multiple depots, thus the potential for duplicate ID's. 
    /// So instead of a simple transaction ID comparison, we use the approach defined here.</remarks>
    /// <param name="other">The Transaction object being compared to \e this instance.</param>
    /// <returns>\e true if Transaction \e rhs is the same, \e false otherwise.</returns>
    public bool Equals(Transaction other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            var left = Tuple.Create(_time, _id, _user, _type);
            var right = Tuple.Create(other._time, other._id, other._user, other._type);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(Transaction)](@ref Transaction#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as Transaction);
        }

        /// <summary>
        /// Override appropriate for type Transaction.
        /// </summary>
        /// <returns>Hash of transaction ID, type, time, and user.</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(_time, _id, _user, _type);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing Transaction objects to sort 
        /// by transaction ID if transaction time is the same, otherwise in reverse chronological 
        /// order (latest transactions on top).
        /// </summary>
        /// <param name="other">A Transaction object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the Transaction objects being compared.</returns>
        /*! \sa [Hist class](@ref AcUtils#Hist) */
        public int CompareTo(Transaction other)
        {
            if (Transaction.ReferenceEquals(this, other))
                return 0;
            else
            {
                int compareTime = Time.CompareTo(other._time);
                if (compareTime == 0) // if transaction times are the same..
                    return ID.CompareTo(other._id); // order by transaction ID
                else
                    // reverse chronological order so latest transactions are on top
                    return -1 * compareTime;
            }
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">A Transaction object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(Transaction)](@ref Transaction#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not a Transaction object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is Transaction))
                throw new ArgumentException("Argument is not a Transaction", "other");
            Transaction o = (Transaction)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Transaction ID.
        /// </summary>
        public int ID
        {
            get { return _id; }
            internal set { _id = value; }
        }

        /// <summary>
        /// Transaction type, e.g. \c promote, \c keep, \c add, etc.
        /// </summary>
        public string Type
        {
            get { return _type ?? String.Empty; }
            internal set { _type = value; }
        }

        /// <summary>
        /// Transaction time.
        /// </summary>
        public DateTime Time
        {
            get { return _time; }
            internal set { _time = value; }
        }

        /// <summary>
        /// User's principal name.
        /// </summary>
        public string User
        {
            get { return _user ?? String.Empty; }
            internal set { _user = value; }
        }

        /// <summary>
        /// Name of the stream where the \c promote transaction occurred.
        /// </summary>
        public string StreamName
        {
            get { return _streamName ?? String.Empty; }
            internal set { _streamName = value; }
        }

        /// <summary>
        /// Stream number where the \c promote transaction occurred.
        /// </summary>
        public int StreamNumber
        {
            get { return _streamNumber; }
            internal set { _streamNumber = value; }
        }

        /// <summary>
        /// The \e from stream in a \c promote transaction.
        /// </summary>
        public string FromStreamName
        {
            get { return _fromStreamName ?? String.Empty; }
            internal set { _fromStreamName = value; }
        }

        /// <summary>
        /// Number of the \e from stream where the \c promote transaction occurred.
        /// </summary>
        public int FromStreamNumber
        {
            get { return _fromStreamNumber; }
            internal set { _fromStreamNumber = value; }
        }

        /// <summary>
        /// Comment given to the transaction by the user.
        /// </summary>
        public string Comment
        {
            get { return _comment ?? String.Empty; }
            internal set { _comment = value; }
        }

        /// <summary>
        /// List of version objects included in this transaction.
        /// </summary>
        public List<Version> Versions
        {
            get { return _versions; }
            internal set { _versions = value; }
        }

        /// <summary>
        /// List of \c move operations that took place in this transaction.
        /// </summary>
        public List<Move> Moves
        {
            get { return _moves; }
            internal set { _moves = value; }
        }

        /// <summary>
        /// List of comp_rule objects included in this transaction.
        /// </summary>
        /// <remarks>Included when one of the following include/exclude rules was applied 
        /// to the element: \c clear, \c excl, \c incl, \c incldo.</remarks>
        public List<CompRule> CompRules
        {
            get { return _compRules; }
            internal set { _compRules = value; }
        }

        /// <summary>
        /// List of stream/workspace objects included in this transaction.
        /// </summary>
        public List<Tuple<AcStream, AcWorkspace>> Streams
        {
            get { return _streams; }
            internal set { _streams = value; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(tran.ToString("LV"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types 
        /// using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Transaction ID (default when not using a format specifier).
        /// \arg \c LV Long version (verbose).
        /// \arg \c K Transaction type, e.g. \e promote, \e keep, \e add, etc.
        /// \arg \c T Transaction time.
        /// \arg \c U User (principal) name.
        /// \arg \c S \e To stream name and number ver_spec, e.g. \c NEPTUNE_UAT\17 (promote only)
        /// \arg \c F \e From stream name and number ver_spec (promote only).
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
                case "LV": // long version (verbose)
                {
                    string text;
                    if (_fromStreamNumber > 0) // promote only
                        text = String.Format("Transaction: {1} {{{2}}} User: {3} {4}{0}To: {5} ({6}), From: {7} ({8}){0}Comment: {9}",
                            Environment.NewLine, ID, Type, User, Time, StreamName, StreamNumber, FromStreamName, FromStreamNumber, Comment);
                    else
                        text = String.Format("Transaction: {0} {{{1}}} User: {2} {3}, Comment: {4}", ID, Type, User, Time, Comment);

                    return text;
                }
                case "K": // Transaction type, e.g. promote, keep, add, etc.
                    return Type;
                case "T": // Transaction time.
                    return Time.ToString();
                case "U": // User (principal) name.
                    return User;
                case "S": // To stream name and number ver_spec, e.g. NEPTUNE_UAT\17 (promote only)
                    return String.Format("{0}\\{1}", StreamName, StreamNumber);
                case "F": // From stream name and number ver_spec (promote only).
                    return String.Format("{0}\\{1}", FromStreamName, FromStreamNumber);
                case "G": // Transaction ID.
                    return ID.ToString(); // general format should be short since it can be called by anything
                default:
                    throw new FormatException(String.Format("The {0} format string is not supported.", format));
            }
        }

        // Calls ToString(string, IFormatProvider) version with a \e null IFormatProvider argument.
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
    /// Defines the attributes of a single version object in a transaction.
    /// </summary>
    /*! \deprecated
        Prefer using LINQ to XML and custom query operators from the Extensions class.
    */
    [Serializable]
    [DebuggerDisplay("{EID} {ElementType} {Location}")]
    public sealed class Version : IFormattable, IEquatable<Version>, IComparable<Version>, IComparable
    {
        #region class variables
        private string _location;
        private int _eid;
        private int _virStreamNumber;
        private int _virVersionNumber;
        private string _stream; // initialized using first part of virtualNamedVersion (named portion)
        private int _realStreamNumber;
        private int _realVersionNumber;
        private string _workspace; // initialized using first part of realNamedVersion (named portion)
        private int _ancestorStreamNumber;
        private int _ancestorVersionNumber;
        private string _ancestor; // initialized using first part of ancestorNamedVersion (named portion)
        private int _mergedWithStreamNumber;
        private int _mergedWithVersionNumber;
        private string _mergedWith; // initialized using first part of mergedAgainstNamedVersion (named portion)
        private ElementType _elementType;
        private bool _folder;
        private DateTime? _mergeTime;
        private long _cksum;
        private long _size;
        #endregion

        /// <summary>
        /// Default constructor. It is called internally and not by user code.
        /// </summary>
        internal Version() { }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>IEquatable implementation to determine the equality of instances of type Version.</summary>
        /// <remarks>Uses the version's EID, virtual stream/version numbers, workspace name 
        /// and real version number to compare instances.</remarks>
        /// <param name="other">The Version object being compared to \e this instance.</param>
        /// <returns>\e true if Version \e other is the same, \e false otherwise.</returns>
        public bool Equals(Version other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            var left = Tuple.Create(EID, VirStreamNumber, VirVersionNumber, Workspace, RealVersionNumber);
            var right = Tuple.Create(other.EID, other.VirStreamNumber, other.VirVersionNumber, other.Workspace, other.RealVersionNumber);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(Version)](@ref Version#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return Equals(other as Version);
        }

        /// <summary>
        /// Override appropriate for type Version.
        /// </summary>
        /// <returns>Hash of version's EID, virtual stream/version numbers, workspace name 
        /// and real version number.</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(EID, VirStreamNumber, VirVersionNumber, Workspace, RealVersionNumber);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing Version objects 
        /// to sort by [element location](@ref AcUtils#Version#Location).
        /// </summary>
        /// <param name="other">A Version object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the Version objects based on location.</returns>
        public int CompareTo(Version other)
        {
            int result;
            if (Version.ReferenceEquals(this, other))
                result = 0;
            else
                result = Location.CompareTo(other.Location);

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">A Version object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(Version)](@ref AcUtils#Version#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not a Version object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is Version))
                throw new ArgumentException("Argument is not a Version", "other");
            Version o = (Version)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Depot-relative path of the element.
        /// </summary>
        public string Location
        {
            get { return _location ?? String.Empty; }
            internal set { _location = value; }
        }

        /// <summary>
        /// The element ID.
        /// </summary>
        public int EID
        {
            get { return _eid; }
            internal set { _eid = value; }
        }

        /// <summary>
        /// Virtual stream number.
        /// </summary>
        public int VirStreamNumber
        {
            get { return _virStreamNumber; }
            internal set { _virStreamNumber = value; }
        }

        /// <summary>
        /// Virtual version number.
        /// </summary>
        public int VirVersionNumber
        {
            get { return _virVersionNumber; }
            internal set { _virVersionNumber = value; }
        }

        /// <summary>
        /// Real stream number.
        /// </summary>
        public int RealStreamNumber
        {
            get { return _realStreamNumber; }
            internal set { _realStreamNumber = value; }
        }

        /// <summary>
        /// Real version number.
        /// </summary>
        public int RealVersionNumber
        {
            get { return _realVersionNumber; }
            internal set { _realVersionNumber = value; }
        }

        /// <summary>
        /// Dynamic stream where the element is located. Initialized using first part of virtualNamedVersion.
        /// </summary>
        public string Stream
        {
            get { return _stream ?? String.Empty; }
            internal set { _stream = value; }
        }

        /// <summary>
        /// Real stream where the element is located. Initialized using first part of realNamedVersion.
        /// </summary>
        public string Workspace
        {
            get { return _workspace ?? String.Empty; }
            internal set { _workspace = value; }
        }

        /// <summary>
        /// Stream number of the immediate ancestor version.
        /// </summary>
        public int AncestorStreamNumber
        {
            get { return _ancestorStreamNumber; }
            internal set { _ancestorStreamNumber = value; }
        }

        /// <summary>
        /// Version number of the immediate ancestor version.
        /// </summary>
        public int AncestorVersionNumber
        {
            get { return _ancestorVersionNumber; }
            internal set { _ancestorVersionNumber = value; }
        }

        /// <summary>
        /// Initialized using first part of ancestorNamedVersion.
        /// </summary>
        public string Ancestor
        {
            get { return _ancestor ?? String.Empty; }
            internal set { _ancestor = value; }
        }

        /// <summary>
        /// Stream number of the version that was merged.
        /// </summary>
        public int MergedWithStreamNumber
        {
            get { return _mergedWithStreamNumber; }
            internal set { _mergedWithStreamNumber = value; }
        }

        /// <summary>
        /// Version number of the version that was merged.
        /// </summary>
        public int MergedWithVersionNumber
        {
            get { return _mergedWithVersionNumber; }
            internal set { _mergedWithVersionNumber = value; }
        }

        /// <summary>
        /// Initialized using first part of mergedAgainstNamedVersion, e.g. \c mergedAgainstNamedVersion="NEPTUNE_DEV3_barnyrd/7"
        /// </summary>
        public string MergedWith
        {
            get { return _mergedWith ?? String.Empty; }
            internal set { _mergedWith = value; }
        }

        /// <summary>
        /// The element's type: \e dir, \e text, \e binary, \e ptext, \e elink, or \e slink.
        /// </summary>
        public ElementType ElementType
        {
            get { return _elementType; }
            internal set { _elementType = value; }
        }

        /// <summary>
        /// \e true if the element is a folder, \e false otherwise.
        /// </summary>
        public bool Folder
        {
            get { return _folder; }
            internal set { _folder = value; }
        }

        /// <summary>
        /// Time the merge took place.
        /// </summary>
        public DateTime? MergeTime
        {
            get { return _mergeTime; }
            internal set { _mergeTime = value; }
        }

        /// <summary>
        /// Checksum of the version.
        /// </summary>
        public long Cksum
        {
            get { return _cksum; }
            internal set { _cksum = value; }
        }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        /*! \sa [Convert file size bytes to megabyte/gigabyte string using C#](http://www.joe-stevens.com/2009/10/21/convert-file-size-bytes-to-megabytegigabyte-string-using-c/) */
        public long Size
        {
            get { return _size; }
            internal set { _size = value; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, e.g. <b>Console.WriteLine(ver.ToString("r"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types 
        /// using [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c LV Long version (verbose).
        /// \arg \c G Location, the depot-relative path of the element (default when not using a format specifier).
        /// \arg \c I Element ID.
        /// \arg \c T The element's type: \e dir, \e text, \e binary, \e ptext, \e elink, or \e slink.
        /// \arg \c F \e True if the element is a folder, \e False otherwise.
        /// \arg \c V Virtual stream\\version number format, e.g. <tt>MARS_STAGE\17 (2\17)</tt>
        /// \arg \c R Real stream\\version number format, e.g. <tt>MARS_DEV3_barnyrd\8 (52\8)</tt>
        /// \arg \c A Ancestor stream\\version number format.
        /// \arg \c M Merged-in stream\\version number format.
        /// \arg \c MN Initialized using first part of mergedAgainstNamedVersion XML attribute, e.g. \c mergedAgainstNamedVersion="MARS_DEV3_barnyrd/8"
        /// \arg \c AN Initialized using first part of ancestorNamedVersion XML attribute.
        /// \arg \c MG Time the merge took place.
        /// \arg \c CS Checksum of the version.
        /// \arg \c FS File size in bytes.
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
                case "LV":
                {
                    string text = null;
                    if (!String.IsNullOrEmpty(MergedWith)) // a merge always includes ancestor attributes, so we check for it first
                    {
                        if ((VirStreamNumber == RealStreamNumber) && (VirVersionNumber == RealVersionNumber))
                        {
                            text = String.Format("\tEID: {1} {{{2}}} Size: {3}, {4}{0}\t\tReal: {5}\\{7} ({6}\\{7}){0}\t\tAncestor: {8}\\{10} ({9}\\{10}){0}\t\tMergedWith: {11}\\{13} ({12}\\{13})",
                                Environment.NewLine, EID, ElementType, Size, Location, Workspace, RealStreamNumber, RealVersionNumber,
                                Ancestor, AncestorStreamNumber, AncestorVersionNumber, MergedWith, MergedWithStreamNumber, MergedWithVersionNumber);
                        }
                        else
                        {
                            text = String.Format("\tEID: {1} {{{2}}} Size: {3}, {4}{0}\t\tVirtual: {5}\\{7} ({6}\\{7}), Real: {8}\\{10} ({9}\\{10}){0}\t\tAncestor: {11}\\{13} ({12}\\{13}){0}\t\tMergedWith: {14}\\{16} ({15}\\{16})",
                                Environment.NewLine, EID, ElementType, Size, Location, Stream, VirStreamNumber, VirVersionNumber, Workspace, RealStreamNumber, RealVersionNumber,
                                Ancestor, AncestorStreamNumber, AncestorVersionNumber, MergedWith, MergedWithStreamNumber, MergedWithVersionNumber);
                        }
                    }
                    else if (!String.IsNullOrEmpty(Ancestor)) // not a result of a merge but does includes ancestor attributes
                    {
                        if ((VirStreamNumber == RealStreamNumber) && (VirVersionNumber == RealVersionNumber))
                        {
                            text = String.Format("\tEID: {1} {{{2}}} Size: {3}, {4}{0}\t\tReal: {5}\\{7} ({6}\\{7}){0}\t\tAncestor: {8}\\{10} ({9}\\{10})",
                                Environment.NewLine, EID, ElementType, Size, Location, Workspace, RealStreamNumber, RealVersionNumber,
                                Ancestor, AncestorStreamNumber, AncestorVersionNumber);
                        }
                        else
                        {
                            text = String.Format("\tEID: {1} {{{2}}} Size: {3}, {4}{0}\t\tVirtual: {5}\\{7} ({6}\\{7}), Real: {8}\\{10} ({9}\\{10}){0}\t\tAncestor: {11}\\{13} ({12}\\{13})",
                                Environment.NewLine, EID, ElementType, Size, Location, Stream, VirStreamNumber, VirVersionNumber, Workspace, RealStreamNumber, RealVersionNumber,
                                Ancestor, AncestorStreamNumber, AncestorVersionNumber);
                        }
                    }
                    else // has only real and virtual attributes
                    {
                        if ((VirStreamNumber == RealStreamNumber) && (VirVersionNumber == RealVersionNumber))
                        {
                            text = String.Format("\tEID: {1} {{{2}}} {3}{0}\t\tReal: {4}\\{6} ({5}\\{6})",
                                Environment.NewLine, EID, ElementType, Location, Workspace, RealStreamNumber, RealVersionNumber);
                        }
                        else
                        {
                            text = String.Format("\tEID: {1} {{{2}}} {3}{0}\t\tVirtual: {4}\\{6} ({5}\\{6}), Real: {7}\\{9} ({8}\\{9})",
                                Environment.NewLine, EID, ElementType, Location, Stream, VirStreamNumber, VirVersionNumber, Workspace, RealStreamNumber, RealVersionNumber);
                        }
                    }

                    return text;
                }
                case "G":  // location, the depot-relative path of the element (default when not using a format specifier)
                    return Location;
                case "I":  // element ID
                    return EID.ToString();
                case "T":  // element's type: dir, text, binary, ptext, elink, or slink
                    return ElementType.ToString();
                case "F":  // True if the element is a folder, False otherwise
                    return Folder.ToString();
                case "V":  // virtual_stream\version_number format, e.g. MARS_STAGE\17 (2\17)
                    return String.Format("{0}\\{2} ({1}\\{2})", Stream, VirStreamNumber, VirVersionNumber);
                case "R":  // real_stream\version_number format, e.g. MARS_DEV3_barnyrd\8 (52\8)
                    return String.Format("{0}\\{2} ({1}\\{2})", Workspace, RealStreamNumber, RealVersionNumber);
                case "A":  // ancestor_stream\version_number format
                    return String.Format("{0}\\{2} ({1}\\{2})", Ancestor, AncestorStreamNumber, AncestorVersionNumber);
                case "M":  // merged-in_stream\\version_number format
                    return String.Format("{0}\\{2} ({1}\\{2})", MergedWith, MergedWithStreamNumber, MergedWithVersionNumber);
                case "MN": // initialized using first part of mergedAgainstNamedVersion XML attribute, e.g. mergedAgainstNamedVersion="MARS_DEV3_barnyrd/8
                    return MergedWith;
                case "AN": // initialized using first part of ancestorNamedVersion XML attribute
                    return Ancestor;
                case "MG": // time the merge took place
                    return MergeTime.ToString();
                case "CS": // checksum of the version
                    return Cksum.ToString();
                case "FS": // file size in bytes
                    return Size.ToString();
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
    /// The destination and source paths for a \c move operation in a transaction.
    /// </summary>
    /*! \deprecated
        Prefer using LINQ to XML and custom query operators from the Extensions class.
    */
    [Serializable]
    public sealed class Move
    {
        #region class variables
        private string _dest;
        private string _source;
        #endregion

        /// <summary>
        /// Destination name.
        /// </summary>
        public string Destination
        {
            get { return _dest ?? String.Empty; }
            internal set { _dest = value; }
        }

        /// <summary>
        /// Source name.
        /// </summary>
        public string Source
        {
            get { return _source ?? String.Empty; }
            internal set { _source = value; }
        }

        /// <summary>
        /// The ToString implementation.
        /// </summary>
        public override string ToString()
        {
            string text = String.Format("src: {1}{0}desc: {2}", Environment.NewLine, _source, _dest);
            return text;
        }
    }

    /// <summary>
    /// The attributes of a rule element in a  \c hist command transaction.
    /// </summary>
    /*! \deprecated
        Prefer using LINQ to XML and custom query operators from the Extensions class.
    */
    [Serializable]
    public sealed class CompRule
    {
        #region class variables
        private RuleKind _ruleKind;
        private int _xlinkStreamNum;
        private string _xlinkStreamName;
        private int _prevXlinkStreamNum;
        private string _prevXlinkStreamName;
        #endregion

        /// <summary>
        /// Clear a rule, include or exclude elements in a workspace or stream, 
        /// or include a directory and not its contents in same.
        /// </summary>
        public RuleKind RuleKind
        {
            get { return _ruleKind; }
            internal set { _ruleKind = value; }
        }

        /// <summary>
        /// ID of the stream to which the element is cross-linked.
        /// </summary>
        public int XlinkStreamNum
        {
            get { return _xlinkStreamNum; }
            internal set { _xlinkStreamNum = value; }
        }

        /// <summary>
        /// Name of stream to which the element is cross-linked.
        /// </summary>
        public string XlinkStreamName
        {
            get { return _xlinkStreamName ?? String.Empty; }
            internal set { _xlinkStreamName = value; }
        }

        /// <summary>
        /// If include rule was reset, the number of the previously cross-linked stream.
        /// </summary>
        public int PrevXlinkStreamNum
        {
            get { return _prevXlinkStreamNum; }
            internal set { _prevXlinkStreamNum = value; }
        }

        /// <summary>
        /// If include rule was reset, the name of the previously cross-linked stream.
        /// </summary>
        public string PrevXlinkStreamName
        {
            get { return _prevXlinkStreamName ?? String.Empty; }
            internal set { _prevXlinkStreamName = value; }
        }

        /// <summary>
        /// The ToString implementation.
        /// </summary>
        public override string ToString()
        {
            string text;
            if (!String.IsNullOrEmpty(PrevXlinkStreamName))
                text = String.Format("{{{0}}}, old xlink: {1} ({2}), new xlink: {3} ({4})",
                    RuleKind, PrevXlinkStreamName, PrevXlinkStreamNum, XlinkStreamName, XlinkStreamNum);
            if (!String.IsNullOrEmpty(XlinkStreamName))
                text = String.Format("{{{0}}}, xlink: {1} ({2})",
                    RuleKind, XlinkStreamName, XlinkStreamNum);
            else
                text = String.Format("{{{0}}}", RuleKind);
            return text;
        }
    }

    /// <summary>
    /// The list of transactions from the \c hist command.
    /// </summary>
    /*! \code
        try {
            AcResult result = await AcCommand.runAsync(@"hist -p ""NEPTUNE"" -t ""2013/10/25 14:01:40 - 2013/10/25 11:50:50"" -k promote -fevx");
            if (result == null || result.RetVal != 0) return false; // error occurred, check log file
            Hist.init(result.CmdResult); // populate list using XML from the hist command
            foreach (Transaction tran in Hist.Transactions.OrderBy(n => n))
            {
                Console.WriteLine(tran.ToString("LV"));
                List<AcUtils.Version> versions = tran.Versions; // get the versions in this transaction
                foreach (AcUtils.Version ver in versions.OrderBy(n => n))
                    Console.WriteLine(ver.ToString("LV"));
                Console.WriteLine();
            }
        }

        catch (AcUtilsException ecx) {
            string msg = String.Format("AcUtilsException caught in Program.show{0}{1}", Environment.NewLine, ecx.Message);
            Console.WriteLine(msg);
        }

        catch (Exception ecx) {
            string msg = String.Format("Exception caught in Program.show{0}{1}", Environment.NewLine, ecx.Message);
            Console.WriteLine(msg);
        }
    \endcode */
    /*! \deprecated
        Prefer using LINQ to XML and custom query operators from the Extensions class.
    */
    [Serializable]
    public static class Hist
    {
        #region class variables
        private static List<Transaction> _transactions = new List<Transaction>();
        private static AcDepots _depots;
        [NonSerialized] private static readonly object _locker = new object();
        #endregion

        /// <summary>
        /// The list of Transaction objects.
        /// </summary>
        public static List<Transaction> Transactions
        {
            get { return _transactions; }
            private set { _transactions = value; }
        }

        /// <summary>
        /// Removes all elements from the (static) transaction history.
        /// </summary>
        public static void clear()
        {
            lock (_locker)
            {
                foreach (Transaction tran in _transactions)
                {
                    List<Version> versions = tran.Versions;
                    versions.Clear();
                    List<Move> moves = tran.Moves;
                    moves.Clear();
                    List<CompRule> rules = tran.CompRules;
                    rules.Clear();
                    List<Tuple<AcStream, AcWorkspace>> streams = tran.Streams;
                    streams.Clear();
                }

                _transactions.Clear();
            }
        }

        /// <summary>
        /// Populate this list with transactions from the XML emitted by the 
        /// [hist](https://supportline.microfocus.com/Documentation/books/AccuRev/AccuRev/7.0.1/webhelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_hist.html) command.
        /// </summary>
        /// <param name="xml">XML from the AccuRev \c hist command.</param>
        /// <param name="EID">\e true if the \c hist command that generated \e xml included 
        /// the \c -e (EID) command line option, otherwise \e false.</param>
        /// <param name="mergedOnly">\e true to include only version elements that were the result of 
        /// a \c merge operation, or \e false to include all.</param>
        /// <returns>\e true if parsing was successful, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> 
        /// on failure to handle a range of exceptions.</exception>
        public static bool init(string xml, bool EID = false, bool mergedOnly = false)
        {
            bool ret = true; // assume success
            try
            {
                using (StringReader reader = new StringReader(xml))
                {
                    string temp;
                    int ival;
                    XPathDocument doc = new XPathDocument(reader);
                    XPathNavigator nav = doc.CreateNavigator();
                    XPathNodeIterator iterTrans = (EID) ?
                        nav.Select("AcResponse/element/transaction") :
                        nav.Select("AcResponse/transaction");
                    foreach (XPathNavigator transNav in iterTrans)
                    {
                        Transaction nTrans = new Transaction();
                        temp = transNav.GetAttribute("id", string.Empty);
                        nTrans.ID = Int32.Parse(temp, NumberStyles.Integer);
                        nTrans.Type = transNav.GetAttribute("type", string.Empty);
                        temp = transNav.GetAttribute("time", string.Empty);
                        nTrans.Time = (DateTime)AcDateTime.AcDate2DateTime(temp);
                        nTrans.User = transNav.GetAttribute("user", string.Empty);
                        nTrans.StreamName = transNav.GetAttribute("streamName", string.Empty);
                        temp = transNav.GetAttribute("streamNumber", string.Empty);
                        if (Int32.TryParse(temp, NumberStyles.Integer, null, out ival))
                            nTrans.StreamNumber = ival;
                        nTrans.FromStreamName = transNav.GetAttribute("fromStreamName", string.Empty);
                        temp = transNav.GetAttribute("fromStreamNumber", string.Empty);
                        if (Int32.TryParse(temp, NumberStyles.Integer, null, out ival))
                            nTrans.FromStreamNumber = ival;

                        XPathNodeIterator xpni = transNav.SelectChildren(XPathNodeType.Element);
                        XPathNavigator iterChildren = xpni.Current;
                        iterChildren.MoveToFirstChild();
                        do
                        {
                            switch (iterChildren.LocalName)
                            {
                                case "comment":
                                    if (!comment(nTrans, iterChildren))
                                        return false; // error already logged
                                    break;
                                case "comp_rule":
                                    if (!comp_rule(nTrans, iterChildren))
                                        return false; // ..
                                    break;
                                case "move":
                                    if (!move(nTrans, iterChildren))
                                        return false; // ..
                                    break;
                                case "version":
                                    if (!mergedOnly)
                                    {
                                        if (!version(nTrans, iterChildren))
                                            return false;
                                    }
                                    else // include only those version elements that have a merged_against attribute
                                    {
                                        XPathNodeIterator merged = (XPathNodeIterator)iterChildren.Evaluate("@merged_against");
                                        if (merged.Count != 0)
                                        {
                                            if (!version(nTrans, iterChildren))
                                                return false;
                                        }
                                    }
                                    break;
                                case "stream":
                                    if (!stream(nTrans, iterChildren))
                                        return false; // ..
                                    break;
                                default:
                                    temp = String.Format("Unknown element {0} in Hist.init", iterChildren.LocalName);
                                    AcDebug.Log(temp, false);
                                    break;
                            }

                        } while (iterChildren.MoveToNext());
                    }
                }
            }

            catch (Exception ecx)
            {
                String err = String.Format("Exception in Hist.init caught and logged.{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(err);
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Converts the XML attributes of a single \e version element from the \c hist command 
        /// to a Version object and then adds it to the Transaction object being initialized. 
        /// The Transaction object is added to our list of transactions if not already there, 
        /// or updated if already present.
        /// </summary>
        /// <param name="newTrans">The transaction object being initialized.</param>
        /// <param name="nav">Navigator object for querying the XML.</param>
        /// <returns>\e true if operation was successful, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> 
        /// on failure to handle a range of exceptions.</exception>
        private static bool version(Transaction newTrans, XPathNavigator nav)
        {
            bool ret = true; // assume success
            try
            {
                int ival;
                Version version = new Version();
                version.Location = nav.GetAttribute("path", String.Empty);
                string temp = nav.GetAttribute("eid", String.Empty);
                if (!String.IsNullOrEmpty(temp))
                    version.EID = Int32.Parse(temp, NumberStyles.Integer);
                temp = nav.GetAttribute("virtual", String.Empty);
                string[] arr = temp.Split('/');
                if (arr.Length > 0 && Int32.TryParse(arr[0], NumberStyles.Integer, null, out ival))
                    version.VirStreamNumber = ival;
                if (arr.Length > 1 && Int32.TryParse(arr[1], NumberStyles.Integer, null, out ival))
                    version.VirVersionNumber = ival;
                temp = nav.GetAttribute("real", String.Empty);
                arr = temp.Split('/');
                if (arr.Length > 0 && Int32.TryParse(arr[0], NumberStyles.Integer, null, out ival))
                    version.RealStreamNumber = ival;
                if (arr.Length > 1 && Int32.TryParse(arr[1], NumberStyles.Integer, null, out ival))
                    version.RealVersionNumber = ival;
                temp = nav.GetAttribute("virtualNamedVersion", String.Empty);
                arr = temp.Split('/');
                version.Stream = arr[0]; // we don't need arr[1] since we already have it from above
                temp = nav.GetAttribute("realNamedVersion", String.Empty);
                arr = temp.Split('/');
                version.Workspace = arr[0]; // we don't need arr[1] since we already have it from above
                temp = nav.GetAttribute("ancestor", String.Empty);
                arr = temp.Split('/');
                if (arr.Length > 0 && Int32.TryParse(arr[0], NumberStyles.Integer, null, out ival))
                    version.AncestorStreamNumber = ival;
                if (arr.Length > 1 && Int32.TryParse(arr[1], NumberStyles.Integer, null, out ival))
                    version.AncestorVersionNumber = ival;
                temp = nav.GetAttribute("ancestorNamedVersion", String.Empty);
                arr = temp.Split('/');
                version.Ancestor = arr[0]; // we don't need arr[1] since we already have it from above
                temp = nav.GetAttribute("merged_against", String.Empty);
                arr = temp.Split('/');
                if (arr.Length > 0 && Int32.TryParse(arr[0], NumberStyles.Integer, null, out ival))
                    version.MergedWithStreamNumber = ival;
                if (arr.Length > 1 && Int32.TryParse(arr[1], NumberStyles.Integer, null, out ival))
                    version.MergedWithVersionNumber = ival;
                temp = nav.GetAttribute("mergedAgainstNamedVersion", String.Empty);
                arr = temp.Split('/');
                version.MergedWith = arr[0]; // we don't need arr[1] since we already have it from above
                // convert string to our enum
                temp = nav.GetAttribute("elem_type", String.Empty);
                if (String.Equals(temp, "* unknown *")) // A known AccuRev defect.
                    version.ElementType = ElementType.unknown;
                else
                    version.ElementType = (ElementType)Enum.Parse(typeof(ElementType), temp);
                temp = nav.GetAttribute("dir", String.Empty);
                version.Folder = String.Equals(temp, "yes");
                temp = nav.GetAttribute("mtime", String.Empty);
                version.MergeTime = AcDateTime.AcDate2DateTime(temp);
                long lval;
                temp = nav.GetAttribute("cksum", String.Empty);
                if (Int64.TryParse(temp, NumberStyles.Number, null, out lval))
                    version.Cksum = lval;
                temp = nav.GetAttribute("sz", String.Empty);
                if (Int64.TryParse(temp, NumberStyles.Number, null, out lval))
                    version.Size = lval;
                // Determine if this transaction already exists in our list of transactions. If not, add it.
                lock (_locker)
                {
                    int ii = _transactions.IndexOf(newTrans);
                    if (ii == -1) // -1 for no, >= zero for yes
                    {
                        newTrans.Versions.Add(version);
                        _transactions.Add(newTrans);
                    }
                    else // Transaction object is already in our list so we just add our version object to it.
                    {
                        Transaction oldTrans = _transactions[ii];
                        oldTrans.Versions.Add(version);
                    }
                }
            }

            catch (Exception ecx)
            {
                String err = String.Format("Exception in Hist.version caught and logged.{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(err);
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Converts the XML attributes of a single \e stream element from the \c hist command 
        /// to a [Streams](@ref AcUtils#Transaction#Streams) object and then adds it to the 
        /// Transaction object being initialized. The Transaction object is added to our list 
        /// of transactions if not already there, or updated if already present.
        /// </summary>
        /// <param name="newTrans">The transaction object being initialized.</param>
        /// <param name="nav">Navigator object for querying the XML.</param>
        /// <returns>\e true if operation was successful, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> 
        /// on failure to handle a range of exceptions.</exception>
        private static bool stream(Transaction newTrans, XPathNavigator nav)
        {
            // create depots object as a singleton (thread safe)
            Func<Task<bool>> depots =
                delegate ()
                {
                    _depots = new AcDepots(true); // true for dynamic streams only
                    return (_depots.initAsync());
                };

            Lazy<Task<bool>> dini = new Lazy<Task<bool>>(depots, true);
            if (!(dini.Value.Result)) // referencing Value invokes our delegate above
                return false; // initialization failed, check log file

            bool ret = true; // assume success
            try
            {
                int ival;
                string temp;
                AcStream acStream = new AcStream();
                AcWorkspace acWorkspace = new AcWorkspace();
                Tuple<AcStream, AcWorkspace> acPair = new Tuple<AcStream, AcWorkspace>(acStream, acWorkspace);

                acPair.Item1.Name = nav.GetAttribute("name", String.Empty);
                temp = nav.GetAttribute("streamNumber", String.Empty);
                if (Int32.TryParse(temp, NumberStyles.Integer, null, out ival))
                    acPair.Item1.ID = ival;
                temp = nav.GetAttribute("depotName", String.Empty);
                acPair.Item1.Depot = _depots.getDepot(temp);
                // convert string to our enum
                temp = nav.GetAttribute("type", String.Empty);
                if (String.Equals(temp, "* unknown *")) // A defect as this should not happen.
                    acPair.Item1.Type = StreamType.unknown;
                else
                    acPair.Item1.Type = (StreamType)Enum.Parse(typeof(StreamType), temp);
                acPair.Item1.BasisName = nav.GetAttribute("basis", String.Empty);
                temp = nav.GetAttribute("basisStreamNumber", String.Empty);
                if (Int32.TryParse(temp, NumberStyles.Integer, null, out ival))
                    acPair.Item1.BasisID = ival;
                if (acPair.Item1.Type == StreamType.workspace)
                {
                    XPathNavigator iterWSpace = nav.SelectSingleNode("wspace");
                    if (iterWSpace != null)
                    {
                        iterWSpace.MoveToFirstChild();
                        acPair.Item2.Storage = iterWSpace.GetAttribute("Storage", String.Empty);
                        acPair.Item2.Host = iterWSpace.GetAttribute("Host", String.Empty);
                        temp = iterWSpace.GetAttribute("Target_trans", String.Empty);
                        acPair.Item2.TargetLevel = Int32.Parse(temp, NumberStyles.Integer);
                        temp = iterWSpace.GetAttribute("fileModTime", string.Empty);
                        acPair.Item2.LastUpdate = AcDateTime.AcDate2DateTime(temp);
                        temp = iterWSpace.GetAttribute("Type", String.Empty);
                        if (Int32.TryParse(temp, NumberStyles.Integer, null, out ival))
                            acPair.Item2.Type = (WsType)ival;
                        temp = iterWSpace.GetAttribute("EOL", String.Empty);
                        if (Int32.TryParse(temp, NumberStyles.Integer, null, out ival))
                            acPair.Item2.EOL = (WsEOL)ival;
                    }
                }
                // Determine if this transaction already exists in our list of transactions. If not, add it.
                lock (_locker)
                {
                    int ii = _transactions.IndexOf(newTrans);
                    if (ii == -1) // -1 for no, >= zero for yes
                    {
                        newTrans.Streams.Add(acPair);
                        _transactions.Add(newTrans);
                    }
                    else // Transaction object is already in our list so we just add our stream object to it.
                    {
                        Transaction oldTrans = _transactions[ii];
                        oldTrans.Streams.Add(acPair);
                    }
                }
            }

            catch (Exception ecx)
            {
                String err = String.Format("Exception in Hist.stream caught and logged.{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(err);
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Converts the \e comment element for the transaction from the \c hist command and adds 
        /// it to the Transaction object being initialized. 
        /// The Transaction object is added to our list of transactions if not already there, 
        /// or updated if already present.
        /// </summary>
        /// <param name="newTrans">The transaction object being initialized.</param>
        /// <param name="nav">Navigator object for querying the XML.</param>
        /// <returns>\e true if operation was successful, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> 
        /// on failure to handle a range of exceptions.</exception>
        private static bool comment(Transaction newTrans, XPathNavigator nav)
        {
            bool ret = true; // assume success
            try
            {
                string comment = nav.InnerXml;
                // Determine if this transaction already exists in our list of transactions. If not, add it.
                lock (_locker)
                {
                    int ii = _transactions.IndexOf(newTrans);
                    if (ii == -1) // -1 for no, >= zero for yes
                    {
                        newTrans.Comment = comment;
                        _transactions.Add(newTrans);
                    }
                    else // Transaction object is already in our list so we just add the comment to it.
                    {
                        Transaction oldTrans = _transactions[ii];
                        oldTrans.Comment = comment;
                    }
                }
            }

            catch (Exception ecx)
            {
                String err = String.Format("Exception in Hist.comment caught and logged.{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(err);
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Converts the XML attributes of a single \e comp_rule element from the \c hist command 
        /// and adds it to the \e Transaction object being initialized. 
        /// The Transaction object is added to our list of transactions if not already there, 
        /// or updated if already present.
        /// </summary>
        /// <param name="newTrans">The transaction object being initialized.</param>
        /// <param name="nav">Navigator object for querying the XML.</param>
        /// <returns>\e true if operation was successful, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> 
        /// on failure to handle a range of exceptions.</exception>
        /*! \code
        <?xml version="1.0" encoding="utf-8"?>
        <AcResponse
            Command="hist"
            TaskId="27581">
          <transaction
              id="668"
              type="defcomp"
              time="1308596399"
              user="barnyrd">
            <stream
                name="PG_STAGE"
                streamNumber="7"
                depotName="PlayGround"
                type="normal"
                basis="PlayGround"
                basisStreamNumber="1"/>
            <version
                path="\.\Iconic"
                eid="288"
                virtual="1/1"
                real="46/1"
                virtualNamedVersion="PlayGround/1"
                realNamedVersion="IC_DEV1_barnyrd/1"
                elem_type="dir"
                dir="yes"/>
            <comp_rule
                kind="incl"
                xlinkStreamNum="45"
                xlinkStreamName="IC_DEV2"
                prevXlinkStreamNum="41"
                prevXlinkStreamName="IC_MAINT1"/>
          </transaction>
          <streams>
            <stream
                id="1"
                name="PlayGround"
                type="normal"/>
          </streams>
        </AcResponse>
        \endcode */
        private static bool comp_rule(Transaction newTrans, XPathNavigator nav)
        {
            bool ret = true; // assume success
            try
            {
                int ival;
                CompRule rule = new CompRule();
                string kind = nav.GetAttribute("kind", String.Empty);
                rule.RuleKind = String.IsNullOrEmpty(kind) ?
                    RuleKind.unknown : (RuleKind)Enum.Parse(typeof(RuleKind), kind);
                string temp = nav.GetAttribute("xlinkStreamNum", String.Empty);
                if (Int32.TryParse(temp, NumberStyles.Integer, null, out ival))
                    rule.XlinkStreamNum = ival;
                rule.XlinkStreamName = nav.GetAttribute("xlinkStreamName", string.Empty);
                temp = nav.GetAttribute("prevXlinkStreamNum", String.Empty);
                if (Int32.TryParse(temp, NumberStyles.Integer, null, out ival))
                    rule.PrevXlinkStreamNum = ival;
                rule.PrevXlinkStreamName = nav.GetAttribute("prevXlinkStreamName", string.Empty);
                // Determine if this transaction already exists in our list of transactions. If not, add it.
                lock (_locker)
                {
                    int ii = _transactions.IndexOf(newTrans);
                    if (ii == -1) // -1 for no, >= zero for yes
                    {
                        newTrans.CompRules.Add(rule);
                        _transactions.Add(newTrans);
                    }
                    else // Transaction object is already in our list so we just add our CompRule object to it.
                    {
                        Transaction oldTrans = _transactions[ii];
                        oldTrans.CompRules.Add(rule);
                    }
                }
            }

            catch (Exception ecx)
            {
                String err = String.Format("Exception in Hist.comp_rule caught and logged.{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(err);
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Converts the XML attributes of a single \e move element from the \c hist command 
        /// to a Move object and then adds it to the Transaction 
        /// object being initialized. The Transaction object is added to our list of transactions 
        /// if not already there, or updated if already present.
        /// </summary>
        /// <param name="newTrans">The transaction object being initialized.</param>
        /// <param name="nav">Navigator object for querying the XML.</param>
        /// <returns>\e true if operation was successful, \e false otherwise.</returns>
        /// <exception cref="Exception">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> 
        /// on failure to handle a range of exceptions.</exception>
        private static bool move(Transaction newTrans, XPathNavigator nav)
        {
            bool ret = true; // assume success
            try
            {
                Move move = new Move();
                move.Destination = nav.GetAttribute("dest", String.Empty);
                move.Source = nav.GetAttribute("source", String.Empty);
                // Determine if this transaction already exists in our list of transactions. If not, add it.
                lock (_locker)
                {
                    int ii = _transactions.IndexOf(newTrans);
                    if (ii == -1) // -1 for no, >= zero for yes
                    {
                        newTrans.Moves.Add(move);
                        _transactions.Add(newTrans);
                    }
                    else // Transaction object is already in our list so we just add our move object to it.
                    {
                        Transaction oldTrans = _transactions[ii];
                        oldTrans.Moves.Add(move);
                    }
                }
            }

            catch (Exception ecx)
            {
                String err = String.Format("Exception in Hist.move caught and logged.{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(err);
                ret = false;
            }

            return ret;
        }
    }
}
