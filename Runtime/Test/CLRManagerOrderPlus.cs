using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace HostGame
{
    [Run.After(typeof(CLRManagerOrderMinus))]
    [AddComponentMenu("")]
    internal class CLRManagerOrderPlus : CLRScript
    {
        public override void OnUpdate()
        {
            Assert.IsTrue(CLRManagerOrderMinus._sharedInt == -1);

            CLRManagerOrderMinus._sharedInt = 1;
        }
    }
}
