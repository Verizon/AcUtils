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
    /// An AccuRev repository [server-port pair](@ref AcUtils#RepoCollection) in <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    [Serializable]
    public sealed class RepoElement : ConfigurationElement
    {
        private static ConfigurationProperty _server;
        private static ConfigurationProperty _port;
        private static ConfigurationPropertyCollection _properties;

        static RepoElement()
        {
            _server = new ConfigurationProperty("server", typeof(string),
                null, ConfigurationPropertyOptions.IsRequired);
            _port = new ConfigurationProperty("port", typeof(int),
                5050, ConfigurationPropertyOptions.IsRequired);

            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_server);
            _properties.Add(_port);
        }

        /// <summary>
        /// The \e server from a [server-port pair](@ref AcUtils#RepoCollection) 
        /// in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("server", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string Server
        {
            get { return (string)base[_server]; }
        }

        /// <summary>
        /// The \e port from a [server-port pair](@ref AcUtils#RepoCollection) 
        /// in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("port", IsKey = false, IsRequired = true)]
        public int Port
        {
            get { return (int)base[_port]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
