using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
    public class Run
    {
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
        public class BeforeAttribute : Attribute
        {
            public BeforeAttribute(Type type)
            {
                if (!ComponentHelpers.IsInherited(type, typeof(CLRScript)))
                    throw new System.Exception("Wrong type. Execute Before attribute works only for CLRScripts");

                targetType = type;
            }

            public Type targetType;
        }

        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
        public class AfterAttribute : Attribute
        {
            public AfterAttribute(Type type)
            {
                if (!ComponentHelpers.IsInherited(type, typeof(CLRScript)))
                    throw new System.Exception("Wrong type. Run.After attribute works only for CLRScripts");

                targetType = type;
            }

            public Type targetType;
        }
    }
}
