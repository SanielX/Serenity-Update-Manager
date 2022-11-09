using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace HostGame
{
    [AddComponentMenu("")]
    internal class CLRManagerOrderMinus : CLRScript
    {
        internal static int _sharedInt;

        public override void OnAwake()
        {
            _sharedInt = 0;
        }

        public override void OnPreUpdate()
        {
            if (_sharedInt != 0)
                Assert.IsTrue(_sharedInt == 1);
        }

        public override void OnUpdate()
        {
            _sharedInt = -1;
        }
    }
}
