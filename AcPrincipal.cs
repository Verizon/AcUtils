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
using System.Diagnostics;

namespace AcUtils
{
    #region enums
    /*! \ingroup acenum */
    ///@{
    /// <summary>
    /// Whether the principal is active or inactive in AccuRev.
    /// </summary>
    public enum PrinStatus
    {
        /*! \var Unknown
        The principal status is \e unknown. */
        Unknown,
        /*! \var Inactive
        The principal is \e inactive. */
        Inactive,
        /*! \var Active
        The principal is \e active. */
        Active
    };
    ///@}
    #endregion

    /// <summary>
    /// Contains the AccuRev principal attributes \e name, \e ID and \e status (active or inactive) 
    /// for users and groups. Additionally, lists of group [members](@ref AcUtils#AcGroups#initMembersListAsync) 
    /// and user group [memberships](@ref AcUtils#AcUser#initGroupsListAsync) are initialized optionally 
    /// as per [AcGroups](@ref AcUtils#AcGroups#AcGroups) and [AcUsers](@ref AcUtils#AcUsers#AcUsers) 
    /// constructor parameters \e includeMembersList and \e includeGroupsList respectively.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{Name} ({ID}) {Status}")]
    public sealed class AcPrincipal : IEquatable<AcPrincipal>, IComparable<AcPrincipal>, IComparable
    {
        #region Class variables
        private int _id;
        private string _name;
        private PrinStatus _status = PrinStatus.Unknown;
        private SortedSet<string> _members;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor used during AcUsers and AcGroups list construction. It is called internally and not by user code. 
        /// </summary>
        internal AcPrincipal() { }
        #endregion

        #region Equality comparison
        /*! \name Equality comparison */
        /**@{*/
        /// <summary>
        /// IEquatable implementation to determine the equality of instances of type AcPrincipal. 
        /// Uses the AccuRev principal ID number to compare instances.
        /// </summary>
        /// <param name="other">The AcPrincipal object being compared to \e this instance.</param>
        /// <returns>\e true if AcPrincipal \e other is the same, \e false otherwise.</returns>
        public bool Equals(AcPrincipal other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ID == other.ID;
        }

        /// <summary>
        /// Overridden to determine equality.
        /// </summary>
        /// <returns>Return value of generic [Equals(AcPrincipal)](@ref AcPrincipal#Equals) version.</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return Equals(other as AcPrincipal);
        }

        /// <summary>
        /// Override appropriate for type AcPrincipal.
        /// </summary>
        /// <returns>AccuRev principal ID number since it's immutable and unique across both users and groups.</returns>
        public override int GetHashCode()
        {
            return ID;
        }
        /**@}*/
        #endregion

        #region Order comparison
        /*! \name Order comparison */
        /**@{*/
        /// <summary>
        /// Generic IComparable implementation (default) for comparing AcPrincipal objects to sort by AccuRev principal name.
        /// </summary>
        /// <param name="other">An AcPrincipal object to compare with this instance.</param>
        /// <returns>Value indicating the relative order of the AcPrincipal objects being compared.</returns>
        public int CompareTo(AcPrincipal other)
        {
            int result;
            if (AcPrincipal.ReferenceEquals(this, other))
                result = 0;
            else
                result = String.Compare(Name, other.Name);
            return result;
        }

        /// <summary>
        /// Pre-generic interface implementation for code using reflection.
        /// </summary>
        /// <param name="other">An AcPrincipal object to compare with this instance.</param>
        /// <returns>Return value of generic [CompareTo(AcPrincipal)](@ref AcPrincipal#CompareTo) version.</returns>
        /// <exception cref="ArgumentException">thrown if argument is not an AcPrincipal object.</exception>
        int IComparable.CompareTo(object other)
        {
            if (!(other is AcPrincipal))
                throw new ArgumentException("Argument is not an AcPrincipal", "other");
            AcPrincipal o = (AcPrincipal)other;
            return this.CompareTo(o);
        }
        /**@}*/
        #endregion

        /// <summary>
        /// AccuRev principal ID number for the user or group.
        /// </summary>
        public int ID
        {
            get { return _id; }
            internal set { _id = value; }
        }

        /// <summary>
        /// AccuRev principal name for the user or group.
        /// </summary>
        public string Name
        {
            get { return _name ?? String.Empty; }
            internal set { _name = value; }
        }

        /// <summary>
        /// Whether the principal is active or inactive in AccuRev.
        /// </summary>
        public PrinStatus Status
        {
            get { return _status; }
            internal set { _status = value; }
        }

        /// <summary>
        /// The list of groups a user has membership in, or the list of principals (users and groups) 
        /// in a group. Both initialized optionally as per [AcGroups](@ref AcUtils#AcGroups#AcGroups) 
        /// and [AcUsers](@ref AcUtils#AcUsers#AcUsers) constructor parameters \e includeMembersList 
        /// and \e includeGroupsList respectively.
        /// </summary>
        /*! \accunote_ We use a set object as a workaround for <tt>show -fx -u \<user\> groups</tt> where 
             the same group names are repeated multiple times. AccuRev defect 28001. */
        public SortedSet<string> Members
        {
            get { return _members; }
            internal set { _members = value; }
        }

        /// <summary>
        /// Returns the AccuRev principal name.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }
    }
}
