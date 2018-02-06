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
    /// An AccuRev stream from the [Streams section](@ref AcUtils#StreamsCollection) in <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    [Serializable]
    public sealed class StreamElement : ConfigurationElement
    {
        private static ConfigurationProperty _stream;
        private static ConfigurationPropertyCollection _properties;

        static StreamElement()
        {
            _stream = new ConfigurationProperty("stream", typeof(string),
                null, ConfigurationPropertyOptions.IsRequired);

            _properties = new ConfigurationPropertyCollection();
            _properties.Add(_stream);
        }

        /// <summary>
        /// The \e stream from the [Streams section](@ref AcUtils#StreamsCollection) in <tt>\<prog_name\>.exe.config</tt>.
        /// </summary>
        [ConfigurationProperty("stream", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string Stream
        {
            get { return (string)base[_stream]; }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }
    }
}
