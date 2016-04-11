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
using System.ComponentModel;

namespace AcUtils
{
    /// <summary>
    /// Support TypeDescriptionProvider attribute on AcUser.
    /// </summary>
    [Serializable]
    class PrncplTypeDescriptor : CustomTypeDescriptor
    {
        public PrncplTypeDescriptor(ICustomTypeDescriptor parent)
            : base(parent)
        {
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            PropertyDescriptorCollection cols = base.GetProperties();
            PropertyDescriptor prncpl = cols["Principal"];
            PropertyDescriptorCollection prncpl_child = prncpl.GetChildProperties();
            PropertyDescriptor[] arr = new PropertyDescriptor[cols.Count + 4];
            cols.CopyTo(arr, 0);
            arr[cols.Count] = new SubPropertyDescriptor(prncpl, prncpl_child["ID"], "Principal_ID");
            arr[cols.Count + 1] = new SubPropertyDescriptor(prncpl, prncpl_child["Name"], "Principal_Name");
            arr[cols.Count + 2] = new SubPropertyDescriptor(prncpl, prncpl_child["Status"], "Principal_Status");
            arr[cols.Count + 3] = new SubPropertyDescriptor(prncpl, prncpl_child["Members"], "Principal_Members");
            PropertyDescriptorCollection newcols = new PropertyDescriptorCollection(arr);
            return newcols;
        }
    }
}
