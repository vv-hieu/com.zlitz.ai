using System;
using UnityEngine;

namespace Zlitz.AI
{
    [AttributeUsage(AttributeTargets.Method)]
    public class BehaviourActionOnEndAttribute : PropertyAttribute
    {
        public string id { get; private set; }

        public int priority { get; private set; }

        public BehaviourActionOnEndAttribute(string id, int priority = 0)
        {
            this.id = id;
            this.priority = priority;
        }
    }
}
