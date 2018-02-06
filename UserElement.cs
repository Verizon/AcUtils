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
    /// A user's domain (login) ID or email address from the [Users section](@ref AcUtils#UsersCollection) in <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    [Serializable]
    public sealed class UserElement : ConfigurationElement
    {
        private static ConfigurationProperty _user; // domain (login) ID or email address
        private static ConfigurationPropertyCollection _properties;

        static UserElement()
        {
            _user = new ConfigurationProperty("user", typeof(string),
                null, ConfigurationPropertyOptions.IsKey);

            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_user);
        }

        /// <summary>
        /// The \e user from the [Users section](@ref AcUtils#UsersCollection) in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        /// <remarks>The user's domain (login) ID or email address.</remarks>
        [ConfigurationProperty("user", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string User
        {
            get { return (string)base[_user]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
