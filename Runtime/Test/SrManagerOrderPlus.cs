using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Serenity
{
    [Run.After(typeof(SrManagerOrderMinus))]
    [AddComponentMenu("")]
    internal class SrManagerOrderPlus : SRScript
    {
        protected internal override void OnUpdate()
        {
            Assert.IsTrue(SrManagerOrderMinus._sharedInt == -1);

            SrManagerOrderMinus._sharedInt = 1;
        }
    }
}
