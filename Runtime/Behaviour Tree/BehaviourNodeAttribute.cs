using System;
using UnityEngine;

namespace Zlitz.AI
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BehaviourNodeAttribute : PropertyAttribute
    {
        public BehaviourNodeAttribute()
        {
        }
    }
}