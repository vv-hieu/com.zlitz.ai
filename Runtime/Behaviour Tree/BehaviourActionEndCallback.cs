using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Zlitz.AI
{
    public class BehaviourActionEndCallback
    {
        private string m_id;

        public void End(BehaviourTreeEvaluator evaluator)
        {
            if (evaluator != null)
            {
                int           currentPriority = int.MinValue;
                MethodInfo    currentMethod   = null;
                MonoBehaviour currentMono     = null;

                foreach (MonoBehaviour mono in evaluator.GetComponents<MonoBehaviour>().Where(m => m != null && m.isActiveAndEnabled))
                {
                    Type monoType = mono.GetType();
                    foreach (MethodInfo method in monoType.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                    {
                        // Check for valid BehaviourActionOnEndAttribute
                        BehaviourActionOnEndAttribute[] attributes = method.GetCustomAttributes<BehaviourActionOnEndAttribute>().ToArray();
                        if (attributes == null || attributes.Length <= 0)
                        {
                            continue;
                        }
                        BehaviourActionOnEndAttribute attribute = attributes[0];
                        if (attribute.id != m_id)
                        {
                            continue;
                        }

                        // Check syntax
                        if (method.ReturnType != typeof(void))
                        {
                            Debug.LogWarning("BehaviourActionEndCallback: Invalid syntax (" + monoType.Name + "." + method.Name + ")");
                            continue;
                        }
                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters == null || parameters.Length != 1 || parameters[0].ParameterType != typeof(BehaviourTreeEvaluator))
                        {
                            Debug.LogWarning("BehaviourActionEndCallback: Invalid syntax (" + monoType.Name + "." + method.Name + ")");
                            continue;
                        }

                        // Check priority
                        if (currentPriority <= attribute.priority)
                        {
                            currentPriority = attribute.priority;
                            currentMethod = method;
                            currentMono = mono;
                        }
                    }
                }

                if (currentMethod != null && currentMono != null)
                {
                    currentMethod.Invoke(currentMono, new object[] { evaluator });
                }
            }
        }

        public BehaviourActionEndCallback(string id)
        {
            m_id = id;
        }
    }
}