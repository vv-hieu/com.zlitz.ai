using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Zlitz.AI
{
    public class BehaviourActionResultCallback
    {
        private string m_id;

        public void Invoke(BehaviourTreeEvaluator evaluator, BehaviourTreeNode.State result)
        {
            if (evaluator != null)
            {
                List<MethodCallInfo> callInfos = new List<MethodCallInfo>();
                foreach (MonoBehaviour mono in evaluator.GetComponents<MonoBehaviour>().Where(m => m != null && m.isActiveAndEnabled))
                {
                    Type monoType = mono.GetType();
                    foreach (MethodInfo method in monoType.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                    {
                        // Check for valid BehaviourActionCallbackAttribute
                        BehaviourActionCallbackAttribute[] attributes = method.GetCustomAttributes<BehaviourActionCallbackAttribute>().ToArray();
                        if (attributes == null || attributes.Length <= 0)
                        {
                            continue;
                        }
                        BehaviourActionCallbackAttribute attribute = attributes[0];
                        if (attribute.id != m_id)
                        {
                            continue;
                        }

                        // Check syntax
                        if (method.ReturnType != typeof(void))
                        {
                            Debug.LogWarning("BehaviourActionResultCallback: Invalid syntax (" + monoType.Name + "." + method.Name + ")");
                            continue;
                        }
                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters == null || parameters.Length != 2 || parameters[0].ParameterType != typeof(BehaviourTreeEvaluator) || parameters[1].ParameterType != typeof(BehaviourTreeNode.State))
                        {
                            Debug.LogWarning("BehaviourActionResultCallback: Invalid syntax (" + monoType.Name + "." + method.Name + ")");
                            continue;
                        }

                        callInfos.Add(new MethodCallInfo(method, mono, attribute.priority));
                    }
                }

                callInfos.Sort();
                object[] p = new object[] { evaluator, result };
                foreach (MethodCallInfo callInfo in callInfos)
                {
                    callInfo.method.Invoke(callInfo.mono, p);
                }
            }
        }

        public BehaviourActionResultCallback(string id)
        {
            m_id = id;
        }

        private struct MethodCallInfo : IComparable<MethodCallInfo>
        {
            public MethodInfo    method;
            public MonoBehaviour mono;
            public int           priority;

            public MethodCallInfo(MethodInfo method, MonoBehaviour mono, int priority)
            {
                this.method   = method;
                this.mono     = mono;
                this.priority = priority;
            }

            public int CompareTo(MethodCallInfo other)
            {
                return other.priority - priority;
            }
        }
    }
}
