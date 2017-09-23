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
using System.Configuration;

namespace AcUtils
{
    /// <summary>
    /// The \e activeDir section in <tt>\<prog_name\>.exe.config</tt>. 
    /// Supports multiple Active Directory domains plus user properties defined 
    /// beyond the <a href="class_ac_utils_1_1_ac_user.html#properties">default set</a>.
    /// </summary>
    /*! \sa DomainCollection, PropCollection, AcUser.Other */
    [Serializable]
    public sealed class ADSection : ConfigurationSection
    {
        private static ConfigurationProperty _domains;
        private static ConfigurationProperty _props;
        private static ConfigurationPropertyCollection _properties;

        static ADSection()
        {
            _domains = new ConfigurationProperty("domains", typeof(DomainCollection),
                null, ConfigurationPropertyOptions.IsRequired);
            _props = new ConfigurationProperty("properties", typeof(PropCollection),
                null, ConfigurationPropertyOptions.IsRequired);
            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_domains);
            _properties.Add(_props);
        }

        /// <summary>
        /// The list of [domains](@ref AcUtils#DomainCollection) from <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("domains")]
        public DomainCollection Domains
        {
            get { return (DomainCollection)base[_domains]; }
        }

        /// <summary>
        /// The list of [properties](@ref AcUtils#PropCollection) from <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("properties")]
        public PropCollection Props
        {
            get { return (PropCollection)base[_props]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
