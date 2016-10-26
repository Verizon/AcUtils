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
    /// An AccuRev user from the [Users section](@ref AcUtils#UsersCollection) in <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    [Serializable]
    public sealed class UserElement : ConfigurationElement
    {
        private static ConfigurationProperty _user; // AccuRev principal name
        private static ConfigurationProperty _name; // AcUser default name (display name from LDAP otherwise principal name)
        private static ConfigurationPropertyCollection _properties;

        static UserElement()
        {
            _user = new ConfigurationProperty("user", typeof(string),
                null, ConfigurationPropertyOptions.IsKey);
            _name = new ConfigurationProperty("name", typeof(string),
                null, ConfigurationPropertyOptions.None);

            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_user);
            _properties.Add(_name);
        }

        /// <summary>
        /// The \e user from the [Users section](@ref AcUtils#UsersCollection) in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        /// <remarks>The user's AccuRev principal name.</remarks>
        [ConfigurationProperty("user", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string User
        {
            get { return (string)base[_user]; }
        }

        /// <summary>
        /// The \e name from the [Users section](@ref AcUtils#UsersCollection) in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        /// <remarks>AcUser object default name: AcUser.DisplayName if it exists, otherwise the user's AccuRev principal name.</remarks>
        [ConfigurationProperty("name", DefaultValue = "", IsKey = false, IsRequired = false)]
        public string Name
        {
            get { return (string)base[_name]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
