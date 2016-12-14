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
    /// The list of AccuRev users from <tt>\<prog_name\>.exe.config</tt>.
    /// </summary>
    /*! \code
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <configSections>
            <section name="Users" type="AcUtils.UsersSection, AcUtils, Version=1.4.0.0, Culture=neutral, PublicKeyToken=26470c2daf5c2e2f, processorArchitecture=MSIL" />
            ...
          </configSections>
          <Users>
            <users>
              <add user="mugcot" name="Mugwort, Cottar" />
              <add user="sanpim" name="Sandyman, Pimpernel" />
              <add user="haymer" name="Hayward, Meriadoc" />
              <add user="gruela" name="Grubb, Elanor" />
              ...
            </users>
          </Users>
          <appSettings>
            ...
        UsersSection usersSection = ConfigurationManager.GetSection("Users") as UsersSection;
        UsersCollection usersCol = usersSection.Users;
        \endcode */
    [ConfigurationCollection(typeof(UserElement),
        CollectionType=ConfigurationElementCollectionType.AddRemoveClearMap)]
    [Serializable]
    public sealed class UsersCollection : ConfigurationElementCollection
    {
        private static ConfigurationPropertyCollection _properties;
        static UsersCollection()
        {
            _properties = new ConfigurationPropertyCollection();
        }

        public UsersCollection() { }

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get { return ConfigurationElementCollectionType.AddRemoveClearMap; }
        }

        public UserElement this[int index]
        {
            get { return (UserElement)base.BaseGet(index); }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                base.BaseAdd(index, value);
            }
        }

        new public UserElement this[string key]
        {
            get { return (UserElement)BaseGet(key); }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new UserElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as UserElement).User;
        }
    }
}
