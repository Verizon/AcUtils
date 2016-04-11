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
    /// The [Depots section](@ref AcUtils#DepotsCollection) in <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    [Serializable]
    public sealed class DepotsSection : ConfigurationSection
    {
        private static ConfigurationProperty _depots;
        private static ConfigurationPropertyCollection _properties;

        static DepotsSection()
        {
            _depots = new ConfigurationProperty("depots", typeof(DepotsCollection),
                null, ConfigurationPropertyOptions.IsRequired);
            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_depots);
        }

        /// <summary>
        /// The list of [depots](@ref AcUtils#DepotsCollection) from <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("depots")]
        public DepotsCollection Depots
        {
            get { return (DepotsCollection)base[_depots]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
