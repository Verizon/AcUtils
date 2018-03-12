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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace AcUtils
{
    #region enums
    /*! \ingroup acenum */
    ///@{
    /// <summary>
    /// Use to specify real or virtual values.
    /// </summary>
    public enum RealVirtual
    {
        /*! \var Real
        Real value(s). */
        Real,
        /*! \var Virtual
        Virtual value(s). */
        Virtual
    };
    ///@}
    #endregion

    /// <summary>
    /// Custom query operators for use when using [LINQ to XML](https://msdn.microsoft.com/en-us/library/bb387098.aspx).
    /// </summary>
    /*! \sa <a href="_file_hist_8cs-example.html">FileHist.cs</a>, <a href="_user_changes_8cs-example.html">UserChanges.cs</a> */
    public static class Extensions
    {
        /// <summary>
        /// Convert \e element's Epoch time \e name XML attribute value to a .NET DateTime object.
        /// </summary>
        /// <param name="element">The element with the \e name attribute value to convert.</param>
        /// <param name="name">The name of the XML attribute, e.g. \b "time", \b "mtime", etc. emitted by the \c hist command.</param>
        /// <returns>On success a DateTime object with the converted value, otherwise \e null.</returns>
        /*! \code
            DateTime? mtime = version.acxTime("mtime"); // get version's last modified time
            \endcode */
        /*! \pre When querying \e version elements include both \c -e: <em>(\"expanded\")</em> and \c -v: <em>(\"verbose\")</em> 
             format directives in the \c hist command (i.e. \c -fevx) so the \b mtime attribute is included in the XML result. */
        public static DateTime? acxTime(this XElement element, XName name)
        {
            DateTime? dt = null;
            XAttribute attr = element.Attribute(name);
            if (attr != null)
                dt = AcDateTime.AcDate2DateTime((long)element.Attribute(attr.Name));
            return dt;
        }

        /// <summary>
        /// Convert \e element's type \e name attribute value to an AcUtils.ElementType enum.
        /// </summary>
        /// <param name="element">The element with the \e name attribute value to convert.</param>
        /// <param name="name">The name of the attribute, e.g. \b "elem_type", \b "elemType", etc.</param>
        /// <returns>On success an AcUtils.ElementType enum that is the converted value, otherwise \e ElementType.unknown.</returns>
        /*! \code
            ElementType type = v.acxType("elemType");
            \endcode */
        /*! \sa <a href="_x_linked_8cs-example.html">XLinked.cs</a> */
        public static ElementType acxType(this XElement element, XName name)
        {
            ElementType type = ElementType.unknown;
            XAttribute attr = element.Attribute(name);
            if (attr != null)
            {
                string temp = (string)element.Attribute(attr.Name);
                if (!String.Equals(temp, "* unknown *")) // known AccuRev defect
                    type = (ElementType)Enum.Parse(typeof(ElementType), temp);
            }

            return type;
        }

        /// <summary>
        /// Determine if \e transaction was a \c promote operation.
        /// </summary>
        /// <param name="transaction">The transaction to query.</param>
        /// <returns>\e true if \e transaction was a \c promote operation, \e false otherwise.</returns>
        public static bool acxIsPromote(this XElement transaction)
        {
            Debug.Assert(transaction.Name == "transaction", @"transaction.Name == ""transaction""");
            return String.Equals((string)transaction.Attribute("type"), "promote");
        }

        /// <summary>
        /// Get the from-stream version for the \c promote \e transaction.
        /// </summary>
        /// <param name="transaction">The transaction to query.</param>
        /// <returns>Formatted from-stream version, e.g. <tt>NEPTUNE_DEV2_thomas/7 (110/7)</tt>, 
        /// otherwise \e null if \e transaction was not a \c promote operation.</returns>
        public static string acxFromStream(this XElement transaction)
        {
            Debug.Assert(transaction.Name == "transaction", @"transaction.Name == ""transaction""");
            string name = null;
            if (transaction.acxIsPromote())
                name = $"{(string)transaction.Attribute("fromStreamName")} ({(int)transaction.Attribute("fromStreamNumber")})";

            return name;
        }

        /// <summary>
        /// Get the to-stream version for the \c promote \e transaction.
        /// </summary>
        /// <param name="transaction">The transaction to query.</param>
        /// <returns>Formatted to-stream version, e.g. <tt>NEPTUNE_DEV4/12 (8/12)</tt>, 
        /// otherwise \e null if \e transaction was not a \c promote operation.</returns>
        public static string acxToStream(this XElement transaction)
        {
            Debug.Assert(transaction.Name == "transaction", @"transaction.Name == ""transaction""");
            string name = null;
            if (transaction.acxIsPromote())
                name = $"{(string)transaction.Attribute("streamName")} ({(int)transaction.Attribute("streamNumber")})";

            return name;
        }

        /// <summary>
        /// Determine if \e transaction was a \c co operation.
        /// </summary>
        /// <param name="transaction">The transaction to query.</param>
        /// <returns>\e true if \e transaction was a \c co operation, \e false otherwise.</returns>
        public static bool acxIsCheckOut(this XElement transaction)
        {
            Debug.Assert(transaction.Name == "transaction", @"transaction.Name == ""transaction""");
            return String.Equals((string)transaction.Attribute("type"), "co");
        }

        /// <summary>
        /// Get the virtual-named version for the \c promote or \c co \e transaction.
        /// </summary>
        /// <param name="transaction">The transaction to query.</param>
        /// <returns>Formatted virtual-named version, e.g. <tt>MARS_UAT/4 (11/4)</tt> for the \c promote or \c co 
        /// \e transaction, otherwise \e null if not found.</returns>
        /*! \accunote_ For \c promote transactions, the \e virtualNamedVersion attribute value is correct only in 
            the first \e version element listed in the parent \e transaction. In all other \e version elements, the 
            \e virtualNamedVersion attribute has the same value as its \e realNamedVersion attribute sibling.
            This falls under AccuRev defect 18636, RPI # 1107275. */
        public static string acxVirtualNamed(this XElement transaction)
        {
            Debug.Assert(transaction.Name == "transaction", @"transaction.Name == ""transaction""");
            string name = null;
            if (transaction.acxIsPromote() || transaction.acxIsCheckOut())
            {
                XElement first = transaction.Elements("version").FirstOrDefault();
                if (first != null)
                    name = $"{(string)first.Attribute("virtualNamedVersion")} ({(string)first.Attribute("virtual")})";
            }

            return name;
        }

        /// <summary>
        /// Get the real-named version for \e version.
        /// </summary>
        /// <param name="version">The version to query.</param>
        /// <returns>Formatted real-named version, e.g. <tt>MARS_DEV3_barnyrd/2 (17/2)</tt>, 
        /// otherwise \e null if not found.</returns>
        /*! \accunote_ For \c promote transactions, the \e realNamedVersion attribute value in the transaction's 
            first \e version element is the same in the second \e version element. Therefore, in this case the 
            first \e version element is ignored and \e null is returned. */
        public static string acxRealNamed(this XElement version)
        {
            Debug.Assert(version.Name == "version", @"version.Name == ""version""");
            XElement transaction = version.Parent;
            XElement first = transaction.Elements("version").FirstOrDefault();
            // return null for promote transactions when this version is the first version element in the transaction
            if (first != null && version == first && transaction.acxIsPromote())
                return null;

            string name = $"{(string)version.Attribute("realNamedVersion")} ({(string)version.Attribute("real")})";
            return name;
        }

        /// <summary>
        /// Get the ancestor-named version for \e version.
        /// </summary>
        /// <param name="version">The version to query.</param>
        /// <returns>Formatted ancestor-named version, e.g. <tt>NEPTUNE_DEV4_thomas/6 (114/6)</tt>, 
        /// otherwise \e null if not found.</returns>
        public static string acxAncestorNamed(this XElement version)
        {
            Debug.Assert(version.Name == "version", @"version.Name == ""version""");
            string name = null;
            string ancestorNamedVersion = (string)version.Attribute("ancestorNamedVersion");
            if (!String.IsNullOrEmpty(ancestorNamedVersion))
                name = $"{ancestorNamedVersion} ({(string)version.Attribute("ancestor")})";
            return name;
        }

        /// <summary>
        /// Get the merged-against-named version for \e version.
        /// </summary>
        /// <param name="version">The version to query.</param>
        /// <returns>Formatted merged-against-named version, e.g. <tt>JUPITER_DEV1_robert/5 (142/5)</tt>, 
        /// otherwise \e null if not found.</returns>
        public static string acxMergedAgainstNamed(this XElement version)
        {
            Debug.Assert(version.Name == "version", @"version.Name == ""version""");
            string name = null;
            string mergedAgainstNamedVersion = (string)version.Attribute("mergedAgainstNamedVersion");
            if (!String.IsNullOrEmpty(mergedAgainstNamedVersion))
                name = $"{mergedAgainstNamedVersion} ({(string)version.Attribute("merged_against")})";
            return name;
        }

        /// <summary>
        /// Get the comment for the transaction or version \e element.
        /// </summary>
        /// <param name="element">The transaction or version element to query.</param>
        /// <returns>The \e comment element value, e.g. <tt>\<comment\><b>Fix defect 452.</b>\</comment\></tt>, 
        /// otherwise \e null if no comment exists or if \e element is a version element and the first in a 
        /// \c promote transaction.</returns>
        /*! \accunote_ For \c promote transactions, the \e comment element above the first \e version element 
            listed in the parent \e transaction is the transaction comment and not the version comment. 
            Therefore in this case, when querying version elements, the first \e version element in the 
            \e transaction is ignored and \e null is returned. */
        public static string acxComment(this XElement element)
        {
            Debug.Assert(element.Name == "transaction" || element.Name == "version",
                @"element.Name == ""transaction"" || element.Name == ""version""");
            XElement xe = null;
            if (element.Name == "transaction")
                xe = element.Elements().FirstOrDefault();
            else if (element.Name == "version")
            {
                XElement trans = element.Parent;
                XElement first = trans.Elements("version").FirstOrDefault();
                // return null for promote transactions when this version is the first version element in the transaction
                if (first != null && element == first && trans.acxIsPromote())
                    return null;
                // get sibling element directly above this version element
                xe = element.ElementsBeforeSelf().LastOrDefault();
            }

            string comment = null;
            if (xe != null && xe.Name == "comment")
                comment = (string)xe;
            return comment;
        }

        /// <summary>
        /// Get the workspace name for \e version.
        /// </summary>
        /// <param name="version">The version to query.</param>
        /// <returns>Name of the workspace.</returns>
        public static string acxWSpaceName(this XElement version)
        {
            Debug.Assert(version.Name == "version", @"version.Name == ""version""");
            string temp = (string)version.Attribute("realNamedVersion");
            string name = temp.Substring(0, temp.IndexOf('/'));
            return name;
        }

        /// <summary>
        /// Get the workspace owner's principal name for \e version.
        /// </summary>
        /// <param name="version">The version to query.</param>
        /// <returns>Name of the principal that owns the workspace, otherwise \e null if not found.</returns>
        public static string acxWSpaceOwner(this XElement version)
        {
            Debug.Assert(version.Name == "version", @"version.Name == ""version""");
            string prncpl = null;
            string wspace = version.acxWSpaceName();
            int ii = wspace.LastIndexOf('_');
            if (ii != -1) prncpl = wspace.Substring(++ii);
            return prncpl;
        }

        /// <summary>
        /// Get the real or virtual stream and version numbers for \e version.
        /// </summary>
        /// <param name="version">The version to query.</param>
        /// <param name="request">Whether real or virtual values should be returned.</param>
        /// <returns>An array initialized as <tt>int[]={[real|virtual]StreamNumber, [real|virtual]VersionNumber}</tt> 
        /// as per \e request on success, otherwise \e null on error.</returns>
        /*! \sa [Stat.getElement](@ref AcUtils#Stat#getElement) */
        public static int[] acxStreamVersion(this XElement version, RealVirtual request)
        {
            Debug.Assert(version.Name == "version", @"version.Name == ""version""");
            string temp = (request == RealVirtual.Real) ? (string)version.Attribute("real") :
                (string)version.Attribute("virtual");
            if (temp == null) return null;
            string[] val = temp.Split('/');
            int[] arr = new int[2];
            if (!Int32.TryParse(val[0], NumberStyles.Integer, null, out arr[0])) return null;
            if (!Int32.TryParse(val[1], NumberStyles.Integer, null, out arr[1])) return null;
            return arr;
        }

        /// <summary>
        /// Get the real or virtual stream name for \e version.
        /// </summary>
        /// <param name="version">The version to query.</param>
        /// <param name="request">Whether the real or virtual stream name should be returned.</param>
        /// <returns>The stream name as per \e request on success, otherwise \e null on error.</returns>
        /*! \sa [Stat.getElement](@ref AcUtils#Stat#getElement) */
        public static string acxStreamName(this XElement version, RealVirtual request)
        {
            Debug.Assert(version.Name == "version", @"version.Name == ""version""");
            string temp = (request == RealVirtual.Real) ? (string)version.Attribute("realNamedVersion") :
                (string)version.Attribute("virtualNamedVersion");
            if (temp == null) return null;
            string stream = temp.Substring(0, temp.IndexOf('/'));
            return stream;
        }
    }
}
