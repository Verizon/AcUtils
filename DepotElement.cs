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
    /// An AccuRev depot from the [Depots section](@ref AcUtils#DepotsCollection) in <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    [Serializable]
    public sealed class DepotElement : ConfigurationElement
    {
        private static ConfigurationProperty _depot;
        private static ConfigurationPropertyCollection _properties;

        static DepotElement()
        {
            _depot = new ConfigurationProperty("depot", typeof(string),
                null, ConfigurationPropertyOptions.IsRequired);

            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_depot);
        }

        /// <summary>
        /// The \e depot from the [Depots section](@ref AcUtils#DepotsCollection) in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("depot", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string Depot
        {
            get { return (string)base[_depot]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
