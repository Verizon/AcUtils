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
    /// The [repositories section](@ref AcUtils#RepoCollection) in <tt>\<prog_name\>.exe.config</tt> 
    /// for multiple AccuRev server support.
    /// </summary>
    [Serializable]
    public sealed class RepoSection : ConfigurationSection
    {
        private static ConfigurationProperty _instance;
        private static ConfigurationPropertyCollection _properties;

        static RepoSection()
        {
            _instance = new ConfigurationProperty("instance", typeof(RepoCollection), 
                null, ConfigurationPropertyOptions.IsRequired);
            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_instance);
        }

        /// <summary>
        /// The list of [server-port pairs](@ref AcUtils#RepoCollection) from <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("instance")]
        public RepoCollection Repositories
        {
            get { return (RepoCollection)base[_instance]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
