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
using System.Runtime.Serialization;

namespace AcUtils
{
    /// <summary>
    /// Exception thrown when an AccuRev command fails. The AccuRev program return value is <em>zero (0)</em> on 
    /// success and <em>one (1)</em> on failure unless otherwise noted in the documentation.
    /// </summary>
    /// <remarks>
    /// When using AccuRev command processing functions [AcCommand.runAsync](@ref AcUtils#AcCommand#runAsync) and 
    /// [AcCommand.run](@ref AcUtils#AcCommand#run), ensure the correct logic is used to determine if an 
    /// AcUtilsException should be thrown based on AccuRev's program return value for the command and the version 
    /// of AccuRev in use. To do so, implement [ICmdValidate](@ref AcUtils#ICmdValidate) or override 
    /// [CmdValidate.isValid](@ref AcUtils#CmdValidate#isValid) and pass a reference to the object as the 
    /// [validator](@ref AcUtils#AcCommand) second function argument.
    /// </remarks>
    [Serializable]
    public sealed class AcUtilsException : System.Exception
    {
        public AcUtilsException() : base()
        { }

        public AcUtilsException(string command) : base(command)
        { }

        public AcUtilsException(string command, Exception innerException)
            : base(command, innerException)
        { }

        public AcUtilsException(SerializationInfo serializationInfo, StreamingContext context)
            : base(serializationInfo, context)
        { }

        public override string Message
        {
            get { return base.Message; }
        }
    }
}
