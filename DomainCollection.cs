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
    /// The list of Active Directory domain \e host-path pairs from <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    /*! \code
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <configSections>
            <section name="activeDir" type="AcUtils.ADSection, AcUtils, Version=1.2.1.0, Culture=neutral, PublicKeyToken=26470c2daf5c2e2f, processorArchitecture=MSIL" />
            ...
          </configSections>
          <activeDir>
            <domains>
              <add host="xyzdc.mycorp.com" path="DC=XYZ,DC=xy,DC=zcorp,DC=com"/>
              <add host="abcdc.mycorp.com" path="DC=ABC,DC=ab,DC=com"/>
              ...
            </domains>
            <properties>
            </properties >
          </activeDir>
          ...
          ADSection adSection = ConfigurationManager.GetSection("activeDir") as ADSection;
          DomainCollection dc = adSection.Domains;
        \endcode */
    /*! \sa PropCollection, <a href="_show_permissions_8cs-example.html">ShowPermissions.cs</a> */
    [ConfigurationCollection(typeof(DomainElement),
        CollectionType=ConfigurationElementCollectionType.AddRemoveClearMap)]
    [Serializable]
    public sealed class DomainCollection : ConfigurationElementCollection
    {
        private static ConfigurationPropertyCollection _properties;
        static DomainCollection()
        {
            _properties = new ConfigurationPropertyCollection();
        }

        public DomainCollection() { }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.AddRemoveClearMap; }
        }

        public DomainElement this[int index]
        {
            get { return (DomainElement)base.BaseGet(index); }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                base.BaseAdd(index, value);
            }
        }

        new public DomainElement this[string key]
        {
            get
            {
                return (DomainElement)BaseGet(key);
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new DomainElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as DomainElement).Host;
        }
    }
}
