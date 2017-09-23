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
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using AcUtils;

namespace AcUtils
{
    /// <summary>
    /// Custom query operators for use when using [LINQ to XML](https://msdn.microsoft.com/en-us/library/bb387098.aspx).
    /// </summary>
    /*! \sa <a href="_file_hist_8cs-example.html">FileHist.cs</a>, <a href="_user_changes_8cs-example.html">UserChanges.cs</a> */
    public static class Extensions
    {
        /// <summary>
        /// Convert \e element's Epoch time \e name attribute value to a .NET DateTime object.
        /// </summary>
        /// <param name="element">The element with the \e name attribute value to convert.</param>
        /// <param name="name">The name of the attribute, e.g. \b "time", \b "mtime", etc.</param>
        /// <returns>On success a DateTime object with the converted value, otherwise \e null.</returns>
        /*! \code
            DateTime? mtime = v.acxTime("mtime"); // version's last modified time
            \endcode */
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
            {
                name = String.Format("{0} ({1})", (string)transaction.Attribute("fromStreamName"),
                    (int)transaction.Attribute("fromStreamNumber"));
            }

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
            {
                name = String.Format("{0} ({1})", (string)transaction.Attribute("streamName"),
                    (int)transaction.Attribute("streamNumber"));
            }

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
                    name = String.Format("{0} ({1})", (string)first.Attribute("virtualNamedVersion"),
                        (string)first.Attribute("virtual"));
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

            string name = String.Format("{0} ({1})", (string)version.Attribute("realNamedVersion"),
                (string)version.Attribute("real"));
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
                name = String.Format("{0} ({1})", ancestorNamedVersion, (string)version.Attribute("ancestor"));
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
                name = String.Format("{0} ({1})", mergedAgainstNamedVersion, (string)version.Attribute("merged_against"));
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
    }
}
