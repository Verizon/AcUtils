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
    /// The list of AccuRev repository \e server-port pairs from <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    /*! \code
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <configSections>
            <section name="repositories" type="AcUtils.RepoSection, AcUtils, Version=1.2.1.0, Culture=neutral, PublicKeyToken=26470c2daf5c2e2f, processorArchitecture=MSIL" />
            ...
          </configSections>
          <repositories>
            <instance>
              <add server="host1_omitted.com" port="5050" />
              <add server="host2_omitted.com" port="5050" />
              ...
            </instance>
          </repositories>
          ...
          RepoSection repoSection = ConfigurationManager.GetSection("repositories") as RepoSection;
          RepoCollection repoCol = repoSection.Repositories;
        \endcode */
    [ConfigurationCollection(typeof(RepoElement),
        CollectionType=ConfigurationElementCollectionType.AddRemoveClearMap)]
    [Serializable]
    public sealed class RepoCollection : ConfigurationElementCollection
    {
        private static ConfigurationPropertyCollection _properties;
        static RepoCollection()
        {
            _properties = new ConfigurationPropertyCollection();
        }

        public RepoCollection() { }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.AddRemoveClearMap; }
        }

        public RepoElement this[int index]
        {
            get { return (RepoElement)base.BaseGet(index); }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                base.BaseAdd(index, value);
            }
        }

        new public RepoElement this[string key]
        {
            get { return (RepoElement)BaseGet(key); }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new RepoElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as RepoElement).Server;
        }
    }
}
