using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
    public interface IGOComponent
    {
        public Transform iTransform { get; }
        public GameObject iGameObject { get; }
    }
}
