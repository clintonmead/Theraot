﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.CompilerServices
{
    public partial class RuntimeOps
    {
#if NET20 || NET30 || NET35 || NET40

        /// <summary>
        /// Provides a list of variables, supporting read/write of the values
        /// Exposed via RuntimeVariablesExpression
        /// </summary>
        internal sealed class MergedRuntimeVariables : IRuntimeVariables
        {
            private readonly IRuntimeVariables _first;

            // For reach item, the index into the first or second list
            // Positive values mean the first array, negative means the second
            private readonly int[] _indexes;

            private readonly IRuntimeVariables _second;

            internal MergedRuntimeVariables(IRuntimeVariables first, IRuntimeVariables second, int[] indexes)
            {
                _first = first;
                _second = second;
                _indexes = indexes;
            }

            public int Count => _indexes.Length;

            public object this[int index]
            {
                get
                {
                    index = _indexes[index];
                    return (index >= 0) ? _first[index] : _second[-1 - index];
                }
                set
                {
                    index = _indexes[index];
                    if (index >= 0)
                    {
                        _first[index] = value;
                    }
                    else
                    {
                        _second[-1 - index] = value;
                    }
                }
            }
        }

#endif
    }
}