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
    /// The list of AccuRev streams from <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    /*! \code
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <configSections>
            <section name="Streams" type="AcUtils.StreamsSection, AcUtils, Version=1.4.0.0, Culture=neutral, PublicKeyToken=26470c2daf5c2e2f, processorArchitecture=MSIL" />
            ...
          </configSections>
          <Streams>
            <streams>
              <add stream="JUPITER_DEV1" />
              <add stream="JUPITER_DEV2" />
              <add stream="NEPTUNE_MAINT" />
              <add stream="NEPTUNE_MAINT1" />
              <add stream="NEPTUNE_MAINT2" />
              ...
            </streams>
          </Streams>
          <appSettings>
            ...
        StreamsSection streamsSection = ConfigurationManager.GetSection("Streams") as StreamsSection;
        StreamsCollection streamsCol = streamsSection.Streams;
        \endcode */
    /*! \sa <a href="_promotion_rights_8cs-example.html">PromotionRights.cs</a>, DepotsCollection */
    [ConfigurationCollection(typeof(StreamElement),
        CollectionType=ConfigurationElementCollectionType.AddRemoveClearMap)]
    [Serializable]
    public sealed class StreamsCollection : ConfigurationElementCollection
    {
        private static ConfigurationPropertyCollection _properties;
        static StreamsCollection()
        {
            _properties = new ConfigurationPropertyCollection();
        }

        public StreamsCollection() { }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.AddRemoveClearMap; }
        }

        public StreamElement this[int index]
        {
            get { return (StreamElement)base.BaseGet(index); }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                base.BaseAdd(index, value);
            }
        }

        new public StreamElement this[string key]
        {
            get { return (StreamElement)BaseGet(key); }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new StreamElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as StreamElement).Stream;
        }
    }
}
