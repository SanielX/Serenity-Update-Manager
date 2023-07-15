using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Serenity
{
    [AddComponentMenu("")]
    internal class SrManagerOrderMinus : SRScript
    {
        internal static int _sharedInt;

        protected override void OnAwake()
        {
            _sharedInt = 0;
        }

        protected internal override void OnPreUpdate()
        {
            if (_sharedInt != 0)
                Assert.IsTrue(_sharedInt == 1);
        }

        protected internal override void OnUpdate()
        {
            _sharedInt = -1;
        }
    }
}
