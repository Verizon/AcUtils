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
    /// The list of AccuRev groups from <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    /*! \code
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <configSections>
            <section name="Groups" type="AcUtils.GroupsSection, AcUtils, Version=1.2.0.0, Culture=neutral, PublicKeyToken=26470c2daf5c2e2f, processorArchitecture=MSIL" />
            ...
          </configSections>
          <Groups>
            <groups>
              <add group="DEV" />
              <add group="SYSTEST" />
              <add group="ADMIN" />
              <add group="UAT" />
              ...
            </groups>
          </Groups>
          <appSettings>
            ...
        GroupsSection groupsSection = ConfigurationManager.GetSection("Groups") as GroupsSection;
        GroupsCollection groupsCol = groupsSection.Groups;
        \endcode */
    [ConfigurationCollection(typeof(GroupElement),
        CollectionType=ConfigurationElementCollectionType.AddRemoveClearMap)]
    [Serializable]
    public sealed class GroupsCollection : ConfigurationElementCollection
    {
        private static ConfigurationPropertyCollection _properties;
        static GroupsCollection()
        {
            _properties = new ConfigurationPropertyCollection();
        }

        public GroupsCollection() { }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.AddRemoveClearMap; }
        }

        public GroupElement this[int index]
        {
            get { return (GroupElement)base.BaseGet(index); }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                base.BaseAdd(index, value);
            }
        }

        new public GroupElement this[string key]
        {
            get { return (GroupElement)BaseGet(key); }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new GroupElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as GroupElement).Group;
        }
    }
}
