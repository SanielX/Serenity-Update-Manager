using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
    public class Run
    {
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
        public class BeforeAttribute : Attribute
        {
            public BeforeAttribute(Type type)
            {
                targetType = type;
            }

            public Type targetType;
        }

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
