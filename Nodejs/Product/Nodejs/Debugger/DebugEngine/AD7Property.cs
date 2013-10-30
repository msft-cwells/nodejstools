/* ****************************************************************************
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
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.NodejsTools.Debugger.DebugEngine {
    // An implementation of IDebugProperty3
    // This interface represents a stack frame property, a program document property, or some other property. 
    // The property is usually the result of an expression evaluation. 
    //
    // The sample engine only supports locals and parameters for functions that have symbols loaded.
    class AD7Property : IDebugProperty3 {
        private readonly NodeEvaluationResult _evaluationResult;
        private readonly AD7StackFrame _frame;

        public AD7Property(AD7StackFrame frame, NodeEvaluationResult evaluationResult) {
            _evaluationResult = evaluationResult;
            _frame = frame;
        }

        // Construct a DEBUG_PROPERTY_INFO representing this local or parameter.
        public DEBUG_PROPERTY_INFO ConstructDebugPropertyInfo(uint radix, enum_DEBUGPROP_INFO_FLAGS dwFields) {
            var propertyInfo = new DEBUG_PROPERTY_INFO();

            if (dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME)) {
                propertyInfo.bstrFullName = _evaluationResult.FullName;
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME;
            }

            if (dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME)) {
                propertyInfo.bstrName = _evaluationResult.Expression;
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;
            }

            if (dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE)) {
                propertyInfo.bstrType = _evaluationResult.TypeName;
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE;
            }

            if (dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE)) {
                if (!string.IsNullOrEmpty(_evaluationResult.ExceptionText)) {
                    propertyInfo.bstrValue = _evaluationResult.ExceptionText;
                } else {
                    var value = radix == 16 ? _evaluationResult.HexValue ?? _evaluationResult.StringValue : _evaluationResult.StringValue;
                    propertyInfo.bstrValue = _evaluationResult.Type.HasFlag(NodeExpressionType.String) ? string.Format("\"{0}\"", value) : value;
                }

                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
            }

            if (dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB)) {
                if (_evaluationResult.Type.HasFlag(NodeExpressionType.ReadOnly)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY;
                }

                if (_evaluationResult.Type.HasFlag(NodeExpressionType.Private)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_PRIVATE;
                }

                if (_evaluationResult.Type.HasFlag(NodeExpressionType.Expandable)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE;
                }

                if (_evaluationResult.Type.HasFlag(NodeExpressionType.String)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING;
                }

                if (_evaluationResult.Type.HasFlag(NodeExpressionType.Boolean)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_BOOLEAN;
                }

                if (_evaluationResult.Type.HasFlag(NodeExpressionType.Property)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_PROPERTY;
                }

                if (_evaluationResult.Type.HasFlag(NodeExpressionType.Function)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_METHOD;
                }
            }

            // Always provide the property so that we can access locals from the automation object.
            propertyInfo.pProperty = this;
            propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;

            return propertyInfo;
        }

        #region IDebugProperty2 Members

        // Enumerates the children of a property. This provides support for dereferencing pointers, displaying members of an array, or fields of a class or struct.
        // The sample debugger only supports pointer dereferencing as children. This means there is only ever one child.
        public int EnumChildren(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, ref Guid guidFilter, enum_DBG_ATTRIB_FLAGS dwAttribFilter, string pszNameFilter, uint dwTimeout,
            out IEnumDebugPropertyInfo2 ppEnum) {
            ppEnum = null;
            var children = _evaluationResult.GetChildren((int) dwTimeout);
            if (children == null) {
                return VSConstants.S_FALSE;
            }

            DEBUG_PROPERTY_INFO[] properties;
            if (children.Length == 0) {
                properties = new[] {new DEBUG_PROPERTY_INFO {dwFields = enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, bstrValue = "No children"}};
            } else {
                properties = new DEBUG_PROPERTY_INFO[children.Length];
                for (int i = 0; i < children.Length; i++) {
                    properties[i] = new AD7Property(_frame, children[i]).ConstructDebugPropertyInfo(dwRadix, dwFields);
                }
            }

            ppEnum = new AD7PropertyEnum(properties);
            return VSConstants.S_OK;
        }

        // Returns the property that describes the most-derived property of a property
        // This is called to support object oriented languages. It allows the debug engine to return an IDebugProperty2 for the most-derived 
        // object in a hierarchy. This engine does not support this.
        public int GetDerivedMostProperty(out IDebugProperty2 ppDerivedMost) {
            throw new Exception("The method or operation is not implemented.");
        }

        // This method exists for the purpose of retrieving information that does not lend itself to being retrieved by calling the IDebugProperty2::GetPropertyInfo 
        // method. This includes information about custom viewers, managed type slots and other information.
        // The sample engine does not support this.
        public int GetExtendedInfo(ref Guid guidExtendedInfo, out object pExtendedInfo) {
            throw new Exception("The method or operation is not implemented.");
        }

        public int GetStringCharLength(out uint pLen) {
            pLen = (uint) _evaluationResult.StringLength;
            return VSConstants.S_OK;
        }

        public int GetStringChars(uint buflen, ushort[] rgString, out uint pceltFetched) {
            pceltFetched = buflen;

            var completion = new AutoResetEvent(false);
            _evaluationResult.Frame.ExecuteText(_evaluationResult.FullName, obj => {
                obj.StringValue.ToCharArray().CopyTo(rgString, 0);
                completion.Set();
            });

            while (!_frame.StackFrame.Thread.Process.HasExited && !completion.WaitOne(100)) {
            }

            if (_frame.StackFrame.Thread.Process.HasExited) {
                return VSConstants.E_FAIL;
            }

            return VSConstants.S_OK;
        }

        public int CreateObjectID() {
            throw new NotImplementedException();
        }

        public int DestroyObjectID() {
            throw new NotImplementedException();
        }

        public int GetCustomViewerCount(out uint pcelt) {
            throw new NotImplementedException();
        }

        public int GetCustomViewerList(uint celtSkip, uint celtRequested, DEBUG_CUSTOM_VIEWER[] rgViewers, out uint pceltFetched) {
            throw new NotImplementedException();
        }

        public int SetValueAsStringWithError(string pszValue, uint dwRadix, uint dwTimeout, out string errorString) {
            errorString = null;
            return SetValueAsString(pszValue, dwRadix, dwTimeout);
        }

        // Returns the memory bytes for a property value.
        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Returns the memory context for a property value.
        public int GetMemoryContext(out IDebugMemoryContext2 ppMemory) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Returns the parent of a property.
        // The sample engine does not support obtaining the parent of properties.
        public int GetParent(out IDebugProperty2 ppParent) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Fills in a DEBUG_PROPERTY_INFO structure that describes a property.
        public int GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, uint dwTimeout, IDebugReference2[] rgpArgs, uint dwArgCount, DEBUG_PROPERTY_INFO[] pPropertyInfo) {
            pPropertyInfo[0] = new DEBUG_PROPERTY_INFO();
            pPropertyInfo[0] = ConstructDebugPropertyInfo(dwRadix, dwFields);
            return VSConstants.S_OK;
        }

        //  Return an IDebugReference2 for this property. An IDebugReference2 can be thought of as a type and an address.
        public int GetReference(out IDebugReference2 ppReference) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Returns the size, in bytes, of the property value.
        public int GetSize(out uint pdwSize) {
            throw new Exception("The method or operation is not implemented.");
        }

        // The debugger will call this when the user tries to edit the property's values
        // We only accept setting values as strings
        public int SetValueAsReference(IDebugReference2[] rgpArgs, uint dwArgCount, IDebugReference2 pValue, uint dwTimeout) {
            throw new Exception("The method or operation is not implemented.");
        }

        // The debugger will call this when the user tries to edit the property's values in one of the debugger windows.
        public int SetValueAsString(string pszValue, uint dwRadix, uint dwTimeout) {
            var completion = new AutoResetEvent(false);
            _evaluationResult.Frame.ExecuteText(_evaluationResult.FullName + " = " + pszValue, obj => completion.Set());

            while (!_frame.StackFrame.Thread.Process.HasExited && !completion.WaitOne(Math.Min((int) dwTimeout, 100))) {
                if (dwTimeout <= 100) {
                    break;
                }
                dwTimeout -= 100;
            }

            if (_frame.StackFrame.Thread.Process.HasExited) {
                return VSConstants.E_FAIL;
            }

            return VSConstants.S_OK;
        }

        #endregion

    }
}
