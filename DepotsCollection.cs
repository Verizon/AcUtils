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
    /// The list of AccuRev depots from <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    /*! \code
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <configSections>
            <section name="Depots" type="AcUtils.DepotsSection, AcUtils, Version=1.6.1.0, Culture=neutral, PublicKeyToken=26470c2daf5c2e2f, processorArchitecture=MSIL" />
            ...
          </configSections>
          <Depots>
            <depots>
              <add depot="MARS" />
              <add depot="JUPITER" />
              <add depot="NEPTUNE" />
              ...
            </depots>
          </Depots>
          <appSettings>
          ...
          DepotsSection depotsSection = ConfigurationManager.GetSection("Depots") as DepotsSection;
          DepotsCollection depotsCol = depotsSection.Depots;
        \endcode */
    /*! \sa <a href="_active_w_spaces_8cs-example.html">ActiveWSpaces.cs</a>, StreamsCollection */
    [ConfigurationCollection(typeof(DepotElement),
        CollectionType=ConfigurationElementCollectionType.AddRemoveClearMap)]
    [Serializable]
    public sealed class DepotsCollection : ConfigurationElementCollection
    {
        private static ConfigurationPropertyCollection _properties;
        static DepotsCollection()
        {
            _properties = new ConfigurationPropertyCollection();
        }

        public DepotsCollection() { }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.AddRemoveClearMap; }
        }

        public DepotElement this[int index]
        {
            get { return (DepotElement)base.BaseGet(index); }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                base.BaseAdd(index, value);
            }
        }

        new public DepotElement this[string key]
        {
            get { return (DepotElement)BaseGet(key); }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new DepotElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as DepotElement).Depot;
        }
    }
}
