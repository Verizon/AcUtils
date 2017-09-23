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
    /// Whether [AppliesTo](@ref AcUtils#AcPermission#AppliesTo) is the principal 
    /// name for a \b group, \b user, or \b builtin type.
    /// </summary>
    public enum PermType {
        /*! \var group
        AppliesTo is the principal name for a group. */
        group,
        /*! \var user
        AppliesTo is the principal name for a user. */
        user,
        /*! \var builtin
        AppliesTo is \b authuser or \b anyuser. */
        builtin
    };

    /// <summary>
    /// Whether permission rights to [Name](@ref AcUtils#AcPermission#Name) 
    /// in [AppliesTo](@ref AcUtils#AcPermission#AppliesTo) is \b all or \b none.
    /// </summary>
    public enum PermRights
    {
        /*! \var none
        AppliesTo does not have permission to access Name. */
        none,
        /*! \var all
        AppliesTo has permission to access Name. */
        all
    };

    /// <summary>
    /// Whether permissions pertain to depots or streams. 
    /// Set when the permission is created by the \c setacl command.
    /// </summary>
    public enum PermKind
    {
        /*! \var depot
        Permissions pertain to depots. */
        depot,
        /*! \var stream
        Permissions pertain to streams. */
        stream
    };
    ///@}
    #endregion

    /// <summary>
    /// A permission object that defines the attributes of an AccuRev 
    /// <a href="https://supportline.microfocus.com/Documentation/books/AccuRev/AccuRev/7.0.1/webhelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_setacl.html">access control list (ACL) entry</a>.
    /// </summary>
    [Serializable]
    public sealed class AcPermission : IFormattable, IEquatable<AcPermission>, IComparable<AcPermission>, IComparable
    {
        #region class variables
        private PermKind _kind; // depot or stream
        private string _name; // name of depot or stream permission applies to
        private string _appliesTo; // principal of group or user permission applies to or one of the built-in types anyuser or authuser
        private PermType _type; // whether _appliesTo is a "group", "user", "builtin"
        private PermRights _rights; // whether permission rights is "all" or "none"

        // depots: when true permission applies to the depot and its entire stream hierarchy, 
        // when false permission applies only to AccuWork issues in the depot and not its version-controlled elements
        // streams: when true permission applies to the stream and its entire subhierarchy, 
        // when false permission applies only to the stream
        private bool _inheritable;
        #endregion

        /// <summary>
        /// Constructor used during AcPermissions list construction. It is called internally and not by user code. 
        /// </summary>
        internal AcPermission(PermKind kind)
        {
            _kind = kind;
        }

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcPermission. 
        /// Uses [Kind](@ref AcUtils#AcPermission#Kind), [Name](@ref AcUtils#AcPermission#Name), 
        /// and [AppliesTo](@ref AcUtils#AcPermission#AppliesTo) to compare instances.
        /// </summary>
        /// <param name="other">The AcPermission object being compared to \e this instance.</param>
        /// <returns>\e true if AcPermission \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcPermission other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            var left = Tuple.Create(Kind, Name, AppliesTo);
            var right = Tuple.Create(other.Kind, other.Name, other.AppliesTo);
            return left.Equals(right);
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcPermission)](@ref AcPermission#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return this.Equals(other as AcPermission);
        }

        /// <summary>
        /// Override appropriate for type AcPermission.
        /// </summary>
        /// <returns>Hash of [Kind](@ref AcUtils#AcPermission#Kind), [Name](@ref AcUtils#AcPermission#Name) and 
        /// [AppliesTo](@ref AcUtils#AcPermission#AppliesTo).</returns>
        public override int GetHashCode()
        {
            var hash = Tuple.Create(Kind, Name, AppliesTo);
            return hash.GetHashCode();
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcPermission objects to sort 
        /// by [Name](@ref AcUtils#AcPermission#Name) and then [AppliesTo](@ref AcUtils#AcPermission#AppliesTo).
        /// </summary>
        /// <param name="other">An AcPermission object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcPermission objects being compared.</returns>
        /*! \sa [AcPermissions constructor example](@ref AcUtils#AcPermissions#AcPermissions) */
        public int CompareTo(AcPermission other)
        {
            int result;
            if (AcPermission.ReferenceEquals(this, other))
                result = 0;
            else
            {
                result = String.Compare(Name, other.Name);
                if (result == 0)
                    result = String.Compare(AppliesTo, other.AppliesTo);
            }

            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcPermission object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcPermission)](@ref AcPermission#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcPermission object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcPermission))
                throw new ArgumentException("Argument is not an AcPermission", "other");
            AcPermission o = (AcPermission)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// Whether the permission pertains to a depot or stream.
        /// </summary>
        public PermKind Kind
        {
            get { return _kind; }
            internal set { _kind = value; }
        }

        /// <summary>
        /// Name of the depot or stream this permission applies to.
        /// </summary>
        public string Name
        {
            get { return _name ?? String.Empty; }
            internal set { _name = value; }
        }

        /// <summary>
        /// Principal name of the group or user this permission applies to 
        /// or one of the built-in types \b anyuser or \b authuser.
        /// </summary>
        public string AppliesTo
        {
            get { return _appliesTo ?? String.Empty; }
            internal set { _appliesTo = value; }
        }

        /// <summary>
        /// Whether [AppliesTo](@ref AcUtils#AcPermission#AppliesTo) is the principal 
        /// name for a \b group, \b user, or a \b builtin type.
        /// </summary>
        public PermType Type
        {
            get { return _type; }
            internal set { _type = value; }
        }

        /// <summary>
        /// Whether permission rights to [Name](@ref AcUtils#AcPermission#Name) 
        /// in [AppliesTo](@ref AcUtils#AcPermission#AppliesTo) is \b all or \b none.
        /// </summary>
        public PermRights Rights
        {
            get { return _rights; }
            internal set { _rights = value; }
        }

        /// <summary>
        /// For [depots](@ref AcUtils#PermKind), when \e true permission applies to the depot and 
        /// its entire stream hierarchy, when \e false permission applies only to AccuWork issues 
        /// in the depot and not its version-controlled elements.<br>
        /// For [streams](@ref AcUtils#PermKind), when \e true permission applies to the stream and 
        /// its entire subhierarchy, when \e false permission applies only to the stream.
        /// </summary>
        public bool Inheritable
        {
            get { return _inheritable; }
            internal set { _inheritable = value; }
        }

        #region ToString
        /// <summary>
        /// The ToString implementation.
        /// </summary>
        /// <param name="format">The format specifier to use, 
        /// e.g. <b>Console.WriteLine(permission.ToString("A"));</b></param>
        /// <param name="provider">Allow clients to format output for their own types using 
        /// [ICustomFormatter](https://msdn.microsoft.com/en-us/library/system.icustomformatter.aspx).</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="FormatException">thrown if an invalid format string is specified.</exception>
        /// \par Format specifiers:
        /// \arg \c G Default when not using a format specifier.
        /// \arg \c K Kind - whether the permission pertains to a depot or stream. 
        /// \arg \c N Name - name of the depot or stream this permission applies to.
        /// \arg \c A AppliesTo - principal name of the group or user this permission applies to or one of the built-in types \b anyuser or \b authuser.
        /// \arg \c T Type - whether [AppliesTo](@ref AcUtils#AcPermission#AppliesTo) is the principal name for a \b group, \b user, or \b builtin type.
        /// \arg \c R Rights - whether permission rights to [Name](@ref AcUtils#AcPermission#Name) in [AppliesTo](@ref AcUtils#AcPermission#AppliesTo) is \b all or \b none.
        /// \arg \c I Inheritable - For [depots](@ref AcUtils#PermKind), when \e true permission applies to the depot and its entire stream hierarchy, 
        /// when \e false permission applies only to AccuWork issues in the depot and not its version-controlled elements. For [streams](@ref AcUtils#PermKind), 
        /// when \e true permission applies to the stream and its entire subhierarchy, when \e false permission applies only to the stream.
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
                    string who = String.Format("{0}", (Type == PermType.builtin) ? AppliesTo : Type.ToString() + " " + AppliesTo);
                    string text = String.Format(@"Permission on {0} {1} applies to {2} {{{3}, {4}}}",
                        Name, Kind, who, Rights, Inheritable ? "inherit" : "no inherit");
                    return text;
                }
                case "K": // whether the permission pertains to a depot or stream
                    return Kind.ToString();
                case "N": // name of the depot or stream this permission applies to
                    return Name;
                case "A": // principal name of the group or user this permission applies to or one of the built-in types "anyuser" or "authuser"
                    return AppliesTo;
                case "T": // whether AppliesTo is the principal name for a "group", "user", or a "builtin" type
                    return Type.ToString();
                case "R": // whether permission rights to Name in AppliesTo is "all" or "none"
                    return Rights.ToString();

                    // For a depot, when true permission applies to the depot and its entire 
                    // stream hierarchy, when false permission applies only to AccuWork issues 
                    // in the depot and not its version-controlled elements.
                    // For a stream, when true permission applies to the stream and its entire 
                    // subhierarchy, when false permission applies only to the stream.
                case "I":
                    return Inheritable.ToString();
                default:
                    throw new FormatException(String.Format("The {0} format string is not supported.", format));
            }
        }

        // Calls ToString(string, IFormatProvider) version with a null IFormatProvider argument.
        public string ToString(string format)
        {
            return ToString(format, null);
        }

        // Calls ToString(string, IFormatProvider) version with the general format 
        // and a null IFormatProvider argument.
        public override string ToString()
        {
            return ToString("G", null);
        }
        #endregion ToString
    }

    /// <summary>
    /// A container of AcPermission objects that define AccuRev 
    /// <a href="https://supportline.microfocus.com/Documentation/books/AccuRev/AccuRev/7.0.1/webhelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_setacl.html">access control list (ACL) entries</a>.
    /// </summary>
    [Serializable]
    public sealed class AcPermissions : List<AcPermission>
    {
        #region class variables
        private PermKind _kind; // whether the list of permissions held pertain to depots or streams
        [NonSerialized] private readonly object _locker = new object();
        #endregion

        /// <summary>
        /// Whether the list of permissions held pertain to depots or streams.
        /// </summary>
        public PermKind Kind
        {
            get { return _kind; }
            internal set { _kind = value; }
        }

        #region object construction:
        //! \name Two-part object construction:
        //@{
        /// <summary>
        /// A container of AcPermission objects that define AccuRev 
        /// <a href="https://supportline.microfocus.com/Documentation/books/AccuRev/AccuRev/7.0.1/webhelp/wwhelp/wwhimpl/js/html/wwhelp.htm#href=AccuRev_User_CLI/cli_ref_setacl.html">access control list (ACL) entries</a>.
        /// </summary>
        /*! \code
            // get the list of permissions for all depots
            AcPermissions permissions = new AcPermissions(PermKind.depot);
            if (!(await permissions.initAsync())) return false;

            // show permissions on depots JUPITER and NEPTUNE
            IEnumerable<AcPermission> filter = permissions.Where(n => n.Name.Equals("NEPTUNE") || n.Name.Equals("JUPITER"));
            foreach (AcPermission permission in filter.OrderBy(n => n)) // use default comparer
                Console.WriteLine(permission);
            ...
            Permission on JUPITER depot applies to group Admin {all, inherit}
            Permission on JUPITER depot applies to anyuser {all, no inherit}
            Permission on JUPITER depot applies to group IT-Reporting {none, no inherit}
            Permission on JUPITER depot applies to group PAT {all, inherit}
            Permission on JUPITER depot applies to user robert {all, no inherit}
            Permission on NEPTUNE depot applies to group Admin {all, inherit}
            Permission on NEPTUNE depot applies to user barnyrd {all, inherit}
            Permission on NEPTUNE depot applies to group IT-Reporting {none, no inherit}
            Permission on NEPTUNE depot applies to group Omnipotent {none, inherit}
            Permission on NEPTUNE depot applies to group PAT {all, inherit}
            \endcode */
        /*! \sa initAsync, [default comparer](@ref AcPermission#CompareTo), [AcDepots.canViewAsync](@ref AcDepots#canViewAsync), 
            <a href="_show_permissions_8cs-example.html">ShowPermissions.cs</a> */
        public AcPermissions(PermKind kind)
        {
            _kind = kind;
        }

        /// <summary>
        /// Populate this container with AcPermission objects.
        /// </summary>
        /// <param name="name">Optional depot or stream name as per 
        /// [constructor parameter](@ref AcUtils#AcPermissions#AcPermissions) \e kind, otherwise all.</param>
        /// <returns>\e true if no failure occurred and list was initialized successfully, \e false otherwise.</returns>
        /// <exception cref="AcUtilsException">caught and [logged](@ref AcUtils#AcDebug#initAcLogging) 
        /// in <tt>\%LOCALAPPDATA\%\\AcTools\\Logs\\<prog_name\>-YYYY-MM-DD.log</tt> on \c lsacl command failure.</exception>
        /// <exception cref="Exception">caught and logged in same on failure to handle a range of exceptions.</exception>
        /*! \sa [AcPermissions constructor](@ref AcUtils#AcPermissions#AcPermissions) */
        /*! \lsacl_ <tt>lsacl -fx {stream|depot} \<name\></tt> */
        public async Task<bool> initAsync(string name = null)
        {
            bool ret = false; // assume failure
            try
            {
                string cmd = String.Format(@"lsacl -fx {0} ""{1}""", _kind, name);
                AcResult r = await AcCommand.runAsync(cmd).ConfigureAwait(false);
                if (r != null && r.RetVal == 0)
                {
                    XElement xml = XElement.Parse(r.CmdResult);
                    IEnumerable<XElement> query = from element in xml.Descendants("Element") select element;
                    foreach (XElement e in query)
                    {
                        AcPermission perm = new AcPermission(_kind);
                        perm.Name = (string)e.Attribute("Name");
                        perm.AppliesTo = (string)e.Attribute("Group");
                        string type = (string)e.Attribute("Type");
                        perm.Type = (PermType)Enum.Parse(typeof(PermType), type);
                        string rights = (string)e.Attribute("Rights");
                        perm.Rights = (PermRights)Enum.Parse(typeof(PermRights), rights);
                        perm.Inheritable = (bool)e.Attribute("Inheritable");
                        lock (_locker) { Add(perm); }
                    }

                    ret = true; // operation succeeded
                }
            }

            catch (AcUtilsException ecx)
            {
                string msg = String.Format("AcUtilsException caught and logged in AcPermissions.initAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            catch (Exception ecx)
            {
                string msg = String.Format("Exception caught and logged in AcPermissions.initAsync{0}{1}",
                    Environment.NewLine, ecx.Message);
                AcDebug.Log(msg);
            }

            return ret;
        }
        //@}
        #endregion
    }
}
