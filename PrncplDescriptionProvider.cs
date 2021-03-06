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
using System.ComponentModel;

namespace AcUtils
{
    /// <summary>
    /// Support TypeDescriptionProvider attribute on AcUser.
    /// </summary>
    [Serializable]
    class PrncplDescriptionProvider : TypeDescriptionProvider
    {
        private ICustomTypeDescriptor td;

        public PrncplDescriptionProvider()
           : this(TypeDescriptor.GetProvider(typeof(AcUser)))
        {
        }

        public PrncplDescriptionProvider(TypeDescriptionProvider parent)
            : base(parent)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            if (td == null)
            {
                td = base.GetTypeDescriptor(objectType, instance);
                td = new PrncplTypeDescriptor(td);
            }

            return td;
        }
    }
}
