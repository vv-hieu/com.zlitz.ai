using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Zlitz.AI
{
    public class BehaviourActionEvaluateCallback
    {
        private string m_id;

        public BehaviourTreeNode.State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
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
                        // Check for valid BehaviourActionOnEvaluateAttribute
                        BehaviourActionOnEvaluateAttribute[] attributes = method.GetCustomAttributes<BehaviourActionOnEvaluateAttribute>().ToArray();
                        if (attributes == null || attributes.Length <= 0)
                        {
                            continue;
                        }
                        BehaviourActionOnEvaluateAttribute attribute = attributes[0];
                        if (attribute.id != m_id)
                        {
                            continue;
                        }

                        // Check syntax
                        if (method.ReturnType != typeof(BehaviourTreeNode.State))
                        {
                            Debug.LogWarning("BehviourActionEvaluateCallback: Invalid syntax (" + monoType.Name + "." + method.Name + ")");
                            continue;
                        }
                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters == null || parameters.Length != 2 || parameters[0].ParameterType != typeof(BehaviourTreeEvaluator) || parameters[1].ParameterType != typeof(BehaviourTree.RunContext))
                        {
                            Debug.LogWarning("BehviourActionEvaluateCallback: Invalid syntax (" + monoType.Name + "." + method.Name + ")");
                            continue;
                        }

                        // Check priority
                        if (currentPriority <= attribute.priority)
                        {
                            currentPriority = attribute.priority;
                            currentMethod   = method;
                            currentMono     = mono;
                        }
                    }
                }

                if (currentMethod != null && currentMono != null)
                {
                    return (BehaviourTreeNode.State)currentMethod.Invoke(currentMono, new object[] { evaluator, context });
                }
            }

            return BehaviourTreeNode.State.Success;
        }

        public BehaviourActionEvaluateCallback(string id)
        {
            m_id = id;
        }
    }
}
