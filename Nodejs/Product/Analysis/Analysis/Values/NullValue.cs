﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
namespace Microsoft.NodejsTools.Analysis.Values {
    [Serializable]
    class NullValue : AnalysisValue {
        private readonly JsAnalyzer _analyzer;

        public NullValue(JsAnalyzer analyzer)
            : base(analyzer._builtinEntry) {
            _analyzer = analyzer;
        }

        public override string Description {
            get {
                return "null";
            }
        }

        public override BuiltinTypeId TypeId {
            get {
                return BuiltinTypeId.Null;
            }
        }

        public override JsMemberType MemberType {
            get {
                return JsMemberType.Null;
            }
        }
    }
}
