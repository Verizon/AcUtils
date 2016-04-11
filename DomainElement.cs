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
    /// An Active Directory [domain element](@ref AcUtils#DomainCollection) 
    /// \e host-path pair from <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    [Serializable]
    public sealed class DomainElement : ConfigurationElement
    {
        private static ConfigurationProperty _host;
        private static ConfigurationProperty _path;
        private static ConfigurationPropertyCollection _properties;

        static DomainElement()
        {
            _host = new ConfigurationProperty("host", typeof(string),
                null, ConfigurationPropertyOptions.IsRequired);
            _path = new ConfigurationProperty("path", typeof(string),
                null, ConfigurationPropertyOptions.IsRequired);

            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_host);
            _properties.Add(_path);
        }

        /// <summary>
        /// The \e host from a [host-path pair](@ref AcUtils#DomainCollection) 
        /// in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("host", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string Host
        {
            get { return (string)base[_host]; }
        }

        /// <summary>
        /// The \e path from a [host-path pair](@ref AcUtils#DomainCollection) 
        /// in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("path", DefaultValue = "", IsKey = false, IsRequired = true)]
        public string Path
        {
            get { return (string)base[_path]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
