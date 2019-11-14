﻿using System.Runtime.CompilerServices;

namespace System.Threading.Tasks
{
    /// <summary>
    ///     Provides methods for creating and manipulating tasks.
    /// </summary>
    /// <remarks>
    ///     TaskEx is a placeholder.
    /// </remarks>
    public static partial class TaskExEx
    {
        /// <summary>
        ///     Creates an awaitable that asynchronously yields back to the current context when awaited.
        /// </summary>
        /// <returns>
        ///     A context that, when awaited, will asynchronously transition back into the current context.
        ///     If SynchronizationContext.Current is non-null, that is treated as the current context.
        ///     Otherwise, TaskScheduler.Current is treated as the current context.
        /// </returns>
        [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
        public static YieldAwaitable Yield()
        {
            return new YieldAwaitable();
        }
    }
}