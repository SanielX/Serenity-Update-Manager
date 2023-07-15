using System;
using UnityEngine;

namespace Serenity
{
    public class Run
    {
        /// <summary>
        /// Same as <see cref="DefaultExecutionOrder"/> but sets it automatically to high value, so that script runs after most other scripts
        /// </summary>
        /// <remarks>
        /// This exists as a shortcut so you don't have to remember exact value to use for "late" scripts
        /// </remarks>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
        public class Late : DefaultExecutionOrder
        {
            /// <param name="offset">Will offset execution order by given amount</param>
            public Late(int offset = 0) : base(10_000 + offset) { }
        }

        /// <summary>
        /// Same as <see cref="DefaultExecutionOrder"/> but sets it automatically to low value so script runs before most others
        /// </summary>
        /// <inheritdoc cref="Late"/>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
        public class Early : DefaultExecutionOrder
        {
            /// <param name="offset">Will offset execution order by given amount</param>
            public Early(int offset = 0) : base(-10_000 + offset) { }
        }

        /// <summary>
        /// Allows to specify that update callbacks on this type must run before some other type
        /// </summary>
        /// <remarks>
        /// Execution order is solved by CLRManager and cached to ScriptableObject. You should be careful to not create invalid dependency loop.
        /// This attribute has any effect only on <see cref="SRScript"/> derived types that use intended virtual methods.
        /// <br></br>
        /// These attributes may work incorrectly if you have both <see cref="BeforeAttribute"/> and <see cref="AfterAttribute"/> on same type
        /// </remarks>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
        public class BeforeAttribute : Attribute
        {
            public BeforeAttribute(Type type)
            {
                targetType = type;
            }

            public Type targetType;
        }

        /// <summary>
        /// Allows to specify that update callbacks on this type must run after some other type
        /// </summary>
        /// <inheritdoc cref="BeforeAttribute"/>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
        public class AfterAttribute : Attribute
        {
            public AfterAttribute(Type type)
            {
                targetType = type;
            }

            public Type targetType;
        }
    }
}
