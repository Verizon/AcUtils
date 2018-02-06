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
using System.Configuration;

namespace AcUtils
{
    /// <summary>
    /// An Active Directory [user property](@ref AcUtils#PropCollection) 
    /// element \e field-title pair from <tt>\<prog_name\>.exe.config</tt>. 
    /// These are user properties not in the regular 
    /// <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a>.
    /// </summary>
    /*! \sa [AcUser.Other](@ref AcUtils#AcUser#Other) */
    [Serializable]
    public sealed class PropElement : ConfigurationElement
    {
        private static ConfigurationProperty _field;
        private static ConfigurationProperty _title;
        private static ConfigurationPropertyCollection _properties;

        static PropElement()
        {
            _field = new ConfigurationProperty("field", typeof(string),
                null, ConfigurationPropertyOptions.IsRequired);
            _title = new ConfigurationProperty("title", typeof(string),
                null, ConfigurationPropertyOptions.IsRequired);

            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_field);
            _properties.Add(_title);
        }

        /// <summary>
        /// The \e field from a [field-title pair](@ref AcUtils#PropCollection) 
        /// in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("field", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string Field
        {
            get { return (string)base[_field]; }
        }

        /// <summary>
        /// The \e title from a [field-title pair](@ref AcUtils#PropCollection) 
        /// in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("title", DefaultValue = "", IsKey = false, IsRequired = true)]
        public string Title
        {
            get { return (string)base[_title]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
